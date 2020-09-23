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
using FFXIV_TexTools.Properties;
using FFXIV_TexTools.Resources;
using FFXIV_TexTools.ViewModels;
using FFXIV_TexTools.Views;
using FFXIV_TexTools.Views.ItemConverter;
using FFXIV_TexTools.Views.Metadata;
using FFXIV_TexTools.Views.Models;
using FolderSelect;
using ForceUpdateAssembly;
using MahApps.Metro;
using MahApps.Metro.Controls.Dialogs;
using SixLabors.ImageSharp;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Interop;
using System.Windows.Threading;
using xivModdingFramework.Cache;
using xivModdingFramework.Exd.FileTypes;
using xivModdingFramework.General.Enums;
using xivModdingFramework.Helpers;
using xivModdingFramework.Items.Categories;
using xivModdingFramework.Items.Interfaces;
using xivModdingFramework.Mods;
using xivModdingFramework.Mods.DataContainers;
using xivModdingFramework.Mods.FileTypes;
using xivModdingFramework.SqPack.FileTypes;
using static xivModdingFramework.Cache.XivCache;
using Application = System.Windows.Application;

namespace FFXIV_TexTools
{

    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow
    {
        private string _startupArgs;
        private static MainWindow _mainWindow;
        private FullModelView _fmv;
        public readonly System.Windows.Forms.IWin32Window Win32Window;

        public static readonly string BetaSuffix = "BETA";
        public static bool IsBetaVersion {
            get
            {
                return BetaSuffix != null;
            }
        }

        public event EventHandler<int> SelectedPrimaryItemValueChanged;

        private int _selectedPrimaryItemValue = -1;

        /// <summary>
        /// This number represents the selected Race or "Number" for the various tab views.
        /// Changing it will trigger an event that those views listen to, for cycling the numbers.
        /// </summary>
        public int SelectedPrimaryItemValue
        {
            get { return _selectedPrimaryItemValue; }
            set
            {
                // Only allow changing this value if it's *actually* changing, and to a real value.
                if (SelectedPrimaryItemValueChanged != null && value >= 0 && value != _selectedPrimaryItemValue)
                {
                    _selectedPrimaryItemValue = value;
                    if (Properties.Settings.Default.Sync_Views)
                    {
                        SelectedPrimaryItemValueChanged.Invoke(this, value);
                    }
                }
            }
        }



        /// <summary>
        /// Static accessor, since we should only ever have one instance of this class anyways.
        /// </summary>
        /// <returns></returns>
        public static MainWindow GetMainWindow()
        {
            return _mainWindow;
        }

        private System.Timers.Timer _statusTimer;

        public bool IsUiLocked
        {
            get { return _lockProgressController != null; }
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

            // Forcefully assign the correct working directory.  This helps keep the 
            // AutoUpdater from choking.
            var cwd = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetEntryAssembly().Location);
            Directory.SetCurrentDirectory(cwd);

            CheckForUpdates();

            // This slightly unusual contrivance is to ensure that we actually exit program on updates
            // *before* performing the rest of the startup initialization.  If we let it continue
            // some odd things can result.

            // In particular, threads can be spawned that may keep the application files locked when 
            // the updater wants to replace them, and/or new installs can error out on the culture info
            // lines below, due to not having valid settings after Application.Shutdown() was already called.
            if(_UPDATING)
            {
                // Shut down any other copies of TexTools that are active.
                MainWindow.MakeHighlander();

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
                int dxVersion = 0;
                bool success = Int32.TryParse(Settings.Default.DX_Version, out dxVersion);
                if (!success)
                {
                    dxVersion = 11;
                }

                // Just do a hard synchronous cache initialization for import only mode.
                var gameDir = new DirectoryInfo(Properties.Settings.Default.FFXIV_Directory);
                var lang = XivLanguages.GetXivLanguage(Properties.Settings.Default.Application_Language);
                XivCache.SetGameInfo(gameDir, lang, dxVersion, true, false);

                _startupArgs = args[0];
                OnlyImport();
            }
            else
            {
                this.Show();

                // Can set this now that we're open.
                Win32Window = new WindowWrapper(new WindowInteropHelper(this).Handle);


                var textureView = TextureTabItem.Content as TextureView;
                var textureViewModel = textureView.DataContext as TextureViewModel;

                textureViewModel.LoadingComplete += TextureViewModelOnLoadingComplete;

                var modelView = ModelTabItem.Content as ModelView;
                var modelViewModel = modelView.DataContext as ModelViewModel;

                modelViewModel.LoadingComplete += ModelViewModelOnLoadingComplete;
                modelViewModel.AddToFullModelEvent += ModelViewModelOnAddToFullModelEvent;

                this.TabsControl.SelectionChanged += TabsControl_SelectionChanged;


                // This can be set whereever, since the item select won't fire it unless things are loaded fully.
                ItemSelect.ItemSelected += ItemSelect_ItemSelected;
                ItemSelect.ItemsLoaded += OnTreeLoaded;

                InitializeCache();
            }
        }

