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
// along with this program.  If not, see <http://www.gnu.org/licenses/>.

using FFXIV_TexTools.Models;
using FFXIV_TexTools.Resources;
using ImageMagick;
using Newtonsoft.Json;
using SharpDX;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using xivModdingFramework.General.Enums;
using xivModdingFramework.Items.DataContainers;
using xivModdingFramework.Materials.FileTypes;
using xivModdingFramework.Mods.DataContainers;
using xivModdingFramework.Textures.DataContainers;
using xivModdingFramework.Textures.Enums;
using xivModdingFramework.Textures.FileTypes;

namespace FFXIV_TexTools.ViewModels
{
    public class ModListViewModel : INotifyPropertyChanged
    {
        private readonly DirectoryInfo _modListDirectory;
        private readonly DirectoryInfo _gameDirectory;
        private string _modToggleText = "Enable/Disable";
        private Visibility _listVisibility = Visibility.Visible, _infoGridVisibility = Visibility.Collapsed;
        private string _modPackTitle, _modPackModAuthorLabel, _modPackModCountLabel, _modPackModVersionLabel, _modPackContentList;
        private bool _itemFilter, _modPackFilter;
        private ObservableCollection<Category> _categories;

        public ModListViewModel()
        {
            _gameDirectory = new DirectoryInfo(Properties.Settings.Default.FFXIV_Directory);
            _modListDirectory = new DirectoryInfo(Path.Combine(_gameDirectory.Parent.Parent.FullName, XivStrings.ModlistFilePath));

            ItemFilter = true;
        }

        /// <summary>
        /// The collection of categories
        /// </summary>
        public ObservableCollection<Category> Categories
        {
            get => _categories;
            set
            {
                _categories = value;
                OnPropertyChanged(nameof(Categories));
            }
        }

        /// <summary>
        /// Gets the categories based on the item filter
        /// </summary>
        private void GetCategoriesItemFilter()
        {
            var modList = JsonConvert.DeserializeObject<ModList>(File.ReadAllText(_modListDirectory.FullName));

            Categories = new ObservableCollection<Category>();

            if (modList == null) return;

            // Mod Packs
            var category = new Category
            {
                Name = "ModPacks",
                Categories = new ObservableCollection<Category>(),
                CategoryList = new List<string>()
            };

            var categoryItem = new Category
            {
                Name = "Standalone (Non-ModPack)",
                ParentCategory = category
            };

            category.Categories.Add(categoryItem);

            foreach (var modListModPack in modList.ModPacks)
            {
                categoryItem = new Category
                {
                    Name = modListModPack.name,
                    ParentCategory = category
                };

                category.Categories.Add(categoryItem);
            }

            Categories.Add(category);

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
                category = new Category
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

                    categoryItem = new Category
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
        }

        /// <summary>
        /// Gets the categoreis based on the mod pack filter
        /// </summary>
        private void GetCategoriesModPackFilter()
        {
            var modList = JsonConvert.DeserializeObject<ModList>(File.ReadAllText(_modListDirectory.FullName));

            Categories = new ObservableCollection<Category>();
            var modPackCatDict = new Dictionary<string, Category>();

            if (modList == null) return;

            // Mod Packs

            var modPacksParent = new Category
            {
                Name = "ModPacks",
            };

            var category = new Category
            {
                Name = "Standalone (Non-ModPack)",
                Categories = new ObservableCollection<Category>(),
                CategoryList = new List<string>(),
                ParentCategory = modPacksParent
            };

            modPackCatDict.Add(category.Name, category);

            foreach (var modListModPack in modList.ModPacks)
            {
                category = new Category
                {
                    Name = modListModPack.name,
                    Categories = new ObservableCollection<Category>(),
                    CategoryList = new List<string>(),
                    ParentCategory = modPacksParent
                };

                modPackCatDict.Add(category.Name, category);
            }

            foreach (var modPackCategory in modPackCatDict)
            {
                List<Mod> modsInModpack;

                if (!modPackCategory.Key.Equals("Standalone (Non-ModPack)"))
                {
                    modsInModpack = (from mod in modList.Mods
                        where mod.modPack != null && mod.modPack.name.Equals(modPackCategory.Key)
                        select mod).ToList();
                }
                else
                {
                    modsInModpack = (from mod in modList.Mods
                        where mod.modPack == null
                        select mod).ToList();
                }

                var mainCategories = new HashSet<string>();

                foreach (var modEntry in modsInModpack)
                {
                    if (!modEntry.name.Equals(string.Empty))
                    {
                        mainCategories.Add(modEntry.category);
                    }
                }

                foreach (var mainCategory in mainCategories)
                {
                    category = new Category
                    {
                        Name = mainCategory,
                        Categories = new ObservableCollection<Category>(),
                        CategoryList = new List<string>()
                    };

                    var modItems =
                        from mod in modsInModpack
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

                    modPackCategory.Value.Categories.Add(category);
                }

                Categories.Add(modPackCategory.Value);
            }
        }


