using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using System;
using System.Diagnostics;
using System.IO;
using Windows.Storage.Pickers;

namespace glTF
{
    public sealed partial class UnpackWindow : Window
    {
        private readonly string inputFilePath;

        public UnpackWindow(string inputFilePath)
        {
            this.InitializeComponent();
            this.ApplySettings();

            this.inputFilePath = inputFilePath;
            var inputDirectoryPath = Path.GetDirectoryName(this.inputFilePath);
            var inputFileName = Path.GetFileNameWithoutExtension(this.inputFilePath);
            this.Folder.Text = GetUniqueDirectoryPath(Path.Combine(inputDirectoryPath, inputFileName));
            this.Folder_TextChanged(this, null);
            this.Folder.SelectAll();
        }

        private static string GetUniqueDirectoryPath(string baseDirectoryPath)
        {
            var directoryPath = baseDirectoryPath;

            for (var index = 2; index < 100; index++)
            {
                if (!Directory.Exists(directoryPath))
                {
                    break;
                }

                directoryPath = $"{baseDirectoryPath} ({index})";
            }

            return directoryPath;
        }

        private async void Browse_Click(object sender, RoutedEventArgs e)
        {
            var folderPicker = new FolderPicker();

            var windowHandle = WinRT.Interop.WindowNative.GetWindowHandle(this);
            WinRT.Interop.InitializeWithWindow.Initialize(folderPicker, windowHandle);

            var folder = await folderPicker.PickSingleFolderAsync();
            if (folder != null)
            {
                this.Folder.Text = folder.Path;
            }
        }

        private async void Unpack_Click(object sender, RoutedEventArgs e)
        {
            var tempDirectoryPath = Path.Combine(Path.GetTempPath(), $"glTF.{Guid.NewGuid()}");

            Directory.CreateDirectory(tempDirectoryPath);

            try
            {
                Unpacker.Unpack(this.inputFilePath, tempDirectoryPath, this.UnpackImages.IsChecked.Value);

                var outputDirectoryPath = this.Folder.Text;
                Directory.CreateDirectory(outputDirectoryPath);

                foreach (var filePath in Directory.EnumerateFiles(tempDirectoryPath))
                {
                    var outputFilePath = Path.Combine(outputDirectoryPath, Path.GetFileName(filePath));

                    if (File.Exists(outputFilePath))
                    {
                        File.Delete(outputFilePath);
                    }

                    File.Move(filePath, outputFilePath);
                }

                if (this.OpenFolder.IsChecked.Value)
                {
                    Process.Start(new ProcessStartInfo(outputDirectoryPath) { UseShellExecute = true });
                }
            }
            catch (Exception ex)
            {
                await this.ShowErrorDialogAsync(ex.Message, "Unpack Error");
            }

            Directory.Delete(tempDirectoryPath, true);
            this.Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void Folder_TextChanged(object sender, TextChangedEventArgs e)
        {
            this.Warning.Visibility = Directory.Exists(this.Folder.Text) ? Visibility.Visible : Visibility.Collapsed;
        }

        private void Folder_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == Windows.System.VirtualKey.Enter)
            {
                this.Unpack_Click(sender, e);
            }
        }
    }
}
