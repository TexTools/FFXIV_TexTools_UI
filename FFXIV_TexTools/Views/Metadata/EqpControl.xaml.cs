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

        private static string SlotCopiedFrom = null;
        private static byte[] CopiedBytes = null;

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

            if(entry.Slot == SlotCopiedFrom && CopiedBytes != null)
            {
                PasteButton.IsEnabled = true;
            } else
            {
                PasteButton.IsEnabled = false;
            }

            var flags = entry.GetFlags();

            // Advanced Flag Setup.
            var idx = 0;
            foreach (var flag in flags)
            {
                var cb = new CheckBox();
                cb.Content = flag.Key.ToString().L();
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
            PresetCollection.Add(new KeyValuePair<string, byte[]>("Custom".L(), null));

            if (Presets.ContainsKey(m.Root.Info.Slot.L()))
            {
                var presets = Presets[m.Root.Info.Slot.L()];

                foreach(var preset in presets)
                {
                    PresetCollection.Add(new KeyValuePair<string, byte[]>(preset.Key.L(), preset.Value ));
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
            { "met".L(), new Dictionary<string, byte[]>()
            {
                // 3 Bytes per
                { "Glasses".L(), new byte [] { 225, 63, 3} },
                { "Hat".L(), new byte [] { 227, 118, 3} },
                { "Open Helmet".L(), new byte [] { 21, 240, 3} },
                { "Full Helmet".L(), new byte [] { 23, 48, 3} },

            } },
            { "top".L(), new Dictionary<string, byte[]>()
            {
                // 2 Bytes per3
                { "Sleeveless Top".L(), new byte [] { 1, 63} },
                {  "Long-Sleeve Top".L(), new byte [] { 115, 103 } },
                {  "Leotard".L(), new byte [] { 1, 62 } },
                {  "Bodysuit".L(), new byte [] { 1, 36 } },
            } },
            { "glv".L(), new Dictionary<string, byte[]>()
            {
                // 1 Byte per
                {  "Bare Hands".L(), new byte [] { 115 } },
                {  "Mid Gloves".L(), new byte [] { 13 } },
                {  "Long Gloves".L(), new byte [] { 15 } },

            } },
            { "dwn".L(), new Dictionary<string, byte[]>()
            {
                // 1 Byte per
                {  "Shorts".L(), new byte [] { 97 } },
                {  "Pants".L(), new byte [] { 105 } },
                {  "Pants and Shoes".L(), new byte [] { 65 } },

            } },
            { "sho".L(), new Dictionary<string, byte[]>()
            {
                // 1 Byte per
                {  "Shoes".L(), new byte [] { 3 } },
                {  "Mid Boots".L(), new byte [] { 13 } },
                {  "Long Boots".L(), new byte [] { 15 } },

            } }
        };

        private void CopyButton_Click(object sender, RoutedEventArgs e)
        {
            var raw = _metadata.EqpEntry.GetBytes();
            var bytes = new byte[raw.Length];
            Array.Copy(raw, bytes, raw.Length);
            CopiedBytes = bytes;
            SlotCopiedFrom = _metadata.Root.Info.Slot;

            PasteButton.IsEnabled = true;
        }

        private void PasteButton_Click(object sender, RoutedEventArgs e)
        {
            var bytes = new byte[CopiedBytes.Length];
            Array.Copy(CopiedBytes, bytes, CopiedBytes.Length);
            _metadata.EqpEntry.SetBytes(bytes);
        }
    }
}
