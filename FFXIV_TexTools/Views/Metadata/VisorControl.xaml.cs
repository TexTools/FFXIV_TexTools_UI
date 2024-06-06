using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using xivModdingFramework.Mods.FileTypes;

namespace FFXIV_TexTools.Views.Metadata
{
    /// <summary>
    /// Interaction logic for VisorControl.xaml
    /// </summary>
    public partial class VisorControl : UserControl
    {
        private ItemMetadata _metadata;
        public event Action FileChanged;
        public VisorControl()
        {
            InitializeComponent();
        }

        public async Task SetMetadata(ItemMetadata m)
        {
            _metadata = m;
            if (m == null || m.GmpEntry == null) return;

            EnabledBox.IsChecked = m.GmpEntry.Enabled;
            AnimatedBox.SelectedIndex = m.GmpEntry.Animated ? 1 : 0;

            RotationABox.Text = m.GmpEntry.RotationA.ToString();
            RotationBBox.Text = m.GmpEntry.RotationB.ToString();
            RotationCBox.Text = m.GmpEntry.RotationC.ToString();

            UnknownHighBox.Text = m.GmpEntry.Byte4High.ToString();
            UnknownLowBox.Text = m.GmpEntry.Byte4Low.ToString();

        }

        private static readonly Regex _nonNumericRegex = new Regex("[^0-9]");
        private void ValidateNumericInput(object sender, TextCompositionEventArgs e)
        {
            if(_nonNumericRegex.IsMatch(e.Text))
            {
                e.Handled = true;
            }
        }

        private void AnimatedBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _metadata.GmpEntry.Animated = AnimatedBox.SelectedIndex == 1 ? true : false;
            FileChanged?.Invoke();
        }

        private void RotationABox_SelectionChanged(object sender, RoutedEventArgs e)
        {
            ushort v;
            var ok = ushort.TryParse(RotationABox.Text, out v);
            if (!ok)
            {
                RotationABox.Text = "0";
                return;
            }
            if (v >= 1024)
            {
                RotationABox.Text = "1023";
                return;
            }

            _metadata.GmpEntry.RotationA = v;
            FileChanged?.Invoke();
        }

        private void RotationBBox_SelectionChanged(object sender, RoutedEventArgs e)
        {
            ushort v;
            var ok = ushort.TryParse(RotationBBox.Text, out v);
            if (!ok)
            {
                RotationBBox.Text = "0";
                return;
            }
            if (v >= 1024)
            {
                RotationBBox.Text = "1023";
                return;
            }
            _metadata.GmpEntry.RotationB = v;
            FileChanged?.Invoke();
        }

        private void RotationCBox_SelectionChanged(object sender, RoutedEventArgs e)
        {
            ushort v;
            var ok = ushort.TryParse(RotationCBox.Text, out v);
            if (!ok) return;
            if (v >= 1024)
            {
                RotationCBox.Text = "1023";
                return;
            }
            _metadata.GmpEntry.RotationC = v;
            FileChanged?.Invoke();
        }

        private void UnknownHighBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            byte v;
            var ok = byte.TryParse(UnknownHighBox.Text, out v);
            if (!ok)
            {
                UnknownHighBox.Text = "15";
                return;
            }
            if (v >= 16)
            {
                UnknownHighBox.Text = "15";
                return;
            }
            _metadata.GmpEntry.Byte4High = v;
            FileChanged?.Invoke();
        }

        private void UnknownLowBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            byte v;
            var ok = byte.TryParse(UnknownLowBox.Text, out v);
            if (!ok)
            {
                UnknownHighBox.Text = "15";
                return;
            }
            if (v >= 16)
            {
                UnknownLowBox.Text = "15";
                return;
            }
            _metadata.GmpEntry.Byte4Low = v;
            FileChanged?.Invoke();
        }

        private void ToggleAllElements(bool enabled)
        {
            UnknownHighBox.IsEnabled = enabled;
            UnknownLowBox.IsEnabled = enabled;
            RotationABox.IsEnabled = enabled;
            RotationBBox.IsEnabled = enabled;
            RotationCBox.IsEnabled = enabled;
            AnimatedBox.IsEnabled = enabled;
        }

        private void EnabledBox_Checked(object sender, RoutedEventArgs e)
        {
            _metadata.GmpEntry.Enabled = true;
            FileChanged?.Invoke();
            ToggleAllElements(true);
        }

        private void EnabledBox_Unchecked(object sender, RoutedEventArgs e)
        {
            _metadata.GmpEntry.Enabled = false;
            FileChanged?.Invoke();
            ToggleAllElements(false);
        }
    }
}
