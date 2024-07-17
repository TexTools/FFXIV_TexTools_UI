using FFXIV_TexTools.Properties;
using FFXIV_TexTools.Resources;
using FFXIV_TexTools.Views.Wizard;
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
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
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

        private ImageSource _HeaderSource { get; set; }
        public ImageSource HeaderSource
        {
            get => _HeaderSource;
            set
            {
                _HeaderSource = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HeaderSource)));
            }
        }

        private string ImagePath;

        public FileListExporter(IEnumerable<string> files)
        {

            DataContext = this;
            InitializeComponent();
            FileList.SetFiles(files, false);

            ModpackAuthor = Properties.Settings.Default.Default_Author;
            ModpackUrl = Properties.Settings.Default.Default_Modpack_Url;
            ModpackVersion = "1.0";

            FileList.SelectionChanged += FileList_SelectionChanged;
            ExportButton.IsEnabled = FileList.AnySelected;

            ModpackNameBox.Focus();
            Closing += FileListExporter_Closing;
        }

        private void FileListExporter_Closing(object sender, CancelEventArgs e)
        {
            if (Owner != null)
            {
                Owner.Activate();
            }
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

            if(string.IsNullOrWhiteSpace(ModpackName))
            {
                ModpackName = "Modpack";
            }

            var pathSafe = IOUtil.MakePathSafe(ModpackName, false);

            var startingFolder = Path.GetFullPath(Settings.Default.ModPack_Directory);
            var sfd = new SaveFileDialog()
            {
                Filter = ViewHelpers.ModpackFileFilter,
                Title = "Save Modpack...",
                InitialDirectory = startingFolder,
                FileName = pathSafe + "." + Settings.Default.Default_Modpack_Format
            };

            if (sfd.ShowDialog() != System.Windows.Forms.DialogResult.OK)
            {
                return;
            }
            var path = sfd.FileName;

            _progressController = await this.ShowProgressAsync("Creating Modpack", UIMessages.PleaseStandByMessage);
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


                // Create the data to write.
                var wizardData = new WizardData();

                wizardData.MetaPage.Author = ModpackAuthor;
                wizardData.MetaPage.Name = ModpackName;
                wizardData.MetaPage.Description = ModpackDescription;
                wizardData.MetaPage.Url = ModpackUrl;
                wizardData.MetaPage.Version = version.ToString();
                wizardData.MetaPage.Image = ImagePath;

                var page = new WizardPageEntry();
                var group = new WizardGroupEntry();
                group.OptionType = EOptionType.Single;
                var option = new WizardOptionEntry(group);
                page.Groups.Add(group);
                wizardData.DataPages.Add(page);
                group.Options.Add(option);

                group.Name = "Default Group";
                option.Name = "Default Option";



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

                    var df = IOUtil.GetDataFileFromPath(file);
                    var handle = tx.UNSAFE_GetStorageInfo(df, offset);

                    option.StandardData.Files.Add(file, handle);
                }

                if (path.ToLower().EndsWith(".ttmp2"))
                {
                    option.Image = ImagePath;
                    await wizardData.WriteWizardPack(path);
                } else
                {
                    await wizardData.WritePmp(path);
                }


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

        private void ChangeImage_Click(object sender, RoutedEventArgs e)
        {
            var imgInfo = this.LoadUserImage();
            if (string.IsNullOrWhiteSpace(imgInfo.File)) return;


            ImagePath = imgInfo.File;
            HeaderSource = imgInfo.Image;
        }

        private void RemoveImage_Click(object sender, RoutedEventArgs e)
        {
            ImagePath = null;
            HeaderSource = null;
        }
    }
}
