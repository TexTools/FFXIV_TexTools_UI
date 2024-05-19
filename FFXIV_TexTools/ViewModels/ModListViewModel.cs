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

using FFXIV_TexTools.Helpers;
using FFXIV_TexTools.Models;
using FFXIV_TexTools.Resources;
using Newtonsoft.Json;
using SharpDX;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Bmp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using xivModdingFramework.Cache;
using xivModdingFramework.General.Enums;
using xivModdingFramework.Helpers;
using xivModdingFramework.Items.DataContainers;
using xivModdingFramework.Materials.FileTypes;
using xivModdingFramework.Mods;
using xivModdingFramework.Mods.DataContainers;
using xivModdingFramework.Mods.Enums;
using xivModdingFramework.Textures.DataContainers;
using xivModdingFramework.Textures.Enums;
using xivModdingFramework.Textures.FileTypes;
using Application = System.Windows.Application;

namespace FFXIV_TexTools.ViewModels
{
    public class ModListViewModel : INotifyPropertyChanged
    {
        private readonly DirectoryInfo _gameDirectory;
        private string _modToggleText = UIStrings.Enable_Disable;
        private Visibility _listVisibility = Visibility.Visible, _infoGridVisibility = Visibility.Collapsed;
        private string _modPackTitle, _modPackModAuthorLabel, _modPackModCountLabel, _modPackModVersionLabel, _modPackContentList, _progressText;
        private bool _itemFilter, _modPackFilter, _nameSort, _dateSort;
        private int _progressValue;
        private ObservableCollection<Category> _categories;
        private IProgress<(int current, int total)> progress;
        private Dictionary<string, List<string>> _modListParents;


