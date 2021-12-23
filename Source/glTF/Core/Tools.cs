using System.IO;

namespace glTF
{
    internal static class Tools
    {
        public static int GetFileLength(string path)
        {
            return checked((int)new FileInfo(path).Length);
        }

        public static int Align(int value)
        {
            return value + 3 - ((value - 1) & 3);
        }
    }
}
