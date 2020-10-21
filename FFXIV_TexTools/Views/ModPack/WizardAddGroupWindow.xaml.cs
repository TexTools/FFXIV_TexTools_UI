// FFXIV TexTools
// Copyright © 2019 Rafael Gonzalez - All Rights Reserved
// 
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
// 
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
// 
// You should have received a copy of the GNU General Public License

using FFXIV_TexTools.Helpers;
using FFXIV_TexTools.Models;
using FFXIV_TexTools.Properties;
using FFXIV_TexTools.Resources;
using FFXIV_TexTools.Views.Models;
using MahApps.Metro.Controls;
using MahApps.Metro.Controls.Dialogs;
using Newtonsoft.Json;
using SixLabors.ImageSharp.Formats.Bmp;
using SixLabors.ImageSharp.Formats.Png;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using xivModdingFramework.Cache;
using xivModdingFramework.General.Enums;
using xivModdingFramework.Helpers;
using xivModdingFramework.Items.Categories;
using xivModdingFramework.Items.DataContainers;
using xivModdingFramework.Items.Interfaces;
using xivModdingFramework.Materials.FileTypes;
using xivModdingFramework.Models.FileTypes;
using xivModdingFramework.Mods;
using xivModdingFramework.Mods.DataContainers;
using xivModdingFramework.Mods.FileTypes;
using xivModdingFramework.SqPack.FileTypes;
using xivModdingFramework.Textures.DataContainers;
using xivModdingFramework.Textures.Enums;
using xivModdingFramework.Textures.FileTypes;
using xivModdingFramework.Variants.FileTypes;
using Image = SixLabors.ImageSharp.Image;

namespace FFXIV_TexTools.Views
{
    /// <summary>
    /// Interaction logic for WizardAddGroupWindow.xaml
    /// </summary>
    public partial class WizardAddGroupWindow
    {
        private List<ModOption> _modOptions;
        private ModOption _selectedModOption;
        private readonly DirectoryInfo _gameDirectory;
        private readonly DirectoryInfo _modListDirectory;
        private readonly List<string> _groupNames;
        private string _editGroupName;
        private bool _editMode;
        private IItem SelectedItem;

        public WizardAddGroupWindow(List<string> groupNames)
        {
            InitializeComponent();
            _modOptions = new List<ModOption>();
            _gameDirectory = new DirectoryInfo(Properties.Settings.Default.FFXIV_Directory);
            _modListDirectory = new DirectoryInfo(Path.Combine(_gameDirectory.Parent.Parent.FullName, XivStrings.ModlistFilePath));
            _groupNames = groupNames;

            ItemList.ExtraSearchFunction = Filter;
            ItemList.ItemSelected += ItemList_ItemSelected;

            ItemList.LockUiFunction = LockUi;
            ItemList.UnlockUiFunction = UnlockUi;

            ModListGrid.IsEnabled = false;
            OptionDescription.IsEnabled = false;
            OptionImageButton.IsEnabled = false;
            RemoveOptionButton.IsEnabled = false;
        }

        private ProgressDialogController _lockProgressController;
        private IProgress<string> _lockProgress;

        public async Task LockUi(string title, string message, object sender)
        {
            _lockProgressController = await this.ShowProgressAsync("Loading", "Please Wait...");

            _lockProgressController.SetIndeterminate();

            _lockProgress = new Progress<string>((update) =>
            {
                _lockProgressController.SetMessage(update);
            });
        }
        public async Task UnlockUi(object sender)
        {
            await _lockProgressController.CloseAsync();
            _lockProgressController = null;
            _lockProgress = null;
        }

        /// <summary>
        /// Extra search filter criterion.  Lets us filter out unsupported items.
        /// </summary>
        private bool Filter(IItem item)
        {
            return true;
        }

        #region Public Methods


        /// <summary>
        /// Sets up the wizard page for edit mode
        /// </summary>
        /// <param name="modGroup">The mod group that will be edited</param>
        public void EditMode(ModGroup modGroup)
        {
            ModGroupTitle.Text = modGroup.GroupName;
            _editGroupName = modGroup.GroupName;

            _editMode = true;

            if (modGroup.SelectionType.Equals("Single"))
            {
                SingleSelectRadioButton.IsChecked = true;
            }
            else
            {
                MultiSelectRadioButton.IsChecked = true;
            }

            _modOptions.AddRange(modGroup.OptionList);

            foreach (var modOption in modGroup.OptionList)
            {
                OptionList.Items.Add(modOption.Name);
            }
        }

