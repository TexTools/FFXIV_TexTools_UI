using FFXIV_TexTools.ViewModels;
using FFXIV_TexTools.Views.Controls;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using xivModdingFramework.Cache;
using xivModdingFramework.General.Enums;
using xivModdingFramework.Items.Interfaces;
using xivModdingFramework.Materials.DataContainers;
using xivModdingFramework.Materials.FileTypes;
using xivModdingFramework.Mods;
using xivModdingFramework.SqPack.FileTypes;
using static xivModdingFramework.Materials.DataContainers.ShaderHelpers;

namespace FFXIV_TexTools.Views.Textures
{
    public enum MaterialEditorMode
    {
        EditSingle,
        EditMulti,
        NewSingle,
        NewRace,
        NewMulti
    }
    /// <summary>
    /// Interaction logic for MaterialEditor.xaml
    /// </summary>
    public partial class MaterialEditorView
    {
        private MaterialEditorViewModel viewModel;
        private XivMtrl _material;
        private IItemModel _item;
        private MaterialEditorMode _mode;

        public ObservableCollection<KeyValuePair<string, EShaderPack>> ShaderSource;

        public ObservableCollection<MtrlTexture> TextureSource;
        private static XivMtrl _copiedMaterial;
        public XivMtrl Material
        {
            get
            {
                return _material;
            }
        }

        public MaterialEditorView()
        {
            InitializeComponent();
            viewModel = new MaterialEditorViewModel(this);
            TextureSource = new ObservableCollection<MtrlTexture>();

            // Setup for the combo boxes.
            ShaderSource = new ObservableCollection<KeyValuePair<string, EShaderPack>>();
            ShaderComboBox.ItemsSource = ShaderSource;
            ShaderComboBox.DisplayMemberPath = "Key";
            ShaderComboBox.SelectedValuePath = "Value";

            var values = Enum.GetValues(typeof(EShaderPack));
            foreach(EShaderPack v in values)
            {
                var kv = new KeyValuePair<string, EShaderPack>(
                    ShaderHelpers.GetEnumDescription(v),
                    v
                );

                ShaderSource.Add(kv);
            }


            PasteMaterialButton.IsEnabled = _copiedMaterial != null;

            DisableButton.IsEnabled = false;
            DisableButton.Visibility = Visibility.Hidden;

            SaveButton.Click += SaveButton_Click;
        }

        public async Task<bool> SetMaterial(XivMtrl material, IItemModel item, MaterialEditorMode mode = MaterialEditorMode.EditSingle)
        {
            _material = material;
            _item = item;
            _mode = mode;

            ShaderComboBox.SelectedValue = _material.Shader;
            UpdateTextureList();
            return await viewModel.SetMaterial(material, item, mode);
        }


        /// <summary>
        /// Updates the visual texture collection.
        /// </summary>
        public void UpdateTextureList()
        {
            TextureSource = new ObservableCollection<MtrlTexture>(_material.Textures);
            TexturesList.ItemsSource = TextureSource;
        }

        public XivMtrl GetMaterial()
        {
            return viewModel.GetMaterial();
        }

        public void Close(bool result)
        {
            DialogResult = result;
            Close();

        }
        private async void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            _material = await viewModel.SaveChanges();
            Close(true);
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            Close(false);
        }


