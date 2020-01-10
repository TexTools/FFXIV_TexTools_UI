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

using AutoUpdaterDotNET;
using FFXIV_TexTools.Helpers;
using FFXIV_TexTools.Models;
using FFXIV_TexTools.Properties;
using FFXIV_TexTools.Resources;
using FFXIV_TexTools.ViewModels;
using FFXIV_TexTools.Views;
using MahApps.Metro;
using MahApps.Metro.Controls.Dialogs;
using SixLabors.ImageSharp;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Windows.Forms;
using System.Windows.Threading;
using xivModdingFramework.General.Enums;
using xivModdingFramework.Helpers;
using xivModdingFramework.Items.Interfaces;
using xivModdingFramework.Mods;
using xivModdingFramework.Mods.DataContainers;
using xivModdingFramework.Mods.FileTypes;
using xivModdingFramework.SqPack.FileTypes;
using Application = System.Windows.Application;
using SysTimer = System.Timers;

namespace FFXIV_TexTools
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow
    {
        private SysTimer.Timer searchTimer = new SysTimer.Timer(300);
        private string _startupArgs;

        public MainWindow(string[] args)
        {
            LanguageSelection();

            var ci = new CultureInfo(Properties.Settings.Default.Application_Language)
            {
                NumberFormat = { NumberDecimalSeparator = "." }
            };

            CultureInfo.DefaultThreadCurrentCulture = ci;
            CultureInfo.DefaultThreadCurrentUICulture = ci;
            CultureInfo.CurrentCulture = ci;
            CultureInfo.CurrentUICulture = ci;

            CheckForUpdates();
            CheckForSettingsUpdate();

            var fileVersion = FileVersionInfo.GetVersionInfo(System.Reflection.Assembly.GetExecutingAssembly().Location).FileVersion;

            if (args != null && args.Length > 0)
            {
                _startupArgs = args[0];
                OnlyImport();
            }

            try
            {
                InitializeComponent();

                if (System.Globalization.CultureInfo.CurrentUICulture.Name == "zh")
                {
                    this.ChinaDiscordButton.Visibility = Visibility.Visible;
                }
            }
            catch (Exception e)
            {
                System.Windows.MessageBox.Show(string.Format(UIMessages.DependencyErrorMessage, e.Message, e.InnerException),
                    string.Format(UIMessages.DependencyErrorTitle, fileVersion));
                Environment.Exit(-1);
                return;
            }

            ItemSearchTextBox.Focus();
            var mainViewModel = new MainViewModel(this);
            this.DataContext = mainViewModel;

            if (searchTimer == null)
            {
                searchTimer = new SysTimer.Timer(300);
            }

            searchTimer.Enabled = true;
            searchTimer.AutoReset = false;
            searchTimer.Elapsed += SearchTimerOnElapsed;

            var textureView = TextureTabItem.Content as TextureView;
            var textureViewModel = textureView.DataContext as TextureViewModel;

            textureViewModel.LoadingComplete += TextureViewModelOnLoadingComplete;

            var modelView = ModelTabItem.Content as ModelView;
            var modelViewModel = modelView.DataContext as ModelViewModel;

            modelViewModel.LoadingComplete += ModelViewModelOnLoadingComplete;
        }

        private void LanguageSelection()
        {
            var lang = Properties.Settings.Default.Application_Language;

            if (lang.Equals(string.Empty))
            {
                var langSelectView = new LanguageSelectView();
                langSelectView.ShowDialog();

                var langCode = langSelectView.LanguageCode;

                Properties.Settings.Default.Application_Language = langCode;
                Properties.Settings.Default.Save();
            }
        }

        private void ModelViewModelOnLoadingComplete(object sender, EventArgs e)
        {
            ItemTreeView.IsEnabled = true;
        }

        private void TextureViewModelOnLoadingComplete(object sender, EventArgs e)
        {
            var selectedItem = ItemTreeView.SelectedItem as Category;

            if(selectedItem?.Item == null) return;

            if (selectedItem.Item.Category.Equals(XivStrings.UI) ||
                selectedItem.Item.ItemCategory.Equals(XivStrings.Face_Paint) ||
                selectedItem.Item.ItemCategory.Equals(XivStrings.Equipment_Decals) ||
                selectedItem.Item.ItemCategory.Equals(XivStrings.Paintings))
            {
                ItemTreeView.IsEnabled = true;
            }
        }

        private void CheckForUpdates()
        {
            AutoUpdater.CheckForUpdateEvent += AutoUpdater_CheckForUpdateEvent;
            AutoUpdater.Start(WebUrl.TexTools_Update_Url);
        }

        private void AutoUpdater_CheckForUpdateEvent(UpdateInfoEventArgs args)
        {
            AutoUpdater.CheckForUpdateEvent -= AutoUpdater_CheckForUpdateEvent; 
            if (args==null||!args.IsUpdateAvailable)
            {            
                Dispatcher.InvokeAsync(() => {
                    AutoUpdater.Start(WebUrl.TexToolsPre_Update_Url);
                });
            }
            else {
                AutoUpdater.ShowUpdateForm();
            }
        }

        private void CheckForSettingsUpdate()
        {
            if (Settings.Default.UpgradeRequired)
            {
                Settings.Default.Upgrade();
                Settings.Default.UpgradeRequired = false;
                Settings.Default.Save();
            }
        }

        private async void OnlyImport()
        {
            var gameDirectory = new DirectoryInfo(Settings.Default.FFXIV_Directory);
            var index = new Index(gameDirectory);

            if (index.IsIndexLocked(XivDataFile._0A_Exd))
            {
                FlexibleMessageBox.Show(UIMessages.IndexLockedErrorMessage, UIMessages.IndexLockedErrorTitle, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            else
            {
                var modPackDirectory = new DirectoryInfo(Settings.Default.ModPack_Directory);

                await ImportModpack(new DirectoryInfo(_startupArgs), modPackDirectory, false, true);
            }

            Application.Current.Shutdown();
        }

        /// <summary>
        /// Event handler for the language button clicked
        /// </summary>
        private void LanguageButton_Click(object sender, RoutedEventArgs e)
        {
            RightFlyout.Content = new LanguageOptionsView();
            RightFlyout.IsOpen = true;
        }

        /// <summary>
        /// Event handler for the light theme clicked
        /// </summary>
        private void MenuLightTheme_Click(object sender, RoutedEventArgs e)
        {
            var appStyle = ThemeManager.DetectAppStyle(Application.Current);

            ThemeManager.ChangeAppStyle(Application.Current, ThemeManager.GetAccent(appStyle.Item2.Name), ThemeManager.GetAppTheme("BaseLight"));

            Settings.Default.Application_Theme = "BaseLight";
            Settings.Default.Save();
        }

        /// <summary>
        /// Event handler for the dark theme clicked
        /// </summary>
        private void MenuDarkTheme_Click(object sender, RoutedEventArgs e)
        {
            var appStyle = ThemeManager.DetectAppStyle(Application.Current);

            ThemeManager.ChangeAppStyle(Application.Current, ThemeManager.GetAccent(appStyle.Item2.Name), ThemeManager.GetAppTheme("BaseDark"));

            Settings.Default.Application_Theme = "BaseDark";
            Settings.Default.Save();
        }

        /// <summary>
        /// Event handler for the treeview selected item changed
        /// </summary>
        private void TreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            UpdateViews(e.NewValue as Category);
        }

        /// <summary>
        /// Updates the texture and model views with the selected item
        /// </summary>
        /// <param name="selectedItem">The selected item</param>
        private async void UpdateViews(Category selectedItem)
        {
            if (selectedItem?.Item != null)
            {
                ItemTreeView.IsEnabled = false;
                var textureView = TextureTabItem.Content as TextureView;
                var textureViewModel = textureView.DataContext as TextureViewModel;

                await textureViewModel.UpdateTexture(selectedItem.Item);

                if (selectedItem.Item.Category.Equals(XivStrings.UI) ||
                    selectedItem.Item.ItemCategory.Equals(XivStrings.Face_Paint) ||
                    selectedItem.Item.ItemCategory.Equals(XivStrings.Equipment_Decals) ||
                    selectedItem.Item.ItemCategory.Equals(XivStrings.Paintings))
                {
                    if (TabsControl.SelectedIndex == 1)
                    {
                        TabsControl.SelectedIndex = 0;
                    }

                    ModelTabItem.IsEnabled = false;
                }
                else
                {
                    ModelTabItem.IsEnabled = true;

                    var modelView = ModelTabItem.Content as ModelView;
                    var modelViewModel = modelView.DataContext as ModelViewModel;

                    await modelViewModel.UpdateModel(selectedItem.Item as IItemModel);
                }
            }
        }

        /// <summary>
        /// Event handler for the model id search clicked
        /// </summary>
        private void Menu_ModelIDSearch_Click(object sender, RoutedEventArgs e)
        {
            var modelSearchView = new ModelSearchView(this) {Owner = this};
            modelSearchView.Show();
        }

        /// <summary>
        /// Event handler for the icon id search clicked
        /// </summary>
        private void Menu_IconIDSearch_Click(object sender, RoutedEventArgs e)
        {
            var iconSearchView = new IconSearchView(this) {Owner = this};
            iconSearchView.Show();
        }

        /// <summary>
        /// Event handler for the about menu item clicked
        /// </summary>
        private void Menu_About_Click(object sender, RoutedEventArgs e)
        {
            var about = new AboutView {Owner = this};
            about.Show();
        }

        /// <summary>
        /// Event handler for the PK Emporium site
        /// </summary>
        private void PKEmporium_Click(object sender, RoutedEventArgs e)
        {
            Process.Start(WebUrl.PKEmporium_Website);
        }

        /// <summary>
        /// Event handler for the Xiv Mod Archive site
        /// </summary>
        private void XivModArchive_Click(object sender, RoutedEventArgs e)
        {
            Process.Start(WebUrl.XivModArchive_Website);
        }

        /// <summary>
        /// Event handler for the Nexus Mods site
        /// </summary>
        private void NexusMods_Click(object sender, RoutedEventArgs e)
        {
            Process.Start(WebUrl.NexusMods_Website);
        }

        /// <summary>
        /// Event handler for the Discord Invite
        /// </summary>
        private void DiscordButton_Click(object sender, RoutedEventArgs e)
        {
            Process.Start(WebUrl.Discord_Invite);
        }

        /// <summary>
        /// Event handler for the BugReport site
        /// </summary>
        private void Menu_BugReport_Click(object sender, RoutedEventArgs e)
        {
            Process.Start(WebUrl.BugReport_Website);
        }

        /// <summary>
        /// Event handler for the Tutorials site
        /// </summary>
        private void Menu_Tutorials_Click(object sender, RoutedEventArgs e)
        {
            Process.Start(WebUrl.Tutorials_Website);
        }

        /// <summary>
        /// Event handler for the mod list menu item clicked
        /// </summary>
        private void Menu_ModList_Click(object sender, RoutedEventArgs e)
        {
            var textureView = TextureTabItem.Content as TextureView;
            var textureViewModel = textureView.DataContext as TextureViewModel;
            var modelView = ModelTabItem.Content as ModelView;
            var modelViewModel = modelView.DataContext as ModelViewModel;

            var modListView = new ModListView(textureViewModel, modelViewModel) {Owner = this};
            modListView.Show();
        }

        /// <summary>
        /// Event handler for the problem check menu item clicked
        /// </summary>
        private void Menu_ProblemCheck_Click(object sender, RoutedEventArgs e)
        {
            var problemCheckView = new ProblemCheckView {Owner = this};
            problemCheckView.Show();
        }

        /// <summary>
        /// Event handler for the customize menu item clicked
        /// </summary>
        private void Customize_Click(object sender, RoutedEventArgs e)
        {
            var customize = new CustomizeSettingsView {Owner = this};
            customize.Show();
        }

        /// <summary>
        /// Event handler for the mod pack wizard menu item clicked
        /// </summary>
        private async void Menu_MakeModpackWizard_Click(object sender, RoutedEventArgs e)
        {
            var wizard = new ModPackWizard {Owner = this};
            var result = wizard.ShowDialog();

            if (result == true)
            {
                if (wizard.ModPackFileName.Equals("NoData"))
                {
                    await this.ShowMessageAsync(UIMessages.ModPackCreationFailedErrorTitle, UIMessages.NoModsDetectedErrorMessage);
                }
                else
                {
                    await this.ShowMessageAsync(UIMessages.ModPackCreationCompleteTitle, string.Format(UIMessages.ModPackCreationCompleteMessage, wizard.ModPackFileName));
                }
            }
        }

        /// <summary>
        /// Event handler for the import mod pack menu item clicked
        /// </summary>
        private async void Menu_ImportModpack_Click(object sender, RoutedEventArgs e)
        {
            var modPackDirectory = new DirectoryInfo(Settings.Default.ModPack_Directory);

            var openFileDialog = new OpenFileDialog {InitialDirectory = modPackDirectory.FullName, Filter = "TexToolsModPack TTMP (*.ttmp;*.ttmp2)|*.ttmp;*.ttmp2", Multiselect = true};

            if (openFileDialog.ShowDialog() != System.Windows.Forms.DialogResult.OK) 
                return;
            
            var importMultiple = openFileDialog.FileNames.Length > 1;

            var modsImported = 0;

            foreach (var fileName in openFileDialog.FileNames)
            {
                var fileInfo = new FileInfo(fileName);

                if (fileInfo.Length == 0)
                {
                    FlexibleMessageBox.Show(string.Format(UIMessages.EmptyTTMPFileErrorMessage, Path.GetFileNameWithoutExtension(fileName)), UIMessages.ImportErrorTitle, MessageBoxButtons.OK, MessageBoxIcon.Error);
                    continue;
                }

                modsImported += await ImportModpack(new DirectoryInfo(fileName), modPackDirectory, importMultiple);
            }

            if (modsImported > 0)
            {
                await this.ShowMessageAsync(UIMessages.ImportCompleteTitle, string.Format(UIMessages.SuccessfulImportCountMessage, modsImported));
            }
        }

        /// <summary>
        /// This method opens the modpack import wizard or imports a modpack silently
        /// </summary>
        /// <param name="path">The path to the modpack</param>
        /// <param name="silent">If the modpack wizard should be shown or the modpack should just be imported without any user interaction</param>
        /// <returns></returns>
        private async Task<int> ImportModpack(DirectoryInfo path, DirectoryInfo modPackDirectory, bool silent = false, bool messageInImport = false)
        {
            var importError = false;
            
            try
            {
                var ttmp = new TTMP(modPackDirectory, XivStrings.TexTools);
                var ttmpData = await ttmp.GetModPackJsonData(path);

                if (ttmpData.ModPackJson.TTMPVersion.Contains("w"))
                {

                    var gameDirectory = new DirectoryInfo(Settings.Default.FFXIV_Directory);
                    var index = new Index(gameDirectory);

                    if (index.IsIndexLocked(XivDataFile._0A_Exd))
                    {
                        FlexibleMessageBox.Show(UIMessages.IndexLockedErrorMessage, UIMessages.IndexLockedErrorTitle, MessageBoxButtons.OK, MessageBoxIcon.Error);

                        return 0;
                    }

                    try
                    {
                        var importWizard = new ImportModPackWizard(ttmpData.ModPackJson, ttmpData.ImageDictionary,
                            path, messageInImport);

                        if (messageInImport)
                        {
                            importWizard.WindowStartupLocation = WindowStartupLocation.CenterScreen;
                        }
                        else
                        {
                            importWizard.Owner = this;
                        }

                        var result = importWizard.ShowDialog();

                        if (result == true)
                        {
                            return importWizard.TotalModsImported;
                        }
                    }
                    catch
                    {
                        importError = true;
                    }
                }
                else if(ttmpData.ModPackJson.TTMPVersion.Contains("s"))
                {
                    try
                    {
                        var simpleImport = new SimpleModPackImporter(path,
                            ttmpData.ModPackJson, silent, messageInImport);

                        if (messageInImport)
                        {
                            simpleImport.WindowStartupLocation = WindowStartupLocation.CenterScreen;
                        }
                        else
                        {
                            simpleImport.Owner = this;
                        }

                        var result = simpleImport.ShowDialog();

                        if (result == true)
                        {
                            return simpleImport.TotalModsImported;
                        }
                    }
                    catch
                    {
                        importError = true;
                    }
                }
            }
            catch (Exception ex)
            {
                if (!importError)
                {
                    var simpleImport = new SimpleModPackImporter(path, null, silent, messageInImport);

                    if (messageInImport)
                    {
                        simpleImport.WindowStartupLocation = WindowStartupLocation.CenterScreen;
                    }
                    else
                    {
                        simpleImport.Owner = this;
                    }

                    var result = simpleImport.ShowDialog();

                    if (result == true)
                    {
                        return simpleImport.TotalModsImported;
                    }
                }
                else
                {
                    FlexibleMessageBox.Show(string.Format(UIMessages.ModPackImportErrorMessage, path.FullName, ex.Message), UIMessages.ModPackImportErrorTitle, MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return 0;
                }
            }

            return 0;
        }

        /// <summary>
        /// Event handler for the simple mod pack menu item clicked
        /// </summary>
        private async void Menu_MakeSimpleModpack_Click(object sender, RoutedEventArgs e)
        {
            var simpleCreator = new SimpleModPackCreator{Owner = this};
            var result = simpleCreator.ShowDialog();

            if (result == true)
            {
                await this.ShowMessageAsync(UIMessages.ModPackCreationCompleteTitle, string.Format(UIMessages.ModPackCreationCompleteMessage, simpleCreator.ModPackFileName));
            }
        }

        /// <summary>
        /// Event handler for the start over menu item clicked
        /// </summary>
        private async void Menu_StartOver_Click(object sender, RoutedEventArgs e)
        {
            var gameDirectory = new DirectoryInfo(Settings.Default.FFXIV_Directory);

            var index = new Index(gameDirectory);
            var outdated = false;

            if (index.IsIndexLocked(XivDataFile._0A_Exd))
            {
                FlexibleMessageBox.Show(UIMessages.IndexLockedErrorMessage, UIMessages.IndexLockedErrorTitle, MessageBoxButtons.OK, MessageBoxIcon.Error);

                return;
            }

            var result = FlexibleMessageBox.Show(UIMessages.StartOverMessage, UIMessages.StartOverTitle, MessageBoxButtons.YesNo, MessageBoxIcon.Information);

            if (result == System.Windows.Forms.DialogResult.Yes)
            {
                var indexBackupsDirectory = new DirectoryInfo(Settings.Default.Backup_Directory);

                if (!Directory.Exists(indexBackupsDirectory.FullName))
                {
                    FlexibleMessageBox.Show(UIMessages.BackupFolderAccessErrorMessage,
                        UIMessages.IndexBackupsErrorTitle, MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                var filesToCheck = new XivDataFile[] { XivDataFile._01_Bgcommon, XivDataFile._04_Chara, XivDataFile._06_Ui };

                var problemChecker = new ProblemChecker(gameDirectory);

                foreach (var xivDataFile in filesToCheck)
                {
                    var backupFile = new DirectoryInfo($"{indexBackupsDirectory.FullName}\\{xivDataFile.GetDataFileName()}.win32.index");

                    if(!File.Exists(backupFile.FullName)) continue;

                    var outdatedCheck = await problemChecker.CheckForOutdatedBackups(xivDataFile, indexBackupsDirectory);

                    if (!outdatedCheck)
                    {
                        FlexibleMessageBox.Show(UIMessages.OutdatedBackupsErrorMessage, UIMessages.IndexBackupsErrorTitle, MessageBoxButtons.OK, MessageBoxIcon.Error);

                        outdated = true;
                    }
                }

                await Task.Run(async () =>
                {
                    var modding = new Modding(gameDirectory);
                    await modding.DeleteAllFilesAddedByTexTools();

                    var dat = new Dat(gameDirectory);

                    var modListDirectory = new DirectoryInfo(Path.Combine(gameDirectory.Parent.Parent.FullName, XivStrings.ModlistFilePath));

                    var backupFiles = Directory.GetFiles(indexBackupsDirectory.FullName);

                    // Make sure backups exist
                    if (backupFiles.Length == 0)
                    {
                        FlexibleMessageBox.Show(string.Format(UIMessages.NoBackupsFoundErrorMessage, indexBackupsDirectory.FullName),
                            UIMessages.BackupFilesMissingTitle, MessageBoxButtons.OK, MessageBoxIcon.Warning);

                        // Toggle off all mods
                        await modding.ToggleAllMods(false);
                    }
                    else if (outdated)
                    {
                        // Toggle off all mods
                        await modding.ToggleAllMods(false);
                    }
                    else
                    {
                        // Copy backups to ffxiv folder
                        foreach (var backupFile in backupFiles)
                        {
                            if (backupFile.Contains(".win32.index"))
                            {
                                File.Copy(backupFile, $"{gameDirectory}/{Path.GetFileName(backupFile)}", true);
                            }
                        }
                    }

                    // Delete modded dat files
                    foreach (var xivDataFile in (XivDataFile[])Enum.GetValues(typeof(XivDataFile)))
                    {
                        var datFiles = await dat.GetModdedDatList(xivDataFile);

                        foreach (var datFile in datFiles)
                        {
                            File.Delete(datFile);
                        }

                        if (datFiles.Count > 0)
                        {
                            await problemChecker.RepairIndexDatCounts(xivDataFile);
                        }
                    }

                    // Delete mod list
                    File.Delete(modListDirectory.FullName);

                    modding.CreateModlist();

                });

                UpdateViews(ItemTreeView.SelectedItem as Category);

                await this.ShowMessageAsync(UIMessages.StartOverCompleteTitle, UIMessages.StartOverCompleteMessage);
            }
        }

        private void Menu_Donate_Click(object sender, RoutedEventArgs e)
        {
            System.Diagnostics.Process.Start(WebUrl.FFXIV_Donate);
        }

        private async void Menu_Backup_Click(object sender, RoutedEventArgs e)
        {
            var result = FlexibleMessageBox.Show(UIMessages.CreateBackupsMessage, UIMessages.CreateBackupsTitle, MessageBoxButtons.YesNo, MessageBoxIcon.Information);

            if (result == System.Windows.Forms.DialogResult.Yes)
            {
                var gameDirectory = new DirectoryInfo(Settings.Default.FFXIV_Directory);
                var backupDirectory = new DirectoryInfo(Properties.Settings.Default.Backup_Directory);
                var indexFiles = new XivDataFile[] { XivDataFile._04_Chara, XivDataFile._06_Ui, XivDataFile._01_Bgcommon };
                var index = new Index(gameDirectory);
                var modding = new Modding(gameDirectory);

                if (index.IsIndexLocked(XivDataFile._0A_Exd))
                {
                    FlexibleMessageBox.Show(UIMessages.IndexLockedBackupFailedMessage, UIMessages.BackupFailedTitle, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                try
                {
                    // Toggle off all mods
                    await modding.ToggleAllMods(false);
                }
                catch (Exception ex)
                {
                    FlexibleMessageBox.Show(string.Format(UIMessages.BackupFailedErrorMessage, ex.Message), UIMessages.BackupFailedTitle, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                foreach (var xivDataFile in indexFiles)
                {
                    try
                    {
                        File.Copy($"{gameDirectory.FullName}\\{xivDataFile.GetDataFileName()}.win32.index",
                            $"{backupDirectory}\\{xivDataFile.GetDataFileName()}.win32.index", true);
                        File.Copy($"{gameDirectory.FullName}\\{xivDataFile.GetDataFileName()}.win32.index2",
                            $"{backupDirectory}\\{xivDataFile.GetDataFileName()}.win32.index2", true);
                    }
                    catch (Exception ex)
                    {
                        FlexibleMessageBox.Show(string.Format(UIMessages.BackupFailedErrorMessage, ex.Message), UIMessages.BackupFailedTitle, MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }

                await this.ShowMessageAsync(UIMessages.BackupCompleteTitle, UIMessages.BackupCompleteMessage);
            }
        }

        public void SetFilter()
        {
            var view = (CollectionView)CollectionViewSource.GetDefaultView(ItemTreeView.ItemsSource);
            view.Filter = SearchFilter;
        }

        private bool SearchFilter(object item)
        {
            if (((Category)item).Categories != null)
            {
                var subItems = (CollectionView)CollectionViewSource.GetDefaultView(((Category)item).Categories);

                subItems.Filter = SearchFilter;

                ((Category)item).IsExpanded = !string.IsNullOrEmpty(ItemSearchTextBox.Text);

                return !subItems.IsEmpty;
            }

            return ((Category)item).Name.ToLower().Contains(ItemSearchTextBox.Text.ToLower());
        }

        private void ItemSearchTextBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            searchTimer.Stop();
            searchTimer.Start();
        }

        private void SearchTimerOnElapsed(object sender, SysTimer.ElapsedEventArgs e)
        {
            Dispatcher.Invoke(UpdateFilter);
        }

        private void UpdateFilter()
        {
            CollectionViewSource.GetDefaultView(ItemTreeView.ItemsSource).Refresh();
        }

        private void MetroWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (searchTimer != null)
            {
                searchTimer.Elapsed -= SearchTimerOnElapsed;
                searchTimer.Dispose();
            }
        }

        private void MetroWindow_Loaded(object sender, RoutedEventArgs e)
        {
            var fileVersion = FileVersionInfo.GetVersionInfo(System.Reflection.Assembly.GetExecutingAssembly().Location).FileVersion;
            var tmps = fileVersion.Split('.');
            var pre = tmps[tmps.Length - 1] == "0" ? "" : $".{tmps[tmps.Length - 1]}";
            Title += $" {fileVersion.Substring(0, fileVersion.LastIndexOf("."))}{pre}";
        }

        private void GithubButton_Click(object sender, RoutedEventArgs e)
        {
            Process.Start(WebUrl.Github_Website);
        }

        private void ChinaDiscordButton_Click(object sender, RoutedEventArgs e)
        {
            Process.Start(WebUrl.Discord_China);
        }

        private async void Menu_ModConverter_Click(object sender, RoutedEventArgs e)
        {
            var modPackDirectory = new DirectoryInfo(Settings.Default.ModPack_Directory);
            var openFileDialog = new OpenFileDialog { InitialDirectory = modPackDirectory.FullName, Filter = "TexToolsModPack TTMP (*.ttmp;*.ttmp2)|*.ttmp;*.ttmp2" };
            if (openFileDialog.ShowDialog() != System.Windows.Forms.DialogResult.OK)
                return;
            var ttmpFileName = openFileDialog.FileName;
            var ttmp = new TTMP(modPackDirectory, XivStrings.TexTools);
            (ModPackJson ModPackJson, Dictionary<string, Image> ImageDictionary) ttmpData;
            var progressController = await this.ShowProgressAsync(UIStrings.Mod_Converter, UIMessages.PleaseStandByMessage);
            var modsJsonList = await ttmp.GetOriginalModPackJsonData(new DirectoryInfo(ttmpFileName));
            if (modsJsonList == null)
            {
                ttmpData = await ttmp.GetModPackJsonData(new DirectoryInfo(ttmpFileName));
            }
            else
            {
                ttmpData = (ModPackJson: new ModPackJson(), ImageDictionary: new Dictionary<string, Image>());
                ttmpData.ModPackJson.Author = "Mod Converter";
                ttmpData.ModPackJson.Version = "1.0.0";
                ttmpData.ModPackJson.Name = Path.GetFileNameWithoutExtension(ttmpFileName);
                ttmpData.ModPackJson.TTMPVersion = "s";
                ttmpData.ModPackJson.SimpleModsList = new List<ModsJson>();
                foreach (var mod in modsJsonList)
                {
                    var modsJson = new ModsJson();
                    modsJson.Category = mod.Category;
                    modsJson.DatFile = mod.DatFile;
                    modsJson.FullPath = mod.FullPath;
                    modsJson.ModOffset = mod.ModOffset;
                    modsJson.ModPackEntry = null;
                    modsJson.ModSize = mod.ModSize;
                    modsJson.Name = mod.Name;
                    ttmpData.ModPackJson.SimpleModsList.Add(modsJson);
                }

            }
            var categorys = ItemTreeView.ItemsSource as ObservableCollection<Category>;
            var list= new List<xivModdingFramework.Items.Interfaces.IItem>();
            var ctgs1 = categorys[0];
            foreach (var ctgs2 in ctgs1.Categories)
            {
                if (ctgs2.Item != null)
                {
                    list.Add(ctgs2.Item);
                    continue;
                }
                foreach (var ctgs3 in ctgs2.Categories)
                {
                    if (ctgs3.Item != null)
                        list.Add(ctgs3.Item);
                }
            }
            var modConverterView = new ModConverterView(list,ttmpFileName, ttmpData) { Owner = this,WindowStartupLocation=WindowStartupLocation.CenterOwner };
            await progressController.CloseAsync();
            modConverterView.ShowDialog();
        }
    }
}
