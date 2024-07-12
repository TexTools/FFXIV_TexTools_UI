using AutoUpdaterDotNET;
using FFXIV_TexTools.Models;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using WK.Libraries.BetterFolderBrowserNS;
using xivModdingFramework.Cache;
using xivModdingFramework.Helpers;
using xivModdingFramework.Mods;
using xivModdingFramework.Mods.DataContainers;
using static FFXIV_TexTools.Models.PenumbraUpgradeStatus;

namespace FFXIV_TexTools.Views.Upgrades
{
    /// <summary>
    /// Interaction logic for PenumbraLibraryUpgradeWindow.xaml
    /// </summary>
    public partial class PenumbraLibraryUpgradeWindow : Window, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        private string _PenumbraPath;
        public string PenumbraPath
        {
            get => _PenumbraPath;
            set
            {
                _PenumbraPath = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(PenumbraPath)));
            }
        }

        private string _DestinationPath;
        public string DestinationPath
        {
            get => _DestinationPath;
            set
            {
                _DestinationPath = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DestinationPath)));
            }
        }

        private string _ContinuePauseText;
        public string ContinuePauseText
        {
            get => _ContinuePauseText;
            set
            {
                _ContinuePauseText = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ContinuePauseText)));
            }
        }

        private string _RemainingText;
        public string RemainingText
        {
            get => _RemainingText;
            set
            {
                _RemainingText = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(RemainingText)));
            }
        }
        private string _ProcessedText;
        public string ProcessedText
        {
            get => _ProcessedText;
            set
            {
                _ProcessedText = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ProcessedText)));
            }
        }


        private string _StatusText;
        public string StatusText
        {
            get => _StatusText;
            set
            {
                _StatusText = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(StatusText)));
            }
        }
        private bool _PathEnabled;
        public bool PathEnabled
        {
            get => _PathEnabled;
            set
            {
                _PathEnabled = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(PathEnabled)));
            }
        }
        private bool _ContinuePauseEnabled;
        public bool ContinuePauseEnabled
        {
            get => _ContinuePauseEnabled;
            set
            {
                _ContinuePauseEnabled = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ContinuePauseEnabled)));
            }
        }

        PenumbraUpgradeStatus Results;


        const string _JsonName = "upgrade_status.json";
        string JsonPath {
            get
            {
                if (string.IsNullOrEmpty(DestinationPath)) return "";
                return Path.GetFullPath(Path.Combine(DestinationPath, _JsonName));
            }
        }

        public ObservableCollection<KeyValuePair<string, string>> RemainingMods { get; set; } = new ObservableCollection<KeyValuePair<string, string>>();
        public ObservableCollection<KeyValuePair<string, string>> ProcessedMods { get; set; } = new ObservableCollection<KeyValuePair<string, string>>();

        public enum UpgradeState
        {
            Stopped,
            Working,
            Paused,
            Completed
        };


        private UpgradeState _State;
        public UpgradeState State {
            get => _State;
            set
            {
                _State = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(State)));

                if(State == UpgradeState.Stopped)
                {
                    ContinuePauseText = "Start";
                    PathEnabled = true;
                } else if(State == UpgradeState.Paused)
                {
                    ContinuePauseText = "Resume";
                    PathEnabled = true;
                }
                else if (State == UpgradeState.Working)
                {
                    ContinuePauseText = "Pause";
                    PathEnabled = false;
                }
                else if (State == UpgradeState.Completed)
                {
                    ContinuePauseText = "Complete";
                    ContinuePauseEnabled = false;
                    PathEnabled = false;
                }
            }
        }

        public PenumbraLibraryUpgradeWindow()
        {
            DataContext = this;
            InitializeComponent();

            State = UpgradeState.Stopped;
            Closing += PenumbraLibraryUpgradeWindow_Closing;
            CheckPaths();

            var p = IOUtil.GetPenumbraDirectory();
            if (!string.IsNullOrWhiteSpace(p)) {
                PenumbraPath = p;
            }
        }

        private void PenumbraLibraryUpgradeWindow_Closing(object sender, CancelEventArgs e)
        {
            _RequestStop = true;
            Owner?.Activate();
        }

        private async void ContinuePause_Click(object sender, RoutedEventArgs e)
        {
            if(State == UpgradeState.Working)
            {
                await Stop();
            } else if (State == UpgradeState.Stopped || State == UpgradeState.Paused)
            {
                await Start();
            }
        }

        private void SelectPenumbraPath_Click(object sender, RoutedEventArgs e)
        {
            var bfb = new BetterFolderBrowser() { Title = "Select Penumbra Library..." };
            bfb.RootFolder = PenumbraPath;
            if(bfb.ShowDialog() != System.Windows.Forms.DialogResult.OK)
            {
                return;
            }

            PenumbraPath = bfb.SelectedFolder;
            CheckPaths();
        }

        private void SelectDestinationPath_Click(object sender, RoutedEventArgs e)
        {
            var bfb = new BetterFolderBrowser() { Title = "Select Destination Folder..." };
            bfb.RootFolder = DestinationPath;
            if (bfb.ShowDialog() != System.Windows.Forms.DialogResult.OK)
            {
                return;
            }

            DestinationPath = bfb.SelectedFolder;
            CheckPaths();
        }

        private void CheckPaths()
        {
            if (string.IsNullOrWhiteSpace(PenumbraPath) || string.IsNullOrWhiteSpace(DestinationPath)
                || PenumbraPath.Contains(DestinationPath + Path.DirectorySeparatorChar) || DestinationPath.Contains(PenumbraPath + Path.DirectorySeparatorChar)
                || DestinationPath == PenumbraPath || !Directory.Exists(PenumbraPath))
            {
                if (string.IsNullOrWhiteSpace(PenumbraPath) || string.IsNullOrWhiteSpace(DestinationPath))
                {
                    ContinuePauseEnabled = false;
                    Results = null;
                    return;
                }
                else if (!Directory.Exists(PenumbraPath))
                {
                    ViewHelpers.ShowWarning(this, "Invalid Folder Settings", "Penumbra Library does not exist.");
                }
                else
                {
                    ViewHelpers.ShowWarning(this, "Invalid Folder Settings", "The Source/Destination folders cannot be sub-folders of each other. \nPlease use an empty folder that is not part of your Penumbra Library.");
                }
                return;
            }

            var existing = File.Exists(JsonPath);
            var fcount = Directory.GetFiles(DestinationPath).Length;
            var dcount = Directory.GetDirectories(DestinationPath).Length;
            if ((fcount > 0 || dcount > 0) && !existing)
            {
                ContinuePauseEnabled = false;
                Results = null;
                ViewHelpers.ShowError("Invalid Target Folder", "Please select or create a new empty folder to use for the copy.");
                return;
            }


            ContinuePauseEnabled = true;

            Directory.CreateDirectory(DestinationPath);

            var di = new DirectoryInfo(DestinationPath);

            LoadOrCreateJson();
            UpdateLists();
        }

        private void LoadOrCreateJson()
        {

            if (File.Exists(JsonPath))
            {
                try
                {
                    Results = JsonConvert.DeserializeObject<PenumbraUpgradeStatus>(File.ReadAllText(JsonPath));
                }
                catch
                {
                    Results = new PenumbraUpgradeStatus();
                }
            }
            else
            {
                Results = new PenumbraUpgradeStatus();
            }

            var di = new DirectoryInfo(PenumbraPath);
            var children = di.EnumerateDirectories().ToList();

            foreach(var c in children)
            {
                if (!Results.Upgrades.ContainsKey(c.Name))
                {
                    var metaPath = Path.GetFullPath(Path.Combine(c.FullName, "meta.json"));
                    if (File.Exists(metaPath))
                    {
                        Results.Upgrades.Add(c.Name, PenumbraUpgradeStatus.EUpgradeResult.NotStarted);
                    }
                }
            }

            if (Results.Upgrades.Any(x => x.Value != PenumbraUpgradeStatus.EUpgradeResult.NotStarted))
            {
                State = UpgradeState.Paused;
            } else
            {
                State = UpgradeState.Stopped;
            }

            File.WriteAllText(JsonPath, JsonConvert.SerializeObject(Results, Formatting.Indented));
        }

        private void SaveJson()
        {
            if (Results == null || string.IsNullOrEmpty(DestinationPath)) return;
            File.WriteAllText(JsonPath, JsonConvert.SerializeObject(Results, Formatting.Indented));
        }
        private void UpdateLists()
        {
            RemainingMods.Clear();
            ProcessedMods.Clear();

            if (Results == null) return;

            var proc = 0;
            var rem = 0;
            foreach(var m in Results.Upgrades)
            {
                if(m.Value == PenumbraUpgradeStatus.EUpgradeResult.NotStarted
                    || m.Value == PenumbraUpgradeStatus.EUpgradeResult.InProgress)
                {
                    rem++;
                    RemainingMods.Add(new KeyValuePair<string, string>(m.Key, m.Key));
                } else if (m.Value == PenumbraUpgradeStatus.EUpgradeResult.Success)
                {
                    proc++;
                    ProcessedMods.Add(new KeyValuePair<string, string>("✓ " + m.Key, m.Key));
                } else if(m.Value == PenumbraUpgradeStatus.EUpgradeResult.Failure)
                {
                    proc++;
                    ProcessedMods.Add(new KeyValuePair<string, string>("𝑿 " + m.Key, m.Key));
                } else if (m.Value == PenumbraUpgradeStatus.EUpgradeResult.Unchanged)
                {
                    proc++;
                    ProcessedMods.Add(new KeyValuePair<string, string>("-- " + m.Key, m.Key));
                }
            }

            ProcessedText = "Processed Mods: " + proc.ToString();
            RemainingText = "Remaining Mods: " + rem.ToString();

        }


        private bool _RequestStop = false;
        private async Task Start()
        {
            if (Results == null) return;
            State = UpgradeState.Working;
            _RequestStop = false;
            ContinuePauseEnabled = true;

            await Task.Run(async () =>
            {
                var workerState = XivCache.CacheWorkerEnabled;
                await XivCache.SetCacheWorkerState(false);
                var nextMod = Results.Upgrades.FirstOrDefault(x => x.Value == PenumbraUpgradeStatus.EUpgradeResult.NotStarted);
                while (!string.IsNullOrWhiteSpace(nextMod.Key) && !_RequestStop)
                {
                    var mod = nextMod.Key;
                    Dispatcher.Invoke(() =>
                    {
                        StatusText = "Processing Mod: " + mod;
                    });

                    var res = await Results.ProcessMod(PenumbraPath, DestinationPath, mod);

                    SaveJson();

                    // Always clear the temp folder between actions.
                    try
                    {
                        IOUtil.ClearTempFolder();
                    }
                    catch
                    {
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            ViewHelpers.ShowWarning(this, "Temp File Clear Error", "Unable to clear TexTools temp folder.\nThe mod as converted successfully, cannot clear its temp folder due to unusual permissions issues with some mod file that was used.\n\nPlease manually delete the folder: %TEMP%/xivmf/\n\n(The upgrade process has been paused.)");
                        });
                        _RequestStop = true;
                    }


                    Dispatcher.Invoke(() =>
                    {
                        UpdateLists();
                    });
                    nextMod = Results.Upgrades.FirstOrDefault(x => x.Value == PenumbraUpgradeStatus.EUpgradeResult.NotStarted);
                }

                if (_RequestStop)
                {
                    State = UpgradeState.Paused;
                    ContinuePauseEnabled = true;
                } else
                {
                    StatusText = "Complete! :D";
                    State = UpgradeState.Completed;
                }

                await XivCache.SetCacheWorkerState(workerState);
            });
        }
        private async Task Stop()
        {
            if (Results == null) return;
            _RequestStop = true;
            ContinuePauseEnabled = false;
        }

        private void Explain_Click(object sender, RoutedEventArgs e)
        {
            var wind = new PenumbraLibraryUpgradeHelp() { Owner = this, WindowStartupLocation = WindowStartupLocation.CenterOwner };
            wind.Show();
        }

        private void SkipItems_Click(object sender, RoutedEventArgs e)
        {
            if(State == UpgradeState.Working)
            {
                ViewHelpers.ShowWarning(this, "Busy Warning", "Cannot skip mods while the updater is running.  Please pause the updater before skipping mods.");
                return;
            }

            var items = RemainingModsBox.SelectedItems;
            foreach(KeyValuePair<string, string> kv in items)
            {
                if (Results.Upgrades.ContainsKey(kv.Key)) {
                    Results.Upgrades[kv.Key] = PenumbraUpgradeStatus.EUpgradeResult.Failure;
                    try
                    {

                        var source = Path.GetFullPath(Path.Combine(PenumbraPath, kv.Key));
                        var target = Path.GetFullPath(Path.Combine(DestinationPath, kv.Key));

                        if (source != target)
                        {
                            IOUtil.RecursiveDeleteDirectory(target);
                        }

                        Directory.CreateDirectory(target);
                        if (source != target)
                        {
                            IOUtil.RecursiveDeleteDirectory(target);
                            IOUtil.CopyFolder(source, target);
                        }
                    }
                    catch(Exception ex) 
                    {
                        ViewHelpers.ShowError("Mod Copy Failure:", "The mod: " + kv.Key + " was unable to be copied to the destination folder.\n\nThis is usually due to a permissions issue or the mod paths being too long for windows.\n\nYou will need to copy the mod folder over by hand.");
                    }
                }
            }

            SaveJson();
            UpdateLists();
        }

        private void Details_Click(object sender, RoutedEventArgs e)
        {
            if (Results == null) return;
            if (Results.Upgrades.Count == 0) return;
            var wind = new PenumbraUpgradeResults(Results) { Owner = this, WindowStartupLocation = WindowStartupLocation.CenterOwner };
            wind.Show();
        }
    }
}
