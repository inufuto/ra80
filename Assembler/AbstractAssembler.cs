using Inu.Language;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace Inu.Assembler
{
    abstract class AbstractAssembler : Language.TokenReader
    {
        public const int Failure = 1;
        public const int Success = 0;

        protected const int MaxErrorCount = 100;
        protected const char EndOfStatement = '|';
        protected const int AutoLabelMinId = 0x8000;
        protected const int ObjVersion = 0x0100;

        private readonly Segment[] segments = new Segment[] { new Segment(AddressType.Code), new Segment(AddressType.Data) };
        public Segment CurrentSegment { get; private set; }
        private readonly Dictionary<int, Symbol> symbols = new Dictionary<int, Symbol>();
        private readonly Dictionary<Address, Address> addressUsages = new Dictionary<Address, Address>();
        public int Pass { get; private set; }

        private bool addressChanged;
        private ListFile listFile = new ListFile();
        private int nextAutoLabelId;
        private readonly Stack<Block> blocks = new Stack<Block>();



        public bool DefineSymbol(int id, Address address)
        {
            Symbol symbol = FindSymbol(id);
            if (symbol == null) {
                symbols[id] = new Symbol(Pass, id, address);
                return true;
            }
            if (symbol.Address == address) { return true; }
            if (symbol.Pass == Pass) {
                // duplicate
                return false;
            }

            addressChanged = true;
            symbol.Address = address;
            return true;
        }

        protected void DefineSymbol(Token identifier, Address address)
        {
            if (!DefineSymbol(identifier.Value, address)) {
                ShowError(identifier.Position, "Multiple definition: " + identifier.ToString());
            }
        }

        protected Symbol FindSymbol(int id)
        {
            if (symbols.TryGetValue(id, out Symbol symbol)) {
                return symbol;
            }
            return null;
        }

        protected Symbol FindSymbol(Token identifier)
        {
            Symbol symbol = FindSymbol(identifier.Value);
            if (symbol != null) {
                if (Pass > 1 && symbol.Address.IsUndefined()) {
                    ShowUndefinedError(identifier);
                }
                return symbol;
            }
            if (Pass > 1) {
                ShowUndefinedError(identifier);
            }
            return null;
        }

        protected Address SymbolAddress(int id)
        {
            Symbol symbol = FindSymbol(id);
            if (symbol != null) {
                return symbol.Address;
            }
            return new Address(AddressType.Undefined, 0);
        }

        protected Address SymbolAddress(Token identifier)
        {
            Address address = SymbolAddress(identifier.Value);
            if (Pass > 1 && address.IsUndefined()) {
                ShowUndefinedError(identifier);
            }
            return address;
        }


        protected void WriteByte(int value)
        {
            CurrentSegment.WriteByte(value);
            listFile.AddByte(value);
        }

        protected abstract byte[] ToBytes(int value);

        protected void WriteWord(Token token, Address value)
        {
            if (value.IsRelocatable() || value.Type == AddressType.External) {
                addressUsages[CurrentSegment.Tail] = value;
            }
            else if (!value.IsConst()) {
                ShowAddressUsageError(token);
            }

            byte[] bytes = ToBytes(value.Value);
            foreach (byte b in bytes) {
                CurrentSegment.WriteByte(b);
                listFile.AddByte(b);
            }
        }

        protected void WriteSpace(int value)
        {
            for (int i = 0; i < value; ++i) {
                CurrentSegment.WriteByte(0);
            }
        }


        protected static void OpenSourceFile(string fileName) { AbstractTokenizer.Instance.OpenSourceFile(fileName); }

        protected Token SkipEndOfStatement()
        {
            bool newLine = false;
            if (LastToken.IsReservedWord(SourceReader.EndOfLine)) {
                listFile.PrintLine();
                newLine = true;
            }
            while (LastToken.IsReservedWord(SourceReader.EndOfLine) || LastToken.IsReservedWord(EndOfStatement)) {
                NextToken();
                if (LastToken.IsReservedWord(SourceReader.EndOfLine)) {
                    listFile.PrintLine();
                    newLine = true;
                }
            }
            if (newLine) {
                listFile.Address = CurrentSegment.Tail;
                listFile.IndentLevel = blocks.Count;
            }
            return LastToken;
        }

        protected void ShowAddressUsageError(Token token)
        {
            if (Pass > 1) {
                ShowError(token.Position, "Cannot use relocatable symbols in expressions: " + token.ToString());
            }
        }

        protected void ShowOutOfRange(Token token, int value)
        {
            ShowError(token.Position, "Out of range: " + value);
        }

        protected void ShowNoStatementError(Token token, string statementName)
        {
            ShowError(token.Position, "No " + statementName + " statement: " + token.ToString());
        }


        protected Address CurrentAddress => CurrentSegment.Tail;


        private Address CharConstant()
        {
            if (LastToken.Type == TokenType.StringValue) {
                Address value = new Address(LastToken.String().ToCharArray()[0]);
                NextToken();
                return value;
            }
            return null;
        }

        private Address ParenthesisExpression()
        {
            if (!LastToken.IsReservedWord('(')) {
                return null;
            }
            NextToken();
            Address value = Expression();
            AcceptReservedWord(')');
            if (value == null) {
                ShowSyntaxError();
                return null;
            }
            value.Parenthesized = true;
            return value;
        }

        private readonly Dictionary<int, Func<int, int>> Monomials = new Dictionary<int, Func<int, int>> {
            { '+', (int value) => {return value; }},
            { '-', (int value) =>{return -value; } },
            { Keyword.Not, (int right) =>{return ~right; } },
            { Keyword.Low, (int right) =>{return right & 0xff; } },
            { Keyword.High, (int right) =>{return (right >> 8) & 0xff; } },
        };
        private Address Monomial()
        {
            Token token = LastToken;
            if (!Monomials.TryGetValue(token.Value, out Func<int, int> function)) {
                return null;
            }
            Token rightToken = NextToken();
            Address right = Factor();
            if (right == null) {
                ShowSyntaxError();
                return null;
            }
            if (!right.IsConst()) {
                ShowAddressUsageError(rightToken);
            }
            int value = function(right.Value);
            return new Address(value);
        }

        private Address Factor()
        {
            TokenType type = LastToken.Type;
            Address value;
            switch (type) {
                case TokenType.NumericValue:
                    value = new Address(LastToken.Value);
                    NextToken();
                    return value;
                case TokenType.Identifier:
                    value = SymbolAddress(LastToken);
                    NextToken();
                    return value;
                case TokenType.ReservedWord:
                    value = Monomial();
                    if (value != null) { return value; }
                    break;
            }
            value = CharConstant();
            if (value != null) { return value; }
            value = ParenthesisExpression();
            if (value != null) { return value; }
            return null;
        }

        private readonly Dictionary<int, Func<int, int, int>>[] Binomials = {
            new Dictionary<int, Func<int, int, int>> {
                { Keyword.Or, (int left, int right) =>{ return left | right; } },
                { Keyword.Xor, (int left, int right)=> { return left ^ right; } },
            },
            new Dictionary<int, Func<int, int, int>> {
                { Keyword.And, (int left, int right) =>{ return left & right; } },
            },
            new Dictionary<int, Func<int, int, int>> {
                { Keyword.Shl, (int left, int right)=> { return left << right; } },
                { Keyword.Shr, (int left, int right)=> { return left >> right; } },
            },
            new Dictionary<int, Func<int, int, int>> {
                { '+', (int left, int right) =>{ return left + right; } },
                { '-', (int left, int right) =>{ return left - right; } },
            },
            new Dictionary<int, Func<int, int, int>> {
                { '*', (int left, int right) =>{ return left* right; } },
                { '/', (int left, int right) =>{ return left / right; } },
                { Keyword.Mod, (int left, int right) =>{ return left % right; } },
            },
            new Dictionary<int, Func<int, int, int>> {
            }
        };

        private Address Binomial(int level)
        {
            Func<Address> factorFunction;
            if (Binomials[level + 1].Count == 0) {
                factorFunction = Factor;
            }
            else {
                factorFunction = () => { return Binomial(level + 1); };
            }

            Token leftToken = LastToken;
            Address left = factorFunction();
            if (left == null) {
                //ShowSyntaxError();
                return null;
            }

        repeat:
            {
                Token operatorToken = LastToken;
                if (operatorToken.Type == TokenType.ReservedWord) {
                    if (Binomials[level].TryGetValue(operatorToken.Value, out Func<int, int, int> operation)) {
                        Token rightToken = NextToken();
                        Address right = factorFunction();
                        if (right == null) {
                            ShowSyntaxError();
                            return null;
                        }
                        if (!right.IsConst()) {
                            ShowAddressUsageError(rightToken);
                        }
                        else if (left.IsConst() || left.IsRelocatable() && (operatorToken.Value == '+' || operatorToken.Value == '-')) {
                            left = new Address(left.Type, operation(left.Value, right.Value));
                        }
                        else {
                            ShowAddressUsageError(leftToken);
                        }
                        goto repeat;
                    }
                }
            }
            return left;
        }
        private Address Binomial() { return Binomial(0); }

        protected Address Expression()
        {
            return Binomial();
        }

        protected int? ByteExpression()
        {
            Token token = LastToken;
            Address value = Expression();
            if (value == null) { return null; }
            if (!value.IsConst()) {
                ShowAddressUsageError(token);
            }
            return value.Value;
        }


        private void IncludeDirective()
        {
            Token token = NextToken();
            if (token.Type == TokenType.StringValue) {
                string fileName = token.String();
                if (File.Exists(fileName)) {
                    OpenSourceFile(fileName);
                }
                else {
                    ShowError(token.Position, "File not found: " + fileName);
                }
                NextToken();
            }
            else {
                ShowError(token.Position, "Missing filename.");
            }
        }
        private void SegmentDirective(AddressType type)
        {
            CurrentSegment = segments[(int)type];
            listFile.Address = CurrentSegment.Tail;
            NextToken();
        }
        private void PublicDirective()
        {
            do {
                Token token = NextToken();
                if (token.Type == TokenType.Identifier) {
                    Symbol symbol = FindSymbol(LastToken);
                    if (symbol != null) {
                        symbol.Public = true;
                    }
                    token = NextToken();
                }
                else {
                    ShowMissingIdentifier(token.Position);
                }
            } while (LastToken.IsReservedWord(','));
        }
        private void ExternDirective()
        {
            do {
                Token token = NextToken();
                if (token.Type == TokenType.Identifier) {
                    var label = token;
                    token = NextToken();
                    DefineSymbol(label, new Address(AddressType.External, label.Value));
                }
                else {
                    ShowMissingIdentifier(token.Position);
                }
            } while (LastToken.IsReservedWord(','));
        }
        private bool ByteStorageOperand()
        {
            Token token = LastToken;
            if (token.Type == TokenType.StringValue) {
                string s = token.String();
                foreach (char c in s) {
                    WriteByte(c);
                }
                NextToken();
                return true;
            }
            int? value = ByteExpression();
            if (value != null) {
                WriteByte(value.Value);
                return true;
            }
            return false;
        }
        private bool WordStorageOperand()
        {
            Token token = LastToken;
            Address value = Expression();
            if (value == null) { return false; }
            WriteWord(token, value);
            return true;
        }
        private bool SpaceStorageOperand()
        {
            Token token = LastToken;
            Address value = Expression();
            if (value == null) { return false; }
            if (!value.IsConst()) {
                ShowAddressUsageError(token);
            }
            WriteSpace(value.Value);
            return true;
        }

        private static readonly Dictionary<int, Func<AbstractAssembler, bool>> StorageDirectives = new Dictionary<int, Func<AbstractAssembler, bool>>
        {
            { Keyword.DefB, (AbstractAssembler t)=>t.ByteStorageOperand()},
            { Keyword.DefW, (AbstractAssembler t)=>t.WordStorageOperand()},
            { Keyword.DefS, (AbstractAssembler t)=>t.SpaceStorageOperand()},
            { Keyword.Db, (AbstractAssembler t)=>t.ByteStorageOperand()},
            { Keyword.Dw, (AbstractAssembler t)=>t.WordStorageOperand()},
            { Keyword.Ds, (AbstractAssembler t)=>t.SpaceStorageOperand()},
        };

        protected AbstractAssembler()
        {
            CurrentSegment = segments[0];
        }

        private bool StorageDirective()
        {
            if (StorageDirectives.TryGetValue(LastToken.Value, out Func<AbstractAssembler, bool> function)) {
                do {
                    NextToken();
                    if (!function(this)) {
                        ShowSyntaxError();
                    }
                } while (LastToken.IsReservedWord(','));
                return true;
            }
            return false;
        }
        private void EquDirective(Token label)
        {
            NextToken();
            Address value = Expression();
            if (value == null) {
                ShowSyntaxError();
                return;
            }
            DefineSymbol(label, value);
        }
        private bool AfterLabel(Token label)
        {
            switch (LastToken.Type) {
                case TokenType.ReservedWord:
                    switch (LastToken.Value) {
                        case Keyword.Equ:
                            EquDirective(label);
                            return true;
                        default:
                            Address address = CurrentAddress;
                            if (StorageDirective()) {
                                DefineSymbol(label, address);
                                return true;
                            }
                            break;
                    }
                    break;
            }
            return false;
        }
        private void Label()
        {
            Token identifier = LastToken;
            Token token = NextToken();
            if (token.IsReservedWord(':')) {
                var address = CurrentAddress;
                DefineSymbol(identifier, address);
                NextToken();
            }
            else {
                if (AfterLabel(identifier)) { return; }
                ShowSyntaxError(identifier);
            }
        }
        protected bool Directive()
        {
            Token token = LastToken;
            switch (token.Value) {
                case Keyword.Include:
                    IncludeDirective();
                    return true; ;
                case Keyword.CSeg:
                    SegmentDirective(AddressType.Code);
                    return true; ;
                case Keyword.DSeg:
                    SegmentDirective(AddressType.Data);
                    return true; ;
                case Keyword.Public:
                    PublicDirective();
                    return true; ;
                case Keyword.Extrn:
                case Keyword.Ext:
                    ExternDirective();
                    return true; ;
            }
            if (StorageDirective()) {
                return true; ;
            }
            return false;
        }


        protected static bool IsRelativeOffsetInRange(int offset) { return offset >= -128 && offset <= 128; }
        protected int RelativeOffset(Address address)
        {
            const int InstructionLength = 2;
            return address.Value - (CurrentAddress.Value + InstructionLength);
        }
        protected bool RelativeOffset(out Address address, out int offset)
        {
            offset = 0;
            Token operand = LastToken;
            address = Expression();
            if (address == null) {
                ShowSyntaxError();
                return false;
            }
            switch (address.Type) {
                case AddressType.Undefined:
                    return false;
                case AddressType.Const:
                case AddressType.External:
                    ShowAddressUsageError(operand);
                    return false;
                default:
                    if (address.Type == CurrentSegment.Type) {
                        offset = RelativeOffset(address);
                    }
                    else {
                        ShowAddressUsageError(operand);
                        return false;
                    }
                    break;
            }
            return IsRelativeOffsetInRange(offset);
        }
        protected abstract bool Instruction();
        protected abstract string ObjExt { get; }


        protected int AutoLabel()
        {
            int id = nextAutoLabelId++;
            //DefineSymbol(id, Address(Address::Undefined, 0));
            return id++;
        }

        protected Block LastBlock()
        {
            listFile.IndentLevel = blocks.Count - 1;
            return blocks.Count > 0 ? blocks.Peek() : null;
        }

        protected IfBlock NewIfBlock()
        {
            IfBlock block = new IfBlock(AutoLabel(), AutoLabel());
            blocks.Push(block);
            return block;
        }

        protected WhileBlock NewWhileBlock()
        {
            WhileBlock block = new WhileBlock(AutoLabel(), AutoLabel(), AutoLabel());
            blocks.Push(block);
            return block;
        }

        protected void EndBlock()
        {
            Debug.Assert(blocks.Count > 0);
            blocks.Pop();
            listFile.IndentLevel = blocks.Count;
        }

        private void SaveObj(string fileName)
        {
            Stream stream = new FileStream(fileName, FileMode.Create, FileAccess.Write);
            stream.WriteWord(ObjVersion);
            foreach (Segment segment in segments) {
                segment.Write(stream);
            }

            ISet<int> ids = new HashSet<int>();
            IList<Symbol> publicSymbols = new List<Symbol>();
            foreach (Symbol symbol in symbols.Values) {
                if (symbol.Public) {
                    publicSymbols.Add(symbol);
                    ids.Add(symbol.Id);
                };
            }
            foreach (Address address in addressUsages.Values) {
                if (address.Type == AddressType.External) {
                    ids.Add(address.Value);
                }
            }

            stream.WriteWord(ids.Count);
            foreach (int id in ids) {
                stream.WriteWord(id);
                stream.WriteString(AbstractTokenizer.Instance.IdentifierFromId(id));
            }

            stream.WriteWord(publicSymbols.Count);
            foreach (var symbol in publicSymbols) {
                symbol.Write(stream);
            }

            stream.WriteWord(addressUsages.Count);
            foreach (var pair in addressUsages) {
                pair.Key.Write(stream);
                pair.Value.Write(stream);
            }
        }
        private void Assemble()
        {
            while (NextToken().IsReservedWord('\n')) ;

            Token token;
            while (!(token = LastToken).IsEof() && ErrorCount < MaxErrorCount) {
                switch (token.Type) {
                    case TokenType.ReservedWord:
                        if (!Directive() && !Instruction()) {
                            if (LastToken.IsEof()) { break; }
                            ShowSyntaxError();
                            NextToken();
                        }
                        break;
                    case TokenType.Identifier:
                        Label();
                        break;
                    default:
                        ShowSyntaxError();
                        NextToken();
                        break;
                }
                SkipEndOfStatement();
            }
        }
        private int Assemble(string sourceName, string objName, string listName)
        {
            SourceReader.Printer = listFile;

            for (Pass = 1; (Pass <= 2 || addressChanged) && ErrorCount <= 0; ++Pass) {
                Console.Out.WriteLine("Pass " + Pass);
                addressChanged = false;
                listFile.Clear();
                addressUsages.Clear();
                nextAutoLabelId = AutoLabelMinId;
                try {
                    OpenSourceFile(sourceName);
                }
                catch (IOException) {
                    Console.Error.WriteLine("Cannot open file: " + sourceName);
                    return Failure;
                }
                foreach (var segment in segments) {
                    segment.Clear();
                }
                listFile.Address = CurrentSegment.Tail;
                listFile.Open(listName);
                Assemble();
                listFile.Close();
            }
            if (ErrorCount > 0) { return Failure; }

            SaveObj(objName);
            return Success;
        }

        public int Main(string[] args)
        {
            if (args.Length <= 0) {
                Console.Error.WriteLine("No source file.");
                return Failure;
            }

            var sourceName = Path.GetFullPath(args[0]);
            var objName = Path.ChangeExtension(sourceName, ObjExt);
            var listName = Path.ChangeExtension(sourceName, ListFile.Ext);
            return Assemble(sourceName, objName, listName);
        }
    }
}
