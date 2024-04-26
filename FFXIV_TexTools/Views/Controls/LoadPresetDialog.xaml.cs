
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using static xivModdingFramework.Materials.DataContainers.ShaderHelpers;
using System.IO;
using System.Linq;
using xivModdingFramework.Materials.DataContainers;
using xivModdingFramework.Cache;
using xivModdingFramework.Materials.FileTypes;
using MahApps.Metro.IconPacks;

namespace FFXIV_TexTools.Views.Controls
{
    /// <summary>
    /// Interaction logic for RawFloatValueDisplay.xaml
    /// </summary>
    public partial class LoadPresetDialog : Window
    {
        public const string _PresetsPath = "./Resources/MaterialPresets";

        XivMtrl Material;

        public LoadPresetDialog(XivMtrl material)
        {
            Material = material;
            InitializeComponent();
            var collection = new ObservableCollection<KeyValuePair<string,string>>();
            var files = Directory.GetFiles(_PresetsPath, "*",SearchOption.AllDirectories).Where(x => x.EndsWith(".mtrl"));
            foreach (var file in files)
            {
                var folderPath = Path.GetDirectoryName(file);
                var parts = folderPath.Split(Path.DirectorySeparatorChar);
                var shaderName = parts.Last() + ".shpk";
                var fName = Path.GetFileNameWithoutExtension(file);
                var displayName = shaderName + " - " + fName;
                var kp = new KeyValuePair<string, string>(displayName, file);
                collection.Add(kp);
            }
            
            PresetsList.ItemsSource = collection.OrderBy(x => x.Key);
            PresetsList.DisplayMemberPath = "Key";
            PresetsList.SelectedValuePath = "Value";
            PresetsList.SelectedIndex = 0;
        }

        private void LoadButton_Click(object sender, RoutedEventArgs e)
        {
            var path = (string) PresetsList.SelectedValue;

            var _mtrl = new Mtrl(XivCache.GameInfo.GameDirectory);
            var bytes = System.IO.File.ReadAllBytes(path);
            var newMtrl = _mtrl.GetMtrlData(bytes, Material.MTRLPath);

            // Carry our colorset information through.
            if (ColorsetBox.IsChecked == false)
            {
                if (newMtrl.ColorSetData.Count > 0 && Material.ColorSetData.Count > 0)
                {
                    newMtrl.ColorSetData = Material.ColorSetData;
                    newMtrl.ColorSetDyeData = Material.ColorSetDyeData;
                }
            }

            // Carry our Shader information through.
            if(ShaderBox.IsChecked == false)
            {
                newMtrl.Shader = Material.Shader;
                newMtrl.ShaderConstants = Material.ShaderConstants;
                newMtrl.ShaderKeys = Material.ShaderKeys;
            }

            // Carry our Other information through.
            if(OtherBox.IsChecked == false)
            {
                newMtrl.MaterialFlags = Material.MaterialFlags;
                newMtrl.MaterialFlags2 = Material.MaterialFlags2;
                newMtrl.AdditionalData = Material.AdditionalData;
            }

            // Carry our Texture information through.
            if(TextureBox.IsChecked == false)
            {
                newMtrl.Textures = Material.Textures;
            }

            Material = newMtrl;
            DialogResult = true;
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            Material = null;
            DialogResult = false;
        }

        public static XivMtrl ShowLoadPresetDialog(XivMtrl material, Window owner = null)
        {
            var wind = new LoadPresetDialog(material);
            wind.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            wind.Owner = owner != null ? owner : System.Windows.Application.Current.MainWindow;
            var result = wind.ShowDialog();
            if(result != true)
            {
                return null;
            }

            return wind.Material;
        }
    }
}
