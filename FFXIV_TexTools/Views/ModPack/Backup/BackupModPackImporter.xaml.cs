using FFXIV_TexTools.Helpers;
using FFXIV_TexTools.Properties;
using FFXIV_TexTools.Resources;
using FFXIV_TexTools.ViewModels;
using MahApps.Metro.Controls.Dialogs;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Input;
using xivModdingFramework.Helpers;
using xivModdingFramework.Mods.DataContainers;
using xivModdingFramework.Mods.FileTypes;

namespace FFXIV_TexTools.Views
{
    /// <summary>
    /// Interaction logic for BackupModpackImporter.xaml
    /// </summary>
    public partial class BackupModPackImporter
    {
        private ProgressDialogController _progressController;
        private List<ModsJson> _modsJsons;
        private bool _messageInImport;
        private DirectoryInfo _modpackDirectory;
        private ModPackJson _modPackJson;

        public BackupModPackImporter(DirectoryInfo modPackDirectory, ModPackJson modPackJson, bool messageInImport = false)
        {
            InitializeComponent();

            MainWindow.MakeHighlander();

            _modpackDirectory = modPackDirectory;
            _modsJsons = modPackJson.SimpleModsList;
            _messageInImport = messageInImport;
            _modPackJson = modPackJson;

            DataContext = new BackupModpackViewModel();
            ModPackName.Content = modPackJson.Name;
            ModpackList.ItemsSource = new List<BackupModpackItemEntry>();

            MakeModpackList();
        }

        #region Public Properties
        public int TotalModsImported { get; private set; }
        public int TotalModsErrored { get; private set; }
        public float ImportDuration { get; private set; }

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
        /// Event handler for when the import modpack button is clicked
        /// </summary>
        private async void ImportModPackButton_Click(object sender, RoutedEventArgs e)
        {
            _progressController = await this.ShowProgressAsync(UIMessages.ModPackImportTitle, UIMessages.PleaseStandByMessage);

            try
            {
                var importList = new List<ModsJson>();

                var selectedModpackNames = (from modpack in (List<BackupModpackItemEntry>)ModpackList.ItemsSource
                                            where (modpack.IsChecked)
                                            select modpack).Select(entry => entry.ModpackName);

                // Separately add the mods that aren't associated to any modpacks if they are to be included in the backup
                if (selectedModpackNames.Contains(UIStrings.Standalone_Non_ModPack))
                {
                    importList.AddRange(from modsJson in _modsJsons
                                        where (modsJson.ModPackEntry == null)
                                        select modsJson);
                }

                importList.AddRange(from modsJson in _modsJsons
                                    where (selectedModpackNames.Contains(modsJson.ModPackEntry?.Name))
                                    select modsJson);


                var importResults = await Task.Run(async () =>
                {
                    var settings = ViewHelpers.GetDefaultImportSettings(_progressController);

                    // Limit the amount of extra actions on backup imports.
                    settings.RootConversionFunction = null;
                    settings.AutoAssignSkinMaterials = false;

                    return await TTMP.ImportModPackAsync(_modpackDirectory.FullName, importList, settings, MainWindow.UserTransaction);
                });

                if (importResults.Imported == null)
                {
                    // User cancelled or modpack had 0 items.
                    DialogResult = false;
                    return;
                }

                TotalModsImported = importResults.Imported.Count;
                TotalModsErrored = importResults.NotImported.Count;
                ImportDuration = importResults.Duration;
            }
            catch (Exception ex)
            {
                FlexibleMessageBox.Show(
                    $"{UIMessages.ErrorImportingModsMessage}\n\n{ex.Message}", UIMessages.ErrorImportingModsTitle,
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                await _progressController.CloseAsync();
            }

            if (_messageInImport)
            {
                var durationString = ImportDuration.ToString("0.00");
                await this.ShowMessageAsync(UIMessages.ImportCompleteTitle,
                    string.Format(UIMessages.SuccessfulImportCountMessage, TotalModsImported, TotalModsErrored, durationString));
            }

            MainWindow.GetMainWindow().ReloadItem();

            DialogResult = true;
        }

        /// <summary>
        /// Event handler for when the selection in the modpack list changes
        /// </summary>
        private void ModpackList_SelectionChanged(object sender, RoutedEventArgs e)
        {
            List<ModsJson> selectedModsJsons = new List<ModsJson>();
            ModPack? selectedModpack = null;

            var selectedModpackName = ((BackupModpackItemEntry)ModpackList.SelectedItem).ModpackName;
            if (selectedModpackName == UIStrings.Standalone_Non_ModPack)
            {
                selectedModsJsons = _modsJsons.FindAll(modsJson => modsJson.ModPackEntry == null);
            }
            else
            {
                selectedModsJsons = _modsJsons.FindAll(modsJson => modsJson.ModPackEntry?.Name == selectedModpackName);
                selectedModpack = selectedModsJsons[0].ModPackEntry;
            }

            var modsInModpack = new List<Mod>();
            foreach (var modsJson in selectedModsJsons)
            {
                var mod = Mod.MakeModFromJson(modsJson, XivStrings.TexTools);
                modsInModpack.Add(mod);
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
        /// Fills the modpack list using the JSON from the modpack
        /// </summary>
        private void MakeModpackList()
        {
            var modPackNames = new List<string>();
            foreach (var modsJson in _modsJsons)
            {
                var modpackName = modsJson.ModPackEntry?.Name ?? UIStrings.Standalone_Non_ModPack;
                if (!modPackNames.Contains(modpackName))
                {
                    modPackNames.Add(modpackName);

                    var entry = new BackupModpackItemEntry(modpackName);
                    ((List<BackupModpackItemEntry>)ModpackList.ItemsSource).Add(entry);
                }
            }
            ModpackList.SelectedIndex = 0;
        }        


        #endregion

    }
}
