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
using FFXIV_TexTools.Views.Controls;
using FFXIV_TexTools.Views.Item;
using FFXIV_TexTools.Views.ItemConverter;
using FFXIV_TexTools.Views.Metadata;
using FFXIV_TexTools.Views.Models;
using FFXIV_TexTools.Views.Projects;
using FFXIV_TexTools.Views.Simple;
using FFXIV_TexTools.Views.Transactions;
using FFXIV_TexTools.Views.Wizard;
using FolderSelect;
using ForceUpdateAssembly;
using HelixToolkit.SharpDX.Core.Utilities;
using MahApps.Metro;
using MahApps.Metro.Controls.Dialogs;
using SixLabors.ImageSharp;
using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Forms;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using WK.Libraries.BetterFolderBrowserNS;
using xivModdingFramework.Cache;
using xivModdingFramework.Exd.FileTypes;
using xivModdingFramework.General.Enums;
using xivModdingFramework.Helpers;
using xivModdingFramework.Items.Categories;
using xivModdingFramework.Items.Interfaces;
using xivModdingFramework.Materials.DataContainers;
using xivModdingFramework.Materials.FileTypes;
using xivModdingFramework.Models.FileTypes;
using xivModdingFramework.Mods;
using xivModdingFramework.Mods.DataContainers;
using xivModdingFramework.Mods.FileTypes;
using xivModdingFramework.Mods.FileTypes.PMP;
using xivModdingFramework.SqPack.DataContainers;
using xivModdingFramework.SqPack.FileTypes;
using static System.Data.Entity.Infrastructure.Design.Executor;
using static xivModdingFramework.Cache.XivCache;

using Application = System.Windows.Application;

namespace FFXIV_TexTools
{

    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow
    {
        private int _LockCount = 0;
        private string _startupArgs;
        private static MainWindow _mainWindow;
        public readonly System.Windows.Forms.IWin32Window Win32Window;

#if ENDWALKER
        public static readonly string BetaSuffix = "- CursedTools Build 36 (ENDWALKER)";
#else
        public static readonly string BetaSuffix = "- CursedTools Build 36 (DAWNTRAIL)";
#endif
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


        private static ModTransaction _UserTransaction;

        /// <summary>
        /// The current active, end-user-controlled, write-enabled transaction, if there is one.
        /// Probably should go somewhere else later, but for now this is accessible until a final location is sorted.
        /// </summary>
        public static ModTransaction UserTransaction
        {
            get { return _UserTransaction;  }
            set
            {
                if(_UserTransaction != null)
                {
                    if(_UserTransaction.State != ETransactionState.Closed)
                    {
                        throw new Exception("Cannot assign new user transaction when one already exists and is not closed.");
                    }
                    _UserTransaction.TransactionStateChanged -= OnUserTxChanged;
                    _UserTransaction.TransactionSettingsChanged -= OnUserTxSettingsChanged;
                    _UserTransaction.FileChanged -= OnUserTxFileChanged;
                }

                _UserTransaction = value;
                TxWatcher.INTERNAL_TxStateChanged(ETransactionState.Invalid, value.State);
                value.TransactionStateChanged += OnUserTxChanged;
                value.TransactionSettingsChanged += OnUserTxSettingsChanged;
                value.FileChanged += OnUserTxFileChanged;
            }
        }

        private static void OnUserTxChanged(ModTransaction sender, ETransactionState oldState, ETransactionState newState)
        {
            if(_UserTransaction != sender)
            {
                return;
            }

            TxWatcher.INTERNAL_TxStateChanged(oldState, newState);

            if(newState == ETransactionState.Closed ||  newState == ETransactionState.Invalid) {
                _UserTransaction = null;
            }
        }
        private static void OnUserTxSettingsChanged(ModTransaction sender, ModTransactionSettings settings)
        {
            if (_UserTransaction != sender)
            {
                return;
            }

            TxWatcher.INTERNAL_TxSettingsChanged(settings);
        }
        private static void OnUserTxFileChanged(string internalFilePath, long newOffset)
        {
            TxWatcher.INTERNAL_TxFileChanged(internalFilePath);
        }


        public static ModTransaction DefaultTransaction
        {
            get
            {
                if(UserTransaction != null)
                {
                    return UserTransaction;
                }

                // Default to a new readonly transaction.
                return ModTransaction.BeginTransaction();
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


        private ProgressDialogController _lockProgressController ;

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
            } else
            {
                // No updates needed? We can clear out the update path then.
                //var updateDir = Path.Combine(Environment.CurrentDirectory, "update");
                //Directory.Delete(updateDir, true);
            }

            XivCache.GameWriteStateChanged += XivCache_GameWriteStateChanged;

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

            CustomizeViewModel.UpdateFrameworkColors();

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
                XivCache.SetGameInfo(gameDir, lang, false);

                _startupArgs = args[0];
                OnlyImport();
            }
            else
            {
                // Normal startup process.
                this.Show();

                // Can set this now that we're open.
                Win32Window = new WindowWrapper(new WindowInteropHelper(this).Handle);

                // This can be set whereever, since the item select won't fire it unless things are loaded fully.
                ItemSelect.ItemSelected += ItemSelect_ItemSelected;
                ItemSelect.ItemsLoaded += OnTreeLoaded;

                ModTransaction.ActiveTransactionBlocked += ModTransaction_ActiveTransactionBlocked;
                _ = AsyncStartup();

            }

            KeyDown += OnKeyDown;
        }

