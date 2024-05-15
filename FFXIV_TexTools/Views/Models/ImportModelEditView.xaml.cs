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

        public ObservableCollection<KeyValuePair<EMeshType, string>> MeshTypeSource = new ObservableCollection<KeyValuePair<EMeshType, string>>();
        public ObservableCollection<KeyValuePair<int, string>> MeshSource = new ObservableCollection<KeyValuePair<int, string>>();
        public ObservableCollection<KeyValuePair<int, string>> PartSource = new ObservableCollection<KeyValuePair<int, string>>();
        public ObservableCollection<KeyValuePair<string, string>> ShapesSource = new ObservableCollection<KeyValuePair<string, string>>();
        public ObservableCollection<KeyValuePair<string, string>> AttributesSource = new ObservableCollection<KeyValuePair<string, string>>();
        public ObservableCollection<KeyValuePair<string, string>> MaterialsSource = new ObservableCollection<KeyValuePair<string, string>>();
        public ObservableCollection<KeyValuePair<string, string>> AllAttributesSource = new ObservableCollection<KeyValuePair<string, string>>();
        public ObservableCollection<KeyValuePair<double, string>> SizeMultiplierSource = new ObservableCollection<KeyValuePair<double, string>>();
        public ObservableCollection<KeyValuePair<int, string>> MdlVersionSource = new ObservableCollection<KeyValuePair<int, string>>();

        public ImportModelEditView(TTModel newModel, TTModel oldModel)
        {
            InitializeComponent();
            _newModel = newModel;
            _oldModel = oldModel;
            MeshNumberBox.Items.Clear();
            PartNumberBox.Items.Clear();
            ScaleComboBox.Items.Clear();
            MdlVersionComboBox.Items.Clear();

            var itemName = Path.GetFileNameWithoutExtension(oldModel.Source);
            for (var mIdx = 0; mIdx < _newModel.MeshGroups.Count; mIdx++)
            {
                var m = _newModel.MeshGroups[mIdx];
                if(m.Name == null)
                {
                    MeshSource.Add(new KeyValuePair<int, string>(mIdx, "#".L() + mIdx.ToString() + ": " + "Unknown".L()));

                } else
                {
                    var name = m.Name.Replace(itemName, "");
                    name = name.Trim();
                    MeshSource.Add(new KeyValuePair<int, string>(mIdx, "#" + mIdx.ToString() + ": " + name));
                }
            }

            var values = Enum.GetValues(typeof(EMeshType));
            foreach(var v in values)
            {
                var e = (EMeshType)v;
                MeshTypeSource.Add(new KeyValuePair<EMeshType, string>(e, e.ToString()));
            }
            ModelTypeComboBox.ItemsSource = MeshTypeSource;
            ModelTypeComboBox.DisplayMemberPath = "Value";
            ModelTypeComboBox.SelectedValuePath = "Key";



            SizeMultiplierSource.Add(new KeyValuePair<double, string>(1.0D, "1x"));
            SizeMultiplierSource.Add(new KeyValuePair<double, string>(2.0D, "2x"));
            SizeMultiplierSource.Add(new KeyValuePair<double, string>(3.0D, "3x"));
            SizeMultiplierSource.Add(new KeyValuePair<double, string>(4.0D, "4x"));
            SizeMultiplierSource.Add(new KeyValuePair<double, string>(5.0D, "5x"));
            SizeMultiplierSource.Add(new KeyValuePair<double, string>(10.0D, "10x"));
            SizeMultiplierSource.Add(new KeyValuePair<double, string>(100.0D, "100x"));
            SizeMultiplierSource.Add(new KeyValuePair<double, string>(.1D, "0.1x"));
            SizeMultiplierSource.Add(new KeyValuePair<double, string>(.01D, "0.01x"));
            SizeMultiplierSource.Add(new KeyValuePair<double, string>(0.03937007874D, "0.039x (Legacy Fix)".L()));
            MdlVersionSource.Add(new KeyValuePair<int, string>(5, "5"));
            MdlVersionSource.Add(new KeyValuePair<int, string>(6, "6"));

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

            MdlVersionComboBox.ItemsSource = MdlVersionSource;
            MdlVersionComboBox.DisplayMemberPath = "Value";
            MdlVersionComboBox.SelectedValuePath = "Key";

            ScaleComboBox.SelectedValue = 1.0f;
            var version = 6;
            if(_newModel.MdlVersion > 0)
            {
                version = _newModel.MdlVersion;
            } else if(_oldModel.MdlVersion > 0)
            {
                version = _oldModel.MdlVersion;
            }

            MdlVersionComboBox.SelectedValue = version;


#if ENDWALKER  
            _newModel.MdlVersion = 5;
            MdlVersionComboBox.IsEnabled = false;
#endif

            _viewModel = new ImportModelEditViewModel(this, _newModel, _oldModel);



            this.DataContext = _viewModel;
        }

        private void ImportButton_Click(object sender, RoutedEventArgs e)
        {
            if (Convert.ToDouble(ScaleComboBox.SelectedValue) != 1.0)
            {
                ModelModifiers.ScaleModel(_newModel, (double)ScaleComboBox.SelectedValue);
            }
            var val = (int) MdlVersionComboBox.SelectedValue;
            _newModel.MdlVersion = (ushort) val;
            DialogResult = true;
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}
