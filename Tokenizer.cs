using Inu.Language;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace Inu.Assembler.Z80
{
    class Tokenizer : AbstractTokenizer
    {
        private static readonly List<string> KeyWords = new List<string>
        {
            "A",
            "ADC",
            "ADD",
            "AF",
            "AF'",
            "AND",
            "B",
            "BC",
            "BIT",
            "C",
            "CALL",
            "CCF",
            "CP",
            "CPD",
            "CPDR",
            "CPI",
            "CPIR",
            "CPL",
            "CSEG",
            "D",
            "DAA",
            "DB",
            "DE",
            "DEC",
            "DEFB",
            "DEFS",
            "DEFW",
            "DI",
            "DJNZ",
            "DO",
            "DS",
            "DSEG",
            "DW",
            "DWNZ",
            "E",
            "EI",
            "ELSE",
            "ELSEIF",
            "ENDIF",
            "EQU",
            "EX",
            "EXT",
            "EXTRN",
            "EXX",
            "H",
            "HALT",
            "HIGH",
            "HL",
            "I",
            "IF",
            "IM",
            "IN",
            "INC",
            "INCLUDE",
            "IND",
            "INDR",
            "INI",
            "INIR",
            "IX",
            "IY",
            "JP",
            "JR",
            "L",
            "LD",
            "LDD",
            "LDDR",
            "LDI",
            "LDIR",
            "LOW",
            "M",
            "MOD",
            "N",
            "NC",
            "NEG",
            "NOP",
            "NOT",
            "NZ",
            "OR",
            "OUT",
            "OUTD",
            "OUTDR",
            "OUTI",
            "OUTIR",
            "P",
            "PE",
            "PO",
            "POP",
            "PUBLIC",
            "PUSH",
            "R",
            "RES",
            "RET",
            "RETI",
            "RETN",
            "RL",
            "RLA",
            "RLC",
            "RLCA",
            "RLD",
            "RR",
            "RRA",
            "RRC",
            "RRCA",
            "RRD",
            "RST",
            "SBC",
            "SCF",
            "SET",
            "SHL",
            "SHR",
            "SLA",
            "SP",
            "SRA",
            "SRL",
            "SUB",
            "WEND",
            "WHILE",
            "XOR",
            "Z",
        };

        private const char Comment = ';';

        public Tokenizer() : base(KeyWords) { }

        protected override void SkipSpaces()
        {
        repeat:
            base.SkipSpaces();
            if (LastChar == Comment) {
                SourceReader.Current.SkipToEndOfLine();
                NextChar();
                goto repeat;
            }
        }
        protected override bool IsSpace(char c)
        {
            return c != SourceReader.EndOfLine && base.IsSpace(c);
        }
        protected override bool IsQuotation(char c)
        {
            return "\'\"".Contains(c);
        }
        protected override bool IsIdentifierHead(char c)
        {
            return base.IsIdentifierHead(c) || "$.?@".Contains(c);
        }
        protected override bool IsIdentifierElement(char c)
        {
            return base.IsIdentifierElement(c) || c == '\'';
        }

        protected override int ReadNumericValue()
        {
            if (ReadHexValue(out int value)) {
                return value;
            }
            return ReadDecValue();
        }

        private bool ReadHexValue(out int value)
        {
            value = 0;
            string s = ReadWord((c) => { return char.IsDigit(c) || c >= 'A' && c <= 'F'; });
            char c = char.ToUpper(LastChar);
            if (c == 'H') {
                NextChar();
                value = int.Parse(s, NumberStyles.AllowHexSpecifier);
                return true;
            }
            ReturnChars(s);
            return false;
        }
    }
}
