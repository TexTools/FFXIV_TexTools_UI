using FFXIV_TexTools.Helpers;
using FFXIV_TexTools.Resources;
using FFXIV_TexTools.Views.Simple;
using FFXIV_TexTools.Views.Wizard;
using MahApps.Metro.Controls;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
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
using xivModdingFramework.Cache;
using xivModdingFramework.General.Enums;
using xivModdingFramework.Helpers;
using xivModdingFramework.Mods.DataContainers;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.Window;
using xivModdingFramework.Mods.FileTypes;
using MahApps.Metro.Controls.Dialogs;
using System.Diagnostics;
using System.Dynamic;
using xivModdingFramework.Mods;
using Path = System.IO.Path;

namespace FFXIV_TexTools.Views
{
    /// <summary>
    /// Interaction logic for ImportOnlyWindow.xaml
    /// </summary>
    public partial class ImportOnlyWindow : MetroWindow, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        private string _Modpack;
        ProgressDialogController _Lock;

        public enum InstallAction
        {
            InstallXiv,
            InstallPenumbra,
            UpdateMod,
            ShrinkMod
        }

        public string ImportText { get; set; }

        private InstallAction _SelectedAction = InstallAction.InstallPenumbra;
        public InstallAction SelectedAction
        {
            get => _SelectedAction;
            set
            {
                _SelectedAction = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectedAction)));
            }
        }

        public ObservableCollection<KeyValuePair<string, InstallAction>> Actions { get; set; } = new ObservableCollection<KeyValuePair<string, InstallAction>>()
        {
            new KeyValuePair<string, InstallAction>("Upgrade and Install to Penumbra", InstallAction.InstallPenumbra),
            new KeyValuePair<string, InstallAction>("Upgrade and Install to FFXIV (UNSAFE)", InstallAction.InstallXiv),
            new KeyValuePair<string, InstallAction>("Upgrade Modpack for Dawntrail", InstallAction.UpdateMod),
            new KeyValuePair<string, InstallAction>("Shrink Modpack", InstallAction.ShrinkMod),
        };

        public ImportOnlyWindow(string modpackPath)
        {
            _Modpack = modpackPath;
            ImportText = "Importing Modpack: " + System.IO.Path.GetFileNameWithoutExtension(modpackPath);
            Title = "Importing " + System.IO.Path.GetFileNameWithoutExtension(modpackPath);
            SelectedAction = InstallAction.InstallXiv;
            try
            {
                var dir = PenumbraAPI.GetPenumbraDirectory();
                if (!string.IsNullOrWhiteSpace(dir)){
                    SelectedAction = InstallAction.InstallPenumbra;
                }
            }
            catch
            {

            }

            DataContext = this;
            InitializeComponent();

            this.Closing += ImportOnlyWindow_Closing;
        }

        public static void ShowImportDialog(string modpackPath)
        {
            if(string.IsNullOrWhiteSpace(modpackPath) || !File.Exists(modpackPath))
            {
                return;
            }

            var wind = new ImportOnlyWindow(modpackPath);
            wind.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            var r = wind.ShowDialog();
        }


        private void ImportOnlyWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            Owner?.Activate();
        }

        private async void Continue_Click(object sender, RoutedEventArgs e)
        {
            this.SizeToContent = SizeToContent.Manual;
            this.Height = 300;
            _Lock = await this.ShowProgressAsync("Working", "Please wait...");
            try
            {
                if (SelectedAction == InstallAction.InstallXiv)
                {
                    if (!this.ShowConfirmation("Import Confirmation", "This will install the modpack to your live FFXIV files.\n\nAre you sure you wish to continue?"))
                    {
                        return;
                    }
                    else
                    {
                        XivCache.GameWriteEnabled = true;
                    }

                    await HardImportModpack();
                } else if(SelectedAction == InstallAction.InstallPenumbra)
                {
                    await PenumbraInstall();
                } else if(SelectedAction == InstallAction.ShrinkMod)
                {
                    await ShrinkModpack();
                } else if(SelectedAction == InstallAction.UpdateMod)
                {
                    await UpdateModpack();
                }

            } catch(Exception ex)
            {
                this.ShowError("Modpack Error", "An error occurred while processing the modpack: " + ex.Message);
            }
            finally
            {
                await _Lock.CloseAsync();
            }
            this.Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private async Task HardImportModpack()
        {
            _Lock.SetTitle("Importing Modpack to FFXIV");
            var modpackType = TTMP.GetModpackType(_Modpack);
            if (modpackType == TTMP.EModpackType.Invalid)
            {
                throw new Exception("Modpack was not a valid PMP or TTMP file, or cannot be read on this version of TexTools.");
            }

            if (modpackType == TTMP.EModpackType.TtmpBackup)
            {
                // TexTools backup modpack.
                var mpl = await TTMP.GetModpackList(_Modpack);
                var backupImport = new BackupModPackImporter(new DirectoryInfo(_Modpack), mpl, false);

                backupImport.WindowStartupLocation = WindowStartupLocation.CenterScreen;

                if (ViewHelpers.IsWindowOpen(this))
                {
                    backupImport.Owner = this;
                }
                    backupImport.ShowDialog();
                return;
            }

            // See if we can get the information in simple mode.
            var modPackFiles = await TTMP.ModPackToSimpleFileList(_Modpack, false, MainWindow.UserTransaction);

            if (modPackFiles != null)
            {
                FileListImporter.ShowModpackImport(_Modpack, modPackFiles.Keys.ToList(), this, true);
                return;
            }

            if (modpackType == TTMP.EModpackType.TtmpWizard || modpackType == TTMP.EModpackType.Pmp)
            {
                // Multi-Option PMP/TTMP
                await ImportWizardWindow.ImportModpack(_Modpack, this, true);
            }
        }

        private async Task PenumbraInstall()
        {
            _Lock.SetTitle("Installing Modpack to Penumbra");
            var info = await TTMP.GetModpackInfo(_Modpack);
            var fname = IOUtil.MakePathSafe(info.ModPack.Name);
            var dir = PenumbraAPI.GetPenumbraDirectory();

            if(string.IsNullOrWhiteSpace(dir))
            {
                throw new Exception("Penumbra is not installed or the library directory could not be found.");
            }
            var newPath = IOUtil.GetUniqueSubfolder(dir, fname, true);

            var newName = System.IO.Path.GetFileName(newPath);

            await ModpackUpgrader.UpgradeModpack(_Modpack, newPath, true, true);
            await PenumbraAPI.ReloadMod(newName);
        }
        private async Task UpdateModpack()
        {
            _Lock.SetTitle("Upgrading Modpack");
            await ModpackUpgraderWrapper.UpgradeModpackPrompted(true, _Modpack);
        }
        private async Task ShrinkModpack()
        {
            _Lock.SetTitle("Shrinking Modpack");
            var data = await ShrinkRay.ShrinkModpack(_Modpack);

            if(data == null)
            {
                throw new InvalidDataException("Unable to load or process modpack.");
            }

            var ext = Path.GetExtension(_Modpack);
            var newPath = _Modpack.Replace(ext, "_smol" + ext);

            var sfd = new SaveFileDialog();
            sfd.Filter = ViewHelpers.ModpackFileFilter;
            sfd.FileName = Path.GetFileName(newPath);
            sfd.InitialDirectory = Path.GetDirectoryName(newPath);

            if(sfd.ShowDialog() != System.Windows.Forms.DialogResult.OK)
            {
                return;
            }

            await data.WriteModpack(sfd.FileName);
        }
    }
}
