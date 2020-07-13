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
using System.Linq;
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
using System.Threading;

namespace FFXIV_TexTools
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow
    {
        private string _startupArgs;
        private Category _selectedCategory;
        private static MainWindow _mainWindow;
        public System.Timers.Timer SearchTimer = new System.Timers.Timer(300);


        /// <summary>
        /// Static accessor, since we should only ever have one instance of this class anyways.
        /// </summary>
        /// <returns></returns>
        public static MainWindow GetMainWindow()
        {
            return _mainWindow;
        }

        public event EventHandler TreeRefreshRequested;

        public MainWindow(string[] args)
        {

            _mainWindow = this;
            CheckForSettingsUpdate();
            LanguageSelection();

            // Data Context needs to be set before we call Initialize Component to ensure
            // that the bindings get connected immediately, and not after the constructor.
            // It's kind of janky though because the constructor of MainViewModel fires
            // an asynchronous non-waited call to check the Indexes/etc., which ideally
            // we would really *want* to be waited, but blocking there will cause the whole
            // application to lock because of inter-dependencies.
            var mainViewModel = new MainViewModel(this);
            this.DataContext = mainViewModel;

            InitializeComponent();

            var ci = new CultureInfo(Properties.Settings.Default.Application_Language)
            {
                NumberFormat = { NumberDecimalSeparator = "." }
            };

            CultureInfo.DefaultThreadCurrentCulture = ci;
            CultureInfo.DefaultThreadCurrentUICulture = ci;
            CultureInfo.CurrentCulture = ci;
            CultureInfo.CurrentUICulture = ci;

            CheckForUpdates();

            var fileVersion = FileVersionInfo.GetVersionInfo(System.Reflection.Assembly.GetExecutingAssembly().Location).FileVersion;

            try
            {
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

            if (args != null && args.Length > 0)
            {
                _startupArgs = args[0];
                OnlyImport();
            }
            else
            {
                this.Show();

                ItemSearchTextBox.Focus();

                if (SearchTimer == null)
                {
                    SearchTimer = new System.Timers.Timer(300);
                }
                SearchTimer.Elapsed += SearchTimerOnElapsed;

                SearchTimer.Enabled = false;
                SearchTimer.AutoReset = false;


                var textureView = TextureTabItem.Content as TextureView;
                var textureViewModel = textureView.DataContext as TextureViewModel;

                textureViewModel.LoadingComplete += TextureViewModelOnLoadingComplete;

                var modelView = ModelTabItem.Content as ModelView;
                var modelViewModel = modelView.DataContext as ModelViewModel;

                modelViewModel.LoadingComplete += ModelViewModelOnLoadingComplete;

                // This can be safely called now.
                RefreshTree();
            }
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

            if (selectedItem.Item.PrimaryCategory.Equals(XivStrings.UI) ||
                selectedItem.Item.SecondaryCategory.Equals(XivStrings.Face_Paint) ||
                selectedItem.Item.SecondaryCategory.Equals(XivStrings.Equipment_Decals) ||
                selectedItem.Item.SecondaryCategory.Equals(XivStrings.Paintings))
            {
                ItemTreeView.IsEnabled = true;
            }
        }

        /// <summary>
        /// Triggers the MainWindow's thread to refresh the tree view.
        /// </summary>
        /// <param name="requestor"></param>
        public void RefreshTree(object requestor = null)
        {
            TreeRefreshRequested.Invoke(requestor, null);
        }

        private void CheckForUpdates()
        {
            AutoUpdater.Start(WebUrl.TexTools_Update_Url);
        }

        private void CheckForSettingsUpdate()
        {
            if (Settings.Default.UpgradeRequired)
            {
                Settings.Default.Upgrade();
                Settings.Default.UpgradeRequired = false;
                Settings.Default.Save();
                
                // Set theme according to settings now that the settings have been upgraded to the new version
                var appStyle = ThemeManager.DetectAppStyle(Application.Current);
                ThemeManager.ChangeAppStyle(Application.Current, ThemeManager.GetAccent(appStyle.Item2.Name), ThemeManager.GetAppTheme(Settings.Default.Application_Theme));
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
            var c = (e.NewValue as Category);
            UpdateViews(c);
        }

        /// <summary>
        /// Select an item in the tree view (and switch to that item)
        /// </summary>
        /// <param name="item"></param>
        public void SelectItem(IItem item, bool selectInTree = true)
        {
            Category c = null;
            if (selectInTree)
            {
                c = FindInTree(item);
                if (c != null)
                {

                    var p = c.ParentCategory;
                    var cats = new List<Category>();
                    while (p != null)
                    {
                        cats.Add(p);
                        p = p.ParentCategory;
                    }
                    cats.Reverse();

                    // Expand from top down.
                    foreach (var cat in cats)
                    {
                        cat.IsExpanded = true;
                    }
                    c.IsSelected = true;
                }
            }

            if(c == null)
            {
                c = new Category { IsSelected = true, Item = item };
            }

            UpdateViews(c);
        }

        /// <summary>
        /// Finds an item in the tree.
        /// This is not efficient by any means, and is a straight O(n) scan of the tree.
        /// </summary>
        /// <param name="item"></param>
        /// <param name="parent"></param>
        /// <returns></returns>
        private Category FindInTree(IItem item, Category parent = null) {
            if(parent == null)
            {
                var treeItems = ItemTreeView.Items;
                foreach (var ti in treeItems)
                {
                    var c = (Category)ti;
                    var result = FindInTree(item, c);
                    if (result != null)
                    {
                        // Apparently the ParentCategory field is not
                        // always populated correctly.
                        if (result.ParentCategory == null)
                        {
                            result.ParentCategory = c;
                        }
                    }
                    return result;
                }
            } else
            {
                foreach(var c in parent.Categories)
                {
                    if(c.Item != null)
                    {

                        if(c.Item.Name == item.Name)
                        {
                            return c;
                        }
                    } else
                    {
                        var r = FindInTree(item, c);
                        if(r != null)
                        {
                            // Apparently the ParentCategory field is not
                            // always populated correctly.
                            if(r.ParentCategory == null)
                            {
                                r.ParentCategory = c;
                            }
                            return r;
                        }
                    }
                }
            }
            return null;
        }

        /// <summary>
        /// Updates the texture and model views with the selected item
        /// </summary>
        /// <param name="selectedItem">The selected item</param>
        private async void UpdateViews(Category category)
        {
            if ((category != null && category.Item != null) // Thing we're selecting has a valid item.
                && (_selectedCategory == null || _selectedCategory.Item == null // And either we have no item selected
                    || (!_selectedCategory.Item.Equals(category.Item))))        // Or a different item selected.
            {
                // De-select the previous category.
                if(_selectedCategory != null && _selectedCategory.IsSelected)
                {
                    _selectedCategory.IsSelected = false;
                }

                var item = category.Item;
                _selectedCategory = category;
                ItemTreeView.IsEnabled = false;
                var textureView = TextureTabItem.Content as TextureView;
                var textureViewModel = textureView.DataContext as TextureViewModel;
                var sharedItemsView = SharedItemsTab.Content as SharedItemsView;
                var sharedItemsViewModel = sharedItemsView.DataContext as SharedItemsViewModel;

                await textureViewModel.UpdateTexture(item);
                var showSharedItems = await sharedItemsViewModel.SetItem(item, this);
                if(showSharedItems) { 
                    SharedItemsTab.IsEnabled = true;
                    SharedItemsTab.Visibility = Visibility.Visible;
                } else
                {
                    if(SharedItemsTab.IsSelected)
                    {
                        SharedItemsTab.IsSelected = false;
                        TextureTabItem.IsSelected = true;
                    }
                    SharedItemsTab.IsEnabled = false;
                    SharedItemsTab.Visibility = Visibility.Hidden;
                }


                if (item.PrimaryCategory.Equals(XivStrings.UI) ||
                    item.SecondaryCategory.Equals(XivStrings.Face_Paint) ||
                    item.SecondaryCategory.Equals(XivStrings.Equipment_Decals) ||
                    item.SecondaryCategory.Equals(XivStrings.Paintings))
                {
                    if (TabsControl.SelectedIndex == 1)
                    {
                        TabsControl.SelectedIndex = 0;
                    }

                    ModelTabItem.IsEnabled = false;
                    ModelTabItem.Visibility = Visibility.Hidden;
                }
                else
                {
                    ModelTabItem.IsEnabled = true;
                    ModelTabItem.Visibility = Visibility.Visible;

                    var modelView = ModelTabItem.Content as ModelView;
                    var modelViewModel = modelView.DataContext as ModelViewModel;

                    await modelViewModel.UpdateModel(item as IItemModel);
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
            TextureView textureView = null;
            TextureViewModel textureViewModel = null;
            ModelView modelView = null;
            ModelViewModel modelViewModel = null;

            if(TextureTabItem != null && ModelTabItem != null)
            {
                textureView = TextureTabItem.Content as TextureView;
                textureViewModel = textureView.DataContext as TextureViewModel;
                modelView = ModelTabItem.Content as ModelView;
                modelViewModel = modelView.DataContext as ModelViewModel;
            }

            if (!path.Extension.Contains("ttmp"))
            {
                FlexibleMessageBox.Show(string.Format(UIMessages.UnsupportedFileExtensionErrorMessage, path.Extension), 
                    UIMessages.UnsupportedFileExtensionErrorTitle, MessageBoxButtons.OK, MessageBoxIcon.Error);

                return 0;
            }

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
                            path, textureViewModel, modelViewModel, messageInImport);

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
                            ttmpData.ModPackJson, textureViewModel, modelViewModel, silent, messageInImport);

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
                    var simpleImport = new SimpleModPackImporter(path, null, textureViewModel, modelViewModel, silent, messageInImport);

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

                var problemChecker = new ProblemChecker(gameDirectory);

                var progressController = await this.ShowProgressAsync(UIStrings.Start_Over, UIMessages.PleaseStandByMessage);

                progressController.SetIndeterminate();

                IProgress<string> progress = new Progress<string>((update) =>
                {
                    progressController.SetMessage(update);
                });

                try
                {
                    await problemChecker.PerformStartOver(indexBackupsDirectory, progress, XivLanguages.GetXivLanguage(Settings.Default.Application_Language));
                }
                catch
                {
                    FlexibleMessageBox.Show(UIMessages.StartOverErrorMessage,
                        UIMessages.StartOverErrorTitle, MessageBoxButtons.OK, MessageBoxIcon.Error);
                    await progressController.CloseAsync();
                    return;
                }

                MainWindow.GetMainWindow().RefreshTree(this);

                await progressController.CloseAsync();


                var c = (ItemTreeView.SelectedItem as Category);
                UpdateViews(c);

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
                var problemChecker = new ProblemChecker(gameDirectory);
                var backupsDirectory = new DirectoryInfo(Properties.Settings.Default.Backup_Directory);
                try
                {
                    await problemChecker.BackupIndexFiles(backupsDirectory);
                }
                catch(Exception ex)
                {
                    FlexibleMessageBox.Show(string.Format(UIMessages.BackupFailedErrorMessage, ex.Message), UIMessages.BackupFailedTitle, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }                

                await this.ShowMessageAsync(UIMessages.BackupCompleteTitle, UIMessages.BackupCompleteMessage);
            }
        }

        public void SetFilter()
        {

            // This must be executed on the main UI thread if it's not already.
            Dispatcher.BeginInvoke((ThreadStart)delegate ()
            {
                var view = (CollectionView)CollectionViewSource.GetDefaultView(ItemTreeView.ItemsSource);
                view.Filter = SearchFilter;
            });
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

            var searchTerms = ItemSearchTextBox.Text.Split(' ');

            return searchTerms.All(term => ((Category)item).Name.ToLower().Contains(term.Trim().ToLower()));
        }

        private void ItemSearchTextBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            if (SearchTimer != null)
            {
                SearchTimer.Stop();
                SearchTimer.Start();
            }
        }

        private void SearchTimerOnElapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            Dispatcher.Invoke(UpdateFilter);
        }

        private void UpdateFilter()
        {
            try
            {
                CollectionViewSource.GetDefaultView(ItemTreeView.ItemsSource).Refresh();
            } catch(Exception ex)
            {
                //No op, non-critical.
            }
        }

        private void MetroWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (SearchTimer != null)
            {
                SearchTimer.Elapsed -= SearchTimerOnElapsed;
                SearchTimer.Dispose();
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
