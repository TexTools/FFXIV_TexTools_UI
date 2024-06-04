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

using AutoUpdaterDotNET;
using FFXIV_TexTools.Helpers;
using FFXIV_TexTools.Models;
using FFXIV_TexTools.Properties;
using FFXIV_TexTools.Resources;
using FFXIV_TexTools.Views.Models;
using FFXIV_TexTools.Views.Wizard;
using MahApps.Metro.Controls;
using MahApps.Metro.Controls.Dialogs;
using Newtonsoft.Json;
using SixLabors.ImageSharp;
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
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using TeximpNet;
using TeximpNet.DDS;
using xivModdingFramework.Cache;
using xivModdingFramework.Exd.FileTypes;
using xivModdingFramework.General.Enums;
using xivModdingFramework.Helpers;
using xivModdingFramework.Items.Categories;
using xivModdingFramework.Items.DataContainers;
using xivModdingFramework.Items.Interfaces;
using xivModdingFramework.Materials.DataContainers;
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
    public partial class EditWizardGroupWindow
    {
        private string _editGroupName;
        private IItem SelectedItem;

        private List<byte> _basicModpackData;

        private WizardGroupEntry Data;

        private WizardOptionEntry SelectedOption;

        private int _LockCount = 0;

        private int LockCount
        {
            get => _LockCount;
            set
            {
                if(value < 0)
                {
                    value = 0;
                }

                _LockCount = value;
                if(LockCount > 0)
                {
                    this.IsEnabled = false;
                }
                else
                {
                    this.IsEnabled = true;
                }
            }
        }

        public EditWizardGroupWindow(WizardGroupEntry data)
        { 
            Data = data;
            InitializeComponent();

            ItemList.ExtraSearchFunction = Filter;
            ItemList.ItemSelected += ItemList_ItemSelected;

            ItemList.LockUiFunction = LockUi;
            ItemList.UnlockUiFunction = UnlockUi;

            ModListGrid.IsEnabled = false;
            OptionDescription.IsEnabled = false;
            OptionImageButton.IsEnabled = false;
            RemoveOptionButton.IsEnabled = false;
            RenameOptionButton.IsEnabled = false;
            MoveOptionUpButton.IsEnabled = false;
            MoveOptionDownButton.IsEnabled = false;

            PriorityBox.Text = Data.Priority.ToString();


            ModGroupTitle.Text = Data.Name;


            if (Data.OptionType == EOptionType.Single)
            {
                SingleSelectRadioButton.IsChecked = true;
            }
            else
            {
                MultiSelectRadioButton.IsChecked = true;
            }

            RebuildOptionList();
        }

        private void RebuildOptionList()
        {
            var opt = SelectedOption;
            OptionList.Items.Clear();

            EditableOptionControl target = null;
            foreach (var option in Data.Options)
            {
                var uiOp = new EditableOptionControl(option);
                OptionList.Items.Add(uiOp);
                if(option == opt)
                {
                    target = uiOp;
                }
            }

            if(target != null)
            {
                OptionList.SelectedItem = target;
            } else
            {
                if (OptionList.Items.Count > 0)
                {
                    OptionList.SelectedIndex = 0;
                }
            }
        }

        private ProgressDialogController _lockProgressController;

        public async Task LockUi(string title = null, string message = null, object sender = null)
        {
            _lockProgressController = await this.ShowProgressAsync("Loading".L(), "Please Wait...".L());

            _lockProgressController.SetIndeterminate();

        }
        public async Task UnlockUi(object sender = null)
        {
            await _lockProgressController.CloseAsync();
            _lockProgressController = null;
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
        /// Updates basic group settings based on UI state.
        /// </summary>
        public void UpdateGroupMeta()
        {
            var selectionType = EOptionType.Single;

            if (MultiSelectRadioButton.IsChecked == true)
            {
                selectionType = EOptionType.Multi;
            }

            Data.Name = ModGroupTitle.Text;
            Data.OptionType = selectionType;

            var s = Int32.TryParse(PriorityBox.Text, out var prio);
            if (s)
            {
                Data.Priority = prio;
            }
        }

        #endregion


        #region Private Methods

        /// <summary>
        /// Adds the option to the group
        /// </summary>
        /// <param name="optionText">The option name</param>
        /// <param name="optionNum">The option number</param>
        private void AddOption(string optionText)
        {
            if (String.IsNullOrWhiteSpace(optionText)) return;

            var option = new WizardOptionEntry(Data);
            option.Name = optionText;

            Data.Options.Add(option);

            RebuildOptionList();
            OptionList.SelectedIndex = OptionList.Items.Count - 1;
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
            if(SelectedOption == null)
            {
                return;
            }

            Data.Options.Remove(SelectedOption);

            RebuildOptionList();
        }

        /// <summary>
        /// The event handler for the rename options button clicked
        /// </summary>
        private void RenameOptionButton_Click(object sender, RoutedEventArgs e)
        {
            if (OptionList.SelectedItem != null)
            {
                (OptionList.SelectedItem as EditableOptionControl).EditMode();
            }
        }

        /// <summary>
        /// The event handler for the move option up button clicked
        /// </summary>
        private void MoveOptionUpButton_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedOption != null && OptionList.SelectedIndex > 0)
            {
                var oldIndex = OptionList.SelectedIndex;
                var newIndex = OptionList.SelectedIndex - 1;

                var optionA = Data.Options[oldIndex];
                var optionB = Data.Options[newIndex];

                Data.Options[oldIndex] = optionB;
                Data.Options[newIndex] = optionA;

                RebuildOptionList();
                OptionList.SelectedIndex = newIndex;
            }
        }

        /// <summary>
        /// The event handler for the move option down button clicked
        /// </summary>
        private void MoveOptionDownButton_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedOption != null && OptionList.SelectedIndex < (OptionList.Items.Count - 1))
            {
                var oldIndex = OptionList.SelectedIndex;
                var newIndex = OptionList.SelectedIndex + 1;

                var optionA = Data.Options[oldIndex];
                var optionB = Data.Options[newIndex];

                Data.Options[oldIndex] = optionB;
                Data.Options[newIndex] = optionA;

                RebuildOptionList();
                OptionList.SelectedIndex = newIndex;
            }
        }


        /// <summary>
        /// The event handler for the option list selection changed
        /// </summary>
        private void OptionList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            LockCount++;
            try
            {
                IncludedModsList.Items.Clear();

                var uiOpt = OptionList.SelectedItem as EditableOptionControl;
                if (uiOpt == null)
                {
                    SelectedOption = null;
                    ModListGrid.IsEnabled = false;
                    OptionDescription.IsEnabled = false;
                    OptionImageButton.IsEnabled = false;
                    RemoveOptionButton.IsEnabled = false;
                    MoveOptionUpButton.IsEnabled = false;
                    MoveOptionDownButton.IsEnabled = false;
                    RenameOptionButton.IsEnabled = false;
                    OptionImage.Source = null;
                    EditManipulationsButton.IsEnabled = false;
                    return;
                }


                SelectedOption = uiOpt.Option;
                OptionDescription.Text = SelectedOption.Description ?? "";

                OptionImage.Source = null;
                if (!string.IsNullOrWhiteSpace(SelectedOption.Image))
                {
                    OptionImage.Source = ViewHelpers.SafeBitmapFromFile(SelectedOption.Image);
                }

                var opData = SelectedOption.StandardData;
                if (opData != null)
                {
                    foreach (var fileKv in opData.Files)
                    {
                        var includedMods = new FileEntry
                        {
                            Name = MakeFriendlyFileName(fileKv.Key),
                            Path = fileKv.Key
                        };

                        IncludedModsList.Items.Add(includedMods);
                    }

                    if (Data.Options.Count > 1)
                    {
                        // Enable the move up button only when the option isn't already first
                        if (OptionList.SelectedIndex > 0)
                        {
                            MoveOptionUpButton.IsEnabled = true;
                        }
                        else
                        {
                            MoveOptionUpButton.IsEnabled = false;
                        }

                        // Enable the move down button only when the option isn't already last
                        if (OptionList.SelectedIndex < (OptionList.Items.Count - 1))
                        {
                            MoveOptionDownButton.IsEnabled = true;
                        }
                        else
                        {
                            MoveOptionDownButton.IsEnabled = false;
                        }
                    }
                    else
                    {
                        MoveOptionUpButton.IsEnabled = false;
                        MoveOptionDownButton.IsEnabled = false;
                    }
                    UpdateManipulationText();
                }

                ModListGrid.IsEnabled = true;
                OptionDescription.IsEnabled = true;
                OptionImageButton.IsEnabled = true;
                RemoveOptionButton.IsEnabled = true;
                RenameOptionButton.IsEnabled = true;
                EditManipulationsButton.IsEnabled = true;

                if (SelectedItem != null)
                {
                    ItemList_ItemSelected(this, SelectedItem);
                }
            }
            finally
            {
                LockCount--;
            }
        }


        /// <summary>
        /// The event handler for the option description loosing focus
        /// </summary>
        private void OptionDescription_LostFocus(object sender, RoutedEventArgs e)
        {
            if (SelectedOption == null) return;

            SelectedOption.Description = OptionDescription.Text;
        }

        /// <summary>
        /// The event handler for the option image button clicked
        /// </summary>
        private void OptionImageButton_Click(object sender, RoutedEventArgs e)
        {
            var res = ViewHelpers.LoadUserImage(this);
            SelectedOption.Image = res.File;
            OptionImage.Source = res.Image;
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



            var tx = MainWindow.DefaultTransaction;
            int mSet = -1;
            string metadataFile = null;
            var models = new List<string>();
            var materials = new List<string>();
            var textures = new List<string>();
            Task.Run(async () =>
            {
                try
                {
                    if (root != null)
                    {
                        // Get ALL THE THINGS
                        // Meta Entries, Models, Materials, Textures, Icons, and VFX Elements.
                        var im = (IItemModel)item;
                        var df = IOUtil.GetDataFileFromPath(root.Info.GetRootFile());


                        metadataFile = root.Info.GetRootFile();
                        mSet = await Imc.GetMaterialSetId(im, false, tx);
                        models = await root.GetModelFiles(tx);
                        materials = await root.GetMaterialFiles(mSet, tx);
                        textures = await root.GetTextureFiles(mSet, tx);

                        var icons = await Tex.GetItemIcons(im, tx);

                        foreach (var icon in icons)
                        {
                            textures.Add(icon);
                        }

                        var paths = await ATex.GetAtexPaths(im, false, tx);
                        foreach (var path in paths)
                        {
                            textures.Add(path);
                        }
                    }
                    else
                    {
                        if (item.GetType() == typeof(XivCharacter))
                        {
                            // Face Paint/Equipment Decals jank-items.  Ugh.
                            if (item.SecondaryCategory == XivStrings.Face_Paint)
                            {
                                var paths = await Character.GetDecalPaths(Character.XivDecalType.FacePaint, tx);

                                foreach (var path in paths)
                                {
                                    textures.Add(path);
                                }

                            }
                            else if (item.SecondaryCategory == XivStrings.Equipment_Decals)
                            {
                                var paths = await Character.GetDecalPaths(Character.XivDecalType.Equipment, tx);
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
                            var paths = await uiItem.GetTexPaths(true, true, tx);
                            foreach (var kv in paths)
                            {
                                textures.Add(kv);
                            }
                        }
                    }
                }
                catch(Exception ex)
                {
                    await Dispatcher.InvokeAsync(() =>
                    {
                        this.ShowError("Item Load Error", "An error occurred while loading the item:\n\n" + ex.Message);
                        return;
                    });
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
                AddNewMetadata.IsEnabled = false;
            } else
            {
                AddMetadataButton.IsEnabled = true;
                AddNewMetadata.IsEnabled = true;
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
                AddRawModel.IsEnabled = true;
                ModelTypeComboBox.SelectedIndex = 0;

            }
            else
            {
                AddCurrentModelButton.IsEnabled = false;
                AddRawModel.IsEnabled = false;
                AdvOptionsButton.IsEnabled = false;
            }

            if (MaterialComboBox.Items.Count > 0)
            {
                AddCurrentMaterialButton.IsEnabled = true;
                AddWithCustomColorsetButton.IsEnabled = true;
                AddCustomMaterialButton.IsEnabled = true;
                MaterialComboBox.SelectedIndex = 0;
            }
            else
            {
                AddCurrentMaterialButton.IsEnabled = false;
                AddWithCustomColorsetButton.IsEnabled = false;
                AddCustomMaterialButton.IsEnabled = false;
            }

            SelectModGroup.IsEnabled = true;
        }





        private async Task AddFile(FileEntry file, FileStorageInformation? storageHandle = null)
        {
            if(SelectedOption == null)
            {
                return;
            }
            if (file == null || file.Path == null || SelectedOption == null) return;

            LockCount++;
            try
            {
                var includedModsList = IncludedModsList.Items.Cast<FileEntry>().ToList();

                if (includedModsList.Any(f => f.Path.Equals(file.Path)))
                {
                    if (FlexibleMessageBox.Show(new Wpf32Window(this),
                            string.Format(UIMessages.ExistingOption, file.Name),
                            UIMessages.OverwriteTitle, MessageBoxButtons.YesNo, MessageBoxIcon.Question) !=
                        System.Windows.Forms.DialogResult.Yes)
                    {
                        return;
                    }
                }

                var data = SelectedOption.StandardData;

                if (storageHandle == null)
                {
                    try
                    {
                        var tx = MainWindow.DefaultTransaction;
                        storageHandle = await tx.UNSAFE_GetStorageInfo(file.Path);
                    }
                    catch (Exception ex)
                    {
                        this.ShowError("File Add Error", "An error occurred while adding the file:\n\n" + ex.Message);
                        return;
                    }
                }

                if (data.Files.ContainsKey(file.Path))
                {
                    data.Files[file.Path] = storageHandle.Value;
                }
                else
                {
                    IncludedModsList.Items.Add(file);
                    data.Files.Add(file.Path, storageHandle.Value);
                }
            }
            finally
            {
                LockCount--;
            }

        }

        private async Task AddWithChildren(string file, FileStorageInformation? storageHandle = null, bool skipSelf = false)
        {
            LockCount++;
            try
            {
                var tx = MainWindow.DefaultTransaction;
                var children = new HashSet<string>();
                if (Path.GetExtension(file) == ".meta")
                {
                    // If we're the root file, use the proper get all function
                    // which will throw in the AVFX/ATEX stuff as well.
                    var root = await XivCache.GetFirstRoot(file);
                    if (root != null)
                    {
                        var files = await root.GetAllFiles();
                        children = files.ToHashSet();
                    }
                }
                else
                {
                    children = await XivCache.GetChildrenRecursive(file, tx);
                }


                foreach (var child in children)
                {
                    if (child == file && skipSelf) continue;

                    var fe = new FileEntry() { Name = MakeFriendlyFileName(child), Path = child };
                    if (child == file && storageHandle != null)
                    {
                        await AddFile(fe, storageHandle.Value);
                    }
                    else
                    {
                        try
                        {
                            var info = await tx.UNSAFE_GetStorageInfo(child);
                            await AddFile(fe, info);
                        }
                        catch (Exception ex)
                        {
                            this.ShowError("File Add Error", "An error occurred while adding a file:\n" + child + "\n\n" + ex.Message);
                        }
                    }
                }
            }
            finally
            {
                LockCount--;
            }
        }
        /// <summary>
        /// The event handler for the current texture button clicked
        /// </summary>
        private void AddCurrentTextureButton_Click(object sender, RoutedEventArgs e)
        {
            var selectedFile = TextureMapComboBox.SelectedItem as FileEntry;
            _ = AddFile(selectedFile);
        }

        private FileStorageInformation WriteTempFile(byte[] data, bool compressed = false)
        {
            LockCount++;
            try
            {
                var tempPath = Path.GetTempFileName();
                File.WriteAllBytes(tempPath, data);

                var info = new FileStorageInformation()
                {
                    RealPath = tempPath,
                    RealOffset = 0,
                    FileSize = data.Length,
                    StorageType = EFileStorageType.UncompressedIndividual,
                };
                if (compressed)
                {
                    info.StorageType = EFileStorageType.CompressedIndividual;
                }

                return info;
            }
            finally
            {
                LockCount--;
            }
        }

        /// <summary>
        /// The event handler for the add custom texture button clicked
        /// </summary>
        private async void AddCustomTextureButton_Click(object sender, RoutedEventArgs e)
        {
            var selectedFile = TextureMapComboBox.SelectedItem as FileEntry;

            var openFileDialog = new OpenFileDialog { Filter = "Texture Files|*.DDS;*.BMP;*.PNG;*.TEX;*.TGA".L() };


            var result = openFileDialog.ShowDialog();

            if (result != System.Windows.Forms.DialogResult.OK) return;

            LockCount++;
            try
            {
                var data = await SmartImport.CreateUncompressedFile(openFileDialog.FileName, selectedFile.Path, MainWindow.DefaultTransaction);
                var info = WriteTempFile(data);

                await AddFile(selectedFile, info);
            }
            catch (Exception ex)
            {
                FlexibleMessageBox.Show(new Wpf32Window(this),
                    string.Format(UIMessages.TextureImportErrorMessage, ex.Message), UIMessages.TextureImportErrorTitle,
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            finally
            {
                LockCount--;
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
                _ = AddWithChildren(selectedFile.Path);
            }
            else
            {
                _ = AddFile(selectedFile);
            }
        }
        private async void AddWithCustomColorsetButton_Click(object sender, RoutedEventArgs e)
        {
            var selectedFile = MaterialComboBox.SelectedItem as FileEntry;
            var addChildren = MaterialIncludeChildrenBox.IsChecked == true ? true : false;

            LockCount++;
            try
            {
                var mtrlData = new byte[0];
                var mtrl = await Mtrl.GetXivMtrl(selectedFile.Path, false, MainWindow.DefaultTransaction);

                var ddsPath = "";

                var od = new OpenFileDialog();
                od.Filter = "DDS Files (*.DDS)|*.DDS";
                var res = od.ShowDialog();
                if(res != System.Windows.Forms.DialogResult.OK)
                {
                    return;
                }

                ddsPath = od.FileName;

                // Read and assign the data.
                Tex.ImportColorsetTexture(mtrl, ddsPath);

                mtrlData = Mtrl.XivMtrlToUncompressedMtrl(mtrl);

                var info = WriteTempFile(mtrlData);


                if (addChildren)
                {
                    await AddWithChildren(selectedFile.Path, info);
                }
                else
                {
                    await AddFile(selectedFile, info);
                }

            } catch(Exception ex)
            {
                ViewHelpers.ShowError("Colorset Import Error".L(), "Unable to Import Colorset:\n\n".L() + ex.Message);
            }
            finally
            {
                LockCount--;
            }


        }
        private async void AddCustomMaterialButton_Click(object sender, RoutedEventArgs e)
        {
            var selectedFile = MaterialComboBox.SelectedItem as FileEntry;

            var openFileDialog = new OpenFileDialog { Filter = "Material Files|*.mtrl".L() };


            var result = openFileDialog.ShowDialog();

            if (result != System.Windows.Forms.DialogResult.OK) return;


            LockCount++;
            try
            {
                var data = await SmartImport.CreateUncompressedFile(openFileDialog.FileName, selectedFile.Path, MainWindow.DefaultTransaction);

                var info = WriteTempFile(data);
                await AddFile(selectedFile, info);
            }
            catch (Exception ex)
            {
                FlexibleMessageBox.Show(new Wpf32Window(this),
                    string.Format(UIMessages.TextureImportErrorMessage, ex.Message), UIMessages.TextureImportErrorTitle,
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            finally
            {
                LockCount--;
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
                _ = AddWithChildren(selectedFile.Path);
            }
            else
            {
                _ = AddFile(selectedFile);
            }
        }

        /// <summary>
        /// The event handler for the advanced options button clicked
        /// </summary>
        private async void AdvOptionsButton_Click(object sender, RoutedEventArgs e)
        {
            var selectedFile = ModelTypeComboBox.SelectedItem as FileEntry;
            var itemModel = (IItemModel) SelectedItem;
            LockCount++;
            try
            {
                var result = await ImportModelView.ImportModel(selectedFile.Path, itemModel, true, null, Window.GetWindow(this));
                if (!result.Success)
                {
                    return;
                }

                var info = WriteTempFile(result.Data);
                await AddFile(selectedFile, info);
            }
            catch (Exception ex)
            {
                FlexibleMessageBox.Show(new Wpf32Window(this), ex.Message, UIMessages.AdvancedImportErrorTitle,
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            finally
            {
                LockCount--;
            }

        }

        /// <summary>
        /// The event handler for the remove mod item button clicked
        /// </summary>
        private void RemoveModItemButton_Click(object sender, RoutedEventArgs e)
        {
            // Enumeration struggles...
            var items = new List<FileEntry>();
            foreach(FileEntry item in IncludedModsList.SelectedItems)
            {
                items.Add(item);
            }

            var opData = SelectedOption.StandardData;
            if(opData == null)
            {
                return;
            }

            foreach (FileEntry item in items)
            {
                opData.Files.Remove(item.Path);
                IncludedModsList.Items.Remove(item);
            }
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
            UpdateGroupMeta();
            DialogResult = true;
        }


        /// <summary>
        /// The event handler for metro window closing
        /// </summary>
        private void MetroWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            // Does any disposing need to happen here?
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

        private void AddMetadataManipulations(ItemMetadata metadata)
        {
            if (SelectedOption == null) return;


            if (SelectedOption.StandardData.Manipulations == null)
            {
                SelectedOption.StandardData.Manipulations = new List<PMPManipulationWrapperJson>();
            }

            LockCount++;
            try
            {
                var toRemove = new List<PMPManipulationWrapperJson>();
                var manips = SelectedOption.StandardData.Manipulations;

                foreach (var m in manips)
                {
                    var im = m.GetManipulation() as IPMPItemMetadata;
                    if (im == null) continue;
                    var mRoot = im.GetRoot();
                    if (mRoot == metadata.Root)
                    {
                        toRemove.Add(m);
                    }
                }

                foreach (var m in toRemove)
                {
                    manips.Remove(m);
                }

                var newManips = PMPExtensions.MetadataToManipulations(metadata);
                manips.AddRange(newManips);
                SelectedOption.StandardData.SortManipulations();
                UpdateManipulationText();
            }
            finally
            {
                LockCount--;
            }
        }

        private async void AddMetadataButton_Click(object sender, RoutedEventArgs e)
        {
            LockCount++;
            try
            {
                if (SelectedOption == null) return;
                var path = MetadataPathBox.Text;
                var selectedFile = new FileEntry()
                {
                    Path = path,
                    Name = MakeFriendlyFileName(path)
                };
                var addChildren = MetadataIncludeChildFilesBox.IsChecked == true ? true : false;


                var meta = await ItemMetadata.GetMetadata(path, false, MainWindow.DefaultTransaction);
                AddMetadataManipulations(meta);


                if (addChildren)
                {
                    await AddWithChildren(selectedFile.Path, null, true);
                }
            }
            catch(Exception ex)
            {
                this.ShowError("Add File Error".L(), "An error occurred while adding the file:\n\n".L() + ex.Message);
            }
            finally
            {
                LockCount--;
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

                niceName = "Material".L();
            } else if(ext == ".atex")
            {
                niceName = "VFX";
            } else if(ext == ".mdl")
            {
                niceName = "Model".L();
            } else if(ext == ".tex")
            {
                var type = SimpleModpackEntry.GuessTextureUsage(path);
                niceName = $"{type.ToString()._()} Texture".L();
            } else if(ext == ".meta")
            {
                niceName = "Metadata".L();
            }



            var ret = filename;
            if(niceName != null)
            {
                ret = niceName + " (" + filename + ")";
            }
            return ret;
        }

        private async void LoadSimpleModpackButton_Click(object sender, RoutedEventArgs e)
        {

            var openFileDialog = new OpenFileDialog
            {
                Filter = "Basic Modpack|*.ttmp2;*.ttmp;*.pmp".L()
            };

            if (openFileDialog.ShowDialog() != System.Windows.Forms.DialogResult.OK)
            {
                SelectAllButton.IsEnabled = false;
                DeselectAllButton.IsEnabled = false;
                return;
            }

            ModpackContents.Items.Clear();

            LockCount++;
            try
            {
                var files = await TTMP.ModPackToSimpleFileList(openFileDialog.FileName, true, MainWindow.UserTransaction);
                if(files == null || files.Count == 0)
                {
                    SelectAllButton.IsEnabled = false;
                    DeselectAllButton.IsEnabled = false;
                    return;
                }

                var ordered = files.OrderBy(x => x.Key);
                foreach(var f in ordered)
                {
                    var entry = new TinyFileListEntry()
                    {
                        FilePath = f.Key,
                        Name = MakeFriendlyFileName(f.Key),
                        StorageInfo = f.Value
                    };
                    ModpackContents.Items.Add(entry);
                }

                SelectAllButton.IsEnabled = true;
                DeselectAllButton.IsEnabled = true;

            }
            catch(Exception ex)
            {
                this.ShowError("Load Error", "An error occurred while loading the file:\n\n" + ex.Message);
            }
            finally
            {
                LockCount--;
            }
        }

        private void SelectAllButton_Click(object sender, RoutedEventArgs e)
        {
            ModpackContents.SelectAll();
        }

        private void DeselectAllButton_Click(object sender, RoutedEventArgs e)
        {
            ModpackContents.UnselectAll();
        }

        private void AddButton_Click(object sender, RoutedEventArgs e)
        {
            foreach (var i in ModpackContents.SelectedItems)
            {
                var item = (TinyFileListEntry)i;
                var fi = new FileEntry()
                {
                    Name = item.Name,
                    Path = item.FilePath
                };

                _ = AddFile(fi, item.StorageInfo);
            }
        }

        private void ModpackContents_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            AddButton.IsEnabled = ModpackContents.SelectedItems.Count > 0;
        }

        private async void AddRawModel_Click(object sender, RoutedEventArgs e)
        {
            var selectedFile = ModelTypeComboBox.SelectedItem as FileEntry;

            // TODO: This should check the actual avaialable importers list from Mdl
            var openFileDialog = new OpenFileDialog { Filter = "Model Files|*.mdl;*.fbx;*.obj;*.db".L() };


            var result = openFileDialog.ShowDialog();

            if (result != System.Windows.Forms.DialogResult.OK) return;

            LockCount++;
            try
            {
                var data = await SmartImport.CreateUncompressedFile(openFileDialog.FileName, selectedFile.Path, MainWindow.DefaultTransaction);
                var info = WriteTempFile(data);

                await AddFile(selectedFile, info);
            }
            catch (Exception ex)
            {
                FlexibleMessageBox.Show(new Wpf32Window(this),
                    string.Format(UIMessages.TextureImportErrorMessage, ex.Message), UIMessages.TextureImportErrorTitle,
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            finally
            {
                LockCount--;
            }
        }

        private async void AddNewMetadata_Click(object sender, RoutedEventArgs e)
        {
            var path = MetadataPathBox.Text;
            var selectedFile = new FileEntry()
            {
                Path = path,
                Name = MakeFriendlyFileName(path)
            };

            var openFileDialog = new OpenFileDialog { Filter = "FFXIV Raw Metadata Files|*.meta".L() };


            var result = openFileDialog.ShowDialog();

            if (result != System.Windows.Forms.DialogResult.OK) return;

            LockCount++;
            try
            {
                // Slightly more complex.  Because Metadata files have internal root path references,
                // We need to import them, then alter them.
                var root = await XivCache.GetFirstRoot(path);
                var fileData = File.ReadAllBytes(openFileDialog.FileName);

                uint fileType = 0;
                using (var br = new BinaryReader(new MemoryStream(fileData)))
                {
                    fileType = Dat.GetSqPackType(br);
                }

                if(fileType == 2)
                {
                    // Compressed file.  Normally we never export .meta files as compressed sqpack files,
                    // But it's not impossible to if the user really wanted to.
                    fileData = await Dat.ReadSqPackType2(fileData);
                }


                var metadata = await ItemMetadata.Deserialize(fileData);
                metadata.AlterRoot(root);

                AddMetadataManipulations(metadata);
            }
            catch (Exception ex)
            {
                FlexibleMessageBox.Show(new Wpf32Window(this),
                    string.Format(UIMessages.TextureImportErrorMessage, ex.Message), UIMessages.TextureImportErrorTitle,
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            finally
            {
                LockCount--;
            }
        }

        private void EditManipulations_Click(object sender, RoutedEventArgs e)
        {
            if(SelectedOption == null) return;

            SelectedOption.StandardData.SortManipulations();

            var data = SelectedOption.StandardData;
            var wind = new ManipulationEditorWindow(data);
            wind.Owner = this;
            wind.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            wind.ShowDialog();

            SelectedOption.StandardData.SortManipulations();
            UpdateManipulationText();
        }
        private void UpdateManipulationText()
        {
            if(SelectedOption == null || SelectedOption.StandardData == null)
            {
                return;
            }

            if (SelectedOption.StandardData.Manipulations != null)
            {
                ManipulationCountLabel.Content = SelectedOption.StandardData.Manipulations.Count() + " " + "Manipulation(s)".L();
            }
            else
            {
                ManipulationCountLabel.Content = "0 " + "Manipulation(s)".L();
            }
        }
    }

    public class TinyFileListEntry
    {
        public string FilePath { get; set; }
        public string Name { get; set; }
        public FileStorageInformation StorageInfo { get; set; }
    }
}
