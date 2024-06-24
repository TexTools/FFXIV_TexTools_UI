using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using xivModdingFramework.Models.DataContainers;
using xivModdingFramework.Models.Helpers;

namespace FFXIV_TexTools.Views.Models
{
    /// <summary>
    /// Interaction logic for ModifyPartWindow.xaml
    /// </summary>
    public partial class ModifyPartWindow : Window
    {
        private TTMeshPart Part;
        private Timer _Timer;
        public ModifyPartWindow(TTMeshPart part)
        {
            Part = part;
            InitializeComponent();
            Closing += ModifyPartWindow_Closing;
            NoticeLabel.Content = "";
        }

        private void ModifyPartWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            Owner?.Activate();
        }

        public static void ShowPartModifier(TTMeshPart part, Window owner)
        {
            var wind = new ModifyPartWindow(part);
            wind.Owner = owner;
            wind.ShowDialog();
        }

        private void Done_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void SetNotice(string text)
        {
            if(_Timer != null)
            {
                _Timer.Stop();
                _Timer.Elapsed -= _Timer_Elapsed;
            }

            NoticeLabel.Foreground = Brushes.DarkGreen;
            NoticeLabel.Content = text;
            _Timer = new Timer(3000);
            _Timer.Start();
            _Timer.Elapsed += _Timer_Elapsed;
            
        }

        private void _Timer_Elapsed(object sender, ElapsedEventArgs e)
        {
            Dispatcher.Invoke(() => { 
                NoticeLabel.Content = "";
            });
        }

        private void ClearUv2_Click(object sender, RoutedEventArgs e)
        {
            ModelModifiers.CloneUV2_Part(Part);
            SetNotice("UV2 Cleared");
        }
        private void CopyUv_Click(object sender, RoutedEventArgs e)
        {
            ModelModifiers.CloneUV2_Part(Part);
            SetNotice("UV1 Copied to UV2");
        }
        private void ClearVColor1_Click(object sender, RoutedEventArgs e)
        {
            ModelModifiers.ClearVColor_Part(Part);
            SetNotice("Vertex Color 1 Cleared");
        }
        private void ClearVColor2_Click(object sender, RoutedEventArgs e)
        {
            ModelModifiers.ClearVColor2_Part(Part);
            SetNotice("Vertex Color 2 Cleared");
        }
        private void ClearVAlpha_Click(object sender, RoutedEventArgs e)
        {
            ModelModifiers.ClearVAlpha_Part(Part);
            SetNotice("Vertex Alpha Cleared");
        }
    }
}
