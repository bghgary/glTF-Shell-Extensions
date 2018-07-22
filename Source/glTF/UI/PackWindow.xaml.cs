using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;

namespace glTF
{
    public partial class PackWindow : Window
    {
        public PackWindow(string inputFilePath)
        {
            InitializeComponent();

            var tempFilePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.glb");

            try
            {
                Packer.Pack(inputFilePath, tempFilePath);

                var inputDirectoryPath = Path.GetDirectoryName(inputFilePath);
                var inputFileName = Path.GetFileNameWithoutExtension(inputFilePath);
                var outputFilePath = GetUniqueFilePath(inputDirectoryPath, inputFileName, ".glb");
                File.Move(tempFilePath, outputFilePath);

                SelectFileInExplorer(outputFilePath, true);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Pack Error");
            }

            File.Delete(tempFilePath);

            this.Close();
        }

        private static string GetUniqueFilePath(string directoryPath, string baseFileName, string fileExtension)
        {
            var filePath = Path.Combine(directoryPath, $"{baseFileName}{fileExtension}");

            for (var index = 2; index < 100; index++)
            {
                if (!File.Exists(filePath))
                {
                    break;
                }

                filePath = Path.Combine(directoryPath, $"{baseFileName} ({index}){fileExtension}");
            }

            return filePath;
        }

        private static void SelectFileInExplorer(string path, bool edit)
        {
            var pidlDirectory = IntPtr.Zero;
            var pidlFile = IntPtr.Zero;
            try
            {
                pidlDirectory = NativeMethods.ILCreateFromPath(Path.GetDirectoryName(path));
                pidlFile = NativeMethods.ILCreateFromPath(path);
                NativeMethods.SHOpenFolderAndSelectItems(pidlDirectory, 1, new[] { pidlFile }, edit ? NativeMethods.OFASI_EDIT : 0);
            }
            catch
            {
                NativeMethods.ILFree(pidlFile);
                NativeMethods.ILFree(pidlDirectory);
            }
        }

        private static class NativeMethods
        {
            public const int OFASI_EDIT = 0x0001;

            [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
            public static extern IntPtr ILCreateFromPath([In] string pszPath);

            [DllImport("shell32.dll")]
            public static extern void ILFree([In] IntPtr pidl);

            [DllImport("shell32.dll", EntryPoint = "SHOpenFolderAndSelectItems")]
            public static extern void SHOpenFolderAndSelectItems([In] IntPtr pidlFolder, uint cidl, [In, Optional, MarshalAs(UnmanagedType.LPArray)] IntPtr[] apidl, int dwFlags);
        }
    }
}
