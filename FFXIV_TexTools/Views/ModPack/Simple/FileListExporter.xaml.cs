using FFXIV_TexTools.Resources;
using MahApps.Metro.Controls.Dialogs;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using xivModdingFramework.Cache;
using xivModdingFramework.General.Enums;
using xivModdingFramework.Helpers;
using xivModdingFramework.Mods;
using xivModdingFramework.Mods.DataContainers;
using xivModdingFramework.Mods.Enums;
using xivModdingFramework.Mods.FileTypes;
using xivModdingFramework.SqPack.FileTypes;


namespace FFXIV_TexTools.Views.Simple
{

    /// <summary>
    /// Interaction logic for FileListImporter.xaml
    /// </summary>
    public partial class FileListExporter : INotifyPropertyChanged
    {
        private ModPack _Modpack;

        private ProgressDialogController _progressController;
        public event PropertyChangedEventHandler PropertyChanged;

        public string ModpackName
        {
            get
            {
                return _Modpack.Name;
            }
            set
            {
                _Modpack.Name = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ModpackName)));
            }
        }
        public string ModpackAuthor
        {
            get
            {
                return _Modpack.Author;
            }
            set
            {
                _Modpack.Author = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ModpackAuthor)));
            }
        }
        public string ModpackVersion
        {
            get
            {
                return _Modpack.Version;
            }
            set
            {
                _Modpack.Version = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ModpackVersion)));
            }
        }
        public string ModpackUrl
        {
            get
            {
                return _Modpack.Url;
            }

            set
            {
                _Modpack.Url = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ModpackUrl)));
            }
        }

        private string _ModpackDescription;
        public string ModpackDescription
        {
            get
            {
                return _ModpackDescription;
            }

            set
            {
                _ModpackDescription = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ModpackDescription)));
            }
        }

        public FileListExporter(IEnumerable<string> files)
        {

            DataContext = this;
            InitializeComponent();
            FileList.SetFiles(files);

            ModpackAuthor = Properties.Settings.Default.Default_Author;
            ModpackVersion = "1.0";

            FileList.SelectionChanged += FileList_SelectionChanged;
            ExportButton.IsEnabled = FileList.AnySelected;

            ModpackNameBox.Focus();
        }

        private void FileList_SelectionChanged(object sender, EventArgs e)
        {
            ExportButton.IsEnabled = FileList.AnySelected;
        }

        public static async void ShowModpackExport()
        {
            var owner = MainWindow.GetMainWindow();

            var tx = MainWindow.DefaultTransaction;
            var ml = await tx.GetModList();

            var files = ml.GetMods(x => !x.IsInternal()).Select(x => x.FilePath).ToList();

            var wind = new FileListExporter(files);
            wind.Owner = owner;
            wind.WindowStartupLocation = WindowStartupLocation.CenterOwner;


            wind.Show();
        }


        private async void Export_Click(object sender, RoutedEventArgs e)
        {

            _progressController = await this.ShowProgressAsync(UIMessages.ModPackImportTitle, UIMessages.PleaseStandByMessage);
            try
            {
                var files = FileList.GetSelectedFiles();

                Version.TryParse(ModpackVersion, out var version);
                if (version == null)
                {
                    version = new Version("1.0");
                    ModpackVersion = "1.0";
                }

                if (string.IsNullOrWhiteSpace(ModpackName))
                {
                    ModpackName = "Modpack";
                }
                if (string.IsNullOrWhiteSpace(ModpackAuthor))
                {
                    ModpackAuthor = "Unknown";
                }

                var smd = new SimpleModPackData()
                {
                    Name = ModpackName,
                    Description = ModpackDescription,
                    Author = ModpackAuthor,
                    Url = ModpackUrl,
                    Version = version,
                    SimpleModDataList = new List<SimpleModData>()
                };

                var tx = MainWindow.DefaultTransaction;
                foreach (var file in files)
                {
                    var mod = await tx.GetMod(file);
                    long offset = 0;
                    if (mod == null)
                    {
                        offset = await tx.Get8xDataOffset(file, true);
                    }
                    else
                    {
                        offset = mod.Value.ModOffset8x;
                    }

                    var root = await XivCache.GetFirstRoot(file);
                    var itemName = "Unknown";
                    var itemCategory = "Unknown";

                    if (root != null)
                    {
                        var item = root.GetFirstItem();
                        if (item != null)
                        {
                            itemName = item.Name;
                            itemCategory = item.SecondaryCategory;
                        }
                    }
                    var df = IOUtil.GetDataFileFromPath(file);
                    var dfString = df.GetFileName();

                    var data = new SimpleModData()
                    {
                        FullPath = file,
                        ModOffset = offset,
                        Name = itemName,
                        Category = itemCategory,

                        // Legacy Data.
                        DatFile = dfString,
                        // Modpack writer will figure out size for us.
                    };

                    smd.SimpleModDataList.Add(data);
                }

                var progress = ViewHelpers.BindReportProgress(_progressController);
                await TTMP.CreateSimpleModPack(smd, Properties.Settings.Default.ModPack_Directory, progress, false, tx);
                await _progressController.CloseAsync();
                this.Close();
            }
            catch(Exception ex)
            {
                ViewHelpers.ShowError("Modpack Export Error", "Unable to export files due to an error:\n\n" + ex.Message);
                await _progressController.CloseAsync();
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
