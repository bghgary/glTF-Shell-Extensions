using System;
using System.IO;
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
    }
}
