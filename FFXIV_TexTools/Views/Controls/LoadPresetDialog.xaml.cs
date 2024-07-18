
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
using SharpDX;
using xivModdingFramework.Mods;

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

            var bytes = System.IO.File.ReadAllBytes(path);
            var newMtrl = Mtrl.GetXivMtrl(bytes, Material.MTRLPath);

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
                newMtrl.ShaderPack = Material.ShaderPack;
                newMtrl.ShaderConstants = Material.ShaderConstants;
                newMtrl.ShaderKeys = Material.ShaderKeys;
                newMtrl.AdditionalData = Material.AdditionalData;
            }

            // Carry our Other information through.
            if(OtherBox.IsChecked == false)
            {
                newMtrl.MaterialFlags = Material.MaterialFlags;
                newMtrl.MaterialFlags2 = Material.MaterialFlags2;
                newMtrl.AdditionalData = Material.AdditionalData;
            }

            var originalTextures = Material.Textures;
            // Carry our Texture information through.
            if (TextureBox.IsChecked == false)
            {
                newMtrl.Textures = Material.Textures;
            }


            if(TexturePathsBox.IsChecked == false)
            {
                foreach(var tex in newMtrl.Textures)
                {
                    if (tex.Sampler == null)
                    {
                        tex.TexturePath = "";
                        continue;
                    }

                    var samp = tex.Sampler.SamplerId;
                    var oldtex = originalTextures.FirstOrDefault(x => x.Sampler != null && x.Sampler.SamplerId == samp);
                    if (oldtex != null)
                    {
                        tex.TexturePath = oldtex.Dx11Path;
                    } else
                    {
                        tex.TexturePath = Material.GetTextureRootDirectory() + "/" + newMtrl.GetDefaultTexureName(newMtrl.ResolveFullUsage(tex), false);
                    }
                }
            }

            // Validate colorset settings.
            if(newMtrl.ShaderPack.UsesColorset() && newMtrl.ColorSetDataSize != 1024)
            {
                if (newMtrl.ShaderPack.UsesColorset() && (newMtrl.ColorSetData == null || newMtrl.ColorSetData.Count == 0))
                {
                    newMtrl.ColorSetData = new List<Half>();
                    for (int i = 0; i < 32; i++)
                    {
                        newMtrl.ColorSetData.AddRange(EndwalkerUpgrade.GetDefaultColorsetRow(newMtrl.ShaderPack));
                        newMtrl.ColorSetDyeData = new byte[128];
                    }

                }
                else if (!newMtrl.ShaderPack.UsesColorset() && (newMtrl.ColorSetData == null || newMtrl.ColorSetData.Count != 0))
                {
                    newMtrl.ColorSetData = new List<Half>();
                    newMtrl.ColorSetDyeData = new byte[0];
                }
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
