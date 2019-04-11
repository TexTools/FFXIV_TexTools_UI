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
using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Windows.Forms;
using System.Windows.Threading;
using xivModdingFramework.General.Enums;
using xivModdingFramework.Helpers;
using xivModdingFramework.Items.Interfaces;
using xivModdingFramework.Mods;
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
                //esrinzou for chinese UI
                if (System.Globalization.CultureInfo.CurrentUICulture.Name == "zh-CN")
                {
                    this.ChinaDiscordButton.Visibility = Visibility.Visible;
                }
                //esrinzou end      
            }
            catch (Exception e)
            {
                System.Windows.MessageBox.Show(
                    "TexTools was unable to locate dependency files.\nPlease make sure you are running TexTools in the folder it came in.\n\nIf you continue to receive this error," +
                    $"\nPlease make sure your Anti-Virus is not blocking TexTools.\n\nError: {e.Message}",
                    "Dependencies Error v" + fileVersion);
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
        }

        private void CheckForUpdates()
        {
            AutoUpdater.Start("https://raw.githubusercontent.com/liinko/FFXIVTexToolsWeb/master/updater.xml");
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
                FlexibleMessageBox.Show("Error Accessing Index File\n\n" +
                                        "Please exit the game before proceeding.\n" +
                                        "-----------------------------------------------------\n\n",
                    "Index Access Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
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
        private void UpdateViews(Category selectedItem)
        {
            if (selectedItem?.Item != null)
            {
                var textureView = TextureTabItem.Content as TextureView;
                var textureViewModel = textureView.DataContext as TextureViewModel;

                textureViewModel.UpdateTexture(selectedItem.Item);

                if (selectedItem.Item.Category.Equals(XivStrings.UI) ||
                    selectedItem.Item.ItemCategory.Equals(XivStrings.Face_Paint) ||
                    selectedItem.Item.ItemCategory.Equals(XivStrings.Equip_Decals))
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

                    modelViewModel.UpdateModel(selectedItem.Item as IItemModel);
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
            var modListView = new ModListView {Owner = this};
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
                    await this.ShowMessageAsync("Error: Mod Pack Creation Failed", "No mods were detected in the options list.");
                }
                else
                {
                    await this.ShowMessageAsync("Mod Pack Creation Complete", $"The ModPack ({wizard.ModPackFileName}.ttmp2) has been successfully Created.");
                }
            }
        }

        /// <summary>
        /// Event handler for the import mod pack menu item clicked
        /// </summary>
        private async void Menu_ImportModpack_Click(object sender, RoutedEventArgs e)
        {
            var gameDirectory = new DirectoryInfo(Settings.Default.FFXIV_Directory);
            var index = new Index(gameDirectory);

            if (index.IsIndexLocked(XivDataFile._0A_Exd))
            {
                FlexibleMessageBox.Show("Error Accessing Index File\n\n" +
                                        "Please exit the game before proceeding.\n" +
                                        "-----------------------------------------------------\n\n",
                    "Index Access Error", MessageBoxButtons.OK, MessageBoxIcon.Error);

                return;
            }

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
                    FlexibleMessageBox.Show($"TTMP file [ {Path.GetFileNameWithoutExtension(fileName)} ] is empty.\n\nImporting Canceled.", $"Import Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    continue;
                }

                modsImported += await ImportModpack(new DirectoryInfo(fileName), modPackDirectory, importMultiple);
            }

            if (modsImported > 0)
            {
                await this.ShowMessageAsync("Import Complete",
                    $"{modsImported} mod(s) successfully imported.");
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
                var ttmpData = ttmp.GetModPackJsonData(path);

                if (ttmpData.ModPackJson.TTMPVersion.Contains("w"))
                {
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
                    FlexibleMessageBox.Show($"There was an error importing the mod pack at {path.FullName}\n\nMessage: {ex.Message}", $"ModPack Import Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
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
                await this.ShowMessageAsync("Mod Pack Creation Complete", $"The ModPack ({simpleCreator.ModPackFileName}.ttmp2) has been successfully Created.");
            }
        }

        /// <summary>
        /// Event handler for the start over menu item clicked
        /// </summary>
        private async void Menu_StartOver_Click(object sender, RoutedEventArgs e)
        {
            var gameDirectory = new DirectoryInfo(Settings.Default.FFXIV_Directory);

            var index = new Index(gameDirectory);

            if (index.IsIndexLocked(XivDataFile._0A_Exd))
            {
                FlexibleMessageBox.Show("Error Accessing Index File\n\n" +
                                        "Please exit the game before proceeding.\n" +
                                        "-----------------------------------------------------\n\n", 
                    "Index Access Error", MessageBoxButtons.OK, MessageBoxIcon.Error);

                return;
            }

            //esrinzou for chinese UI
            /*
            var result = FlexibleMessageBox.Show("Starting over will:\n\n" +
                                                 "Restore index files to their original state.\n" +
                                                 "Delete all mod dat files from game folder.\n" +
                                                 "Delete all mod list file entries.\n\n" +
                                                 "Do you want to start over?", "Start Over", MessageBoxButtons.YesNo, MessageBoxIcon.Information);

             */
            //esrinzou begin
            var result = FlexibleMessageBox.Show(UIStrings.Start_Over_Info, UIStrings.Start_Over, MessageBoxButtons.YesNo, MessageBoxIcon.Information);

            //esrinzou end

            if (result == System.Windows.Forms.DialogResult.Yes)
            {
                var indexBackupsDirectory = new DirectoryInfo(Settings.Default.Backup_Directory);

                if (!Directory.Exists(indexBackupsDirectory.FullName))
                {
                    FlexibleMessageBox.Show("Error Accessing Index Backups Folder\n\n" +
                                            "Please set your Index Backups directory in Options > Customize and try again.\n",
                        "Index Backups Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                var task = Task.Run((() =>
                {
                    var modding = new Modding(gameDirectory);
                    var problemChecker = new ProblemChecker(gameDirectory);
                    var dat = new Dat(gameDirectory);

                    var modListDirectory = new DirectoryInfo(Path.Combine(gameDirectory.Parent.Parent.FullName, XivStrings.ModlistFilePath));

                    var backupFiles = Directory.GetFiles(indexBackupsDirectory.FullName);

                    // Make sure backups exist
                    if (backupFiles.Length == 0)
                    {
                        FlexibleMessageBox.Show($"No backup files found in the following directory:\n{indexBackupsDirectory.FullName}\n\n" +
                                                $"Index entries will be put back to original offsets instead.\n" +
                                                "-----------------------------------------------------\n\n",
                            "Backup Files Missing", MessageBoxButtons.OK, MessageBoxIcon.Warning);

                        // Toggle off all mods
                        modding.ToggleAllMods(false);
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
                        var datFiles = dat.GetModdedDatList(xivDataFile);

                        foreach (var datFile in datFiles)
                        {
                            File.Delete(datFile);
                        }

                        if (datFiles.Count > 0)
                        {
                            problemChecker.RepairIndexDatCounts(xivDataFile);
                        }
                    }

                    // Delete mod list
                    File.Delete(modListDirectory.FullName);

                    modding.CreateModlist();

                }));

                task.Wait();

                UpdateViews(ItemTreeView.SelectedItem as Category);

                await this.ShowMessageAsync("Start Over Complete", "The start over process has been completed.");
            }
        }

        private void Menu_Donate_Click(object sender, RoutedEventArgs e)
        {
            System.Diagnostics.Process.Start(WebUrl.FFXIV_Donate);
        }

        private async void Menu_Backup_Click(object sender, RoutedEventArgs e)
        {
            var result = FlexibleMessageBox.Show("This will create a backup of the index files TexTools can modify (01, 04, 06)\n\n" +
                                                 "Do you want to Backup Now?" +
                                                 "\n\nWarning:\nIn order to create a clean backup, all active modifications will be set to disabled, they will have to be re-enabled manually.",
                "Backup Index Files", MessageBoxButtons.YesNo, MessageBoxIcon.Information);

            if (result == System.Windows.Forms.DialogResult.Yes)
            {
                var gameDirectory = new DirectoryInfo(Settings.Default.FFXIV_Directory);
                var backupDirectory = new DirectoryInfo(Properties.Settings.Default.Backup_Directory);
                var indexFiles = new XivDataFile[] { XivDataFile._04_Chara, XivDataFile._06_Ui, XivDataFile._01_Bgcommon };
                var index = new Index(gameDirectory);
                var modding = new Modding(gameDirectory);

                if (index.IsIndexLocked(XivDataFile._0A_Exd))
                {
                    FlexibleMessageBox.Show("Unable to create backup while game is running.", $"Backup Creation Failed", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                try
                {
                    // Toggle off all mods
                    modding.ToggleAllMods(false);
                }
                catch (Exception ex)
                {
                    FlexibleMessageBox.Show($"Unable to create backup files.\n\nError Message:\n{ex.Message}", $"Backup Creation Failed", MessageBoxButtons.OK, MessageBoxIcon.Warning);
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
                        FlexibleMessageBox.Show($"Unable to create backups.\n\nError: {ex.Message}", $"Backup Creation Failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }

                await this.ShowMessageAsync("Backup Complete", "The index files have been successfully backed up");
            }
        }
        private void ItemTreeView_Loaded(object sender, RoutedEventArgs e)
        {
            //esrinzou for quick start
            //var view = (CollectionView)CollectionViewSource.GetDefaultView(ItemTreeView.ItemsSource);
            //view.Filter = SearchFilter;
            //esrinzou begin
            void DoEvent()
            {
                DispatcherOperationCallback exitFrameCallback = new DispatcherOperationCallback(ExitFrame);
                DispatcherFrame nestedFrame = new DispatcherFrame();
                DispatcherOperation exitOperation = Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Background, exitFrameCallback, nestedFrame);
                Dispatcher.PushFrame(nestedFrame);
                if (exitOperation.Status !=
                DispatcherOperationStatus.Completed)
                {
                    exitOperation.Abort();
                }
            }
            object ExitFrame(object state)
            {
                DispatcherFrame frame = state as DispatcherFrame;
                frame.Continue = false;
                return null;
            }
            Dispatcher.CurrentDispatcher.BeginInvoke(new Action(() =>
            {
                var vm = this.DataContext as MainViewModel;
                try
                {
                    vm.FillTree(() =>
                    {
                        DoEvent();
                    });
                }
                catch(Exception ex)
                {
                    FlexibleMessageBox.Show($"There was an error getting the Items List\n\n{ex.Message}", $"Items List Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                var view = (CollectionView)CollectionViewSource.GetDefaultView(ItemTreeView.ItemsSource);
                view.Filter = SearchFilter;
            }),DispatcherPriority.Background);
            //esrinzou end
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

            Title += $" {fileVersion.Substring(0, fileVersion.LastIndexOf("."))}";
        }

        private void GithubButton_Click(object sender, RoutedEventArgs e)
        {
            Process.Start(WebUrl.Github_Website);
        }

        private void ChinaDiscordButton_Click(object sender, RoutedEventArgs e)
        {
            Process.Start("http://149.129.96.215:8989");
        }
    }
}