        private void ShaderComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ShaderComboBox.SelectedValue == null)
            {
                return;
            }
        }
        private void HelpButton_Click(object sender, RoutedEventArgs e)
        {
            var help = new Views.Textures.MaterialEditorHelpView();
            help.ShowDialog();
        }

        private void CopyMaterialButton_Click(object sender, RoutedEventArgs e)
        {
            _copiedMaterial = (XivMtrl) _material.Clone();
            PasteMaterialButton.IsEnabled = true;
        }

        private async void PasteMaterialButton_Click(object sender, RoutedEventArgs e)
        {
            if (_copiedMaterial != null)
            {
                // Paste the copied Material into the editor using our current path and item.
                _copiedMaterial.MTRLPath = _material.MTRLPath;
                await SetMaterial(_copiedMaterial, _item, _mode);
            }
        }

        private async void DisableButton_Click(object sender, RoutedEventArgs e)
        {
            await viewModel.DisableMod();
        }

        private void NewSharedButton_Click(object sender, RoutedEventArgs e)
        {
            var sharedTex = "{item_folder}/{default_name}";
        }

        private void NewUniqueButton_Click(object sender, RoutedEventArgs e)
        {
            var uniqueTex = "{item_folder}/{variant}_{default_name}";

        }

        private void TexturePathBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            var tb = (TextBox)sender;
            var tex = (MtrlTexture)tb.DataContext;
            tex.TexturePath = tb.Text;
        }

        private void SavePresetButton_Click(object sender, RoutedEventArgs e)
        {
            var path = SavePresetDialog.ShowSavePresetDialog(_material.Shader, System.IO.Path.GetFileNameWithoutExtension(Material.MTRLPath));
            if (path == "")
            {
                return;
            }

            var _mtrl = new Mtrl(XivCache.GameInfo.GameDirectory);
            var bytes = _mtrl.CreateMtrlFile((XivMtrl)_material.Clone());
            System.IO.File.WriteAllBytes(path, bytes);
        }

        private async void LoadPresetButton_Click(object sender, RoutedEventArgs e)
        {
            var path = LoadPresetDialog.ShowLoadPresetDialog(_material.Shader);
            if(path == "")
            {
                return;
            }

            var _mtrl = new Mtrl(XivCache.GameInfo.GameDirectory);
            var bytes = System.IO.File.ReadAllBytes(path);
            var newMtrl = _mtrl.GetMtrlData(bytes, _material.MTRLPath);

            // Carry our colorset information through.
            if(newMtrl.ColorSetData.Count > 0 && _material.ColorSetData.Count > 0)
            {
                newMtrl.ColorSetData = _material.ColorSetData;
                newMtrl.ColorSetDyeData = _material.ColorSetDyeData;
            }

            await SetMaterial(newMtrl, _item, _mode);
        }

        private void EditShaderFlags_Click(object sender, RoutedEventArgs e)
        {
            var result = MaterialFlagsEditor.ShowFlagsEditor(_material.MaterialFlags, _material.MaterialFlags2);
            if(result.Success == true)
            {
                _material.MaterialFlags = result.Flags;
                _material.MaterialFlags2 = result.Unknown;
            }
        }
        private void EditShaderKeys_Click(object sender, RoutedEventArgs e)
        {
            var result = ShaderKeysEditor.ShowKeysEditor(_material.ShaderKeys);
            if(result != null)
            {
                _material.ShaderKeys = result;
            }
        }

        private void EditShaderConstants_Click(object sender, RoutedEventArgs e)
        {
            var result = ShaderConstantsEditor.ShowConstantsEditor(_material.ShaderConstants);
            if(result != null)
            {
                _material.ShaderConstants = result;
            }
        }

        private void EditUsage_Click(object sender, RoutedEventArgs e)
        {
            var tex = (MtrlTexture)((Button)sender).DataContext;
            var result = TextureSamplerSettings.ShowSamplerSettings(tex);
            if(result != null)
            {
                var idx = _material.Textures.IndexOf(tex);
                _material.Textures.RemoveAt(idx);
                _material.Textures.Insert(idx, result);
                UpdateTextureList();
            }
        }
        
        private void RemoveTexture_Click(object sender, RoutedEventArgs e)
        {
            var tex = (MtrlTexture) ((Button)sender).DataContext;
            _material.Textures.Remove(tex);
            UpdateTextureList();
        }
        private void AddTexture_Click(object sender, RoutedEventArgs e)
        {
            var tex = new MtrlTexture();
            tex.Sampler = new TextureSampler();
            tex.Sampler.SamplerId = ESamplerId.g_SamplerNormal;
            _material.Textures.Add(tex);
            UpdateTextureList();
        }


    }
}
