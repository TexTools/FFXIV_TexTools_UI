using FFXIV_TexTools.Views.Controls;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using xivModdingFramework.Cache;
using xivModdingFramework.Helpers;
using xivModdingFramework.Materials.FileTypes;
using xivModdingFramework.Mods.FileTypes;
using xivModdingFramework.Textures.FileTypes;
using xivModdingFramework.Variants.DataContainers;
using xivModdingFramework.VFX.FileTypes;

namespace FFXIV_TexTools.Views.Metadata
{
    /// <summary>
    /// Interaction logic for ImcControl.xaml
    /// </summary>
    public partial class ImcControl : UserControl
    {
        private ItemMetadata _metadata;
        public ImcControl()
        {
            InitializeComponent();

            ImcVariantBox.SelectionChanged += ImcVariantBox_SelectionChanged;
            MaterialSetBox.SelectionChanged += MaterialSetBox_SelectionChanged;

            foreach (var cb in PartsGrid.Children)
            {
                var box = (CheckBox)cb;
                box.Checked += Box_Checked;
                box.Unchecked += Box_Checked;
            }
        }

        public async Task SetMetadata(ItemMetadata m, int startingVariant = 0)
        {
            _metadata = m;
            ImcVariantBox.Items.Clear();
            MaterialSetBox.Items.Clear();

            int maxMaterialSetId = 0;
            for (int i = 0; i < m.ImcEntries.Count; i++)
            {
                ImcVariantBox.Items.Add(i);
                if (m.ImcEntries[i].Variant > maxMaterialSetId)
                {
                    maxMaterialSetId = m.ImcEntries[i].Variant;
                }
            }

            for(int i = 0; i <= maxMaterialSetId; i++)
            {
                MaterialSetBox.Items.Add(i);
            }

            ImcVariantBox.SelectedItem = startingVariant;
        }


        private void Box_Checked(object sender, RoutedEventArgs e)
        {
            var box = (CheckBox)sender;
            var part = box.Content.ToString().Trim().ToLower()[0];
            var partIndex = Array.IndexOf(Constants.Alphabet, part);

            ushort bit = (ushort)(1 << partIndex);
            var variant = (int)ImcVariantBox.SelectedItem;
            var on = box.IsChecked;

            // Update the mask as needed.
            if (on == true)
            {
                _metadata.ImcEntries[variant].Mask = (ushort)(_metadata.ImcEntries[variant].Mask | bit);
            } else
            {
                _metadata.ImcEntries[variant].Mask = (ushort)(_metadata.ImcEntries[variant].Mask & ~bit);
            }
        }

        private void ImcVariantBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_metadata == null) return;
            if (ImcVariantBox.SelectedItem == null) return;

