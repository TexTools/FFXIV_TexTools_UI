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

namespace FFXIV_TexTools.Views
{
    /// <summary>
    /// Interaction logic for AffectedFilesView.xaml
    /// </summary>
    public partial class AffectedFilesView : Window
    {
        public AffectedFilesView(IEnumerable<string> entries, string title)
        {
            WindowStartupLocation = WindowStartupLocation.CenterScreen;

            InitializeComponent();
            Title = title;
            foreach (var e in entries)
            {
                FileBox.Items.Add(e);
            }

        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
