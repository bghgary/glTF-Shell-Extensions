using System.IO;

namespace glTF
{
    internal static class StreamExtensions
    {
        public static void Align(this Stream stream, byte pad = 0)
        {
            var count = 3 - ((stream.Position - 1) & 3);
            while (count != 0)
            {
                stream.WriteByte(pad);
                count--;
            }
        }
    }
}
