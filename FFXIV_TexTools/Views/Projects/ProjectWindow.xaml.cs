using FFXIV_TexTools.Models;
using FFXIV_TexTools.Resources;
using Newtonsoft.Json;
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
using WK.Libraries.BetterFolderBrowserNS;
using xivModdingFramework.Exd.FileTypes;
using xivModdingFramework.Helpers;
using xivModdingFramework.Mods;
using xivModdingFramework.Mods.FileTypes;
using xivModdingFramework.SqPack.FileTypes;

namespace FFXIV_TexTools.Views.Projects
{
    /// <summary>
    /// Interaction logic for ProjectWindow.xaml
    /// </summary>
    public partial class ProjectWindow : Window, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        public static TTProject Project { get; private set; }

        public static ProjectWindow Instance;
        public ObservableCollection<string> FileListSource { get; set; } = new ObservableCollection<string>();
        public Visibility CloseVisible
        {
            get
            {
                return Project == null ? Visibility.Collapsed : Visibility.Visible;
            }
        }

        public static Window ErrorTarget
        {
            get
            {
                if(Instance != null)
                {
                    return Instance;
                }
                return MainWindow.GetMainWindow();
            }
        }

        private static List<FileSystemWatcher> Watchers = new List<FileSystemWatcher>();

        public ProjectWindow()
        {
            DataContext = this;
            Instance = this;
            InitializeComponent();
            Closing += ProjectWindow_Closing;

            if(DebouncedSaveModpack == null)
            {
                DebouncedSaveModpack = ViewHelpers.Debounce(() =>
                {
                    _ = SaveModpack();
                }, 1000);

                DebouncedLoadFiles = ViewHelpers.Debounce(() =>
                {
                    _ = LoadFiles();
                }, 1000);
            }
            UpdateFileList();
        }

