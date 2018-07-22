namespace glTF
{
    internal class Binary
    {
        public const uint Magic = 0x46546C67;
        public const uint Version = 2;
        public const uint HeaderLength = sizeof(uint) + sizeof(uint) + sizeof(int);
        public const uint ChunkHeaderLength = sizeof(uint) + sizeof(uint);
        public const uint ChunkFormatJson = 0x4E4F534A;
        public const uint ChunkFormatBin = 0x004E4942;
    }
}
