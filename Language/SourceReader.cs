using System.Diagnostics;
using System.IO;
using System.Text;

namespace Inu.Language
{
    class SourceReader
    {
        public const char EndOfLine = '\n';

        public static SourceReader Current { get; private set; }
        public static SourcePrinter Printer { private get; set; }

        private SourceReader parent;
        public string FileName { get; private set; }
        private StreamReader stream;
        public int LineNumber { get; private set; } = 0;
        private string currentLine = "";
        private string prevLine;
        private int currentIndex = 1;

        public SourceReader(string fileName)
        {
            FileName = fileName;
            stream = new StreamReader(fileName, Encoding.UTF8);
        }
        public static void OpenFile(string fileName)
        {
            SourceReader sourceReader = new SourceReader(fileName)
            {
                parent = Current
            };
            Current = sourceReader;
        }

        private void Print()
        {
            if (LineNumber > 0 && Printer != null) {
                Printer.AddSourceLine(prevLine);
            }
        }

        public char GetChar()
        {
            if (currentLine == null) { }
            if (currentIndex >= currentLine.Length) {
                if (currentLine.Length == 0 && stream.EndOfStream) {
                    prevLine = currentLine;
                    Print();
                    Current = parent;
                    return '\0';
                }
                prevLine = currentLine;
                currentLine = stream.ReadLine();
                if (currentLine == null) {
                    currentLine = "";
                }
                Print();
                ++LineNumber;
                currentIndex = 0;
                return EndOfLine;
            }
            Debug.Assert(currentLine[currentIndex] != 0);
            return currentLine[currentIndex++];
        }

        public void SkipToEndOfLine()
        {
            currentIndex = currentLine.Length;
        }

        public SourcePosition CurrentPosition => new SourcePosition(FileName, LineNumber);

        public string Directory => Path.GetDirectoryName(FileName);
    }
}
