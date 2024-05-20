using System;
using System.Collections.Generic;
using System.IO;
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

namespace FFXIV_TexTools.Views.MaterialEditor
{
    /// <summary>
    /// Interaction logic for LoadColorsetWindow.xaml
    /// </summary>
    public partial class LoadColorsetWindow
    {
        public LoadColorsetWindow(string path)
        {
            InitializeComponent();

            if(!File.Exists(path))
            {
                DialogResult = false;
                return;
            }

            PathBox.Text = path;

            var datPath = path.Replace(".dds", ".dat");
            DyeImportBox.IsChecked = true;
            ColorsetImportBox.IsChecked = true;
            if (!File.Exists(datPath))
            {
                DyeImportBox.IsEnabled = false;
                DyeImportBox.IsChecked = false;
                DyeImportBox.ToolTip = "Dye File (*.dat) does not exist.";
            }
        }

        public bool ImportColorset = true;
        public bool ImportDye = true;

        private void Import_Click(object sender, RoutedEventArgs e)
        {
            ImportColorset = ColorsetImportBox.IsChecked == true;
            ImportDye = DyeImportBox.IsChecked == true;
            DialogResult = true;
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }

        public static (bool? ImportColorset, bool? ImportDye) ShowImport(string path, Window owner = null)
        {
            if (owner == null)
            {
                owner = MainWindow.GetMainWindow();
            }
            var wind = new LoadColorsetWindow(path);
            wind.Owner = owner;
            wind.WindowStartupLocation = WindowStartupLocation.CenterOwner;

            var res = wind.ShowDialog();
            if(res != true)
            {
                return (null, null);
            }
            return (wind.ImportColorset, wind.ImportDye);
        }
    }
}
