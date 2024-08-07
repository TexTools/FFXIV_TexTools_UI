﻿using ControlzEx.Standard;
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
using xivModdingFramework.Cache;
using xivModdingFramework.Exd.FileTypes;
using xivModdingFramework.General.Enums;
using xivModdingFramework.Helpers;
using xivModdingFramework.Mods;
using xivModdingFramework.Mods.DataContainers;
using xivModdingFramework.Mods.Enums;
using xivModdingFramework.Mods.FileTypes;
using xivModdingFramework.SqPack.FileTypes;
using static xivModdingFramework.Mods.DataContainers.ModPackData;
using Path = System.IO.Path;


namespace FFXIV_TexTools.Views.Simple
{

    /// <summary>
    /// Interaction logic for FileListImporter.xaml
    /// </summary>
    public partial class FileListImporter : INotifyPropertyChanged
    {
        private string _ModpackPath;
        private ModPack? _Modpack;

        public string ModpackName
        {
            get
            {
                if(_Modpack == null)
                {
                    return "";
                }
                return _Modpack.Value.Name;
            }
        }
        public string ModpackAuthor
        {
            get
            {
                if (_Modpack == null)
                {
                    return "";
                }
                return _Modpack.Value.Author;
            }
        }
        public string ModpackVersion
        {
            get
            {
                if (_Modpack == null)
                {
                    return "";
                }
                return _Modpack.Value.Version;
            }
        }
        public string ModpackUrl
        {
            get
            {
                if (_Modpack == null)
                {
                    return "";
                }
                return _Modpack.Value.Url;
            }
        }

        private ImageSource _HeaderSource { get; set; }
        public ImageSource HeaderSource {
            get => _HeaderSource;
            set
            {
                _HeaderSource = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HeaderSource)));
            }
        }

        public string ModpackDescription { get; set; }

        public FileListImporter(IEnumerable<string> files, string modpackPath = null)
        {

            DataContext = this;
            _ModpackPath = modpackPath;
            InitializeComponent();
            FileList.SetFiles(files);

            _ = AsyncInit();

            this.Closing += FileListImporter_Closing;
        }

        private void FileListImporter_Closing(object sender, CancelEventArgs e)
        {
            if(Owner != null)
            {
                Owner.Activate();
            }
        }

        private async Task AsyncInit()
        {
            ImageSource src = null;
            try
            {
                var mpi = await TTMP.GetModpackInfo(_ModpackPath);
                _Modpack = mpi.ModPack;
                ModpackDescription = mpi.Description;
                if (!string.IsNullOrWhiteSpace(mpi.HeaderImage))
                {
                    src = ViewHelpers.SafeBitmapFromFile(mpi.HeaderImage);
                }

            }
            catch (Exception ex)
            {
                _Modpack = new ModPack(null)
                {
                    Name = Path.GetFileNameWithoutExtension(_ModpackPath),
                    Author = "Unknown",
                    Url = "",
                    Version = "1.0",
                };
                ModpackDescription = "";
            }

            if (src == null)
            {
                src = ViewHelpers.GetDefaultModImage();
            }

            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ModpackDescription)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ModpackAuthor)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ModpackUrl)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ModpackVersion)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ModpackName)));

            HeaderSource = src;
        }

        public static void ShowModpackImport(string modpackPath, IEnumerable<string> files, Window owner = null, bool asDialog = false)
        {
            if (owner == null)
            {
                var mw = MainWindow.GetMainWindow();
                if (mw != null && mw.MainWindowLoaded)
                {
                    owner = MainWindow.GetMainWindow();
                }
            }

            var wind = new FileListImporter(files, modpackPath);
            if (ViewHelpers.IsWindowOpen(owner))
            {
                wind.Owner = owner;
            }

            wind.WindowStartupLocation = owner == null ? WindowStartupLocation.CenterScreen : WindowStartupLocation.CenterOwner;

            if (asDialog)
            {
                wind.ShowDialog();
            }
            else
            {
                wind.Show();
            }
        }


        public static void ShowFileList(IEnumerable<string> files, Window owner = null)
        {
            if(owner == null)
            {
                owner = MainWindow.GetMainWindow();
            }

            var wind = new FileListImporter(files);
            wind.Owner = owner;
            wind.WindowStartupLocation = WindowStartupLocation.CenterOwner;

            wind.Show();
        }

        private ProgressDialogController _progressController;

        public event PropertyChangedEventHandler PropertyChanged;

        private async void Import_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(_ModpackPath))
            {
                return;
            }

            var success = false;
            _progressController = await this.ShowProgressAsync(UIMessages.ModPackImportTitle, UIMessages.PleaseStandByMessage);
            try
            {

                var allFiles = new Dictionary<string, FileStorageInformation>();
                var tx = MainWindow.UserTransaction;
                var boiler = await TxBoiler.BeginWrite(tx);
                tx = boiler.Transaction;
                try
                {
                    // Read Modpack basic info.
                    ModPack modpack = new ModPack()
                    {
                        Name = ModpackName,
                        Author = ModpackAuthor,
                        Version = ModpackVersion,
                        Url = ModpackUrl,
                    };

                    // Unpack files we'll actually use from the modpack.
                    // Providing a TX here lets the transaction handler take care of managing the temp files so we don't have to.
                    allFiles = await TTMP.ModPackToSimpleFileList(_ModpackPath, true, tx);

                    var unselected = FileList.GetUnselectedFiles();

                    foreach (var file in unselected)
                    {
                        allFiles.Remove(file);

                    }

                    var settings = ViewHelpers.GetDefaultImportSettings(_progressController, this);
                    await TTMP.ImportFiles(allFiles, modpack, settings, tx);

                    if (boiler.OwnTx)
                    {
                        settings.ProgressReporter?.Report((0, 0, "Compressing Files and Committing Transaction..."));
                    }
                    await boiler.Commit();
                    success = true;
                }
                catch (Exception ex)
                {
                    await boiler.Cancel();
                    throw;
                }
            } catch(Exception ex)
            {
                ViewHelpers.ShowError("Modpack File List Import Error", "The import has been cancelled due to an error:\n\n" + ex.Message);
                return;
            }
            finally
            {
                await _progressController.CloseAsync();
                if (success)
                {
                    Close();
                }
            }


        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void Url_Click(object sender, MouseButtonEventArgs e)
        {
            if (_Modpack == null) return;

            var url = IOUtil.ValidateUrl(_Modpack.Value.Url);
            if (url == null)
            {
                return;
            }

            Process.Start(new ProcessStartInfo(url));
            e.Handled = true;
        }
    }
}
