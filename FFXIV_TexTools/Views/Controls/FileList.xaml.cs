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

namespace FFXIV_TexTools.Views.Controls
{
    /// <summary>
    /// Interaction logic for FileList.xaml
    /// </summary>
    public partial class FileList : UserControl
    {
        private string _listTitle = "List Title";

        private ObservableCollection<string> FilesList { get; set; }
        public string ListTitle { get
            {
                return _listTitle;
            }
            set
            {
                _listTitle = value;
                FilesLabel.Content = _listTitle;
            }
        }

        public event EventHandler<string> FileRemoved;

        public FileList()
        {
            DataContext = this;
            InitializeComponent();

            RemoveButton.Click += RemoveButton_Click;
        }

        private void RemoveButton_Click(object sender, RoutedEventArgs e)
        {
            var file = (string) (FileBox.SelectedItem);
            if (file == null) return;
            FilesList.Remove(file);
        }
    }
}
