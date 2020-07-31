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
using Application = System.Windows.Application;
using xivModdingFramework.Cache;
using xivModdingFramework.Mods;

namespace FFXIV_TexTools.Views
{
    /// <summary>
    /// Interaction logic for ProblemCheckView.xaml
    /// </summary>
    public partial class ProblemCheckView
    {
        private ProblemChecker _problemChecker;
        private DirectoryInfo _gameDirectory;
        private List<XivDataFile> _indexDatRepairList = new List<XivDataFile>();
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

            _problemChecker = new ProblemChecker(_gameDirectory);

            RunChecks();
        }

        private async void RunChecks()
        {
            var index = new Index(_gameDirectory);

            IProgress<(int current, int total)> progress = new Progress<(int current, int total)>((update) =>
            {
                ProgressBar.Value = (((float) update.current / (float) update.total) * 100);
                ProgressLabel.Content = $"{update.current} / {update.total}";
            });

            AddText($"{UIStrings.ProblemCheck_Initialize}\n\n", textColor);

            AddText($"{UIStrings.ProblemCheck_IndexDat}\n", secondaryTextColor);

            if (await CheckIndexDatCounts())
            {
                AddText($"\n{UIStrings.ProblemCheck_ErrorsFound}\n", secondaryTextColor);
                if (!index.IsIndexLocked(XivDataFile._0A_Exd))
                {
                    await FixIndexDatCounts();
                    AddText($"{UIStrings.ProblemCheck_RepairComplete}\n", "Green");
                    await CheckIndexDatCounts();
                }
                else
                {
                    AddText($"\n{UIStrings.ProblemCheck_IndexLocked} \n", "Red");
                }
            }

            AddText($"\n{UIStrings.ProblemCheck_IndexBackups}\n", secondaryTextColor);
            await CheckBackups();

            AddText($"\n{UIStrings.ProblemCheck_DatSize}\n", secondaryTextColor);
            await CheckDatSizes();

            try
            {
                AddText($"\n{UIStrings.ProblemCheck_ModList}\n", secondaryTextColor);
                await CheckMods(progress);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Loading Canceled\n\n{ex.Message}");
            }


            try
            {
                AddText($"\n{UIStrings.ProblemCheck_LoD}\n", secondaryTextColor);
                cfpTextBox.ScrollToEnd();
                await CheckLoD();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Loading Canceled\n\n{ex.Message}");
            }

            ProgressBar.Value = 0;
            ProgressLabel.Content = UIStrings.Done;
        }

