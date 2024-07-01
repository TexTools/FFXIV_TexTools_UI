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
using System.Diagnostics;
using System.Threading;
using System.CodeDom;
using xivModdingFramework.Helpers;
using SharpDX;

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
                return View.Material.ResolveFullUsage(Texture);
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
                Texture.TexturePath = View.Material.DetokenizePath(value, Texture);
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

        private bool _MTRL_LOADING = false;

        private XivMtrl _Material;
        public XivMtrl Material
        {
            get
            {
                return _Material;
            }
            set
            {
                _MTRL_LOADING = true;
                _Material = value;
                UpdateTextureList();
                _ = UpdateColorsetImage();
                OnPropertyChanged(nameof(Material));
                OnPropertyChanged(nameof(ShaderPack));
                OnPropertyChanged(nameof(ColorsetEnabled));
                _MTRL_LOADING = false;
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
                    return EShaderPack.Unknown;
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
                    v.ToString(),
                    v
                );

                ShaderSource.Add(kv);
            }

            ColorsetImage.MouseLeftButtonDown += ColorsetImage_MouseLeftButtonDown;
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
        public override void INTERNAL_ClearFile()
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

        protected override async Task<bool> INTERNAL_LoadFile(byte[] data, string path, IItem referenceItem, ModTransaction tx)
        {
            var exists = await tx.FileExists(path, false);

            var root = await XivCache.GetFirstRoot(path);
            var usesImc = root == null ? false : Imc.UsesImc(root);

            // The incoming data is an uncompressed MTRL file.
            var mtrl = Mtrl.GetXivMtrl(data, path);


            var msetRegex = new Regex("\\/v[0-9]{4}\\/");

            if (path != null && msetRegex.IsMatch(path))
            {
                NewSharedButton.IsEnabled = true;
                NewUniqueButton.IsEnabled = true;
            } else
            { 
                NewSharedButton.IsEnabled = false;
                NewUniqueButton.IsEnabled = false;
            }


            Material = mtrl;
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
                var tex = await Mtrl.GetColorsetXivTex(Material);
                var dyePath = Path.Combine(Path.GetDirectoryName(externalFilePath), Path.GetFileNameWithoutExtension(externalFilePath) + ".dat");

                Mtrl.SaveColorsetDyeData(Material, dyePath);
                Tex.SaveTexAsDDS(externalFilePath, tex);
                return true;
            } else
            {
                // Need to implement .dat only export later.
                throw new NotImplementedException();
            }

        }

        protected override async Task<bool> INTERNAL_WriteModFile(ModTransaction tx)
        {


            // We override this in order to use MTRL's import function, which checks for missing texture files, etc.
            await Mtrl.ImportMtrl(Material, ReferenceItem, XivStrings.TexTools, true, tx);

            await EndwalkerUpgrade.UpdateEndwalkerMaterial(Material, XivStrings.TexTools, true, tx);

            return true;
        }

        protected override async Task<byte[]> INTERNAL_ExternalToUncompressedFile(string externalFile, string internalFile, IItem referenceItem, ModTransaction tx)
        {
            var ext = Path.GetExtension(externalFile).ToLower();
            if (ext == ".mtrl")
            {
                return await base.INTERNAL_ExternalToUncompressedFile(externalFile, internalFile, referenceItem, tx);
            } else if(ext == ".dds")
            {
                if (!Material.ShaderPack.UsesColorset())
                {
                    throw new InvalidDataException(ShaderPack.ToString() + " shader pack does not use Colorsets.");
                }

                var csetData = Tex.GetColorsetDataFromDDS(externalFile);

                var mtrl = (XivMtrl) Material.Clone();
                mtrl.ColorSetData = csetData.ColorsetData;
                mtrl.ColorSetDyeData = csetData.DyeData;
                return Mtrl.XivMtrlToUncompressedMtrl(mtrl);
            }
            else
            {
                throw new NotImplementedException();
            }
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
                if(!await tx.FileExists(changedFile) || Material == null)
                {
                    // File was deleted.
                    return true;
                }

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
                Material.ColorSetData = newMtrl.ColorSetData;
                Material.ColorSetDyeData = newMtrl.ColorSetDyeData;

                // This has to be dispatched to the render thread since this event could come from any thread.
                await DispatchUpdateColorset();
                await UpdateModState(tx);

                return false;
            });
        }


        private async Task DispatchUpdateColorset()
        {
            await await Dispatcher.InvokeAsync(async () =>
            {
                try
                {
                    await UpdateColorsetImage();
                }
                catch
                {
                    // No-Op
                }
            });
        }
        private async Task UpdateColorsetImage()
        {
            if(Material == null || Material.ColorSetDataSize <= 0)
            {
                ColorsetImage.Source = null;
                return;
            }
            try
            {

                ImageSource imageSource = null;
                var pixels = new byte[0];
                var width = 0;
                var height = 0;
                await Task.Run(async () =>
                {

                    var tex = await Mtrl.GetColorsetXivTex(Material);
                    pixels = await tex.GetRawPixels();

                    // Fix opacity and convert to BGRA
                    for (int i = 0; i < pixels.Length; i += 4)
                    {
                        pixels[i + 3] = 255;

                        var r = pixels[i + 0];
                        var b = pixels[i + 2];
                        pixels[i + 2] = r;
                        pixels[i + 0] = b;
                    }

                    width = tex.Width;
                    height = tex.Height;

                });

                // This seems to generate invalid image sources when not run on the render thread.
                imageSource = BitmapSource.Create(width, height, 1, 1, PixelFormats.Bgra32, null, pixels, width * 4);

                ColorsetImage.Source = imageSource;
            }
            catch
            {
                // No-Op
            }
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

            if (_LastShpk == ShaderPack)
            {
                // Nothing changed.
                return;
            }

            UnsavedChanges = true;

            if (ShaderComboBox.SelectedValue == null || _MTRL_LOADING)
            {
                _LastShpk = ShaderPack;
                return;
            }
            if (_LastShpk == EShaderPack.Unknown || ShaderPack == EShaderPack.Unknown)
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


            // Create or purge colorset as necessary.
            if(ShaderPack != EShaderPack.Character)
            {
                Material.ColorSetData = new List<Half>();
                Material.ColorSetDyeData = new byte[0];
            } else
            {
                var length = 1024;
                var dyeLength = 128;
                Material.ColorSetData = new List<Half>(new Half[length]);
                Material.ColorSetDyeData = new byte[dyeLength];
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
                var path = Material.GetTextureRootDirectoy() + "/" + Material.GetDefaultTexureName(Material.ResolveFullUsage(tex), false);
                tex.TexturePath = path;
            }
            UnsavedChanges = true;
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
                var path = Material.GetTextureRootDirectoy() + "/" + Material.GetDefaultTexureName(Material.ResolveFullUsage(tex), true);
                tex.TexturePath = path;
            }
            UnsavedChanges = true;
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
            if (string.IsNullOrWhiteSpace(path))
            {
                return;
            }

            try
            {
                var bytes = Mtrl.XivMtrlToUncompressedMtrl((XivMtrl)Material.Clone());
                System.IO.File.WriteAllBytes(path, bytes);
            }
            catch(Exception ex)
            {
                this.ShowError("Preset Save Error", "There was an error saving the preset:\n\n" + ex.Message);
            }
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
                await INTERNAL_LoadFile(bytes, InternalFilePath, ReferenceItem, MainWindow.DefaultTransaction);
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

            var firstTex = Material.Textures.FirstOrDefault(x => x.Sampler != null);
            if(firstTex != null)
            {
                tex.Sampler.UTilingMode = firstTex.Sampler.UTilingMode;
                tex.Sampler.VTilingMode = firstTex.Sampler.VTilingMode;
            }

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

        private async void ColorsetImage_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            await OpenColorset();
        }

        private async void EditColorset_Click(object sender, RoutedEventArgs e)
        {
            await OpenColorset();
        }

        private async Task AddNewMaterial()
        {
            try
            {
                string mdl = null;
                var ivc = GetItemControlParent();

                if(ivc != null && ivc.ModelWrapper != null && ivc.ModelWrapper.FileControl != null && ivc.ModelWrapper.FileControl.HasFile)
                {
                    // Yoink.
                    mdl = ivc.ModelWrapper.FileControl.InternalFilePath;
                }

                var result = await CreateMaterialDialog.ShowCreateMaterialDialogSimple(Material, mdl, Window.GetWindow(this));
                if (result == null)
                {
                    return;
                }


                // Load the new data at the new path.
                var data = Mtrl.XivMtrlToUncompressedMtrl(result);
                await SimpleFileViewWindow.OpenFile(result.MTRLPath, ReferenceItem, data, typeof(MaterialFileControl), Window.GetWindow(this));
            }
            catch (Exception ex)
            {
                Trace.WriteLine(ex);
            }
        }
        private async Task OpenColorset()
        {
            try
            {
                if (Material.ColorSetDataSize > 0)
                {
                    byte[] data = null;

                    if (UnsavedChanges)
                    {
                        data = Mtrl.XivMtrlToUncompressedMtrl(Material);
                    }

                    await SimpleFileViewWindow.OpenFile(Material.MTRLPath, ReferenceItem, data, typeof(ColorsetFileControl), Window.GetWindow(this));
                }
            }
            catch
            {
                // No-Op
            }
        }

        public override bool HasAdditionalSaveFunctions()
        {
            return true;
        }

        public override async Task<List<(string Name, Func<Task> Function, bool Enabled)>> GetAdditionalSaveFunctions()
        {
            var list = new List<(string Name, Func<Task> Function, bool Enabled)>();

            var tx = MainWindow.DefaultTransaction;

            var item = ReferenceItem;
            if(item == null)
            {
                var root = await XivCache.GetFirstRoot(InternalFilePath);
                if(root != null)
                {
                    item = root.GetFirstItem();
                }
            }

            var enabled = false;
            var im = item as IItemModel;
            var exists = await tx.FileExists(InternalFilePath);
            if(exists && im != null && Imc.UsesImc(im))
            {
                enabled = true;
            }

            list.Add(("Save to all Material Sets".L(), SaveAllVersions, enabled));

            return list;
        }

        public async Task SaveAllVersions()
        {
            try
            {
                await Mtrl.ImportMtrlToAllVersions(Material, ReferenceItem as IItemModel, XivStrings.TexTools, MainWindow.UserTransaction);
            }
            catch(Exception ex)
            {
                this.ShowError("Save Error", "An error occurred while saving the materials:\n\n" + ex.Message);
            }
        }

        private async void AddMaterial_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                await AddNewMaterial();
            }
            catch(Exception ex)
            {
                Trace.WriteLine(ex);
            }
        }

        private void WrapAll_Click(object sender, RoutedEventArgs e)
        {
            foreach (var t in Material.Textures)
            {
                if (t.Sampler == null) continue;
                t.Sampler.UTilingMode = TextureSampler.ETilingMode.Wrap;
                t.Sampler.VTilingMode = TextureSampler.ETilingMode.Wrap;
            }
            UnsavedChanges = true;
        }

        private void MirrorAll_Click(object sender, RoutedEventArgs e)
        {
            foreach(var t in Material.Textures)
            {
                if (t.Sampler == null) continue;
                t.Sampler.UTilingMode = TextureSampler.ETilingMode.Mirror;
                t.Sampler.VTilingMode = TextureSampler.ETilingMode.Mirror;
            }
            UnsavedChanges = true;
        }
        private void ClampAll_Click(object sender, RoutedEventArgs e)
        {
            foreach (var t in Material.Textures)
            {
                if (t.Sampler == null) continue;
                t.Sampler.UTilingMode = TextureSampler.ETilingMode.Clamp;
                t.Sampler.VTilingMode = TextureSampler.ETilingMode.Clamp;
            }
            UnsavedChanges = true;
        }
        private void BorderAll_Click(object sender, RoutedEventArgs e)
        {
            foreach (var t in Material.Textures)
            {
                if (t.Sampler == null) continue;
                t.Sampler.UTilingMode = TextureSampler.ETilingMode.Border;
                t.Sampler.VTilingMode = TextureSampler.ETilingMode.Border;
            }
            UnsavedChanges = true;
        }

        private void SetTilingMode_Click(object sender, RoutedEventArgs e)
        {
            TilingModeContextMenu.PlacementTarget = TilingModeButton;
            TilingModeContextMenu.IsOpen = true;
        }
    }
}
