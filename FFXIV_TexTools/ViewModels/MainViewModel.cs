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
using xivModdingFramework.Items.Categories;
using xivModdingFramework.Models.FileTypes;
using xivModdingFramework.Models.DataContainers;
using xivModdingFramework.Items.Interfaces;
using xivModdingFramework.Items.DataContainers;
using System.Text.RegularExpressions;
using xivModdingFramework.Variants.FileTypes;
using System.Text; // For Imc

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


            await _mainWindow.LockUi("Performing Post-Patch Maintenence".L(), "This may take a few minutes if you have many mods installed.".L(), this);

            var readonlyTx = ModTransaction.BeginReadonlyTransaction();
            if ((await readonlyTx.GetModList()).Mods.Count == 0)
            {
                // No mods.  Just create backups and move on with our life.
                await BackupIndexFiles();
                await _mainWindow.UnlockUi();
                return;
            }
            MainWindow.MakeHighlander();

            var originalWriteSetting = XivCache.GameWriteEnabled;
            XivCache.GameWriteEnabled = true;

            var workerStatus = XivCache.CacheWorkerEnabled;

            await XivCache.SetCacheWorkerState(false);
            try
            {
                // Cache our currently enabled stuff.
                using (var tx = await ModTransaction.BeginTransaction(true))
                {
                    var modList = await tx.GetModList();
                    var allMods = modList.GetMods().ToList();

                    var anyChanges = false;
                    foreach(var mod in allMods)
                    {
                        var state = await mod.GetState(tx);

                        if(state != EModState.Invalid)
                        {
                            // Mod is fine.  Can continue on as normal.
                            continue;
                        }
                        anyChanges = true;

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
                            Dat.AssertOriginalOffsetIsSafe(mod.DataFile, currentOffset);
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

                    if (anyChanges)
                    {
                        // We now have a working, valid modlist.  Nice.
                        // Make some fresh backups.
                        await ModTransaction.CommitTransaction(tx);
                    } else
                    {
                        await ModTransaction.CancelTransaction(tx, true);
                    }

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
                await XivCache.SetCacheWorkerState(workerStatus);
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

        #region Batch Operations
        public ICommand BatchExportHousingIndoorFurnitureCommand => new RelayCommand(BatchExportHousingIndoorFurniture);
        #endregion

        /// <summary>
        /// Enables all mods in the mod list
        /// </summary>
        /// <param name="obj"></param>
        private async void EnableAllMods(object obj)
        {
            if (!MainWindow.GetMainWindow().CheckFileWrite())
            {
                return;
            }

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
            if (!MainWindow.GetMainWindow().CheckFileWrite())
            {
                return;
            }

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

        private async void BatchExportHousingIndoorFurniture(object obj)
        {
            var folderDialog = new FolderSelectDialog
            {
                Title = "Select Base Export Directory for Housing Furniture",
                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
            };

            bool? dialogResult;
            if (System.Windows.Application.Current.Dispatcher.CheckAccess())
            {
                 dialogResult = folderDialog.ShowDialog(new WindowInteropHelper(_mainWindow).Handle);
            }
            else
            {
                dialogResult = await System.Windows.Application.Current.Dispatcher.InvokeAsync(() => folderDialog.ShowDialog(new WindowInteropHelper(_mainWindow).Handle));
            }


            if (dialogResult != true)
            {
                await _mainWindow.ShowMessageAsync("Operation Cancelled", "Batch export was cancelled by the user.");
                return;
            }
            string userSelectedBaseDir = folderDialog.FileName;

            ProgressDialogController progressController = null;
            var _BatchExportErrors = new List<string>();

            try
            {
                progressController = await _mainWindow.ShowProgressAsync("Batch Exporting Furniture...", "Starting operation...");
                progressController.SetIndeterminate();

                var housingCategoryProvider = new Housing();

				const string IndoorFurnitureCategoryName = "Indoor Furniture";
				List<IItemModel> allFurniture = await housingCategoryProvider.GetUncachedFurnitureList(MainWindow.DefaultTransaction);
				List<IItemModel> itemsToExport = allFurniture.Where(item =>
					item is XivFurniture furniture &&
					string.Equals(furniture.SecondaryCategory, IndoorFurnitureCategoryName, StringComparison.OrdinalIgnoreCase)).ToList();
				if (itemsToExport == null || !itemsToExport.Any())
                {
                    if (progressController.IsOpen) await progressController.CloseAsync();
                    await _mainWindow.ShowMessageAsync("No Items Found", "No indoor furniture items were found to export.");
                    return;
                }

                progressController.SetCancelable(true);
                double currentItemCount = 0;
                double totalItemCount = itemsToExport.Count;
                progressController.SetProgress(0);
                progressController.SetMessage("Preparing to export " + totalItemCount + " items...");


                await Task.Run(async () => // Run the loop on a background thread
                {
                    foreach (IItemModel item in itemsToExport)
                    {
                        if (progressController.IsCanceled) break;
                        currentItemCount++;
                        string currentItemName = item.Name ?? "UnknownItem";

						// Dispatcher needed for progress updates to UI thread
						await System.Windows.Application.Current.Dispatcher.InvokeAsync(() => {
                            progressController.SetMessage($"Processing item {currentItemCount} of {totalItemCount}: {currentItemName}");
                            progressController.SetProgress(currentItemCount / totalItemCount);
                        });

                        try
                        {
                            XivDependencyRoot root = item.GetRoot();
                            if (root == null)
                            {
                                _BatchExportErrors.Add($"Skipped item (no XivDependencyRoot): {currentItemName}");
                                Trace.WriteLine($"Skipped item (no XivDependencyRoot): {currentItemName}");
                                continue;
                            }

                            List<string> modelPaths = await root.GetModelFiles(MainWindow.DefaultTransaction);
                            if (modelPaths == null || !modelPaths.Any())
                            {
                                _BatchExportErrors.Add($"Skipped item (no model paths found via root.GetModelFiles): {currentItemName}");
                                Trace.WriteLine($"Skipped item (no model paths found via root.GetModelFiles): {currentItemName}");
                                continue;
                            }

                            foreach (string modelPath in modelPaths)
                            {
                                if (string.IsNullOrEmpty(modelPath))
                                {
                                    _BatchExportErrors.Add($"Skipped model (empty path) for item: {currentItemName}");
                                    Trace.WriteLine($"Skipped model (empty path) for item: {currentItemName}");
                                    continue;
                                }

                                TTModel ttModel = await Mdl.GetTTModel(modelPath, false, MainWindow.DefaultTransaction);
                                if (ttModel == null)
                                {
                                    _BatchExportErrors.Add($"Skipped model (TTModel load failed for {modelPath}): {currentItemName}");
                                    Trace.WriteLine($"Skipped model (TTModel load failed for {modelPath}): {currentItemName}");
                                    continue;
                                }

                                var version = 1;
                                if (root != null && Imc.UsesImc(root))
                                {
                                    version = await Imc.GetMaterialSetId(item, false, MainWindow.DefaultTransaction);
                                }

                                var modelExportSettings = new ModelExportSettings()
                            {
                                IncludeTextures = true,
                                ShiftUVs = false,
                                PbrTextures = false
                            };


                                string itemNameSafe = SanitizePath(currentItemName);
                                // Use current modelPath for the filename, not ttModel.Source, to be certain
                                string modelFileName = Path.GetFileNameWithoutExtension(modelPath);
                                string modelFileNameSafe = SanitizePath(modelFileName + ".fbx");

                                string itemExportDir = Path.Combine(userSelectedBaseDir, "BatchExport", "IndoorFurniture", itemNameSafe);
                                Directory.CreateDirectory(itemExportDir);
                                string exportPath = Path.Combine(itemExportDir, modelFileNameSafe);

                                // This UI update should be fine here as it's within the Task.Run but dispatched.
                                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() => {
									progressController.SetMessage($"Exporting: {itemNameSafe} ({currentItemCount}/{totalItemCount})");
								});
                                Trace.WriteLine($"Exporting model {modelPath} for item {itemNameSafe} to {exportPath}");

                                await Mdl.ExportTTModelToFile(ttModel, exportPath, version, modelExportSettings, MainWindow.DefaultTransaction);
                            } // End foreach modelPath
                        }
                        catch (Exception ex)
                        {
                            // This catch block is for errors during GetRoot, GetModelFiles, or general item processing before individual model export.
                            _BatchExportErrors.Add($"Error processing item {currentItemName} (before model loop or if model loop itself failed): {ex.Message}");
                            Trace.WriteLine($"Error processing item {currentItemName}: {ex.Message} - {ex.StackTrace}");
                        }
                    } // End foreach item
                }); // End Task.Run

                if (progressController.IsOpen) await progressController.CloseAsync(); // Ensure it's closed if open

                if (_BatchExportErrors.Any())
                {
                    StringBuilder sb = new StringBuilder();
                    sb.AppendLine("Batch export completed with some errors:");
                    foreach (string error in _BatchExportErrors.Take(20))
                    {
                        sb.AppendLine($"- {error}");
                    }
                    if (_BatchExportErrors.Count > 20)
                    {
                        sb.AppendLine($"...and {_BatchExportErrors.Count - 20} more errors.");
                    }
                    await _mainWindow.ShowMessageAsync("Export Completed with Errors", sb.ToString());
                }
                else if (progressController.IsCanceled)
                {
                     await _mainWindow.ShowMessageAsync("Operation Cancelled", "Batch export was cancelled by the user.");
                }
                else
                {
                    await _mainWindow.ShowMessageAsync("Export Complete", $"Successfully exported {Convert.ToInt32(totalItemCount - _BatchExportErrors.Count)} items to {Path.Combine(userSelectedBaseDir, "TexTools", "Saved", "Indoor Furniture")}");
                }
            }
            catch (Exception ex)
            {
                if (progressController != null && progressController.IsOpen) await progressController.CloseAsync();
                await _mainWindow.ShowMessageAsync("Batch Export Error", $"An unexpected error occurred: {ex.Message}");
                Trace.WriteLine($"Batch Export General Error: {ex.Message} - {ex.StackTrace}");
            }
            finally
            {
                 if (progressController != null && progressController.IsOpen)
                 {
                     await progressController.CloseAsync();
                 }
            }
        }

        private static string SanitizePath(string name)
        {
             if (string.IsNullOrEmpty(name)) return "Unnamed";
             string invalidChars = Regex.Escape(new string(Path.GetInvalidFileNameChars()) + new string(Path.GetInvalidPathChars()));
             string invalidRegStr = string.Format(@"([{0}]*\.+$)|([{0}]+)", invalidChars);
             string sanitized = Regex.Replace(name, invalidRegStr, "_");
             return sanitized.Length > 100 ? sanitized.Substring(0, 100) : sanitized; // Limit length
        }
    }
}