using FFXIV_TexTools.Helpers;
using FFXIV_TexTools.Resources;
using FFXIV_TexTools.ViewModels;
using MahApps.Metro.Controls.Dialogs;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
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

        public BackupModPackImporter(DirectoryInfo modPackDirectory, ModPackJson modPackJson, bool messageInImport = false)
        {
            InitializeComponent();

            _modpackDirectory = modPackDirectory;
            _modsJsons = modPackJson.SimpleModsList;
            _messageInImport = messageInImport;

            DataContext = new BackupModpackViewModel();
            ModPackName.Content = modPackJson.Name;
            ModpackList.ItemsSource = new List<BackupModpackItemEntry>();

            MakeModpackList();            
        }

        public int TotalModsImported { get; private set; }
        public int TotalModsErrored { get; private set; }
        public float ImportDuration { get; private set; }

        private void MakeModpackList()
        {
            var modPackNames = new List<string>();
            foreach (var modsJson in _modsJsons)
            {
                var modpackName = modsJson.ModPackEntry?.name ?? UIStrings.Standalone_Non_ModPack;
                if (!modPackNames.Contains(modpackName))
                {
                    modPackNames.Add(modpackName);

                    var entry = new BackupModpackItemEntry(modpackName);
                    ((List<BackupModpackItemEntry>)ModpackList.ItemsSource).Add(entry);
                }
            }
            ModpackList.SelectedIndex = 0;
        }

        private void SelectAllButton_Click(object sender, RoutedEventArgs e)
        {
            foreach (var entry in (List<BackupModpackItemEntry>)ModpackList.ItemsSource)
            {
                entry.IsChecked = true;
            }
        }

        private void ClearSelectedButton_Click(object sender, RoutedEventArgs e)
        {
            foreach (var entry in (List<BackupModpackItemEntry>)ModpackList.ItemsSource)
            {
                entry.IsChecked = false;
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

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
                                    where (selectedModpackNames.Contains(modsJson.ModPackEntry?.name))
                                    select modsJson);


                var texToolsModPack = new TTMP(new DirectoryInfo(Properties.Settings.Default.ModPack_Directory), XivStrings.TexTools);
                var gameDirectory = new DirectoryInfo(Properties.Settings.Default.FFXIV_Directory);
                var modListDirectory = new DirectoryInfo(System.IO.Path.Combine(gameDirectory.Parent.Parent.FullName, XivStrings.ModlistFilePath));
                var progressIndicator = new Progress<(int current, int total, string message)>(ReportProgress);

                var importResults = await Task.Run(async () =>
                {
                    return await texToolsModPack.ImportModPackAsync(_modpackDirectory, importList, gameDirectory, modListDirectory, progressIndicator);
                });

                TotalModsImported = importResults.ImportCount;
                TotalModsErrored = importResults.ErrorCount;
                ImportDuration = importResults.Duration;

                if (!string.IsNullOrEmpty(importResults.Errors))
                {
                    FlexibleMessageBox.Show(
                        $"{UIMessages.ErrorImportingModsMessage}\n\n{importResults.Errors}", UIMessages.ErrorImportingModsTitle,
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
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

        private void ModpackList_SelectionChanged(object sender, RoutedEventArgs e)
        {
            List<ModsJson> selectedModsJsons = new List<ModsJson>();
            ModPack selectedModpack = null;

            var selectedModpackName = ((BackupModpackItemEntry)ModpackList.SelectedItem).ModpackName;
            if (selectedModpackName == UIStrings.Standalone_Non_ModPack)
            {
                selectedModsJsons = _modsJsons.FindAll(modsJson => modsJson.ModPackEntry == null);
            }
            else
            {
                selectedModsJsons = _modsJsons.FindAll(modsJson => modsJson.ModPackEntry?.name == selectedModpackName);
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
    }
}
