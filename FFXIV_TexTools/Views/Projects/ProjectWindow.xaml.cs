using FFXIV_TexTools.Models;
using FFXIV_TexTools.Resources;
using FFXIV_TexTools.Views.Controls;
using FFXIV_TexTools.Views.Transactions;
using MahApps.Metro.Controls.Dialogs;
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
using xivModdingFramework.Models.Helpers;
using xivModdingFramework.Mods;
using xivModdingFramework.Mods.FileTypes;
using xivModdingFramework.SqPack.FileTypes;
using Button = System.Windows.Controls.Button;

namespace FFXIV_TexTools.Views.Projects
{
    public class ProjectFileListEntry : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        private string _InternalPath;
        public string InternalPath
        {
            get => _InternalPath;
            set
            {
                if (_InternalPath == value) return;
                _InternalPath = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(InternalPath)));
            }
        }

        private string _ExternalPath;
        public string ExternalPath
        {
            get => _ExternalPath;
            set
            {
                if (_ExternalPath == value) return;
                _ExternalPath = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ExternalPath)));
            }
        }
    }

    /// <summary>
    /// Interaction logic for ProjectWindow.xaml
    /// </summary>
    public partial class ProjectWindow : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        public static TTProject Project { get; private set; }

        public static ProjectWindow Instance;
        public ObservableCollection<ProjectFileListEntry> FileListSource { get; set; } = new ObservableCollection<ProjectFileListEntry>();
        public Visibility CloseVisible
        {
            get
            {
                return Project == null ? Visibility.Collapsed : Visibility.Visible;
            }
        }
        public Visibility OpenVisible
        {
            get
            {
                return Project == null ? Visibility.Visible : Visibility.Collapsed;
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

        private static string _ProjectFileFilter = "TexTools Project Files|*.ttproject";

        public ProjectWindow()
        {
            if (TransactionStatusWindow.Instance != null)
            {
                TransactionStatusWindow.Instance.Close();
            }

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
            if (MainWindow.UserTransaction != null && MainWindow.UserTransaction.State != ETransactionState.Closed && MainWindow.UserTransaction.ModifiedFiles.Count > 0)
            {
                if (!ViewHelpers.ShowConfirmation(this, "Close Transaction Confirmation", "This will cancel your current transaction, are you sure you wish to continue?"))
                {
                    return;
                }
            }

            var sfd = new OpenFileDialog();
            sfd.Filter = _ProjectFileFilter;
            if (sfd.ShowDialog() != System.Windows.Forms.DialogResult.OK)
            {
                return;
            }

            try
            {
                var text = File.ReadAllText(sfd.FileName);
                var project = JsonConvert.DeserializeObject<TTProject>(text);
                project.JsonPath = sfd.FileName;
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

        private void NewProject_Click(object sender, RoutedEventArgs e)
        {
            if (MainWindow.UserTransaction != null && MainWindow.UserTransaction.State != ETransactionState.Closed && MainWindow.UserTransaction.ModifiedFiles.Count > 0)
            {
                if (!ViewHelpers.ShowConfirmation(this, "Close Transaction Confirmation", "This will cancel your current transaction, are you sure you wish to continue?"))
                {
                    return;
                }
            }

            var sfd = new SaveFileDialog();
            sfd.Filter = _ProjectFileFilter;
            if (sfd.ShowDialog() != System.Windows.Forms.DialogResult.OK)
            {
                return;
            }

            var dir = Path.Combine(Path.GetDirectoryName(sfd.FileName), Path.GetFileNameWithoutExtension(sfd.FileName));
            Directory.CreateDirectory(dir);

            var projectPath = Path.Combine(dir, Path.GetFileNameWithoutExtension(sfd.FileName) + ".ttproject");

            _ = CreateProject(projectPath, ETransactionTarget.FolderTree, null);
        }

        private async void NewPenumbra_Click(object sender, RoutedEventArgs e)
        {
            if (MainWindow.UserTransaction != null && MainWindow.UserTransaction.State != ETransactionState.Closed && MainWindow.UserTransaction.ModifiedFiles.Count > 0)
            {
                if (!ViewHelpers.ShowConfirmation(this, "Close Transaction Confirmation", "This will cancel your current transaction, are you sure you wish to continue?"))
                {
                    return;
                }
            }

            if (MainWindow.UserTransaction != null && MainWindow.UserTransaction.State != ETransactionState.Closed && MainWindow.UserTransaction.ModifiedFiles.Count > 0)
            {
                if (!ViewHelpers.ShowConfirmation(this, "Close Transaction Confirmation", "This will cancel your current transaction, are you sure you wish to continue?"))
                {
                    return;
                }
            }
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

            var ttProjectPath = Path.Combine(folder, "project.ttproject");

            _ = CreateProject(ttProjectPath, ETransactionTarget.PenumbraModFolder, folder);
        }

        private async void NewModpack_Click(object sender, RoutedEventArgs e)
        {
            if (MainWindow.UserTransaction != null && MainWindow.UserTransaction.State != ETransactionState.Closed && MainWindow.UserTransaction.ModifiedFiles.Count > 0)
            {
                if (!ViewHelpers.ShowConfirmation(this, "Close Transaction Confirmation", "This will cancel your current transaction, are you sure you wish to continue?"))
                {
                    return;
                }
            }

            if (MainWindow.UserTransaction != null && MainWindow.UserTransaction.State != ETransactionState.Closed && MainWindow.UserTransaction.ModifiedFiles.Count > 0)
            {
                if(!ViewHelpers.ShowConfirmation(this, "Close Transaction Confirmation", "This will cancel your current transaction, are you sure you wish to continue?"))
                {
                    return;
                }
            }
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

            var dir = Path.Combine(Path.GetDirectoryName(sfd.FileName), "Project - " + Path.GetFileNameWithoutExtension(sfd.FileName));
            Directory.CreateDirectory(dir);

            var projectPath = Path.Combine(dir, Path.GetFileNameWithoutExtension(sfd.FileName) + ".ttproject");


            _ = CreateProject(projectPath, ETransactionTarget.FolderTree, sfd.FileName);
        }

        private async Task CreateProject(string ttProjectPath, ETransactionTarget target, string intialModpackPath = null)
        {
            await CloseProject();

            if(!ttProjectPath.EndsWith(".ttproject"))
            {
                this.ShowError("Project Creation Error", "Path is not valid: " + ttProjectPath);
                return;
            }

            if(target != ETransactionTarget.FolderTree && target != ETransactionTarget.PenumbraModFolder)
            {
                this.ShowError("Project Creation Error", "Invalid project target: " + target.ToString());
                return;
            }

            try
            {
                var sType = EFileStorageType.UncompressedIndividual;
                var folder = Path.GetDirectoryName(ttProjectPath);
                Directory.CreateDirectory(folder);

                var settings = new ModTransactionSettings()
                {
                    Target = target,
                    StorageType = sType,
                    TargetPath = folder,
                };

                if (MainWindow.UserTransaction != null)
                {
                    return;
                }

                var project = new TTProject();



                project.Name = Path.GetFileNameWithoutExtension(ttProjectPath);
                project.JsonPath = ttProjectPath;
                project.TransactionSettings = settings;

                var json = JsonConvert.SerializeObject(project, Formatting.Indented);
                File.Delete(ttProjectPath);
                File.WriteAllText(ttProjectPath, json);
                _ = OpenProject(project, true, intialModpackPath);
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
                await ModTransaction.CancelTransaction(MainWindow.UserTransaction, true);
            }

            SaveProject();
            Project = null;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(OpenVisible)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CloseVisible)));
        }

        public async Task OpenProject(TTProject project, bool firstTime = false, string initialModpackPath = null)
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

                var tx = await ModTransaction.BeginTransaction(true, null, Project.TransactionSettings, needsPrep);

                if (needsPrep)
                {
                    await LoadPrepModpacks();
                    tx.Start();
                }

                try
                {
                    await LoadModpack(initialModpackPath, firstTime, tx);
                }
                catch (Exception ex)
                {
                    this.ShowError("Project Load Error", "An error occurred while loading the project:\n\n" + ex.Message);
                    MainWindow.UserTransaction = tx;
                    await CloseProject();
                    return;
                }

                if (initialModpackPath != null)
                {
                    await SaveModpack();
                }

                try
                {
                    await LoadFiles();
                    SaveProject();
                } catch(Exception ex)
                {
                    this.ShowError("Project Load Error", "An error occurred while loading some of the project files the project:\n\n" + ex.Message);
                    MainWindow.UserTransaction = tx;
                    await CloseProject();
                    return;
                }

                if (Project.TransactionSettings.Target == ETransactionTarget.PenumbraModFolder)
                {
                    await PenumbraAttachHandler.Attach(Project.TransactionSettings.TargetPath, tx, true);
                }
                MainWindow.UserTransaction = tx;
            }
            finally
            {
                this.IsEnabled = true;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(OpenVisible)));
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
        private static async Task LoadModpack(string path = null, bool ignoreMissing = false, ModTransaction tx = null)
        {
            if(tx == null)
            {
                tx = MainWindow.UserTransaction;
            }
            ProgressDialogController controller = null;
            if(Instance != null)
            {
                controller = await Instance.ShowProgressAsync("Loading Modpack", "Please wait...");
            }
            try
            {
                var upgrade = true;
                if (path == null)
                {
                    path = Project.TransactionSettings.TargetPath;
                    upgrade = false;
                }

                var files = await TTMP.ModPackToSimpleFileList(path, true, MainWindow.UserTransaction);
                if (files == null)
                {
                    if (ignoreMissing)
                    {
                        return;
                    }
                    throw new FileNotFoundException("The project modpack was invalid, contained multiple options, or was corrupted.");
                }

                var settings = new ModPackImportSettings()
                {
                    AutoAssignSkinMaterials = false,
                    ProgressReporter = null,
                    RootConversionFunction = null,
                    SourceApplication = XivStrings.TexTools,
                    UpdateEndwalkerFiles = upgrade,
                    UpdatePartialEndwalkerFiles = upgrade,
                };


                await TTMP.ImportFiles(files, null, settings, tx);
                AttachEvents();
            }
            finally
            {
                if (controller != null)
                {
                    await controller.CloseAsync();
                }
            }
        }

        private static void SaveProject()
        {
            if (Project == null) return;
            if (string.IsNullOrWhiteSpace(Project.JsonPath)) return;

            var json = JsonConvert.SerializeObject(Project, Formatting.Indented);
            File.Delete(Project.JsonPath);
            File.WriteAllText(Project.JsonPath, json);
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
                    if (string.IsNullOrWhiteSpace(kv.Value)) continue;

                    var folder = Path.GetDirectoryName(kv.Value);
                    var file = Path.GetFileName(kv.Value);
                    var watch = new FileSystemWatcher(folder, file);
                    watch.EnableRaisingEvents = true;
                    watch.Renamed += Watcher_FileRenamed;
                    watch.Deleted += Watcher_FileDeleted;

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
            if (PenumbraAttachHandler.IsAttached && !PenumbraAttachHandler.IsPaused) return;

            try
            {
                await ModTransaction.CommitTransaction(MainWindow.UserTransaction, false);


                foreach(var file in MainWindow.UserTransaction.ModifiedFiles)
                {
                    if (!Project.Files.ContainsKey(file))
                    {
                        AddExternalSource(file, "", false);
                    }
                }

                SaveProject();
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
                if (string.IsNullOrWhiteSpace(kv.Value))
                {
                    continue;
                }

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

                if(modTime != Project.LastModifiedTimes[kv.Value])
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
            bool reloadPenumbra = false;
            if (PenumbraAttachHandler.IsAttached)
            {
                reloadPenumbra = true;
                PenumbraAttachHandler.Pause();
            }

            try
            {
                foreach(var kv in toLoad)
                {
                    SmartImportOptions options = null;
                    if (Project.ImportOptions.ContainsKey(kv.Value))
                    {
                        options = Project.ImportOptions[kv.Value];
                    }
                    try
                    {
                        await SmartImport.Import(kv.Value, kv.Key, XivStrings.TexTools, MainWindow.UserTransaction, options);
                    } catch (Exception ex)
                    {
                        ErrorTarget.ShowError("File Import Error", "An error occurred while importing a project file:\n\n" + kv.Value + "\n" + kv.Key + "\n\n" + ex.Message);
                    }
                }
                await SaveModpack();
                SaveProject();
            }
            finally
            {
                AttachEvents();
                if (reloadPenumbra)
                {
                    PenumbraAttachHandler.Resume();
                }
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
        private static void Watcher_FileDeleted(object sender, FileSystemEventArgs e)
        {
            DebouncedLoadFiles();
        }

        private static void Watcher_FileRenamed(object sender, RenamedEventArgs e)
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
            if (MainWindow.UserTransaction == null  || Project == null)
            {
                return;
            }
            _ = Dispatcher.InvokeAsync(() =>
            {
                if (MainWindow.UserTransaction == null || Project == null)
                {
                    return;
                }

                var tx = MainWindow.UserTransaction;
                FileListSource.Clear();
                var files = tx.ModifiedFiles.OrderBy(x => x).ToList();
                foreach (var file in files)
                {
                    if (IOUtil.IsMetaInternalFile(file))
                    {
                        continue;
                    }

                    var extPath = "";
                    if (Project.Files.ContainsKey(file))
                    {
                        extPath = Project.Files[file];
                    }


                    var entry = new ProjectFileListEntry()
                    {
                        InternalPath = file,
                        ExternalPath = extPath,
                    };

                    FileListSource.Add(entry);
                }

                foreach(var file in Project.Files)
                {
                    if (files.Contains(file.Key)) continue;

                    var extPath = file.Value ?? "";


                    var entry = new ProjectFileListEntry()
                    {
                        InternalPath = file.Key,
                        ExternalPath = extPath,
                    };

                    FileListSource.Add(entry);
                }
            });
        }

        public static void AddExternalSource(string internalFile, string externalFile, bool save = true, SmartImportOptions importOptions = null)
        {
            if (Project == null) return;
            if (string.IsNullOrWhiteSpace(internalFile)) return;
            if (!IOUtil.IsFFXIVInternalPath(internalFile)) return;
            if (IOUtil.IsMetaInternalFile(internalFile)) return;

            if (!string.IsNullOrWhiteSpace(externalFile))
            {
                if (!File.Exists(externalFile))
                {
                    externalFile = "";
                }
            } else
            {
                externalFile = "";
            }


            if (Project.Files.ContainsKey(internalFile))
            {
                Project.Files[internalFile] = externalFile;
            }
            else
            {
                Project.Files.Add(internalFile, externalFile);
            }

            if (externalFile != "")
            {
                if (!Project.LastModifiedTimes.ContainsKey(externalFile))
                {
                    Project.LastModifiedTimes.Add(externalFile, new FileInfo(externalFile).LastWriteTime);
                }

                if(importOptions != null)
                {
                    var options = CleanImportOptions(importOptions);

                    if (Project.ImportOptions.ContainsKey(externalFile)){
                        Project.ImportOptions[externalFile] = options;
                    }
                    else
                    {
                        Project.ImportOptions.Add(externalFile, options);
                    }
                }
            }

            DetatchEvents();
            AttachEvents();

            if (save)
            {
                SaveProject();
            }
        }

        private static SmartImportOptions CleanImportOptions(SmartImportOptions modelOptions)
        {
            var op = (SmartImportOptions) modelOptions.Clone();

            op.ModelOptions.IntermediaryFunction = null;
            op.ModelOptions.LoggingFunction = null;
            op.ModelOptions.ReferenceItem = null;
            return op;
        }

        private async void ResetFile_Click(object sender, RoutedEventArgs e)
        {

            if (FileListBox.SelectedItems.Count == 0) return;
            if (Project == null || MainWindow.UserTransaction == null || MainWindow.UserTransaction.State != ETransactionState.Open) return;

            // Remove the files from the transaction and project.
            var toReset = new List<string>();
            foreach (ProjectFileListEntry entry in FileListBox.SelectedItems)
            {
                toReset.Add(entry.InternalPath);
            }

            foreach (var f in toReset) {
                await MainWindow.UserTransaction.ResetFile(f);
                Project.Files.Remove(f);

                if(Project.TransactionSettings.Target == ETransactionTarget.FolderTree)
                {
                    var basePath = Path.GetDirectoryName(Project.JsonPath);
                    var treePath = Path.Combine(basePath, f);
                    File.Delete(treePath);
                }
            }

            // Remove file tracking info if the external file is no longer in use.
            var toRemove = new List<string>();
            foreach(var f in Project.LastModifiedTimes)
            {
                if(Project.Files.Any(x => x.Value == f.Key))
                {
                    continue;
                }

                toRemove.Add(f.Key);
            }

            foreach(var f in toRemove)
            {
                Project.LastModifiedTimes.Remove(f);
            }

            SaveProject();

        }

        private void AddFile_Click(object sender, RoutedEventArgs e)
        {
            var win = new ImportRawDialog() { Owner = this };
            win.ShowDialog();
        }

        private void ClearFile_Click(object sender, RoutedEventArgs e)
        {
            if (Project == null || MainWindow.UserTransaction == null || MainWindow.UserTransaction.State != ETransactionState.Open)
            {
                return;
            }
            var entry = (ProjectFileListEntry)((Button)sender).DataContext;

            if (Project.Files.ContainsKey(entry.InternalPath))
            {
                AddExternalSource(entry.InternalPath, "");
            }
            entry.ExternalPath = "";
        }

        private async void ChangeFile_Click(object sender, RoutedEventArgs e)
        {
            if (Project == null || MainWindow.UserTransaction == null || MainWindow.UserTransaction.State != ETransactionState.Open)
            {
                return;
            }
            var entry = (ProjectFileListEntry)((Button)sender).DataContext;

            var ofd = new OpenFileDialog();
            if (ofd.ShowDialog() != System.Windows.Forms.DialogResult.OK)
            {
                return;
            }

            try
            {
                IsEnabled = false;
                await SmartImport.Import(ofd.FileName, entry.InternalPath, XivStrings.TexTools, MainWindow.UserTransaction);
            }
            catch (Exception ex)
            {
                this.ShowError("Import Error", "An error occurred while importing the file:\n\n" + ex.Message);
                return;
            }
            finally
            {
                IsEnabled = true;
            }

            entry = this.FileListSource.FirstOrDefault(x => x.InternalPath == entry.InternalPath);
            if (entry == null) return;

            if (Project.Files.ContainsKey(entry.InternalPath))
            {
                AddExternalSource(entry.InternalPath, ofd.FileName);
            }
            entry.ExternalPath = ofd.FileName;

        }
    }
}
