using System.IO;
using System.IO.MemoryMappedFiles;

namespace glTF
{
    internal static class BinaryReaderExtensions
    {
        public static MemoryMappedViewStream ReadMemoryMappedViewStream(this BinaryReader binaryReader, MemoryMappedFile memoryMappedFile, long size, MemoryMappedFileAccess access)
        {
            var stream = memoryMappedFile.CreateViewStream(binaryReader.BaseStream.Position, size, access);
            binaryReader.BaseStream.Position += size;
            return stream;
        }
    }
}
