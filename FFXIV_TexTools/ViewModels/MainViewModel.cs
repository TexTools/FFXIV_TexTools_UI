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
using FFXIV_TexTools.Properties;
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
using xivModdingFramework.Cache;
using FFXIV_TexTools.Views;
using xivModdingFramework.Mods.DataContainers;
using xivModdingFramework.SqPack.DataContainers;

using System.Drawing.Imaging;
using xivModdingFramework.Mods.Enums;
using System.ComponentModel.Composition.Primitives;
using AutoUpdaterDotNET;
using System.Windows.Threading;
using System.Windows.Media;

namespace FFXIV_TexTools.ViewModels
{
    public class MainViewModel : INotifyPropertyChanged
    {

        private string _TxStatusText;
        public string TxStatusText
        {
            get => _TxStatusText;
            set
            {
                _TxStatusText = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(TxStatusText)));
            }
        }

        private Brush _TxStatusBrush;
        public Brush TxStatusBrush
        {
            get => _TxStatusBrush;
            set
            {
                _TxStatusBrush = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(TxStatusBrush)));
            }
        }

        private readonly MainWindow _mainWindow;

        private ObservableCollection<Category> _categories = new ObservableCollection<Category>();

        private string _searchText, _progressLabel;
        private string _dxVersionText = $"DX: {Properties.Settings.Default.DX_Version}";
        private int _progressValue;
        private Visibility _progressBarVisible, _progressLabelVisible;
        private ProgressDialogController _progressController;

        public System.Timers.Timer CacheTimer = new System.Timers.Timer(3000);

        private const string WarningIdentifier = "!!";

        public MainViewModel(MainWindow mainWindow)
        {
            _mainWindow = mainWindow;
            // This is actually synchronous and can just be called immediately...
            SetDirectories();

            if (ProgressLabel == null)
            {
                ProgressLabel = "";
            }


            CacheTimer.Elapsed += UpdateDependencyQueueCount;

            UpdateTxState(MainWindow.UserTransaction == null ? ETransactionState.Closed : MainWindow.UserTransaction.State);
            TxWatcher.UserTxStateChanged += TxStateChanged;
        }

        public async void UpdateDependencyQueueCount(object sender, System.Timers.ElapsedEventArgs e)
        {
            if (XivCache.CacheWorkerEnabled)
            {
                var count = 0;
                if (count > 0)
                {
                    //_mainWindow.ShowStatusMessage($"Queue Length: {count._()}".L());

                    // Removed localization on this because the localization is throwing an error for some reason(?)
                    _mainWindow.ShowStatusMessage($"Queue Length: {count}");
                }
            } else
            {

                _mainWindow.ShowStatusMessage($"Cache Worker Paused");
            }
        }

        /// <summary>
        /// Validates the various directories TexTools relies on.
        /// </summary>
        private void SetDirectories()
        {

            var resourceManager = CommonInstallDirectories.ResourceManager;
            var resourceSet = resourceManager.GetResourceSet(CultureInfo.CurrentCulture, true, true);

            if (!Properties.Settings.Default.FFXIV_Directory.EndsWith("ffxiv"))
            {
                Properties.Settings.Default.FFXIV_Directory = "";
            }

            if (Settings.Default.FFXIV_Directory == "") 
            { 
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
                        while (!installDirectory.EndsWith("ffxiv"))
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

            // Create/assign directories if they don't exist already.
            SetSaveDirectory();

            SetBackupsDirectory();

            SetModPackDirectory();
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

        private uint GetExpectedType(string file)
        {
            if(file.EndsWith(".tex"))
            {
                return 4;
            } else if (file.EndsWith(".mdl"))
            {
                return 3;
            }
            else
            {
                return 2;
            }
        }

        private async Task<bool> CheckFile(ModTransaction tx, string file, long offset)
        {
            try
            {
                var expected = GetExpectedType(file);
                var df = IOUtil.GetDataFileFromPath(file);
                var validTypes = new List<uint>() { 2, 3, 4 };
                using (var br = await tx.GetFileStream(df, offset, true))
                {
                    // Type Check
                    var type = Dat.GetSqPackType(br);
                    if(type != expected)
                    {
                        return false;
                    }

                    // Decompression Check
                    await tx.ReadFile(df, offset, false);

                    // If we got this far, the file is valid enough to pass our check.
                    return true;
                }
            }
            catch
            {
                return false;
            }


        }

        /// <summary>
        /// Performs post-patch modlist corrections and validation, prompting user also to generate backups after a successful completion.
        /// </summary>
        /// <returns></returns>
        public async Task DoPostPatchCleanup()
        {

            FlexibleMessageBox.Show(_mainWindow.Win32Window, UIMessages.PatchDetectedMessage, "Post Patch Cleanup Starting".L(), MessageBoxButtons.OK, MessageBoxIcon.Information, MessageBoxDefaultButton.Button1);
            MainWindow.MakeHighlander();


            await _mainWindow.LockUi("Performing Post-Patch Maintenence".L(), "This may take a few minutes if you have many mods installed.".L(), this);

            var readonlyTx = ModTransaction.BeginTransaction();
            if ((await readonlyTx.GetModList()).Mods.Count == 0)
            {
                // No mods.  Just create backups and move on with our life.
                await BackupIndexFiles();
                await _mainWindow.UnlockUi();
                return;
            }

            var originalWriteSetting = XivCache.GameWriteEnabled;
            XivCache.GameWriteEnabled = true;

            var workerStatus = XivCache.CacheWorkerEnabled;
            XivCache.CacheWorkerEnabled = false;
            try
            {
                // Cache our currently enabled stuff.
                using (var tx = ModTransaction.BeginTransaction(true))
                {
                    var modList = await tx.GetModList();
                    var allMods = modList.GetMods().ToList();

                    foreach(var mod in allMods)
                    {
                        var state = await mod.GetState(tx);

                        if(state != EModState.Invalid)
                        {
                            // Mod is fine.  Can continue on as normal.
                            continue;
                        }

                        // An Invalid state mod points to Neither the original, nor the modded offset.
                        var df = IOUtil.GetDataFileFromPath(mod.FilePath);
                        var currentOffset = await tx.Get8xDataOffset(mod.FilePath);

                        var originalOk = await CheckFile(tx, mod.FilePath, mod.OriginalOffset8x);
                        var moddedOk = await CheckFile(tx, mod.FilePath, mod.ModOffset8x);
                        var currentOK = await CheckFile(tx, mod.FilePath, currentOffset);

                        if(originalOk && moddedOk && !currentOK)
                        {
                            // Mod is fine but current offset is bad.  Restore it to original.
                            await tx.Set8xDataOffset(mod.FilePath, mod.OriginalOffset8x);
                        } else if(moddedOk && currentOK && !originalOk)
                        {
                            // Original offset moved.  Just update the mod entry.
                            var m = mod;
                            m.OriginalOffset8x = currentOffset;
                            await tx.UpdateMod(mod, mod.FilePath);
                        } else if(currentOK && !moddedOk && !originalOk)
                        {
                            // Mod got blasted, but the base file seems fine.  Remove the mod entry.
                            await tx.RemoveMod(mod);
                        } else
                        {
                            // Any of these other states are indeterminate and unfixable.
                            throw new InvalidDataException("Offsets for one or more files are unrecoverable.  Please use Download Index Backups => Start Over.");
                        }

                        // Set to current value to ensure the index points to the same offset for both indexes.
                        await tx.Set8xDataOffset(mod.FilePath, await tx.Get8xDataOffset(mod.FilePath));
                    }

                    // We now have a working, valid modlist.  Nice.
                    // Make some fresh backups.
                    await ModTransaction.CommitTransaction(tx);

                }

                // Always create clean index backups after this process is completed.
                _mainWindow.LockProgress.Report("Creating fresh index backups...".L());
                await ProblemChecker.CreateIndexBackups(Settings.Default.Backup_Directory);

                FlexibleMessageBox.Show(_mainWindow.Win32Window, UIMessages.PostPatchComplete, "Post-Patch Process Complete".L(), MessageBoxButtons.OK, MessageBoxIcon.Information, MessageBoxDefaultButton.Button1);

            }
            catch(Exception Ex)
            {
                // Show the user the error, then let them go about their business of fixing things.
                FlexibleMessageBox.Show(_mainWindow.Win32Window, String.Format(UIMessages.PostPatchError, Ex.Message), "Post-Patch Failure".L(), MessageBoxButtons.OK, MessageBoxIcon.Information, MessageBoxDefaultButton.Button1);
            }
            finally
            {
                XivCache.CacheWorkerEnabled = workerStatus;
                XivCache.GameWriteEnabled = originalWriteSetting;
                await _mainWindow.UnlockUi(this);
            }
        }

        private async Task BackupIndexFiles()
        {
            _mainWindow.LockProgress?.Report("Creating Index Backups...".L());
            try
            {
                await ProblemChecker.CreateIndexBackups(Settings.Default.Backup_Directory);
            }
            catch(Exception ex)
            {
                ViewHelpers.ShowError("Index Backup Error", "Index backups were unabled to be created:\n\n" + ex.Message);
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
        public ICommand EnableAllModsCommand => new RelayCommand(EnableAllMods);
        public ICommand DisableAllModsCommand => new RelayCommand(DisableAllMods);


        /// <summary>
        /// Enables all mods in the mod list
        /// </summary>
        /// <param name="obj"></param>
        private async void EnableAllMods(object obj)
        {
            _progressController = await _mainWindow.ShowProgressAsync(UIMessages.EnablingModsTitle, UIMessages.PleaseWaitMessage);

            if (FlexibleMessageBox.Show(
                    UIMessages.EnableAllModsMessage, UIMessages.EnablingModsTitle, MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
            {
                bool err = false;
                try
                {
                    // Run on new thread so we don't block.
                    await Task.Run(async () =>
                    {
                        await Modding.SetAllModStates(EModState.Enabled, ViewHelpers.BindReportProgress(_progressController), MainWindow.UserTransaction);
                    });
                } catch(Exception ex)
                {
                    FlexibleMessageBox.Show("Failed to Enable all Mods: \n\nError:".L() + ex.Message, "Enable Mod Error".L(), MessageBoxButtons.OK, MessageBoxIcon.Error);
                    err = true;
                }

                await _progressController.CloseAsync();
                if (!err)
                {
                    await _mainWindow.ShowMessageAsync(UIMessages.SuccessTitle, UIMessages.ModsEnabledSuccessMessage);
                }
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
            _progressController = await _mainWindow.ShowProgressAsync(UIMessages.DisablingModsTitle, UIMessages.PleaseWaitMessage);

            if (FlexibleMessageBox.Show(
                    UIMessages.DisableAllModsMessage, UIMessages.DisableAllModsTitle, MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
            {
                bool err = false;
                try
                {
                    // Run on new thread so we don't block.
                    await Task.Run(async () =>
                    {
                        await Modding.SetAllModStates(EModState.Disabled, ViewHelpers.BindReportProgress(_progressController), MainWindow.UserTransaction);
                    });
                } catch (Exception ex)
                {
                    FlexibleMessageBox.Show("Failed to Disable all Mods: \n\nError:".L() + ex.Message, "Disable Mod Error".L(), MessageBoxButtons.OK, MessageBoxIcon.Error);
                    err = true;
                }

                await _progressController.CloseAsync();

                if (!err)
                {
                    await _mainWindow.ShowMessageAsync(UIMessages.SuccessTitle, UIMessages.ModsDisabledSuccessMessage);
                }
            }
            else
            {
                await _progressController.CloseAsync();
            }

        }

        #endregion


        private async void TxStateChanged(ETransactionState oldState, ETransactionState newState)
        {
            try
            {
                await await _mainWindow.Dispatcher.InvokeAsync(async () =>
                {
                    UpdateTxState(newState);
                });
            }
            catch (Exception ex)
            {
                Trace.WriteLine(ex);
            }
        }
        private void UpdateTxState(ETransactionState newState)
        {
            TxStatusText = newState.ToString();
            if(PenumbraAttachHandler.IsAttached && newState == ETransactionState.Open)
            {
                TxStatusText = "Penumbra Sync".L();
            }

            if (newState == ETransactionState.Open)
            {
                // TX is ready for writing.
                TxStatusBrush = Brushes.DarkGreen;
            }
            else if (newState == ETransactionState.Invalid || newState == ETransactionState.Closed)
            {
                // TX is closed.
                TxStatusBrush = Brushes.DarkGray;
            }
            else if (newState == ETransactionState.Preparing)
            {
                // TX is ready for prep-writing.
                TxStatusBrush = Brushes.DarkOrange;
            }
            else
            {
                // TX is working.
                TxStatusBrush = Brushes.DarkRed;
            }

        }


        public event PropertyChangedEventHandler PropertyChanged;

        [NotifyPropertyChangedInvocator]
        protected virtual void NotifyPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

    }
}