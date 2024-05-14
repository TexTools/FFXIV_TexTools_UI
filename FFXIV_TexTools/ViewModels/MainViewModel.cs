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

namespace FFXIV_TexTools.ViewModels
{
    public class MainViewModel : INotifyPropertyChanged
    {
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
        }

        public void UpdateDependencyQueueCount(object sender, System.Timers.ElapsedEventArgs e)
        {
            var count = XivCache.GetDependencyQueueLength();
            if (count > 0)
            {
                _mainWindow.ShowStatusMessage($"Queue Length: {count._()}".L());
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


        /// <summary>
        /// Performs post-patch modlist corrections and validation, prompting user also to generate backups after a successful completion.
        /// </summary>
        /// <returns></returns>
        public async Task DoPostPatchCleanup()
        {

            FlexibleMessageBox.Show(_mainWindow.Win32Window, UIMessages.PatchDetectedMessage, "Post Patch Cleanup Starting".L(), MessageBoxButtons.OK, MessageBoxIcon.Information, MessageBoxDefaultButton.Button1);
            MainWindow.MakeHighlander();

            var resetLumina = false;

            await _mainWindow.LockUi("Performing Post-Patch Maintenence".L(), "This may take a few minutes if you have many mods installed.".L(), this);

            var gi = XivCache.GameInfo;
            if (XivCache.GameInfo.UseLumina)
            {
                resetLumina = true;
                XivCache.SetGameInfo(gi.GameDirectory, gi.GameLanguage, gi.DxMode, false, false, gi.LuminaDirectory, gi.UseLumina);
            }

            var workerStatus = XivCache.CacheWorkerEnabled;
            XivCache.CacheWorkerEnabled = false;

            var readonlyTx = ModTransaction.BeginTransaction();
            if ((await readonlyTx.GetModList()).Mods.Count == 0)
            {
                // No mods.  Just create backups and move on with our life.
                await BackupIndexFiles();
                return;
            }

            if (!Dat.AllowDatAlteration)
            {
                // We have mods on file, we'll need write access here.
                throw new Exception("Cannot perform Post-Patch Cleanup with ");
            }

            try
            {


                var validTypes = new List<int>() { 2, 3, 4 };

                // We have to do a few things here.
                // 1.  Save a list of what mods were enabled.
                // 2.  Go through and validate everything that says it is enabled actually is enabled, or mark it as disabled and update its original index offset if it is not.
                // 3.  Prompt the user for either a full disable and backup creation, or a restore to normal state (re-enable anything that was enabled before but is not now)

                var toRemove = new List<Mod>();
                List<Mod> enabledMods;
                // Cache our currently enabled stuff.
                throw new NotImplementedException("Post-Patch Cleanup is currently non-implemented.");
                using (var tx = ModTransaction.BeginTransaction(true))
                {
                    var modList = await tx.GetModList();
                    var allMods = modList.GetMods();
                    // MARK: MOD ALTERATION
                    // TODO: Redo this.
                    /*
                    enabledMods = modList.GetMods(x => x.Enabled).ToList();

                    foreach (var oMod in allMods)
                    {
                        var mod = oMod;

                        var df = IOUtil.GetDataFileFromPath(mod.FilePath);
                        var index = await tx.GetIndexFile(df);

                        var index1Value = index.Get8xDataOffset(mod.FilePath);
                        var index2Value = index.Get8xDataOffsetIndex2(mod.FilePath);
                        var oldOriginalOffset = mod.OriginalOffset8x;
                        var modOffset = mod.ModOffset8x;


                        // In any event where an offset does not match either of our saved offsets, we must assume this is a new
                        // default file offset for post-patch.
                        if (index1Value != oldOriginalOffset && index1Value != modOffset && index1Value != 0)
                        {
                            // Index 1 value is our new base offset.
                            var type = _dat.GetFileType(index1Value, df);

                            // Make sure the file it's trying to point to is actually valid.
                            if (validTypes.Contains(type))
                            {
                                mod.OriginalOffset8x = index1Value;
                            }
                            else
                            {
                                // Oh dear.  The new index is fucked.  Is the old Index Ok?
                                type = _dat.GetFileType(oldOriginalOffset, df);

                                if (validTypes.Contains(type) && oldOriginalOffset != 0)
                                {
                                    // Old index is fine, so keep using that.

                                    // But mark the index value as invalid, so that we stomp on the index value after this.
                                    index1Value = -1;
                                }
                                else
                                {
                                    // Okay... Maybe the new Index2 Value?
                                    if (index2Value != 0)
                                    {
                                        type = _dat.GetFileType(index2Value, df);
                                        if (validTypes.Contains(type))
                                        {
                                            // Set the index 1 value to invalid so that the if later down the chain stomps the index1 value.
                                            index1Value = -1;

                                            mod.OriginalOffset8x = index2Value;
                                        }
                                        else
                                        {
                                            // We be fucked.
                                            throw new Exception("Unable to determine working original offset for file:".L() + mod.FilePath);
                                        }
                                    }
                                    else
                                    {
                                        // We be fucked.
                                        throw new Exception("Unable to determine working original offset for file:".L() + mod.FilePath);
                                    }
                                }
                            }
                        }
                        else if (index2Value != oldOriginalOffset && index2Value != modOffset && index2Value != 0)
                        {
                            // Our Index 1 was normal, but our Index 2 is changed to an unknown value.
                            // If the index 2 points to a valid file, we must assume that this new file 
                            // is our new base data offset.

                            var type = _dat.GetFileType(index2Value, df);

                            if (validTypes.Contains(type) && index2Value != 0)
                            {
                                mod.OriginalOffset8x = index2Value;
                            }
                            else
                            {
                                // Oh dear.  The new index is fucked.  Is the old Index Ok?
                                type = _dat.GetFileType(oldOriginalOffset, df);

                                if (validTypes.Contains(type) && oldOriginalOffset != 0)
                                {
                                    // Old index is fine, so keep using that, but set the index2 value to invalid to ensure we 
                                    // stomp on the current broken index value.
                                    index2Value = -1;
                                }
                                else
                                {
                                    // We be fucked.
                                    throw new Exception("Unable to determine working original offset for file:".L() + mod.FilePath);
                                }
                            }
                        }

                        // Indexes don't match.  This can occur if SE adds something to index2 that didn't exist in index2 before.
                        if (index1Value != index2Value && index2Value != 0)
                        {
                            // We should never actually get to this state for file-addition mods.  If we do, uh.. I guess correct the indexes and yolo?
                            // ( Only way we get here is if SE added a new file at the same name as a file the user had created via modding, in which case, it's technically no longer a file addition mod )
                            index.Set8xDataOffset(mod.FilePath, mod.OriginalOffset8x);

                            index1Value = mod.OriginalOffset8x;
                            index2Value = mod.OriginalOffset8x;
                        }

                        // Perform a basic file type check on our results.
                        var fileType = _dat.GetFileType(mod.ModOffset8x, IOUtil.GetDataFileFromPath(mod.FilePath));
                        var originalFileType = _dat.GetFileType(mod.OriginalOffset8x , IOUtil.GetDataFileFromPath(mod.FilePath));

                        if (!validTypes.Contains(fileType) || mod.ModOffset8x == 0)
                        {
                            // Mod data is busted.  Fun.
                            toRemove.Add(mod);
                        }

                        if ((!validTypes.Contains(originalFileType)) || mod.OriginalOffset8x == 0)
                        {
                            if (mod.IsCustomFile())
                            {
                                // Okay, in this case this is recoverable as the mod is a custom addition anyways, so we can just delete it.
                                if (!toRemove.Contains(mod))
                                {
                                    toRemove.Add(mod);
                                }
                            }
                            else
                            {
                                // Update ended up with us unable to find a working offset.  Double fun.
                                throw new Exception("Unable to determine working offset for file:".L() + mod.FilePath);
                            }
                        }

                        // Okay, this mod is now represented in the modlist in it's actual post-patch index state.
                        var datNum = (int)((mod.ModOffset8x / 8) & 0x0F) / 2;
                        var dat = IOUtil.GetDataFileFromPath(mod.FilePath);

                        var originalDats = await _dat.GetUnmoddedDatList(dat);
                        var datPath = Dat.GetDatPath(dat, datNum);

                        // Test for SE Dat file rollover.
                        if (originalDats.Contains(datPath))
                        {
                            // Shit.  This means that the dat file where this mod lived got eaten by SE.  We have to destroy the modlist entry at this point.
                            toRemove.Add(mod);
                        }

                        modList.AddOrUpdateMod(mod);
                    }
                    
                    */
                    await ModTransaction.CommitTransaction(tx);
                    // Internal files always need to be purged and rebuilt.
                    var internalFiles = allMods.Where(x => x.IsInternal());
                    toRemove.AddRange(internalFiles);
                }

                // We now need to clear out any mods that are irreparably fucked, and clear out all of our
                // internal data files so we can rebuild them later.
                if (toRemove.Count > 0)
                {
                    using (var tx = ModTransaction.BeginTransaction(true))
                    {
                        var modList = await tx.GetModList();

                        var removedString = "";
                        var allMods = modList.GetMods();

                        // Soft-Disable all metadata mods, since we're going to purge their internal file entries.
                        var metadata = allMods.Where(x => x.FilePath.EndsWith(".meta") || x.FilePath.EndsWith(".rgsp"));
                        foreach (var mod in metadata)
                        {
                            var df = IOUtil.GetDataFileFromPath(mod.FilePath);
                            await Modding.ToggleModUnsafe(false, mod, true, false, tx);
                        }

                        foreach (var mod in toRemove)
                        {
                            if (mod.ModOffset8x == 0 || mod.OriginalOffset8x == 0)
                            {
                                modList.RemoveMod(mod);
                                removedString += mod.FilePath + "\n";
                            }
                            else
                            {
                                var df = IOUtil.GetDataFileFromPath(mod.FilePath);
                                await Modding.ToggleModUnsafe(false, mod, true, false, tx);

                                modList.RemoveMod(mod);

                                if (!mod.IsInternal())
                                {
                                    removedString += mod.FilePath + "\n";
                                }
                            }
                        }

                        await ModTransaction.CommitTransaction(tx);

                        // Show the user a message if we purged any real files.
                        if (toRemove.Any(x => !String.IsNullOrEmpty(x.FilePath) && !x.IsInternal()))
                        {
                            var text = String.Format(UIMessages.PatchDestroyedFiles, removedString);

                            FlexibleMessageBox.Show(_mainWindow.Win32Window, text, "Destroyed Files Notification".L(), MessageBoxButtons.OK, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button1);
                        }
                    }
                }

                // Always create clean index backups after this process is completed.
                _mainWindow.LockProgress.Report("Disabling Mods...".L());
                await Modding.ToggleAllMods(false);

                await BackupIndexFiles();

                // Now restore the modlist enable/disable state back to how the user had it before.
                _mainWindow.LockProgress.Report("Re-Enabling mods...");

                // Re-enable things.
                //await Modding.ToggleMods(true, enabledMods.Select(x => x.FilePath));

                FlexibleMessageBox.Show(_mainWindow.Win32Window, UIMessages.PostPatchComplete, "Post-Patch Process Complete".L(), MessageBoxButtons.OK, MessageBoxIcon.Information, MessageBoxDefaultButton.Button1);

            }
            catch(Exception Ex)
            {
                // Show the user the error, then let them go about their business of fixing things.
                FlexibleMessageBox.Show(_mainWindow.Win32Window, String.Format(UIMessages.PostPatchError, Ex.Message), "Post-Patch Failure".L(), MessageBoxButtons.OK, MessageBoxIcon.Information, MessageBoxDefaultButton.Button1);
            }
            finally
            {

                if(resetLumina)
                {
                    // Reset lumina mode back to on if we disabled it to perform update checks.
                    XivCache.SetGameInfo(gi.GameDirectory, gi.GameLanguage, gi.DxMode, true, false, gi.LuminaDirectory, true);
                }

                XivCache.CacheWorkerEnabled = workerStatus;
                await _mainWindow.UnlockUi(this);
            }
        }

        private async Task BackupIndexFiles()
        {
            _mainWindow.LockProgress?.Report("Creating Index Backups...".L());
            var pc = new ProblemChecker(XivCache.GameInfo.GameDirectory);
            DirectoryInfo backupDir;
            try
            {
                Directory.CreateDirectory(Settings.Default.Backup_Directory);
                backupDir = new DirectoryInfo(Settings.Default.Backup_Directory);
            }
            catch
            {
                throw new Exception("Unable to create index backups.\nThe Index Backup directory is invalid or inaccessible: ".L() + Settings.Default.Backup_Directory);
            }

            await pc.BackupIndexFiles(backupDir);

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
            var gi = XivCache.GameInfo;
            if (DXVersionText.Contains("11"))
            {
                Properties.Settings.Default.DX_Version = "9";
                Properties.Settings.Default.Save();
                XivCache.SetGameInfo(gi.GameDirectory, gi.GameLanguage, 9, true, true, gi.LuminaDirectory, gi.UseLumina);
            }
            else
            {
                Properties.Settings.Default.DX_Version = "11";
                Properties.Settings.Default.Save();
                XivCache.SetGameInfo(gi.GameDirectory, gi.GameLanguage, 11, true, true, gi.LuminaDirectory, gi.UseLumina);
            }

            DXVersionText = $"DX: {Properties.Settings.Default.DX_Version}";
        }



        /// <summary>
        /// Enables all mods in the mod list
        /// </summary>
        /// <param name="obj"></param>
        private async void EnableAllMods(object obj)
        {
            _progressController = await _mainWindow.ShowProgressAsync(UIMessages.EnablingModsTitle, UIMessages.PleaseWaitMessage);
            var progressIndicator = new Progress<(int current, int total, string message)>(ReportProgress);

            if (FlexibleMessageBox.Show(
                    UIMessages.EnableAllModsMessage, UIMessages.EnablingModsTitle, MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
            {
                bool err = false;
                try
                {
                    await Modding.ToggleAllMods(true, progressIndicator);
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
            var progressIndicator = new Progress<(int current, int total, string message)>(ReportProgress);

            if (FlexibleMessageBox.Show(
                    UIMessages.DisableAllModsMessage, UIMessages.DisableAllModsTitle, MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
            {
                bool err = false;
                try { 
                    await Modding.ToggleAllMods(false, progressIndicator);
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

        /// <summary>
        /// Updates the progress bar
        /// </summary>
        /// <param name="value">The progress value</param>
        private void ReportProgress((int current, int total, string message) report)
        {
            if (!report.message.Equals(string.Empty))
            {
                _progressController.SetMessage(report.message.L());
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

    }
}