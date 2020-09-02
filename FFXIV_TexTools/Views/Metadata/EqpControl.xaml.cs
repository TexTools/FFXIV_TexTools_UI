using HelixToolkit.SharpDX.Core.Helper;
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
using xivModdingFramework.Models.DataContainers;
using xivModdingFramework.Mods.FileTypes;

namespace FFXIV_TexTools.Views.Metadata
{
    /// <summary>
    /// Interaction logic for EqpView.xaml
    /// </summary>
    public partial class EqpControl : UserControl
    {
        private ItemMetadata _metadata;
        private EquipmentParameter entry;

        private ObservableCollection<KeyValuePair<string, byte[]>> PresetCollection = new ObservableCollection<KeyValuePair<string, byte[]>>();
        public EqpControl()
        {
            InitializeComponent();

            TabSelection.SelectionChanged += TabSelection_SelectionChanged;


            PresetComboBox.ItemsSource = PresetCollection;
            PresetComboBox.DisplayMemberPath = "Key";
            PresetComboBox.SelectedValuePath = "Value";

            PresetComboBox.SelectionChanged += PresetComboBox_SelectionChanged;
        }

        private void TabSelection_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if(e.Source == TabSelection)
            {
                UpdateSimpleTab();
                UpdateAdvancedTab();
            }
        }

        public async Task SetMetadata(ItemMetadata m)
        {
            _metadata = m;

            entry = _metadata.EqpEntry;

            RawGrid.Children.Clear();
            PresetCollection.Clear();
            if (entry == null) return;

            var flags = entry.GetFlags();

            // Advanced Flag Setup.
            var idx = 0;
            foreach (var flag in flags)
            {
                var cb = new CheckBox();
                cb.Content = flag.Key.ToString();
                cb.DataContext = flag.Key;
                cb.IsChecked = flag.Value;

                cb.SetValue(Grid.RowProperty, idx / 4);
                cb.SetValue(Grid.ColumnProperty, idx % 4);

                cb.Checked += Cb_Checked;
                cb.Unchecked += Cb_Checked;

                RawGrid.Children.Add(cb);
                idx++;
            }

            // Simple setup.
            PresetCollection.Add(new KeyValuePair<string, byte[]>("Custom", null));

            if (Presets.ContainsKey(m.Root.Info.Slot))
            {
                var presets = Presets[m.Root.Info.Slot];

                foreach(var preset in presets)
                {
                    PresetCollection.Add(new KeyValuePair<string, byte[]>(preset.Key, preset.Value ));
                }
            }

            UpdateSimpleTab();
        }
        private void PresetComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var preset = PresetComboBox.SelectedValue;
            if (preset == null) return;

            var bytes = (byte[])preset;
            if (bytes == null) return;

            entry.SetBytes(bytes);
        }


        private void UpdateSimpleTab()
        {
            var eBytes = entry.GetBytes();
            bool found = false;
            foreach(var kv in PresetCollection)
            {
                var bytes = kv.Value;
                if (bytes == null) continue;
                if (bytes.Length != eBytes.Length) continue;

                bool good = true;
                for(int i = 0; i < bytes.Length; i++)
                {
                    if (bytes[i] != eBytes[i])
                    {
                        good = false; 
                        break;
                    }
                }
                if(good)
                {
                    PresetComboBox.SelectedValue = bytes;
                    found = true;
                    break;
                }
            }

            if(!found)
            {
                PresetComboBox.SelectedIndex = 0;
            }
        }
        private void UpdateAdvancedTab()
        {
            foreach(var child in RawGrid.Children)
            {
                var cb = (CheckBox)child;
                var flag = (EquipmentParameterFlag)cb.DataContext;
                var enabled = _metadata.EqpEntry.GetFlag(flag);
                cb.IsChecked = enabled;
            }
        }

        private void Cb_Checked(object sender, RoutedEventArgs e)
        {
            var bytes = entry.GetBytes();
            var cb = (CheckBox)sender;
            var enabled = cb.IsChecked == true ? true : false;
            var flag = (EquipmentParameterFlag)cb.DataContext;

            entry.SetFlag(flag, enabled);
        }

        /// <summary>
        /// EQP Preset information.
        /// </summary>
        private static readonly Dictionary<string, Dictionary<string, byte[]>> Presets = new Dictionary<string, Dictionary<string, byte[]>>()
        {
            { "met", new Dictionary<string, byte[]>()
            {
                // 3 Bytes per
                { "Glasses", new byte [] { 255, 63, 3} },
                { "Hat", new byte [] { 227, 118, 3} },
                { "Open Helmet", new byte [] { 21, 240, 3} },
                { "Full Helmet", new byte [] { 23, 48, 3} },

            } },
            { "top", new Dictionary<string, byte[]>()
            {
                // 2 Bytes per3
                { "Sleeveless Top", new byte [] { 1, 63} },
                {  "Long-Sleeve Top", new byte [] { 115, 103 } },
                {  "Leotard", new byte [] { 1, 62 } },
                {  "Bodysuit", new byte [] { 1, 36 } },
            } },
            { "glv", new Dictionary<string, byte[]>()
            {
                // 1 Byte per
                {  "Bare Hands", new byte [] { 115 } }
                {  "Mid Gloves", new byte [] { 13 } },
                {  "Long Gloves", new byte [] { 15 } },

            } },
            { "dwn", new Dictionary<string, byte[]>()
            {
                // 1 Byte per
                {  "Shorts", new byte [] { 97 } },
                {  "Pants", new byte [] { 105 } },
                {  "Pants and Shoes", new byte [] { 65 } },

            } },
            { "sho", new Dictionary<string, byte[]>()
            {
                // 1 Byte per
                {  "Shoes", new byte [] { 3 } }
                {  "Mid Boots", new byte [] { 13 } },
                {  "Long Boots", new byte [] { 15 } },

            } }
        };
    }
}