        /// <summary>
        /// Gets the resulting mod group
        /// </summary>
        /// <returns>The mod group</returns>
        public ModGroup GetResults()
        {
            var selectionType = "Single";

            if (MultiSelectRadioButton.IsChecked == true)
            {
                selectionType = "Multi";
            }


            foreach (var modOption in _modOptions)
            {
                modOption.GroupName = ModGroupTitle.Text;
                modOption.SelectionType = selectionType;
            }

            var modGroup = new ModGroup
            {
                GroupName = ModGroupTitle.Text,
                SelectionType = selectionType,
                OptionList = _modOptions
            };

            return modGroup;
        }

        /// <summary>
        /// Updates the mod group after an edit
        /// </summary>
        /// <param name="modGroup">The mod group</param>
        public void UpdateModGroup(ModGroup modGroup)
        {
            var selectionType = "Single";

            if (MultiSelectRadioButton.IsChecked == true)
            {
                selectionType = "Multi";
            }


            foreach (var modOption in _modOptions)
            {
                modOption.GroupName = ModGroupTitle.Text;
                modOption.SelectionType = selectionType;
            }

            modGroup.GroupName = ModGroupTitle.Text;
            modGroup.SelectionType = selectionType;
            modGroup.OptionList = _modOptions;
        }

        #endregion


        #region Private Methods

        /// <summary>
        /// Adds the option to the group
        /// </summary>
        /// <param name="optionText">The option name</param>
        /// <param name="optionNum">The option number</param>
        private void AddOption(string optionText, int optionNum = 0)
        {
            if (String.IsNullOrWhiteSpace(OptionNameTextBox.Text)) return;

            var optionListItems = OptionList.Items.Cast<string>().ToList();

            if (optionListItems.IndexOf(optionText) != -1)
            {
                AddOption($"{OptionNameTextBox.Text} {optionNum + 1}", optionNum + 1);
            }
            else
            {
                var modOption = new ModOption
                {
                    Name = optionText
                };

                _modOptions.Add(modOption);

                OptionList.Items.Add(optionText);
                OptionList.SelectedIndex = OptionList.Items.Count - 1;
            }

            OptionNameTextBox.Text = string.Empty;
        }


        #endregion


        #region Event Handlers

        /// <summary>
        /// The event handler for the add option button clicked
        /// </summary>
        private void AddOptionButton_Click(object sender, RoutedEventArgs e)
        {
            AddOption(OptionNameTextBox.Text);
        }

        /// <summary>
        /// The event handler for the remove options button clicked
        /// </summary>
        private void RemoveOptionButton_Click(object sender, RoutedEventArgs e)
        {
            _selectedModOption =
                (from option in _modOptions
                    where option.Name.Equals(OptionList.SelectedItem.ToString())
                    select option).FirstOrDefault();

            _modOptions.Remove(_selectedModOption);

            foreach (var item in OptionList.Items)
            {
                if (item.ToString().Equals(_selectedModOption.Name))
                {
                    if (OptionList.Items.Count > 0)
                    {
                        OptionList.SelectedIndex = 0;
                    }
                    else
                    {
                        OptionList.SelectedIndex = -1;
                    }
                    OptionList.Items.Remove(item);
                    break;
                }
            }
        }


        /// <summary>
        /// The event handler for the option list selection changed
        /// </summary>
        private void OptionList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            IncludedModsList.Items.Clear();

