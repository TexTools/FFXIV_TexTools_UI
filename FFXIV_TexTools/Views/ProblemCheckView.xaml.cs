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

using FFXIV_TexTools.Helpers;
using FFXIV_TexTools.Resources;
using FFXIV_TexTools.Properties;
using MahApps.Metro;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Forms;
using System.Windows.Media;
using xivModdingFramework.General.Enums;
using xivModdingFramework.Helpers;
using xivModdingFramework.Mods.DataContainers;
using xivModdingFramework.SqPack.FileTypes;
using xivModdingFramework.Cache;
using xivModdingFramework.Mods;
using System.Runtime.CompilerServices;
using System.Linq;
using FFXIV_TexTools.ViewModels;
using xivModdingFramework.SqPack.DataContainers;

using Application = System.Windows.Application;
using xivModdingFramework.Mods.Enums;
using System.CodeDom;

namespace FFXIV_TexTools.Views
{
    /// <summary>
    /// Interaction logic for ProblemCheckView.xaml
    /// </summary>
    public partial class ProblemCheckView
    {
        private DirectoryInfo _gameDirectory;
        private string textColor = "Black";
        private string secondaryTextColor = "Blue";
        private CancellationTokenSource cts = new CancellationTokenSource();

        public ProblemCheckView()
        {
            InitializeComponent();

            var appStyle = ThemeManager.DetectAppStyle(Application.Current);
            if (((AppTheme) appStyle.Item1).Name.Equals("BaseDark"))
            {
                textColor = "White";
                secondaryTextColor = "LightBlue";
            }

            _gameDirectory = new DirectoryInfo(Properties.Settings.Default.FFXIV_Directory);


        }

        public async Task RunChecks()
        {
            bool started = false;
            try
            {
                if (!Dat.AllowDatAlteration)
                {
                    // A few of the constituent functions alter the DATs in the process of fixing known errors.
                    throw new Exception("Cannot run problem checker when DAT writing is disabled.");
                }

                started = true;

                IProgress<(int current, int total)> progress = new Progress<(int current, int total)>((update) =>
                {
                    ProgressBar.Value = (((float)update.current / (float)update.total) * 100);
                    ProgressLabel.Content = $"{update.current} / {update.total}";
                });

                AddText($"{UIStrings.ProblemCheck_Initialize}", textColor);
                AddText("\t\u2714", "Green");
                AddBreak();

                AddText($"Cleaning Index Files...", secondaryTextColor);
                await ProblemChecker.RevalidateAllIndexHashes();
                AddText("\t\u2714", "Green");
                AddBreak();

                AddText($"Removing Empty DAT Files...", secondaryTextColor);
                await CheckDatSizes();
                AddText("\t\u2714", "Green");
                AddBreak();

                AddText($"\n{UIStrings.ProblemCheck_ModList}\n", secondaryTextColor);
                await CheckMods(progress);


                AddText($"\n{UIStrings.ProblemCheck_LoD}\n", secondaryTextColor);
                cfpTextBox.ScrollToEnd();
                await CheckDXSettings();

                ProgressBar.Value = 0;
                ProgressLabel.Content = UIStrings.Done;
            } catch(Exception ex) {
                this.ShowError("Problem Checker Error", "An error occurred in the Problem Checker:\n\n" + ex.Message);
                if (!started)
                {
                    this.Close();
                }
            }
        }

        private async Task CheckDatSizes()
        {
            foreach (XivDataFile file in Enum.GetValues(typeof(XivDataFile)))
            {
                await Dat.RemoveEmptyDats(file);
            }
        }

