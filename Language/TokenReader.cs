using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Inu.Language
{
    class TokenReader
    {
        public Token LastToken { get; private set; }
        private readonly Dictionary<SourcePosition, string> errors = new Dictionary<SourcePosition, string>();

        public TokenReader()
        {
            LastToken = new Token(new SourcePosition("", 0),TokenType.ReservedWord, Tokenizer.EndOfFile);
        }

        public Token NextToken()
        {
            return LastToken = Tokenizer.Instance.GetToken();
        }

        public void ShowError(SourcePosition position, string error)
        {
            if (!errors.ContainsKey(position)) {
                string s = $"{position.ToString()}: {error}";
                errors[position] = s;
                Console.Error.WriteLine(s);
            }
        }

        public void ShowSyntaxError(Token token)
        {
            ShowError(token.Position, "Syntax error: " + token.ToString());
        }

        public void ShowSyntaxError()
        {
            ShowSyntaxError(LastToken);
        }

        public void ShowUndefinedError(Token identifier)
        {
            ShowError(identifier.Position, "Undefined: " + identifier.ToString());
        }

        public void ShowMissingIdentifier(SourcePosition position)
        {
            ShowError(position, "Missing identifier.");
        }

        public int ErrorCount => errors.Count;

        public Token AcceptReservedWord(int id)
        {
            if (LastToken.Type != TokenType.ReservedWord || LastToken.Value != id) {
                ShowError(LastToken.Position, "Missing " + Token.ReservedWordFromId(id));
                return LastToken;
            }
            return NextToken();
        }

        //public static string ChangeExt(string sourcePath, string newExt)
        //{
        //    string directory = Path.GetDirectoryName(sourcePath);
        //    string name = Path.GetFileNameWithoutExtension(sourcePath);
        //    return directory +Path. + name + newExt;
        //}
    }
}
