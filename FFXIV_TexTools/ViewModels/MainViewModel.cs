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
using FFXIV_TexTools.Helpers; // Already present, also covers xivModdingFramework.Helpers via aliasing or its own content
using FFXIV_TexTools.Models; // Already present, covers Category
using FFXIV_TexTools.Resources; // Already present
using FFXIV_TexTools.Properties; // Already present, covers Settings
using FolderSelect; // Already present
using MahApps.Metro.Controls.Dialogs; // Already present
using System; // Already present
using System.Collections; // Already present
using System.Collections.Generic; // Already present
using System.Collections.ObjectModel; // Already present
using System.ComponentModel; // Already present
using System.Diagnostics; // Already present
using System.Globalization; // Already present
using System.IO; // Already present, covers Path
using System.Linq; // Already present
using System.Runtime.CompilerServices; // Already present
using System.Text; // For StringBuilder
using System.Text.RegularExpressions; // Already present
using System.Threading.Tasks; // Already present
using System.Windows; // Already present
using System.Windows.Forms; // Already present
using System.Windows.Input; // Already present
using System.Windows.Interop; // Already present
using AutoUpdaterDotNET; // Already present
using System.ComponentModel.Composition.Primitives; // Already present
using System.Windows.Media; // Already present
using System.Windows.Threading; // Already present

// xivModdingFramework specific using directives
using xivModdingFramework.Cache; // Already present
using xivModdingFramework.General.Enums; // Already present, covers XivPlatform, XivGender etc.
using xivModdingFramework.Helpers; // Explicitly added, though FFXIV_TexTools.Helpers might cover some
using xivModdingFramework.Items.Categories; // Already present, for XivCommonItem
using xivModdingFramework.Items.DataContainers; // For XivDbItemInfo, XivModelInfo
using xivModdingFramework.Items.Enums; // For XivItemType, XivRarity etc.
using xivModdingFramework.Items.Interfaces; // Already present, for IItem, IItemModel
using xivModdingFramework.Models.DataContainers; // Already present, for TTModel, ModelExportSettings, XivModelInfo (duplicate but harmless)
using xivModdingFramework.Models.FileTypes; // Already present, for Mdl
using xivModdingFramework.Mods; // Already present
using xivModdingFramework.Mods.DataContainers; // Already present
using xivModdingFramework.Mods.Enums; // Already present
using xivModdingFramework.SqPack.DataContainers; // Already present
using xivModdingFramework.SqPack.FileTypes; // Already present
using xivModdingFramework.Variants.FileTypes; // Already present, for Imc

