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

using FFXIV_TexTools.Helpers;
using FFXIV_TexTools.Resources;
using MahApps.Metro.Controls.Dialogs;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Forms;
using System.Windows.Input;
using FFXIV_TexTools.Properties;
using xivModdingFramework.General.Enums;
using xivModdingFramework.Mods;
using xivModdingFramework.Mods.DataContainers;
using xivModdingFramework.Mods.FileTypes;
using xivModdingFramework.Textures.Enums;
using SysTimer = System.Timers;
using System.Diagnostics;

namespace FFXIV_TexTools.Views
{
    /// <summary>
    /// Interaction logic for SimpleModPackCreator.xaml
    /// </summary>
    public partial class SimpleModPackCreator
    {
        private ListSortDirection _lastDirection = ListSortDirection.Ascending;
        private ProgressDialogController _progressController;
        private readonly DirectoryInfo _gameDirectory;
        private int _modCount;
        private long _modSize;
        private SysTimer.Timer searchTimer = new SysTimer.Timer(300);

        [DllImport("Shlwapi.dll", CharSet = CharSet.Auto)]
        public static extern long StrFormatByteSize(long fileSize, [MarshalAs(UnmanagedType.LPTStr)] StringBuilder buffer, int bufferSize);

        public SimpleModPackCreator()
        {
            InitializeComponent();

            this.ContentArea.DataContext = this;

            _gameDirectory = new DirectoryInfo(Properties.Settings.Default.FFXIV_Directory);

            if (searchTimer == null)
            {
                searchTimer = new SysTimer.Timer(300);
            }

            searchTimer.Enabled = true;
            searchTimer.AutoReset = false;
            searchTimer.Elapsed += SearchTimerOnElapsed;

            Task.Run(Initialize);
        }

        #region Public Properties

        /// <summary>
        /// The mod pack file name
        /// </summary>
        public string ModPackFileName { get; private set; }

        public ObservableCollection<SimpleModPackEntries> Entries { get; set; } = new ObservableCollection<SimpleModPackEntries>();

        #endregion


        #region Private Methods
        /// <summary>
        /// Initializes the Mod List view
        /// </summary>
        private async void Initialize()
        {
            App.Current.Dispatcher.Invoke(() =>
            {
                ModListView.IsEnabled = false;
                ModSizeLabel.Content = UIStrings.Loading;
                ModPackAuthor.Text = Settings.Default.Default_Author;
            });

            await MakeSimpleDataList();

            App.Current.Dispatcher.Invoke(() =>
            {
                CollectionView cv = (CollectionView)CollectionViewSource.GetDefaultView(this.Entries);
                cv.SortDescriptions.Clear();
                cv.SortDescriptions.Add(new SortDescription(nameof(SimpleModPackEntries.Name), _lastDirection));

                ModSizeLabel.Content = "0";
                ModListView.IsEnabled = true;

                CollectionView view = (CollectionView)CollectionViewSource.GetDefaultView(this.Entries);
                view.Filter = NameFilter;
            });
        }
        /// <summary>
        /// filtering ModListView
        /// </summary>
        /// <param name="item"></param>
        /// <returns></returns>
        private bool NameFilter(object item)
        {
            if (string.IsNullOrEmpty(SearchTextBox.Text.Trim()))
                return true;

            string[] searchTerms = SearchTextBox.Text.Split('|');

            foreach (string searchTerm in searchTerms)
            {
                if ((item as SimpleModPackEntries).Name.IndexOf(searchTerm.Trim(), StringComparison.OrdinalIgnoreCase) >= 0) return true;
            }
            return false;
        }

        /// <summary>
        /// Filtering Text
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            searchTimer.Stop();
            searchTimer.Start();
        }

        private void SearchTimerOnElapsed(object sender, SysTimer.ElapsedEventArgs e)
        {
            Dispatcher.Invoke(UpdateFilter);
        }

        private void UpdateFilter()
        {
            CollectionViewSource.GetDefaultView(this.Entries).Refresh();
        }

