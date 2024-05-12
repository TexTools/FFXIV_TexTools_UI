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

using Index = xivModdingFramework.SqPack.FileTypes.Index;
using System.Drawing.Imaging;

namespace FFXIV_TexTools.ViewModels
{
    public class MainViewModel : INotifyPropertyChanged
    {
        private DirectoryInfo _gameDirectory;
        private readonly MainWindow _mainWindow;

        private ObservableCollection<Category> _categories = new ObservableCollection<Category>();

        private string _searchText, _progressLabel;
        private string _dxVersionText = $"DX: {Properties.Settings.Default.DX_Version}";
        private int _progressValue;
        private Visibility _progressBarVisible, _progressLabelVisible;
        private Index _index;
        private ProgressDialogController _progressController;
        public System.Timers.Timer CacheTimer = new System.Timers.Timer(3000);

        private const string WarningIdentifier = "!!";

        public MainViewModel(MainWindow mainWindow)
        {
            _mainWindow = mainWindow;
            // This is actually synchronous and can just be called immediately...
            SetDirectories();

            _gameDirectory = new DirectoryInfo(Properties.Settings.Default.FFXIV_Directory);
            _index = new Index(_gameDirectory);
            if (ProgressLabel == null)
            {
                ProgressLabel = "";
            }

            // And the rest of this can be pushed into a new thread.
            var task = Task.Run(Initialize);

            // Now we can wait on it.  But we have to thread-safety it.
            task.Wait();

            var exception = task.Exception;
            if(exception != null)
            {
                throw exception;
            }
            var result = task.Result;
            if (!result)
            {
                // We need to die NOW, and not risk any other functions possibly
                // fucking with broken files.
                Process.GetCurrentProcess().Kill();
                return;
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
        /// This function is called on a separate thread, *while* the main thread is blocked.
        /// This means a few things.
        ///   1.  You cannot access the view's UI elements (Thread safety error & uninitialized) 
        ///   2.  You cannot use Dispatcher.Invoke (Deadlock)
        ///   3.  You cannot spawn a new full-fledged windows form (Thread safety error) (Basic default popups are OK)
        ///   4.  You cannot shut down the application (Thread safety error)
        ///   
        /// As such, the return value indicates if we want to gracefully shut down the application.
        /// (True for success/continue, False for failure/graceful shutdown.)
        /// 
        /// Exceptions are checked and rethrown on the main thread.
        /// 
        /// This is really 100% only for things that can be safely checked and sanitized
        /// without external code references or UI interaction beyond basic windows dialogs.
        /// </summary>
        /// <returns></returns>
        public async Task<bool> Initialize()
        {

            var success =  await CheckIndexFiles();
            if (!success)
            {
                return false;
            }

            try
            {
                await CheckGameDxVersion();
            } catch
            {
                // Unable to determine version, skip it.
            }

            return true;

        }

        /// <summary>
        /// Checks FFXIV's selected DirectX version and changes TexTools to the appropriate mode if it does not already match.
        /// </summary>
        /// <returns></returns>
        private async Task CheckGameDxVersion()
        {

            var dir = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) +
                      "\\My Games\\FINAL FANTASY XIV - A Realm Reborn";

            var DX11 = await Task.Run(() =>
            {
                var dx = false;

                if (File.Exists($"{dir}\\FFXIV_BOOT.cfg"))
                {
                    var lines = File.ReadAllLines($"{dir}\\FFXIV_BOOT.cfg");

                    foreach (var line in lines)
                    {
                        if (line.Contains("DX11Enabled"))
                        {
                            var val = line.Substring(line.Length - 1, 1);
                            if (val.Equals("1"))
                            {
                                dx = true;
                            }

                            break;
                        }
                    }
                }

                return dx;
            });

            if (DX11)
            {
                if (Properties.Settings.Default.DX_Version != "11")
                {
                    // Set the User's DX Mode to 11 in TexTools to match 
                    Properties.Settings.Default.DX_Version = "11";
                    Properties.Settings.Default.Save();
                    DXVersionText = "DX: 11";

                    if(XivCache.Initialized)
                    {
                        var gi = XivCache.GameInfo;
                        XivCache.SetGameInfo(gi.GameDirectory, gi.GameLanguage, 11, true, true, gi.LuminaDirectory, gi.UseLumina);
                    }
                }
            }
            else
            {

                if (Properties.Settings.Default.DX_Version != "9")
                {
                    // Set the User's DX Mode to 9 in TexTools to match 
                    var gi = XivCache.GameInfo;
                    Properties.Settings.Default.DX_Version = "9";
                    Properties.Settings.Default.Save();
                    DXVersionText = "DX: 9";
                }

                if (XivCache.Initialized)
                {
                    var gi = XivCache.GameInfo;
                    XivCache.SetGameInfo(gi.GameDirectory, gi.GameLanguage, 9, true, true, gi.LuminaDirectory, gi.UseLumina);
                }
            }

        }


        private async Task<bool> CheckIndexFiles()
        {
            var xivDataFiles = new XivDataFile[] { XivDataFile._0A_Exd, XivDataFile._01_Bgcommon, XivDataFile._04_Chara, XivDataFile._06_Ui };
            var problemChecker = new ProblemChecker(_gameDirectory);

            try
            {
                foreach (var xivDataFile in xivDataFiles)
                {
                    var errorFound = await problemChecker.CheckIndexDatCounts(xivDataFile);

                    if (errorFound)
                    {
                        await problemChecker.RepairIndexDatCounts(xivDataFile);
                    }
                }
            }
            catch (Exception ex)
            {
                var result = FlexibleMessageBox.Show("A critical error occurred when attempting to read the FFXIV index files.\n\nWould you like to restore your index backups?\n\nError: ".L() + ex.Message, "Critical Index Error".L(), MessageBoxButtons.YesNo, MessageBoxIcon.Error, MessageBoxDefaultButton.Button1);
                if (result == DialogResult.Yes)
                {
                    var indexBackupsDirectory = new DirectoryInfo(Settings.Default.Backup_Directory);
                    var success = await problemChecker.RestoreBackups(indexBackupsDirectory);
                    if(!success)
                    {
                        FlexibleMessageBox.Show("Unable to restore Index Backups, shutting down TexTools.".L(), "Critical Error Shutdown".L(), MessageBoxButtons.OK, MessageBoxIcon.Error, MessageBoxDefaultButton.Button1);
                        return false;
                    }
                }
                else
                {
                    FlexibleMessageBox.Show("Shutting Down TexTools.".L(), "Critical Error Shutdown".L(),  MessageBoxButtons.OK, MessageBoxIcon.Error, MessageBoxDefaultButton.Button1);
                    return false;
                }
            }
            return true;
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

            var modding = new Modding(new DirectoryInfo(Properties.Settings.Default.FFXIV_Directory));
            modding.CreateModlist();
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

            var readonlyTx = ModTransaction.BeginTransaction(true);
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

                var modding = new Modding(_gameDirectory);
                var _index = new Index(_gameDirectory);
                var _dat = new Dat(_gameDirectory);

                var validTypes = new List<int>() { 2, 3, 4 };

                // We have to do a few things here.
                // 1.  Save a list of what mods were enabled.
                // 2.  Go through and validate everything that says it is enabled actually is enabled, or mark it as disabled and update its original index offset if it is not.
                // 3.  Prompt the user for either a full disable and backup creation, or a restore to normal state (re-enable anything that was enabled before but is not now)

                var toRemove = new List<Mod>();
                List<Mod> enabledMods;
                // Cache our currently enabled stuff.
                using (var tx = ModTransaction.BeginTransaction())
                {
                    var modList = await tx.GetModList();
                    enabledMods = modList.Mods.Where(x => x.enabled == true).ToList();

                    foreach (var mod in modList.Mods)
                    {
                        if (!String.IsNullOrEmpty(mod.fullPath))
                        {
                            var df = IOUtil.GetDataFileFromPath(mod.fullPath);
                            var index = await tx.GetIndexFile(df);

                            var index1Value = index.Get8xDataOffset(mod.fullPath);
                            var index2Value = index.Get8xDataOffsetIndex2(mod.fullPath);
                            var oldOriginalOffset = mod.data.originalOffset;
                            var modOffset = mod.data.modOffset;


                            // In any event where an offset does not match either of our saved offsets, we must assume this is a new
                            // default file offset for post-patch.
                            if (index1Value != oldOriginalOffset && index1Value != modOffset && index1Value != 0)
                            {
                                // Index 1 value is our new base offset.
                                var type = _dat.GetFileType(index1Value, df);

                                // Make sure the file it's trying to point to is actually valid.
                                if (validTypes.Contains(type))
                                {
                                    mod.data.originalOffset = index1Value;
                                    mod.enabled = false;
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
                                        mod.enabled = false;
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

                                                mod.data.originalOffset = index2Value;
                                                mod.enabled = false;
                                            }
                                            else
                                            {
                                                // We be fucked.
                                                throw new Exception("Unable to determine working original offset for file:".L() + mod.fullPath);
                                            }
                                        }
                                        else
                                        {
                                            // We be fucked.
                                            throw new Exception("Unable to determine working original offset for file:".L() + mod.fullPath);
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
                                    mod.data.originalOffset = index2Value;
                                    mod.enabled = false;
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
                                        throw new Exception("Unable to determine working original offset for file:".L() + mod.fullPath);
                                    }
                                }
                            }

                            // Indexes don't match.  This can occur if SE adds something to index2 that didn't exist in index2 before.
                            if (index1Value != index2Value && index2Value != 0)
                            {
                                // We should never actually get to this state for file-addition mods.  If we do, uh.. I guess correct the indexes and yolo?
                                // ( Only way we get here is if SE added a new file at the same name as a file the user had created via modding, in which case, it's technically no longer a file addition mod )
                                index.SetDataOffset(mod.fullPath, mod.data.originalOffset);

                                index1Value = mod.data.originalOffset;
                                index2Value = mod.data.originalOffset;

                                mod.enabled = false;
                            }

                            // Set it to the corrected state.
                            if (index1Value == mod.data.modOffset)
                            {
                                mod.enabled = true;
                            }
                            else
                            {
                                mod.enabled = false;
                            }

                            // Perform a basic file type check on our results.
                            var fileType = _dat.GetFileType(mod.data.modOffset, IOUtil.GetDataFileFromPath(mod.fullPath));
                            var originalFileType = _dat.GetFileType(mod.data.modOffset, IOUtil.GetDataFileFromPath(mod.fullPath));

                            if (!validTypes.Contains(fileType) || mod.data.modOffset == 0)
                            {
                                // Mod data is busted.  Fun.
                                toRemove.Add(mod);
                            }

                            if ((!validTypes.Contains(originalFileType)) || mod.data.originalOffset == 0)
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
                                    throw new Exception("Unable to determine working offset for file:".L() + mod.fullPath);
                                }
                            }
                        }

