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
using MahApps.Metro;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Forms;
using System.Windows.Media;
using xivModdingFramework.General.Enums;
using xivModdingFramework.Helpers;
using xivModdingFramework.Mods.DataContainers;
using xivModdingFramework.SqPack.FileTypes;
using Application = System.Windows.Application;

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

        public ProblemCheckView()
        {
            InitializeComponent();

            var appStyle = ThemeManager.DetectAppStyle(Application.Current);
            if (((AppTheme)appStyle.Item1).Name.Equals("BaseDark"))
            {
                textColor = "White";
            }

            _gameDirectory = new DirectoryInfo(Properties.Settings.Default.FFXIV_Directory);

            _problemChecker = new ProblemChecker(_gameDirectory);

            var index = new Index(_gameDirectory);

            AddText("Initializing Problem Check....\n\n", textColor);

            AddText("Checking Index Dat Values....\n", "Blue");

            if (CheckIndexDatCounts())
            {
                AddText("\nErrors found attempting to repair....\n", "Blue");
                if (!index.IsIndexLocked(XivDataFile._0A_Exd))
                {
                    FixIndexDatCounts();
                    AddText("Repairs Complete\n", "Green");
                    CheckIndexDatCounts();
                }
                else
                {
                    AddText("\nCannot run repairs with game open. Please exit the game and run Check For Problems again. \n", "Red");
                }
            }

            AddText("\nChecking Index Backups....\n", "Blue");
            CheckBackups();

            AddText("\nChecking Dat....\n", "Blue");
            CheckDat();

            AddText("\nChecking Modlist....\n", "Blue");
            CheckMods();

            AddText("\nChecking LoD settings....\n", "Blue");
            CheckLoD();
        }

        /// <summary>
        /// Checks the dat counts in the index file
        /// </summary>
        /// <returns>Flag for problem found</returns>
        private bool CheckIndexDatCounts()
        {
            var problemFound = false;

            var filesToCheck = new XivDataFile[] {XivDataFile._0A_Exd, XivDataFile._01_Bgcommon, XivDataFile._04_Chara, XivDataFile._06_Ui};

            foreach (var file in filesToCheck)
            {
                AddText($"\t{file.GetDataFileName()} Index Files", textColor);

                try
                {
                    var result = _problemChecker.CheckIndexDatCounts(file);

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

                    result = _problemChecker.CheckForLargeDats(file);

                    if (result)
                    {
                        AddText("\t\u2716\nExtra Dat files found, recommend Start Over\n", "Red");
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
                        $"There was an issue checking Index Dat Counts\n{ex.Message}", "Problem Check Error",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }

            return problemFound;
        }

        /// <summary>
        /// Fixes the dat counts in the index files
        /// </summary>
        private void FixIndexDatCounts()
        {
            foreach (var xivDataFile in _indexDatRepairList)
            {
                _problemChecker.RepairIndexDatCounts(xivDataFile);
            }
        }

        private void CheckDat()
        {
            var fileInfo = new FileInfo($"{_gameDirectory}\\{XivDataFile._06_Ui.GetDataFileName()}.win32.dat1");

            AddText($"\t{XivDataFile._06_Ui.GetDataFileName()} Dat1", textColor);

            if (fileInfo.Exists)
            {
                if (fileInfo.Length < 10000000)
                {
                    AddText("\t\u2716\n", "Red");
                    AddText("\tThe Dat File ( 060000.win32.dat1 ) is missing data. \n", "Red");
                }
                else
                {
                    AddText("\t\u2714\n", "Green");
                }
            }
            else
            {
                AddText("\t\u2716\n", "Red");
                AddText("\tThe Dat File ( 060000.win32.dat1 ) could not be found. \n", "Red");
            }


        }

        /// <summary>
        /// Checks the mods for any problems
        /// </summary>
        private void CheckMods()
        {
            var modListDirectory = new DirectoryInfo(Path.Combine(_gameDirectory.Parent.Parent.FullName, XivStrings.ModlistFilePath));

            var modList = JsonConvert.DeserializeObject<ModList>(File.ReadAllText(modListDirectory.FullName));

            var dat = new Dat(_gameDirectory);

            if (modList.modCount > 0)
            {
                foreach (var mod in modList.Mods)
                {
                    if (mod.name.Equals(string.Empty)) continue;

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

                    AddText($"\t{fileName}{tabs}", textColor);

                    if (mod.data.originalOffset == 0)
                    {
                        AddText("\t\u2716\n", "Red");
                        AddText("\tOriginal Offset was 0, you will be unable to revert to original, consider starting over. \n", "Red");
                    }
                    else if (mod.data.modOffset == 0)
                    {
                        AddText("\t\u2716\n", "Red");
                        AddText("\tMod Offset was 0, Disable from File > Modlist and reimport.\n", "Red");
                    }
                    else
                    {
                        AddText("\t\u2714", "Green");
                    }

                    var fileType = 0;
                    try
                    {
                        fileType = dat.GetFileType(mod.data.modOffset, XivDataFiles.GetXivDataFile(mod.datFile));
                    }
                    catch(Exception ex)
                    {
                        AddText("\t\u2716\n", "Red");
                        AddText($"\tError: {ex.Message}\n", "Red");
                    }


                    if (fileType != 2 && fileType != 3 && fileType != 4)
                    {
                        AddText("\t\u2716\n", "Red");
                        AddText($"\tFound unknown file type ( {fileType} ) offset is most likely corrupt.\n", "Red");
                    }
                    else
                    {
                        AddText("\t\u2714\n", "Green");
                    }
                }
            }
            else
            {
                AddText("\tNo entries found in modlist.\n", "Orange");
            }
        }

        /// <summary>
        /// Checks if LoD is on or off, and turns off if it is enabled
        /// </summary>
        private void CheckLoD()
        {
            var dir = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) + "\\My Games\\FINAL FANTASY XIV - A Realm Reborn";

            var problem = false;
            var DX11 = false;

            if (Directory.Exists(dir))
            {
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
                                DX11 = true;
                            }

                            break;
                        }
                    }
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
                            if (DX11 && line.Contains("DX11"))
                            {
                                if (val.Equals("1"))
                                {
                                    AddText($"\t{line.Substring(0, line.IndexOf("\t"))} ON\t", textColor);
                                    AddText("\u2716\n", "Red");

                                    AddText("\nCertain mods have issues with LoD ON.\n", "Orange");
                                    AddText("\tTurning off LoD...\n", textColor);
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

                        AddText("\tLoD OFF, running check...\n\n", textColor);
                        CheckLoD();
                    }
                }
            }
        }

        private void CheckBackups()
        {
            var filesToCheck = new XivDataFile[] { XivDataFile._01_Bgcommon, XivDataFile._04_Chara, XivDataFile._06_Ui };

            var backupDirectory = new DirectoryInfo(Properties.Settings.Default.Backup_Directory);

            foreach (var file in filesToCheck)
            {
                AddText($"\t{file.GetDataFileName()} Index Files", textColor);

                try
                {
                    var backupFile = new DirectoryInfo($"{backupDirectory.FullName}\\{file.GetDataFileName()}.win32.index");

                    if (!File.Exists(backupFile.FullName))
                    {
                        AddText("\t\u2716\nNo Backup Found.\n", "Red");
                        continue;
                    }

                    var result = _problemChecker.CheckForOutdatedBackups(file, backupDirectory);

                    if (!result)
                    {
                        AddText("\t\u2716 index out of date.\n", "Red");
                    }
                    else
                    {
                        AddText("\t\u2714\n", "Green");
                    }
                }
                catch (Exception ex)
                {
                    FlexibleMessageBox.Show(
                        $"There was an issue checking Backup Index Files\n{ex.Message}", "Problem Check Error",
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
            catch (FormatException) { }
        }

        /// <summary>
        /// Event handler for close button
        /// </summary>
        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