        private void TabsControl_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (e.Source == TabsControl)
            {
                if (TabsControl.SelectedItem == this.ModelTabItem)
                {
                    var modelView = ModelTabItem.Content as ModelView;
                    var modelViewModel = modelView.DataContext as ModelViewModel;
                    modelViewModel.OnTabShown();
                }
            }
        }

        /// <summary>
        /// Event handler for adding models to the full model view
        /// </summary>
        private async void ModelViewModelOnAddToFullModelEvent(object sender, EventArgs e)
        {
            // Gets the model data of the current model in the model view
            var modelData = e as ModelViewModel.fullModelEventArgs;

            var fmv = FullModelView.Instance;
            fmv.Owner = this;
            fmv.Show();

            await fmv.AddModel(modelData.TTModelData, modelData.TextureData, modelData.Item, modelData.XivRace);
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

        private bool _FFXIV_PATCHED = false;
        private bool _NEW_INSTALL = false;
        private void OnCacheRebuild(object sender, CacheRebuildReason reason)
        {
            // If the cache is cycling because of a FFXIV version mismatch, we need to trigger the 
            // version update process after it's done.
            if(IsUiLocked)
            {
                _lockProgress.Report("Rebuilding Cache... This may take up to 60 seconds.  (Rebuild Reason: " + reason.ToString() + ")");
            }

            if (reason == CacheRebuildReason.FFXIVUpdate)
            {
                _FFXIV_PATCHED = true;
            }

            if(reason == CacheRebuildReason.NoCache)
            {
                // If the user had no cache, and no modlist, they're a new install (or close enough to one)
                var gameDir = new DirectoryInfo(Properties.Settings.Default.FFXIV_Directory);
                var modding = new Modding(gameDir);
                var modList = modding.GetModList();

                if(modList.Mods.Count == 0)
                {
                    // New install prompt time after rebuild is done.
                    _NEW_INSTALL = true;
                }
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
            await LockUi(UIStrings.Updating_Cache, UIStrings.Updating_Cache_Message, this);

            // Kick this in a new thread because the cache call will lock up the one it's on if it has to do a rebuild.
            await Task.Run(async () =>
            {
                bool cacheOK = true;
                try
                {
                    // If the cache needs to be rebuilt, this will synchronously block until it is done.
                    int dxVersion = 0;
                    bool success = Int32.TryParse(Settings.Default.DX_Version, out dxVersion);
                    if (!success)
                    {
                        dxVersion = 11;
                    }

                    XivCache.CacheRebuilding += OnCacheRebuild;
                    XivCache.SetGameInfo(gameDir, lang, dxVersion);
                    CustomizeViewModel.UpdateCacheSettings();

                } catch(Exception ex)
                {
                    cacheOK = false;
                    if(ex.GetType() == typeof(AggregateException))
                    {
                        var x = (AggregateException)ex;
                        var bas = x.GetBaseException();
                        if(bas != null)
                        {
                            ex = bas;
                        }

                    }
                    FlexibleMessageBox.Show("An error occurred while attempting to rebuild the cache.\n" + ex.Message, "Cache Rebuild Error.", MessageBoxButtons.OK, MessageBoxIcon.Error, MessageBoxDefaultButton.Button1);
                }

                await Dispatcher.Invoke(async () =>
                {
                    await UnlockUi();

                    // Oh boy, update time.  This part's fun.
                    if(_FFXIV_PATCHED)
                    {
                        var vm = (MainViewModel)DataContext;
                        await vm.DoPostPatchCleanup();
                        _FFXIV_PATCHED = false;
                    }

                    if(_NEW_INSTALL)
                    {
                        // Back up their stuff if they're a totally fresh install.
                        var gameDirectory = new DirectoryInfo(Settings.Default.FFXIV_Directory);
                        var problemChecker = new ProblemChecker(gameDirectory);
                        var backupsDirectory = new DirectoryInfo(Properties.Settings.Default.Backup_Directory);


                        await LockUi("Creating Initial Backups", "This should only take a moment...");
                        try
                        {
                            await problemChecker.BackupIndexFiles(backupsDirectory);
                            await this.ShowMessageAsync(UIMessages.BackupCompleteTitle, UIMessages.BackupCompleteMessage);
                        }
                        catch (Exception ex)
                        {
                            FlexibleMessageBox.Show(string.Format(UIMessages.BackupFailedErrorMessage, ex.Message), UIMessages.BackupFailedTitle, MessageBoxButtons.OK, MessageBoxIcon.Error);
                        }
                        finally
                        {
                            await UnlockUi();
                        }
                    }

                    if (cacheOK)
                    {
                        await RefreshTree();
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

        public async Task LockUi(string title = null, string msg = null, object caller = null)
        {
            await _lockScreenSemaphore.WaitAsync();
            try
            {
                if (IsUiLocked)
                {
                    return;
                }

                if (title == null)
                {
                    title = UIStrings.Loading;
                }

                if (msg == null)
                {
                    msg = UIStrings.Please_Wait;
                }

                // If the lock screen doesn't proc within 1 second, kill it.
                const int timeout = 1000;
                var task = this.ShowProgressAsync(title, msg);

                if (await Task.WhenAny(task, Task.Delay(timeout)) == task)
                {
                    // Task completed within timeout
                    _lockProgressController = task.Result;
                    _lockProgressController.SetIndeterminate();

                    _lockProgress = new Progress<string>((update) =>
                    {
                        _lockProgressController.SetMessage(update);
                    });
                }
                else
                {
                    // Lock screen failed to resolve, don't let us deadlock.
                }
            }
            finally
            {
                _lockScreenSemaphore.Release();
            }

            if (UiLocked != null)
            {
                UiLocked.Invoke(caller, null);
            }
        }

        public async Task UnlockUi(object caller = null)
        {
            await _lockScreenSemaphore.WaitAsync();
            try
            {
                if (!IsUiLocked)
                {
                    return;
                }

                try
                {
                    // Sometimes this chokes, not sure why.
                    const int timeout = 1000;
                    var task = _lockProgressController.CloseAsync();

                    if (await Task.WhenAny(task, Task.Delay(timeout)) == task)
                    {
                        // Task completed within timeout
                    }
                    else
                    {
                        // Unlock screen failed to resolve, don't let us deadlock.
                    }
                }
                catch
                {

                }
                _lockProgressController = null;
                _lockProgress = null;
            }
            finally
            {
                _lockScreenSemaphore.Release();
            }

            if (UiUnlocked != null)
            {
                UiUnlocked.Invoke(caller, null);
            }
        }

        public void Restart()
        {
            System.Diagnostics.Process.Start(Application.ResourceAssembly.Location);
            Application.Current.Shutdown();
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
        public async Task RefreshTree(object requestor = null)
        {
            if (TreeRefreshing != null)
            {
                // Let any other elements that need to know know that we're reloading the item list.
                TreeRefreshing.Invoke(requestor, null);
            }

            await ItemSelect.LoadItems();
        }

        public static void CheckForUpdates()
        {
            AutoUpdater.Synchronous = true;
            try
            {
                if (IsBetaVersion)
                {
                    AutoUpdater.Start(WebUrl.TexTools_Beta_Update_Url);
                } else
                {
                    AutoUpdater.Start(WebUrl.TexTools_Update_Url);
                }
            } catch
            {
                AutoUpdater.Start(WebUrl.TexTools_Update_Url);
            }
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

        /// <summary>
        /// There can only be one.
        /// </summary>
        public static void MakeHighlander()
        {
            try
            {
                List<Process> toKill = new List<Process>();
                // Scan all processes for any processes with our name.
                var self = Process.GetCurrentProcess();

                Process[] processCollection = Process.GetProcesses();
                foreach (Process p in processCollection)
                {
                    if (p.ProcessName == self.ProcessName && p.Id != self.Id)
                    {
                        toKill.Add(p);
                    }
                }

                if (toKill.Count > 0)
                {
                    FlexibleMessageBox.Show("More than one TexTools process detected.  Shutting down other TexTools copies.", "Multi-Application Shutdown.", MessageBoxButtons.OK, MessageBoxIcon.Warning);

                    foreach (var p in toKill)
                    {
                        p.Kill();
                    }
                }
            }
            catch
            {
                // If this fails because of some security issue on getting the process list or the like,
                // just try to continue as normal.
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
        private void ItemSelect_ItemSelected(object sender, IItem item)
        {
            UpdateViews(item);
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
            var metadataView = MetadataTab.Content as MetadataView;

            var showMetadata = await metadataView.SetItem(item);
            if (showMetadata)
            {
                MetadataTab.IsEnabled = true;
                MetadataTab.Visibility = Visibility.Visible;
            }
            else
            {
                if (MetadataTab.IsSelected)
                {
                    MetadataTab.IsSelected = false;
                    TextureTabItem.IsSelected = true;
                }
                MetadataTab.IsEnabled = false;
                MetadataTab.Visibility = Visibility.Collapsed;
            }

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
                SharedItemsTab.Visibility = Visibility.Collapsed;
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
                ModelTabItem.Visibility = Visibility.Collapsed;
            }
            else
            {
                ModelTabItem.IsEnabled = true;
                ModelTabItem.Visibility = Visibility.Visible;

                var modelView = ModelTabItem.Content as ModelView;
                var modelViewModel = modelView.DataContext as ModelViewModel;

                await modelViewModel.UpdateModel(item as IItemModel);
            }

            // Need to do this here in case any of the views came back immediately and fired before we finished this function.
            await CheckItemLoadComplete();
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
            var modsErrored = 0;
            float duration = 0;

            foreach (var fileName in openFileDialog.FileNames)
            {
                var fileInfo = new FileInfo(fileName);

                if (fileInfo.Length == 0)
                {
                    FlexibleMessageBox.Show(string.Format(UIMessages.EmptyTTMPFileErrorMessage, Path.GetFileNameWithoutExtension(fileName)), UIMessages.ImportErrorTitle, MessageBoxButtons.OK, MessageBoxIcon.Error);
                    continue;
                }

                var r = await ImportModpack(new DirectoryInfo(fileName), modPackDirectory, importMultiple);
                modsImported += r.Imported;
                modsErrored += r.Errors;
                duration += r.Duration;
            }

            if (modsImported > 0)
            {
                var durationString = duration.ToString("0.00");
                await this.ShowMessageAsync(UIMessages.ImportCompleteTitle, string.Format(UIMessages.SuccessfulImportCountMessage, modsImported, modsErrored, durationString));
            }
        }

        /// <summary>
        /// This method opens the modpack import wizard or imports a modpack silently
        /// </summary>
        /// <param name="path">The path to the modpack</param>
        /// <param name="silent">If the modpack wizard should be shown or the modpack should just be imported without any user interaction</param>
        /// <returns></returns>
        private async Task<(int Imported, int Errors, float Duration)> ImportModpack(DirectoryInfo path, DirectoryInfo modPackDirectory, bool silent = false, bool messageInImport = false)
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

                return (0, 1, 0);
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

                        return (0, 1, 0);
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
                            return (importWizard.TotalModsImported, importWizard.TotalModsErrored, importWizard.ImportDuration);
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
                            return (simpleImport.TotalModsImported, simpleImport.TotalModsErrored, simpleImport.ImportDuration);
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
                        return (simpleImport.TotalModsImported, simpleImport.TotalModsErrored, simpleImport.ImportDuration);
                    }
                }
                else
                {
                    FlexibleMessageBox.Show(string.Format(UIMessages.ModPackImportErrorMessage, path.FullName, ex.Message), UIMessages.ModPackImportErrorTitle, MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return (0, 1, 0);
                }
            }

            return (0, 0, 0);
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
                try
                {
                    await Task.Run(() =>
                    {
                        XivCache.RebuildCache();
                    });

                    CustomizeViewModel.UpdateCacheSettings();
                } catch(Exception ex)
                {
                    FlexibleMessageBox.Show("Unable to rebuild cache file.\n\nError:" + ex.Message, "Cache Rebuild Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                await UnlockUi();
                await RefreshTree(this);
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

                try
                {
                    await Task.Run(XivCache.RebuildAllRoots);
                } catch(Exception ex)
                {
                    FlexibleMessageBox.Show( "An error occured while trying to scan for new item sets.\n\n" + ex.Message, "Item Scan Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                await UnlockUi();
                await RefreshTree();
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

                MakeHighlander();

                try
                {
                    await problemChecker.PerformStartOver(indexBackupsDirectory, _lockProgress, XivLanguages.GetXivLanguage(Settings.Default.Application_Language));
                    CustomizeViewModel.UpdateCacheSettings();
                }
                catch(Exception ex)
                {
                    while(ex.InnerException != null)
                    {
                        ex = ex.InnerException;
                    }

                    var msg = UIMessages.StartOverErrorMessage + "\n\nError: " + ex.Message;
                    FlexibleMessageBox.Show(msg,
                        UIMessages.StartOverErrorTitle, MessageBoxButtons.OK, MessageBoxIcon.Error);
                    await UnlockUi();
                    return;
                }

                await UnlockUi();

                await RefreshTree(this);



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
                await LockUi("Backing Up Indexes", "If you have many mods enabled, this may take some time...");
                try
                {
                    await problemChecker.BackupIndexFiles(backupsDirectory);
                    await this.ShowMessageAsync(UIMessages.BackupCompleteTitle, UIMessages.BackupCompleteMessage);
                }
                catch(Exception ex)
                {
                    FlexibleMessageBox.Show(string.Format(UIMessages.BackupFailedErrorMessage, ex.Message), UIMessages.BackupFailedTitle, MessageBoxButtons.OK, MessageBoxIcon.Error);
                } finally
                {
                    await UnlockUi();
                }
            }
        }

        private void MetroWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
        }

        private void MetroWindow_Loaded(object sender, RoutedEventArgs e)
        {
            var fileVersion = FileVersionInfo.GetVersionInfo(System.Reflection.Assembly.GetExecutingAssembly().Location).FileVersion;

            if (IsBetaVersion)
            {
                fileVersion = fileVersion + " " + BetaSuffix;
            }

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

        private async void Menu_ItemConverter_Click(object sender, RoutedEventArgs e)
        {

            var wind = new ItemConverterWindow() { Owner = this };
            wind.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            wind.Show();
        }

        /// <summary>
        /// Event handler for clicking on Full Model Viewer in the menu
        /// </summary>
        private void FullModelViewer_Click(object sender, RoutedEventArgs e)
        {
            var fmv = FullModelView.Instance;
            fmv.Owner = this;

            fmv.Show();
        }


        private void Menu_WebBackups_Click(object sender, RoutedEventArgs e)
        {
            DownloadIndexBackups();
        }
        private async Task DownloadIndexBackups()
        {
            var url = UIStrings.Index_Backups_Url;
            if (url == "INVALID" || String.IsNullOrWhiteSpace(url))
            {
                FlexibleMessageBox.Show("Index backup download is not currently supported for your client language.", "Web Download Error", MessageBoxButtons.OK, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button1);
                return;
            }

            var result = FlexibleMessageBox.Show("This will download index backups from the internet. Proceed?", "Web Download Confirmation",MessageBoxButtons.OKCancel, MessageBoxIcon.Information, MessageBoxDefaultButton.Button2);

            if (result != System.Windows.Forms.DialogResult.OK) return;

            await LockUi("Downloading Backups");
            try
            {
                await Task.Run(async () =>
                {
                    var tempDir = Path.GetTempPath();
                    tempDir += "/index_backup";
                    Directory.CreateDirectory(tempDir);

                    _lockProgress.Report("Downloading Indexes...");
                    var localPath = Path.GetTempFileName();
                    using (var client = new WebClient())
                    {
                        client.DownloadFile(url, localPath);
                    }


                    var tempDi = new DirectoryInfo(tempDir);
                    foreach (FileInfo file in tempDi.GetFiles())
                    {
                        file.Delete();
                    }


                    _lockProgress.Report("Unzipping new Indexes...");
                    ZipFile.ExtractToDirectory(localPath, tempDir);

                    _lockProgress.Report("Checking downloaded index version...");
                    var versionRaw = File.ReadAllText(tempDir + "/ffxivgame.ver");
                    
                    Version version = new Version(versionRaw.Substring(0, versionRaw.LastIndexOf(".", StringComparison.Ordinal)));

                    if (version != XivCache.GameInfo.GameVersion)
                    {
                        throw new Exception("Downloaded Index version does not match game version.");
                    }

                    _lockProgress.Report("Removing old Backups...");
                    var backupDir = new DirectoryInfo(Settings.Default.Backup_Directory);
                    foreach (FileInfo file in backupDir.GetFiles())
                    {
                        file.Delete();
                    }


                    _lockProgress.Report("Copying new indexes to backup directory...");
                    var newFiles = Directory.GetFiles(tempDi.FullName);
                    foreach (var nFile in newFiles)
                    {
                        try
                        {
                            if (nFile.Contains(".win32.index"))
                            {
                                File.Copy(nFile, $"{Settings.Default.Backup_Directory}/{Path.GetFileName(nFile)}", true);
                            }
                        }
                        catch (Exception ex)
                        {
                            throw new Exception("Failed to copy index files.\n\n" + ex.Message);
                        }
                    }


                    _lockProgress.Report("Job Done.");
                });
                FlexibleMessageBox.Show("Successfully downloaded fresh index backups.\nYou may now use [Start Over] to apply them, if desired.", "Backup Download Success", MessageBoxButtons.OK, MessageBoxIcon.Information, MessageBoxDefaultButton.Button1);
            }
            catch(Exception Ex)
            {
                FlexibleMessageBox.Show("Unable to download Index Backups.\n\n" + Ex.Message, "Web Download Error", MessageBoxButtons.OK, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button1);

            }
            finally
            {
                await UnlockUi();
            }
        }

        private void Menu_CopyFile_Click(object sender, RoutedEventArgs e)
        {
            var win = new CopyFileDialog() { Owner = this };
            win.Show();
        }

        private void Menu_ExtractRaw_Click(object sender, RoutedEventArgs e)
        {
            var win = new ExtractRawDialog() { Owner = this };
            win.Show();
        }

        private void Menu_ImportRaw_Click(object sender, RoutedEventArgs e)
        {
            var win = new ImportRawDialog() { Owner = this };
            win.Show();
        }

        private void ReportNumericProgress((int Count, int Total, string Message) data)
        {
            if (data.Total > 0)
            {
                float value = ((float)data.Count) / ((float)data.Total);
                _lockProgressController.SetProgress(value);
            } else
            {
                _lockProgressController.SetIndeterminate();
            }

            _lockProgress.Report(data.Message + $" ({data.Count}/{data.Total})");
        }
        private async void Menu_CleanUpModList_Click(object sender, RoutedEventArgs e)
        {
            var queueLength = XivCache.GetDependencyQueueLength();

            System.Windows.Forms.DialogResult result;
            if(queueLength > 100)
            {
                result = FlexibleMessageBox.Show("This will update the Modlist to ensure all modded files are\nlabeled under the correct items.\n\nAs the Queue currently has a large amount of files still processing,\nthis operation may take an extended amount of time to complete.\n(Up to one hour)", "Modlist Cleanup Confirmation", MessageBoxButtons.OKCancel, MessageBoxIcon.Warning);
            } else
            {
                result = FlexibleMessageBox.Show("This will update the Modlist to ensure all modded files are\nlabeled under the correct items.\n\nThis may take up to 5 minutes to complete.", "Modlist Cleanup Confirmation", MessageBoxButtons.OKCancel, MessageBoxIcon.Information);
            }

            if (result != System.Windows.Forms.DialogResult.OK) return;

            await LockUi("Cleaning up Modlist");
            try
            {
                Progress<(int Count, int Total, string Message)> reporter = new Progress<(int Count, int Total, string Message)>(ReportNumericProgress);
                // Run in new thread so UI doesn't lock.
                await Task.Run(async () =>
                {
                    var modding = new Modding(XivCache.GameInfo.GameDirectory);
                    await modding.CleanUpModlist(reporter);
                });
                FlexibleMessageBox.Show("The Modlist was cleaned up successfully.", "Cleanup Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch(Exception ex)
            {
                FlexibleMessageBox.Show("An error occurred during the cleanup process.\n\nError: " + ex.Message, "Cleanup Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                await UnlockUi();
            }
        }
        private static string FormatBytes(long bytes)
        {
            string[] Suffix = { "B", "KB", "MB", "GB", "TB" };
            int i;
            double dblSByte = bytes;
            for (i = 0; i < Suffix.Length && bytes >= 1024; i++, bytes /= 1024)
            {
                dblSByte = bytes / 1024.0;
            }

            return String.Format("{0:0.##} {1}", dblSByte, Suffix[i]);
        }

        private async void Menu_RecoverSpace_Click(object sender, RoutedEventArgs e)
        {
            var result = FlexibleMessageBox.Show("This will recover unused space in the game files by defragmenting the modded DAT files.\n\nPlease do not close TexTools or open FFXIV until this operation is complete", "Recover Space Confirmation", MessageBoxButtons.OKCancel, MessageBoxIcon.Information);

            if (result != System.Windows.Forms.DialogResult.OK) return;
            await LockUi("Defragmenting DAT Files");
            try
            {
                long savedBytes = 0;
                Progress<(int Count, int Total, string Message)> reporter = new Progress<(int Count, int Total, string Message)>(ReportNumericProgress);
                // Run in new thread so UI doesn't lock.
                await Task.Run(async () =>
                {
                    var modding = new Modding(XivCache.GameInfo.GameDirectory);
                    savedBytes = await modding.DefragmentModdedDats(reporter);
                });

                var savedSpace = FormatBytes(savedBytes);
                FlexibleMessageBox.Show($"DAT File Defragmentation completed successfully.\n\n{savedSpace} of unused space has been recovered.", "Defragmentation Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                FlexibleMessageBox.Show("An error occurred during the defragmentation process.\n\nError: " + ex.Message, "Modlist Defragmentation Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                await UnlockUi();
            }
        }

        private void Menu_CopyModel_Click(object sender, RoutedEventArgs e)
        {
            var wind = new CopyModelDialog() { Owner = this };
            wind.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            wind.Show();
        }



        private void Menu_RacialScaling_Click(object sender, RoutedEventArgs e)
        {
            var wind = new RacialSettingsEditor() { Owner = this };
            wind.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            wind.Show();
        }
    }
}
