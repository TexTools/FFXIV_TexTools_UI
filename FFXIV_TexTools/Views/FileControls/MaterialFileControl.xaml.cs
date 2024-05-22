using FFXIV_TexTools.Textures;
using SharpDX.Toolkit.Graphics;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Bmp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using TeximpNet.DDS;
using xivModdingFramework.Cache;
using xivModdingFramework.Items.Enums;
using xivModdingFramework.Items.Interfaces;
using xivModdingFramework.SqPack.FileTypes;
using xivModdingFramework.Textures.DataContainers;
using xivModdingFramework.Textures.Enums;
using xivModdingFramework.Textures.FileTypes;
using xivModdingFramework.Variants.DataContainers;
using xivModdingFramework.Variants.FileTypes;
using static FFXIV_TexTools.ViewModels.TextureViewModel;
using xivModdingFramework.Items;
using Image = SixLabors.ImageSharp.Image;
using FFXIV_TexTools.Views.MaterialEditor;
using xivModdingFramework.Materials.DataContainers;
using FFXIV_TexTools.ViewModels;
using static xivModdingFramework.Materials.DataContainers.ShaderHelpers;
using System.Collections.ObjectModel;
using FFXIV_TexTools.Helpers;
using xivModdingFramework.Materials.FileTypes;
using xivModdingFramework.Mods;
using FFXIV_TexTools.Resources;

namespace FFXIV_TexTools.Views.Controls
{
    public class WrappedTexture : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        public MtrlTexture Texture { get; set; }

        public MaterialFileControl View;

        public XivTexType Usage
        {
            get
            {
                return Texture.Usage;
            }
        }

