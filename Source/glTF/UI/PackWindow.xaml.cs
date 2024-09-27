using Microsoft.UI.Xaml;
using System;
using System.IO;

namespace glTF
{
    public sealed partial class PackWindow : Window
    {
        public PackWindow(string inputFilePath)
        {
            this.InitializeComponent();
            this.ApplySettings();

            this.Pack(inputFilePath);
        }

        private async void Pack(string inputFilePath)
        {
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
                await this.ShowErrorDialogAsync(ex.Message, "Pack Error");
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
