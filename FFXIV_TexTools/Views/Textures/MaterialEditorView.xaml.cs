using FFXIV_TexTools.Helpers;
using FFXIV_TexTools.ViewModels;
using FFXIV_TexTools.Views.Controls;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using xivModdingFramework.Cache;
using xivModdingFramework.Exd.FileTypes;
using xivModdingFramework.General.Enums;
using xivModdingFramework.Items.Interfaces;
using xivModdingFramework.Materials.DataContainers;
using xivModdingFramework.Materials.FileTypes;
using xivModdingFramework.Mods;
using xivModdingFramework.SqPack.FileTypes;
using xivModdingFramework.Textures.Enums;
using static xivModdingFramework.Materials.DataContainers.ShaderHelpers;
using Button = System.Windows.Controls.Button;

namespace FFXIV_TexTools.Views.Textures
{
    public enum MaterialEditorMode
    {
        // Simple single-edit mode.  The most common option.
        EditSingle,
        
        // Used when editing all variants of a material simultaneously.
        EditMulti,

        // Used when adding a new racial skin.
        NewRace,

        // Used when adding a new material to a gear set.
        // Triggers adding the material to all variants of the gear for safety.
        NewMulti
    }
    /// <summary>
    /// Interaction logic for MaterialEditor.xaml
    /// </summary>
    public partial class MaterialEditorView
    {
        public class WrappedTexture : INotifyPropertyChanged
        {
            public event PropertyChangedEventHandler PropertyChanged;
            public MtrlTexture Texture { get; set; }

            public MaterialEditorView View;

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

            public WrappedTexture(MtrlTexture tex, MaterialEditorView view)
            {
                Texture = tex;
                View = view;
            }
        }

        private MaterialEditorViewModel viewModel;
        private IItemModel _item;
        private MaterialEditorMode _mode;

        public ObservableCollection<KeyValuePair<string, EShaderPack>> ShaderSource;

        public ObservableCollection<WrappedTexture> TextureSource;
        private static XivMtrl _copiedMaterial;

        private XivMtrl _Material;
        public XivMtrl Material
        {
            get
            {
                return _Material;
            }
        }

        public MaterialEditorView()
        {
            InitializeComponent();
            viewModel = new MaterialEditorViewModel(this);
            TextureSource = new ObservableCollection<WrappedTexture>();

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
            if(material == null)
            {
                Close();
                return false;
            }
            material = (XivMtrl) material.Clone();
            _Material = material;
            _item = item;
            _mode = mode;

            ShaderComboBox.SelectedValue = Material.ShaderPack;
            UpdateTextureList();
            return await viewModel.SetMaterial(material, item, mode);
        }


        /// <summary>
        /// Updates the visual texture collection.
        /// </summary>
        public void UpdateTextureList()
        {
            TextureSource = new ObservableCollection<WrappedTexture>();
            foreach(var tx in Material.Textures)
            {
                var wt = new WrappedTexture(tx, this);
                TextureSource.Add(wt);
            }
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
            _Material = await viewModel.SaveChanges();
            Close(true);
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            Close(false);
        }

        private EShaderPack _LastShpk;

        private void ShaderComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            Material.ShaderPack = (EShaderPack)((ComboBox)sender).SelectedValue;
            if (ShaderComboBox.SelectedValue == null)
            {
                _LastShpk = Material.ShaderPack;
                return;
            }
            if(_LastShpk == EShaderPack.Invalid || Material.ShaderPack == EShaderPack.Invalid)
            {
                // Don't mess with anything if we were on a broken state before, or are transitioning to one.
                _LastShpk = Material.ShaderPack;
                return;
            }

            var lastDes = ShaderHelpers.GetEnumDescription(_LastShpk);
            var curDes = ShaderHelpers.GetEnumDescription(Material.ShaderPack);

            var lastWithoutLegacy = lastDes.Replace("legacy", "");
            var curWithoutLegacy = curDes.Replace("legacy", "");

            if(lastWithoutLegacy == curWithoutLegacy)
            {
                // If just flipping between legacy modes, leave the shader keys/constants intact.
                // We don't know exactly which of them is valid or not on each mode, so from a use frustration standpoint it makes more sense to keep them,
                // Rather than wipe them and force the user to manually remake.
                _LastShpk = Material.ShaderPack;
                return;
            }

            // Reset shader vars.
            Material.ShaderKeys = new List<ShaderKey>();
            Material.ShaderConstants = new List<ShaderConstant>();

            _LastShpk = Material.ShaderPack;
        }
        private void HelpButton_Click(object sender, RoutedEventArgs e)
        {
            var help = new Views.Textures.MaterialEditorHelpView();
            help.ShowDialog();
        }

        private void CopyMaterialButton_Click(object sender, RoutedEventArgs e)
        {
            _copiedMaterial = (XivMtrl) Material.Clone();
            PasteMaterialButton.IsEnabled = true;
        }

        private async void PasteMaterialButton_Click(object sender, RoutedEventArgs e)
        {
            if (_copiedMaterial != null)
            {
                // Paste the copied Material into the editor using our current path and item.
                _copiedMaterial.MTRLPath = Material.MTRLPath;
                await SetMaterial(_copiedMaterial, _item, _mode);
            }
        }

