using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace FFXIV_TexTools.Helpers
{
    static class UIHelper
    {
        public static void UIInvoke(Action action) {
            App.Current.Dispatcher.Invoke(action);
        }
        public static T UIInvoke<T>(Func<T> func)
        {
            return App.Current.Dispatcher.Invoke<T>(func);
        }
        public static void UIInvokeAsync(Action action)
        {
            App.Current.Dispatcher.InvokeAsync(action);
        }
        static void DoEvent()
        {
            DispatcherOperationCallback exitFrameCallback = new DispatcherOperationCallback(ExitFrame);
            DispatcherFrame nestedFrame = new DispatcherFrame();
            DispatcherOperation exitOperation = Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Background, exitFrameCallback, nestedFrame);
            Dispatcher.PushFrame(nestedFrame);
            if (exitOperation.Status !=
            DispatcherOperationStatus.Completed)
            {
                exitOperation.Abort();
            }
        }
        static object ExitFrame(object state)
        {
            DispatcherFrame frame = state as DispatcherFrame;
            frame.Continue = false;
            return null;
        }
        public static object Lock { get; } = new object();
        public static void RefreshUI()
        {
            DoEvent();
        }
    }
}
