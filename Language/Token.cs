using System.Diagnostics;

namespace Inu.Language
{
    enum TokenType
    {
        ReservedWord,
        Identifier,
        NumericValue,
        StringValue,
    }

    class Token
    {
        public const int KeywordMinId = 0x80;
        public const int IdentifierMinId = 0x100;
        public const int StringValueMinId = 0x4000;

        public SourcePosition Position { get; private set; }
        public TokenType Type { get; private set; }
        public int Value { get; private set; }

#if DEBUG
        string asString;
#endif
        public Token(SourcePosition position, TokenType type, int value)
        {
            Position = position;
            Type = type;
            Value = value;
#if DEBUG
            asString = ToString();
#endif
        }

        public bool IsEof() { return Type == TokenType.ReservedWord && Value == 0; }

        public bool IsIdentifier()
        {
            return Type == TokenType.Identifier;
        }

        public static string ReservedWordFromId(int id)
        {
            if (id >= KeywordMinId) {
                return AbstractTokenizer.Instance.KeyWordFromId(id);
            }
            return "\'" + (char)id + "\'";
        }

        public string String()
        {
            Debug.Assert(Type == TokenType.StringValue);
            return AbstractTokenizer.Instance.StringFromId(Value);
        }

        public bool IsReservedWord(int id) { return Type == TokenType.ReservedWord && Value == id; }

        public override string ToString()
        {
            switch(Type) {
                case TokenType. ReservedWord:
                    if (Value == SourceReader.EndOfLine) {
                        return "end of line";
                    }
                    return ReservedWordFromId(Value);
                case TokenType.Identifier:
                    return AbstractTokenizer.Instance.IdentifierFromId(Value);
                case TokenType.NumericValue:
                    return Value.ToString();
                case TokenType.StringValue:
                    return '\"' + String() + '\"';
                default:
                    return "";
            }
        }
    }
}