            if (OptionList.SelectedIndex != -1)
            {
                _selectedModOption =
                    (from option in _modOptions
                     where option.Name.Equals(OptionList.SelectedItem.ToString())
                     select option).FirstOrDefault();

                if (_selectedModOption.Description != null)
                {
                    OptionDescription.Text = _selectedModOption.Description;
                }
                else
                {
                    OptionDescription.Text = string.Empty;
                }

                if (_selectedModOption.Image != null)
                {
                    BitmapImage bmp;

                    using (var ms = new MemoryStream())
                    {
                        _selectedModOption.Image.Save(ms, new BmpEncoder());

                        bmp = new BitmapImage();
                        bmp.BeginInit();
                        bmp.StreamSource = ms;
                        bmp.CacheOption = BitmapCacheOption.OnLoad;
                        bmp.EndInit();
                        bmp.Freeze();
                    }

                    OptionImage.Source = bmp;
                }
                else
                {
                    OptionImage.Source = null;
                }

                if (_selectedModOption.Mods != null && _selectedModOption.Mods.Count > 0)
                {
                    foreach (var mod in _selectedModOption.Mods)
                    {
                        var includedMods = new FileEntry
                        {
                            Name = MakeFriendlyFileName(mod.Value.FullPath),
                            Path = mod.Value.FullPath
                        };

                        IncludedModsList.Items.Add(includedMods);
                    }
                }

                ModListGrid.IsEnabled = true;
                OptionDescription.IsEnabled = true;
                OptionImageButton.IsEnabled = true;
                RemoveOptionButton.IsEnabled = true;
            }
            else
            {
                ModListGrid.IsEnabled = false;
                OptionDescription.IsEnabled = false;
                OptionImageButton.IsEnabled = false;
                RemoveOptionButton.IsEnabled = false;
            }

            if (SelectedItem != null)
            {
                ItemList_ItemSelected(this, SelectedItem);
            }

        }


        /// <summary>
        /// The event handler for the option description loosing focus
        /// </summary>
        private void OptionDescription_LostFocus(object sender, RoutedEventArgs e)
        {
            _selectedModOption.Description = OptionDescription.Text;
        }

        /// <summary>
        /// The event handler for the option image button clicked
        /// </summary>
        private void OptionImageButton_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new OpenFileDialog
            {
                Filter = "Image Files(*.BMP;*.JPG;*.GIF;*.PNG)|*.BMP;*.JPG;*.GIF;*.PNG"
            };