        private void ProjectWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            Instance = null;
            if (null != Owner)
            {
                Owner.Activate();
            }
        }

        private void LoadButton_Click(object sender, RoutedEventArgs e)
        {
            var sfd = new OpenFileDialog();
            sfd.Filter = "TexTools Project Files|*.ttproject";
            if (sfd.ShowDialog() != System.Windows.Forms.DialogResult.OK)
            {
                return;
            }

            try
            {
                var text = File.ReadAllText(sfd.FileName);
                var project = JsonConvert.DeserializeObject<TTProject>(text);
                _ = OpenProject(project);
            }
            catch(Exception ex)
            {
                this.ShowError("Project Load Error", "An error occurred while loading the project file.\n\n" + ex.Message);
                return;
            } 

        }
        private void NewButton_Click(object sender, RoutedEventArgs e)
        {
            NewContextMenu.PlacementTarget = NewButton;
            NewContextMenu.IsOpen = true;
        }

        private void NewTtmp_Click(object sender, RoutedEventArgs e)
        {
            var sfd = new SaveFileDialog();
            sfd.Filter = "TexTools Modpacks|*.ttmp2";
            if (sfd.ShowDialog() != System.Windows.Forms.DialogResult.OK)
            {
                return;
            }

            CreateProject(sfd.FileName, ETransactionTarget.TTMP, false);

        }

        public static void AddExternalSource(string internalFile, string externalFile)
        {
            if (Project == null) return;
            if (string.IsNullOrWhiteSpace(internalFile) || string.IsNullOrWhiteSpace(externalFile)) return;
            if (!IOUtil.IsFFXIVInternalPath(internalFile)) return;
            if (!File.Exists(externalFile)) return;

            if(Project.Files.ContainsKey(internalFile))
            {
                Project.Files[internalFile] = externalFile;
            } else
            {
                Project.Files.Add(internalFile, externalFile);
            }

            if (!Project.LastModifiedTimes.ContainsKey(externalFile))
            {
                Project.LastModifiedTimes.Add(externalFile, new FileInfo(externalFile).LastWriteTime);
            }
        }

        private void NewPmp_Click(object sender, RoutedEventArgs e)
        {
            var sfd = new SaveFileDialog();
            sfd.Filter = "TexTools Modpacks|*.ttmp2";
            if (sfd.ShowDialog() != System.Windows.Forms.DialogResult.OK)
            {
                return;
            }
            CreateProject(sfd.FileName, ETransactionTarget.PMP, false);

        }

        private async void NewPenumbra_Click(object sender, RoutedEventArgs e)
        {
            var sfd = new BetterFolderBrowser();
            if (sfd.ShowDialog() != System.Windows.Forms.DialogResult.OK)
            {
                return;
            }

            var folder = sfd.SelectedPath;

            try
            {
                // Unpack Test
                var files = await TTMP.ModPackToSimpleFileList(folder, false);
                if(files == null)
                {
                    this.ShowError("Load Error".L(), "The Penumbra mod was invalid or contained multiple options.");
                    return;
                }
            }
            catch(Exception ex)
            {
                this.ShowError("Load Error".L(), "An error occurred while trying to load the Penumbra file:\n\n" +  ex.Message);
                return;
            }

            CreateProject(folder, ETransactionTarget.PenumbraModFolder, true);
        }

        private async void NewModpack_Click(object sender, RoutedEventArgs e)
        {
            var sfd = new OpenFileDialog();
            sfd.Filter = "Modpack Files|*.ttmp2;*.ttmp;*.pmp;*.json";
            if (sfd.ShowDialog() != System.Windows.Forms.DialogResult.OK)
            {
                return;
            }
            try
            {
                // Unpack Test
                var files = await TTMP.ModPackToSimpleFileList(sfd.FileName, false);
                if (files == null)
                {
                    this.ShowError("Load Error".L(), "The modpack was invalid or contained multiple options.");
                    return;
                }
            }
            catch (Exception ex)
            {
                this.ShowError("Load Error".L(), "An error occurred while trying to load the modpack:\n\n" + ex.Message);
                return;
            }

            var target = ETransactionTarget.TTMP;
            if (sfd.FileName.ToLower().EndsWith(".pmp"))
            {
                target = ETransactionTarget.PMP;
            }

            CreateProject(sfd.FileName, target, true);
        }

        private void CreateProject(string path, ETransactionTarget target, bool initialModpack)
        {
            try
            {
                var sType = EFileStorageType.UncompressedIndividual;
                if (target == ETransactionTarget.TTMP)
                {
                    sType = EFileStorageType.CompressedIndividual;
                }

                var settings = new ModTransactionSettings()
                {
                    Target = target,
                    StorageType = sType,
                    TargetPath = path,
                };

                if (MainWindow.UserTransaction != null)
                {
                    return;
                }

                var project = new TTProject();


                var jsonPath = "";
                if (IOUtil.IsDirectory(path))
                {
                    jsonPath = Path.Combine(path, "Project.ttproject");
                }
                else
                {
                    jsonPath = Path.Combine(Path.GetDirectoryName(path), Path.GetFileNameWithoutExtension(path) + ".ttproject");
                    if (initialModpack == false)
                    {
                        File.Delete(path);
                    }
                }

                project.Name = Path.GetFileNameWithoutExtension(path);
                project.JsonPath = jsonPath;
                project.TransactionSettings = settings;

                var json = JsonConvert.SerializeObject(project, Formatting.Indented);
                File.Delete(jsonPath);
                File.WriteAllText(jsonPath, json);
                _ = OpenProject(project, true);
            } catch(Exception ex)
            {
                this.ShowError("Project Creation Error", "An error occurred while creating the project:\n\n" + ex.Message);
                return;
            }

        }

        private async Task CloseProject()
        {
            DetatchEvents();

            if (MainWindow.UserTransaction != null)
            {
                ModTransaction.CancelTransaction(MainWindow.UserTransaction, true);
            }


            Project = null;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CloseVisible)));
        }

        public async Task OpenProject(TTProject project, bool firstTime = false)
        {
            this.IsEnabled = false;
            try
            {
                await CloseProject();

                Project = project;


                bool needsPrep = false;
                if (Project.PreparationModpacks.Count > 0)
                {
                    needsPrep = true;
                }

                var tx = ModTransaction.BeginTransaction(true, null, Project.TransactionSettings, needsPrep);
                MainWindow.UserTransaction = tx;

                if (needsPrep)
                {
                    await LoadPrepModpacks();
                    tx.Start();
                }

                try
                {
                    await LoadModpack(firstTime);
                }
                catch (Exception ex)
                {
                    this.ShowError("Project Load Error", "An error occurred while loading the project:\n\n" + ex.Message);
                    await CloseProject();
                    return;
                }

                if (Project.TransactionSettings.Target == ETransactionTarget.PenumbraModFolder)
                {
                    await PenumbraAttachHandler.Attach(Project.TransactionSettings.TargetPath, MainWindow.UserTransaction);
                }
            }
            finally
            {
                this.IsEnabled = true;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CloseVisible)));
                StaticUpdateFileList();
            }
        }

        private static async Task LoadPrepModpacks()
        {
            foreach (var pp in Project.PreparationModpacks)
            {
                var files = await TTMP.ModPackToSimpleFileList(Project.TransactionSettings.TargetPath, false);
                if (files == null)
                {
                    ErrorTarget.ShowWarning("Pre-Modpack Error", "The following Prep modpack failed to load:\n" + pp + "\n\nThe Project will still be loaded, but may be missing preparation files.");
                    continue;
                }
                await TTMP.ImportFiles(files, null, null, MainWindow.UserTransaction);
            }
        }
        private static async Task LoadModpack(bool ignoreMissing = false)
        {
            var files = await TTMP.ModPackToSimpleFileList(Project.TransactionSettings.TargetPath, true, MainWindow.UserTransaction);
            if(files == null)
            {
                if (ignoreMissing)
                {
                    return;
                }
                throw new FileNotFoundException("The project modpack was invalid, contained multiple options, or was corrupted.");
            }


            await TTMP.ImportFiles(files, null, null, MainWindow.UserTransaction);
            AttachEvents();
        }

        private static void AttachEvents()
        {
            DetatchEvents();
            if (MainWindow.UserTransaction != null)
            {
                MainWindow.UserTransaction.FileChanged += Transaction_FileChanged;
            }

            if (Project != null)
            {
                // Attach a file watch for every file.
                foreach (var kv in Project.Files)
                {
                    var folder = Path.GetDirectoryName(kv.Value);
                    var file = Path.GetFileName(kv.Value);
                    var watch = new FileSystemWatcher(folder, file);
                    watch.EnableRaisingEvents = true;

                    watch.Changed += Watcher_FileChanged;
                    watch.Created += Watcher_FileCreated;
                    watch.Error += Watcher_Error;

                    Watchers.Add(watch);
                }
            }
        }

        private static void DetatchEvents()
        {
            if(MainWindow.UserTransaction != null)
            {
                MainWindow.UserTransaction.FileChanged -= Transaction_FileChanged;
            }
            foreach(var watch in Watchers)
            {
                watch.Changed -= Watcher_FileChanged;
                watch.Created -= Watcher_FileCreated;
                watch.Error -= Watcher_Error;
                watch.Dispose();
            }
            Watchers.Clear();

        }

        private static void Transaction_FileChanged(string internalFilePath, long newOffset)
        {
            if (!PenumbraAttachHandler.IsAttached)
            {
                DebouncedSaveModpack();
            }

            StaticUpdateFileList();
        }

        private static Action DebouncedSaveModpack;
        private static async Task SaveModpack()
        {
            if (Project == null) return;
            if (MainWindow.UserTransaction == null || MainWindow.UserTransaction.State != ETransactionState.Open) return;
            if (PenumbraAttachHandler.IsAttached) return;

            try
            {
                await ModTransaction.CommitTransaction(MainWindow.UserTransaction, false);
            } catch(Exception ex)
            {
                await ErrorTarget.Dispatcher.InvokeAsync(() =>
                {
                    ErrorTarget.ShowWarning("Project Save Error", "An error occurred while saving the project:\n" + ex.Message + "\n\nThe project will continue and remain open.");
                });
            }
        }

        private static Action DebouncedLoadFiles;
        private static async Task LoadFiles()
        {
            if (Project == null) return;
            if (MainWindow.UserTransaction == null || MainWindow.UserTransaction.State != ETransactionState.Open) return;

            var toLoad = new Dictionary<string, string>();
            foreach(var kv in Project.Files)
            {

                if (!File.Exists(kv.Value))
                {
                    continue;
                }

                if (!Project.LastModifiedTimes.ContainsKey(kv.Value))
                {
                    toLoad.Add(kv.Key, kv.Value);
                    continue;
                }

                var modTime = new FileInfo(kv.Value).LastWriteTimeUtc;

                if(modTime != Project.LastModifiedTimes[kv.Key])
                {
                    toLoad.Add(kv.Key, kv.Value);
                }
            }

            if(toLoad.Count == 0)
            {
                return;
            }

            // Unbind events while we're loading files to be safe.
            // We'll commit the transaction after.
            DetatchEvents();
            if (PenumbraAttachHandler.IsAttached)
            {
                PenumbraAttachHandler.Pause();
            }

            try
            {
                foreach(var kv in toLoad)
                {
                    try
                    {
                        await SmartImport.Import(kv.Value, kv.Key, XivStrings.TexTools, MainWindow.UserTransaction);
                    } catch (Exception ex)
                    {
                        ErrorTarget.ShowError("File Import Error", "An error occurred while importing a project file:\n\n" + kv.Value + "\n" + kv.Key + "\n\n" + ex.Message);
                    }
                }
                await SaveModpack();
            }
            finally
            {
                AttachEvents();
                PenumbraAttachHandler.Resume();
                StaticUpdateFileList();
            }
        }

        private static void Watcher_FileCreated(object sender, FileSystemEventArgs e)
        {
            DebouncedLoadFiles();
        }
        private static void Watcher_FileChanged(object sender, FileSystemEventArgs e)
        {
            DebouncedLoadFiles();
        }
        private static void Watcher_Error(object sender, ErrorEventArgs e)
        {
            _ = ErrorTarget.Dispatcher.InvokeAsync(() =>
            {
                ErrorTarget.ShowWarning("File Watch Error", "A file watch in the project threw an error:\n\n" + e.ToString());
            });
        }

        private void CloseProject_Click(object sender, RoutedEventArgs e)
        {
            _ = CloseProject();
        }



        public static void ShowProjectWindow()
        {
            var wind = ProjectWindow.Instance;
            if (wind == null)
            {
                wind = new ProjectWindow();
            }
            wind.Owner = MainWindow.GetMainWindow();
            wind.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            wind.Show();
        }

        public static void StaticUpdateFileList()
        {
            Instance?.UpdateFileList();
        }

        public void UpdateFileList()
        {
            if (MainWindow.UserTransaction == null)
            {
                return;
            }
            var tx = MainWindow.UserTransaction;
            FileListSource.Clear();
            var files = tx.ModifiedFiles.OrderBy(x => x);
            foreach (var file in files)
            {
                if (IOUtil.IsMetaInternalFile(file))
                {
                    continue;
                }

                FileListSource.Add(file);
            }
        }
    }
}