                        // Okay, this mod is now represented in the modlist in it's actual post-patch index state.
                        var datNum = (int)((mod.data.modOffset / 8) & 0x0F) / 2;
                        var dat = XivDataFiles.GetXivDataFile(mod.datFile);

                        var originalDats = await _dat.GetUnmoddedDatList(dat);
                        var datPath = Dat.GetDatPath(dat, datNum);

                        // Test for SE Dat file rollover.
                        if (originalDats.Contains(datPath))
                        {
                            // Shit.  This means that the dat file where this mod lived got eaten by SE.  We have to destroy the modlist entry at this point.
                            toRemove.Add(mod);
                        }
                    }

                    await ModTransaction.CommitTransaction(tx);

                    // Internal files always need to be purged and rebuilt.
                    var internalFiles = modList.Mods.Where(x => x.IsInternal());
                    toRemove.AddRange(internalFiles);
                }

                // We now need to clear out any mods that are irreparably fucked, and clear out all of our
                // internal data files so we can rebuild them later.
                if (toRemove.Count > 0)
                {
                    using (var tx = ModTransaction.BeginTransaction())
                    {
                        var modList = await tx.GetModList();

                        var removedString = "";

                        // Soft-Disable all metadata mods, since we're going to purge their internal file entries.
                        var metadata = modList.Mods.Where(x => x.fullPath.EndsWith(".meta") || x.fullPath.EndsWith(".rgsp"));
                        foreach (var mod in metadata)
                        {
                            var df = IOUtil.GetDataFileFromPath(mod.fullPath);
                            await modding.ToggleModUnsafe(false, mod, true, false, tx);
                        }

                        foreach (var mod in toRemove)
                        {
                            if (mod.data.modOffset == 0 || mod.data.originalOffset == 0)
                            {
                                if (mod.data.originalOffset == 0 && mod.enabled)
                                {
                                    // This is awkward.  We have a mod whose data got bashed, but has no valid original offset to restore.
                                    // So the indexes here are fucked if we do, fucked if we don't.
                                    throw new Exception("Patch-Broken file has no valid index to restore.  Clean Index Restoration required.".L());
                                }

                                modList.RemoveMod(mod);
                                enabledMods.Remove(mod);
                                removedString += mod.fullPath + "\n";
                            }
                            else
                            {

                                if (mod.enabled)
                                {
                                    var df = IOUtil.GetDataFileFromPath(mod.fullPath);
                                    await modding.ToggleModUnsafe(false, mod, true, false, tx);
                                }

                                modList.RemoveMod(mod);

                                // Since we're deleting this entry entirely, we can't leave it in the other cached list either to get re-enabled later.
                                enabledMods.Remove(mod);

                                if (!mod.IsInternal())
                                {
                                    removedString += mod.fullPath + "\n";
                                }
                            }
                        }

                        await ModTransaction.CommitTransaction(tx);

                        // Show the user a message if we purged any real files.
                        if (toRemove.Any(x => !String.IsNullOrEmpty(x.fullPath) && !x.IsInternal()))
                        {
                            var text = String.Format(UIMessages.PatchDestroyedFiles, removedString);

                            FlexibleMessageBox.Show(_mainWindow.Win32Window, text, "Destroyed Files Notification".L(), MessageBoxButtons.OK, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button1);
                        }
                    }
                }

                // Always create clean index backups after this process is completed.
                _mainWindow.LockProgress.Report("Disabling Mods...".L());
                await modding.ToggleAllMods(false);

                await BackupIndexFiles();

                // Now restore the modlist enable/disable state back to how the user had it before.
                _mainWindow.LockProgress.Report("Re-Enabling mods...");

                // Re-enable things.
                await modding.ToggleMods(true, enabledMods.Select(x => x.fullPath));

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
            var pc = new ProblemChecker(_gameDirectory);
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
            if (_index.IsIndexLocked(XivDataFile._0A_Exd))
            {
                FlexibleMessageBox.Show(UIMessages.IndexLockedErrorMessage, UIMessages.IndexLockedErrorTitle, MessageBoxButtons.OK, MessageBoxIcon.Error);

                return;
            }

            _progressController = await _mainWindow.ShowProgressAsync(UIMessages.EnablingModsTitle, UIMessages.PleaseWaitMessage);
            var progressIndicator = new Progress<(int current, int total, string message)>(ReportProgress);

            if (FlexibleMessageBox.Show(
                    UIMessages.EnableAllModsMessage, UIMessages.EnablingModsTitle, MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
            {
                var modding = new Modding(_gameDirectory);
                bool err = false;
                try
                {
                    await modding.ToggleAllMods(true, progressIndicator);
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
            if (_index.IsIndexLocked(XivDataFile._0A_Exd))
            {
                FlexibleMessageBox.Show(UIMessages.IndexLockedErrorMessage, UIMessages.IndexLockedErrorTitle, MessageBoxButtons.OK, MessageBoxIcon.Error);

                return;
            }

            _progressController = await _mainWindow.ShowProgressAsync(UIMessages.DisablingModsTitle, UIMessages.PleaseWaitMessage);
            var progressIndicator = new Progress<(int current, int total, string message)>(ReportProgress);

            if (FlexibleMessageBox.Show(
                    UIMessages.DisableAllModsMessage, UIMessages.DisableAllModsTitle, MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
            {
                var modding = new Modding(_gameDirectory);
                bool err = false;
                try { 
                    await modding.ToggleAllMods(false, progressIndicator);
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