        /// <summary>
        /// Checks the dat counts in the index file
        /// </summary>
        /// <returns>Flag for problem found</returns>
        private async Task<bool> CheckIndexDatCounts()
        {
            var problemFound = false;

            var filesToCheck = new XivDataFile[]
                {XivDataFile._0A_Exd, XivDataFile._01_Bgcommon, XivDataFile._04_Chara, XivDataFile._06_Ui};

            foreach (var file in filesToCheck)
            {
                AddText($"\t{file.GetDataFileName()} Index Files", textColor);

                try
                {
                    var result = await _problemChecker.CheckIndexDatCounts(file);

                    if (result)
                    {
                        _indexDatRepairList.Add(file);
                        AddText("\t\u2716\t", "Red");
                        problemFound = true;
                    }
                    else
                    {
                        AddText("\t\u2714\t", "Green");
                    }

                    result = await _problemChecker.CheckForLargeDats(file);

                    if (result)
                    {
                        AddText($"\t\u2716\n{UIStrings.ProblemCheck_ExtraDats}\n", "Red");
                        problemFound = true;
                    }
                    else
                    {
                        AddText("\t\u2714\n", "Green");
                    }
                }
                catch (Exception ex)
                {
                    FlexibleMessageBox.Show(
                        $"{UIMessages.ProblemCheckDatIssueMessage}\n{ex.Message}", UIMessages.ProblemCheckErrorTitle,
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }

            return problemFound;
        }

        /// <summary>
        /// Fixes the dat counts in the index files
        /// </summary>
        private async Task FixIndexDatCounts()
        {
            foreach (var xivDataFile in _indexDatRepairList)
            {
                await _problemChecker.RepairIndexDatCounts(xivDataFile);
            }
        }


        private async Task CheckDatSizes()
        {
            var filesToCheck = new XivDataFile[]
                {XivDataFile._0A_Exd, XivDataFile._01_Bgcommon, XivDataFile._04_Chara, XivDataFile._06_Ui};

            foreach (var file in filesToCheck)
            {
                AddText($"\t{file.GetDataFileName()} Dat Files", textColor);

                try
                {
                    var result = await _problemChecker.CheckForEmptyDatFiles(file);

                    if (result.Count > 0)
                    {
                        foreach (var datNum in result)
                        {
                            AddText($"\n\t{datNum} \t\u2716\t", "Red");
                            AddText($"\nFixing...", "Black");

                            File.Delete($"{_gameDirectory}\\{file.GetDataFileName()}.win32.dat{datNum}");

                            AddText($"\t\u2714\n", "Green");
                        }
                    }
                    else
                    {
                        AddText("\t\u2714\n", "Green");
                    }
                }
                catch (Exception ex)
                {
                    FlexibleMessageBox.Show(
                        $"{UIMessages.ProblemCheckDatIssueMessage}\n{ex.Message}", UIMessages.ProblemCheckErrorTitle,
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        /// <summary>
        /// Checks the mods for any problems
        /// </summary>
        private Task CheckMods(IProgress<(int current, int total)> progress)
        {
            var checkModsLock = new object();
            var addTextLock = new object();
            var modListDirectory =
                new DirectoryInfo(Path.Combine(_gameDirectory.Parent.Parent.FullName, XivStrings.ModlistFilePath));
            var modList = new ModList();

            try
            {
                var modding = new Modding(_gameDirectory);
                modList = modding.GetModList();

                // Someone somehow had their entire modlist filled with 0's causing the deserealization to 
                // just return null so this was added to still detect that as a corrupted modlist
                if (modList == null) throw new Exception("How did this even happen?");
            }
            catch
            {
                FlexibleMessageBox.Show(
                    $"{UIStrings.ProblemCheck_ErrorsFound}\n", "Corrupted ModList Detected",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);

                return Task.Run( async () =>
                {
                    var problemChecker = new ProblemChecker(_gameDirectory);
                    var indexBackupsDirectory = new DirectoryInfo(Settings.Default.Backup_Directory);
                    try
                    {
                        await problemChecker.PerformStartOver(indexBackupsDirectory, null, XivLanguages.GetXivLanguage(Properties.Settings.Default.Application_Language));

                        Dispatcher.Invoke(() => AddText("\t\u2714", "Green"));
                        Dispatcher.Invoke(() => AddText("\tModList restored", "Green"));
                    }
                    catch
                    {
                        Dispatcher.Invoke(() => AddText("\t\u2716", "Red"));
                        Dispatcher.Invoke(() => AddText($"\tModList Corrupted\n", "Red"));

                        FlexibleMessageBox.Show("Unable to repair TexTools\n\n" +
                            "Please manually update your index backups.", "Repair Failed",
                            MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                });                    
            }

            var dat = new Dat(_gameDirectory);

            // Filter out empty entries in the mod list
            modList.Mods.RemoveAll(mod => mod.name.Equals(string.Empty));

            return Task.Run(() =>
            {
                if (modList.modCount > 0)
                {
                    var modNum = 0;

                    Parallel.ForEach(modList.Mods, (mod) =>
                    {
                        if (cts.IsCancellationRequested)
                        {
                            cts.Token.ThrowIfCancellationRequested();
                            return;
                        }

                        lock (checkModsLock)
                        {
                            progress.Report((++modNum, modList.Mods.Count));
                        }

                        var fileName = Path.GetFileName(mod.fullPath);

                        var tabs = "";
                        if (fileName.Length < 21 && fileName.Length > 12)
                        {
                            tabs = "\t";
                        }
                        else if (fileName.Length < 12)
                        {
                            tabs = "\t\t";
                        }

                        lock (addTextLock)
                        {
                            Dispatcher.Invoke(() => AddText($"\t{fileName}{tabs}", textColor));

                            if (mod.data.originalOffset == 0)
                            {
                                Dispatcher.Invoke(() => AddText("\t\u2716\n", "Red"));
                                Dispatcher.Invoke(() => AddText($"\t{UIStrings.ProblemCheck_OriginalZero} \n", "Red"));
                            }
                            else if (mod.data.modOffset == 0)
                            {
                                Dispatcher.Invoke(() => AddText("\t\u2716\n", "Red"));
                                Dispatcher.Invoke(() => AddText($"\t{UIStrings.ProblemCheck_ModZero}\n", "Red"));
                            }
                            else
                            {
                                Dispatcher.Invoke(() => AddText("\t\u2714", "Green"));
                            }

                            var fileType = 0;
                            try
                            {
                                fileType = dat.GetFileType(mod.data.modOffset,
                                    XivDataFiles.GetXivDataFile(mod.datFile));
                            }
                            catch (Exception ex)
                            {
                                Dispatcher.Invoke(() => AddText("\t\u2716\n", "Red"));
                                Dispatcher.Invoke(() => AddText($"\tError: {ex.Message}\n", "Red"));
                            }

                            if (fileType != 2 && fileType != 3 && fileType != 4)
                            {
                                Dispatcher.Invoke(() => AddText("\t\u2716\n", "Red"));
                                Dispatcher.Invoke(() =>
                                    AddText($"\t{string.Format(UIStrings.ProblemCheck_UnkType, fileType)} [{mod.data.modOffset}, {((mod.data.modOffset / 8) & 0x0F) / 2}]\n", "Red"));
                            }
                            else
                            {
                                Dispatcher.Invoke(() => AddText("\t\u2714\n", "Green"));
                            }

                            Dispatcher.Invoke(() => cfpTextBox.ScrollToEnd());
                        }
                    });
                }
                else
                {
                    Dispatcher.Invoke(() => AddText($"\t{UIStrings.ProblemCheck_NoEntries}\n", "Orange"));
                }
            }, cts.Token);
        }

        /// <summary>
        /// Checks if LoD is on or off, and turns off if it is enabled
        /// </summary>
        private async Task CheckLoD()
        {
            var dir = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) +
                      "\\My Games\\FINAL FANTASY XIV - A Realm Reborn";

            var problem = false;

            if (Directory.Exists(dir))
            {
                var DX11 = await Task.Run(() =>
                {
                    var dx = false;

                    if (File.Exists($"{dir}\\FFXIV_BOOT.cfg"))
                    {
                        var lines = File.ReadAllLines($"{dir}\\FFXIV_BOOT.cfg");

                        foreach (var line in lines)
                        {
                            if (cts.IsCancellationRequested)
                            {
                                cts.Token.ThrowIfCancellationRequested();
                                return dx;
                            }

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
                }, cts.Token);


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
                            if (DX11 && line.Contains("DX11"))
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
                            else if (!DX11 && !line.Contains("DX11"))
                            {
                                if (val.Equals("1"))
                                {
                                    AddText($"\t{line.Substring(0, line.IndexOf("\t"))} ON\t", textColor);
                                    AddText("\u2716\n", "Red");
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
                        await CheckLoD();
                    }
                }
            }
        }

        private async Task CheckBackups()
        {
            var filesToCheck = new XivDataFile[] { XivDataFile._0A_Exd, XivDataFile._01_Bgcommon, XivDataFile._04_Chara, XivDataFile._06_Ui};

            var backupDirectory = new DirectoryInfo(Properties.Settings.Default.Backup_Directory);

            foreach (var file in filesToCheck)
            {
                AddText($"\t{file.GetDataFileName()} Index Files", textColor);

                try
                {
                    var backupFile =
                        new DirectoryInfo($"{backupDirectory.FullName}\\{file.GetDataFileName()}.win32.index");

                    if (!File.Exists(backupFile.FullName))
                    {
                        AddText($"\t\u2716\n{UIStrings.ProblemCheck_NoBackup}\n", "Red");
                        continue;
                    }

                    var result = await _problemChecker.CheckForOutdatedBackups(file, backupDirectory);

                    if (!result)
                    {
                        AddText($"\t\u2716 {UIStrings.ProblemCheck_OutOfDate}\n", "Red");
                    }
                    else
                    {
                        AddText("\t\u2714\n", "Green");
                    }
                }
                catch (Exception ex)
                {
                    FlexibleMessageBox.Show(
                        $"{UIStrings.ProblemCheck_BackupError}\n{ex.Message}", "Problem Check Error",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
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

        }
    }
}
