using Inu.Language;
using System.IO;

namespace Inu.Assembler
{
    enum AddressType
    {
        Code, Data, Count,
        Undefined = -1, Const = -2, External = -3
    }

    class Address
    {
        public AddressType Type { get; private set; }
        public int Value { get; private set; }
        public bool Parenthesized { get; set; } = false;

        public Address(AddressType type, int value)
        {
            Type = type;
            Value = value;
        }

        public Address(int constValue) : this(AddressType.Const, constValue) { }

        public bool IsUndefined() { return Type == AddressType.Undefined; }

        public bool IsConst() { return Type == AddressType.Const; }

        public bool IsRelocatable() { return Type >= 0 || Type == AddressType.External; }

        public void Write(Stream stream)
        {
            stream.WriteByte((int)Type);
            stream.WriteWord(Value);
        }

        public void Read(Stream stream)
        {
            Type = (AddressType)stream.ReadByte();
            Value = stream.ReadWord();
        }

        public void AddOffset(int offset) { Value += offset; }

        public static bool operator ==(Address a, Address b)
        {
            if (((object)a) == null) { return ((object)b) == null; }
            if (((object)b) == null) { return false; }
            return a.Type == b.Type && a.Value == b.Value;
        }

        public static bool operator !=(Address a, Address b)
        {
            return !(a == b);
        }

        public override bool Equals(object obj)
        {
            if (obj is Address) {
                return this == (Address)obj;
            }
            return base.Equals(obj);
        }

        public override int GetHashCode()
        {
            return Type.GetHashCode() + Value.GetHashCode();
        }
    }
}
