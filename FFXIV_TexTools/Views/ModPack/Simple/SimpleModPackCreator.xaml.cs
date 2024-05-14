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
using SharpDX.Direct2D1;
using xivModdingFramework.Cache;
using xivModdingFramework.SqPack.FileTypes;
using xivModdingFramework.Helpers;
using System.Linq;
using xivModdingFramework.Mods.Enums;

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
        private long _modSize;

        public Dictionary<string, List<string>> ParentsDictionary;
        public ModTransaction Transaction;
        public long ModpackSize { 
            get
            {
                return _modSize;
            }
            set {
                _modSize = value;

                StringBuilder byteFormatedString = new StringBuilder(32);
                StrFormatByteSize(_modSize, byteFormatedString, byteFormatedString.Capacity);

                ModCountLabel.Content = SelectedMods.Count;
                ModSizeLabel.Content = byteFormatedString.ToString();

                CreateModPackButton.IsEnabled = SelectedMods.Count > 0;
            }
        }

        private SysTimer.Timer searchTimer = new SysTimer.Timer(300);

        [DllImport("Shlwapi.dll", CharSet = CharSet.Auto)]
        public static extern long StrFormatByteSize(long fileSize, [MarshalAs(UnmanagedType.LPTStr)] StringBuilder buffer, int bufferSize);


        #region Public Properties

        /// <summary>
        /// The mod pack file name
        /// </summary>
        public string ModPackFileName { get; private set; }

        public ObservableCollection<SimpleModpackEntry> Entries { get; set; } = new ObservableCollection<SimpleModpackEntry>();
        public ModList ModList;

        // List of selected mods, by index.
        public HashSet<string> SelectedMods;

        #endregion

        public SimpleModPackCreator()
        {
            Transaction = MainWindow.DefaultTransaction;
            InitializeComponent();
            SelectedMods = new HashSet<string>();

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
                cv.SortDescriptions.Add(new SortDescription(nameof(SimpleModpackEntry.ItemName), _lastDirection));

                ModSizeLabel.Content = "0";
                ModListView.IsEnabled = true;

                CollectionView view = (CollectionView)CollectionViewSource.GetDefaultView(this.Entries);
                view.Filter = NameFilter;
            });
        }
        /// <summary>
        /// Filters the list view.  This is called on every item in the list in sequence when triggered.
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
                if ((item as SimpleModpackEntry).ItemName.IndexOf(searchTerm.Trim(), StringComparison.OrdinalIgnoreCase) >= 0) return true;
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

            var tx = MainWindow.DefaultTransaction;
            this.ModList = await tx.GetModList();

            // Don't show or list internal mods at all in this menu.
            var allMods = ModList.GetMods(x => !x.IsInternal()).ToList();

            // Rip through the mod list and get the correct raw compressed sizes for all the mods.
            var _dat = new Dat(XivCache.GameInfo.GameDirectory);

            this.ParentsDictionary = await XivCache.GetModListParents(tx);


            List<SimpleModpackEntry> entries = new List<SimpleModpackEntry>();
            foreach(var m in allMods)
            { 
                SimpleModpackEntry entry = MakeEntry(m.FilePath);
                if (entry == null)
                    continue;

                entries.Add(entry);
            }

            await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
            {
                foreach (SimpleModpackEntry entry in entries)
                {
                    this.Entries.Add(entry);
                }
            });
        }

        private SimpleModpackEntry MakeEntry(string path)
        {
            var mod = this.ModList.Mods[path];

            SimpleModpackEntry entry = new SimpleModpackEntry(path, this);

            return entry;
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
            // This is when a property is simply clicked on - NOT when the checkbox is checked on.
            // So really we don't actually want to do anything here, since Items are 'unselected'
            // whenever they're filtered out of view.
        }

        /// <summary>
        /// The event handler for create mod pack button clicked
        /// </summary>
        private async void CreateModPackButton_Click(object sender, RoutedEventArgs e)
        {
            var _dat = new Dat(XivCache.GameInfo.GameDirectory);
            if (ModPackName.Text.Equals(string.Empty))
            {
                if (FlexibleMessageBox.Show(new Wpf32Window(this),
                        UIMessages.DefaultModPackNameMessage,
                        UIMessages.NoNameFoundTitle, MessageBoxButtons.OKCancel, MessageBoxIcon.Warning) ==
                    System.Windows.Forms.DialogResult.OK)
                {
                    ModPackName.Text = "ModPack".L();
                }
                else
                {
                    return;
                }
            }

            var tx = MainWindow.DefaultTransaction;

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
                    ModPackAuthor.Text = "TexTools User".L();
                }
                else
                {
                    return;
                }
            }

            _progressController = await this.ShowProgressAsync(UIMessages.ModPackCreationMessage, UIMessages.PleaseStandByMessage);
            ModPackFileName = ModPackName.Text;

            TTMP texToolsModPack = new TTMP(new DirectoryInfo(Properties.Settings.Default.ModPack_Directory));

            SimpleModPackData simpleModPackData = new SimpleModPackData
            {
                Name = ModPackName.Text,
                Author = ModPackAuthor.Text,
                Version = versionNumber,
                SimpleModDataList = new List<SimpleModData>()
            };

            foreach (var idx in SelectedMods)
            {
                var mod = ModList.Mods[idx];
                var compressedSize = mod.FileSize;
                try
                {
                    // Use the explicit offset here, because we want to include the mod data even if the mod is disabled.
                    compressedSize = await tx.GetCompressedFileSize(mod.DataFile, mod.ModOffset8x);
                } catch
                {
                    // Don't allow creation of modpacks with broken files.
                    throw new Exception("Unable to determine compressed file size of modded file: " + mod.FilePath);
                }

                SimpleModData simpleData = new SimpleModData
                {
                    Name = mod.ItemName,
                    Category = mod.ItemCategory,
                    FullPath = mod.FilePath,
                    ModOffset = mod.ModOffset8x,
                    ModSize = compressedSize,
                    DatFile = mod.DataFile.ToString()
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

            await texToolsModPack.CreateSimpleModPack(simpleModPackData, progressIndicator, overwriteModpack);

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


        private bool MatchesFilter(Mod item)
        {

            if (string.IsNullOrEmpty(SearchTextBox.Text.Trim()))
                return true;

            string[] searchTerms = SearchTextBox.Text.Split('|');

            foreach (string searchTerm in searchTerms)
            {
                if (SimpleModpackEntry.GetFancyName(item.ItemName, item.FilePath).IndexOf(searchTerm.Trim(), StringComparison.OrdinalIgnoreCase) >= 0) return true;
            }
            return false;
        }

        /// <summary>
        /// The event handler for the select all button clicked
        /// </summary>
        private void SelectAllButton_Click(object sender, RoutedEventArgs e)
        {

            long addedSize = 0;
            foreach(var mod in ModList.GetMods())
            {
                // Ignore things not in the filter.
                if (!MatchesFilter(mod)) continue;

                // Ignore things that are already selected.
                if (SelectedMods.Contains(mod.FilePath)) continue;

                SelectedMods.Add(mod.FilePath);
                addedSize += mod.FileSize;
            }
            ModpackSize += addedSize;


            foreach (SimpleModpackEntry entry in this.Entries)
            {
                entry.MarkDirty();
            }
            ModListView.Focus();
        }

        /// <summary>
        /// The event handler for the select active button clicked
        /// </summary>
        private async void SelectActiveButton_Click(object sender, RoutedEventArgs e)
        {
            long addedSize = 0;
            var tx = MainWindow.DefaultTransaction;
            foreach (var mod in ModList.GetMods())
            {
                // Ignore disabled mods
                if (await mod.GetState(tx) != EModState.Enabled) continue;

                // Ignore things not in the filter.
                if (!MatchesFilter(mod)) continue;

                // Ignore things that are already selected.
                if (SelectedMods.Contains(mod.FilePath)) continue;

                SelectedMods.Add(mod.FilePath);
                addedSize += mod.FileSize;
            }
            ModpackSize += addedSize;


            // Entries CANNOT be iterrated to add values.  Unloaded entries have
            // totally default values.
            // They can, however, be iterrated to mark them dirty and have the UI update.
            foreach (SimpleModpackEntry entry in this.Entries)
            {
                entry.MarkDirty();
            }
            ModListView.Focus();
        }


        /// <summary>
        /// The event handler for the clear selected button clicked
        /// </summary>
        private async void ClearSelectedButton_Click(object sender, RoutedEventArgs e)
        {
            long removedSize = 0;
            var tx = MainWindow.DefaultTransaction;
            foreach (var mod in ModList.GetMods())
            {
                // Ignore disabled mods
                if (await mod.GetState(tx) != EModState.Enabled) continue;

                // Ignore things not in the filter.
                if (!MatchesFilter(mod)) continue;

                // Ignore things that are already selected.
                if (SelectedMods.Contains(mod.FilePath)) continue;

                SelectedMods.Remove(mod.FilePath);
                removedSize += mod.FileSize;
            }
            ModpackSize -= removedSize;

            foreach (SimpleModpackEntry entry in this.Entries)
            {
                entry.MarkDirty();
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

            if (e.OriginalSource is GridViewColumnHeader h && h.Content != null)
            {
                var binding = h.Column.DisplayMemberBinding;
                CollectionView cv = (CollectionView)CollectionViewSource.GetDefaultView(ModListView.ItemsSource);
                cv.SortDescriptions.Clear();

                var sortMember = "";
                if (h.Content.ToString() == UIStrings.ItemPlural)
                {
                    sortMember = "ItemName";
                }
                else if (h.Content.ToString() == UIStrings.FileName)
                {
                    sortMember = "FileName";
                }
                else if (h.Content.ToString() == UIStrings.Type)
                {
                    sortMember = "Type";
                }
                else if (h.Content.ToString() == UIStrings.Race)
                {
                    sortMember = "Race";
                }
                else if (h.Content.ToString() == UIStrings.Material)
                {
                    sortMember = "Material";
                }
                else if (h.Content.ToString() == UIStrings.Active)
                {
                    sortMember = "ActiveText";
                }

                cv.SortDescriptions.Add(new SortDescription(sortMember, _lastDirection));

                // Item Name -> File Name is always the tiebreaker.
                cv.SortDescriptions.Add(new SortDescription("ItemName", _lastDirection));
                cv.SortDescriptions.Add(new SortDescription("FileName", _lastDirection));
            }
        }
        #endregion
    }
}
