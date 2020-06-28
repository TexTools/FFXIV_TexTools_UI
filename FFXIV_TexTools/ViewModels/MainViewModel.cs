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
using System.Windows;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Interop;
using xivModdingFramework.General.Enums;
using xivModdingFramework.Helpers;
using xivModdingFramework.Mods;
using xivModdingFramework.SqPack.FileTypes;

namespace FFXIV_TexTools.ViewModels
{
    public class MainViewModel : INotifyPropertyChanged
    {
        private DirectoryInfo _gameDirectory;
        private readonly MainWindow _mainWindow;

        private ObservableCollection<Category> _categories = new ObservableCollection<Category>();

        private string _searchText, _progressLabel;
        private string _dxVersionText = $"DX: {Properties.Settings.Default.DX_Version}";
        private int _progressValue;
        private Visibility _progressBarVisible, _progressLabelVisible;
        private Index _index;
        private System.Windows.Forms.IWin32Window _win32Window;
        private ProgressDialogController _progressController;

        public MainViewModel(MainWindow mainWindow)
        {
            _mainWindow = mainWindow;
            _win32Window = new WindowWrapper(new WindowInteropHelper(_mainWindow).Handle);

            try
            {
                Initialize();
            }
            catch (Exception ex)
            {
                FlexibleMessageBox.Show($"There was an error getting the Items List\n\n{ex.Message}", $"Items List Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async void Initialize()
        {
            SetDirectories(true);
            _gameDirectory = new DirectoryInfo(Properties.Settings.Default.FFXIV_Directory);
            _index = new Index(_gameDirectory);

            ProgressLabel = "Checking for old installs...";
            CheckForOldModList();
            ProgressLabel = "Checking Game Version...";
            await CheckGameVersion();
            ProgressLabel = "Checking Index Files...";
            await CheckIndexFiles();

            IProgress<(int current, string category)> progress = new Progress<(int current, string category)>((prog) =>
            {
                if (prog.category == "Done")
                {
                    ProgressBarVisible = Visibility.Collapsed;
                    ProgressLabelVisible = Visibility.Collapsed;
                }
                else
                {
                    ProgressValue = prog.current;
                    ProgressLabel = $"Loading {prog.category} List";
                }
            });

            _mainWindow.ItemSearchTextBox.IsEnabled = false;
            try
            {
                await FillTree(progress);
            }
            catch(Exception e)
            {
                var lang = Properties.Settings.Default.Application_Language;

                if (lang.Equals("zh") || lang.Equals("ko"))
                {
                    if (FlexibleMessageBox.Show(UIMessages.LanguageError,
                            UIMessages.LanguageErrorTitle, MessageBoxButtons.OK, MessageBoxIcon.Error) ==
                        DialogResult.OK)
                    {
                        Properties.Settings.Default.Application_Language = "en";
                        Properties.Settings.Default.Save();

                        System.Windows.Forms.Application.Restart();
                        System.Windows.Application.Current.Shutdown();
                    }
                }
            }
            _mainWindow.Menu_ModConverter.IsEnabled = true;
            _mainWindow.ItemSearchTextBox.IsEnabled = true;
            _mainWindow.SetFilter();
            SetDefaults();
        }

        /// <summary>
        /// Checks for older modlist
        /// </summary>
        private async void CheckForOldModList()
        {
            var oldModListFileDirectory =
                new DirectoryInfo(
                    $"{Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)}/TexTools/TexTools.modlist");

            if (File.Exists(oldModListFileDirectory.FullName))
            {
                var modListContent = File.ReadAllLines(oldModListFileDirectory.FullName);

                if (modListContent.Length > 0)
                {
                    if (FlexibleMessageBox.Show(_win32Window, 
                            UIMessages.OldTexToolsFoundMessage, UIMessages.OldModListFoundTitle,
                            MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                    {
                        var modding = new Modding(_gameDirectory);

                        var dat = new Dat(_gameDirectory);
                        var error = false;

                        if (_index.IsIndexLocked(XivDataFile._0A_Exd))
                        {
                            FlexibleMessageBox.Show(_win32Window, UIMessages.ModListIndexLockedErrorMessage,
                                UIMessages.ModListDisableFailedTitle, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                            error = true;
                        }
                        else
                        {
                            try
                            {
                                await modding.DisableOldModList(oldModListFileDirectory);
                            }
                            catch (Exception ex)
                            {
                                error = true;
                                FlexibleMessageBox.Show(_win32Window, 
                                    string.Format(UIMessages.OldModListDisableFailedMessage, ex.Message),
                                    UIMessages.PreviousVersionErrorTitle, MessageBoxButtons.OK,
                                    MessageBoxIcon.Error);
                            }
                        }

                        if (!error)
                        {
                            File.Delete(oldModListFileDirectory.FullName);

                            // Delete modded dat files
                            foreach (var xivDataFile in (XivDataFile[])Enum.GetValues(typeof(XivDataFile)))
                            {
                                var datFiles = await dat.GetModdedDatList(xivDataFile);

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

        private async Task CheckIndexFiles()
        {
            var xivDataFiles = new XivDataFile[] { XivDataFile._0A_Exd, XivDataFile._01_Bgcommon, XivDataFile._04_Chara, XivDataFile._06_Ui };
            var problemChecker = new ProblemChecker(_gameDirectory);

            foreach (var xivDataFile in xivDataFiles)
            {
                var errorFound = await problemChecker.CheckIndexDatCounts(xivDataFile);

                if (errorFound)
                {
                    await problemChecker.RepairIndexDatCounts(xivDataFile);
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

                        if (FlexibleMessageBox.Show(string.Format(UIMessages.InstallDirectoryFoundMessage, commonInstallPath.Value), UIMessages.InstallDirectoryFoundTitle, MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                        {
                            installDirectory = commonInstallPath.Value.ToString();
                            Properties.Settings.Default.FFXIV_Directory = installDirectory;
                            Properties.Settings.Default.Save();
                            break;
                        }
                    }

                    if (string.IsNullOrEmpty(installDirectory))
                    {
                        if (FlexibleMessageBox.Show(UIMessages.InstallDirectoryNotFoundMessage, UIMessages.InstallDirectoryNotFoundTitle, MessageBoxButtons.OK, MessageBoxIcon.Question) == DialogResult.OK)
                        {
                            while (!installDirectory.Contains("ffxiv"))
                            {
                                var folderSelect = new FolderSelectDialog()
                                {
                                    Title = UIMessages.SelectffxivFolderTitle
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

                if (fileLastModifiedTime.Year < 2020)
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
                if (FlexibleMessageBox.Show(UIMessages.OutOfDateInstallMessage, UIMessages.OutOfDateInstallTitle, MessageBoxButtons.OK, MessageBoxIcon.Question) == System.Windows.Forms.DialogResult.OK)
                {
                    var installDirectory = "";

                    while (!installDirectory.Contains("ffxiv"))
                    {
                        var folderSelect = new FolderSelectDialog()
                        {
                            Title = UIMessages.SelectffxivFolderTitle
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

        private Task CheckGameVersion()
        {
            return Task.Run(async () =>
            {
                var applicationVersion = FileVersionInfo
                    .GetVersionInfo(System.Reflection.Assembly.GetExecutingAssembly().Location).FileVersion;

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
                    FlexibleMessageBox.Show(_win32Window, UIMessages.GameVersionErrorMessage,
                        string.Format(UIMessages.GameVersionErrorTitle, applicationVersion), MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                    return;
                }

                if (string.IsNullOrEmpty(Properties.Settings.Default.FFXIV_Version))
                {
                    Properties.Settings.Default.FFXIV_Version = ffxivVersion.ToString();
                    Properties.Settings.Default.Save();

                    needsNewBackup = true;
                    backupMessage = UIMessages.NewInstallDetectedBackupMessage;
                }
                else
                {
                    var versionCheck = new Version(Properties.Settings.Default.FFXIV_Version);

                    if (ffxivVersion > versionCheck)
                    {
                        needsNewBackup = true;
                        backupMessage = UIMessages.NewerVersionDetectedBackupMessage;
                    }
                }

                if (!Directory.Exists(backupDirectory.FullName))
                {
                    FlexibleMessageBox.Show(_win32Window, UIMessages.BackupsDirectoryErrorMessage, UIMessages.BackupFailedTitle,
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                if (Directory.GetFiles(backupDirectory.FullName).Length == 0)
                {
                    needsNewBackup = true;
                    backupMessage = UIMessages.NoBackupsFoundMessage;
                }

                if (needsNewBackup)
                {
                    var indexFiles = new XivDataFile[]
                        { XivDataFile._0A_Exd, XivDataFile._04_Chara, XivDataFile._06_Ui, XivDataFile._01_Bgcommon };

                    if (FlexibleMessageBox.Show(_win32Window, backupMessage, UIMessages.CreateBackupTitle, MessageBoxButtons.YesNo,
                            MessageBoxIcon.Warning) == DialogResult.Yes)
                    {
                        if (_index.IsIndexLocked(XivDataFile._0A_Exd))
                        {
                            FlexibleMessageBox.Show(_win32Window, UIMessages.IndexLockedBackupFailedMessage,
                                UIMessages.BackupFailedTitle, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                            return;
                        }

                        try
                        {
                            // Toggle off all mods
                            await modding.ToggleAllMods(false);
                        }
                        catch (Exception ex)
                        {
                            FlexibleMessageBox.Show(_win32Window, string.Format(UIMessages.BackupFailedErrorMessage, ex.Message),
                                UIMessages.BackupFailedTitle, MessageBoxButtons.OK, MessageBoxIcon.Warning);
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
                            catch (Exception e)
                            {
                                FlexibleMessageBox.Show(_win32Window, string.Format(UIMessages.BackupFailedErrorMessage, e.Message),
                                    UIMessages.BackupFailedTitle, MessageBoxButtons.OK, MessageBoxIcon.Error);
                            }
                        }

                        Properties.Settings.Default.FFXIV_Version = ffxivVersion.ToString();
                        Properties.Settings.Default.Save();
                    }
                }
            });
        }

        /// <summary>
        /// Fills the tree view with items
        /// </summary>
        public async Task FillTree(IProgress<(int current, string total)> progress)
        {
            Categories.Add(new Category{Name = XivStrings.Gear, Categories = new ObservableCollection<Category>(), CategoryList = new List<string>() });
            Categories.Add(new Category{Name = XivStrings.Character, Categories = new ObservableCollection<Category>(), CategoryList = new List<string>() });
            Categories.Add(new Category{Name = XivStrings.Companions, Categories = new ObservableCollection<Category>(), CategoryList = new List<string>() });
            Categories.Add(new Category{Name = XivStrings.UI, Categories = new ObservableCollection<Category>(), CategoryList = new List<string>() });
            Categories.Add(new Category{Name = XivStrings.Housing, Categories = new ObservableCollection<Category>(), CategoryList = new List<string>() });

            var itemList = new ItemsList(_gameDirectory);

            foreach (var categoryOrder in _categoryOrderList)
            {
                var category = new Category { Name = categoryOrder, Categories = new ObservableCollection<Category>(), CategoryList = new List<string>(), ParentCategory = Categories[0] };
                Categories[0].Categories.Add(category);
                Categories[0].CategoryList.Add(categoryOrder);
            }

            //var gearListTask      = itemList.GetGearList();
            //var characterListTask = itemList.GetCharacterList();
            //var companionListTask = itemList.GetCompanionList();
            //var uiListTask        = itemList.GetUIList();
            //var housingListTask   = itemList.GetHousingList();

            // Gear List
            progress.Report((0, "Gear"));
            var gearList = await itemList.GetGearList();
            //var gearList = await gearListTask;

            foreach (var xivGear in gearList)
            {
                if (Categories[0].CategoryList.Contains(xivGear.SecondaryCategory))
                {
                    var cat = (from category1 in Categories[0].Categories
                        where category1.Name == xivGear.SecondaryCategory
                        select category1).FirstOrDefault();

                    cat.Categories.Add(new Category{Name = xivGear.Name, Item = xivGear, ParentCategory = cat });
                }
                else
                {
                    var category = new Category {Name = xivGear.SecondaryCategory, Categories = new ObservableCollection<Category>(), CategoryList = new List<string>(), ParentCategory = Categories[0]};
                    category.Categories.Add(new Category{Name = xivGear.Name, Item = xivGear});
                    category.CategoryList.Add(xivGear.Name);
                    Categories[0].Categories.Add(category);
                    Categories[0].CategoryList.Add(xivGear.SecondaryCategory);
                }
            }

            // Character List
            progress.Report((20, "Character"));
            var characterList = await itemList.GetCharacterList();
            //var characterList = await characterListTask;

            foreach (var xivCharacter in characterList)
            {
                Categories[1].Categories.Add(new Category{Name = xivCharacter.Name, Item = xivCharacter});
                Categories[1].CategoryList.Add(xivCharacter.Name);
            }

            // Companion List
            progress.Report((40, "Companion"));
            var companionList = await itemList.GetCompanionList();
            //var companionList = await companionListTask;

            var minionCategory = new Category { Name = XivStrings.Minions, Categories = new ObservableCollection<Category>()};
            Categories[2].Categories.Add(minionCategory);
            foreach (var xivMinion in companionList.MinionList)
            {
                minionCategory.Categories.Add(new Category{Name = xivMinion.Name, Item = xivMinion});
            }

            var mountCategory = new Category { Name = XivStrings.Mounts, Categories = new ObservableCollection<Category>() };
            Categories[2].Categories.Add(mountCategory);
            foreach (var xivMount in companionList.MountList)
            {
                mountCategory.Categories.Add(new Category { Name = xivMount.Name, Item = xivMount });
            }

            var petCategory = new Category { Name = XivStrings.Pets, Categories = new ObservableCollection<Category>() };
            Categories[2].Categories.Add(petCategory);
            foreach (var xivPet in companionList.PetList)
            {
                petCategory.Categories.Add(new Category { Name = xivPet.Name, Item = xivPet });
            }

            var ornamentCategory = new Category { Name = "Ornaments", Categories = new ObservableCollection<Category>() };
            Categories[2].Categories.Add(ornamentCategory);
            foreach (var xivOrnament in companionList.OrnamentList)
            {
                ornamentCategory.Categories.Add(new Category { Name = xivOrnament.Name, Item = xivOrnament });
            }

            // UI List
            progress.Report((60, "UI"));
            var uiList = await itemList.GetUIList();
            //var uiList = await uiListTask;

            foreach (var xivUi in uiList)
            {
                if (xivUi.TertiaryCategory != null)
                {
                    if (Categories[3].CategoryList.Contains(xivUi.SecondaryCategory))
                    {
                        var cat = (from category1 in Categories[3].Categories
                            where category1.Name == xivUi.SecondaryCategory
                            select category1).FirstOrDefault();

                        if (cat.CategoryList.Contains(xivUi.TertiaryCategory))
                        {
                            var subcat = (from category1 in cat.Categories
                                where category1.Name == xivUi.TertiaryCategory
                                select category1).FirstOrDefault();

                            subcat.Categories.Add(new Category { Name = xivUi.Name, Item = xivUi });
                        }
                        else
                        {
                            var subCategory = new Category { Name = xivUi.TertiaryCategory, Categories = new ObservableCollection<Category>() };
                            subCategory.Categories.Add(new Category { Name = xivUi.Name, Item = xivUi });

                            cat.Categories.Add(subCategory);
                            cat.CategoryList.Add(xivUi.TertiaryCategory);
                        }
                    }
                    else
                    {
                        var category = new Category { Name = xivUi.SecondaryCategory, Categories = new ObservableCollection<Category>(), CategoryList = new List<string>()};
                        var subCategory = new Category { Name = xivUi.TertiaryCategory, Categories = new ObservableCollection<Category>()};
                        subCategory.Categories.Add(new Category { Name = xivUi.Name, Item = xivUi });
                 
                        category.Categories.Add(subCategory);
                        category.CategoryList.Add(xivUi.TertiaryCategory);

                        Categories[3].Categories.Add(category);
                        Categories[3].CategoryList.Add(xivUi.SecondaryCategory);
                    }
                }
                else
                {
                    if (Categories[3].CategoryList.Contains(xivUi.SecondaryCategory))
                    {
                        var cat = (from category1 in Categories[3].Categories
                            where category1.Name == xivUi.SecondaryCategory
                            select category1).FirstOrDefault();

                        cat.Categories.Add(new Category { Name = xivUi.Name, Item = xivUi });
                    }
                    else
                    {
                        var category = new Category { Name = xivUi.SecondaryCategory, Categories = new ObservableCollection<Category>() };
                        category.Categories.Add(new Category { Name = xivUi.Name, Item = xivUi });
                        Categories[3].Categories.Add(category);
                        Categories[3].CategoryList.Add(xivUi.SecondaryCategory);
                    }
                }
            }

            // Housing List
            progress.Report((80, "Housing"));
            var housingList = await itemList.GetHousingList();
            //var housingList = await housingListTask;

            foreach (var xivFurniture in housingList)
            {
                if (Categories[4].CategoryList.Contains(xivFurniture.SecondaryCategory))
                {
                    var cat = (from category1 in Categories[4].Categories
                        where category1.Name == xivFurniture.SecondaryCategory
                        select category1).FirstOrDefault();

                    cat.Categories.Add(new Category{Name = xivFurniture.Name, Item = xivFurniture});
                }
                else
                {
                    var category = new Category { Name = xivFurniture.SecondaryCategory, Categories = new ObservableCollection<Category>(), CategoryList = new List<string>() };
                    category.Categories.Add(new Category { Name = xivFurniture.Name, Item = xivFurniture });
                    category.CategoryList.Add(xivFurniture.Name);
                    Categories[4].Categories.Add(category);
                    Categories[4].CategoryList.Add(xivFurniture.SecondaryCategory);
                }
            }

            progress.Report((100, "Done"));
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
        public ObservableCollection<Category> Categories
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

        /// <summary>
        /// The value for the progressbar
        /// </summary>
        public int ProgressValue
        {
            get => _progressValue;
            set
            {
                _progressValue = value;
                NotifyPropertyChanged(nameof(ProgressValue));
            }
        }

        /// <summary>
        /// The text for the progress label
        /// </summary>
        public string ProgressLabel
        {
            get => _progressLabel;
            set
            {
                _progressLabel = value;
                NotifyPropertyChanged(nameof(ProgressLabel));
            }
        }

        public Visibility ProgressBarVisible
        {
            get => _progressBarVisible;
            set
            {
                _progressBarVisible = value;
                NotifyPropertyChanged(nameof(ProgressBarVisible));
            }
        }

        public Visibility ProgressLabelVisible
        {
            get => _progressLabelVisible;
            set
            {
                _progressLabelVisible = value;
                NotifyPropertyChanged(nameof(ProgressLabelVisible));
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
            if (_index.IsIndexLocked(XivDataFile._0A_Exd))
            {
                FlexibleMessageBox.Show(UIMessages.IndexLockedErrorMessage, UIMessages.IndexLockedErrorTitle, MessageBoxButtons.OK, MessageBoxIcon.Error);

                return;
            }

            _progressController = await _mainWindow.ShowProgressAsync(UIMessages.EnablingModsTitle, UIMessages.PleaseWaitMessage);
            var progressIndicator = new Progress<(int current, int total, string message)>(ReportProgress);

            if (FlexibleMessageBox.Show(
                    UIMessages.EnableAllModsMessage, UIMessages.EnablingModsTitle, MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
            {
                var modding = new Modding(_gameDirectory);
                await modding.ToggleAllMods(true, progressIndicator);

                await _progressController.CloseAsync();

                await _mainWindow.ShowMessageAsync(UIMessages.SuccessTitle, UIMessages.ModsEnabledSuccessMessage);
            }
            else
            {
                await _progressController.CloseAsync();
            }
        }

        /// <summary>
        /// Disables all mods in the mod list
        /// </summary>
        private async void DisableAllMods(object obj)
        {
            if (_index.IsIndexLocked(XivDataFile._0A_Exd))
            {
                FlexibleMessageBox.Show(UIMessages.IndexLockedErrorMessage, UIMessages.IndexLockedErrorTitle, MessageBoxButtons.OK, MessageBoxIcon.Error);

                return;
            }

            _progressController = await _mainWindow.ShowProgressAsync(UIMessages.DisablingModsTitle, UIMessages.PleaseWaitMessage);
            var progressIndicator = new Progress<(int current, int total, string message)>(ReportProgress);

            if (FlexibleMessageBox.Show(
                    UIMessages.DisableAllModsMessage, UIMessages.DisableAllModsTitle, MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
            {
                var modding = new Modding(_gameDirectory);
                await modding.ToggleAllMods(false, progressIndicator);

                await _progressController.CloseAsync();

                await _mainWindow.ShowMessageAsync(UIMessages.SuccessTitle, UIMessages.ModsDisabledSuccessMessage);
            }
            else
            {
                await _progressController.CloseAsync();
            }

        }

        /// <summary>
        /// Updates the progress bar
        /// </summary>
        /// <param name="value">The progress value</param>
        private void ReportProgress((int current, int total, string message) report)
        {
            if (!report.message.Equals(string.Empty))
            {
                _progressController.SetMessage(report.message);
                _progressController.SetIndeterminate();
            }
            else
            {
                _progressController.SetMessage(
                    $"{UIMessages.PleaseStandByMessage} ({report.current} / {report.total})");

                var value = (double)report.current / (double)report.total;
                _progressController.SetProgress(value);
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