        public string TexturePath
        {
            get
            {
                return View.Material.TokenizePath(Texture.TexturePath, Texture.Usage);
            }
            set
            {
                Texture.TexturePath = View.Material.DetokenizePath(value, Texture.Usage);
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(TexturePath)));
            }
        }

        public WrappedTexture(MtrlTexture tex, MaterialFileControl view)
        {
            Texture = tex;
            View = view;
        }
    }

    public partial class MaterialFileControl : FileViewControl, INotifyPropertyChanged
    {

        public ObservableCollection<KeyValuePair<string, EShaderPack>> ShaderSource;

        public ObservableCollection<WrappedTexture> TextureSource;

        private XivMtrl _Material;
        public XivMtrl Material
        {
            get
            {
                return _Material;
            }
            set
            {
                _Material = value;
                UpdateTextureList();
                OnPropertyChanged(nameof(Material));
                OnPropertyChanged(nameof(ShaderPack));
                OnPropertyChanged(nameof(ColorsetEnabled));
            }
        }

        public bool ColorsetEnabled
        {
            get
            {
                if(Material == null)
                {
                    return false;
                }
                return Material.ColorSetDataSize > 0;
            }
        }

        #region Wrapped Data Accessors for Base Material/UI
        public EShaderPack ShaderPack
        {
            get
            {
                if (Material == null)
                {
                    return EShaderPack.Invalid;
                }

                return Material.ShaderPack;
            }
            set
            {
                if (Material == null) return;
                Material.ShaderPack = value;
                OnPropertyChanged(nameof(ShaderPack));
            }
        }

        #endregion

        public MaterialFileControl()
        {
            DataContext = this;
            InitializeComponent();
            ViewType = EFileViewType.Editor;

            TextureSource = new ObservableCollection<WrappedTexture>();

            // Setup for the combo boxes.
            ShaderSource = new ObservableCollection<KeyValuePair<string, EShaderPack>>();
            ShaderComboBox.ItemsSource = ShaderSource;
            ShaderComboBox.DisplayMemberPath = "Key";
            ShaderComboBox.SelectedValuePath = "Value";

            // Bind SHPK names.
            // It's possible a user could add SHPKs at runtime, but we'll deal with that hurdle when we get there.
            var values = Enum.GetValues(typeof(EShaderPack));
            foreach (EShaderPack v in values)
            {
                var kv = new KeyValuePair<string, EShaderPack>(
                    ShaderHelpers.GetEnumDescription(v),
                    v
                );

                ShaderSource.Add(kv);
            }
        }


        public virtual string GetNiceName()
        {
            return "Material";
        }

        protected override KeyValuePair<string, string> GetDefaultExtension()
        {
            var extensions = GetValidFileExtensions();
            if (extensions == null || extensions.Count == 0)
            {
                return new KeyValuePair<string, string>(".mtrl", "Material");
            }
            return extensions.First();
        }
        public override Dictionary<string, string> GetValidFileExtensions()
        {
            return new Dictionary<string, string>()
            {
                { ".mtrl", "FFXIV Material" },
                { ".dds", "Colorset Image" },
                //{ ".dat", "Colorset Dye" },
            };
        }
        public override async Task INTERNAL_ClearFile()
        {
            // Clear the current editor state to blank.
            // Typically in preparation to load a new file.
            Material = null;
        }

        protected override async Task<byte[]> INTERNAL_GetUncompressedData()
        {
            // Get the uncompressed FFXIV style bytes of the current editor state.
            return Mtrl.XivMtrlToUncompressedMtrl(_Material);
        }

        protected override async Task<bool> INTERNAL_LoadFile(byte[] data)
        {
            // The incoming data is an uncompressed MTRL file.
            Material = Mtrl.GetXivMtrl(data, InternalFilePath);
            return true;
        }

        protected override async Task<bool> INTERNAL_SaveAs(string externalFilePath)
        {
            // Saving the current state to external file.
            var ext = Path.GetExtension(externalFilePath).ToLower();
                
            if(ext == ".mtrl") {
                var data = Mtrl.XivMtrlToUncompressedMtrl(_Material);
                File.WriteAllBytes(externalFilePath, data);
                return true;
            } else if(ext == ".dds")
            {
                Mtrl.SaveColorsetDyeData(_Material, externalFilePath);
                return true;
            } else
            {
                // Need to implement .dat only export later.
                throw new NotImplementedException();
            }

        }

        protected internal override async Task<bool> INTERNAL_WriteModFile(ModTransaction tx)
        {
            // We override this in order to use MTRL's import function, which checks for missing texture files, etc.
            await Mtrl.ImportMtrl(Material, ReferenceItem, XivStrings.TexTools, true, tx);
            return true;
        }


        protected override async Task<bool> ShouldUpdateOnFileChange(string changedFile)
        {
            if (!string.Equals(changedFile, InternalFilePath))
            {
                // We only care about changing if our exact file was altered.
                return false;
            }

            // Time for some cursed tech.
            return await Task.Run(async () =>
            {
                var tx = MainWindow.DefaultTransaction;
                var newMtrl = await Mtrl.GetXivMtrl(changedFile, false, tx);

                var result = Mtrl.CompareMaterials(Material, newMtrl);

                if (result.OtherDifferences)
                {
                    // If parts other than the colorset were changed, we need to prompt a reload.
                    return true;
                }

                if (!result.ColorsetDifferences)
                {
                    // Nothing actually changed, don't bother reloading.
                    return true;
                }


                // Okay, now we need to merge the data.
                // We don't need to do any UI updates because colorset information isn't displayed to start with.
                Material.ColorSetData = newMtrl.ColorSetData;
                Material.ColorSetDyeData = newMtrl.ColorSetDyeData;

                return false;
            });
        }

        /// <summary>
        /// Updates the visual texture collection.
        /// </summary>
        public void UpdateTextureList()
        {
            TextureSource = new ObservableCollection<WrappedTexture>();
            if (Material != null)
            {
                foreach (var tx in Material.Textures)
                {
                    var wt = new WrappedTexture(tx, this);
                    TextureSource.Add(wt);
                }
            }
            TexturesList.ItemsSource = TextureSource;
            OnPropertyChanged(nameof(TexturesList));
        }

        private EShaderPack _LastShpk;

        private void ShaderComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (Material == null)
            {
                return;
            }

            ShaderPack = (EShaderPack)((ComboBox)sender).SelectedValue;
            UnsavedChanges = true;

            if (ShaderComboBox.SelectedValue == null)
            {
                _LastShpk = ShaderPack;
                return;
            }
            if (_LastShpk == EShaderPack.Invalid || Material.ShaderPack == EShaderPack.Invalid)
            {
                // Don't mess with anything if we were on a broken state before, or are transitioning to one.
                _LastShpk = ShaderPack;
                return;
            }

            var lastDes = ShaderHelpers.GetEnumDescription(_LastShpk);
            var curDes = ShaderHelpers.GetEnumDescription(ShaderPack);

            var lastWithoutLegacy = lastDes.Replace("legacy", "");
            var curWithoutLegacy = curDes.Replace("legacy", "");

            if (lastWithoutLegacy == curWithoutLegacy)
            {
                // If just flipping between legacy modes, leave the shader keys/constants intact.
                // We don't know exactly which of them is valid or not on each mode, so from a use frustration standpoint it makes more sense to keep them,
                // Rather than wipe them and force the user to manually remake.
                _LastShpk = ShaderPack;
                return;
            }

            // Reset shader vars.
            Material.ShaderKeys = new List<ShaderKey>();
            Material.ShaderConstants = new List<ShaderConstant>();

            _LastShpk = ShaderPack;
        }
        private void HelpButton_Click(object sender, RoutedEventArgs e)
        {
            var help = new MaterialEditorHelpView();
            help.ShowDialog();
        }

        private void NewSharedButton_Click(object sender, RoutedEventArgs e)
        {
            if (Material == null)
            {
                return;
            }
            foreach (var tex in Material.Textures)
            {
                var path = Material.GetTextureRootDirectoy() + "/" + Material.GetDefaultTexureName(tex.Usage, false);
                tex.TexturePath = path;
            }
            UpdateTextureList();
        }

        private void NewUniqueButton_Click(object sender, RoutedEventArgs e)
        {
            if (Material == null)
            {
                return;
            }
            foreach (var tex in Material.Textures)
            {
                var path = Material.GetTextureRootDirectoy() + "/" + Material.GetDefaultTexureName(tex.Usage, true);
                tex.TexturePath = path;
            }
            UpdateTextureList();
        }

        private void TexturePathBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (Material == null)
            {
                return;
            }
            UnsavedChanges = true;
            var tb = (TextBox)sender;
            var tex = (WrappedTexture)tb.DataContext;
            tex.TexturePath = tb.Text;
        }

        private void SavePresetButton_Click(object sender, RoutedEventArgs e)
        {
            if (Material == null)
            {
                return;
            }
            var path = SavePresetDialog.ShowSavePresetDialog(Material.ShaderPack, System.IO.Path.GetFileNameWithoutExtension(Material.MTRLPath));
            if (path == "")
            {
                return;
            }

            var bytes = Mtrl.XivMtrlToUncompressedMtrl((XivMtrl)Material.Clone());
            System.IO.File.WriteAllBytes(path, bytes);
        }

        private async void LoadPresetButton_Click(object sender, RoutedEventArgs e)
        {
            if (Material == null)
            {
                return;
            }
            var result = LoadPresetDialog.ShowLoadPresetDialog(Material);
            if (result != null)
            {
                UnsavedChanges = true;
                var bytes = Mtrl.XivMtrlToUncompressedMtrl(result);
                await INTERNAL_LoadFile(bytes);
            }

        }

        private void EditShaderFlags_Click(object sender, RoutedEventArgs e)
        {
            if (Material == null)
            {
                return;
            }
            var result = MaterialFlagsEditor.ShowFlagsEditor(Material, Window.GetWindow(this));
            if (result)
            {
                UnsavedChanges = true;
                // Don't really need to do anything here, since the editor handles updating the material.
            }
        }
        private void EditShaderKeys_Click(object sender, RoutedEventArgs e)
        {
            if (Material == null)
            {
                return;
            }
            var result = ShaderKeysEditor.ShowKeysEditor(Material, Window.GetWindow(this));
            if (result == true)
            {
                UnsavedChanges = true;
                // Don't really need to do anything here, since the editor handles updating the material.
            }
        }

        private void EditShaderConstants_Click(object sender, RoutedEventArgs e)
        {
            if (Material == null)
            {
                return;
            }
            var result = ShaderConstantsEditor.ShowConstantsEditor(Material, Window.GetWindow(this));
            if (result == true)
            {
                UnsavedChanges = true;
                // Don't really need to do anything here, since the editor handles updating the material.
            }
        }

        private void EditUsage_Click(object sender, RoutedEventArgs e)
        {
            if (Material == null)
            {
                return;
            }
            var tex = (WrappedTexture)((Button)sender).DataContext;
            var result = TextureSamplerSettings.ShowSamplerSettings(Material, tex.Texture, Window.GetWindow(this));
            if (result == true)
            {
                UnsavedChanges = true;
                UpdateTextureList();
            }
        }

        private void RemoveTexture_Click(object sender, RoutedEventArgs e)
        {
            if (Material == null)
            {
                return;
            }
            var tex = (WrappedTexture)((Button)sender).DataContext;
            Material.Textures.Remove(tex.Texture);
            UnsavedChanges = true;
            UpdateTextureList();
        }
        private void AddTexture_Click(object sender, RoutedEventArgs e)
        {
            if (Material == null)
            {
                return;
            }
            var tex = new MtrlTexture();
            tex.Sampler = new TextureSampler();
            tex.Sampler.SamplerId = ESamplerId.g_SamplerNormal;
            Material.Textures.Add(tex);
            UnsavedChanges = true;
            UpdateTextureList();
        }

        private async void EditTexture_Click(object sender, RoutedEventArgs e)
        {
            if (Material == null)
            {
                return;
            }
            var tex = (WrappedTexture)((Button)sender).DataContext;
            await SimpleFileViewWindow.OpenFile(tex.Texture.TexturePath);
        }

        private async void EditColorset_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if(Material.ColorSetDataSize > 0)
                {
                    var data = Mtrl.XivMtrlToUncompressedMtrl(Material);
                    await SimpleFileViewWindow.OpenFile(Material.MTRLPath, ReferenceItem, data, true, Window.GetWindow(this));
                }
            }
            catch
            {
                // No-Op
            }
        }
    }
}
