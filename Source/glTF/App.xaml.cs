using System.Windows;

namespace glTF
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            if (e.Args.Length == 2)
            {
                var command = e.Args[0];
                switch (command)
                {
                    case "Pack":
                    {
                        new PackWindow(e.Args[1]);
                        break;
                    }
                    case "Unpack":
                    {
                        var window = new UnpackWindow(e.Args[1]);
                        window.ShowDialog();
                        break;
                    }
                }
            }
            else
            {
                MessageBox.Show(
                    "Right-click on a .gltf or .glb in Windows Explorer to pack or unpack the selected file.",
                    "glTF Shell Extensions");
            }

            this.Shutdown();
        }
    }
}
