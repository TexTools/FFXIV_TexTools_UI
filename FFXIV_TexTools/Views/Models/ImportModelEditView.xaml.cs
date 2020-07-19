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
using xivModdingFramework.Models.Helpers;

namespace FFXIV_TexTools.Views.Models
{
    /// <summary>
    /// Interaction logic for ImportModelEditView.xaml
    /// </summary>
    public partial class ImportModelEditView 
    {
        private ImportModelEditViewModel _viewModel;
        private TTModel _model;

        public ObservableCollection<KeyValuePair<int, string>> MeshSource = new ObservableCollection<KeyValuePair<int, string>>();
        public ObservableCollection<KeyValuePair<int, string>> PartSource = new ObservableCollection<KeyValuePair<int, string>>();
        public ObservableCollection<KeyValuePair<string, string>> ShapesSource = new ObservableCollection<KeyValuePair<string, string>>();
        public ObservableCollection<KeyValuePair<string, string>> AttributesSource = new ObservableCollection<KeyValuePair<string, string>>();
        public ObservableCollection<KeyValuePair<string, string>> MaterialsSource = new ObservableCollection<KeyValuePair<string, string>>();
        public ObservableCollection<KeyValuePair<string, string>> AllAttributesSource = new ObservableCollection<KeyValuePair<string, string>>();
        public ObservableCollection<KeyValuePair<float, string>> SizeMultiplierSource = new ObservableCollection<KeyValuePair<float, string>>();

        public ImportModelEditView(TTModel model)
        {
            InitializeComponent();
            _model = model;
            MeshNumberBox.Items.Clear();
            PartNumberBox.Items.Clear();
            ScaleComboBox.Items.Clear();

            for (var mIdx = 0; mIdx < model.MeshGroups.Count; mIdx++)
            {
                var m = model.MeshGroups[mIdx];
                if(m.Name == null)
                {
                    MeshSource.Add(new KeyValuePair<int, string>(mIdx, "#" + mIdx.ToString()));

                } else
                {
                    MeshSource.Add(new KeyValuePair<int, string>(mIdx, "#" + mIdx.ToString() + ": " + m.Name));
                }
            }


            SizeMultiplierSource.Add(new KeyValuePair<float, string>(1.0f, "1x"));
            SizeMultiplierSource.Add(new KeyValuePair<float, string>(10.0f, "10x"));
            SizeMultiplierSource.Add(new KeyValuePair<float, string>(100.0f, "100x"));
            SizeMultiplierSource.Add(new KeyValuePair<float, string>(.1f, "0.1x"));
            SizeMultiplierSource.Add(new KeyValuePair<float, string>(.01f, "0.01x"));

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

            AddAttributeBox.ItemsSource = AllAttributesSource;
            AddAttributeBox.DisplayMemberPath = "Value";
            AddAttributeBox.SelectedValuePath = "Key";

            ScaleComboBox.ItemsSource = SizeMultiplierSource;
            ScaleComboBox.DisplayMemberPath = "Value";
            ScaleComboBox.SelectedValuePath = "Key";

            ScaleComboBox.SelectedValue = 1.0f;


            _viewModel = new ImportModelEditViewModel(this, model);
            this.DataContext = _viewModel;
        }

        private void ImportButton_Click(object sender, RoutedEventArgs e)
        {
            if ((float)ScaleComboBox.SelectedValue != 1.0f)
            {
                ModelModifiers.ScaleModel(_model, (float)ScaleComboBox.SelectedValue);
            }
            DialogResult = true;
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}
