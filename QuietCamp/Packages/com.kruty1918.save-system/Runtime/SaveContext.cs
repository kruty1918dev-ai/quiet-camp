using System.IO;

namespace Kruty1918.SaveSystem
{
    public sealed class SaveContext : ISaveContext
    {
        public BinaryWriter Writer { get; }
        public BinaryReader Reader { get; }

        internal SaveContext(BinaryWriter writer, BinaryReader reader)
        {
            Writer = writer;
            Reader = reader;
        }
    }
}
