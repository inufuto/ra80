using System.Collections.Generic;

namespace Inu.Language
{
    struct SourcePosition
    {
        public string FileName { get; private set; }
        public int LineNumber { get; private set; }

        public SourcePosition(string fileName, int lineNumber)
        {
            FileName = fileName;
            LineNumber = lineNumber;
        }

        public override bool Equals(object obj)
        {
            if (obj is SourcePosition) {
                SourcePosition sourcePosition = (SourcePosition)obj;
                return FileName.Equals(sourcePosition.FileName) && LineNumber.Equals(LineNumber);
            }
            return base.Equals(obj);
        }

        public override int GetHashCode()
        {
            return FileName.GetHashCode() + LineNumber.GetHashCode();
        }

        public override string ToString()
        {
            return string.Format("{0}({1:d})", FileName, LineNumber);
        }
    }
}
