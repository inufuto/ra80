using Inu.Language;
using System.IO;

namespace Inu.Assembler
{
    class Symbol
    {
        public int Pass { get; private set; }
        public int Id { get; private set; }
#if DEBUG
        private string? name;
#endif
        public Address Address { get; set; }
        public bool Public { get; set; } = false;

        public Symbol(int pass, int id, Address address)
        {
            Pass = pass;
            Id = id;
            Address = address;
#if DEBUG
            if (id != 0 && id < 0x8000) {
                name = Tokenizer.Instance.IdentifierFromId(id);
            }
#endif   
        }

        public void Write(Stream stream)
        {
            stream.WriteWord(Id);
            Address.Write(stream);
        }
    }
}
