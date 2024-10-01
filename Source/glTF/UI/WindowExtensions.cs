using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using WinRT.Interop;

namespace glTF
{
    internal static class WindowExtensions
    {
        private static void CenterOnScreen(this Window window, double width, double height)
        {
            var hWnd = WindowNative.GetWindowHandle(window);
            var hWndDesktop = Win32.MonitorFromWindow(hWnd, Win32.MONITOR_DEFAULTTONEAREST);
            var info = new Win32.MONITORINFO();
            info.cbSize = (uint)Marshal.SizeOf(info);
            Win32.GetMonitorInfo(hWndDesktop, ref info);
            var dpi = Win32.GetDpiForWindow(hWnd);
            var scalingFactor = dpi / 96d;
            var w = (int)(width * scalingFactor);
            var h = (int)(height * scalingFactor);
            var cx = (info.rcMonitor.left + info.rcMonitor.right) / 2;
            var cy = (info.rcMonitor.bottom + info.rcMonitor.top) / 2;
            var left = cx - (w / 2);
            var top = cy - (h / 2);
            Win32.SetWindowPos(hWnd, IntPtr.Zero, left, top, w, h, 0);
        }

        public static void ApplySettings(this Window window)
        {
            window.AppWindow.SetIcon(@"UI\Logo.ico");
            window.Title = "glTF Shell Extensions";
            window.SystemBackdrop = new MicaBackdrop();
            window.ExtendsContentIntoTitleBar = true;
            window.CenterOnScreen(800, 400);
        }

        public static async Task ShowErrorDialogAsync(this Window window, string message, string title)
        {
            var dialog = new ContentDialog()
            {
                XamlRoot = window.Content.XamlRoot,
                Title = title,
                Content = message,
                CloseButtonText = "Ok"
            };

            await dialog.ShowAsync();
        }
    }
}
