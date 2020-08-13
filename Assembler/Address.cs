using Inu.Language;
using System;
using System.Diagnostics;
using System.IO;

namespace Inu.Assembler
{
    enum AddressType
    {
        Code, Data, SegmentCount,
        Undefined = -1, Const = -2, External = -3
    }

    enum AddressPart
    {
        Word, LowByte, HighByte
    }

    class Address : IComparable<Address>
    {
        public readonly AddressType Type;
        public readonly AddressPart Part;
        public readonly int Value;
        public readonly int? Id;

        public bool Parenthesized { get; set; } = false;
        public static Address Default => new Address(AddressType.Const, 0);

        public Address(AddressType type, int value, int? id = null, AddressPart part = AddressPart.Word)
        {
            if (type == AddressType.External) {
                Debug.Assert(id != null);
            }
            Type = type;
            Value = value;
            Id = id;
            Part = part;
        }

        public Address(int constValue) : this(AddressType.Const, constValue) { }

        public Address(Stream stream)
        {
            Type = (AddressType)(sbyte)stream.ReadByte();
            Value = stream.ReadWord();
            if (Type == AddressType.External) {
                Id = stream.ReadWord();
            }
            if (Type != AddressType.Const) {
                Part = (AddressPart)stream.ReadByte();
            }
        }

        public bool IsUndefined() { return Type == AddressType.Undefined; }

        public bool IsConst() { return Type == AddressType.Const; }

        public bool IsRelocatable() { return Type >= 0 || Type == AddressType.External; }

        public void Write(Stream stream)
        {
            stream.WriteByte((int)Type);
            stream.WriteWord(Value);
            if (Type == AddressType.External) {
                Debug.Assert(Id != null);
                stream.WriteWord(Id.Value);
            }
            if (Type != AddressType.Const) {
                stream.WriteByte((int)Part);
            }
        }

        public Address Add(int offset)
        {
            return new Address(Type, Value +offset, Id, Part);
        }

        public static bool operator ==(Address? a, Address? b)
        {
            if ((a as object) == null) { return (b as object) == null; }
            if ((b as object) == null) { return false; }
            return a.Type == b.Type && a.Value == b.Value;
        }

        public static bool operator !=(Address? a, Address? b)
        {
            return !(a == b);
        }

        public override bool Equals(object? obj)
        {
            return obj is Address address && this == address;
        }

        public override int GetHashCode()
        {
            return Type.GetHashCode() + Value.GetHashCode();
        }

        public int CompareTo(Address address)
        {
            int compare = Type.CompareTo(address.Type);
            if (compare == 0) {
                compare = Value.CompareTo(address.Value);
            }
            return compare;
        }

        public Address? Low()
        {
            return Type == AddressType.Const ? new Address(Type, Value & 0xff) : new Address(Type, Value, Id, AddressPart.LowByte);
        }

        public Address? High()
        {
            return Type == AddressType.Const ? new Address(Type, (Value >> 8) & 0xff) : new Address(Type, Value, Id, AddressPart.HighByte);
        }
    }
}