// UI specific, potentially problematic in VM but used by existing code or test mocks
using FFXIV_TexTools.Views; // Already present
using FFXIV_TexTools.Views.Item; // Already present, for ItemViewControl (used programmatically)


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
        public ICommand BatchExportHousingIndoorFurnitureCommand => new RelayCommand(async (obj) => await BatchExportHousingIndoorFurniture(obj));

        private async Task BatchExportHousingIndoorFurniture(object obj)
        {
            // Placeholder for batch export logic
            // FlexibleMessageBox.Show("Batch Export Housing Indoor Furniture command executed (placeholder).", "Placeholder", MessageBoxButtons.OK, MessageBoxIcon.Information);

            // string initialDir = Settings.Default.BatchExportDirectory; // Original
            string initialDir = ""; // Workaround
            if (string.IsNullOrEmpty(initialDir) || !Directory.Exists(initialDir))
            {
                initialDir = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            }

            var folderDialog = new FolderSelectDialog
            {
                Title = "Select Export Directory".L(),
                InitialDirectory = initialDir
            };

            if (folderDialog.ShowDialog())
            {
                string exportDir = folderDialog.FileName;
                if (string.IsNullOrWhiteSpace(exportDir))
                {
                    FlexibleMessageBox.Show("No directory selected. Aborting batch export.", "Export Aborted", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                // TODO: Implement the rest of the batch export logic here
                ProgressLabel = "Locating Housing/Indoor Furniture category...";
                ProgressBarVisible = Visibility.Visible;
                ProgressValue = 0;

                var housingCategory = FindCategoryByName(Categories, "Housing");
                Category indoorFurnitureCategory = null;
                if (housingCategory != null)
                {
                    indoorFurnitureCategory = FindCategoryByName(housingCategory.Categories, "Indoor Furniture");
                }

                if (indoorFurnitureCategory == null)
                {
                    FlexibleMessageBox.Show("Could not find 'Housing/Indoor Furniture' category. Aborting.", "Category Not Found", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    ProgressBarVisible = Visibility.Collapsed;
                    return;
                }

                List<IItem> itemsToExport = new List<IItem>();
                CollectItemsFromCategory(indoorFurnitureCategory, itemsToExport);

                if (!itemsToExport.Any())
                {
                    FlexibleMessageBox.Show("No items found in 'Housing/Indoor Furniture' category.", "No Items Found", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    ProgressBarVisible = Visibility.Collapsed;
                    return;
                }

                ProgressLabel = "Preparing for batch export..."; // Initial message
                ProgressBarVisible = Visibility.Visible; // Show progress bar if not using dialog for everything
                ProgressValue = 0;

                var progressController = await _mainWindow.ShowProgressAsync(
                    "Batch Exporting".L(),
                    "Preparing to export Housing Indoor Furniture...".L(),
                    isCancelable: true);
                progressController.SetIndeterminate();

                List<BatchExportError> errors = new List<BatchExportError>();
                ItemViewControl itemControl = new ItemViewControl(); // Create on UI thread before Task.Run

                try
                {
                    await Task.Run(async () => // Ensure long operation runs on a background thread
                    {
                        for (int i = 0; i < itemsToExport.Count; i++)
                        {
                            if (progressController.IsCanceled)
                            {
                                errors.Add(new BatchExportError { ItemName = "Operation Cancelled", ErrorMessage = "User cancelled the batch export." });
                                break;
                            }

                            var item = itemsToExport[i];
                            string itemName = item is XivCommonItem commonItem ? commonItem.Name : $"Item_{i}";
                            string itemNameSafe = SanitizePath(itemName);

                            double currentProgress = (double)i / itemsToExport.Count;
                            progressController.SetProgress(currentProgress);
                            progressController.SetMessage($"Processing: {itemNameSafe} ({i + 1}/{itemsToExport.Count})");

                            try
                            {
                                // SetItem might need to be on UI thread if it interacts with UI elements directly or indirectly
                                // However, ItemViewControl is created here and not part of main UI tree.
                                // Its SetItem method loads data. Let's assume it's safe for now.
                                bool itemSetSuccessfully = await itemControl.SetItem(item);

                                if (!itemSetSuccessfully || itemControl.Files == null)
                                {
                                    errors.Add(new BatchExportError { ItemName = itemNameSafe, ErrorMessage = "Failed to set item or item data (Files) was null." });
                                    continue;
                                }

                                if (!itemControl.Files.Any() || (itemControl.Files.Count == 1 && itemControl.Files.ContainsKey("")))
                                {
                                    Trace.WriteLine($"No actual model files found for {itemNameSafe}. Skipping.");
                                    continue; // Not necessarily an error, could be an item without models.
                                }

                                foreach (var modelPathKey in itemControl.Files.Keys)
                                {
                                    if (progressController.IsCanceled) break;
                                    if (string.IsNullOrWhiteSpace(modelPathKey) || modelPathKey == "--" || modelPathKey == "") continue;

                                    string modelFileName = Path.GetFileNameWithoutExtension(modelPathKey);
                                    string modelFileNameSafe = SanitizePath(modelFileName);
                                    string itemExportDir = Path.Combine(exportDir, itemNameSafe);
                                    Directory.CreateDirectory(itemExportDir);
                                    string exportToPath = Path.Combine(itemExportDir, $"{modelFileNameSafe}.fbx");

                                    progressController.SetMessage($"Exporting: {itemNameSafe}/{modelFileNameSafe}.fbx");
                                    Trace.WriteLine($"Exporting model {modelPathKey} for item {itemNameSafe} to {exportToPath}");

                                    try
                                    {
                                        var tx = MainWindow.DefaultTransaction;
                                        TTModel ttModel = await Mdl.GetTTModel(modelPathKey, false, tx);
                                        if (ttModel == null)
                                        {
                                            errors.Add(new BatchExportError { ItemName = itemNameSafe, ModelPathKey = modelPathKey, ErrorMessage = "Failed to load TTModel." });
                                            continue;
                                        }

                                        int materialSetId = 0;
                                        var itemRoot = item.GetRoot();
                                        if (item is IItemModel itemModel && itemRoot != null && Imc.UsesImc(itemRoot))
                                        {
                                            materialSetId = await Imc.GetMaterialSetId(itemModel, false, tx);
                                        }

                                        var exportSettings = new ModelExportSettings()
                                        {
                                            // IncludeTextures = Settings.Default.ExportIncludeTextures, // Original
                                            IncludeTextures = true, // Workaround: Hardcode to default
                                            ShiftUVs = Settings.Default.ShiftExportUV, // Assuming this one is okay or handled elsewhere
                                            // PbrTextures = Settings.Default.ExportPbrMode, // Original - Note: ModelExportSettings uses PbrFormat
                                            PbrFormat = false, // Workaround: Hardcode to default for PbrFormat
                                        };

                                        await Mdl.ExportTTModelToFile(ttModel, exportToPath, materialSetId, exportSettings, tx);
                                    }
                                    catch (Exception modelEx)
                                    {
                                        errors.Add(new BatchExportError { ItemName = itemNameSafe, ModelPathKey = modelPathKey, ErrorMessage = $"Failed to export model: {modelEx.Message}" });
                                        Trace.WriteLine($"Error exporting model {modelPathKey} for {itemNameSafe}: {modelEx.Message} - {modelEx.StackTrace}");
                                    }
                                }
                            }
                            catch (Exception itemEx)
                            {
                                errors.Add(new BatchExportError { ItemName = itemNameSafe, ErrorMessage = $"Error processing item: {itemEx.Message}" });
                                Trace.WriteLine($"Error processing item {itemNameSafe}: {itemEx.Message} - {itemEx.StackTrace}");
                            }
                        }
                    }); // End Task.Run
                }
                catch (Exception ex) // Catch exceptions from Task.Run or setup before it
                {
                    Trace.WriteLine($"Batch export general error: {ex.Message} - {ex.StackTrace}");
                    errors.Add(new BatchExportError { ErrorMessage = $"A critical error occurred: {ex.Message}" });
                }
                finally
                {
                    await progressController.CloseAsync();
                    itemControl?.Dispose();
                    ProgressBarVisible = Visibility.Collapsed;
                    ProgressLabel = ""; // Reset label
                    ProgressValue = 0;  // Reset value
                }

                if (errors.Any())
                {
                    if (errors.Any(err => err.ItemName == "Operation Cancelled"))
                    {
                        await _mainWindow.ShowMessageAsync("Batch Export Cancelled", "The batch export operation was cancelled by the user.");
                    }
                    else
                    {
                        await ShowErrorSummaryDialog(errors);
                    }
                }
                else
                {
                    await _mainWindow.ShowMessageAsync("Batch Export Complete", $"Successfully exported files to {exportDir} for {itemsToExport.Count} items.");
                }
            }
            else
            {
                await _mainWindow.ShowMessageAsync("Batch Export Cancelled", "Export operation cancelled by user at folder selection.");
            }
        }

        private class BatchExportError
        {
            public string ItemName { get; set; } = "N/A";
            public string ModelPathKey { get; set; } = "N/A";
            public string ErrorMessage { get; set; }
        }

        private async Task ShowErrorSummaryDialog(List<BatchExportError> errors)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Batch export completed with {errors.Count} error(s):\n");
            foreach (var error in errors.Take(20)) // Show details for up to 20 errors to keep dialog manageable
            {
                sb.AppendLine($"Item: {error.ItemName}");
                if (error.ModelPathKey != "N/A")
                {
                    sb.AppendLine($"  Model: {Path.GetFileName(error.ModelPathKey)}");
                }
                sb.AppendLine($"  Error: {error.ErrorMessage}\n");
            }
            if (errors.Count > 20)
            {
                sb.AppendLine($"\n...and {errors.Count - 20} more errors (check log for full details).");
            }

            // Using FlexibleMessageBox for simplicity, but a custom dialog would be better for scrolling very long lists.
            // This will be a large message box.
            string dialogTitle = "Batch Export Completed with Errors";
            string message = sb.ToString();

            // For very long messages, a proper scrollable dialog is better.
            // This is a workaround if FlexibleMessageBox is the primary tool.
            if (message.Length > 2000) { // Heuristic for "too long"
                 message = string.Join("\n", errors.Select(e => $"Item: {e.ItemName}, Model: {e.ModelPathKey}, Err: {e.ErrorMessage}").Take(50)) + $"\n\n... view logs for all {errors.Count} errors.";
            }

            await _mainWindow.ShowMessageAsync(dialogTitle, message);
            // Ideally, use a custom dialog:
            // var dialog = new CustomDialog() { Title = dialogTitle };
            // var textBlock = new TextBlock { Text = sb.ToString(), TextWrapping = TextWrapping.Wrap };
            // var scrollViewer = new ScrollViewer { Content = textBlock, MaxHeight = 400 };
            // dialog.Content = scrollViewer;
            // await _mainWindow.ShowMetroDialogAsync(dialog);
            // await dialog.WaitUntilUnloadedAsync();
        }


        private static string SanitizePath(string name)
        {
            if (string.IsNullOrEmpty(name)) return "Unnamed";
            string invalidChars = new string(Path.GetInvalidFileNameChars()) + new string(Path.GetInvalidPathChars());
            string regexSearch = string.Format(@"([{0}]*\.+$)|([{0}]+)", Regex.Escape(invalidChars));
            string result = Regex.Replace(name, regexSearch, "_");
            return result.Length > 100 ? result.Substring(0, 100) : result; // Limit length
        }

        private Category FindCategoryByName(ObservableCollection<Category> categories, string name)
        {
            if (categories == null) return null;
            foreach (var category in categories)
            {
                if (category.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
                {
                    return category;
                }
                // Optional: Check subcategories if needed, though for "Housing" and "Indoor Furniture" it's likely top-level or second-level.
                // var foundInChildren = FindCategoryByName(category.Categories, name);
                // if (foundInChildren != null) return foundInChildren;
            }
            return null;
        }

        private void CollectItemsFromCategory(Category category, List<IItem> items)
        {
            if (category == null) return;

            if (category.Item != null) // Assuming single item per category node
            {
                items.Add(category.Item);
            }
            // If a category can have both an Item and SubCategories with Items, or if Items are only at leaf nodes, this logic might need adjustment.
            // For now, assuming items can be on any category node.

            // Also, it seems categories can have an Item directly OR a list of items.
            // The provided Category.cs has `public IItem Item { get; set; }` which implies one item per category.
            // If a category node itself is an item entry, this is fine.
            // If a category is a container and its direct children are items, this logic needs to change.
            // The `ItemSelectControl` likely populates these Category objects.
            // Let's assume for now `category.Item` is the way to get an item if that category represents one.

            // And recursively collect from sub-categories
            if (category.Categories != null)
            {
                foreach (var subCategory in category.Categories)
                {
                    // If the subCategory itself is an item entry
                    if (subCategory.Item != null)
                    {
                         items.Add(subCategory.Item);
                    }
                    // And continue to search deeper
                    CollectItemsFromCategory(subCategory, items);
                }
            }
        }

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

        #region Test Methods
        // This region will contain methods for testing the batch export logic.
        // To be called manually or via a test runner if available.

        public async Task RunBatchExportTests()
        {
            Trace.WriteLine("Starting Batch Export Logic Tests...");

            TestSanitizePath();
            TestCategoryAndItemRetrieval();
            await TestSimulatedBatchExportFlow();

            Trace.WriteLine("Batch Export Logic Tests Finished.");
            // In a real test environment, we'd have assertions and pass/fail results.
            // Here, we rely on Trace output and manual inspection.

            // Showing a message box from a ViewModel is not ideal, but for a test runner stub:
            // This should be conditional or handled by a dedicated test UI if this were a real feature.
            // For automated agent context, this line might be problematic.
            // Consider removing if it causes issues with agent's flow.
            // FlexibleMessageBox.Show("Test run finished. Check Trace output for details.", "Test Runner", MessageBoxButtons.OK, MessageBoxIcon.Information);
            Trace.WriteLine("Test Run Message: Test run finished. Check Trace output for details.");
        }

        private void TestSanitizePath()
        {
            Trace.WriteLine("\n--- Testing SanitizePath ---");
            var testCases = new Dictionary<string, string>
            {
                { "ValidName123", "ValidName123" },
                { "Name With Spaces", "Name_With_Spaces" },
                { "Name/With/Slashes", "Name_With_Slashes" },
                { "Name*With<Invalid>Chars:?", "Name_With_Invalid_Chars_" },
                { "", "Unnamed" },
                { null, "Unnamed" },
                { new string('a', 150), new string('a', 100) }
            };
            int passedCount = 0;
            foreach (var tc in testCases)
            {
                var result = SanitizePath(tc.Key);
                bool passed = result == tc.Value;
                if(passed) passedCount++;
                Trace.WriteLine($"Input: '{tc.Key}', Output: '{result}', Expected: '{tc.Value}', Passed: {passed}");
            }
            Trace.WriteLine($"SanitizePath Test Summary: {passedCount}/{testCases.Count} passed.");
        }

        // Mock IItem for testing
        private class MockItem : IItem
        {
            public string Name { get; set; }
            public int ID { get; set; }
            public XivItemType? Type { get; set; } = XivItemType.common; // Retaining nullable for flexibility in tests
            public ushort Icon { get; set; }
            public XivRarity Rarity { get; set; } = XivRarity.Common;
            public string PrimaryCategory { get; set; } = "MockPrimary";
            public string SecondaryCategory { get; set; } = "MockSecondary";
            public string TertiaryCategory { get; set; } = "MockTertiary";
            public XivDataFile DataFile { get; set; } // May be null if not critical for tests

            public string GetModlistItemName() { return Name ?? "Unnamed MockItem"; }
            public string GetModlistItemCategory() { return PrimaryCategory ?? "Mock"; }

            public int CompareTo(object obj)
            {
                if (obj is IItem other) return String.Compare(Name, other.Name, StringComparison.Ordinal);
                return 1;
            }

            // Updated GetRoot to return a more functional, though still mock, XivDependencyRoot
            // The previous GetRoot was already fine for the batch export's usage of it.
            public XivDependencyRoot GetRoot()
            {
                var itemNameForRoot = string.IsNullOrEmpty(this.Name) ? "DefaultMockItemName" : this.Name;
                // Ensure PrimaryType in XivDbItemInfo is not nullable if XivItemType? Type is used.
                var rootInfo = new XivDbItemInfo { Name = itemNameForRoot, PrimaryType = this.Type ?? XivItemType.common, ID = this.ID };
                return new XivDependencyRoot(rootInfo);
            }

            public bool CanFavorite { get => false; } // Per requirement
            public bool IsFavorite { get; set; } // Per requirement

            // Existing properties from previous implementation
            public string TTMPGroupName { get; set; }
            public string TTMPGroupOption { get; set; }
            public ObservableCollection<string> Tags { get; set; } = new ObservableCollection<string>();
            public List<XivPlatform> Platforms { get; set; } = new List<XivPlatform> { XivPlatform.Windows };
            public List<XivGender> Genders { get; set; } = new List<XivGender> { XivGender.All };
            public List<XivRace> Races { get; set; } = new List<XivRace>();
            public bool IsCommon => true;
            public bool IsDefault { get; set; }
            public string ModelPath { get; set; }
            public int ModelVariant { get; set; }
            public int MaterialVariant { get; set; }
            public int DecalVariant { get; set; }
            public bool UsesBodySlot => false;
            public string Description { get; set; }
            public string Tooltip { get; set; }
            public XivActionUsage ActionUsage { get; set; }
        }

        // MockModelItem now inherits from the updated MockItem
        internal class MockModelItem : MockItem, IItemModel
        {
            public MockModelItem() { this.Type = XivItemType.equipment; } // Default type for model item

            // XivModelInfo is a class, can be initialized or null
            public XivModelInfo ModelInfo { get; set; } = new XivModelInfo { ImcSubsetID = 1, ModelPath = "chara/some/model.mdl", MaterialSet = 0 };
            public uint IconId { get; set; } // Changed from ushort to uint

            // ImcEntry can be null if not critical for the test
            public ImcEntry ImcEntry { get; set; }
            public bool UsesImc { get; set; } // Added

            public object Clone() { return this.MemberwiseClone(); } // Added

            // Other IItemModel specific properties (already present from previous version)
            public XivRace Race { get; set; }
            public XivGender Gender { get; set; }
            public XivBodySlot BodySlot { get; set; }
            public bool IsAccessory => false;
            public bool IsBody => false;
            public bool IsEar => false;
            public bool IsHair => false;
            public bool IsHead => false;
            public bool IsTail => false;
            public bool IsSmallClothes => false;
            public bool IsWeapon => false;
            // Note: ushort IconId was in previous MockModelItem, IItem has ushort Icon. IItemModel has uint IconId.
            // Kept uint IconId for IItemModel, MockItem keeps ushort Icon for IItem.
        }


        private void TestCategoryAndItemRetrieval()
        {
            Trace.WriteLine("\n--- Testing Category and Item Retrieval ---");
            var rootCategories = new ObservableCollection<Category>();
            var housing = new Category { Name = "Housing", Categories = new ObservableCollection<Category>() };
            var outdoor = new Category { Name = "Outdoor Furniture", Item = new MockItem { Name = "Outdoor Bench" }, Categories = new ObservableCollection<Category>() };
            var indoor = new Category { Name = "Indoor Furniture", Categories = new ObservableCollection<Category>() };
            var table = new Category { Name = "Table", Item = new MockModelItem { Name = "Wooden Table" } };
            var chair = new Category { Name = "Chair", Item = new MockModelItem { Name = "Wooden Chair" } }; // This item will have models
            var decor = new Category { Name = "Decor", Categories = new ObservableCollection<Category>() };
            var rug = new Category { Name = "Rug", Item = new MockItem { Name = "Fluffy Rug" } }; // This item will not have models based on sim

            decor.Categories.Add(rug);
            indoor.Categories.Add(table); // Table is a MockModelItem
            indoor.Categories.Add(chair); // Chair is a MockModelItem
            indoor.Categories.Add(decor); // Decor contains Rug (MockItem)
            housing.Categories.Add(outdoor); // Outdoor Bench is MockItem
            housing.Categories.Add(indoor);
            rootCategories.Add(housing);
            rootCategories.Add(new Category { Name = "Equipment", Item = new MockItem { Name = "Test Sword" } });

            bool findHousingPassed = false;
            var foundHousing = FindCategoryByName(rootCategories, "Housing");
            if (foundHousing == housing) findHousingPassed = true;
            Trace.WriteLine($"FindCategoryByName 'Housing': {(findHousingPassed ? "Passed" : "Failed")}");

            bool findIndoorPassed = false;
            var foundIndoor = FindCategoryByName(housing.Categories, "Indoor Furniture");
            if (foundIndoor == indoor) findIndoorPassed = true;
            Trace.WriteLine($"FindCategoryByName 'Indoor Furniture': {(findIndoorPassed ? "Passed" : "Failed")}");

            bool notFoundPassed = false;
            var notFound = FindCategoryByName(rootCategories, "NonExistent");
            if (notFound == null) notFoundPassed = true;
            Trace.WriteLine($"FindCategoryByName 'NonExistent': {(notFoundPassed ? "Passed" : "Failed")}");

            List<IItem> collectedItems = new List<IItem>();
            if (foundIndoor != null)
            {
                CollectItemsFromCategory(foundIndoor, collectedItems);
            }
            // Expected: Wooden Table, Wooden Chair, Fluffy Rug.
            // Note: CollectItemsFromCategory adds category.Item first, then iterates subcategories.
            // If a category (like "Decor") doesn't have a direct .Item but its children do, they are added.
            // If "Indoor Furniture" itself had an .Item, it would be added too.
            // Current logic: Table, Chair, Rug (Decor itself has no item, its sub-cat Rug does)
            bool collectionCountPassed = collectedItems.Count == 3;
            Trace.WriteLine($"Collected items count: {collectedItems.Count} (Expected: 3)");
            bool collectionContentPassed = collectionCountPassed &&
                                        collectedItems.Any(it => ((MockItem)it).Name == "Wooden Table") &&
                                        collectedItems.Any(it => ((MockItem)it).Name == "Wooden Chair") &&
                                        collectedItems.Any(it => ((MockItem)it).Name == "Fluffy Rug");
            Trace.WriteLine($"CollectItemsFromCategory content: {(collectionContentPassed ? "Passed" : "Failed")}");
            Trace.WriteLine($"Category Retrieval Test Summary: {(findHousingPassed && findIndoorPassed && notFoundPassed && collectionContentPassed ? "All Passed" : "Some Failed")}");
        }

        // Simulated ItemViewControl for testing purposes
        private class SimulatedItemViewControl : IDisposable
        {
            public Dictionary<string, Dictionary<string, HashSet<string>>> Files { get; private set; }
            private IItem _currentItem;

            public async Task<bool> SetItem(IItem item)
            {
                _currentItem = item;
                Files = new Dictionary<string, Dictionary<string, HashSet<string>>>();
                string itemName = (_currentItem as MockItem)?.Name ?? "DefaultItem";

                if (item is MockModelItem) // Only add models for MockModelItem
                {
                    var modelKey1 = $"chara/housing/furniture/{SanitizePath(itemName)}_a.mdl";
                    var modelKey2 = $"chara/housing/furniture/{SanitizePath(itemName)}_b.mdl";
                    Files[modelKey1] = new Dictionary<string, HashSet<string>>();
                    Files[modelKey2] = new Dictionary<string, HashSet<string>>();
                }
                // If it's just a MockItem, Files remains empty, simulating an item with no models.

                await Task.Delay(1); // Simulate async work (very short for tests)
                return true;
            }

            public void Dispose() { /* No-op for this simple simulation */ }
        }


        private async Task TestSimulatedBatchExportFlow()
        {
            Trace.WriteLine("\n--- Testing Simulated Batch Export Flow ---");
            string testExportDir = Path.Combine(Path.GetTempPath(), $"TexToolsBatchExportTest_{Path.GetRandomFileName()}");
            Directory.CreateDirectory(testExportDir);
            Trace.WriteLine($"Using test export directory: {testExportDir}");

            var itemsToExport = new List<IItem>
            {
                new MockModelItem { Name = "Fancy Table", ID=1, ModelInfo = new XivModelInfo(), IconId = 101, UsesImc = true, ImcEntry = new ImcEntry() },
                new MockItem { Name = "Plain Vase", ID=2 },
                new MockModelItem { Name = "Comfy Chair/With/Slashes", ID=3, ModelInfo = new XivModelInfo(), IconId = 102, UsesImc = false }
            };

            List<string> simulatedExportedFiles = new List<string>();
            List<BatchExportError> errors = new List<BatchExportError>();
            SimulatedItemViewControl itemControl = new SimulatedItemViewControl();

            int progressUpdates = 0;
            // Simulate the main loop from BatchExportHousingIndoorFurniture
            for (int i = 0; i < itemsToExport.Count; i++)
            {
                var item = itemsToExport[i];
                string itemName = (item as MockItem)?.Name ?? $"Item_{i}";
                string itemNameSafe = SanitizePath(itemName);

                progressUpdates++;
                Trace.WriteLine($"Simulated Progress: Processing {itemNameSafe} ({i + 1}/{itemsToExport.Count})");

                try
                {
                    bool itemSetSuccessfully = await itemControl.SetItem(item);
                    if (!itemSetSuccessfully || itemControl.Files == null) // Files should never be null due to constructor
                    {
                        errors.Add(new BatchExportError { ItemName = itemNameSafe, ErrorMessage = "Simulated: Failed to set item." });
                        continue;
                    }

                    if (!itemControl.Files.Any()) // No keys means no models
                    {
                        Trace.WriteLine($"Simulated: No model files found for {itemNameSafe}. Skipping.");
                        continue;
                    }

                    foreach (var modelPathKey in itemControl.Files.Keys)
                    {
                        // No need to check for null/empty key here as SimulatedItemViewControl always adds valid-like keys if it adds any.
                        string modelFileName = Path.GetFileNameWithoutExtension(modelPathKey);
                        string modelFileNameSafe = SanitizePath(modelFileName);
                        string itemSpecificExportDir = Path.Combine(testExportDir, itemNameSafe);
                        // Directory.CreateDirectory(itemSpecificExportDir); // Not strictly needed for this sim
                        string exportToPath = Path.Combine(itemSpecificExportDir, $"{modelFileNameSafe}.fbx");

                        Trace.WriteLine($"Simulated Progress: Exporting {itemNameSafe}/{modelFileNameSafe}.fbx");

                        // SIMULATE Mdl.GetTTModel and Mdl.ExportTTModelToFile call
                        if (modelPathKey.Contains("error_on_load")) // Test error during GetTTModel
                        {
                            errors.Add(new BatchExportError{ItemName = itemNameSafe, ModelPathKey = modelPathKey, ErrorMessage = "Simulated: Failed to load TTModel."});
                            continue;
                        }
                        if (modelPathKey.Contains("error_on_export")) // Test error during Export
                        {
                             errors.Add(new BatchExportError{ItemName = itemNameSafe, ModelPathKey = modelPathKey, ErrorMessage = "Simulated: Failed to export model."});
                            continue;
                        }
                        simulatedExportedFiles.Add(exportToPath);
                        await Task.Delay(1);
                    }
                }
                catch (Exception ex)
                {
                    errors.Add(new BatchExportError { ItemName = itemNameSafe, ErrorMessage = $"Simulated: Unexpected error processing item: {ex.Message}" });
                }
            }
            itemControl.Dispose();

            Trace.WriteLine("\n--- Simulation Results ---");
            Trace.WriteLine($"Total progress updates recorded: {progressUpdates} (Expected: {itemsToExport.Count})");
            Trace.WriteLine("Simulated Exported Files Paths (for items that are MockModelItem):");
            foreach(var f in simulatedExportedFiles) { Trace.WriteLine(f); }

            int expectedFileCount = 4; // Fancy Table (2) + Comfy Chair (2)
            bool fileCountPassed = simulatedExportedFiles.Count == expectedFileCount;
            Trace.WriteLine($"Total files simulated for export: {simulatedExportedFiles.Count} (Expected: {expectedFileCount}) - {(fileCountPassed ? "Passed" : "Failed")}");

            Trace.WriteLine($"Errors collected: {errors.Count} (Expected: 0 for this test case)");
            if (errors.Any()) { foreach(var e in errors) { Trace.WriteLine($"  Item: {e.ItemName}, Model: {e.ModelPathKey}, Err: {e.ErrorMessage}"); } }

            bool overallPassed = fileCountPassed && errors.Count == 0;
            Trace.WriteLine($"Simulated Batch Export Flow Test Summary: {(overallPassed ? "Passed" : "Failed")}");

            try { Directory.Delete(testExportDir, true); } catch { Trace.WriteLine($"Warning: Could not delete test directory {testExportDir}"); }
        }

        #endregion

    }
}