using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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

namespace FFXIV_TexTools.Views.Models
{
    /// <summary>
    /// Interaction logic for HighilightedColorsetSelection.xaml
    /// </summary>
    public partial class HighilightedColorsetSelection : Window
    {
        public int SelectedRow = 0;
        ObservableCollection<KeyValuePair<int, string>> Values = new ObservableCollection<KeyValuePair<int, string>>();
        public HighilightedColorsetSelection(int current)
        {
            InitializeComponent();

            SelectionComboBox.DisplayMemberPath = "Value";
            SelectionComboBox.SelectedValuePath = "Key";
            SelectionComboBox.ItemsSource = Values;

            Values.Add(new KeyValuePair<int, string>(-1, "None".L()));
            for (int i = 0; i < 16; i++)
            {
                Values.Add(new KeyValuePair<int, string>(i, (i+1).ToString()));
            }

            SelectionComboBox.SelectedValue = current;
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }

        private void ConfirmButton_Click(object sender, RoutedEventArgs e)
        {
            if (SelectionComboBox.SelectedValue == null)
            {
                SelectionComboBox.SelectedValue = -1;
            }

            SelectedRow = ((int)SelectionComboBox.SelectedValue);
            DialogResult = true;
        }
    }
}