        public ObservableCollection<ModListModel> ModListPreviewList { get; set; } = new ObservableCollection<ModListModel>();

        /// <summary>
        /// Makes an generic item model from a mod item
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


                if (item.ItemCategory.Equals(XivStrings.Body))
                {
                    item.ModelInfo = new XivModelInfo
                    {
                        ModelID = int.Parse(fullPath.Substring(fullPath.IndexOf("/body", StringComparison.Ordinal) + 7, 4))
                    };
                }
                else if (item.ItemCategory.Equals(XivStrings.Hair))
                {
                    item.ModelInfo = new XivModelInfo
                    {
                        ModelID = int.Parse(fullPath.Substring(fullPath.IndexOf("/hair", StringComparison.Ordinal) + 7, 4))
                    };
                }
                else if (item.ItemCategory.Equals(XivStrings.Face))
                {
                    item.ModelInfo = new XivModelInfo
                    {
                        ModelID = int.Parse(fullPath.Substring(fullPath.IndexOf("/face", StringComparison.Ordinal) + 7, 4))
                    };
                }
                else if (item.ItemCategory.Equals(XivStrings.Tail))
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

                if (item.ItemCategory.Equals(XivStrings.Face_Paint))
                {
                    item.ModelInfo = new XivModelInfo
                    {
                        ModelID = int.Parse(fullPath.Substring(fullPath.LastIndexOf("_", StringComparison.Ordinal) + 1, 1))
                    };
                }
                else if (item.ItemCategory.Equals(XivStrings.Equip_Decals))
                {
                    item.ModelInfo = new XivModelInfo
                    {
                        ModelID = int.Parse(fullPath.Substring(fullPath.LastIndexOf("_", StringComparison.Ordinal) + 1, 3))
                    };
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

                item.ModelInfo = new XivModelInfo
                {
                    ModelID = int.Parse(fullPath.Substring(fullPath.LastIndexOf("/", StringComparison.Ordinal) + 1, 6))
                };
            }

            if (modItem.fullPath.Contains("/hou/"))
            {
                item.Category = XivStrings.Housing;

                item.ModelInfo = new XivModelInfo
                {
                    ModelID = int.Parse(fullPath.Substring(fullPath.LastIndexOf("_m", StringComparison.Ordinal) + 2, 4))
                };
            }

            return item;
        }

