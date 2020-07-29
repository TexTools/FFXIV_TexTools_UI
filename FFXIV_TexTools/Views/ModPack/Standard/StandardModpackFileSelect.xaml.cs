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
using System.Windows.Navigation;
using System.Windows.Shapes;
using xivModdingFramework.Cache;
using xivModdingFramework.Items.Interfaces;

namespace FFXIV_TexTools.Views
{
    /// <summary>
    /// Interaction logic for StandardModpackFileSelect.xaml
    /// </summary>
    public partial class StandardModpackFileSelect : Page
    {
        public event EventHandler<ObservableCollection<string>> FilesSelected;

        private ObservableCollection<string> SelectedFiles;
        public StandardModpackFileSelect(IItem item, XivDependencyLevel level)
        {
            InitializeComponent();
        }
    }
}
