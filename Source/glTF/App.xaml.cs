using Microsoft.UI.Xaml;
using System;

namespace glTF
{
    public partial class App : Application
    {
        public App()
        {
            this.InitializeComponent();
        }

        protected override void OnLaunched(LaunchActivatedEventArgs e)
        {
            var args = Environment.GetCommandLineArgs();
            if (args.Length == 3)
            {
                switch (args[1])
                {
                    case "Pack":
                        {
                            new PackWindow(args[2]);
                            break;
                        }
                    case "Unpack":
                        {
                            var window = new UnpackWindow(args[2]);
                            window.Activate();
                            break;
                        }
                }
            }
            else
            {
                var window = new MainWindow();
                window.Activate();
            }
        }
    }
}
