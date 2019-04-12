using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace FFXIV_TexTools
{
    /// <summary>
    /// Window1.xaml 的交互逻辑
    /// </summary>
    public partial class ProgressBarWindow : Window
    {
        public Action CancelAction { get; set; } = () =>
        {
            throw new NotImplementedException();
        };
        public Action UpdateProcessAction { get; set; }
        public ProgressBarWindow()
        {
            InitializeComponent();
            var LoadingMediaElement = this.FindName("LoadingMediaElement") as MediaElement;
            //LoadingMediaElement.Source = new Uri($"file://{Environment.CurrentDirectory}\\lib\\loading.gif");
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            Task.Factory.StartNew(() => {
                if (this.UpdateProcessAction != null)
                {
                    this.UpdateProcessAction();
                    App.Current.Dispatcher.Invoke(this.Close);
                }
                else
                {
                    App.Current.Dispatcher.Invoke(this.Close);
                }
            });
        }
    }
}