            if (openFileDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                _selectedModOption.Image = Image.Load(openFileDialog.FileName);
                _selectedModOption.ImageFileName = openFileDialog.FileName;
                OptionImage.Source = new BitmapImage(new Uri(openFileDialog.FileName));
            }
        }

        private void ItemList_ItemSelected(object sender, IItem item)
        {
            if(item == null)
            {
                return;
            }

            SelectedItem = item;

            var root = item.GetRoot();

            TextureMapComboBox.Items.Clear();
            ModelTypeComboBox.Items.Clear();
            MaterialComboBox.Items.Clear();

            if (OptionList.SelectedItem == null)
            {
                return;
            }


            var modding = new Modding(_gameDirectory);
            //var modList = modding.GetModList();
            var _imc = new Imc(_gameDirectory);

            int mSet = -1;
            string metadataFile = null;
            var models = new List<string>();
            var materials = new List<string>();
            var textures = new List<string>();
            Task.Run(async () =>
            {
                if (root != null)
                {
                    // Get ALL THE THINGS
                    // Meta Entries, Models, Materials, Textures, Icons, and VFX Elements.
                    var im = (IItemModel)item;
                    var df = IOUtil.GetDataFileFromPath(root.Info.GetRootFile());

                    metadataFile = root.Info.GetRootFile();
                    mSet = await _imc.GetMaterialSetId(im);
                    models = await root.GetModelFiles();
                    materials = await root.GetMaterialFiles(mSet);
                    textures = await root.GetTextureFiles(mSet);

                    var _tex = new Tex(XivCache.GameInfo.GameDirectory);
                    var icons = await _tex.GetItemIcons(im);

                    foreach (var icon in icons)
                    {
                        textures.Add(icon.Path);
                    }

                    var _atex = new ATex(XivCache.GameInfo.GameDirectory, df);
                    var paths = await _atex.GetAtexPaths(im);
                    foreach (var path in paths)
                    {
                        textures.Add(path.Path);
                    }
                }
                else
                {
                    if (item.GetType() == typeof(XivCharacter))
                    {
                        // Face Paint/Equipment Decals jank-items.  Ugh.
                        if (item.SecondaryCategory == XivStrings.Face_Paint)
                        {
                            var _character = new Character(XivCache.GameInfo.GameDirectory, XivCache.GameInfo.GameLanguage);

                            var paths = await _character.GetDecalPaths(Character.XivDecalType.FacePaint);

                            foreach (var path in paths)
                            {
                                textures.Add(path);
                            }

                        }
                        else if (item.SecondaryCategory == XivStrings.Equipment_Decals)
                        {
                            var _character = new Character(XivCache.GameInfo.GameDirectory, XivCache.GameInfo.GameLanguage);
                            var paths = await _character.GetDecalPaths(Character.XivDecalType.Equipment);
                            foreach (var path in paths)
                            {
                                textures.Add(path);
                            }
                        }
                    }
                    else
                    {
                        // This is a UI item or otherwise an item which has no root, and only has textures.
                        var uiItem = (XivUi)item;
                        var paths = await uiItem.GetTexPaths();
                        foreach (var kv in paths)
                        {
                            textures.Add(kv.Value);
                        }
                    }
                }
            }).Wait();

            MetadataPathBox.Text = metadataFile;

            foreach(var file in models)
            {
                var fe = new FileEntry();
                fe.Path = file;
                fe.Name = MakeFriendlyFileName(file);

                ModelTypeComboBox.Items.Add(fe);
            }

            foreach (var file in materials)
            {
                var fe = new FileEntry();
                fe.Path = file;
                fe.Name = MakeFriendlyFileName(file);

                MaterialComboBox.Items.Add(fe);
            }

            foreach (var file in textures)
            {
                var fe = new FileEntry();
                fe.Path = file;
                fe.Name = MakeFriendlyFileName(file);

                TextureMapComboBox.Items.Add(fe);
            }

            if(String.IsNullOrEmpty(metadataFile))
            {
                AddMetadataButton.IsEnabled = false;
            } else
            {
                AddMetadataButton.IsEnabled = true;
            }

            if (TextureMapComboBox.Items.Count > 0)
            {
                AddCurrentTextureButton.IsEnabled = true;
                AddCustomTextureButton.IsEnabled = true;
                TextureMapComboBox.SelectedIndex = 0;
            }
            else
            {
                AddCurrentTextureButton.IsEnabled = false;
                AddCustomTextureButton.IsEnabled = false;
            }

            if (ModelTypeComboBox.Items.Count > 0)
            {
                AddCurrentModelButton.IsEnabled = true;
                AdvOptionsButton.IsEnabled = true;
                ModelTypeComboBox.SelectedIndex = 0;

            }
            else
            {
                AddCurrentModelButton.IsEnabled = false;
                AdvOptionsButton.IsEnabled = false;
            }

            if (MaterialComboBox.Items.Count > 0)
            {
                AddCurrentMaterialButton.IsEnabled = true;
                MaterialComboBox.SelectedIndex = 0;
            }
            else
            {
                AddCurrentMaterialButton.IsEnabled = false;
            }

            SelectModGroup.IsEnabled = true;
        }





        private async Task AddFile(FileEntry file, IItem item, byte[] rawData = null)
        {
            var dat = new Dat(_gameDirectory);
            var index = new Index(_gameDirectory);

            if (file == null || file.Path == null || _selectedModOption == null) return;

            var includedModsList = IncludedModsList.Items.Cast<FileEntry>().ToList();

            if (includedModsList.Any(f => f.Path.Equals(file.Path)))
            {
                if (FlexibleMessageBox.Show(
                        string.Format(UIMessages.ExistingOption, file.Name),
                        UIMessages.OverwriteTitle, MessageBoxButtons.YesNo, MessageBoxIcon.Question) !=
                    System.Windows.Forms.DialogResult.Yes)
                {
                    return;
                }
            }

            if (rawData == null)
            {
                var df = IOUtil.GetDataFileFromPath(file.Path);
                var offset = await index.GetDataOffset(file.Path);
                if (offset <= 0)
                {
                    FlexibleMessageBox.Show("Cannot include file, file offset invalid.",
                        UIMessages.ModDataReadErrorTitle, MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
                var size = await dat.GetCompressedFileSize(offset, df);
                rawData = dat.GetRawData(offset, df, size);

                if (rawData == null)
                {
                    FlexibleMessageBox.Show("Cannot include file, file offset invalid.",
                        UIMessages.ModDataReadErrorTitle, MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
            }

            
            if(_selectedModOption.Mods.ContainsKey(file.Path))
            {
                _selectedModOption.Mods[file.Path].ModDataBytes = rawData;
            } else
            {
                IncludedModsList.Items.Add(file);
                var modData = new ModData
                {
                    Name = item.Name,
                    Category = item.SecondaryCategory,
                    FullPath = file.Path,
                    ModDataBytes = rawData,
                };
                _selectedModOption.Mods.Add(file.Path, modData);
            }

        }

        private async Task AddWithChildren(string file, IItem item, byte[] rawData = null)
        {
            var children = new HashSet<string>();
            if (Path.GetExtension(file) == ".meta")
            {
                // If we're the root file, use the proper get all function
                // which will throw in the AVFX/ATEX stuff as well.
                var root = await XivCache.GetFirstRoot(file);
                if(root != null)
                {
                    var files = await root.GetAllFiles();
                    children = files.ToHashSet();
                }
            } else
            {
                children = await XivCache.GetChildrenRecursive(file);
            }


            foreach (var child in children)
            {
                var fe = new FileEntry() { Name = MakeFriendlyFileName(child), Path = child };
                if (child == file && rawData != null)
                {
                    await AddFile(fe, item, rawData);
                } else 
                {
                    await AddFile(fe, item);
                }
            }
        }

        /// <summary>
        /// The event handler for the current texture button clicked
        /// </summary>
        private void AddCurrentTextureButton_Click(object sender, RoutedEventArgs e)
        {
            var selectedFile = TextureMapComboBox.SelectedItem as FileEntry;
            AddFile(selectedFile, SelectedItem);
        }

        /// <summary>
        /// The event handler for the add custom texture button clicked
        /// </summary>
        private async void AddCustomTextureButton_Click(object sender, RoutedEventArgs e)
        {
            var selectedFile = TextureMapComboBox.SelectedItem as FileEntry;

            var openFileDialog = new OpenFileDialog { Filter = "Texture Files(*.DDS;*.BMP;*.PNG) |*.DDS;*.BMP;*.PNG" };


            var result = openFileDialog.ShowDialog();

            if (result != System.Windows.Forms.DialogResult.OK) return;
                ;

            var _tex = new Tex(XivCache.GameInfo.GameDirectory);

            try
            {
                var data = await _tex.MakeTexData(selectedFile.Path, openFileDialog.FileName);
                await AddFile(selectedFile, SelectedItem, data);
            }
            catch (Exception ex)
            {
                FlexibleMessageBox.Show(
                    string.Format(UIMessages.TextureImportErrorMessage, ex.Message), UIMessages.TextureImportErrorTitle,
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
        }

        /// <summary>
        /// The event handler for the add current material button clicked
        /// </summary>
        private void AddCurrentMaterialButton_Click(object sender, RoutedEventArgs e)
        {
            var selectedFile = MaterialComboBox.SelectedItem as FileEntry;
            var addChildren = MaterialIncludeChildrenBox.IsChecked == true ? true : false;
            if (addChildren)
            {
                AddWithChildren(selectedFile.Path, SelectedItem);
            }
            else
            {
                AddFile(selectedFile, SelectedItem);
            }
        }

        /// <summary>
        /// The event handler for the current model button clicked
        /// </summary>
        private void AddCurrentModelButton_Click(object sender, RoutedEventArgs e)
        {
            var selectedFile = ModelTypeComboBox.SelectedItem as FileEntry;
            var addChildren = ModelIncludeChildFilesBox.IsChecked == true ? true : false;
            if (addChildren)
            {
                AddWithChildren(selectedFile.Path, SelectedItem);
            }
            else
            {
                AddFile(selectedFile, SelectedItem);
            }
        }

        /// <summary>
        /// The event handler for the advanced options button clicked
        /// </summary>
        private async void AdvOptionsButton_Click(object sender, RoutedEventArgs e)
        {
            var selectedFile = ModelTypeComboBox.SelectedItem as FileEntry;
            var itemModel = (IItemModel) SelectedItem;
            var mdl = new Mdl(_gameDirectory, IOUtil.GetDataFileFromPath(selectedFile.Path));
            try
            {
                // TODO - Include Submesh ID ?
                // Do we even have any kind of UI To specify this in the wizard?
                // Submeshes are only used for Furniture anyways, so it might be a 'will not fix'
                bool success = await ImportModelView.ImportModel(itemModel, IOUtil.GetRaceFromPath(selectedFile.Path), null, this, null, true);
                if (!success)
                {
                    return;
                }
            }
            catch (Exception ex)
            {
                FlexibleMessageBox.Show(ex.Message, UIMessages.AdvancedImportErrorTitle,
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            var mdlData = ImportModelView.GetData();
            AddFile(selectedFile, SelectedItem, mdlData);
        }

        /// <summary>
        /// The event handler for the remove mod item button clicked
        /// </summary>
        private void RemoveModItemButton_Click(object sender, RoutedEventArgs e)
        {
            if (IncludedModsList.SelectedItem == null) return;
            _selectedModOption.Mods.Remove(((FileEntry)IncludedModsList.SelectedItem).Path);
            IncludedModsList.Items.Remove(IncludedModsList.SelectedItem);
        }

        /// <summary>
        /// The event handler for the mod list selection changed
        /// </summary>
        private void IncludedModsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            RemoveModItemButton.IsEnabled = true;
        }

        /// <summary>
        /// The event handler for the done button clicked
        /// </summary>
        private void DoneButton_Click(object sender, RoutedEventArgs e)
        {
            if (ModGroupTitle.Text.Equals(string.Empty))
            {
                ModGroupTitle.Focus();
                ModGroupTitle.BorderBrush = Brushes.Red;
                ControlsHelper.SetFocusBorderBrush(ModGroupTitle, Brushes.Red);
            }
            else if (!_editMode && _groupNames.Contains(ModGroupTitle.Text) || _editMode && !_editGroupName.Equals(ModGroupTitle.Text) && _groupNames.Contains(ModGroupTitle.Text))
            {
                ModGroupTitle.Focus();
                ModGroupTitle.BorderBrush = Brushes.Red;
                ControlsHelper.SetFocusBorderBrush(ModGroupTitle, Brushes.Red);

                FlexibleMessageBox.Show(
                    $"\"{ModGroupTitle.Text}\" {UIMessages.ExistingGroupMessage}",
                    UIMessages.ExistingGroupTitle, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            else
            {
                DialogResult = true;
                Close();
            }

        }


        /// <summary>
        /// The event handler for metro window closing
        /// </summary>
        private void MetroWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (DialogResult != true)
            {
                if (_modOptions.Count > 0)
                {
                    foreach (var modOption in _modOptions)
                    {
                        if (modOption.Image != null)
                        {
                            modOption.Image.Dispose();
                        }
                    }
                }

                _modOptions = null;
            }
        }

        #endregion


        private class FileEntry
        {
            /// <summary>
            /// Display Name
            /// </summary>
            public string Name { get; set; }

            /// <summary>
            /// The underlying file.
            /// </summary>
            public string Path { get; set; }
        }

        private void OptionNameTextBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == Key.Return)
            {
                AddOption(OptionNameTextBox.Text);
                e.Handled = true;
            }
        }

        /// <summary>
        /// Gets the language for the application
        /// </summary>
        /// <returns>The application language as XivLanguage</returns>
        private static XivLanguage GetLanguage()
        {
            return XivLanguages.GetXivLanguage(Properties.Settings.Default.Application_Language);
        }

        private async void AddMetadataButton_Click(object sender, RoutedEventArgs e)
        {
            var path = MetadataPathBox.Text;
            var selectedFile = new FileEntry()
            {
                Path = path,
                Name = MakeFriendlyFileName(path)
            };
            var addChildren = MetadataIncludeChildFilesBox.IsChecked == true ? true : false;

            var _dat = new Dat(_gameDirectory);
            var meta = await ItemMetadata.GetMetadata(path);
            var data = await _dat.CreateType2Data(await ItemMetadata.Serialize(meta));

            if (addChildren)
            {
                AddWithChildren(selectedFile.Path, SelectedItem, data);
            }
            else
            {
                AddFile(selectedFile, SelectedItem, data);
            }

        }

        public static string MakeFriendlyFileName(string path)
        {
            var filename = Path.GetFileName(path);
            var ext = Path.GetExtension(path);
            string niceName = null;
            if(ext == ".mtrl")
            {
                // Include material set identifier for materials.
                var rex = new Regex("v[0-9]{4}/");
                var m = rex.Match(path);

                if(m.Success)
                {
                    filename = m.Value + filename;
                }

                niceName = "Material";
            } else if(ext == ".atex")
            {
                niceName = "VFX";
            } else if(ext == ".mdl")
            {
                niceName = "Model";
            } else if(ext == ".tex")
            {
                var type = SimpleModpackEntry.GuessTextureUsage(path);
                niceName = type.ToString() + " Texture";
            } else if(ext == ".meta")
            {
                niceName = "Metadata";
            }



            var ret = filename;
            if(niceName != null)
            {
                ret = niceName + " (" + filename + ")";
            }
            return ret;
        }
    }
}
