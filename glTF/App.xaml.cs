using System.Windows;

namespace glTF
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            var command = e.Args[0];
            switch (command)
            {
                case "Pack":
                {
                    Packer.Pack(e.Args[1]);
                    break;
                }
                case "Unpack":
                {
                    var window = new UnpackWindow(e.Args[1]);
                    window.ShowDialog();
                    break;
                }
            }

            this.Shutdown();
        }
    }
}