        private async void DisableButton_Click(object sender, RoutedEventArgs e)
        {
            await viewModel.DisableMod();
        }

        private void NewSharedButton_Click(object sender, RoutedEventArgs e)
        {
            foreach (var tex in Material.Textures)
            {
                var path = Material.GetTextureRootDirectoy() + "/" + Material.GetDefaultTexureName(tex.Usage, false);
                tex.TexturePath = path;
            }
            UpdateTextureList();
        }

        private void NewUniqueButton_Click(object sender, RoutedEventArgs e)
        {
            foreach (var tex in Material.Textures)
            {
                var path = Material.GetTextureRootDirectoy() + "/" + Material.GetDefaultTexureName(tex.Usage, true);
                tex.TexturePath = path;
            }
            UpdateTextureList();
        }

        private void TexturePathBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            //var tb = (TextBox)sender;
            //var tex = (WrappedTexture)tb.DataContext;
            //tex.TexturePath = tb.Text;
        }

        private void SavePresetButton_Click(object sender, RoutedEventArgs e)
        {
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
            var result = LoadPresetDialog.ShowLoadPresetDialog(Material);
            if(result != null)
            {
                await SetMaterial(result, _item, _mode);
            }

        }

        private void EditShaderFlags_Click(object sender, RoutedEventArgs e)
        {
            var result = MaterialFlagsEditor.ShowFlagsEditor(Material, this);
            if(result)
            {
                // Don't really need to do anything here, since the editor handles updating the material.
            }
        }
        private void EditShaderKeys_Click(object sender, RoutedEventArgs e)
        {
            var result = ShaderKeysEditor.ShowKeysEditor(Material, this);
            if(result == true)
            {
                // Don't really need to do anything here, since the editor handles updating the material.
            }
        }

        private void EditShaderConstants_Click(object sender, RoutedEventArgs e)
        {
            var result = ShaderConstantsEditor.ShowConstantsEditor(Material, this);
            if(result == true)
            {
                // Don't really need to do anything here, since the editor handles updating the material.
            }
        }

        private void EditUsage_Click(object sender, RoutedEventArgs e)
        {
            var tex = (WrappedTexture)((Button)sender).DataContext;
            var result = TextureSamplerSettings.ShowSamplerSettings(Material, tex.Texture, this);
            if(result == true)
            {
                UpdateTextureList();
            }
        }
        
        private void RemoveTexture_Click(object sender, RoutedEventArgs e)
        {
            var tex = (WrappedTexture) ((Button)sender).DataContext;
            Material.Textures.Remove(tex.Texture);
            UpdateTextureList();
        }
        private void AddTexture_Click(object sender, RoutedEventArgs e)
        {
            var tex = new MtrlTexture();
            tex.Sampler = new TextureSampler();
            tex.Sampler.SamplerId = ESamplerId.g_SamplerNormal;
            Material.Textures.Add(tex);
            UpdateTextureList();
        }

        private async void LoadRawButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new System.Windows.Forms.OpenFileDialog();
            dialog.Filter = "Material Files (*.mtrl)|*.mtrl|All Files (*.*)|*.*";
            if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK)
                return;
            try
            {
                var bytes = File.ReadAllBytes(dialog.FileName);
                await SetMaterial(Mtrl.GetXivMtrl(bytes, _Material.MTRLPath), _item, _mode);
            }
            catch (Exception ex)
            {
                FlexibleMessageBox.Show("Unable to load file:".L() + ex.Message, "Material Save Error".L(), System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Error);
            }
        }

        private void SaveRawButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new System.Windows.Forms.SaveFileDialog();
            dialog.Filter = "Material Files (*.mtrl)|*.mtrl|All Files (*.*)|*.*";
            dialog.FileName = Path.GetFileName(Material.MTRLPath);
            if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK)
                return;

            try
            {
                var bytes = Mtrl.XivMtrlToUncompressedMtrl(Material);
                File.WriteAllBytes(dialog.FileName, bytes);
            } catch(Exception ex)
            {
                FlexibleMessageBox.Show("Unable to save file:".L() + ex.Message, "Material Save Error".L(), System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Error);
            }
        }

        private async void SaveModpack_Click(object sender, RoutedEventArgs e)
        {
            // Because the user likely expects the outgoing modpack to reflect their changes
            // (Same behavior as the other save functions), we should save the changes to a TX here and then restore state after export.

            var tx = MainWindow.UserTransaction;
            bool ownTx = false;
            TxFileState state = null;
            if(tx == null)
            {
                ownTx = true;
                tx = ModTransaction.BeginTransaction(true);
            }
            try
            {

                state = await tx.SaveFileState(_Material.MTRLPath);

                await Mtrl.ImportMtrl(Material, null, "Temp", false, tx);

                SingleFileModpackCreator.ExportFile(_Material.MTRLPath, this, tx);
            }
            finally
            {
                if (ownTx)
                {
                    ModTransaction.CancelTransaction(tx, true);
                } else
                {
                    await tx.RestoreFileState(state);
                }
            }
        }
    }
}