        /// <summary>
        /// Creates the simple mod pack data list
        /// </summary>
        /// <returns>Task</returns>
        private async Task MakeSimpleDataList()
        {
            DirectoryInfo modListDirectory = new DirectoryInfo(Path.Combine(_gameDirectory.Parent.Parent.FullName, XivStrings.ModlistFilePath));
            Modding modding = new Modding(_gameDirectory);

            ModList modList = JsonConvert.DeserializeObject<ModList>(File.ReadAllText(modListDirectory.FullName));

            List<SimpleModPackEntries> entries = new List<SimpleModPackEntries>();
            foreach (Mod mod in modList.Mods)
            {
                SimpleModPackEntries entry = GetEntry(mod, modding);
                if (entry == null)
                    continue;

                entries.Add(entry);
            }

            await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
            {
                foreach (SimpleModPackEntries entry in entries)
                {
                    this.Entries.Add(entry);
                }
            });
        }

        private SimpleModPackEntries GetEntry(Mod mod, Modding modding)
        {
            if (mod.fullPath.Equals(string.Empty))
                return null;

            SimpleModPackEntries entry = new SimpleModPackEntries();

            entry.Name = mod.name;
            entry.Category = mod.category;

            XivRace race = GetRace(mod.fullPath);
            entry.Race = race.GetDisplayName();

            entry.Type = GetType(mod.fullPath);
            entry.Part = GetPart(mod.fullPath);
            entry.Num = GetNumber(mod.fullPath);
            entry.Map = GetMap(mod.fullPath);
            entry.Active = mod.enabled || mod.data.modOffset == mod.data.originalOffset;
            entry.ModEntry = mod;

            return entry;
        }

        /// <summary>
        /// Gets the race from the path
        /// </summary>
        /// <param name="modPath">The mod path</param>
        /// <returns>The race as XivRace</returns>
        private XivRace GetRace(string modPath)
        {
            XivRace xivRace = XivRace.All_Races;

            if (modPath.Contains("ui/") || modPath.Contains(".avfx"))
            {
                xivRace = XivRace.All_Races;
            }
            else if (modPath.Contains("monster"))
            {
                xivRace = XivRace.Monster;
            }
            else if (modPath.Contains("bgcommon"))
            {
                xivRace = XivRace.All_Races;
            }
            else if (modPath.Contains(".tex") || modPath.Contains(".mdl") || modPath.Contains(".atex"))
            {
                if (modPath.Contains("accessory") || modPath.Contains("weapon") || modPath.Contains("/common/"))
                {
                    xivRace = XivRace.All_Races;
                }
                else
                {
                    if (modPath.Contains("demihuman"))
                    {
                        xivRace = XivRace.DemiHuman;
                    }
                    else if (modPath.Contains("/v"))
                    {
                        string raceCode = modPath.Substring(modPath.IndexOf("_c") + 2, 4);
                        xivRace = XivRaces.GetXivRace(raceCode);
                    }
                    else
                    {
                        string raceCode = modPath.Substring(modPath.IndexOf("/c") + 2, 4);
                        xivRace = XivRaces.GetXivRace(raceCode);
                    }
                }
            }

            return xivRace;
        }


        /// <summary>
        /// Gets the number from the path
        /// </summary>
        /// <param name="modPath">The mod path</param>
        /// <returns>The number</returns>
        private string GetNumber(string modPath)
        {
            string number = "-";

            if (modPath.Contains("/human/") && modPath.Contains("/body/"))
            {
                string subString = modPath.Substring(modPath.LastIndexOf("/b") + 2, 4);
                number = int.Parse(subString).ToString();
            }

            if (modPath.Contains("/face/"))
            {
                string subString = modPath.Substring(modPath.LastIndexOf("/f") + 2, 4);
                number = int.Parse(subString).ToString();
            }

            if (modPath.Contains("decal_face"))
            {
                int length = modPath.LastIndexOf(".") - (modPath.LastIndexOf("_") + 1);
                string subString = modPath.Substring(modPath.LastIndexOf("_") + 1, length);

                number = int.Parse(subString).ToString();
            }

            if (modPath.Contains("decal_equip"))
            {
                string subString = modPath.Substring(modPath.LastIndexOf("_") + 1, 3);

                try
                {
                    number = int.Parse(subString).ToString();
                }
                catch
                {
                    if (modPath.Contains("stigma"))
                    {
                        number = "stigma";
                    }
                    else
                    {
                        number = "Error";
                    }
                }
            }

            if (modPath.Contains("/hair/"))
            {
                string t = modPath.Substring(modPath.LastIndexOf("/h") + 2, 4);
                number = int.Parse(t).ToString();
            }

            if (modPath.Contains("/tail/"))
            {
                string t = modPath.Substring(modPath.LastIndexOf("l/t") + 3, 4);
                number = int.Parse(t).ToString();
            }

            return number;
        }

        /// <summary>
        /// Gets the type from the path
        /// </summary>
        /// <param name="modPath">The mod path</param>
        /// <returns>The type</returns>
        private string GetType(string modPath)
        {
            string type = "-";

            if (modPath.Contains(".tex") || modPath.Contains(".mdl") || modPath.Contains(".atex"))
            {
                if (modPath.Contains("demihuman"))
                {
                    type = slotAbr[modPath.Substring(modPath.LastIndexOf("/") + 16, 3)];
                }

                if (modPath.Contains("/face/"))
                {
                    if (modPath.Contains(".tex"))
                    {
                        string fileName = Path.GetFileNameWithoutExtension(modPath);
                        try
                        {
                            type = FaceTypes[fileName.Substring(fileName.IndexOf("_") + 1, 3)];
                        } catch(Exception ex)
                        {
                            type = "Unknown"; 
                        }
                    }
                }

                if (modPath.Contains("/hair/"))
                {
                    if (modPath.Contains(".tex"))
                    {
                        string fileName = Path.GetFileNameWithoutExtension(modPath);
                        try
                        {
                            type = HairTypes[fileName.Substring(fileName.IndexOf("_") + 1, 3)];
                        } catch
                        {
                            type = "Unknown";
                        }
                    }
                }

                if (modPath.Contains("/vfx/"))
                {
                    type = "VFX";
                }

            }
            else if (modPath.Contains(".avfx"))
            {
                type = "AVFX";
            }

            return type;
        }

        /// <summary>
        /// Gets the part from the path
        /// </summary>
        /// <param name="modPath">The mod path</param>
        /// <returns>The part</returns>
        private string GetPart(string modPath)
        {
            string part = "-";
            string[] parts = new[] { "a", "b", "c", "d", "e", "f" };

            if (modPath.Contains("/equipment/"))
            {
                if(modPath.Contains("/texture/"))
                {
                    part = modPath.Substring(modPath.LastIndexOf("_") - 1, 1);
                    foreach(string letter in parts)
                    {
                        if (part == letter) return part;
                    }
                    return "a";
                }

                if(modPath.Contains("/material/"))
                {
                    return modPath.Substring(modPath.LastIndexOf("_") + 1, 1);
                }
            }

            return part;
        }


        /// <summary>
        /// Gets the map from the path
        /// </summary>
        /// <param name="modPath">The mod path</param>
        /// <returns>The map</returns>
        private string GetMap(string modPath)
        {
            XivTexType xivTexType = XivTexType.Other;

            if (modPath.Contains(".mdl"))
            {
                return "3D";
            }

            if (modPath.Contains(".mtrl"))
            {
                return "ColorSet";
            }

            if (modPath.Contains("ui/"))
            {
                string subString = modPath.Substring(modPath.IndexOf("/") + 1);
                return subString.Substring(0, subString.IndexOf("/"));
            }

            if (modPath.Contains("_s.tex") || modPath.Contains("skin_m"))
            {
                xivTexType = XivTexType.Specular;
            }
            else if (modPath.Contains("_d.tex"))
            {
                xivTexType = XivTexType.Diffuse;
            }
            else if (modPath.Contains("_n.tex"))
            {
                xivTexType = XivTexType.Normal;
            }
            else if (modPath.Contains("_m.tex"))
            {
                xivTexType = XivTexType.Multi;
            }
            else if (modPath.Contains(".atex"))
            {
                string atex = Path.GetFileNameWithoutExtension(modPath);
                return atex.Substring(0, 4);
            }
            else if (modPath.Contains("decal"))
            {
                xivTexType = XivTexType.Mask;
            }

            return xivTexType.ToString();
        }

        /// <summary>
        /// Updates the progress bar
        /// </summary>
        /// <param name="value">The progress value</param>
        private void ReportProgress((int current, int total, string message) report)
        {
            if (!report.message.Equals(string.Empty))
            {
                _progressController.SetMessage(report.message);
                _progressController.SetIndeterminate();
            }
            else
            {
                _progressController.SetMessage(
                    $"{UIMessages.TTMPGettingData} ({report.current} / {report.total})");

                double value = (double)report.current / (double)report.total;
                _progressController.SetProgress(value);
            }
        }

        #endregion


        #region Event Handlers

        /// <summary>
        /// The event handler for the mod list selection changed
        /// </summary>
        private void ModListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            foreach (SimpleModPackEntries modItem in e.AddedItems)
            {
                _modCount++;
                _modSize += modItem.ModEntry.data.modSize;
            }

            foreach (SimpleModPackEntries modItem in e.RemovedItems)
            {
                _modCount--;
                _modSize -= modItem.ModEntry.data.modSize;
            }

            StringBuilder byteFormatedString = new StringBuilder(32);
            StrFormatByteSize(_modSize, byteFormatedString, byteFormatedString.Capacity);

            ModCountLabel.Content = _modCount;
            ModSizeLabel.Content = byteFormatedString.ToString();

            CreateModPackButton.IsEnabled = ModListView.SelectedItems.Count > 0;
        }

        /// <summary>
        /// The event handler for create mod pack button clicked
        /// </summary>
        private async void CreateModPackButton_Click(object sender, RoutedEventArgs e)
        {
            if (ModPackName.Text.Equals(string.Empty))
            {
                if (FlexibleMessageBox.Show(new Wpf32Window(this),
                        UIMessages.DefaultModPackNameMessage,
                        UIMessages.NoNameFoundTitle, MessageBoxButtons.OKCancel, MessageBoxIcon.Warning) ==
                    System.Windows.Forms.DialogResult.OK)
                {
                    ModPackName.Text = "ModPack";
                }
                else
                {
                    return;
                }
            }

            char[] invalidChars = { '/', '\\', ':', '*', '?', '"', '<', '>', '|' };

            if (ModPackName.Text.IndexOfAny(invalidChars) >= 0)
            {
                if (FlexibleMessageBox.Show(new Wpf32Window(this),
                        UIMessages.InvalidCharacterModpackNameMessage,
                        UIMessages.InvalidCharacterModpackNameTitle, MessageBoxButtons.OK, MessageBoxIcon.Warning) ==
                    System.Windows.Forms.DialogResult.OK)
                {
                    return;
                }
            }

            string verString = ModPackVersion.Text.Replace("_", "0");

            // Replace commas with periods for different culture formats such as FR
            if (verString.Contains(","))
            {
                verString = verString.Replace(",", ".");
            }

            Version versionNumber = Version.Parse(verString);

            if (versionNumber.ToString().Equals("0.0.0"))
            {
                if (FlexibleMessageBox.Show(new Wpf32Window(this),
                        UIMessages.DefaultModPackVersionMessage,
                        UIMessages.NoVersionFoundTitle, MessageBoxButtons.OKCancel, MessageBoxIcon.Warning) ==
                    System.Windows.Forms.DialogResult.OK)
                {
                    versionNumber = new Version(1, 0, 0);
                }
                else
                {
                    return;
                }
            }

            if (ModPackAuthor.Text.Equals(string.Empty))
            {
                if (FlexibleMessageBox.Show(new Wpf32Window(this),
                        UIMessages.DefaultModPackAuthorMessage,
                        UIMessages.NoAuthorFoundTitle, MessageBoxButtons.OKCancel, MessageBoxIcon.Warning) ==
                    System.Windows.Forms.DialogResult.OK)
                {
                    ModPackAuthor.Text = "TexTools User";
                }
                else
                {
                    return;
                }
            }

            _progressController = await this.ShowProgressAsync(UIMessages.ModPackCreationMessage, UIMessages.PleaseStandByMessage);
            ModPackFileName = ModPackName.Text;

            TTMP texToolsModPack = new TTMP(new DirectoryInfo(Properties.Settings.Default.ModPack_Directory), XivStrings.TexTools);

            SimpleModPackData simpleModPackData = new SimpleModPackData
            {
                Name = ModPackName.Text,
                Author = ModPackAuthor.Text,
                Version = versionNumber,
                SimpleModDataList = new List<SimpleModData>()
            };

            foreach (SimpleModPackEntries simpleEntry in ModListView.SelectedItems)
            {
                SimpleModData simpleData = new SimpleModData
                {
                    Name = simpleEntry.Name,
                    Category = simpleEntry.Category,
                    FullPath = simpleEntry.ModEntry.fullPath,
                    ModOffset = simpleEntry.ModEntry.data.modOffset,
                    ModSize = simpleEntry.ModEntry.data.modSize,
                    DatFile = simpleEntry.ModEntry.datFile,
                    
                };

                simpleModPackData.SimpleModDataList.Add(simpleData);
            }

            Progress<(int current, int total, string message)> progressIndicator = new Progress<(int current, int total, string message)>(ReportProgress);

            string modPackPath = Path.Combine(Properties.Settings.Default.ModPack_Directory, $"{simpleModPackData.Name}.ttmp2");
            bool overwriteModpack = false;

            if (File.Exists(modPackPath))
            {
                DialogResult overwriteDialogResult = FlexibleMessageBox.Show(new Wpf32Window(this), UIMessages.ModPackOverwriteMessage,
                                            UIMessages.OverwriteTitle, MessageBoxButtons.YesNoCancel, MessageBoxIcon.Warning);
                if (overwriteDialogResult == System.Windows.Forms.DialogResult.Yes)
                {
                    overwriteModpack = true;
                }
                else if (overwriteDialogResult == System.Windows.Forms.DialogResult.Cancel)
                {
                    await _progressController.CloseAsync();
                    return;
                }
            }

            await texToolsModPack.CreateSimpleModPack(simpleModPackData, _gameDirectory, progressIndicator, overwriteModpack);

            await _progressController.CloseAsync();

            DialogResult = true;
        }

        /// <summary>
        /// The event handler when clicking on the mod pack version text box
        /// </summary>
        /// <remarks>
        /// This sets the caret to the start of the text box
        /// </remarks>
        private void ModPackVersion_PreviewMouseUp(object sender, MouseButtonEventArgs e)
        {
            ModPackVersion.CaretIndex = 0;
        }

        /// <summary>
        /// The event handler for the select all button clicked
        /// </summary>
        private void SelectAllButton_Click(object sender, RoutedEventArgs e)
        {
            ModListView.SelectAll();
            ModListView.Focus();
        }

        /// <summary>
        /// The event handler for the select active button clicked
        /// </summary>
        private void SelectActiveButton_Click(object sender, RoutedEventArgs e)
        {
            foreach(SimpleModPackEntries entry in this.Entries)
            {
                entry.IsSelected = entry.Active;
            }

            ModListView.Focus();
        }


        /// <summary>
        /// The event handler for the clear selected button clicked
        /// </summary>
        private void ClearSelectedButton_Click(object sender, RoutedEventArgs e)
        {
            ModListView.UnselectAll();

            foreach (SimpleModPackEntries entry in this.Entries)
            {
                entry.IsSelected = false;
            }

            ModListView.Focus();
        }

        /// <summary>
        /// The event handler for the cancel button clicked
        /// </summary>
        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }


        /// <summary>
        /// The event handler for one of the headers in thea list being clicked
        /// </summary>
        private void Header_Click(object sender, RoutedEventArgs e)
        {
            _lastDirection = _lastDirection == ListSortDirection.Ascending ? ListSortDirection.Descending : ListSortDirection.Ascending;

            if (e.OriginalSource is GridViewColumnHeader h && !h.Content.ToString().Equals("_"))
            {
                CollectionView cv = (CollectionView)CollectionViewSource.GetDefaultView(ModListView.ItemsSource);
                cv.SortDescriptions.Clear();
                cv.SortDescriptions.Add(new SortDescription(h.Content.ToString(), _lastDirection));
            }
        }
        #endregion

        private static readonly Dictionary<string, string> FaceTypes = new Dictionary<string, string>
        {
            {"fac", XivStrings.Face},
            {"iri", XivStrings.Iris},
            {"etc", XivStrings.Etc},
            {"acc", XivStrings.Accessory}
        };

        private static readonly Dictionary<string, string> HairTypes = new Dictionary<string, string>
        {
            {"acc", XivStrings.Accessory},
            {"hir", XivStrings.Hair},
        };

        private static readonly Dictionary<string, string> slotAbr = new Dictionary<string, string>
        {
            {"met", XivStrings.Head},
            {"glv", XivStrings.Hands},
            {"dwn", XivStrings.Legs},
            {"sho", XivStrings.Feet},
            {"top", XivStrings.Body},
            {"ear", XivStrings.Ears},
            {"nek", XivStrings.Neck},
            {"rir", XivStrings.Ring_Right},
            {"ril", XivStrings.Ring_Left},
            {"wrs", XivStrings.Wrists},
        };
    }
}