            SetImcVariant((int)ImcVariantBox.SelectedItem);
        }

        public async Task SetImcVariant(int variant)
        {
            var entry = _metadata.ImcEntries[variant];
            foreach (var cb in PartsGrid.Children)
            {
                var box = (CheckBox)cb;
                var part = box.Content.ToString().Trim().ToLower()[0];
                var partIndex = Array.IndexOf(Constants.Alphabet, part);

                ushort bit = (ushort)(1 << partIndex);
                var active = (bit & entry.Mask) > 0;
                box.IsChecked = active;
            }

            var root = _metadata.Root;
            var items = await root.GetAllItems(variant);
            ItemNameBox.Text = "[" + items.Count + "] " + items[0].Name;

            ushort sfx = (ushort)(entry.Mask >> 10);
            SfxBox.Text = sfx.ToString();

            ushort vfx = entry.Vfx;
            VfxBox.Text = vfx.ToString();

            MaterialSetBox.SelectedIndex = entry.Variant;

        }

        private void MaterialSetBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if(ImcVariantBox.SelectedItem == null)
            {
                return;
            }
            var entry = _metadata.ImcEntries[(int)ImcVariantBox.SelectedItem];
            entry.Variant = (byte)MaterialSetBox.SelectedIndex;
        }

        private void AffectedItemsButton_Click(object sender, RoutedEventArgs e)
        {
            ShowAffectedItems();
        }
        private async Task ShowAffectedItems()
        {
            var items = await _metadata.Root.GetAllItems((int)ImcVariantBox.SelectedItem);
            var itemNames = items.Select(x => x.Name);

            var win = new AffectedFilesView(itemNames, "Affected Items");
            win.Show();
        }

        private static readonly Regex _nonNumericRegex = new Regex("[^0-9]");
        private void PreviewNumericInput(object sender, System.Windows.Input.TextCompositionEventArgs e)
        {
            if(_nonNumericRegex.IsMatch(e.Text))
            {
                e.Handled = true;
            }
        }

        private void SfxBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if(String.IsNullOrWhiteSpace(((TextBox)sender).Text) || _metadata == null)
            {
                return;
            }

            var value = Int32.Parse(((TextBox)sender).Text);
            var entry = _metadata.ImcEntries[(int)ImcVariantBox.SelectedItem];

            if(value > 63) { value = 63; }
            if(value < 0) { value = 0; }

            ushort sfxBits = (ushort) (value << 10);

            ushort baseMask = (ushort)(entry.Mask & 0x3FF);

            entry.Mask = (ushort)(baseMask | sfxBits);
        }

        private void VfxBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (String.IsNullOrWhiteSpace(((TextBox)sender).Text) || _metadata == null )
            {
                return;
            }

            var value = Int32.Parse(((TextBox)sender).Text);
            var entry = _metadata.ImcEntries[(int)ImcVariantBox.SelectedItem];

            if (value > ushort.MaxValue ) { value = ushort.MaxValue; }
            if (value < 0) { value = 0; }

            entry.Vfx = (ushort)value;
        }


        private async void ShowPaths(object sender, RoutedEventArgs e)
        {
            var entry = _metadata.ImcEntries[(int)ImcVariantBox.SelectedItem];

            var paths = new List<(string title, string path)>();

            var mtrl = new Mtrl(XivCache.GameInfo.GameDirectory, IOUtil.GetDataFileFromPath(_metadata.Root.Info.GetRootFile()), XivCache.GameInfo.GameLanguage);
            var folder = mtrl.GetMtrlFolder(_metadata.Root.Info, entry.Variant) + "/";
            paths.Add(("Material Folder", folder));

            if (entry.Vfx > 0)
            {
                var pair = await ATex.GetVfxPath(_metadata.Root.Info, entry.Vfx);
                paths.Add(("VFX File Path", pair.Folder + "/" + pair.File));
            } else
            {
                paths.Add(("VFX File Path", "--"));
            }


            var wind = new PathDisplay(paths);
            wind.Show();
        }
        private void ApplyAllButton_Click(object sender, RoutedEventArgs e)
        {
            var current = _metadata.ImcEntries[(int)ImcVariantBox.SelectedItem];

            foreach(var entry in _metadata.ImcEntries)
            {
                entry.Mask = current.Mask;
                entry.Unknown = current.Unknown;
                entry.Vfx = current.Vfx;
                entry.Variant = current.Variant;
            }
        }

        private void AddVariantButton_Click(object sender, RoutedEventArgs e)
        {
            var current = _metadata.ImcEntries[(int)ImcVariantBox.SelectedItem];
            var entry = new XivImc();
            entry.Mask = current.Mask;
            entry.Unknown = current.Unknown;
            entry.Vfx = current.Vfx;
            entry.Variant = current.Variant;

            var idx = _metadata.ImcEntries.Count;
            _metadata.ImcEntries.Add(entry);
            ImcVariantBox.Items.Add(idx);
            ImcVariantBox.SelectedIndex = idx;
        }

        private void AddMaterialSetButton_Click(object sender, RoutedEventArgs e)
        {
            var currentMax = MaterialSetBox.Items.Count;
            if (currentMax >= 256) return;

            MaterialSetBox.Items.Add(currentMax);
            MaterialSetBox.SelectedIndex = currentMax;
        }
    }
}