        /// <summary>
        /// Update the mod list entries
        /// </summary>
        /// <param name="selectedItem">The selected item to update the entries for</param>
        public void UpdateList(XivGenericItemModel selectedItem)
        {
            ListVisibility = Visibility.Visible;
            InfoGridVisibility = Visibility.Collapsed;

            var modList = JsonConvert.DeserializeObject<ModList>(File.ReadAllText(_modListDirectory.FullName));

            var modItems =
                from mod in modList.Mods
                where mod.name.Equals(selectedItem.Name)
                select mod;

            ModListPreviewList.Clear();

            foreach (var modItem in modItems)
            {
                var itemPath = modItem.fullPath;

                var modListModel = new ModListModel
                {
                    ModItem = modItem
                };

                // Race
                if (selectedItem.Category.Equals(XivStrings.Gear))
                {
                    if (modItem.fullPath.Contains("equipment"))
                    {
                        var raceCode = itemPath.Substring(itemPath.LastIndexOf("_c") + 2, 4);
                        modListModel.Race = XivRaces.GetXivRace(raceCode).GetDisplayName();
                    }
                    else
                    {
                        modListModel.Race = XivStrings.All;
                    }
                }
                else if (selectedItem.Category.Equals(XivStrings.Character))
                {
                    if (!modItem.fullPath.Contains("chara/common"))
                    {
                        var raceCode = itemPath.Substring(itemPath.IndexOf("n/c") + 3, 4);
                        modListModel.Race = XivRaces.GetXivRace(raceCode).GetDisplayName();
                    }
                    else
                    {
                        modListModel.Race = XivStrings.All;
                    }

                }
                else if (selectedItem.Category.Equals(XivStrings.Companions))
                {
                    modListModel.Race = XivStrings.Monster;
                }
                else if (selectedItem.Category.Equals(XivStrings.UI))
                {
                    modListModel.Race = XivStrings.All;
                }
                else if (selectedItem.Category.Equals(XivStrings.Housing))
                {
                    modListModel.Race = XivStrings.All;
                }

                XivTexType? xivTexType = null;
                // Map
                if (itemPath.Contains("_d."))
                {
                    xivTexType = XivTexType.Diffuse;
                    modListModel.Map = xivTexType.ToString();
                }
                else if (itemPath.Contains("_n."))
                {
                    xivTexType = XivTexType.Normal;
                    modListModel.Map = xivTexType.ToString();
                }
                else if (itemPath.Contains("_s."))
                {
                    xivTexType = XivTexType.Specular;
                    modListModel.Map = xivTexType.ToString();
                }
                else if (itemPath.Contains("_m."))
                {
                    xivTexType = XivTexType.Multi;
                    modListModel.Map = xivTexType.ToString();
                }
                else if (itemPath.Contains("material"))
                {
                    xivTexType = XivTexType.ColorSet;
                    modListModel.Map = xivTexType.ToString();
                }
                else if (itemPath.Contains("decal"))
                {
                    xivTexType = XivTexType.Mask;
                    modListModel.Map = xivTexType.ToString();
                }
                else if (itemPath.Contains("vfx"))
                {
                    xivTexType = XivTexType.Vfx;
                    modListModel.Map = xivTexType.ToString();
                }
                else if (itemPath.Contains("ui/"))
                {
                    if (itemPath.Contains("icon"))
                    {
                        xivTexType = XivTexType.Icon;
                        modListModel.Map = xivTexType.ToString();
                    }
                    else if (itemPath.Contains("map"))
                    {
                        xivTexType = XivTexType.Map;
                        modListModel.Map = xivTexType.ToString();
                    }
                    else
                    {
                        modListModel.Map = "UI";
                    }
                }
                else if (itemPath.Contains("model"))
                {
                    modListModel.Map = "3D";
                }
                else
                {
                    modListModel.Map = "--";
                }

                // Part
                if (itemPath.Contains("_b_"))
                {
                    modListModel.Part = "b";
                }
                else if (itemPath.Contains("_c_"))
                {
                    modListModel.Part = "c";
                }
                else if (itemPath.Contains("_d_"))
                {
                    modListModel.Part = "d";
                }
                else if (itemPath.Contains("decal"))
                {
                    modListModel.Part = itemPath.Substring(itemPath.LastIndexOf('_') + 1, itemPath.LastIndexOf('.') - (itemPath.LastIndexOf('_') + 1));
                }
                else
                {
                    modListModel.Part = "a";
                }

                // Type
                if (itemPath.Contains("_iri_"))
                {
                    modListModel.Type = XivStrings.Iris;
                }
                else if (itemPath.Contains("_etc_"))
                {
                    modListModel.Type = XivStrings.Etc;
                }
                else if (itemPath.Contains("_fac_"))
                {
                    modListModel.Type = XivStrings.Face;
                }
                else if (itemPath.Contains("_hir_"))
                {
                    modListModel.Type = XivStrings.Hair;
                }
                else if (itemPath.Contains("_acc_"))
                {
                    modListModel.Type = XivStrings.Accessory;
                }
                else if (itemPath.Contains("demihuman"))
                {
                    modListModel.Type = itemPath.Substring(itemPath.LastIndexOf('_') - 3, 3);
                }
                else
                {
                    modListModel.Type = "--";
                }

                // Image
                if (itemPath.Contains("material"))
                {
                    var dxVersion = int.Parse(Properties.Settings.Default.DX_Version);

                    var mtrl = new Mtrl(_gameDirectory, selectedItem.DataFile);

                    var offset = modItem.enabled ? modItem.data.modOffset : modItem.data.originalOffset;

                    var mtrlData = mtrl.GetMtrlData(offset, modItem.fullPath, dxVersion);

                    var floats = Half.ConvertToFloat(mtrlData.ColorSetData.ToArray());

                    var floatArray = Utilities.ToByteArray(floats);

                    var pixelSettings =
                        new PixelReadSettings(4, 16, StorageType.Float, PixelMapping.RGBA);

                    using (var magickImage = new MagickImage(floatArray, pixelSettings))
                    {
                        magickImage.Alpha(AlphaOption.Opaque);
                        modListModel.Image = magickImage.ToBitmapSource();
                    }
                }
                else if (itemPath.Contains("model"))
                {
                    modListModel.Image = new BitmapImage(new Uri("pack://application:,,,/FFXIV_TexTools;component/Resources/3DModel.png"));
                }
                else
                {
                    var tex = new Tex(_gameDirectory);

                    var ttp = new TexTypePath
                    {
                        Type = xivTexType.GetValueOrDefault(),
                        DataFile = selectedItem.DataFile,
                        Path = modItem.fullPath
                    };

                    var texData = tex.GetTexData(ttp);
                    var mapBytes = tex.GetImageData(texData);

                    var pixelSettings =
                        new PixelReadSettings(texData.Width, texData.Height, StorageType.Char, PixelMapping.RGBA);

                    using (var magickImage = new MagickImage(mapBytes, pixelSettings))
                    {
                        if (!modItem.fullPath.Contains("ui/"))
                        {
                            magickImage.Alpha(AlphaOption.Opaque);
                        }

                        magickImage.Thumbnail(512, 512);

                        modListModel.Image = magickImage.ToBitmapSource();
                    }
                }

                // Status
                if (modItem.enabled)
                {
                    modListModel.ActiveBorder = Brushes.Green;
                    modListModel.Active = Brushes.Transparent;
                    modListModel.ActiveOpacity = 1;
                }
                else
                {
                    modListModel.ActiveBorder = Brushes.Red;
                    modListModel.Active = Brushes.Gray;
                    modListModel.ActiveOpacity = 0.5f;
                }

                ModListPreviewList.Add(modListModel);
            }
        }