        /// <summary>
        /// Checks the mods for any problems
        /// </summary>
        private async Task CheckMods(IProgress<(int current, int total)> progress)
        {
            var checkModsLock = new object();
            var addTextLock = new object();
            var modListDirectory =
                new DirectoryInfo(Path.Combine(_gameDirectory.Parent.Parent.FullName, XivStrings.ModlistFilePath));
            ModList modList;

            var tx = MainWindow.DefaultTransaction;
            try
            {
                modList = await tx.GetModList();

                // Someone somehow had their entire modlist filled with 0's causing the deserialization to 
                // just return null so this was added to still detect that as a corrupted modlist
                if (modList == null) throw new Exception("How did this even happen?".L());
            }
            catch
            {
                FlexibleMessageBox.Show(
                    $"{UIStrings.ProblemCheck_ErrorsFound}\n", "Corrupted ModList Detected".L(),
                    MessageBoxButtons.OK, MessageBoxIcon.Error);

                await Task.Run( async () =>
                {
                    var indexBackupsDirectory = new DirectoryInfo(Settings.Default.Backup_Directory);
                    try
                    {
                        await ProblemChecker.ResetAllGameFiles(indexBackupsDirectory, null);

                        Dispatcher.Invoke(() => AddText("\t\u2714", "Green"));
                        Dispatcher.Invoke(() => AddText("\tModList restored".L(), "Green"));
                    }
                    catch
                    {
                        Dispatcher.Invoke(() => AddText("\t\u2716", "Red"));
                        Dispatcher.Invoke(() => AddText($"\tModList Corrupted\n".L(), "Red"));

                        FlexibleMessageBox.Show("Unable to repair TexTools\n\nPlease manually update your index backups.".L(), "Repair Failed".L(),
                            MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                });
                return;
            }



            int resolvedErrors = 0;
            int unresolvedCriticalErrors = 0;
            int unresolvedWarnings = 0;

            await Task.Run(async () =>
            {
                var modNum = 0;
                if (modList.Mods.Count > 0)
                {
                    var files = modList.Mods.Keys.ToList();


                    using (var tx = ModTransaction.BeginTransaction(true))
                    {
                        modList = await tx.GetModList();
                        var allMods = modList.GetMods();
                        foreach (var mod in allMods)
                        {
                            bool index2CorrectionNeeded = false;
                            List<(string text, string color)> textsToAdd = new List<(string text, string color)>();
                            if (cts.IsCancellationRequested)
                            {
                                cts.Token.ThrowIfCancellationRequested();
                                return;
                            }

                            var df = IOUtil.GetDataFileFromPath(mod.FilePath);
                            var iFile = await tx.GetIndexFile(df);
                            long index1Offset = iFile.Get8xDataOffsetIndex1(mod.FilePath);
                            long index2Offset = iFile.Get8xDataOffsetIndex2(mod.FilePath);

                            var fileName = Path.GetFileName(mod.FilePath);

                            var tabs = "";
                            if (fileName.Length < 21 && fileName.Length > 12)
                            {
                                tabs = "\t";
                            }
                            else if (fileName.Length < 12)
                            {
                                tabs = "\t\t";
                            }

                            bool purgeMod = false;

                            textsToAdd.Add(($"\t{fileName}{tabs}", textColor));

                            if (mod.OriginalOffset8x < 0)
                            {
                                textsToAdd.Add(("\t\u2716\n", "Red"));
                                textsToAdd.Add(($"\tOriginal FFXIV Offset is Invalid.  Unrecoverable ModList state.\n\t Please use [Download Index Backups] =>  [Start Over].\n".L(), "Red"));
                                unresolvedCriticalErrors++;
                            }
                            else if (mod.ModOffset8x < 0)
                            {
                                textsToAdd.Add(("\t\u2716\n", "Red"));
                                textsToAdd.Add(($"\tMod Data Offset is invalid. \n\t The Mod will be disabled, deleted, and the mod slot will be purged from the ModList.\n".L(), "Red"));
                                purgeMod = true;
                            }
                            else
                            {
                                textsToAdd.Add(("\t\u2714", "Green"));
                            }

                            uint fileType = 0;
                            try
                            {
                                fileType = await tx.GetSqPackType(mod.DataFile, mod.ModOffset8x);
                            }
                            catch (Exception ex)
                            {
                                textsToAdd.Add(("\t\u2716\n", "Red"));
                                textsToAdd.Add(($"\tError: {ex.Message}\n", "Red"));
                            }

                            if (fileType != 2 && fileType != 3 && fileType != 4)
                            {
                                textsToAdd.Add(("\t\u2716\n", "Red"));
                                textsToAdd.Add(($"\t{string.Format(UIStrings.ProblemCheck_UnkType, fileType)} [{mod.ModOffset8x}, {((mod.ModOffset8x / 8) & 0x0F) / 2}]\n", "Red"));
                                textsToAdd.Add(($"\tThe Mod will automatically be disabled and deleted.\n".L(), "Red"));
                                purgeMod = true;
                            }
                            else
                            {
                                textsToAdd.Add(("\t\u2714", "Green"));
                            }

                            // If the file exists in both indexes, but has a DIFFERENT index in index2...
                            if (index1Offset != index2Offset && index2Offset != 0)
                            {

                                textsToAdd.Add(("\t\u2716\n", "Orange"));
                                textsToAdd.Add(($"Index 1/2 Mismatch: Index Values will be repaired.\n".L(), "Orange"));
                                index2CorrectionNeeded = true;
                            }
                            else
                            {
                                textsToAdd.Add(("\t\u2714", "Green"));
                            }

                            textsToAdd.Add(("\t\u2714\n", "Green"));




                            if (index2CorrectionNeeded)
                            {
                                try
                                {
                                    iFile.RepairIndexValue(mod.FilePath);
                                    resolvedErrors++;
                                }
                                catch(Exception ex)
                                {
                                    textsToAdd.Add((ex.Message, "Red"));
                                    textsToAdd.Add(($"Critical Error: Unable to Correct Index Discrepency for Mod: {mod.FilePath._()}\n\tPlease use [Download Index Backups] =>  [Start Over]".L(), "Red"));
                                    unresolvedCriticalErrors++;
                                }
                            }

                            if (purgeMod)
                            {
                                // Attempt to disable the mod.
                                try
                                {
                                    // Delete the Mod
                                    await Modding.DeleteMod(mod.FilePath, tx);
                                    resolvedErrors++;
                                }
                                catch
                                {
                                    textsToAdd.Add(($"Critical Error: Unable to Disable or Delete Mod: {mod.FilePath._()}\n\tPlease use [Download Index Backups] =>  [Start Over]".L(), "Red"));
                                    unresolvedCriticalErrors++;
                                }
                            }

                            await Dispatcher.InvokeAsync(() =>
                            {
                                progress.Report((++modNum, modList.Mods.Count));
                                foreach (var entry in textsToAdd)
                                {
                                    AddText(entry.text, entry.color);
                                }
                                cfpTextBox.ScrollToEnd();
                            });
                        }

                        // Commit the changes.
                        await ModTransaction.CommitTransaction(tx);
                    }
                }
                else
                {
                    Dispatcher.Invoke(() => AddText($"\t{UIStrings.ProblemCheck_NoEntries}\n", "Orange"));
                }

                await Dispatcher.InvokeAsync(() =>
                {
                    AddText($"Scanned {modNum._()} modded files.\n".L(), textColor);
                    if(unresolvedCriticalErrors > 0)
                    {
                        AddText($"\t{unresolvedCriticalErrors._()} Unresolved Critical Errors (May cause crashes in game)\n".L(), "Red");
                    }
                    if (unresolvedWarnings > 0)
                    {
                        AddText($"\t{unresolvedWarnings._()} Warnings (May cause invisible items/models in game)\n".L(), "Orange");
                    }

                    if(resolvedErrors > 0)
                    {
                        AddText($"\t{resolvedErrors._()} Resolved Errors (Mod files disabled/deleted)\n".L(), textColor);
                    }
                });
                }, cts.Token);
        }

        /// <summary>
        /// Checks if LoD is on or off, and turns off if it is enabled
        /// </summary>
        private async Task CheckDXSettings()
        {
            var dir = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) +
                      "\\My Games\\FINAL FANTASY XIV - A Realm Reborn";
            var problem = false;

            if (!Directory.Exists(dir))
            {
                dir = _gameDirectory + "\\..\\..\\My Games\\FINAL FANTASY XIV - A Realm Reborn";
            }

            if (Directory.Exists(dir))
            {

                var datSizeLimit = Dat.GetMaximumDatSize();
                double gb = ((double)datSizeLimit) / 1024D / 1024D / 1024D;
                string datSize = gb.ToString("0.00") + " GB";
                AddText($"\tPer-DAT File Size Limit: {datSize._()}\n".L(), textColor);

                var totalModSize = Dat.GetTotalModDataSize();
                gb = ((double)datSizeLimit) / 1024D / 1024D / 1024D;
                string modSize = gb.ToString("0.00") + " GB";
                AddText($"\tSum Total Mod Files Size: {modSize._()}\n".L(), textColor);

                var runningIn32bMode = IntPtr.Size == 4;
                if (runningIn32bMode)
                {
                    AddText($"TexTools is running in 32bit Mode. This will reduce the available mod data limit.\n".L(), "Orange");
                }

                if (File.Exists($"{dir}\\FFXIV.cfg"))
                {
                    var lines = File.ReadAllLines($"{dir}\\FFXIV.cfg");

                    var lineNum = 0;
                    var tmpLine = 0;
                    foreach (var line in lines)
                    {
                        if (line.Contains("LodType"))
                        {
                            var val = line.Substring(line.Length - 1, 1);
                            if (line.Contains("DX11"))
                            {
                                if (val.Equals("1"))
                                {
                                    AddText($"\t{line.Substring(0, line.IndexOf("\t"))} ON\t", textColor);
                                    AddText("\u2716\n", "Red");

                                    AddText($"\n{UIStrings.ProblemCheck_LoDIssue}\n", "Orange");
                                    AddText($"\t{UIStrings.ProblemCheck_LoDOff}\n", textColor);
                                    tmpLine = lineNum;
                                    problem = true;

                                    break;
                                }

                                AddText($"\t{line.Substring(0, line.IndexOf("\t"))} OFF\t", textColor);
                                AddText("\u2714\n", "Green");

                            }
                        }

                        lineNum++;
                    }

                    if (problem)
                    {
                        var line = lines[tmpLine];
                        line = line.Substring(0, line.Length - 1) + 0;

                        lines[tmpLine] = line;

                        File.WriteAllLines($"{dir}\\FFXIV.cfg", lines);

                        AddText($"\t{UIStrings.ProblemCheck_LoDOffDone}\n\n", textColor);
                        await CheckDXSettings();
                    }
                }
            }
            cfpTextBox.ScrollToEnd();
        }


        private void AddBreak()
        {
            AddText("\n", "Green");
        }


        /// <summary>
        /// Adds text to the text box
        /// </summary>
        /// <param name="text">The text to add</param>
        /// <param name="color">The color of the text</param>
        private void AddText(string text, string color)
        {
            var bc = new BrushConverter();
            var tr = new TextRange(cfpTextBox.Document.ContentEnd, cfpTextBox.Document.ContentEnd) {Text = text};
            try
            {
                tr.ApplyPropertyValue(TextElement.ForegroundProperty, bc.ConvertFromString(color));
            }
            catch (FormatException)
            {
            }
        }

        /// <summary>
        /// Event handler for close button
        /// </summary>
        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void MetroWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            cts?.Cancel();

            if (null != Owner)
            {
                Owner.Activate();
            }

        }
    }
}