        private void OnKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (ItemView != null)
            {
                ItemView.OnKeyDown(sender, e);
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
                _ = InitializeCache();
            }
        }

        private bool _FFXIV_PATCHED = false;
        private bool _NEW_INSTALL = false;
        private async void OnCacheRebuild(object sender, CacheRebuildReason reason)
        {
            // If the cache is cycling because of a FFXIV version mismatch, we need to trigger the 
            // version update process after it's done.
            if(IsUiLocked)
            {
                _lockProgress.Report($"Rebuilding Cache... This may take up to 60 seconds.  (Rebuild Reason: {reason.ToString()._()})".L());
            }

            if (reason == CacheRebuildReason.FFXIVUpdate)
            {
                _FFXIV_PATCHED = true;
            }

            if(reason == CacheRebuildReason.NoCache)
            {
                // If the user had no cache, and no modlist, they're a new install (or close enough to one)
                var tx = MainWindow.DefaultTransaction;

                if((await tx.GetModList()).Mods.Count == 0)
                {
                    // New install prompt time after rebuild is done.
                    _NEW_INSTALL = true;
                }
            }
        }


        private async Task AsyncStartup()
        {
            await InitializeCache();

            if (!string.IsNullOrWhiteSpace(Settings.Default.Backup_Directory))
            {
                var validBackups = ProblemChecker.AreBackupsValid(Settings.Default.Backup_Directory);
                if (!validBackups)
                {
                    if(this.InfoPrompt("Missing Index Backups", "Your do not currently have Index Backups, or they are from a previous game version.\n\nWould you like to create new backups now?  This is STRONGLY recommended."))
                    {
                        await LockUi("Creating Index Backups");
                        try
                        {
                            await ProblemChecker.CreateIndexBackups(Settings.Default.Backup_Directory);
                        }
                        catch(Exception ex)
                        {
                            this.ShowError("Index Backup Error", "An error occurred while creating the index backups:\n\n" + ex.Message);
                        }
                        finally
                        {
                            await UnlockUi();
                        }
                    }
                }
            }

            if (Settings.Default.OpenTransactionOnStart)
            {
                UserTransaction = ModTransaction.BeginTransaction(true, null, null, false, false);
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


                    XivCache.SetGameInfo(gameDir, lang, true);
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
                    FlexibleMessageBox.Show(("An error occurred while attempting to rebuild the cache. This may be caused by this version of Final Fantasy XIV " +
                        "not being supported by this version of TexTools.\n\n").L() + ex.Message, "Cache Rebuild Error.".L(), MessageBoxButtons.OK,  MessageBoxIcon.Error, 
                        MessageBoxDefaultButton.Button1);
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


                        await LockUi("Creating Initial Backups".L(), "This should only take a moment...".L());
                        try
                        {
                            await ProblemChecker.CreateIndexBackups(Settings.Default.Backup_Directory);
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

            if (Settings.Default.LiveDangerously)
            {
                XivCache.GameWriteEnabled = true;
            } else
            {
                XivCache.GameWriteEnabled = false;
            }
            UpdateWriteStateUi();
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

                ShowStatusMessage("Item Loaded Successfully.".L());
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
                _LockCount++;
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

                _lockProgressController = await this.ShowProgressAsync(title, msg);
                _lockProgress = new Progress<string>((update) =>
                {
                    _lockProgressController.SetMessage(update);
                });
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
                _LockCount--;
                if(_LockCount < 0)
                {
                    _LockCount = 0;
                }

                if (!IsUiLocked)
                {
                    return;
                }

                if(_LockCount > 0)
                {
                    return;
                }

                await _lockProgressController.CloseAsync();
                _lockProgressController = null;
                _lockProgress = null;

                if (UiUnlocked != null)
                {
                    UiUnlocked.Invoke(caller, null);
                }
            }
            finally
            {
                _lockScreenSemaphore.Release();
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
            var updateDir = Path.Combine(Environment.CurrentDirectory, "update");
            Directory.CreateDirectory(updateDir);
            AutoUpdater.DownloadPath = updateDir;
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
                    FlexibleMessageBox.Show("More than one TexTools process detected.  Shutting down other TexTools copies.".L(), "Multi-Application Shutdown.".L(), MessageBoxButtons.OK, MessageBoxIcon.Warning);

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

            ShowStatusMessage("Item List Loaded Successfully.".L());
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
        /// NOTE: Forcibly closes all external viewers without prompts for user progress.
        /// </summary>
        public async Task ReloadItem()
        {
            CloseAllViewers();
            await UpdateViews(ItemSelect.SelectedItem);
        }

        /// <summary>
        /// Triggered when the ItemSelect has its selection changed.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void ItemSelect_ItemSelected(object sender, IItem item)
        {
            try
            {
                await UpdateViews(item);
            }
            catch(Exception ex)
            {
                Trace.WriteLine(ex);
            }
        }

        /// <summary>
        /// Updates the various tab views with the information from the selected item.
        /// Locks the UI until those tab views come back and confirm they've been loaded.
        /// </summary>
        /// <param name="selectedItem">The selected item</param>
        private async Task UpdateViews(IItem item)
        {
            await ItemView.SetItem(item);
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
        /// Event handler for the icon id search clicked
        /// </summary>
        private async void Menu_AutoSkinUpdate_Click(object sender, RoutedEventArgs e)
        {
            var r = FlexibleMessageBox.Show("This will auto-assign the skin materials for all moded player models.\n Are you sure you wish to proceed?".L(), "Skin Auto-Assign Confirmation".L(), MessageBoxButtons.OKCancel, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button1);
            if (r == System.Windows.Forms.DialogResult.OK)
            {
                await LockUi("Updating skin materials for all modded models...".L(),"This may take a few minutes if you have many mods.".L());
                try
                {
                    var changed = 0;
                    await Task.Run(async () =>
                    {
                        changed = await Mdl.CheckAllModsSkinAssignments();
                    });

                    FlexibleMessageBox.Show($"Skin Auto-Assigment is complete.\n\n{changed._()} Models updated.".L(), "Skin Auto - Assign Complete".L(), MessageBoxButtons.OK, MessageBoxIcon.Information);

                }
                catch (Exception ex)
                {
                    while (ex.InnerException != null)
                    {
                        ex = ex.InnerException;
                    }
                    FlexibleMessageBox.Show("An error occured while trying to update player models.\nYour mods/game files have not been altered.\n\nError:".L() + ex.Message, "Skin Auto-Assign Error".L(), MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                await UnlockUi();
            }
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
            Process.Start(WebUrl.PKEmporium_Discord);
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
            var modListView = new ModListView() {Owner = this};
            modListView.Show();
        }

        /// <summary>
        /// Event handler for the problem check menu item clicked
        /// </summary>
        private void Menu_ProblemCheck_Click(object sender, RoutedEventArgs e)
        {
            var problemCheckView = new ProblemCheckView {Owner = this};
            try
            {
                problemCheckView.Show();
                _ = problemCheckView.RunChecks();
            }
            catch
            {
                //No op
            }
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
            NotifyUnsaved();
            var wizard = new ExportWizardWindow { Owner = this};
            var result = wizard.ShowDialog();

            if (result == true)
            {
                await this.ShowMessageAsync(UIMessages.ModPackCreationCompleteTitle, string.Format(UIMessages.ModPackCreationCompleteMessage, wizard.ModPackFileName));
            }
        }

        private async void Menu_MakeStandardModpack_Click(object sender, RoutedEventArgs e)
        {
            NotifyUnsaved();
            var dialog = new StandardModpackCreator { Owner = this };
            var result = dialog.ShowDialog();
            
        }

        /// <summary>
        /// Event handler for when the create backup modpack menu item is clicked
        /// </summary>
        private async void Menu_MakeBackupModpack_Click(object sender, RoutedEventArgs e)
        {
            NotifyUnsaved();
            var tx = MainWindow.DefaultTransaction;
            var ml = await tx.GetModList();
            var backupCreator = new BackupModPackCreator(ml) { Owner = this };
            var result = backupCreator.ShowDialog();

            if (result == true)
            {
                await this.ShowMessageAsync(UIMessages.ModPackCreationCompleteTitle, string.Format(UIMessages.ModPackCreationCompleteMessage, backupCreator.ModPackFileName));
            }
        }

        /// <summary>
        /// Event handler for the import mod pack menu item clicked
        /// </summary>
        private async void Menu_ImportModpack_Click(object sender, RoutedEventArgs e)
        {
            if (!XivCache.GameWriteEnabled && UserTransaction == null)
            {
                this.ShowError("Mod Safety Error", "Cannot import modpacks in SAFE mode outside of Transaction.");
                return;
            }

            var modPackDirectory = new DirectoryInfo(Settings.Default.ModPack_Directory);

            var openFileDialog = new OpenFileDialog {InitialDirectory = modPackDirectory.FullName, Filter = "Modpack Files|*.ttmp;*.ttmp2;*.pmp;*.json".L(), Multiselect = true};

            if (openFileDialog.ShowDialog() != System.Windows.Forms.DialogResult.OK) 
                return;

            foreach (var fileName in openFileDialog.FileNames)
            {
                await ImportModpack(fileName);
            }
        }

        private async Task ImportFolder()
        {
            if (!XivCache.GameWriteEnabled && UserTransaction == null)
            {
                this.ShowError("Mod Safety Error", "Cannot import modpacks in SAFE mode outside of Transaction.");
                return;
            }


            var ofd = new BetterFolderBrowser {
                Title = "Import FFXIV Folder Tree"
            };

            if (ofd.ShowDialog() != System.Windows.Forms.DialogResult.OK)
                return;

            try
            {
                // See if we can get the information in simple mode.
                var modPackFiles = await TTMP.ModPackToSimpleFileList(ofd.SelectedPath, false, MainWindow.UserTransaction);

                if (modPackFiles != null)
                {
                    FileListImporter.ShowModpackImport(ofd.SelectedPath, modPackFiles.Keys.ToList(), this);
                    return;
                }
            } catch(Exception ex)
            {
                ViewHelpers.ShowError(this, "Folder Import Error", "An error occurred while importing the folder:\n\n" + ex.Message);
            }
        }

        /// <summary>
        /// This method opens the modpack import wizard or imports a modpack silently
        /// </summary>
        /// <param name="path">The path to the modpack</param>
        /// <param name="silent">If the modpack wizard should be shown or the modpack should just be imported without any user interaction</param>
        /// <returns></returns>
        private async Task ImportModpack(string path)
        {
            try
            {
                var modpackType = TTMP.GetModpackType(path);
                if(modpackType == TTMP.EModpackType.Invalid)
                {
                    throw new Exception("Modpack was not a valid PMP or TTMP file, or cannot be read on this version of TexTools.");
                }

                if (modpackType == TTMP.EModpackType.TtmpBackup)
                {
                    // TexTools backup modpack.
                    var mpl = await TTMP.GetModpackList(path);
                    var backupImport = new BackupModPackImporter(new DirectoryInfo(path), mpl, false);

                    backupImport.WindowStartupLocation = WindowStartupLocation.CenterScreen;
                    backupImport.Owner = this;

                    var result = backupImport.ShowDialog();
                    return;
                }

                // See if we can get the information in simple mode.
                var modPackFiles = await TTMP.ModPackToSimpleFileList(path, false, MainWindow.UserTransaction);

                if(modPackFiles != null)
                {
                    FileListImporter.ShowModpackImport(path, modPackFiles.Keys.ToList(), this);
                    return;
                }

                if (modpackType == TTMP.EModpackType.TtmpWizard || modpackType == TTMP.EModpackType.Pmp)
                {
                    // Multi-Option PMP/TTMP
                    await ImportWizardWindow.ImportModpack(path, this);
                }
            }
            catch (Exception ex)
            {
                FlexibleMessageBox.Show(string.Format(UIMessages.ModPackImportErrorMessage, path, ex.Message), UIMessages.ModPackImportErrorTitle, MessageBoxButtons.OK, MessageBoxIcon.Error);                  
            }
        }

        /// <summary>
        /// Event handler for the simple mod pack menu item clicked
        /// </summary>
        private async void Menu_MakeSimpleModpack_Click(object sender, RoutedEventArgs e)
        {
            NotifyUnsaved();
            FileListExporter.ShowModpackExport();
        }

        private async void Menu_RebuildCache_Click(object sender, RoutedEventArgs e)
        {
            var r = FlexibleMessageBox.Show("This will rebuild the TexTools cache.\nThis may take up to 5 minutes if you have many mods installed.".L(), "Cache Rebuild Confirmation".L(), MessageBoxButtons.OKCancel, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button1);
            if (r == System.Windows.Forms.DialogResult.OK)
            {
                await LockUi("Rebuilding Cache".L());
                try
                {
                    await Task.Run(() =>
                    {
                        XivCache.RebuildCache(XivCache.CacheVersion);
                    });

                    CustomizeViewModel.UpdateCacheSettings();
                } catch(Exception ex)
                {
                    while(ex.InnerException != null)
                    {
                        ex = ex.InnerException;
                    }

                    FlexibleMessageBox.Show("Unable to rebuild cache file.\n\nError:".L() + ex.Message, "Cache Rebuild Error".L(), MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                await UnlockUi();
                await RefreshTree(this);
            }
        }
        private async void Menu_ScanForSets_Click(object sender, RoutedEventArgs e)
        {
            var r = FlexibleMessageBox.Show("This will scan the entire FFXIV file system for new item sets.\n\nThis operation can take up to an hour.\nAre you sure you wish to proceed?.".L(), "Set Scan Confirmation".L(), MessageBoxButtons.OKCancel, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button1);
            if (r == System.Windows.Forms.DialogResult.OK)
            {
                await LockUi("Scanning for new Item Sets".L(), "This can take up to roughly an hour, depending on computer specs.".L());

                try
                {
                    await Task.Run(XivCache.RebuildAllRoots);
                } catch(Exception ex)
                {
                    FlexibleMessageBox.Show( "An error occured while trying to scan for new item sets.\n\n".L() + ex.Message, "Item Scan Error".L(), MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                await UnlockUi();
                await RefreshTree();
            }
        }
        private async void Menu_LoadSets_Click(object sender, RoutedEventArgs e)
        {
            using (OpenFileDialog openFileDialog = new OpenFileDialog())
            {
                openFileDialog.Filter = "db files|*.db";
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

                    FlexibleMessageBox.Show("Item Sets loaded.\nRestarting TexTools.".L(), "TexTools Restarting".L(), MessageBoxButtons.OK);
                    Restart();
                }
            }
        }

        private void CloseAllViewers()
        {
            var fileWindows = SimpleFileViewWindow.OpenFileWindows.ToList();
            foreach (var wind in fileWindows)
            {
                wind._IgnoreUnsaved = true;
                wind.Close();
            }
            var itemWindows = SimpleItemViewWindow.OpenItemWindows.ToList();
            foreach(var wind in itemWindows)
            {
                wind._IgnoreUnsaved = true;
                wind.Close();
            }
        }

        public bool PromptUnsavedAllViewers()
        {
            var fileWindows = SimpleFileViewWindow.OpenFileWindows.ToList();
            foreach (var wind in fileWindows)
            {
                if (!wind.FileWrapper.HandleUnsaveConfirmation())
                {
                    return false;
                }
            }

            var itemWindows = SimpleItemViewWindow.OpenItemWindows.ToList();
            foreach (var wind in itemWindows)
            {
                if(!wind.ItemView.HandleUnsaveConfirmation(null, null))
                {
                    return false;
                }
            }

            if (!ItemView.HandleUnsaveConfirmation(null, null))
            {
                return false;
            }
            return true;
        }

        public void NotifyUnsaved()
        {
            if (AnyUnsavedChanges())
            {
                ViewHelpers.ShowWarning(this, "Unsaved Changes Warning", "You have one or more unsaved changes that you may wish to save before proceeding");
            }
        }

        public bool AnyUnsavedChanges()
        {
            var anyUnsaved = false;
            var fileWindows = SimpleFileViewWindow.OpenFileWindows.ToList();
            foreach (var wind in fileWindows)
            {
                if (wind.FileWrapper.UnsavedChanges)
                {
                    anyUnsaved = true;
                }
            }

            var itemWindows = SimpleItemViewWindow.OpenItemWindows.ToList();
            foreach (var wind in itemWindows)
            {
                if (wind.ItemView.UnsavedChanges)
                {
                    anyUnsaved = true;
                }
            }

            if (ItemView.UnsavedChanges)
            {
                anyUnsaved = true;
            }

            return anyUnsaved;
        }


        /// <summary>
        /// Event handler for the start over menu item clicked
        /// </summary>
        private async void Menu_StartOver_Click(object sender, RoutedEventArgs e)
        {
            if(!XivCache.GameWriteEnabled)
            {
                FlexibleMessageBox.Show("Cannot perform Start Over while FFXIV file writing is disabled.".L(), "Permissions Error".L(), MessageBoxButtons.OK, MessageBoxIcon.Error);

                return;
            }
            try
            {

                var result = FlexibleMessageBox.Show(UIMessages.StartOverMessage, UIMessages.StartOverTitle, MessageBoxButtons.YesNo, MessageBoxIcon.Information);

                if (result == System.Windows.Forms.DialogResult.Yes)
                {
                    CloseAllViewers();


                    var indexBackupsDirectory = new DirectoryInfo(Settings.Default.Backup_Directory);

                    if (!Directory.Exists(indexBackupsDirectory.FullName))
                    {
                        FlexibleMessageBox.Show(UIMessages.BackupFolderAccessErrorMessage,
                            UIMessages.IndexBackupsErrorTitle, MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }


                    await LockUi(UIStrings.Start_Over, UIMessages.PleaseStandByMessage, this);

                    try
                    {
                        MakeHighlander();

                        try
                        {
                            await ProblemChecker.ResetAllGameFiles(indexBackupsDirectory, _lockProgress);
                            CustomizeViewModel.UpdateCacheSettings();
                        }
                        catch (Exception ex)
                        {
                            while (ex.InnerException != null)
                            {
                                ex = ex.InnerException;
                            }

                            var msg = UIMessages.StartOverErrorMessage + "\n\nError: ".L() + ex.Message;
                            FlexibleMessageBox.Show(msg,
                                UIMessages.StartOverErrorTitle, MessageBoxButtons.OK, MessageBoxIcon.Error);
                            await UnlockUi();
                            return;
                        }
                    }
                    finally
                    {
                        await UnlockUi();

                        await RefreshTree(this);
                    }



                    var item = ItemSelect.SelectedItem;
                    if (item != null)
                    {
                        await UpdateViews(item);
                    }

                    await this.ShowMessageAsync(UIMessages.StartOverCompleteTitle, UIMessages.StartOverCompleteMessage);
                }
            } catch(Exception ex)
            {
                this.ShowError(UIMessages.StartOverErrorTitle, "An unhandled error occurred when Starting Over:\n\n" + ex.Message);
            }
        }

        private void Menu_Donate_Click(object sender, RoutedEventArgs e)
        {
            //System.Diagnostics.Process.Start(WebUrl.FFXIV_Donate);
        }

        private async void Menu_Backup_Click(object sender, RoutedEventArgs e)
        {
            var result = FlexibleMessageBox.Show(UIMessages.CreateBackupsMessage, UIMessages.CreateBackupsTitle, MessageBoxButtons.YesNo, MessageBoxIcon.Information);

            if (result == System.Windows.Forms.DialogResult.Yes)
            {
                var gameDirectory = new DirectoryInfo(Settings.Default.FFXIV_Directory);
                var backupsDirectory = new DirectoryInfo(Properties.Settings.Default.Backup_Directory);
                await LockUi("Backing Up Indexes".L(), "Please wait...".L());
                try
                {
                    await ProblemChecker.CreateIndexBackups(backupsDirectory.FullName);
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

        private async void MetroWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {

            var res = ItemView.HandleUnsaveConfirmation(null, null);
            if (res == false)
            {
                e.Cancel = true;
                return;
            }

            if(UserTransaction != null && UserTransaction.State != ETransactionState.Closed)
            {
                if (UserTransaction.ModifiedFiles.Count > 0)
                {
                    ViewHelpers.ShowConfirmation(this, "Unsaved Transaction Confirmation", "You have an open transaction, are you sure you wish to close TexTools?\n\nAny un-commited changes will be lost.");
                }
            }

            if (UserTransaction != null)
            {
                try
                {
                    ModTransaction.CancelTransaction(UserTransaction, true);
                }
                catch (Exception ex)
                {
                    Trace.WriteLine(ex);
                }
            }

            // Probably not necessary given the entire application is closing typically, but who knows, maybe one day we'll spawn multiple mainwindows..?
            ItemView.Dispose();

            try
            {
                XivCache.CacheWorkerEnabled = false;
            }
            catch
            {
                //No-Op
            }
            return;
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
            FullModelView.ShowFmv();
        }


        private async void Menu_WebBackups_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                await DownloadIndexBackups();
            }
            catch(Exception ex)
            {
                Trace.WriteLine(ex);
            }
        }
        private async Task DownloadIndexBackups()
        {
            var url = UIStrings.Index_Backups_Url;
            if (url == "NONE" || String.IsNullOrWhiteSpace(url))
            {
                FlexibleMessageBox.Show("Index backup download is not currently supported for your client language.".L(), "Web Download Error".L(), MessageBoxButtons.OK, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button1);
                return;
            }

            var result = FlexibleMessageBox.Show("This will download index backups from the internet. Proceed?".L(), "Web Download Confirmation".L(),MessageBoxButtons.OKCancel, MessageBoxIcon.Information, MessageBoxDefaultButton.Button2);

            if (result != System.Windows.Forms.DialogResult.OK) return;

            await LockUi("Downloading Backups".L());
            string localPath = null;
            try
            {
                await Task.Run(async () =>
                {
                    var tempDir = Path.GetTempPath();
                    tempDir += "/index_backup";
                    Directory.CreateDirectory(tempDir);

                    _lockProgress.Report("Downloading Indexes...".L());
                    localPath = Path.GetTempFileName();
                    using (var client = new WebClient())
                    {
                        client.DownloadFile(url, localPath);
                    }


                    var tempDi = new DirectoryInfo(tempDir);
                    foreach (FileInfo file in tempDi.GetFiles())
                    {
                        file.Delete();
                    }


                    _lockProgress.Report("Unzipping new Indexes...".L());
                    ZipFile.ExtractToDirectory(localPath, tempDir);

                    _lockProgress.Report("Checking downloaded index version...".L());
                    var versionRaw = File.ReadAllText(tempDir + "/ffxivgame.ver");
                    
                    Version version = new Version(versionRaw.Substring(0, versionRaw.LastIndexOf(".", StringComparison.Ordinal)));

                    if (version != XivCache.GameInfo.GameVersion)
                    {
                        throw new Exception("Downloaded Index version does not match game version.".L());
                    }

                    _lockProgress.Report("Removing old Backups...".L());
                    var backupDir = new DirectoryInfo(Settings.Default.Backup_Directory);
                    foreach (FileInfo file in backupDir.GetFiles())
                    {
                        file.Delete();
                    }


                    _lockProgress.Report("Copying new indexes to backup directory...".L());
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
                            throw new Exception("Failed to copy index files.\n\n".L() + ex.Message);
                        }
                    }


                    _lockProgress.Report("Job Done.".L());
                });
                FlexibleMessageBox.Show("Successfully downloaded fresh index backups.\nYou may now use [Start Over] to apply them, if desired.".L(), "Backup Download Success".L(), MessageBoxButtons.OK, MessageBoxIcon.Information, MessageBoxDefaultButton.Button1);
            }
            catch(Exception Ex)
            {
                FlexibleMessageBox.Show("Unable to download Index Backups.\n\n".L() + Ex.Message, "Web Download Error".L(), MessageBoxButtons.OK, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button1);

            }
            finally
            {
                if(localPath != null)
                {
                    File.Delete(localPath);
                }
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

            _lockProgress.Report(data.Message.L() + $" ({data.Count}/{data.Total})");
        }
        private async void Menu_CleanUpModList_Click(object sender, RoutedEventArgs e)
        {
            var result = FlexibleMessageBox.Show("This will update the Modlist to ensure all modded files are\nlabeled under the correct items.\n\nThis may take up to 5 minutes to complete.".L(), "Modlist Cleanup Confirmation".L(), MessageBoxButtons.OKCancel, MessageBoxIcon.Information);

            if (result != System.Windows.Forms.DialogResult.OK) return;

            await LockUi("Cleaning up Modlist".L());
            try
            {
                Progress<(int Count, int Total, string Message)> reporter = new Progress<(int Count, int Total, string Message)>(ReportNumericProgress);
                // Run in new thread so UI doesn't lock.
                await Task.Run(async () =>
                {
                    await Modding.CleanUpModlistItems(reporter, MainWindow.UserTransaction);
                });
                FlexibleMessageBox.Show("The Modlist was cleaned up successfully.".L(), "Cleanup Complete".L(), MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch(Exception ex)
            {
                FlexibleMessageBox.Show("An error occurred during the cleanup process.\n\nError: ".L() + ex.Message, "Cleanup Error".L(), MessageBoxButtons.OK, MessageBoxIcon.Error);
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
            var result = FlexibleMessageBox.Show("This will recover unused space in the game files by defragmenting the modded DAT files.\n\nPlease do not close TexTools or open FFXIV until this operation is complete".L(), "Recover Space Confirmation".L(), MessageBoxButtons.OKCancel, MessageBoxIcon.Information);

            if (result != System.Windows.Forms.DialogResult.OK) return;
            await LockUi("Defragmenting DAT Files".L());
            try
            {
                long savedBytes = 0;
                Progress<(int Count, int Total, string Message)> reporter = new Progress<(int Count, int Total, string Message)>(ReportNumericProgress);
                // Run in new thread so UI doesn't lock.
                await Task.Run(async () =>
                {
                    savedBytes = await Dat.DefragmentModdedDats(reporter);
                });

                var savedSpace = FormatBytes(savedBytes);
                FlexibleMessageBox.Show($"DAT File Defragmentation completed successfully.\n\n{savedSpace._()} of unused space has been recovered.".L(), "Defragmentation Complete".L(), MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                FlexibleMessageBox.Show("An error occurred during the defragmentation process.\n\nError: ".L() + ex.Message, "Modlist Defragmentation Error".L(), MessageBoxButtons.OK, MessageBoxIcon.Error);
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

        private void Menu_MergeModels_Click(object sender, RoutedEventArgs e)
        {
            var wind = new MergeModelsDialog() { Owner = this };
            wind.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            wind.Show();
        }


        private void Menu_RacialScaling_Click(object sender, RoutedEventArgs e)
        {
            var wind = new RacialSettingsEditor() { Owner = this };
            wind.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            wind.Show();
        }

        private async void ScanShaders_Click(object sender, RoutedEventArgs e)
        {

            await LockUi("Updating Shader References...".L(), "This may take up to 5 minutes depending on your computer specs...".L(), this);
            try
            {
                await Task.Run(async () => {

#if ENDWALKER
                    await Mtrl.UpdateShaderDB(false);
#else
                    // TODO - This should be [false] when switching off the benchmark install.
                    // ( Controls whether it uses Index 1 or Index 2 for the search )
                    await Mtrl.UpdateShaderDB(true);
#endif
                    await ShaderHelpers.LoadShaderInfo();
                });
            } catch(Exception ex)
            {
                FlexibleMessageBox.Show("An error occurred durin the shader update process.\n\nError: ".L() + ex.Message, "Shader Update Error".L(), MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                await UnlockUi(this);
            }
        }


        private void ModTransaction_ActiveTransactionBlocked(ModTransaction sender)
        {
            var result = FlexibleMessageBox.Show("The current action in TexTools is blocked due to the game files currently being in use.\n\nYou may cancel the action by pressing CANCEL, or wait for the files to become accessible by pressing OK.", "Transaction Blocked", MessageBoxButtons.OKCancel, MessageBoxIcon.Information);

            if(result == System.Windows.Forms.DialogResult.Cancel)
            {
                ModTransaction.CancelBlockedTransaction();
            }

        }

        private async void FileViewerButton_Click(object sender, RoutedEventArgs e)
        {
            var success = await SimpleFileViewWindow.OpenFile();
        }

        private void TransactionStatus_Click(object sender, RoutedEventArgs e)
        {
            if (ProjectWindow.Project != null)
            {
                ProjectWindow.ShowProjectWindow();
            }
            else
            {
                TransactionStatusWindow.ShowTxStatus();
            }
        }

        private void SafeToggle_Click(object sender, RoutedEventArgs e)
        {
            XivCache.GameWriteEnabled = !XivCache.GameWriteEnabled;
        }
        private void XivCache_GameWriteStateChanged(bool newState)
        {
            UpdateWriteStateUi();
        }
        private void UpdateWriteStateUi()
        {
            Dispatcher.InvokeAsync(() =>
            {
                SafeToggleButton.Content = XivCache.GameWriteEnabled ? "UNSAFE".L() : "SAFE".L();
                SafeToggleButton.Foreground = XivCache.GameWriteEnabled ? Brushes.DarkRed : Brushes.DarkGreen;
            });
        }

        private void Menu_ProjectManager_Click(object sender, RoutedEventArgs e)
        {
            ProjectWindow.ShowProjectWindow();
        }

        private void ItemViewer_Click(object sender, RoutedEventArgs e)
        {
            var item = PopupItemSelection.ShowItemSelection(null, null, this);
            if (item != null)
            {
                _ = SimpleItemViewWindow.ShowItem(item, this);
            }
        }

        private void Menu_ImportFolder_Click(object sender, RoutedEventArgs e)
        {
            _ = ImportFolder();
        }
    }
}
