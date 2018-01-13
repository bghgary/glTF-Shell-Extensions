using Ookii.Dialogs.Wpf;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;

namespace glTF
{
    public partial class UnpackWindow : Window
    {
        private string inputFilePath;

        public UnpackWindow(string path)
        {
            InitializeComponent();

            this.inputFilePath = path;

            var inputDirectoryPath = Path.GetDirectoryName(path);
            var inputFileName = Path.GetFileNameWithoutExtension(path);
            this.Folder.Text = GetUniqueDirectoryPath(Path.Combine(inputDirectoryPath, inputFileName));
            this.Folder.SelectAll();
            this.Folder.Focus();
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

        private void Browse_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new VistaFolderBrowserDialog
            {
                ShowNewFolderButton = true,
                SelectedPath = this.Folder.Text,
            };

            if (dialog.ShowDialog(this).Value)
            {
                this.Folder.Text = dialog.SelectedPath;
            }
        }

        private void Unpack_Click(object sender, RoutedEventArgs e)
        {
            var outputDirectoryPath = this.Folder.Text;
            Directory.CreateDirectory(outputDirectoryPath);

            Unpacker.Unpack(this.inputFilePath, outputDirectoryPath);

            if (this.OpenFolder.IsChecked.Value)
            {
                Process.Start(outputDirectoryPath);
            }

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
    }
}
