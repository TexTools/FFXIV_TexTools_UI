using FFXIV_TexTools.Views.Wizard;
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
using System.Windows.Navigation;
using System.Windows.Shapes;
using xivModdingFramework.Mods.DataContainers;

namespace FFXIV_TexTools.Views
{
    /// <summary>
    /// Interaction logic for EditableOption.xaml
    /// </summary>
    public partial class EditableOptionControl : UserControl
    {
        public WizardOptionEntry Option;
        public EditableOptionControl(WizardOptionEntry option)
        {
            Option = option;

            InitializeComponent();

            OptionLabel.Content = option.Name;
            OptionTextBox.Text = option.Name;

            OptionLabel.MouseDoubleClick += OptionLabel_MouseDoubleClick;
            OptionTextBox.TextChanged += OptionTextBox_TextChanged;
            OptionTextBox.KeyDown += OptionTextBox_KeyDown;
            OptionTextBox.LostFocus += OptionTextBox_LostFocus;
        }

        private void OptionTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            OptionLabel.Visibility = Visibility.Visible;
            OptionTextBox.Visibility = Visibility.Hidden;
        }

        private void OptionTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                OptionLabel.Visibility = Visibility.Visible;
                OptionTextBox.Visibility = Visibility.Hidden;
            }
        }

        private void OptionTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            OptionLabel.Content = OptionTextBox.Text;
            Option.Name = OptionTextBox.Text;
        }

        private void OptionLabel_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            EditMode();
        }

        public void EditMode()
        {
            OptionLabel.Visibility = Visibility.Hidden;
            OptionTextBox.Visibility = Visibility.Visible;

            // Kinda hacky but I couldn't get it to focus without a delay or via another event
            Task.Delay(50).ContinueWith(_ =>
            {
                Application.Current.Dispatcher.Invoke(new Action(() =>
                {
                    OptionTextBox.Focus();
                    OptionTextBox.SelectAll();
                }));
            });
        }

        public override string ToString() 
        {
            return OptionLabel.Content.ToString();
        }
    }
}
