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
using AutoUpdaterDotNET;
using FFXIV_TexTools.ViewModels;
using xivModdingFramework.Models.DataContainers;

namespace FFXIV_TexTools.Views.Models
{
    /// <summary>
    /// Interaction logic for ImportModelEditView.xaml
    /// </summary>
    public partial class ImportModelEditView 
    {
        private ImportModelEditViewModel _viewModel;

        public ObservableCollection<KeyValuePair<int, string>> MeshSource = new ObservableCollection<KeyValuePair<int, string>>();
        public ObservableCollection<KeyValuePair<int, string>> PartSource = new ObservableCollection<KeyValuePair<int, string>>();
        public ObservableCollection<KeyValuePair<string, string>> ShapesSource = new ObservableCollection<KeyValuePair<string, string>>();
        public ObservableCollection<KeyValuePair<string, string>> AttributesSource = new ObservableCollection<KeyValuePair<string, string>>();
        public ObservableCollection<KeyValuePair<string, string>> MaterialsSource = new ObservableCollection<KeyValuePair<string, string>>();
        public ImportModelEditView(TTModel model)
        {
            InitializeComponent();

            MeshNumberBox.Items.Clear();
            PartNumberBox.Items.Clear();

            for (var mIdx = 0; mIdx < model.MeshGroups.Count; mIdx++)
            {
                var m = model.MeshGroups[mIdx];
                MeshSource.Add(new KeyValuePair<int, string>(mIdx, "#" + mIdx.ToString()));
            }

            MeshNumberBox.ItemsSource = MeshSource;
            MeshNumberBox.DisplayMemberPath = "Value";
            MeshNumberBox.SelectedValuePath = "Key";

            PartNumberBox.ItemsSource = PartSource;
            PartNumberBox.DisplayMemberPath = "Value";
            PartNumberBox.SelectedValuePath = "Key";


            MaterialSelectorBox.ItemsSource = MaterialsSource;
            MaterialSelectorBox.DisplayMemberPath = "Value";
            MaterialSelectorBox.SelectedValuePath = "Key";

            ShapesListBox.ItemsSource = ShapesSource;
            ShapesListBox.DisplayMemberPath = "Value";
            ShapesListBox.SelectedValuePath = "Key";

            AttributesListBox.ItemsSource = AttributesSource;
            AttributesListBox.DisplayMemberPath = "Value";
            AttributesListBox.SelectedValuePath = "Key";



            _viewModel = new ImportModelEditViewModel(this, model);
            this.DataContext = _viewModel;
        }

        private void ImportButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}
