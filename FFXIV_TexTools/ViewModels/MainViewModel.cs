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

using FFXIV_TexTools.Annotations;
using FFXIV_TexTools.Helpers;
using FFXIV_TexTools.Models;
using FFXIV_TexTools.Resources;
using FolderSelect;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Forms;
using System.Windows.Input;
using xivModdingFramework.General.Enums;
using xivModdingFramework.Mods;

namespace FFXIV_TexTools.ViewModels
{
    public class MainViewModel : INotifyPropertyChanged
    {
        private readonly DirectoryInfo _gameDirectory;

        private List<Category> _categories = new List<Category>();

        private List<Category> _originalCategories = new List<Category>();

        private string _searchText;
        private string _dxVersionText = $"DX: {Properties.Settings.Default.DX_Version}";

        public MainViewModel()
        {
            SetDirectories(true);

            _gameDirectory = new DirectoryInfo(Properties.Settings.Default.FFXIV_Directory);

            FillTree();
        }

        /// <summary>
        /// Asks for game directory and sets default save directory
        /// </summary>
        private static void SetDirectories(bool valid)
        {
            if (valid)
            {
                var resourceManager = CommonInstallDirectories.ResourceManager;
                var resourceSet = resourceManager.GetResourceSet(CultureInfo.CurrentCulture, true, true);

                if (Properties.Settings.Default.FFXIV_Directory.Equals(""))
                {
                    var saveDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) + "/TexTools/Saved";
                    Directory.CreateDirectory(saveDirectory);
                    Properties.Settings.Default.Save_Directory = saveDirectory;
                    Properties.Settings.Default.Save();

                    var installDirectory = "";
                    foreach (DictionaryEntry commonInstallPath in resourceSet)
                    {
                        if (!Directory.Exists(commonInstallPath.Value.ToString())) continue;

                        if (FlexibleMessageBox.Show("FFXIV install directory found at \n\n" + commonInstallPath.Value + "\n\nUse this directory? ", "Install Directory Found", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                        {
                            installDirectory = commonInstallPath.Value.ToString();
                            Properties.Settings.Default.FFXIV_Directory = installDirectory;
                            Properties.Settings.Default.Save();
                            break;
                        }
                    }

                    if (string.IsNullOrEmpty(installDirectory))
                    {
                        if (FlexibleMessageBox.Show("Please locate the following directory. \n\n .../FINAL FANTASY XIV - A Realm Reborn/game/sqpack/ffxiv", "Install Directory Not Found", MessageBoxButtons.OK, MessageBoxIcon.Question) == DialogResult.OK)
                        {
                            while (!installDirectory.Contains("ffxiv"))
                            {
                                var folderSelect = new FolderSelectDialog()
                                {
                                    Title = "Select sqpack/ffxiv Folder"
                                };

                                var result = folderSelect.ShowDialog();

                                if (result)
                                {
                                    installDirectory = folderSelect.FileName;
                                }
                                else
                                {
                                    Environment.Exit(0);
                                }
                            }

                            Properties.Settings.Default.FFXIV_Directory = installDirectory;
                            Properties.Settings.Default.Save();
                        }
                        else
                        {
                            Environment.Exit(0);
                        }
                    }
                }

                // Check if it is an old Directory
                var fileLastModifiedTime = File.GetLastWriteTime(
                    $"{Properties.Settings.Default.FFXIV_Directory}\\{XivDataFile._0A_Exd.GetDataFileName()}.win32.dat0");

                if (fileLastModifiedTime.Year < 2019)
                {
                    SetDirectories(false);
                }

                if (Properties.Settings.Default.Save_Directory.Equals(""))
                {
                    var md = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) + "/TexTools/Saved";
                    Directory.CreateDirectory(md);
                    Properties.Settings.Default.Save_Directory = md;
                    Properties.Settings.Default.Save();
                }

                if (Properties.Settings.Default.Backup_Directory.Equals(""))
                {
                    var md = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) + "/TexTools/Index_Backups";
                    Directory.CreateDirectory(md);
                    Properties.Settings.Default.Backup_Directory = md;
                    Properties.Settings.Default.Save();
                }

                if (Properties.Settings.Default.ModPack_Directory.Equals(""))
                {
                    var md = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) + "/TexTools/ModPacks";
                    Directory.CreateDirectory(md);
                    Properties.Settings.Default.ModPack_Directory = md;
                    Properties.Settings.Default.Save();
                }
            }
            else
            {
                if (FlexibleMessageBox.Show("The install location chosen is out of date \n\nPlease locate the following directory. \n\n " +
                                            ".../FINAL FANTASY XIV - A Realm Reborn/game/sqpack/ffxiv", "Install Directory Not Found", MessageBoxButtons.OK, MessageBoxIcon.Question) == System.Windows.Forms.DialogResult.OK)
                {
                    var installDirectory = "";

                    while (!installDirectory.Contains("ffxiv"))
                    {
                        var folderSelect = new FolderSelectDialog()
                        {
                            Title = "Select sqpack/ffxiv Folder"
                        };

                        var result = folderSelect.ShowDialog();

                        if (result)
                        {
                            installDirectory = folderSelect.FileName;
                        }
                        else
                        {
                            Environment.Exit(0);
                        }
                    }

                    // Check if it is an old Directory
                    var fileLastModifiedTime = File.GetLastWriteTime(
                        $"{installDirectory}\\{XivDataFile._0A_Exd.GetDataFileName()}.win32.dat0");

                    if (fileLastModifiedTime.Year < 2019)
                    {
                        SetDirectories(false);
                    }
                    else
                    {
                        Properties.Settings.Default.FFXIV_Directory = installDirectory;
                        Properties.Settings.Default.Save();
                    }
                }
                else
                {
                    Environment.Exit(0);
                }
            }
        }

        /// <summary>
        /// Fills the tree view with items
        /// </summary>
        private void FillTree()
        {
            Categories.Add(new Category{Name = "Gear", Categories = new ObservableCollection<Category>(), CategoryList = new List<string>()});
            Categories.Add(new Category{Name = "Character", Categories = new ObservableCollection<Category>(), CategoryList = new List<string>() });
            Categories.Add(new Category{Name = "Companions", Categories = new ObservableCollection<Category>(), CategoryList = new List<string>() });
            Categories.Add(new Category{Name = "UI", Categories = new ObservableCollection<Category>(), CategoryList = new List<string>() });
            Categories.Add(new Category{Name = "Housing", Categories = new ObservableCollection<Category>(), CategoryList = new List<string>() });

            var itemList = new ItemsList(_gameDirectory);

            // Gear List
            var gearList = itemList.GetGearList();

            foreach (var xivGear in gearList)
            {
                if (Categories[0].CategoryList.Contains(xivGear.ItemCategory))
                {
                    var cat = (from category1 in Categories[0].Categories
                        where category1.Name == xivGear.ItemCategory
                        select category1).FirstOrDefault();

                    cat.Categories.Add(new Category{Name = xivGear.Name, Item = xivGear});
                }
                else
                {
                    var category = new Category {Name = xivGear.ItemCategory, Categories = new ObservableCollection<Category>(), CategoryList = new List<string>()};
                    category.Categories.Add(new Category{Name = xivGear.Name, Item = xivGear});
                    category.CategoryList.Add(xivGear.Name);
                    Categories[0].Categories.Add(category);
                    Categories[0].CategoryList.Add(xivGear.ItemCategory);
                }
            }

            // Character List
            var characterList = itemList.GetCharacterList();

            foreach (var xivCharacter in characterList)
            {
                Categories[1].Categories.Add(new Category{Name = xivCharacter.Name, Item = xivCharacter});
                Categories[1].CategoryList.Add(xivCharacter.Name);
            }

            // Companion List
            var companionList = itemList.GetCompanionList();

            var minionCategory = new Category { Name = "Minions", Categories = new ObservableCollection<Category>()};
            Categories[2].Categories.Add(minionCategory);
            foreach (var xivMinion in companionList.MinionList)
            {
                minionCategory.Categories.Add(new Category{Name = xivMinion.Name, Item = xivMinion});
            }

            var mountCategory = new Category { Name = "Mounts", Categories = new ObservableCollection<Category>() };
            Categories[2].Categories.Add(mountCategory);
            foreach (var xivMount in companionList.MountList)
            {
                mountCategory.Categories.Add(new Category { Name = xivMount.Name, Item = xivMount });
            }

            var petCategory = new Category { Name = "Pets", Categories = new ObservableCollection<Category>() };
            Categories[2].Categories.Add(petCategory);
            foreach (var xivPet in companionList.PetList)
            {
                petCategory.Categories.Add(new Category { Name = xivPet.Name, Item = xivPet });
            }

            // UI List
            var uiList = itemList.GetUIList();

            foreach (var xivUi in uiList)
            {
                if (xivUi.ItemSubCategory != null)
                {
                    if (Categories[3].CategoryList.Contains(xivUi.ItemCategory))
                    {
                        var cat = (from category1 in Categories[3].Categories
                            where category1.Name == xivUi.ItemCategory
                            select category1).FirstOrDefault();

                        if (cat.CategoryList.Contains(xivUi.ItemSubCategory))
                        {
                            var subcat = (from category1 in cat.Categories
                                where category1.Name == xivUi.ItemSubCategory
                                select category1).FirstOrDefault();

                            subcat.Categories.Add(new Category { Name = xivUi.Name, Item = xivUi });
                        }
                        else
                        {
                            var subCategory = new Category { Name = xivUi.ItemSubCategory, Categories = new ObservableCollection<Category>() };
                            subCategory.Categories.Add(new Category { Name = xivUi.Name, Item = xivUi });

                            cat.Categories.Add(subCategory);
                            cat.CategoryList.Add(xivUi.ItemSubCategory);
                        }
                    }
                    else
                    {
                        var category = new Category { Name = xivUi.ItemCategory, Categories = new ObservableCollection<Category>(), CategoryList = new List<string>()};
                        var subCategory = new Category { Name = xivUi.ItemSubCategory, Categories = new ObservableCollection<Category>()};
                        subCategory.Categories.Add(new Category { Name = xivUi.Name, Item = xivUi });
                 
                        category.Categories.Add(subCategory);
                        category.CategoryList.Add(xivUi.ItemSubCategory);

                        Categories[3].Categories.Add(category);
                        Categories[3].CategoryList.Add(xivUi.ItemCategory);
                    }
                }
                else
                {
                    if (Categories[3].CategoryList.Contains(xivUi.ItemCategory))
                    {
                        var cat = (from category1 in Categories[3].Categories
                            where category1.Name == xivUi.ItemCategory
                            select category1).FirstOrDefault();

                        cat.Categories.Add(new Category { Name = xivUi.Name, Item = xivUi });
                    }
                    else
                    {
                        var category = new Category { Name = xivUi.ItemCategory, Categories = new ObservableCollection<Category>() };
                        category.Categories.Add(new Category { Name = xivUi.Name, Item = xivUi });
                        Categories[3].Categories.Add(category);
                        Categories[3].CategoryList.Add(xivUi.ItemCategory);
                    }
                }
            }

            // Housing List
            var housingList = itemList.GetHousingList();

            foreach (var xivFurniture in housingList)
            {
                if (Categories[4].CategoryList.Contains(xivFurniture.ItemCategory))
                {
                    var cat = (from category1 in Categories[4].Categories
                        where category1.Name == xivFurniture.ItemCategory
                        select category1).FirstOrDefault();

                    cat.Categories.Add(new Category{Name = xivFurniture.Name, Item = xivFurniture});
                }
                else
                {
                    var category = new Category { Name = xivFurniture.ItemCategory, Categories = new ObservableCollection<Category>(), CategoryList = new List<string>() };
                    category.Categories.Add(new Category { Name = xivFurniture.Name, Item = xivFurniture });
                    category.CategoryList.Add(xivFurniture.Name);
                    Categories[4].Categories.Add(category);
                    Categories[4].CategoryList.Add(xivFurniture.ItemCategory);
                }
            }

            _originalCategories = Categories;
        }

        /// <summary>
        /// The DX Version
        /// </summary>
        public string DXVersionText
        {
            get => _dxVersionText;
            set
            {
                _dxVersionText = value;
                NotifyPropertyChanged(nameof(DXVersionText));
            }
        }

        /// <summary>
        /// The list of categories
        /// </summary>
        public List<Category> Categories
        {
            get => _categories;
            set
            {
                _categories = value;
                NotifyPropertyChanged(nameof(Categories));
            }
        }

        /// <summary>
        /// The text from the search box
        /// </summary>
        public string SearchText
        {
            get => _searchText;
            set
            {
                _searchText = value;
                NotifyPropertyChanged(nameof(SearchText));
                SearchTextFilter();
            }
        }

        /// <summary>
        /// The search Text Filter
        /// </summary>
        private void SearchTextFilter()
        {
            if (SearchText.Length > 2)
            {
                var filteredCategory = new List<Category>();

                // Gear / Companion
                foreach (var category in _originalCategories)
                {
                    var mainCategory = new Category{Name = category.Name, CategoryList = new List<string>(), Categories = new ObservableCollection<Category>()};

                    if (category.Categories.Count > 0)
                    {
                        foreach (var subCategory in category.Categories)
                        {
                            if (subCategory.Categories != null && subCategory.Categories.Count > 0)
                            {
                                var sCategory = new Category { Name = subCategory.Name, CategoryList = new List<string>(), Categories = new ObservableCollection<Category>() };

                                foreach (var item in subCategory.Categories)
                                {
                                    if (item.Categories != null && item.Categories.Count > 0)
                                    {
                                        var uiCat = new Category{ Name = item.Name, CategoryList = new List<string>(), Categories = new ObservableCollection<Category>() };

                                        foreach (var uiItem in item.Categories)
                                        {
                                            if (uiItem.Name.ToLower().Contains(SearchText.ToLower()))
                                            {
                                                uiCat.IsExpanded = true;
                                                uiCat.CategoryList.Add(item.Name);
                                                uiCat.Categories.Add(uiItem);
                                            }
                                        }

                                        if (uiCat.Categories.Count > 0)
                                        {
                                            sCategory.CategoryList.Add(item.Name);
                                            sCategory.Categories.Add(uiCat);
                                        }
                                    }
                                    else
                                    {
                                        if (item.Name.ToLower().Contains(SearchText.ToLower()))
                                        {
                                            sCategory.CategoryList.Add(item.Name);
                                            sCategory.Categories.Add(item);
                                        }
                                    }
                                }

                                if (sCategory.CategoryList.Count > 0)
                                {
                                    sCategory.IsExpanded = true;
                                    mainCategory.CategoryList.Add(sCategory.Name);
                                    mainCategory.Categories.Add(sCategory);
                                }
                            }
                            else
                            {
                                if (subCategory.Name.ToLower().Contains(SearchText.ToLower()))
                                {
                                    mainCategory.CategoryList.Add(subCategory.Name);
                                    mainCategory.Categories.Add(subCategory);
                                }
                            }
                        }
                    }

                    if (mainCategory.CategoryList.Count > 0)
                    {
                        mainCategory.IsExpanded = true;
                        filteredCategory.Add(mainCategory);
                    }
                }

                Categories = filteredCategory;
            }
            else
            {
                Categories = _originalCategories;
            }
        }

        #region MenuItems

        public ICommand DXVersionCommand => new RelayCommand(SetDXVersion);
        public ICommand EnableAllModsCommand => new RelayCommand(EnableAllMods);
        public ICommand DisableAllModsCommand => new RelayCommand(DisableAllMods);

        /// <summary>
        /// Sets the DX version for the application
        /// </summary>
        private void SetDXVersion(object obj)
        {
            if (DXVersionText.Contains("11"))
            {
                Properties.Settings.Default.DX_Version = "9";
                Properties.Settings.Default.Save();
            }
            else
            {
                Properties.Settings.Default.DX_Version = "11";
                Properties.Settings.Default.Save();
            }

            DXVersionText = $"DX: {Properties.Settings.Default.DX_Version}";
        }

        /// <summary>
        /// Enables all mods in the mod list
        /// </summary>
        /// <param name="obj"></param>
        private void EnableAllMods(object obj)
        {
            if (FlexibleMessageBox.Show(
                    "This will Enable all mods located in the modlist. \n\n Are you sure you want to proceed?",
                    "Enable All Mods", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
            {
                var modding = new Modding(_gameDirectory);
                modding.ToggleAllMods(true);

                FlexibleMessageBox.Show("All Mods were Enabled Successfully.", "Success", MessageBoxButtons.OK, MessageBoxIcon.None);
            }

        }

        /// <summary>
        /// Disables all mods in the mod list
        /// </summary>
        private void DisableAllMods(object obj)
        {
            if (FlexibleMessageBox.Show(
                    "This will Disable all mods located in the modlist. \n\n Are you sure you want to proceed?",
                    "Disable All Mods", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
            {
                var modding = new Modding(_gameDirectory);
                modding.ToggleAllMods(false);

                FlexibleMessageBox.Show("All Mods were Disabled Successfully.", "Success", MessageBoxButtons.OK, MessageBoxIcon.None);
            }

        }

        #endregion

        public event PropertyChangedEventHandler PropertyChanged;

        [NotifyPropertyChangedInvocator]
        protected virtual void NotifyPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}