        /// <summary>
        /// Update the info grid
        /// </summary>
        /// <remarks>
        /// The info grid shows mod pack details
        /// </remarks>
        /// <param name="category">The category to update the info grid for</param>
        public void UpdateInfoGrid(Category category)
        {
            ListVisibility = Visibility.Collapsed;
            InfoGridVisibility = Visibility.Visible;
            ModPackContentList = string.Empty;
            var enabledCount = 0;
            var disabledCount = 0;

            var modList = JsonConvert.DeserializeObject<ModList>(File.ReadAllText(_modListDirectory.FullName));
            List<Mod> modPackModList = null;

            if (category.Name.Equals("Standalone (Non-ModPack)"))
            {
                modPackModList = (from items in modList.Mods
                    where !items.name.Equals(string.Empty) && items.modPack == null
                    select items).ToList();

                ModPackModAuthorLabel = "[ N/A ]";
                ModPackModVersionLabel = "[ N/A ]";
            }
            else
            {
                var modPackData = (from data in modList.ModPacks
                    where data.name == category.Name
                    select data).FirstOrDefault();

                modPackModList = (from items in modList.Mods
                    where (items.modPack != null && items.modPack.name == category.Name)
                    select items).ToList();

                ModPackModAuthorLabel = modPackData.author;
                ModPackModVersionLabel = modPackData.version;
            }

            ModPackTitle = category.Name;
            ModPackModCountLabel = modPackModList.Count.ToString();

            var modNameDict = new Dictionary<string, int>();

            foreach (var mod in modPackModList)
            {
                if (mod.enabled)
                {
                    enabledCount++;
                }
                else
                {
                    disabledCount++;
                }

                if (!modNameDict.ContainsKey(mod.name))
                {
                    modNameDict.Add(mod.name, 1);
                }
                else
                {
                    modNameDict[mod.name] += 1;
                }
            }

            foreach (var mod in modNameDict)
            {
                ModPackContentList += $"[{ mod.Value}] {mod.Key}\n";
            }

            ModToggleText = enabledCount > disabledCount ? "Disable" : "Enable";
        }

