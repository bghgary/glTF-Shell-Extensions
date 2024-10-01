using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using System;
using System.Diagnostics;

namespace glTF
{
    public sealed partial class MainWindow : Window
    {
        public MainWindow()
        {
            this.InitializeComponent();
            this.ApplySettings();
        }

        private async void Hyperlink_Click(Hyperlink sender, HyperlinkClickEventArgs args)
        {
            var dialog = new ContentDialog()
            {
                XamlRoot = this.Content.XamlRoot,
                Title = "Restart the File Explorer?",
                Content = "Doing this will close all open File Explorer windows.",
                PrimaryButtonText = "Yes",
                CloseButtonText = "No",
                DefaultButton = ContentDialogButton.Primary,
            };

            if (await dialog.ShowAsync() == ContentDialogResult.Primary)
            {
                KillExplorer();
            }
        }

        private static void KillExplorer()
        {
            var processes = Process.GetProcessesByName("explorer");
            foreach (var process in processes)
            {
                process.Kill();
                process.WaitForExit();
            }
        }
    }
}
