using FFXIV_TexTools.Helpers;
using FFXIV_TexTools.Resources;
using FFXIV_TexTools.ViewModels;
using MahApps.Metro.Controls.Dialogs;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Input;
using xivModdingFramework.Helpers;
using xivModdingFramework.Mods;
using xivModdingFramework.Mods.DataContainers;
using xivModdingFramework.Mods.FileTypes;

namespace FFXIV_TexTools.Views
{
    /// <summary>
    /// Interaction logic for BackupModpackCreator.xaml
    /// </summary>
    public partial class BackupModPackCreator
    {
        private readonly DirectoryInfo _gameDirectory;
        private readonly ModList _modList;
        private ProgressDialogController _progressController;

        public BackupModPackCreator()
        {
            InitializeComponent();

            _gameDirectory = new DirectoryInfo(Properties.Settings.Default.FFXIV_Directory);
            var modding = new Modding(_gameDirectory);
            _modList = modding.GetModList();

            DataContext = new BackupModpackViewModel();
            ModpackList.ItemsSource = new List<BackupModpackItemEntry>();

            // Manually add an entry for the mods that don't belong to a modpack
            ((List<BackupModpackItemEntry>)ModpackList.ItemsSource).Add(new BackupModpackItemEntry(UIStrings.Standalone_Non_ModPack));

            foreach (var modpack in _modList.ModPacks)
            {
                var entry = new BackupModpackItemEntry(modpack.name);
                ((List<BackupModpackItemEntry>)ModpackList.ItemsSource).Add(entry);
            }

            ModpackList.SelectedIndex = 0;
        }

        #region Public Properties

        /// <summary>
        /// The mod pack file name
        /// </summary>
        public string ModPackFileName { get; set; }

        #endregion

        #region Event Handlers

        /// <summary>
        /// Event handler for when the select all button is clicked
        /// </summary>
        private void SelectAllButton_Click(object sender, RoutedEventArgs e)
        {
            foreach (var entry in (List<BackupModpackItemEntry>)ModpackList.ItemsSource)
            {
                entry.IsChecked = true;
            }
        }

        /// <summary>
        /// Event handler for when the clear selected button is clicked
        /// </summary>
        private void ClearSelectedButton_Click(object sender, RoutedEventArgs e)
        {
            foreach (var entry in (List<BackupModpackItemEntry>)ModpackList.ItemsSource)
            {
                entry.IsChecked = false;
            }
        }

        /// <summary>
        /// Event handler for when the cancel button is clicked
        /// </summary>
        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        /// <summary>
        /// Event handler for when the create modpack button is clicked, this method creates a modpack with "Backup dd-MM-yy" as its name
        /// </summary>
        private async void CreateModPackButton_Click(object sender, RoutedEventArgs e)
        {
            _progressController = await this.ShowProgressAsync(UIMessages.ModPackCreationMessage, UIMessages.PleaseStandByMessage);

            try
            {
                var backupModpackData = new BackupModPackData
                {
                    Name = string.Format("Backup {0}", DateTime.Now.Date.ToString("dd-MM-yy")),
                    ModsToBackup = new List<BackupModData>()
                };

                var selectedEntries = from modpack in (List<BackupModpackItemEntry>)ModpackList.ItemsSource
                                      where (modpack.IsChecked)
                                      select modpack;
                if (selectedEntries.Count() == 0) throw new Exception("No selected modpacks detected.");

                foreach (var modpackEntry in selectedEntries)
                {
                    ModPack selectedModpack = null;
                    IEnumerable<Mod> modsInModpack = new List<Mod>();

                    if (modpackEntry.ModpackName == UIStrings.Standalone_Non_ModPack)
                    {
                        modsInModpack = from mods in _modList.Mods
                                        where !mods.name.Equals(string.Empty) && mods.modPack == null
                                        select mods;
                    }
                    else
                    {
                        selectedModpack = _modList.ModPacks.First(modPack => modPack.name == modpackEntry.ModpackName);
                        modsInModpack = from mods in _modList.Mods
                                        where (mods.modPack != null && mods.modPack.name == selectedModpack.name)
                                        select mods;
                    }

                    foreach (var mod in modsInModpack)
                    {
                        var simpleModData = new SimpleModData
                        {
                            Name = mod.name,
                            Category = mod.category,
                            FullPath = mod.fullPath,
                            ModOffset = mod.data.modOffset,
                            ModSize = mod.data.modSize,
                            DatFile = mod.datFile
                        };

                        var backupModData = new BackupModData
                        {
                            SimpleModData = simpleModData,
                            ModPack = selectedModpack
                        };

                        backupModpackData.ModsToBackup.Add(backupModData);
                    }

                }

                var texToolsModPack = new TTMP(new DirectoryInfo(Properties.Settings.Default.ModPack_Directory), XivStrings.TexTools);

                string modPackPath = System.IO.Path.Combine(Properties.Settings.Default.ModPack_Directory, $"{backupModpackData.Name}.ttmp2");
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

                var progressIndicator = new Progress<(int current, int total, string message)>(ReportProgress);

                ModPackFileName = backupModpackData.Name;

                await texToolsModPack.CreateBackupModpack(backupModpackData, _gameDirectory, progressIndicator, overwriteModpack);
            }
            catch (Exception ex)
            {
                FlexibleMessageBox.Show("Failed to create modpack.\n\nError: " + ex.Message, "Modpack Creation Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            finally
            {
                await _progressController.CloseAsync();
            }

            DialogResult = true;
        }

        /// <summary>
        /// Event handler for when selection in the modpack list changes
        /// </summary>
        private void ModpackList_SelectionChanged(object sender, RoutedEventArgs e)
        {
            ModPack selectedModpack = null;
            List<Mod> modsInModpack = new List<Mod>();

            var selectedModpackName = ((BackupModpackItemEntry)ModpackList.SelectedItem).ModpackName;
            if (selectedModpackName == UIStrings.Standalone_Non_ModPack)
            {
                modsInModpack = (from mods in _modList.Mods
                                 where !mods.name.Equals(string.Empty) && mods.modPack == null
                                 select mods).ToList();
            }
            else
            {
                selectedModpack = _modList.ModPacks.First(modPack => modPack.name == selectedModpackName);
                modsInModpack = (from mods in _modList.Mods
                                 where (mods.modPack != null && mods.modPack.name == selectedModpack.name)
                                 select mods).ToList();
            }
            (DataContext as BackupModpackViewModel).UpdateDescription(selectedModpack, modsInModpack);
        }

        /// <summary>
        /// Event handler to open the browser when the modpack URL is clicked
        /// </summary>
        private void DescriptionModPackUrl_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            var url = IOUtil.ValidateUrl((DataContext as BackupModpackViewModel).DescriptionModpackUrl);
            if (url == null)
            {
                return;
            }

            Process.Start(new ProcessStartInfo(url));
            e.Handled = true;
        }

        #endregion

        #region Private Methods

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
    }
}
