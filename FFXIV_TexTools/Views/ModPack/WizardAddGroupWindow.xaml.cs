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
using ImageMagick;
using MahApps.Metro.Controls;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media;
using xivModdingFramework.General.Enums;
using xivModdingFramework.Items.DataContainers;
using xivModdingFramework.Items.Interfaces;
using xivModdingFramework.Materials.FileTypes;
using xivModdingFramework.Models.FileTypes;
using xivModdingFramework.Mods.DataContainers;
using xivModdingFramework.SqPack.FileTypes;
using xivModdingFramework.Textures.DataContainers;
using xivModdingFramework.Textures.Enums;
using xivModdingFramework.Textures.FileTypes;

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

        public WizardAddGroupWindow(List<string> groupNames)
        {
            InitializeComponent();
            _modOptions = new List<ModOption>();
            _gameDirectory = new DirectoryInfo(Properties.Settings.Default.FFXIV_Directory);
            _modListDirectory = new DirectoryInfo(Path.Combine(_gameDirectory.Parent.Parent.FullName, XivStrings.ModlistFilePath));
            _groupNames = groupNames;

            FillModListTreeView();
            ModListGrid.IsEnabled = false;
            OptionDescription.IsEnabled = false;
            OptionImageButton.IsEnabled = false;
            SelectModGroup.IsEnabled = false;
            RemoveModItemButton.IsEnabled = false;
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

            var modOptionsToRemove = new List<ModOption>();

            foreach (var modOption in _modOptions)
            {
                if (modOption.Mods.Count < 1)
                {
                    modOptionsToRemove.Add(modOption);
                    continue;
                }

                modOption.GroupName = ModGroupTitle.Text;
                modOption.SelectionType = selectionType;
            }

            foreach (var modOption in modOptionsToRemove)
            {
                _modOptions.Remove(modOption);
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

            var modOptionsToRemove = new List<ModOption>();

            foreach (var modOption in _modOptions)
            {
                if (modOption.Mods.Count < 1)
                {
                    modOptionsToRemove.Add(modOption);
                    continue;
                }

                modOption.GroupName = ModGroupTitle.Text;
                modOption.SelectionType = selectionType;
            }

            foreach (var modOption in modOptionsToRemove)
            {
                _modOptions.Remove(modOption);
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
            if (OptionNameTextBox.Text.Equals(string.Empty)) return;

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


        /// <summary>
        /// Fills the mod list tree view
        /// </summary>
        private void FillModListTreeView()
        {
            var modList = JsonConvert.DeserializeObject<ModList>(File.ReadAllText(_modListDirectory.FullName));

            var Categories = new ObservableCollection<Category>();

            if (modList == null) return;

            // Mods
            var mainCategories = new HashSet<string>();

            foreach (var modEntry in modList.Mods)
            {
                if (!modEntry.name.Equals(string.Empty))
                {
                    mainCategories.Add(modEntry.category);
                }
            }

            foreach (var mainCategory in mainCategories)
            {
                var category = new Category
                {
                    Name = mainCategory,
                    Categories = new ObservableCollection<Category>(),
                    CategoryList = new List<string>()
                };

                var modItems =
                    from mod in modList.Mods
                    where mod.category.Equals(mainCategory)
                    select mod;

                foreach (var modItem in modItems)
                {
                    if (category.CategoryList.Contains(modItem.name)) continue;

                    var categoryItem = new Category
                    {
                        Name = modItem.name,
                        Item = MakeItemModel(modItem),
                        ParentCategory = category
                    };

                    category.Categories.Add(categoryItem);
                    category.CategoryList.Add(modItem.name);

                }

                Categories.Add(category);
            }

            ModListTreeView.ItemsSource = Categories;
        }

        /// <summary>
        /// Makes a generic item model from the mod item
        /// </summary>
        /// <param name="modItem">The mod item</param>
        /// <returns>The mod item as a XivGenericItemModel</returns>
        private static XivGenericItemModel MakeItemModel(Mod modItem)
        {
            var fullPath = modItem.fullPath;

            var item = new XivGenericItemModel
            {
                Name = modItem.name,
                ItemCategory = modItem.category,
                DataFile = XivDataFiles.GetXivDataFile(modItem.datFile)
            };


            if (modItem.fullPath.Contains("chara/equipment") || modItem.fullPath.Contains("chara/accessory"))
            {
                item.Category = XivStrings.Gear;
                item.ModelInfo = new XivModelInfo
                {
                    ModelID = int.Parse(fullPath.Substring(17, 4))
                };
            }


            if (modItem.fullPath.Contains("chara/weapon"))
            {
                item.Category = XivStrings.Gear;
                item.ModelInfo = new XivModelInfo
                {
                    ModelID = int.Parse(fullPath.Substring(14, 4))
                };
            }

            if (modItem.fullPath.Contains("chara/human"))
            {
                item.Category = XivStrings.Character;

                if (item.Name.Equals(XivStrings.Body))
                {
                    item.ModelInfo = new XivModelInfo
                    {
                        ModelID = int.Parse(fullPath.Substring(fullPath.IndexOf("/body", StringComparison.Ordinal) + 7, 4))
                    };
                }
                else if (item.Name.Equals(XivStrings.Hair))
                {
                    item.ModelInfo = new XivModelInfo
                    {
                        ModelID = int.Parse(fullPath.Substring(fullPath.IndexOf("/hair", StringComparison.Ordinal) + 7, 4))
                    };
                }
                else if (item.Name.Equals(XivStrings.Face))
                {
                    item.ModelInfo = new XivModelInfo
                    {
                        ModelID = int.Parse(fullPath.Substring(fullPath.IndexOf("/face", StringComparison.Ordinal) + 7, 4))
                    };
                }
                else if (item.Name.Equals(XivStrings.Tail))
                {
                    item.ModelInfo = new XivModelInfo
                    {
                        ModelID = int.Parse(fullPath.Substring(fullPath.IndexOf("/tail", StringComparison.Ordinal) + 7, 4))
                    };
                }
            }

            if (modItem.fullPath.Contains("chara/common"))
            {
                item.Category = XivStrings.Character;

                if (item.Name.Equals(XivStrings.Face_Paint))
                {
                    item.ModelInfo = new XivModelInfo
                    {
                        ModelID = int.Parse(fullPath.Substring(fullPath.LastIndexOf("_", StringComparison.Ordinal) + 1, 1))
                    };
                }
                else if (item.Name.Equals(XivStrings.Equip_Decals))
                {
                    if (!fullPath.Contains("_stigma"))
                    {
                        item.ModelInfo = new XivModelInfo
                        {
                            ModelID = int.Parse(fullPath.Substring(fullPath.LastIndexOf("_", StringComparison.Ordinal) + 1, 3))
                        };
                    }
                }
            }

            if (modItem.fullPath.Contains("chara/monster"))
            {
                item.Category = XivStrings.Companions;

                item.ModelInfo = new XivModelInfo
                {
                    ModelID = int.Parse(fullPath.Substring(15, 4)),
                    Body = int.Parse(fullPath.Substring(fullPath.IndexOf("/body", StringComparison.Ordinal) + 7, 4))
                };
            }

            if (modItem.fullPath.Contains("chara/demihuman"))
            {
                item.Category = XivStrings.Companions;

                item.ModelInfo = new XivModelInfo
                {
                    Body = int.Parse(fullPath.Substring(17, 4)),
                    ModelID = int.Parse(fullPath.Substring(fullPath.IndexOf("t/e", StringComparison.Ordinal) + 3, 4))
                };
            }

            if (modItem.fullPath.Contains("ui/"))
            {
                item.Category = XivStrings.UI;

                if (modItem.fullPath.Contains("ui/uld") || modItem.fullPath.Contains("ui/map"))
                {
                    item.ModelInfo = new XivModelInfo
                    {
                        ModelID = 0
                    };
                }
                else
                {
                    item.ModelInfo = new XivModelInfo
                    {
                        ModelID = int.Parse(fullPath.Substring(fullPath.LastIndexOf("/", StringComparison.Ordinal) + 1,
                            6))
                    };
                }
            }

            if (modItem.fullPath.Contains("/hou/"))
            {
                item.Category = XivStrings.Housing;

                item.ModelInfo = new XivModelInfo
                {
                    ModelID = int.Parse(fullPath.Substring(fullPath.LastIndexOf("_m", StringComparison.Ordinal) + 2, 3))
                };
            }

            return item;
        }


        /// <summary>
        /// Gets the race from the path
        /// </summary>
        /// <param name="modPath">The mod path</param>
        /// <returns>The race as XivRace</returns>
        private XivRace GetRace(string modPath)
        {
            var xivRace = XivRace.All_Races;

            if (modPath.Contains("ui/") || modPath.Contains(".avfx"))
            {
                xivRace = XivRace.All_Races;
            }
            else if (modPath.Contains("monster"))
            {
                xivRace = XivRace.Monster;
            }
            else if (modPath.Contains(".tex") || modPath.Contains(".mdl") || modPath.Contains(".atex"))
            {
                if (modPath.Contains("accessory") || modPath.Contains("weapon") || modPath.Contains("/common/"))
                {
                    xivRace = XivRace.All_Races;
                }
                else
                {
                    if (modPath.Contains("demihuman"))
                    {
                        xivRace = XivRace.DemiHuman;
                    }
                    else if (modPath.Contains("/v"))
                    {
                        var raceCode = modPath.Substring(modPath.IndexOf("_c") + 2, 4);
                        xivRace = XivRaces.GetXivRace(raceCode);
                    }
                    else
                    {
                        var raceCode = modPath.Substring(modPath.IndexOf("/c") + 2, 4);
                        xivRace = XivRaces.GetXivRace(raceCode);
                    }
                }

            }

            return xivRace;
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
                    OptionImage.Source = _selectedModOption.Image.ToBitmapSource();
                }
                else
                {
                    OptionImage.Source = null;
                }

                if (_selectedModOption.Mods != null && _selectedModOption.Mods.Count > 0)
                {
                    foreach (var mod in _selectedModOption.Mods)
                    {
                        var includedMods = new IncludedMods
                        {
                            Name = $"{Path.GetFileNameWithoutExtension(mod.Key)} ({mod.Value.Name})",
                            FullPath = mod.Value.FullPath
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
                var magickImage = new MagickImage(openFileDialog.FileName);
                _selectedModOption.Image = magickImage;
                OptionImage.Source = magickImage.ToBitmapSource();
            }
        }

        /// <summary>
        /// The event handler for the tree view item selection changed
        /// </summary>
        private void TreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            TextureMapComboBox.Items.Clear();
            ModelTypeComboBox.Items.Clear();
            MaterialComboBox.Items.Clear();
            CustomTextureTextBox.Text = string.Empty;
            CustomModelTextBox.Text = string.Empty;

            var selectedItem = e.NewValue as Category;

            var modList = JsonConvert.DeserializeObject<ModList>(File.ReadAllText(_modListDirectory.FullName));

            var modItems =
                from mod in modList.Mods
                where mod.name.Equals(selectedItem.Name)
                select mod;

            foreach (var modItem in modItems)
            {
                var itemPath = modItem.fullPath;
                var modCB = new ModComboBox();
                var ttp = new TexTypePath
                {
                    Path = itemPath,
                    DataFile = XivDataFiles.GetXivDataFile(modItem.datFile)
                };

                // Textures
                if (itemPath.Contains("_d."))
                {
                    modCB.Name = $"{XivTexType.Diffuse} ({Path.GetFileNameWithoutExtension(itemPath)})";
                    modCB.SelectedMod = modItem;
                    ttp.Type = XivTexType.Diffuse;
                }
                else if (itemPath.Contains("_n."))
                {
                    modCB.Name = $"{XivTexType.Normal} ({Path.GetFileNameWithoutExtension(itemPath)})";
                    modCB.SelectedMod = modItem;
                    ttp.Type = XivTexType.Normal;
                }
                else if (itemPath.Contains("_s."))
                {
                    modCB.Name = $"{XivTexType.Specular} ({Path.GetFileNameWithoutExtension(itemPath)})";
                    modCB.SelectedMod = modItem;
                    ttp.Type = XivTexType.Specular;
                }
                else if (itemPath.Contains("_m."))
                {
                    modCB.Name = $"{XivTexType.Multi} ({Path.GetFileNameWithoutExtension(itemPath)})";
                    modCB.SelectedMod = modItem;
                    ttp.Type = XivTexType.Multi;
                }
                else if (itemPath.Contains("material"))
                {
                    modCB.Name = $"{XivTexType.ColorSet} ({Path.GetFileNameWithoutExtension(itemPath)})";
                    modCB.SelectedMod = modItem;
                    ttp.Type = XivTexType.ColorSet;
                }
                else if (itemPath.Contains("decal"))
                {
                    modCB.Name = $"{XivTexType.Mask} ({Path.GetFileNameWithoutExtension(itemPath)})";
                    modCB.SelectedMod = modItem;
                    ttp.Type = XivTexType.Mask;
                }
                else if (itemPath.Contains("vfx"))
                {
                    modCB.Name = $"{XivTexType.Vfx} ({Path.GetFileNameWithoutExtension(itemPath)})";
                    modCB.SelectedMod = modItem;
                    ttp.Type = XivTexType.Vfx;
                }
                else if (itemPath.Contains("ui/"))
                {
                    if (itemPath.Contains("icon"))
                    {
                        modCB.Name = $"{XivTexType.Icon} ({Path.GetFileNameWithoutExtension(itemPath)})";
                        modCB.SelectedMod = modItem;
                        ttp.Type = XivTexType.Icon;
                    }
                    else if (itemPath.Contains("map"))
                    {
                        modCB.Name = $"{XivTexType.Map} ({Path.GetFileNameWithoutExtension(itemPath)})";
                        modCB.SelectedMod = modItem;
                        ttp.Type = XivTexType.Map;
                    }
                    else
                    {
                        modCB.Name = $"UI ({Path.GetFileNameWithoutExtension(itemPath)})";
                        modCB.SelectedMod = modItem;
                        ttp.Type = XivTexType.Other;
                    }
                }

                if (modCB.Name != null)
                {
                    modCB.TexTypePath = ttp;
                    TextureMapComboBox.Items.Add(modCB);
                }

                // Models
                if (itemPath.Contains(".mdl"))
                {
                    //esrinzou for Repair program crash when selecting [character/Body] item
                    //modCB.Name = $"{((IItemModel)selectedItem.Item).ModelInfo.ModelID} ({Path.GetFileNameWithoutExtension(itemPath)})";
                    //esrinzou begin
                    if (((IItemModel)selectedItem.Item).ModelInfo == null)
                    {
                        modCB.Name = $"{((IItemModel)selectedItem.Item).Name} ({Path.GetFileNameWithoutExtension(itemPath)})";
                    }
                    else
                    {
                        var modelId = ((IItemModel)selectedItem.Item).ModelInfo.ModelID;

                        if (selectedItem.Item.Category.Equals(XivStrings.Character))
                        {
                            var item = selectedItem.Item;

                            if (item.Name.Equals(XivStrings.Body))
                            {
                                modelId = int.Parse(
                                    itemPath.Substring(itemPath.IndexOf("/body", StringComparison.Ordinal) + 7, 4));
                            }
                            else if (item.Name.Equals(XivStrings.Hair))
                            {
                                modelId = int.Parse(
                                    itemPath.Substring(itemPath.IndexOf("/hair", StringComparison.Ordinal) + 7, 4));
                            }
                            else if (item.Name.Equals(XivStrings.Face))
                            {
                                modelId = int.Parse(
                                    itemPath.Substring(itemPath.IndexOf("/face", StringComparison.Ordinal) + 7, 4));
                            }
                            else if (item.Name.Equals(XivStrings.Tail))
                            {
                                modelId = int.Parse(
                                    itemPath.Substring(itemPath.IndexOf("/tail", StringComparison.Ordinal) + 7, 4));
                            }
                        }

                        modCB.Name = $"{modelId} ({Path.GetFileNameWithoutExtension(itemPath)})";
                    }
                    //esrinzou end
                    modCB.SelectedMod = modItem;
                    modCB.TexTypePath = null;

                    ModelTypeComboBox.Items.Add(modCB);
                }

                // Material
                if (itemPath.Contains(".mtrl"))
                {
                    var materialModCB = new ModComboBox
                    {
                        Name = $"Material ({Path.GetFileNameWithoutExtension(itemPath)})",
                        SelectedMod = modItem,
                        TexTypePath = null
                    };

                    MaterialComboBox.Items.Add(materialModCB);
                    MaterialTabItem.IsEnabled = true;
                }
            }

            if (TextureMapComboBox.Items.Count > 0)
            {
                AddCurrentTextureButton.IsEnabled = true;
                GetCustomTextureButton.IsEnabled = true;
                CustomTextureTextBox.IsEnabled = true;
                AddCustomTextureButton.IsEnabled = false;
                TextureMapComboBox.SelectedIndex = 0;
                NoTextureModsLabel.Content = string.Empty;
            }
            else
            {
                AddCurrentTextureButton.IsEnabled = false;
                GetCustomTextureButton.IsEnabled = false;
                CustomTextureTextBox.IsEnabled = false;
                AddCustomTextureButton.IsEnabled = false;
                NoTextureModsLabel.Content = UIStrings.No_Texture_Mods;
            }

            if (ModelTypeComboBox.Items.Count > 0)
            {
                AddCurrentModelButton.IsEnabled = true;
                GetCustomModelButton.IsEnabled = true;
                CustomModelTextBox.IsEnabled = true;
                AdvOptionsButton.IsEnabled = true;
                AddCustomModelButton.IsEnabled = false;
                ModelTypeComboBox.SelectedIndex = 0;
                NoModelModsLabel.Content = string.Empty;

            }
            else
            {
                AddCurrentModelButton.IsEnabled = false;
                GetCustomModelButton.IsEnabled = false;
                CustomModelTextBox.IsEnabled = false;
                AdvOptionsButton.IsEnabled = false;
                AddCustomModelButton.IsEnabled = false;
                NoModelModsLabel.Content = UIStrings.No_3D_Mods;
            }

            if (MaterialComboBox.Items.Count > 0)
            {
                AddCurrentMaterialButton.IsEnabled = true;
                MaterialComboBox.SelectedIndex = 0;
                NoMaterialsModsLabel.Content = string.Empty;
            }
            else
            {
                AddCurrentMaterialButton.IsEnabled = false;
                NoMaterialsModsLabel.Content = UIStrings.No_Material_Mods;
            }

            SelectModGroup.IsEnabled = true;
        }



        /// <summary>
        /// The event handler for the custom texture button clicked
        /// </summary>
        private void GetCustomTextureButton_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new OpenFileDialog { Filter = "Texture Files(*.DDS)|*.DDS" };


            if (openFileDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                CustomTextureTextBox.Text = openFileDialog.FileName;
                AddCustomTextureButton.IsEnabled = true;
            }
        }

        /// <summary>
        /// The event handler for the custom model button clicked
        /// </summary>
        private void GetCustomModelButton_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new OpenFileDialog { Filter = "Collada Files(*.DAE)|*.DAE" };


            if (openFileDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                CustomModelTextBox.Text = openFileDialog.FileName;
                AddCustomModelButton.IsEnabled = true;
                //AdvOptionsButton.IsEnabled = true;
            }
        }

        /// <summary>
        /// The event handler for the current texture button clicked
        /// </summary>
        private void AddCurrentTextureButton_Click(object sender, RoutedEventArgs e)
        {
            var dat = new Dat(_gameDirectory);

            var selectedItem = TextureMapComboBox.SelectedItem as ModComboBox;

            var mod = selectedItem.SelectedMod;

            var includedMod = new IncludedMods
            {
                Name = $"{Path.GetFileNameWithoutExtension(mod.fullPath)} ({((Category)ModListTreeView.SelectedItem).Name})",
                FullPath = mod.fullPath
            };

            var includedModsList = IncludedModsList.Items.Cast<IncludedMods>().ToList();

            if (includedModsList.Any(item => item.Name.Equals(includedMod.Name)))
            {
                if (FlexibleMessageBox.Show(
                        string.Format(UIMessages.ExistingOption, includedMod.Name),
                        UIMessages.OverwriteTitle, MessageBoxButtons.YesNo, MessageBoxIcon.Question) ==
                    System.Windows.Forms.DialogResult.Yes)
                {
                    var rawData = dat.GetRawData(mod.data.modOffset, XivDataFiles.GetXivDataFile(mod.datFile), mod.data.modSize);

                    if (rawData == null)
                    {
                        FlexibleMessageBox.Show(
                            string.Format(UIMessages.RawDataErrorMessage, mod.data.modOffset, mod.datFile),
                            UIMessages.ModDataReadErrorTitle, MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }

                    _selectedModOption.Mods[mod.fullPath].ModDataBytes = rawData;
                }
            }
            else
            {
                var rawData = dat.GetRawData(mod.data.modOffset, XivDataFiles.GetXivDataFile(mod.datFile), mod.data.modSize);

                if (rawData == null)
                {
                    FlexibleMessageBox.Show(
                        string.Format(UIMessages.RawDataErrorMessage, mod.data.modOffset, mod.datFile),
                        UIMessages.ModDataReadErrorTitle, MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                var modData = new ModData
                {
                    Name = mod.name,
                    Category = mod.category,
                    FullPath = mod.fullPath,
                    ModDataBytes = rawData
                };

                IncludedModsList.Items.Add(includedMod);
                _selectedModOption.Mods.Add(mod.fullPath, modData);
            }
        }

        /// <summary>
        /// The event handler for the add custom texture button clicked
        /// </summary>
        private async void AddCustomTextureButton_Click(object sender, RoutedEventArgs e)
        {
            var selectedItem = TextureMapComboBox.SelectedItem as ModComboBox;

            var mod = selectedItem.SelectedMod;

            byte[] modData;

            var includedMod = new IncludedMods
            {
                Name = $"{Path.GetFileNameWithoutExtension(mod.fullPath)} ({((Category)ModListTreeView.SelectedItem).Name})",
                FullPath = mod.fullPath
            };

            var includedModsList = IncludedModsList.Items.Cast<IncludedMods>().ToList();

            var tex = new Tex(_gameDirectory);

            var ddsDirectory = new DirectoryInfo(CustomTextureTextBox.Text);

            if (selectedItem.TexTypePath.Type == XivTexType.ColorSet)
            {
                var mtrl = new Mtrl(_gameDirectory, XivDataFiles.GetXivDataFile(mod.datFile), GetLanguage());

                var xivMtrl = await mtrl.GetMtrlData(mod.data.modOffset, mod.fullPath, int.Parse(Settings.Default.DX_Version));

                modData = tex.DDStoMtrlData(xivMtrl, ddsDirectory, ((Category) ModListTreeView.SelectedItem).Item, GetLanguage());
            }
            else
            {
                var texData = await tex.GetTexData(selectedItem.TexTypePath);

                modData = await tex.DDStoTexData(texData, ((Category)ModListTreeView.SelectedItem).Item, ddsDirectory);
            }

            if (includedModsList.Any(item => item.Name.Equals(includedMod.Name)))
            {
                if (FlexibleMessageBox.Show(
                        string.Format(UIMessages.ExistingOption, includedMod.Name),
                        UIMessages.OverwriteTitle, MessageBoxButtons.YesNo, MessageBoxIcon.Question) ==
                    System.Windows.Forms.DialogResult.Yes)
                {
                    _selectedModOption.Mods[mod.fullPath].ModDataBytes = modData;
                }
            }
            else
            {
                IncludedModsList.Items.Add(includedMod);
                _selectedModOption.Mods.Add(mod.fullPath, new ModData
                {
                    Name = mod.name,
                    Category = mod.category,
                    FullPath = mod.fullPath,
                    ModDataBytes = modData
                });
            }
        }

        /// <summary>
        /// The event handler for the add current material button clicked
        /// </summary>
        private void AddCurrentMaterialButton_Click(object sender, RoutedEventArgs e)
        {
            var dat = new Dat(_gameDirectory);

            var selectedItem = MaterialComboBox.SelectedItem as ModComboBox;

            var mod = selectedItem.SelectedMod;

            var includedMod = new IncludedMods
            {
                Name = $"{Path.GetFileNameWithoutExtension(mod.fullPath)} ({((Category)ModListTreeView.SelectedItem).Name})",
                FullPath = mod.fullPath
            };

            var includedModsList = IncludedModsList.Items.Cast<IncludedMods>().ToList();

            if (includedModsList.Any(item => item.Name.Equals(includedMod.Name)))
            {
                if (FlexibleMessageBox.Show(
                        string.Format(UIMessages.ExistingOption, includedMod.Name),
                        UIMessages.OverwriteTitle, MessageBoxButtons.YesNo, MessageBoxIcon.Question) ==
                    System.Windows.Forms.DialogResult.Yes)
                {
                    var rawData = dat.GetRawData(mod.data.modOffset, XivDataFiles.GetXivDataFile(mod.datFile), mod.data.modSize);

                    if (rawData == null)
                    {
                        FlexibleMessageBox.Show(
                            string.Format(UIMessages.RawDataErrorMessage, mod.data.modOffset, mod.datFile),
                            UIMessages.ModDataReadErrorTitle, MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }

                    _selectedModOption.Mods[mod.fullPath].ModDataBytes = rawData;
                }
            }
            else
            {
                var rawData = dat.GetRawData(mod.data.modOffset, XivDataFiles.GetXivDataFile(mod.datFile), mod.data.modSize);

                if (rawData == null)
                {
                    FlexibleMessageBox.Show(
                        string.Format(UIMessages.RawDataErrorMessage, mod.data.modOffset, mod.datFile),
                        UIMessages.ModDataReadErrorTitle, MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                var modData = new ModData
                {
                    Name = mod.name,
                    Category = mod.category,
                    FullPath = mod.fullPath,
                    ModDataBytes = rawData
                };

                IncludedModsList.Items.Add(includedMod);
                _selectedModOption.Mods.Add(mod.fullPath, modData);
            }
        }

        /// <summary>
        /// The event handler for the current model button clicked
        /// </summary>
        private void AddCurrentModelButton_Click(object sender, RoutedEventArgs e)
        {
            var dat = new Dat(_gameDirectory);

            var selectedItem = ModelTypeComboBox.SelectedItem as ModComboBox;

            var mod = selectedItem.SelectedMod;

            var includedMod = new IncludedMods
            {
                Name = $"{Path.GetFileNameWithoutExtension(mod.fullPath)} ({((Category)ModListTreeView.SelectedItem).Name})",
                FullPath = mod.fullPath
            };

            var includedModsList = IncludedModsList.Items.Cast<IncludedMods>().ToList();

            if (includedModsList.Any(item => item.Name.Equals(includedMod.Name)))
            {
                if (FlexibleMessageBox.Show(
                        string.Format(UIMessages.ExistingOption, includedMod.Name),
                        UIMessages.OverwriteTitle, MessageBoxButtons.YesNo, MessageBoxIcon.Question) ==
                    System.Windows.Forms.DialogResult.Yes)
                {
                    var rawData = dat.GetRawData(mod.data.modOffset, XivDataFiles.GetXivDataFile(mod.datFile), mod.data.modSize);

                    if (rawData == null)
                    {
                        FlexibleMessageBox.Show(
                            string.Format(UIMessages.RawDataErrorMessage, mod.data.modOffset, mod.datFile),
                            UIMessages.ModDataReadErrorTitle, MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }

                    _selectedModOption.Mods[mod.fullPath].ModDataBytes = rawData;
                }
            }
            else
            {
                var rawData = dat.GetRawData(mod.data.modOffset, XivDataFiles.GetXivDataFile(mod.datFile), mod.data.modSize);

                if (rawData == null)
                {
                    FlexibleMessageBox.Show(
                        string.Format(UIMessages.RawDataErrorMessage, mod.data.modOffset, mod.datFile),
                        UIMessages.ModDataReadErrorTitle, MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                var modData = new ModData
                {
                    Name = mod.name,
                    Category = mod.category,
                    FullPath = mod.fullPath,
                    ModDataBytes = rawData
                };

                IncludedModsList.Items.Add(includedMod);
                _selectedModOption.Mods.Add(mod.fullPath, modData);
            }
        }

        /// <summary>
        /// The event handler for the advanced options button clicked
        /// </summary>
        private async void AdvOptionsButton_Click(object sender, RoutedEventArgs e)
        {
            var selectedItem = ModelTypeComboBox.SelectedItem as ModComboBox;

            var mod = selectedItem.SelectedMod;

            var includedMod = new IncludedMods
            {
                Name = $"{Path.GetFileNameWithoutExtension(mod.fullPath)} ({((Category)ModListTreeView.SelectedItem).Name})",
                FullPath = mod.fullPath
            };

            var itemModel = MakeItemModel(mod);

            var includedModsList = IncludedModsList.Items.Cast<IncludedMods>().ToList();
            var mdl = new Mdl(_gameDirectory, XivDataFiles.GetXivDataFile(mod.datFile));

            var xivMdl = await mdl.GetMdlData(itemModel, GetRace(mod.fullPath), null, null, mod.data.originalOffset);

            var advancedImportView = new AdvancedModelImportView(xivMdl, itemModel, GetRace(mod.fullPath), true);
            var result = advancedImportView.ShowDialog();

            if (result == true)
            {
                if (includedModsList.Any(item => item.Name.Equals(includedMod.Name)))
                {
                    if (FlexibleMessageBox.Show(
                            string.Format(UIMessages.ExistingOption, includedMod.Name),
                            UIMessages.OverwriteTitle, MessageBoxButtons.YesNo, MessageBoxIcon.Question) ==
                        System.Windows.Forms.DialogResult.Yes)
                    {
                        _selectedModOption.Mods[mod.fullPath].ModDataBytes = advancedImportView.RawModelData;
                    }
                }
                else
                {
                    IncludedModsList.Items.Add(includedMod);
                    _selectedModOption.Mods.Add(mod.fullPath, new ModData
                    {
                        Name = mod.name,
                        Category = mod.category,
                        FullPath = mod.fullPath,
                        ModDataBytes = advancedImportView.RawModelData
                    });
                }
            }
        }


        /// <summary>
        /// The event handler for the custom model button clicked
        /// </summary>
        private async void AddCustomModelButton_Click(object sender, RoutedEventArgs e)
        {
            var selectedItem = ModelTypeComboBox.SelectedItem as ModComboBox;

            var mod = selectedItem.SelectedMod;

            var includedMod = new IncludedMods
            {
                Name = $"{Path.GetFileNameWithoutExtension(mod.fullPath)} ({((Category)ModListTreeView.SelectedItem).Name})",
                FullPath = mod.fullPath
            };

            var itemModel = MakeItemModel(mod);

            var includedModsList = IncludedModsList.Items.Cast<IncludedMods>().ToList();
            var mdl = new Mdl(_gameDirectory, XivDataFiles.GetXivDataFile(mod.datFile));
            var xivMdl = await mdl.GetMdlData(itemModel, GetRace(mod.fullPath), null, null, mod.data.originalOffset);
            var warnings = await mdl.ImportModel(itemModel, xivMdl, new DirectoryInfo(CustomModelTextBox.Text), null, XivStrings.TexTools, 
                Settings.Default.DAE_Plugin_Target, true);

            if (warnings.Count > 0)
            {
                foreach (var warning in warnings)
                {
                    FlexibleMessageBox.Show(
                        $"{warning.Value}", $"{warning.Key}",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }

            var mdlData = mdl.MDLRawData;

            if (includedModsList.Any(item => item.Name.Equals(includedMod.Name)))
            {
                if (FlexibleMessageBox.Show(
                        string.Format(UIMessages.ExistingOption, includedMod.Name),
                        UIMessages.OverwriteTitle, MessageBoxButtons.YesNo, MessageBoxIcon.Question) ==
                    System.Windows.Forms.DialogResult.Yes)
                {
                    _selectedModOption.Mods[mod.fullPath].ModDataBytes = mdlData;
                }
            }
            else
            {
                IncludedModsList.Items.Add(includedMod);
                _selectedModOption.Mods.Add(mod.fullPath, new ModData
                {
                    Name = mod.name,
                    Category = mod.category,
                    FullPath = mod.fullPath,
                    ModDataBytes = mdlData
                });
            }
        }

        /// <summary>
        /// The event handler for the remove mod item button clicked
        /// </summary>
        private void RemoveModItemButton_Click(object sender, RoutedEventArgs e)
        {
            _selectedModOption.Mods.Remove(((IncludedMods)IncludedModsList.SelectedItem).FullPath);
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


        private class ModComboBox
        {
            /// <summary>
            /// The name of the mod
            /// </summary>
            public string Name { get; set; }

            /// <summary>
            /// The selected mod
            /// </summary>
            public Mod SelectedMod { get; set; }

            /// <summary>
            /// The texture type path
            /// </summary>
            public TexTypePath TexTypePath { get; set; }
        }

        private class IncludedMods
        {
            /// <summary>
            /// The mod name
            /// </summary>
            public string Name { get; set; }

            /// <summary>
            /// The mods full path
            /// </summary>
            public string FullPath { get; set; }
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
    }
}
