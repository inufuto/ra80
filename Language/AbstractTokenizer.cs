using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Inu.Language
{
    abstract class AbstractTokenizer
    {
        public static AbstractTokenizer Instance { get; private set; }

        private readonly StringTable keyWords;
        private readonly StringTable identifiers = new StringTable(Token.IdentifierMinId);
        private readonly StringTable stringValues = new StringTable(Token.IdentifierMinId);
        private SourcePosition lastPosition;
        public char LastChar { get; private set; } = '\0';
        private readonly Stack<char> lastChars = new Stack<char>();
        private readonly Stack<char> returnedChars = new Stack<char>();

        public AbstractTokenizer(ICollection<string> sortedKeywords)
        {
            keyWords = new StringTable(Token.KeywordMinId, sortedKeywords);
            Debug.Assert(Instance == null);
            Instance = this;
        }

        public string KeyWordFromId(int id)
        {
            return keyWords.FromId(id);
        }

        public string IdentifierFromId(int id)
        {
            return identifiers.FromId(id);
        }
        public string StringFromId(int id)
        {
            return stringValues.FromId(id);
        }

        public void OpenSourceFile(string fileName)
        {
            string modifiedFileName;
            if (SourceReader.Current != null) {
                modifiedFileName = SourceReader.Current.Directory + fileName;
            }
            else {
                modifiedFileName = fileName;
            }
            SourceReader.OpenFile(modifiedFileName);
            if (LastChar != 0) {
                lastChars.Push(LastChar);
            }
            NextChar();
        }

        public Token GetToken()
        {
            SkipSpaces();

            SourcePosition position = lastPosition;
            char c = LastChar;
            if (IsQuotation(c)) {
                return new Token(position, TokenType.StringValue, stringValues.Add(ReadQuotedString()));
            }
            if (IsNumericValueHead(c)) {
                int value = ReadNumericValue();
                return new Token(position, TokenType.NumericValue, value);
            }
            if (IsIdentifierHead(c)) {
                string word = ReadWord();
                int id = keyWords.ToId(word);
                if (id > 0) {
                    return new Token(position, TokenType.ReservedWord, id);
                }
                id = AddIdentifier(word);
                return new Token(position, TokenType.Identifier, id);
            }
            if (IsSequenceHead(c)) {
                char nextChar = NextChar();
                string word = new string(new char[] { c, LastChar });
                var id = keyWords.ToId(word);
                if (id > 0) {
                    NextChar();
                    return new Token(position, TokenType.ReservedWord, id);
                }
                ReturnChar(nextChar);
                //return new Token(position, TokenType.ReservedWord, c);
            }
            NextChar();
            return new Token(position, TokenType.ReservedWord, c);
        }

        public int AddIdentifier(string identifier)
        {
            return identifiers.Add(identifier);
        }

        protected virtual void SkipSpaces()
        {
            while (LastChar > 0 && IsSpace(LastChar)) {
                NextChar();
            }
        }
        protected char NextChar()
        {
            if (returnedChars.Count > 0) {
                LastChar = returnedChars.Pop();
                return LastChar;
            }
            do {
                if (SourceReader.Current == null) {
                    return '\0';
                }
                lastPosition = SourceReader.Current.CurrentPosition;
                LastChar = SourceReader.Current.GetChar();
                if (LastChar == 0 && lastChars.Count > 0) {
                    LastChar = lastChars.Pop();
                }
            } while (LastChar == 0);
            return LastChar;
        }
        protected void ReturnChar(char c)
        {
            returnedChars.Push(LastChar);
            LastChar = c;
        }
        protected void ReturnChars(string s)
        {
            foreach(char c in s.Reverse()) {
                ReturnChar(c);
            }
        }

        private string ReadString(Func<char, bool> isEnd)
        {
            string s = "";
            while (LastChar >= ' ' && !isEnd(LastChar)) {
                s += LastChar;
                NextChar();
            }
            return s;
        }
        private string ReadQuotedString()
        {
            char endChar = LastChar;
            NextChar();
            string s = ReadString((char c) => { return c == endChar; });
            NextChar();
            return s;
        }

        protected string ReadWord(Func<char, bool> function)
        {
            string s = "";
            char c = char.ToUpper(LastChar);
            do {
                s += c;
                c = char.ToUpper(NextChar());
            } while (function(c));
            return s;
        }

        private string ReadWord()
        {
            return ReadWord(IsIdentifierElement);
        }

        protected virtual bool IsSpace(char c)
        {
            return char.IsWhiteSpace(c);
        }
        protected virtual bool IsQuotation(char c)
        {
            return c == '\"';
        }
        protected virtual bool IsNumericValueHead(char c)
        {
            return char.IsDigit(c);
        }
        protected virtual bool IsIdentifierHead(char c)
        {
            char upper = char.ToUpper(c);
            return (upper >= 'A' && upper <= 'Z') || c == '_';
        }
        protected virtual bool IsIdentifierElement(char c)
        {
            return IsIdentifierHead(c) || char.IsDigit(c);
        }
        protected bool IsSequenceHead(char c)
        {
            return false;
        }

        protected abstract int ReadNumericValue();

        protected int ReadDecValue()
        {
            string s = ReadWord(char.IsDigit);
            return int.Parse(s);
        }
    }
}
