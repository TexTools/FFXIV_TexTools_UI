using FFXIV_TexTools.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace FFXIV_TexTools.Views.Controls
{
    /// <summary>
    /// Interaction logic for RawFloatValueDisplay.xaml
    /// </summary>
    public partial class RawFloatValueDisplay : Window
    {
        public float Red;
        public float Green;
        public float Blue;
        public float Alpha;
        public RawFloatValueDisplay(float red, float green, float blue, string name = null)
        {
            InitializeComponent();

            if (name != null)
            {
                this.Title = "Raw ".L() + name.L() + " Value Editor".L();

            }

            RedBox.Text = red.ToString();
            GreenBox.Text = green.ToString();
            BlueBox.Text = blue.ToString();
            AlphaLabel.Visibility = Visibility.Collapsed;
            AlphaBox.Visibility = Visibility.Collapsed;

        }
        public RawFloatValueDisplay(float red, float green, float blue, float alpha, string name = null)
        {
            InitializeComponent();

            if (name != null)
            {
                this.Title = "Raw ".L() + name.L() + " Value Editor".L();

            }
            RedBox.Text = red.ToString();
            GreenBox.Text = green.ToString();
            BlueBox.Text = blue.ToString();
            AlphaBox.Text = alpha.ToString();

        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Red = float.Parse(RedBox.Text);
                Green = float.Parse(GreenBox.Text);
                Blue = float.Parse(BlueBox.Text);
                Alpha = float.Parse(AlphaBox.Text);

                DialogResult = true;
            } catch
            {
                FlexibleMessageBox.Show("Unable to set values.  Some values are invalid.".L(), "Invalid Values Error".L(), System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Warning);
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        public static (float Red, float Green, float Blue) ShowEditor(float red, float green, float blue, string name = null)
        {
            var wind = new RawFloatValueDisplay(red, green, blue, name);
            wind.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            var result = wind.ShowDialog();

            if(result == true)
            {
                return (wind.Red, wind.Green, wind.Blue);
            } else
            {
                return (float.NaN, float.NaN, float.NaN);
            }

        }
        public static (float Red, float Green, float Blue, float Alpha) ShowEditor(float red, float green, float blue, float alpha, string name = null)
        {
            var wind = new RawFloatValueDisplay(red, green, blue, alpha, name);
            wind.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            var result = wind.ShowDialog();

            if (result == true)
            {
                return (wind.Red, wind.Green, wind.Blue, wind.Alpha);
            }
            else
            {
                return (float.NaN, float.NaN, float.NaN, float.NaN);
            }

        }
    }
}
