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
using MahApps.Metro.Controls.Dialogs;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Input;
using xivModdingFramework.General.Enums;
using xivModdingFramework.Helpers;
using xivModdingFramework.Mods;
using xivModdingFramework.SqPack.FileTypes;

namespace FFXIV_TexTools.ViewModels
{
    public class MainViewModel : INotifyPropertyChanged
    {
        private readonly DirectoryInfo _gameDirectory;
        private MainWindow _mainWindow;

        private List<Category> _categories = new List<Category>();

        private string _searchText;
        private string _dxVersionText = $"DX: {Properties.Settings.Default.DX_Version}";

        public MainViewModel(MainWindow mainWindow)
        {
            var ci = new CultureInfo(Properties.Settings.Default.Application_Language)
            {
                NumberFormat = {NumberDecimalSeparator = "."}
            };
            CultureInfo.DefaultThreadCurrentCulture = ci;
            CultureInfo.DefaultThreadCurrentUICulture = ci;
            CultureInfo.CurrentCulture = ci;
            CultureInfo.CurrentUICulture = ci;

            SetDirectories(true);

            _mainWindow = mainWindow;
            _gameDirectory = new DirectoryInfo(Properties.Settings.Default.FFXIV_Directory);

            CheckForOldModList();
            CheckGameVersion();
            CheckIndexFiles();

            try
            {
                FillTree();
            }
            catch (Exception ex)
            {
                FlexibleMessageBox.Show($"There was an error getting the Items List\n\n{ex.Message}", $"Items List Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

            SetDefaults();
        }

        /// <summary>
        /// Checks for older modlist
        /// </summary>
        private void CheckForOldModList()
        {
            var oldModListFileDirectory = new DirectoryInfo($"{Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)}/TexTools/TexTools.modlist");

            if (File.Exists(oldModListFileDirectory.FullName))
            {
                var modListContent = File.ReadAllLines(oldModListFileDirectory.FullName);

                if (modListContent.Length > 0)
                {
                    var warningMessage =
                        "Older TexTools ModList Found.\n\nThe Older ModList is incompatible with this version.\n\nIn order to use this version, all previous mods will be disabled, and the previous ModList erased.\n\n" +
                        "If you would like to retain your mods, it is recommended that you create a backup ModPack in the older TexTools Version, then import it into this one.\n\nWould you like to continue?";

                    if (FlexibleMessageBox.Show(
                            $"{warningMessage}",
                            "Older ModList Found", MessageBoxButtons.YesNo, MessageBoxIcon.Question) ==
                        DialogResult.Yes)
                    {
                        var modding = new Modding(_gameDirectory);
                        var index = new Index(_gameDirectory);
                        var dat = new Dat(_gameDirectory);
                        var error = false;

                        if (index.IsIndexLocked(XivDataFile._0A_Exd))
                        {
                            FlexibleMessageBox.Show("Unable to continue while game is running.\n\nPlease exit the game and try again.", $"ModList Disable Failed", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                            error = true;
                        }
                        else
                        {
                            try
                            {
                                modding.DisableOldModList(oldModListFileDirectory);
                            }
                            catch (Exception ex)
                            {
                                error = true;
                                var message =
                                    $"There was an error attempting to disable a mod from previous version.\n\nError Message:\n{ex.Message}\n\nIt is recommended to do a Start Over from the previous version first.";
                                FlexibleMessageBox.Show(message, $"Previous Version Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            }
                        }

                        if (!error)
                        {
                            File.Delete(oldModListFileDirectory.FullName);

                            // Delete modded dat files
                            foreach (var xivDataFile in (XivDataFile[]) Enum.GetValues(typeof(XivDataFile)))
                            {
                                var datFiles = dat.GetModdedDatList(xivDataFile);

                                foreach (var datFile in datFiles)
                                {
                                    File.Delete(datFile);
                                }
                            }
                        }
                        else
                        {
                            System.Windows.Application.Current.Shutdown();
                        }
                    }
                    else
                    {
                        System.Windows.Application.Current.Shutdown();
                    }
                }
            }
        }

        private void CheckIndexFiles()
        {
            var xivDataFiles = new XivDataFile[] { XivDataFile._0A_Exd, XivDataFile._01_Bgcommon, XivDataFile._04_Chara, XivDataFile._06_Ui };
            var problemChecker = new ProblemChecker(_gameDirectory);

            foreach (var xivDataFile in xivDataFiles)
            {
                var errorFound = problemChecker.CheckIndexDatCounts(xivDataFile);

                if (errorFound)
                {
                    problemChecker.RepairIndexDatCounts(xivDataFile);
                }
            }
        }

        /// <summary>
        /// Asks for game directory and sets default save directory
        /// </summary>
        private void SetDirectories(bool valid)
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

                SetSaveDirectory();

                SetBackupsDirectory();

                SetModPackDirectory();

                var modding = new Modding(new DirectoryInfo(Properties.Settings.Default.FFXIV_Directory));
                modding.CreateModlist();
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

        private void SetSaveDirectory()
        {
            if (string.IsNullOrEmpty(Properties.Settings.Default.Save_Directory))
            {
                var md = $"{Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)}/TexTools/Saved";
                Directory.CreateDirectory(md);
                Properties.Settings.Default.Save_Directory = md;
                Properties.Settings.Default.Save();
            }
            else
            {
                if (!Directory.Exists(Properties.Settings.Default.Save_Directory))
                {
                    Directory.CreateDirectory(Properties.Settings.Default.Save_Directory);
                }
            }
        }

        private void SetBackupsDirectory()
        {
            if (string.IsNullOrEmpty(Properties.Settings.Default.Backup_Directory))
            {
                var md = $"{Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)}/TexTools/Index_Backups";
                Directory.CreateDirectory(md);
                Properties.Settings.Default.Backup_Directory = md;
                Properties.Settings.Default.Save();
            }
            else
            {
                if (!Directory.Exists(Properties.Settings.Default.Backup_Directory))
                {
                    Directory.CreateDirectory(Properties.Settings.Default.Backup_Directory);
                }
            }
        }

        private void SetModPackDirectory()
        {
            if (string.IsNullOrEmpty(Properties.Settings.Default.ModPack_Directory))
            {
                var md = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) + "/TexTools/ModPacks";
                Directory.CreateDirectory(md);
                Properties.Settings.Default.ModPack_Directory = md;
                Properties.Settings.Default.Save();
            }
            else
            {
                if (!Directory.Exists(Properties.Settings.Default.ModPack_Directory))
                {
                    Directory.CreateDirectory(Properties.Settings.Default.ModPack_Directory);
                }
            }
        }

        private void CheckGameVersion()
        {
            var applicationVersion = FileVersionInfo.GetVersionInfo(System.Reflection.Assembly.GetExecutingAssembly().Location).FileVersion;

            Version ffxivVersion = null;
            var needsNewBackup = false;
            var backupMessage = "";

            var modding = new Modding(_gameDirectory);
            var backupDirectory = new DirectoryInfo(Properties.Settings.Default.Backup_Directory);

            var versionFile = $"{_gameDirectory.Parent.Parent.FullName}\\ffxivgame.ver";

            if (File.Exists(versionFile))
            {
                var versionData = File.ReadAllLines(versionFile);
                ffxivVersion = new Version(versionData[0].Substring(0, versionData[0].LastIndexOf(".")));
            }
            else
            {
                FlexibleMessageBox.Show("TexTools was unable to determine the game version.", $"Version Check Error {applicationVersion}", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (string.IsNullOrEmpty(Properties.Settings.Default.FFXIV_Version))
            {
                Properties.Settings.Default.FFXIV_Version = ffxivVersion.ToString();
                Properties.Settings.Default.Save();

                needsNewBackup = true;
                backupMessage = "New TexTools Install Detected. \nWould you like to create a new backup of your index files now? (Recommended)";
            }
            else
            {
                var versionCheck = new Version(Properties.Settings.Default.FFXIV_Version);

                if (ffxivVersion > versionCheck)
                {
                    needsNewBackup = true;
                    backupMessage = "A newer version of FFXIV was detected. \nWould you like to create a new backup of your index files now? (Recommended) \n\nWarning:\nIn order to create a clean backup, all active modifications will be set to disabled, they will have to be re-enabled manually.";
                }
            }

            if (!Directory.Exists(backupDirectory.FullName))
            {
                FlexibleMessageBox.Show("Unable to find backup directory", $"Backup Creation Failed {applicationVersion}", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (Directory.GetFiles(backupDirectory.FullName).Length == 0)
            {
                needsNewBackup = true;
                backupMessage = "No Index Backups were found. \nWould you like to create a new backup of your index files now? (Recommended) \n\nWarning:\nIn order to create a clean backup, all active modifications will be set to disabled, they will have to be re-enabled manually.";
            }

            if (needsNewBackup)
            {
                var indexFiles = new XivDataFile[] {XivDataFile._04_Chara, XivDataFile._06_Ui, XivDataFile._01_Bgcommon};
                var index = new Index(_gameDirectory);

                if (MessageBox.Show(backupMessage, "Create Backup?", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) ==
                    DialogResult.Yes)
                {
                    if (index.IsIndexLocked(XivDataFile._0A_Exd))
                    {
                        FlexibleMessageBox.Show("Unable to create backup while game is running.", $"Backup Creation Failed {applicationVersion}", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }

                    try
                    {
                        // Toggle off all mods
                        modding.ToggleAllMods(false);
                    }
                    catch (Exception ex)
                    {
                        FlexibleMessageBox.Show($"Unable to create backup files.\n\nError Message:\n{ex.Message}", $"Backup Creation Failed {applicationVersion}", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }

                    foreach (var xivDataFile in indexFiles)
                    {
                        try
                        {
                            File.Copy($"{_gameDirectory.FullName}\\{xivDataFile.GetDataFileName()}.win32.index",
                                $"{backupDirectory}\\{xivDataFile.GetDataFileName()}.win32.index", true);
                            File.Copy($"{_gameDirectory.FullName}\\{xivDataFile.GetDataFileName()}.win32.index2",
                                $"{backupDirectory}\\{xivDataFile.GetDataFileName()}.win32.index2", true);
                        }
                        catch(Exception e)
                        {
                            FlexibleMessageBox.Show($"Unable to create backups.\n\nError: {e.Message}", $"Backup Creation Failed {applicationVersion}", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        }
                    }

                    Properties.Settings.Default.FFXIV_Version = ffxivVersion.ToString();
                    Properties.Settings.Default.Save();
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

            foreach (var categoryOrder in _categoryOrderList)
            {
                var category = new Category { Name = categoryOrder, Categories = new ObservableCollection<Category>(), CategoryList = new List<string>() };
                Categories[0].Categories.Add(category);
                Categories[0].CategoryList.Add(categoryOrder);
            }

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


        private void SetDefaults()
        {
            if (string.IsNullOrEmpty(Properties.Settings.Default.Default_Race_Selection))
            {
                Properties.Settings.Default.Default_Race_Selection = XivRace.Hyur_Midlander_Male.GetDisplayName();
            }
        }

        /// <summary>
        /// Enables all mods in the mod list
        /// </summary>
        /// <param name="obj"></param>
        private async void EnableAllMods(object obj)
        {
            var index = new Index(_gameDirectory);

            if (index.IsIndexLocked(XivDataFile._0A_Exd))
            {
                FlexibleMessageBox.Show("Error Accessing Index File\n\n" +
                                        "Please exit the game before proceeding.\n" +
                                        "-----------------------------------------------------\n\n",
                    "Index Access Error", MessageBoxButtons.OK, MessageBoxIcon.Error);

                return;
            }

            var progressController = await _mainWindow.ShowProgressAsync("Enabling Mods", "Please Wait...");

            if (FlexibleMessageBox.Show(
                    "This will Enable all mods located in the modlist. \n\n Are you sure you want to proceed?",
                    "Enable All Mods", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
            {
                var task = Task.Run(() =>
                {
                    var modding = new Modding(_gameDirectory);
                    modding.ToggleAllMods(true);
                });

                task.Wait();

                await progressController.CloseAsync();

                await _mainWindow.ShowMessageAsync("Success", "All Mods were Enabled Successfully");
            }
            else
            {
                await progressController.CloseAsync();
            }
        }

        /// <summary>
        /// Disables all mods in the mod list
        /// </summary>
        private async void DisableAllMods(object obj)
        {
            var index = new Index(_gameDirectory);

            if (index.IsIndexLocked(XivDataFile._0A_Exd))
            {
                FlexibleMessageBox.Show("Error Accessing Index File\n\n" +
                                        "Please exit the game before proceeding.\n" +
                                        "-----------------------------------------------------\n\n",
                    "Index Access Error", MessageBoxButtons.OK, MessageBoxIcon.Error);

                return;
            }

            var progressController = await _mainWindow.ShowProgressAsync("Disabling Mods", "Please Wait...");

            if (FlexibleMessageBox.Show(
                    "This will Disable all mods located in the modlist. \n\n Are you sure you want to proceed?",
                    "Disable All Mods", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
            {
                var task = Task.Run((() =>
                {
                    var modding = new Modding(_gameDirectory);
                    modding.ToggleAllMods(false);
                }));

                task.Wait();

                await progressController.CloseAsync();

                await _mainWindow.ShowMessageAsync("Success", "All Mods were Disabled Successfully");
            }
            else
            {
                await progressController.CloseAsync();
            }

        }

        #endregion

        public event PropertyChangedEventHandler PropertyChanged;

        [NotifyPropertyChangedInvocator]
        protected virtual void NotifyPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        // A list containing the category order
        private readonly List<string> _categoryOrderList = new List<string>
        {
            {XivStrings.Head },
            {XivStrings.Body },
            {XivStrings.Hands },
            {XivStrings.Legs },
            {XivStrings.Feet },
            {XivStrings.Main_Hand },
            {XivStrings.Off_Hand },
            {XivStrings.Two_Handed },
            {XivStrings.Main_Off },
            {XivStrings.Ears },
            {XivStrings.Neck },
            {XivStrings.Wrists },
            {XivStrings.Rings },
            {XivStrings.Body_Hands_Legs_Feet },
            {XivStrings.Body_Hands_Legs },
            {XivStrings.Body_Legs_Feet },
            {XivStrings.Head_Body },
            {XivStrings.Legs_Feet },
            {XivStrings.All },
            {XivStrings.Food }
        };
    }
}