        /// <summary>
        /// Clears the list of mods 
        /// </summary>
        public void ClearList()
        {
            ListVisibility = Visibility.Visible;
            InfoGridVisibility = Visibility.Collapsed;
            ModListPreviewList.Clear();
        }

        /// <summary>
        /// Removes an item from the list when deleted
        /// </summary>
        /// <param name="item">The mod item to remove</param>
        /// <param name="category">The Category object for the item</param>
        public void RemoveItem(ModListModel item, Category category)
        {
            var modList = JsonConvert.DeserializeObject<ModList>(File.ReadAllText(_modListDirectory.FullName));

            var remainingList = (from items in modList.Mods
                                where items.name == item.ModItem.name
                                select items).ToList();

            if (remainingList.Count == 0)
            {
                var parentCategory = (from parent in Categories
                    where parent.Name.Equals(item.ModItem.category)
                    select parent).FirstOrDefault();

                parentCategory.Categories.Remove(category);

                if (parentCategory.Categories.Count == 0)
                {
                    Categories.Remove(parentCategory);
                }
            }

            ModListPreviewList.Remove(item);
        }

        /// <summary>
        /// Refreshes the view after a mod pack is deleted
        /// </summary>
        public void RemoveModPack()
        {
            SetFilter(ItemFilter ? "ItemFilter" : "ModPackFilter");
        }

        /// <summary>
        /// The text for the mod toggle button
        /// </summary>
        public string ModToggleText
        {
            get => _modToggleText;
            set
            {
                _modToggleText = value;
                OnPropertyChanged(nameof(ModToggleText));
            }
        }

        /// <summary>
        /// The visibility of the mod list item view
        /// </summary>
        public Visibility ListVisibility
        {
            get => _listVisibility;
            set
            {
                _listVisibility = value;
                OnPropertyChanged(nameof(ListVisibility));
            }
        }

        /// <summary>
        /// THe visibility of the info grid view
        /// </summary>
        public Visibility InfoGridVisibility
        {
            get => _infoGridVisibility;
            set
            {
                _infoGridVisibility = value;
                OnPropertyChanged(nameof(InfoGridVisibility));
            }
        }

        /// <summary>
        /// The mod pack title
        /// </summary>
        public string ModPackTitle
        {
            get => _modPackTitle;
            set
            {
                _modPackTitle = value;
                OnPropertyChanged(nameof(ModPackTitle));
            }
        }