        public ModListViewModel()
        {
            _gameDirectory = new DirectoryInfo(Properties.Settings.Default.FFXIV_Directory);

            progress = new Progress<(int current, int total)>((result) =>
            {
                ProgressValue = (int)(((float) result.current / (float) result.total) * 100);
                ProgressText = $"{result.current} / {result.total}";
            });

            if (Properties.Settings.Default.ModList_Sorting.Equals("NameSort")) _nameSort = true;
            else _dateSort = true;

            if (Properties.Settings.Default.ModList_Filter.Equals("Item"))
            {
                _itemFilter = true;
                SetFilter("ItemFilter");
            }                
            else
            {
                _modPackFilter = true;
                SetFilter("ModPackFilter");
            }

            var task = Task.Run(async () =>
            {
                // Run this on another thread to ensure we don't hard-lock.
                return await XivCache.GetModListParents(MainWindow.DefaultTransaction);
            });

            _modListParents = task.Result;
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
        private Task GetCategoriesItemFilter()
        {
            Categories = new ObservableCollection<Category>();
            var tx = MainWindow.DefaultTransaction;

            return Task.Run(async () =>
            {
                var modList = await tx.GetModList();

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
                    Name = UIStrings.Standalone_Non_ModPack,
                    ParentCategory = category
                };

                category.Categories.Add(categoryItem);

                foreach (var mpkv in modList.ModPacks)
                {
                    var modsInModpack = mpkv.Value.Mods;


                    // If the modpack has no mods associated with it, don't bother listing it
                    if (modsInModpack.Count == 0) continue;

                    categoryItem = new Category
                    {
                        Name = mpkv.Value.Name,
                        ParentCategory = category
                    };

                    category.Categories.Add(categoryItem);
                }


                if(_nameSort)
                {
                    category.Categories = new ObservableCollection<Category>(category.Categories.OrderBy(i => i.Name));
                }
                
                Application.Current.Dispatcher.Invoke(() => Categories.Add(category));

                // Mods
                var mainCategories = new HashSet<string>();

                var allmods = modList.GetMods();
                foreach (var modEntry in allmods)
                {
                    if (modEntry.IsInternal()) continue;
                    if (!modEntry.ItemName.Equals(string.Empty))
                    {
                        mainCategories.Add(modEntry.ItemCategory);
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
                        from mod in allmods
                        where mod.ItemCategory.Equals(mainCategory)
                        select mod;

                    foreach (var modItem in modItems)
                    {
                        if (category.CategoryList.Contains(modItem.ItemName)) continue;

                        try
                        {
                            categoryItem = new Category
                            {
                                Name = modItem.ItemName,
                                Item = MakeItemModel(modItem),
                                ParentCategory = category
                            };
                        } catch(Exception ex)
                        {
                            var im = new XivGenericItemModel()
                            {
                                Name = "UNIDENTIFIABLE FILE - " + modItem.ItemName,
                                SecondaryCategory = modItem.ItemCategory,
                                DataFile = IOUtil.GetDataFileFromPath(modItem.FilePath),
                                ModelInfo = new XivModelInfo()
                            };

                            categoryItem = new Category
                            {
                                Name = modItem.ItemName,
                                Item = im,
                                ParentCategory = category
                            };
                        }

                        category.Categories.Add(categoryItem);
                        category.CategoryList.Add(modItem.ItemName);
                    }

                    if (_nameSort)
                    {
                        category.Categories = new ObservableCollection<Category>(category.Categories.OrderBy(i => i.Name));
                    }

                    Application.Current.Dispatcher.Invoke(() => Categories.Add(category));
                }
            });
        }

        /// <summary>
        /// Gets the categoreis based on the mod pack filter
        /// </summary>
        private Task GetCategoriesModPackFilter()
        {
            Categories = new ObservableCollection<Category>();

            var tx = MainWindow.DefaultTransaction;
            return Task.Run(async () =>
            {
                var modList = await tx.GetModList();

                var modPackCatDict = new Dictionary<string, Category>();

                if (modList == null) return;

                // Mod Packs

                var modPacksParent = new Category
                {
                    Name = "ModPacks",
                };

                var category = new Category
                {
                    Name = UIStrings.Standalone_Non_ModPack,
                    Categories = new ObservableCollection<Category>(),
                    CategoryList = new List<string>(),
                    ParentCategory = modPacksParent
                };

                modPackCatDict.Add(category.Name, category);

                var allModPacks = modList.ModPacks.Select(x => x.Value);
                var allMods = modList.GetMods();
                foreach (var mp in allModPacks)
                {
                    // If the modpack has no mods associated with it, don't bother listing it
                    if (mp.Mods.Count == 0) continue;

                    category = new Category
                    {
                        Name = mp.Name,
                        Categories = new ObservableCollection<Category>(),
                        CategoryList = new List<string>(),
                        ParentCategory = modPacksParent
                    };

                    if (!modPackCatDict.ContainsKey(category.Name))
                    {
                        modPackCatDict.Add(category.Name, category);
                    }
                }

                var sortedModPackCatDict = new Dictionary<string, Category>();

                if (_nameSort)
                {
                    sortedModPackCatDict = modPackCatDict.OrderBy(i => i.Value.Name).ToDictionary(pair => pair.Key, pair => pair.Value);
                }
                else
                {
                    sortedModPackCatDict = modPackCatDict;
                }

                foreach (var modPackCategory in sortedModPackCatDict)
                {
                    List<Mod> modsInModpack;

                    var mp = modList.GetModPack(modPackCategory.Key);

                    if (mp != null)
                    {
                        modsInModpack = mp.Value.GetMods(modList).ToList();
                    } else
                    {
                        modsInModpack = modList.GetMods(x => x.ModPack == "").ToList();
                    }

                    var mainCategories = new HashSet<string>();

                    foreach (var modEntry in modsInModpack)
                    {
                        if (modEntry.IsInternal()) continue;
                        if (!modEntry.ItemName.Equals(string.Empty))
                        {
                            mainCategories.Add(modEntry.ItemCategory);
                        }
                    }

                    foreach (var mainCategory in mainCategories)
                    {
                        category = new Category
                        {
                            Name = mainCategory,
                            Categories = new ObservableCollection<Category>(),
                            CategoryList = new List<string>(),
                            ParentCategory = modPackCategory.Value
                        };

                        var modItems =
                            from mod in modsInModpack
                            where mod.ItemCategory.Equals(mainCategory)
                            select mod;

                        foreach (var modItem in modItems)
                        {
                            if (category.CategoryList.Contains(modItem.ItemName)) continue;

                            Category categoryItem;
                            try
                            {
                                categoryItem = new Category
                                {
                                    Name = modItem.ItemName,
                                    Item = MakeItemModel(modItem),
                                    ParentCategory = category
                                };
                            }
                            catch (Exception ex)
                            {
                                var im = new XivGenericItemModel()
                                {
                                    Name = "UNIDENTIFIABLE FILE - " + modItem.ItemName,
                                    SecondaryCategory = modItem.ItemCategory,
                                    DataFile = modItem.DataFile,
                                    ModelInfo = new XivModelInfo()
                                };

                                categoryItem = new Category
                                {
                                    Name = modItem.ItemName,
                                    Item = im,
                                    ParentCategory = category
                                };
                            }

                            category.Categories.Add(categoryItem);
                            category.CategoryList.Add(modItem.ItemName);

                        }

                        if(_nameSort)
                        {
                            category.Categories = new ObservableCollection<Category>(category.Categories.OrderBy(i => i.Name));
                        }
                        
                        modPackCategory.Value.Categories.Add(category);
                    }

                    Application.Current.Dispatcher.Invoke(() => Categories.Add(modPackCategory.Value));
                }
            });
        }


        public ObservableCollection<ModListModel> ModListPreviewList { get; set; } = new ObservableCollection<ModListModel>();

        /// <summary>
        /// Makes an generic item model from a mod item
        /// 
        /// NOTE: This function will THROW if it fails to create a valid item model.
        /// </summary>
        /// <param name="modItem">The mod item</param>
        /// <returns>The mod item as a XivGenericItemModel</returns>
        private static XivGenericItemModel MakeItemModel(Mod modItem)
        {
            var fullPath = modItem.FilePath;

            var item = new XivGenericItemModel
            {
                Name = modItem.ItemName,
                SecondaryCategory = modItem.ItemCategory,
                DataFile = modItem.DataFile
            };

            try
            {
                if (modItem.FilePath.Contains("chara/equipment") || modItem.FilePath.Contains("chara/accessory"))
                {
                    item.PrimaryCategory = XivStrings.Gear;
                    item.ModelInfo = new XivModelInfo
                    {
                        PrimaryID = int.Parse(fullPath.Substring(17, 4))
                    };
                }

                if (modItem.FilePath.Contains("chara/weapon"))
                {
                    item.PrimaryCategory = XivStrings.Gear;
                    item.ModelInfo = new XivModelInfo
                    {
                        PrimaryID = int.Parse(fullPath.Substring(14, 4))
                    };
                }

                if (modItem.FilePath.Contains("chara/human"))
                {
                    item.PrimaryCategory = XivStrings.Character;


                    if (item.Name.Equals(XivStrings.Body))
                    {
                        item.ModelInfo = new XivModelInfo
                        {
                            PrimaryID = int.Parse(
                                fullPath.Substring(fullPath.IndexOf("/body", StringComparison.Ordinal) + 7, 4))
                        };
                    }
                    else if (item.Name.Equals(XivStrings.Hair))
                    {
                        item.ModelInfo = new XivModelInfo
                        {
                            PrimaryID = int.Parse(
                                fullPath.Substring(fullPath.IndexOf("/hair", StringComparison.Ordinal) + 7, 4))
                        };
                    }
                    else if (item.Name.Equals(XivStrings.Face))
                    {
                        item.ModelInfo = new XivModelInfo
                        {
                            PrimaryID = int.Parse(
                                fullPath.Substring(fullPath.IndexOf("/face", StringComparison.Ordinal) + 7, 4))
                        };
                    }
                    else if (item.Name.Equals(XivStrings.Tail))
                    {
                        item.ModelInfo = new XivModelInfo
                        {
                            PrimaryID = int.Parse(
                                fullPath.Substring(fullPath.IndexOf("/tail", StringComparison.Ordinal) + 7, 4))
                        };
                    }
                }

                if (modItem.FilePath.Contains("chara/common"))
                {
                    item.PrimaryCategory = XivStrings.Character;

                    if (item.Name.Equals(XivStrings.Face_Paint))
                    {
                        item.ModelInfo = new XivModelInfo
                        {
                            PrimaryID = int.Parse(
                                fullPath.Substring(fullPath.LastIndexOf("_", StringComparison.Ordinal) + 1, 1))
                        };
                    }
                    else if (item.Name.Equals(XivStrings.Equipment_Decals))
                    {
                        item.ModelInfo = new XivModelInfo();

                        if (!fullPath.Contains("_stigma"))
                        {
                            item.ModelInfo.PrimaryID = int.Parse(
                                fullPath.Substring(fullPath.LastIndexOf("_", StringComparison.Ordinal) + 1, 3));
                        }
                    }
                }

                if (modItem.FilePath.Contains("chara/monster"))
                {
                    item.PrimaryCategory = XivStrings.Companions;

                    item.ModelInfo = new XivModelInfo
                    {
                        PrimaryID = int.Parse(fullPath.Substring(15, 4)),
                        SecondaryID = int.Parse(fullPath.Substring(fullPath.IndexOf("/body", StringComparison.Ordinal) + 7, 4))
                    };
                }

                if (modItem.FilePath.Contains("chara/demihuman"))
                {
                    item.PrimaryCategory = XivStrings.Companions;

                    item.ModelInfo = new XivModelInfo
                    {
                        SecondaryID = int.Parse(fullPath.Substring(17, 4)),
                        PrimaryID = int.Parse(
                            fullPath.Substring(fullPath.IndexOf("t/e", StringComparison.Ordinal) + 3, 4))
                    };
                }

                if (modItem.FilePath.Contains("ui/"))
                {
                    item.PrimaryCategory = XivStrings.UI;

                    if (modItem.FilePath.Contains("ui/uld") || modItem.FilePath.Contains("ui/map") || modItem.FilePath.Contains("ui/loadingimage"))
                    {
                        item.ModelInfo = new XivModelInfo
                        {
                            PrimaryID = 0
                        };
                    }
                    else
                    {
                        item.ModelInfo = new XivModelInfo
                        {
                            PrimaryID = int.Parse(fullPath.Substring(fullPath.LastIndexOf("/", StringComparison.Ordinal) + 1,
                                6))
                        };
                    }
                }

                if (modItem.FilePath.Contains("/hou/"))
                {
                    item.PrimaryCategory = XivStrings.Housing;
                    var furnitureRegex = new Regex("\\/general\\/([0-9]{4})\\/");

                    var match = furnitureRegex.Match(fullPath);
                    if (match.Success)
                    {
                        item.ModelInfo = new XivModelInfo
                        {
                            PrimaryID = int.Parse(match.Groups[1].Value)
                        };
                    } else
                    {
                        item.ModelInfo = new XivModelInfo();
                    }

                }
            }
            catch (Exception ex)
            {
                throw new Exception(string.Format(UIMessages.ModelDataErrorMessage, modItem.ItemName, modItem.FilePath));
            }

            return item;
        }

        /// <summary>
        /// Update the mod list entries
        /// </summary>
        /// <param name="selectedItem">The selected item to update the entries for</param>
        public Task UpdateList(Category category, CancellationTokenSource cts)
        {
            var updateLock = new object();
            ListVisibility = Visibility.Visible;
            InfoGridVisibility = Visibility.Collapsed;
            ModListPreviewList.Clear();

            ProgressValue = 0;
            ProgressText = string.Empty;

            return Task.Run(async () =>
            {
                var tx = MainWindow.DefaultTransaction;
                var selectedItem = category.Item as XivGenericItemModel;
                if (selectedItem == null) return;

                var modList = await tx.GetModList();

                var modItems = new List<Mod>();

                var allMods = modList.GetMods();
                if (ModPackFilter)
                {
                    var modPackCategory = category;

                    while (!modPackCategory.ParentCategory.Name.Equals("ModPacks"))
                    {
                        modPackCategory = modPackCategory.ParentCategory;
                    }

                    foreach (var mod in allMods)
                    {
                        if (!mod.ItemName.Equals(selectedItem.Name)) continue;

                        if (mod.ModPack == modPackCategory.Name)
                        {
                            modItems.Add(mod);
                        }
                        else
                        {
                            modItems.Add(mod);
                        }
                    }
                }
                else
                {
                    modItems =
                        (from mod in allMods
                         where mod.ItemName.Equals(selectedItem.Name)
                         select mod).ToList();
                }

                var modNum = 0;

                await Task.Run(async () =>
                {
                    foreach (var modItem in modItems)
                    {
                        if (modItem.IsInternal()) continue;

                        var itemPath = modItem.FilePath;

                        var modListModel = new ModListModel
                        {
                            ModItem = modItem
                        };

                        string parent = null;
                        if(_modListParents.ContainsKey(modItem.FilePath) && _modListParents[modItem.FilePath] != null && _modListParents[modItem.FilePath].Count > 0)
                        {
                            parent = _modListParents[modItem.FilePath][0];
                        }

                        var suffix = "";
                        try
                        {
                            suffix = Path.GetExtension(modItem.FilePath).Substring(1);
                        }
                        catch
                        {
                            // No-op.
                        }

                        // Race
                        modListModel.ItemName = SimpleModpackEntry.GetFancyName(modItem.ItemName, modItem.FilePath);

                        // File Name
                        modListModel.FileName = Path.GetFileName(modItem.FilePath);

                        // Type
                        modListModel.Type = SimpleModpackEntry.GetType(modItem.FilePath);

                        // Material
                        if (suffix == "tex" && parent != null)
                        {
                            modListModel.Material = SimpleModpackEntry.GetMaterialId(parent);
                        }
                        else
                        {
                            modListModel.Material = SimpleModpackEntry.GetMaterialId(modItem.FilePath);
                        }

                        // Race
                        if (suffix == "tex" && parent != null)
                        {
                            modListModel.Race = SimpleModpackEntry.GetRace(parent).GetDisplayName();
                        }
                        else
                        {
                            modListModel.Race = SimpleModpackEntry.GetRace(modItem.FilePath).GetDisplayName();
                        }


                        // Added files which are currently disabled cannot be previewed, as their index file entries don't exist.
                        // (???) The above comment is wrong.  The data still exists in the Dats, and we have the offset in the modlist file...
                        var state = await modItem.GetState(tx);
                        if (!(modItem.OriginalOffset8x == modItem.ModOffset8x && state != EModState.Enabled))
                        {
                            // Image
                            if (itemPath.Contains(".mtrl"))
                            {
                                try
                                {
                                    var mtrlData = await Mtrl.GetXivMtrl(modItem.FilePath, false, tx);
                                    if (mtrlData != null)
                                    {

                                        var floats = Half.ConvertToFloat(mtrlData.ColorSetData.ToArray());

                                        var floatArray = Utilities.ToByteArray(floats);

                                        if (floatArray.Length > 0)
                                        {
                                            using (var img = Image.LoadPixelData<RgbaVector>(floatArray, 4, 16))
                                            {
                                                img.Mutate(x => x.Opacity(1));

                                                BitmapImage bmp;

                                                using (var ms = new MemoryStream())
                                                {
                                                    img.Save(ms, new BmpEncoder());

                                                    bmp = new BitmapImage();
                                                    bmp.BeginInit();
                                                    bmp.StreamSource = ms;
                                                    bmp.CacheOption = BitmapCacheOption.OnLoad;
                                                    bmp.EndInit();
                                                    bmp.Freeze();
                                                }

                                                modListModel.Image =
                                                    Application.Current.Dispatcher.Invoke(() => bmp);
                                            }
                                        }
                                    }
                                }
                                catch (Exception ex)
                                {
                                    // User doesn't need to be bombarded with error messages if something is broken here.
                                    /*FlexibleMessageBox.Show(
                                        string.Format(UIMessages.MaterialFileReadErrorMessage, modItem.FilePath,
                                            ex.Message),
                                        UIMessages.MaterialDataReadErrorTitle,
                                        MessageBoxButtons.OK, MessageBoxIcon.Error);*/
                                }

                            }
                            else if (itemPath.Contains(".mdl"))
                            {
                                modListModel.Image = Application.Current.Dispatcher.Invoke(() => new BitmapImage(
                                    new Uri("pack://application:,,,/FFXIV_TexTools;component/Resources/3DModel.png")));
                            }
                            else if (itemPath.Contains(".imc") || itemPath.Contains(".eqp") || itemPath.Contains(".eqdp") || itemPath.Contains(".meta"))
                            {
                                modListModel.Image = Application.Current.Dispatcher.Invoke(() => new BitmapImage(
                                    new Uri("pack://application:,,,/FFXIV_TexTools;component/Resources/Metadata.png")));
                            }
                            else if(itemPath.Contains(".tex"))
                            {
                                var ttp = new TexTypePath
                                {
                                    Type = XivTexType.Diffuse,
                                    DataFile = modItem.DataFile,
                                    Path = modItem.FilePath
                                };

                                XivTex texData;
                                try
                                {
                                    texData = await Tex.GetXivTex(modItem.FilePath, false, tx);

                                    var mapBytes = await texData.GetRawPixels();

                                    using (var img = Image.LoadPixelData<Rgba32>(mapBytes, texData.Width, texData.Height))
                                    {
                                        img.Mutate(x => x.Opacity(1));

                                        BitmapImage bmp;

                                        using (var ms = new MemoryStream())
                                        {
                                            img.Save(ms, new BmpEncoder());

                                            bmp = new BitmapImage();
                                            bmp.BeginInit();
                                            bmp.StreamSource = ms;
                                            bmp.CacheOption = BitmapCacheOption.OnLoad;
                                            bmp.EndInit();
                                            bmp.Freeze();
                                        }

                                        modListModel.Image =
                                            Application.Current.Dispatcher.Invoke(() => bmp);
                                    }
                                }
                                catch (Exception ex)
                                {
                                    /*
                                    FlexibleMessageBox.Show(
                                        string.Format(UIMessages.TextureFileReadErrorMessage, ttp.Path, ex.Message),
                                        UIMessages.TextureDataReadErrorTitle,
                                        MessageBoxButtons.OK, MessageBoxIcon.Error);*/
                                }
                            }

                        }

                        // Status
                        if (state == EModState.Enabled)
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

                        cts.Token.ThrowIfCancellationRequested();

                        lock (updateLock)
                        {
                            progress.Report((++modNum, modItems.Count));
                        }

                        if (!cts.IsCancellationRequested)
                        {
                            Application.Current.Dispatcher.Invoke(() => ModListPreviewList.Add(modListModel));
                        }
                    }
                }, cts.Token);
            }, cts.Token);
        }

        /// <summary>
        /// Update the info grid
        /// </summary>
        /// <remarks>
        /// The info grid shows mod pack details
        /// </remarks>
        /// <param name="category">The category to update the info grid for</param>
        public async void UpdateInfoGrid(Category category)
        {
            ListVisibility = Visibility.Collapsed;
            InfoGridVisibility = Visibility.Visible;
            ModPackContentList = string.Empty;
            var enabledCount = 0;
            var disabledCount = 0;

            ProgressValue = 0;
            ProgressText = string.Empty;

            var tx = MainWindow.DefaultTransaction;

            var modList = await tx.GetModList();
            List<Mod> modPackModList = null;
            var allMods = modList.GetMods();
            var allModpacks = modList.GetModPacks();

            if (category.Name.Equals(UIStrings.Standalone_Non_ModPack))
            {
                modPackModList = (from items in allMods
                    where !items.ItemName.Equals(string.Empty) && items.ModPack == null
                    select items).ToList();

                ModPackModAuthorLabel = "[ N/A ]";
                ModPackModVersionLabel = "[ N/A ]";
            }
            else
            {
                var modPackData = (from data in allModpacks
                     where data.Name == category.Name
                    select data).FirstOrDefault();

                modPackModList = (from items in allMods
                where (items.ModPack != null && items.ModPack == category.Name)
                    select items).ToList();

                ModPackModAuthorLabel = modPackData.Author;
                ModPackModVersionLabel = modPackData.Version;
            }

            ModPackTitle = category.Name;

            var modNameDict = new Dictionary<string, int>();

            var count = 0;
            foreach (var mod in modPackModList)
            {
                if (mod.IsInternal()) continue;

                count++;

                if (await mod.GetState(tx) == EModState.Enabled)
                {
                    enabledCount++;
                }
                else
                {
                    disabledCount++;
                }

                if (!modNameDict.ContainsKey(mod.ItemName))
                {
                    modNameDict.Add(mod.ItemName, 1);
                }
                else
                {
                    modNameDict[mod.ItemName] += 1;
                }
            }
            ModPackModCountLabel = count.ToString();

            foreach (var mod in modNameDict)
            {
                ModPackContentList += $"[{ mod.Value}] {mod.Key}\n";
            }

            ModToggleText = enabledCount > disabledCount ? UIStrings.Disable : UIStrings.Enable;
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
        public async Task RemoveItem(ModListModel item, Category category)
        {

            var tx = MainWindow.DefaultTransaction;
            var modList = await tx.GetModList();

            var allMods = modList.GetMods();
            var remainingList = (from items in allMods
                                 where items.ItemName == item.ModItem.ItemName
                                select items).ToList();

            if (remainingList.Count == 0)
            {
                Category parentCategory = null;
                if (ModPackFilter)
                {
                    foreach (var modPackCategory in Categories)
                    {
                        parentCategory = (from parent in modPackCategory.Categories
                            where parent.Name.Equals(item.ModItem.ItemCategory)
                            select parent).FirstOrDefault();

                        if (parentCategory != null)
                        {
                            break;
                        }
                    }
                }
                else
                {
                    parentCategory = (from parent in Categories
                        where parent.Name.Equals(item.ModItem.ItemCategory)
                        select parent).FirstOrDefault();
                }

                if (Categories != null)
                {
                    parentCategory.Categories.Remove(category);

                    if (parentCategory.Categories.Count == 0)
                    {
                        Categories.Remove(parentCategory);
                    }
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
                if (value && !_itemFilter)
                {
                    SetFilter("ItemFilter");
                }
                _itemFilter = value;
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
                if (value && !_modPackFilter)
                {
                    SetFilter("ModPackFilter");
                }
                _modPackFilter = value;
                OnPropertyChanged(nameof(ModPackFilter));
            }
        }

        /// <summary>
        /// The status of the name sort
        /// </summary>
        public bool NameSort
        {
            get => _nameSort;
            set
            {                
                if (value && !_nameSort)
                {
                    if (_modPackFilter) SetFilter("ModPackFilter");
                    if (_itemFilter) SetFilter("ItemFilter");
                }
                _nameSort = value;
                OnPropertyChanged(nameof(NameSort));
            }
        }

        /// <summary>
        /// The status of the date sort
        /// </summary>
        public bool DateSort
        {
            get => _dateSort;
            set
            {               
                if (value && !_dateSort)
                {
                    if (_modPackFilter) SetFilter("ModPackFilter");
                    if (_itemFilter) SetFilter("ItemFilter");
                }
                _dateSort = value;
                OnPropertyChanged(nameof(DateSort));
            }
        }

        public int ProgressValue
        {
            get => _progressValue;
            set
            {
                _progressValue = value;
                OnPropertyChanged(nameof(ProgressValue));
            }
        }

        public string ProgressText
        {
            get => _progressText;
            set
            {
                _progressText = value;
                OnPropertyChanged(nameof(ProgressText));
            }
        }

        /// <summary>
        /// Sets the filter for the mod list treeview
        /// </summary>
        /// <param name="type">The type of the filter</param>
        private async void SetFilter(string type)
        {
            if (type.Equals("ItemFilter"))
            {
                await GetCategoriesItemFilter();
            }
            else if (type.Equals("ModPackFilter"))
            {
                await GetCategoriesModPackFilter();
            }
        }

        public void Dispose()
        {
            Categories = null;
            ModListPreviewList = null;

            string sortMethod;
            if (_nameSort)
                sortMethod = "NameSort";
            else
                sortMethod = "DateSort";

            string filter;
            if (_modPackFilter)
                filter = "ModPack";
            else
                filter = "Item";

            Properties.Settings.Default.ModList_Filter = filter;
            Properties.Settings.Default.ModList_Sorting = sortMethod;
            Properties.Settings.Default.Save();
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
            /// The name of the parent item.
            /// </summary>
            public string ItemName { get; set; }

            /// <summary>
            /// The name of the file.
            /// </summary>
            public string FileName { get; set; }

            /// <summary>
            /// The race the file is associated with.
            /// </summary>
            public string Race { get; set; }

            /// <summary>
            /// The file type and usage of the file.
            /// </summary>
            public string Type { get; set; }


            /// <summary>
            /// The material the file is associated with.
            /// </summary>
            public string Material { get; set; }

            /// <summary>
            /// The brush color reflecting the active status of the modded item
            /// </summary>
            public SolidColorBrush Active
            {
                get => _active;
                set
                {
                    _active = value;
                    OnPropertyChanged(nameof(Active));
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
                    OnPropertyChanged(nameof(ActiveOpacity));
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
                    OnPropertyChanged(nameof(ActiveBorder));
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