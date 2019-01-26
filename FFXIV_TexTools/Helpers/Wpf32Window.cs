using System;
using System.Windows;
using System.Windows.Interop;
using IWin32Window = System.Windows.Forms.IWin32Window;

namespace FFXIV_TexTools.Helpers
{
    public class Wpf32Window : IWin32Window
    {
        public IntPtr Handle { get; }

        public Wpf32Window(Window wpfWindow)
        {
            Handle = new WindowInteropHelper(wpfWindow).Handle;
        }
    }
}