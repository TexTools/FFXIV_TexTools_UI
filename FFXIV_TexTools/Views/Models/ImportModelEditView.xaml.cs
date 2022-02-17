using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
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
        private TTModel _newModel;
        private TTModel _oldModel;

        public ObservableCollection<KeyValuePair<int, string>> MeshSource = new ObservableCollection<KeyValuePair<int, string>>();
        public ObservableCollection<KeyValuePair<int, string>> PartSource = new ObservableCollection<KeyValuePair<int, string>>();
        public ObservableCollection<KeyValuePair<string, string>> ShapesSource = new ObservableCollection<KeyValuePair<string, string>>();
        public ObservableCollection<KeyValuePair<string, string>> AttributesSource = new ObservableCollection<KeyValuePair<string, string>>();
        public ObservableCollection<KeyValuePair<string, string>> MaterialsSource = new ObservableCollection<KeyValuePair<string, string>>();
        public ObservableCollection<KeyValuePair<string, string>> AllAttributesSource = new ObservableCollection<KeyValuePair<string, string>>();
        public ObservableCollection<KeyValuePair<double, string>> SizeMultiplierSource = new ObservableCollection<KeyValuePair<double, string>>();

        public ImportModelEditView(TTModel newModel, TTModel oldModel)
        {
            InitializeComponent();
            _newModel = newModel;
            _oldModel = oldModel;
            MeshNumberBox.Items.Clear();
            PartNumberBox.Items.Clear();
            ScaleComboBox.Items.Clear();

            var itemName = Path.GetFileNameWithoutExtension(oldModel.Source);
            for (var mIdx = 0; mIdx < _newModel.MeshGroups.Count; mIdx++)
            {
                var m = _newModel.MeshGroups[mIdx];
                if(m.Name == null)
                {
                    MeshSource.Add(new KeyValuePair<int, string>(mIdx, "#" + mIdx.ToString() + ": " + "Unknown"));

                } else
                {
                    var name = m.Name.Replace(itemName, "");
                    name = name.Trim();
                    MeshSource.Add(new KeyValuePair<int, string>(mIdx, "#" + mIdx.ToString() + ": " + name));
                }
            }

            SizeMultiplierSource.Add(new KeyValuePair<double, string>(1.0D, "1x"));
            SizeMultiplierSource.Add(new KeyValuePair<double, string>(2.0D, "2x"));
            SizeMultiplierSource.Add(new KeyValuePair<double, string>(3.0D, "3x"));
            SizeMultiplierSource.Add(new KeyValuePair<double, string>(4.0D, "4x"));
            SizeMultiplierSource.Add(new KeyValuePair<double, string>(5.0D, "5x"));
            SizeMultiplierSource.Add(new KeyValuePair<double, string>(10.0D, "10x"));
            SizeMultiplierSource.Add(new KeyValuePair<double, string>(100.0D, "100x"));
            SizeMultiplierSource.Add(new KeyValuePair<double, string>(.1D, "0.1x"));
            SizeMultiplierSource.Add(new KeyValuePair<double, string>(.01D, "0.01x"));
            SizeMultiplierSource.Add(new KeyValuePair<double, string>(0.03937007874D, "0.039x (Legacy Fix)"));
            SizeMultiplierSource.Add(new KeyValuePair<double, string>(0, "Custom"));

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


            _viewModel = new ImportModelEditViewModel(this, _newModel, _oldModel);
            this.DataContext = _viewModel;
        }

        private void ImportButton_Click(object sender, RoutedEventArgs e)
        {
            if (_viewModel.ModelSize != 1.0)
            {
                ModelModifiers.ScaleModel(_newModel, _viewModel.ModelSize);
            }
            DialogResult = true;
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}
