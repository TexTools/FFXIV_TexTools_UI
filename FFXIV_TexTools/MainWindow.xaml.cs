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
using xivModdingFramework.Cache;

namespace FFXIV_TexTools
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow
    {
        private string _startupArgs;
        private static MainWindow _mainWindow;


        /// <summary>
        /// Static accessor, since we should only ever have one instance of this class anyways.
        /// </summary>
        /// <returns></returns>
        public static MainWindow GetMainWindow()
        {
            return _mainWindow;
        }

        private System.Timers.Timer _statusTimer;

        private bool _uiLocked = false;

        public bool IsUiLocked
        {
            get { return _uiLocked; }
        }

        private IProgress<string> _lockProgress;

        /// <summary>
        /// Progress message reporter for the lock screen.  
        /// Only available during the window lock period.
        /// </summary>
        public IProgress<string> LockProgress { get { return _lockProgress; } }

        private ProgressDialogController _lockProgressController;

        /// <summary>
        /// Fired when the old tree is about to be discarded.
        /// </summary>
        public event EventHandler TreeRefreshing;

        /// <summary>
        /// Fired once the tree has been fully rebuilt.
        /// </summary>
        public event EventHandler TreeRefreshed;

        /// <summary>
        /// Fired when the sub views are about to be changed.
        /// </summary>
        public event EventHandler ItemChanging;

        /// <summary>
        /// Fired once all the sub-views have been fully initialized and the UI unlocked.
        /// </summary>
        public event EventHandler ItemChanged;

        /// <summary>
        /// Fired after the UI has been locked.
        /// </summary>
        public event EventHandler UiLocked;

        /// <summary>
        /// Fired after the UI has been unlocked.
        /// </summary>
        public event EventHandler UiUnlocked;

        /// <summary>
        /// Fired when the cache has been validated.
        /// </summary>
        public event EventHandler InitialLoadComplete;


        private bool _UPDATING = false;
        private void AutoUpdater_ApplicationExitEvent()
        {
            _UPDATING = true;
        }
        public MainWindow(string[] args)
        {
            _mainWindow = this;
            AutoUpdater.ApplicationExitEvent += AutoUpdater_ApplicationExitEvent;

            CheckForUpdates();

            // This slightly unusual contrivance is to ensure that we actually exit program on updates
            // *before* performing the rest of the startup initialization.  If we let it continue
            // some odd things can result.

            // In particular, threads can be spawned that may keep the application files locked when 
            // the updater wants to replace them, and/or new installs can error out on the culture info
            // lines below, due to not having valid settings after Application.Shutdown() was already called.
            if(_UPDATING)
            {
                if (Application.Current != null) { 
                    Application.Current.Shutdown();
                }
                return;
            }

            CheckForSettingsUpdate();
            LanguageSelection();

            var ci = new CultureInfo(Properties.Settings.Default.Application_Language)
            {
                NumberFormat = { NumberDecimalSeparator = "." }
            };

            CultureInfo.DefaultThreadCurrentCulture = ci;
            CultureInfo.DefaultThreadCurrentUICulture = ci;
            CultureInfo.CurrentCulture = ci;
            CultureInfo.CurrentUICulture = ci;

            // Data Context needs to be set before we call Initialize Component to ensure
            // that the bindings get connected immediately, and not after the constructor.
            var mainViewModel = new MainViewModel(this);
            this.DataContext = mainViewModel;


            InitializeComponent();

            var fileVersion = FileVersionInfo.GetVersionInfo(System.Reflection.Assembly.GetExecutingAssembly().Location).FileVersion;

            // Clear out the debug message shown in the xaml designer.
            StatusTextBox.Text = "";

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


                var textureView = TextureTabItem.Content as TextureView;
                var textureViewModel = textureView.DataContext as TextureViewModel;

                textureViewModel.LoadingComplete += TextureViewModelOnLoadingComplete;

                var modelView = ModelTabItem.Content as ModelView;
                var modelViewModel = modelView.DataContext as ModelViewModel;

                modelViewModel.LoadingComplete += ModelViewModelOnLoadingComplete;


                // This can be set whereever, since the item select won't fire it unless things are loaded fully.
                ItemSelect.ItemSelected += ItemSelect_ItemSelected;
                ItemSelect.ItemsLoaded += OnTreeLoaded;

                InitializeCache();
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
            } else if (lang != Properties.Settings.Default.Application_Language)
            {
                // We shouldn't evet actually get here, as this function is only called on 
                // first time installs, where lang would be empty, and other calls force-restart the application.
                // But doesn't hurt to have a safety check here anyways.
                InitializeCache();
            }
        }

        /// <summary>
        /// Initializes the Cache and loads the item tree for the first time when done.
        /// </summary>
        /// <returns></returns>
        private async Task InitializeCache()
        {
            var gameDir = new DirectoryInfo(Properties.Settings.Default.FFXIV_Directory);
            var lang = XivLanguages.GetXivLanguage(Properties.Settings.Default.Application_Language);
            await LockUi("Validating Cache", "If you have many mods, this may take a minute...", this);
            // Kick this in a new thread because the cache call will lock up the one it's on.
            await Task.Run(async () =>
            {
                bool cacheOK = true;
                try
                {
                    // If the cache needs to be rebuilt, this will synchronously block until it is done.
                    XivCache.SetGameInfo(gameDir, lang);
                } catch(Exception ex)
                {
                    cacheOK = false;
                    FlexibleMessageBox.Show("An error occurred while attempting to rebuild the cache.\n" + ex.Message, "Cache Rebuild Error.", MessageBoxButtons.OK, MessageBoxIcon.Error, MessageBoxDefaultButton.Button1);
                }

                await Dispatcher.Invoke(async () =>
                {
                    await UnlockUi();

                    if (cacheOK)
                    {
                        RefreshTree();
                    }

                    if(InitialLoadComplete != null)
                    {
                        InitialLoadComplete.Invoke(this, null);
                    }
                });
            });
        }


        // Item load helpers.
        bool _modelLoaded = false;
        bool _texturesLoaded = false;
        bool _waitingForItemLoad = false;
        private void ModelViewModelOnLoadingComplete(object sender, EventArgs e)
        {
            _modelLoaded = true;
            CheckItemLoadComplete();
        }

        private void TextureViewModelOnLoadingComplete(object sender, EventArgs e)
        {
            _texturesLoaded = true;
            CheckItemLoadComplete();
        }

        private async Task CheckItemLoadComplete()
        {
            // Don't allow spurious event firings to fuck with us.
            if(!_waitingForItemLoad)
            {
                _modelLoaded = false;
                _texturesLoaded = false;
            }

            if(_modelLoaded && _texturesLoaded)
            {
                _modelLoaded = false;
                _texturesLoaded = false;
                _waitingForItemLoad = false;

                await UnlockUi();

                ShowStatusMessage("Item Loaded Successfully.");
                if (ItemChanged != null)
                {
                    ItemChanged.Invoke(this, null);
                }
            }
        }

        private SemaphoreSlim _lockScreenSemaphore = new SemaphoreSlim(1);
        public async Task LockUi(string title = "Loading", string msg = "Please Wait...", object caller = null)
        {
            await _lockScreenSemaphore.WaitAsync();
            if (_uiLocked) return;

            _uiLocked = true;
            _lockProgressController = await this.ShowProgressAsync(title, msg);

            _lockProgressController.SetIndeterminate();

            _lockProgress = new Progress<string>((update) =>
            {
                _lockProgressController.SetMessage(update);
            });

            _lockScreenSemaphore.Release();

            if (UiLocked != null)
            {
                UiLocked.Invoke(caller, null);
            }
        }

        public async Task UnlockUi(object caller = null)
        {
            await _lockScreenSemaphore.WaitAsync();
            if (!_uiLocked) return;

            _uiLocked = false;

            try
            {
                // Sometimes this chokes, not sure why.
                await _lockProgressController.CloseAsync();
            } catch
            {

            }
            _lockProgressController = null;
            _lockProgress = null;

            _lockScreenSemaphore.Release();

            if (UiUnlocked != null)
            {
                UiUnlocked.Invoke(caller, null);
            }
        }

        public void Restart()
        {
            System.Diagnostics.Process.Start(Application.ResourceAssembly.Location);
            Application.Current.Shutdown();
            // Async invoked here to avoid deadlock in case this was called in some kind of dialog window.
            Dispatcher.InvokeAsync(() =>
            {
            });
        }

        public void ShowStatusMessage(string message, float duration = 5000.0f)
        {
            Dispatcher.InvokeAsync(() =>
            {
                if (_statusTimer != null)
                {
                    _statusTimer.Stop();
                    _statusTimer.Dispose();
                }

                StatusTextBox.Text = message;
                _statusTimer = new System.Timers.Timer(duration);
                _statusTimer.Elapsed += StatusTimerExpired; ;
                _statusTimer.AutoReset = false;
                _statusTimer.Enabled = true;
            });
        }

        private void StatusTimerExpired(object sender, System.Timers.ElapsedEventArgs e)
        {
            Dispatcher.InvokeAsync(() =>
            {
                StatusTextBox.Text = "";
                if (_statusTimer != null)
                {
                    _statusTimer.Stop();
                    _statusTimer.Dispose();
                }
            });
        }

        /// <summary>
        /// Triggers the MainWindow's thread to refresh the tree view.
        /// </summary>
        /// <param name="requestor"></param>
        public void RefreshTree(object requestor = null)
        {
            if (TreeRefreshing != null)
            {
                // Let any other elements that need to know know that we're reloading the item list.
                TreeRefreshing.Invoke(requestor, null);
            }

            ItemSelect.LoadItems();
        }

        private void CheckForUpdates()
        {
            AutoUpdater.Synchronous = true;
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

        private void OnTreeLoaded(object sender, EventArgs e)
        {

            var vm = (MainViewModel)DataContext;
            vm.CacheTimer.Enabled = true;
            vm.CacheTimer.Start();
            Menu_ModConverter.IsEnabled = true;

            if (string.IsNullOrEmpty(Properties.Settings.Default.Default_Race_Selection))
            {
                Properties.Settings.Default.Default_Race_Selection = XivRace.Hyur_Midlander_Male.GetDisplayName();
            }

            ShowStatusMessage("Item List Loaded Successfully.");
            if (TreeRefreshed != null)
            {
                TreeRefreshed.Invoke(this, null);
            }
        }

        /// <summary>
        /// Select an item in the tree view (and switch to that item)
        /// </summary>
        /// <param name="item"></param>
        public void SetSelectedItem(IItem item)
        {
            ItemSelect.SelectedItem = item;
        }

        public IItem GetSelectedItem()
        {
            return ItemSelect.SelectedItem;
        }

        /// <summary>
        /// Reloads the currently selected item.
        /// </summary>
        public void ReloadItem()
        {
            UpdateViews(ItemSelect.SelectedItem);
        }

        /// <summary>
        /// Triggered when the ItemSelect has its selection changed.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ItemSelect_ItemSelected(object sender, EventArgs e)
        {
            UpdateViews(ItemSelect.SelectedItem);
        }

        /// <summary>
        /// Updates the various tab views with the information from the selected item.
        /// Locks the UI until those tab views come back and confirm they've been loaded.
        /// </summary>
        /// <param name="selectedItem">The selected item</param>
        private async void UpdateViews(IItem item)
        {
            if(item == null)
            {
                return;
            }

            if (ItemChanging != null)
            {
                ItemChanging.Invoke(this, null);
            }

            await LockUi();

            _modelLoaded = false;
            _texturesLoaded = false;
            _waitingForItemLoad = true;

            var textureView = TextureTabItem.Content as TextureView;
            var textureViewModel = textureView.DataContext as TextureViewModel;
            var sharedItemsView = SharedItemsTab.Content as SharedItemsView;
            var sharedItemsViewModel = sharedItemsView.DataContext as SharedItemsViewModel;

            // This guy has no funny async callback.  It's ready once these awaits are done.
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

                // No model to load, just set us as already loaded.
                _modelLoaded = true;

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
        private async void Menu_MakeStandardModpack_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new StandardModpackCreator { Owner = this };
            var result = dialog.ShowDialog();
            
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

        private async void Menu_RebuildCache_Click(object sender, RoutedEventArgs e)
        {
            var r = FlexibleMessageBox.Show("This will rebuild the TexTools cache.\nThis may take up to 5 minutes if you have many mods installed.", "Cache Rebuild Confirmation", MessageBoxButtons.OKCancel, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button1);
            if (r == System.Windows.Forms.DialogResult.OK)
            {
                await LockUi("Rebuilding Cache");
                await Task.Run(XivCache.RebuildCache);
                await UnlockUi();
            }
        }
        private async void Menu_ScanForSets_Click(object sender, RoutedEventArgs e)
        {
            var r = FlexibleMessageBox.Show("This will scan the entire FFXIV file system for new item sets.\n\nThis operation can take up to an hour.\nAre you sure you wish to proceed?.", "Set Scan Confirmation", MessageBoxButtons.OKCancel, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button1);
            if (r == System.Windows.Forms.DialogResult.OK)
            {
                await LockUi("Scanning for new Item Sets", "This can take up to roughly an hour, depending on computer specs.");

                // Stop the worker, in case it was reading from the file for some reason.
                XivCache.CacheWorkerEnabled = false;

                await Task.Run(XivCache.RebuildAllRoots);
                await UnlockUi();
            }
        }
        private async void Menu_LoadSets_Click(object sender, RoutedEventArgs e)
        {
            using (OpenFileDialog openFileDialog = new OpenFileDialog())
            {
                openFileDialog.Filter = "db files (*.db)|*.db";
                openFileDialog.RestoreDirectory = true;

                if (openFileDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    // Stop the worker, in case it was reading from the file for some reason.
                    XivCache.CacheWorkerEnabled = false;

                    //Get the path of specified file
                    var filePath = openFileDialog.FileName;
                    var targetPath = new DirectoryInfo(Path.Combine(XivCache.GameInfo.GameDirectory.Parent.Parent.FullName, "item_sets.db"));
                    File.Delete(targetPath.FullName);
                    File.Copy(filePath, targetPath.FullName);

                    FlexibleMessageBox.Show("Item Sets loaded.\nRestarting TexTools.", "TexTools Restarting", MessageBoxButtons.OK);
                    Restart();
                }
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

                await LockUi(UIStrings.Start_Over, UIMessages.PleaseStandByMessage, this);


                try
                {
                    await problemChecker.PerformStartOver(indexBackupsDirectory, _lockProgress, XivLanguages.GetXivLanguage(Settings.Default.Application_Language));
                }
                catch(Exception ex)
                {
                    FlexibleMessageBox.Show(UIMessages.StartOverErrorMessage,
                        UIMessages.StartOverErrorTitle, MessageBoxButtons.OK, MessageBoxIcon.Error);
                    await UnlockUi();
                    return;
                }

                await UnlockUi();

                MainWindow.GetMainWindow().RefreshTree(this);



                var item = ItemSelect.SelectedItem;
                if (item != null)
                {
                    UpdateViews(item);
                }

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

        private void MetroWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
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
            var list= new List<xivModdingFramework.Items.Interfaces.IItem>();
            list.Add(ItemSelect.SelectedItem);
            var modConverterView = new ModConverterView(list,ttmpFileName, ttmpData) { Owner = this,WindowStartupLocation=WindowStartupLocation.CenterOwner };
            await progressController.CloseAsync();
            modConverterView.ShowDialog();
        }
    }
}