        /// <summary>
        /// The label for the mod pack author in the info grid
        /// </summary>
        public string ModPackModAuthorLabel
        {
            get => _modPackModAuthorLabel;
            set
            {
                _modPackModAuthorLabel = value;
                OnPropertyChanged(nameof(ModPackModAuthorLabel));
            }
        }

        /// <summary>
        /// The label for the mod pack mod count in the info grid
        /// </summary>
        public string ModPackModCountLabel
        {
            get => _modPackModCountLabel;
            set
            {
                _modPackModCountLabel = value;
                OnPropertyChanged(nameof(ModPackModCountLabel));
            }
        }

        /// <summary>
        /// the label for the mod pack version in the info grid
        /// </summary>
        public string ModPackModVersionLabel
        {
            get => _modPackModVersionLabel;
            set
            {
                _modPackModVersionLabel = value;
                OnPropertyChanged(nameof(ModPackModVersionLabel));
            }
        }

        /// <summary>
        /// The content of the mod pack as a string
        /// </summary>
        public string ModPackContentList
        {
            get => _modPackContentList;
            set
            {
                _modPackContentList = value;
                OnPropertyChanged(nameof(ModPackContentList));
            }
        }

        /// <summary>
        /// The status of the item filter
        /// </summary>
        public bool ItemFilter
        {
            get => _itemFilter;
            set
            {
                _itemFilter = value;
                if (value)
                {
                    SetFilter("ItemFilter");
                }
                OnPropertyChanged(nameof(ItemFilter));
            }
        }

        /// <summary>
        /// The status of the mod pack filter
        /// </summary>
        public bool ModPackFilter
        {
            get => _modPackFilter;
            set
            {
                _modPackFilter = value;
                if (value)
                {
                    SetFilter("ModPackFilter");
                }
                OnPropertyChanged(nameof(ModPackFilter));
            }
        }

        /// <summary>
        /// Sets the filter for the mod list treeview
        /// </summary>
        /// <param name="type">The type of the filter</param>
        private void SetFilter(string type)
        {
            if (type.Equals("ItemFilter"))
            {
                GetCategoriesItemFilter();
            }
            else if (type.Equals("ModPackFilter"))
            {
                GetCategoriesModPackFilter();
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public class ModListModel : INotifyPropertyChanged
        {
            private SolidColorBrush _active, _activeBorder;
            private float _opacity;

            /// <summary>
            /// The race of the modded item
            /// </summary>
            public string Race { get; set; }

            /// <summary>
            /// The texture map of the modded item
            /// </summary>
            public string Map { get; set; }

            /// <summary>
            /// The part of the modded item
            /// </summary>
            public string Part { get; set; }

            /// <summary>
            /// The type of the modded item
            /// </summary>
            public string Type { get; set; }

            /// <summary>
            /// The brush color reflecting the active status of the modded item
            /// </summary>
            public SolidColorBrush Active
            {
                get => _active;
                set
                {
                    _active = value;
                    OnPropertyChanged("Active");
                }
            }

            /// <summary>
            /// The opacity reflecting the active status of the modded item
            /// </summary>
            public float ActiveOpacity
            {
                get => _opacity;

                set
                {
                    _opacity = value;
                    OnPropertyChanged("ActiveOpacity");
                }
            }

            /// <summary>
            /// The border brush color reflecting the active status of the modded item
            /// </summary>
            public SolidColorBrush ActiveBorder
            {
                get => _activeBorder;
                set
                {
                    _activeBorder = value;
                    OnPropertyChanged("ActiveBorder");
                }
            }

            /// <summary>
            /// The mod item
            /// </summary>
            public Mod ModItem { get; set; }

            /// <summary>
            /// The image of the modded item
            /// </summary>
            public BitmapSource Image { get; set; }


            public event PropertyChangedEventHandler PropertyChanged;

            protected virtual void OnPropertyChanged(string propertyName)
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            }
        }
    }
}