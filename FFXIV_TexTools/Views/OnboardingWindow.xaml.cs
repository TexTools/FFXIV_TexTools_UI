using FFXIV_TexTools.Helpers;
using FFXIV_TexTools.Properties;
using FFXIV_TexTools.Resources;
using FolderSelect;
using HelixToolkit.Wpf.SharpDX.Elements2D;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
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
using xivModdingFramework.Cache;
using xivModdingFramework.Helpers;
using xivModdingFramework.Models.DataContainers;

namespace FFXIV_TexTools.Views
{

    internal class OnboardingViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        public ObservableCollection<KeyValuePair<string, string>> Languages { get; set; } = new ObservableCollection<KeyValuePair<string, string>>()
        {
            new KeyValuePair<string, string>("English", "en"),
            new KeyValuePair<string, string>("日本語 (Japanese)", "jp"),
            new KeyValuePair<string, string>("Deutsch (German)", "de"),
            new KeyValuePair<string, string>("Français (French)", "fr"),
            new KeyValuePair<string, string>("한국어 (Korean)", "ko"),
            new KeyValuePair<string, string>("汉语 (Chinese)", "zh"),
        };

        public static ObservableCollection<KeyValuePair<string, string>> ModelingToolsList { get; set; } = new ObservableCollection<KeyValuePair<string, string>>()
        {
            new KeyValuePair<string, string>("Blender", EModelingTool.Blender.ToString()),
            new KeyValuePair<string, string>("3DS Max", EModelingTool.Max.ToString()),
            new KeyValuePair<string, string>("Maya", EModelingTool.Maya.ToString()),
            new KeyValuePair<string, string>("Unity", EModelingTool.Unity.ToString()),
            new KeyValuePair<string, string>("Unreal", EModelingTool.Unreal.ToString()),
        };

        public ObservableCollection<KeyValuePair<string, string>> ModelingTools { get; set; } = ModelingToolsList;

        public ObservableCollection<KeyValuePair<string, bool>> UseCases { get; set; } = new ObservableCollection<KeyValuePair<string, bool>>()
        {
            new KeyValuePair<string, bool>("Create Mods", false),
            new KeyValuePair<string, bool>("Install or Use Mods", true),
        };


        public string FFXIV_Directory
        {
            get => Settings.Default.FFXIV_Directory ?? "";
            set
            {
                Settings.Default.FFXIV_Directory = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(FFXIV_Directory)));
            }
        }
        public string Save_Directory
        {
            get => Settings.Default.Save_Directory ?? "";
            set
            {
                Settings.Default.Save_Directory = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Save_Directory)));
            }
        }
        public string Backup_Directory
        {
            get => Settings.Default.Backup_Directory ?? "";
            set
            {
                Settings.Default.Backup_Directory = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Backup_Directory)));
            }
        }
        public string ModPack_Directory
        {
            get => Settings.Default.ModPack_Directory ?? "";
            set
            {
                Settings.Default.ModPack_Directory = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ModPack_Directory)));
            }
        }
        public string Application_Language
        {
            get => Settings.Default.Application_Language ?? "";
            set
            {
                Settings.Default.Application_Language = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Application_Language)));
            }
        }
        public string ModelingTool
        {
            get => Settings.Default.ModelingTool ?? "";
            set
            {
                Settings.Default.ModelingTool = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ModelingTool)));
            }
        }

        public bool LiveDangerously
        {
            get => Settings.Default.LiveDangerously;
            set
            {
                Settings.Default.LiveDangerously = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(LiveDangerously)));
            }
        }

    }

    /// <summary>
    /// Interaction logic for OnboardingWindow.xaml
    /// </summary>
    public partial class OnboardingWindow : Window
    {
        private OnboardingViewModel ViewModel
        {
            get
            {
                return DataContext as OnboardingViewModel;
            }
        }
        public OnboardingWindow()
        {
            DataContext = new OnboardingViewModel();
            InitializeComponent();

            UseCaseBox.SelectionChanged += UseCase_Changed;

        }




        private void Done_Click(object sender, RoutedEventArgs e)
        {
            if (!IsGameDirectoryValid(Settings.Default.FFXIV_Directory)) {
                ViewHelpers.ShowWarning(this, "Invalid FFXIV Directory", "You must select a valid FFXIV Install to continue.");
                return;
            }

            Settings.Default.Save();
            DialogResult = true;
            System.Windows.Forms.Application.Restart();
            System.Windows.Application.Current.Shutdown();
        }

        /// <summary>
        /// Primary Synchronous startup function for TexTools settings initialization.
        /// 
        /// Triggers the onboarding window if needed, or otherwise initializes the base system settings.
        /// </summary>
        public static void OnboardAndInitialize()
        {
            if (string.IsNullOrEmpty(Settings.Default.ModelingTool)
                || string.IsNullOrEmpty(Settings.Default.Application_Language)
                || !IsGameDirectoryValid(Settings.Default.FFXIV_Directory))
            {
                DoOnboarding();
                return;
            }

            InitializeSettings();
        }


        public static void DoOnboarding()
        {

            // Defaults
            Settings.Default.ModelingTool = SetDefault(Settings.Default.ModelingTool, EModelingTool.Blender.ToString());
            Settings.Default.Application_Language = SetDefault(Settings.Default.Application_Language, "en");

            Settings.Default.Save_Directory = SetDefault(Settings.Default.Save_Directory, 
                Path.GetFullPath(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) + "/TexTools/Saved"));

            Settings.Default.Backup_Directory = SetDefault(Settings.Default.Backup_Directory,
                Path.GetFullPath(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) + "/TexTools/Index_Backups"));

            Settings.Default.ModPack_Directory = SetDefault(Settings.Default.ModPack_Directory,
                Path.GetFullPath(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) + "/TexTools/ModPacks"));

            Settings.Default.FFXIV_Directory = SetDefault(Settings.Default.FFXIV_Directory,
                GetDefaultInstallDirectory());

            var wind = new OnboardingWindow();
            wind.WindowStartupLocation = WindowStartupLocation.CenterScreen;

            var res = wind.ShowDialog();
            if(res != true)
            {
                System.Windows.Application.Current.Shutdown();
                return;
            }

        }

        private static string SetDefault(string value, string def)
        {
            return string.IsNullOrWhiteSpace(value) ? def : value;
        }

        public static string GetDefaultInstallDirectory()
        {
            var resourceManager = CommonInstallDirectories.ResourceManager;
            var resourceSet = resourceManager.GetResourceSet(CultureInfo.CurrentCulture, true, true);

            string installDirectory = null;
            foreach (DictionaryEntry commonInstallPath in resourceSet)
            {
                if (!Directory.Exists(commonInstallPath.Value.ToString())) continue;
                return commonInstallPath.Value.ToString();
            }

            return installDirectory;
        }

        public static void InitializeSettings()
        {
            SetDirectories();
            XivCache.FrameworkSettings.DefaultTextureFormat = Settings.Default.CompressEndwalkerUpgradeTextures ? xivModdingFramework.Textures.Enums.XivTexFormat.BC7 : xivModdingFramework.Textures.Enums.XivTexFormat.A8R8G8B8;

            if (Enum.TryParse<EModelingTool>(Settings.Default.ModelingTool, true, out var mt))
            {
                XivCache.FrameworkSettings.ModelingTool = mt;
            }
            XivCache.FrameworkSettings.DefaultTextureFormat = Settings.Default.CompressEndwalkerUpgradeTextures ? xivModdingFramework.Textures.Enums.XivTexFormat.BC7 : xivModdingFramework.Textures.Enums.XivTexFormat.A8R8G8B8;

        }

        public static bool IsGameDirectoryValid(string dir)
        {
            if (string.IsNullOrEmpty(dir))
            {
                return false;
            }

            if (!dir.EndsWith("ffxiv"))
            {
                return false;
            }

            if (!Directory.Exists(dir))
            {
                return false;
            }

            return true;
        }




        /// <summary>
        /// Validates the various directories TexTools relies on.
        /// </summary>
        private static void SetDirectories()
        {
            // Create/assign directories if they don't exist already.
            SetSaveDirectory();

            SetBackupsDirectory();

            SetModPackDirectory();
        }

        private static void SetSaveDirectory()
        {
            if (!Directory.Exists(Properties.Settings.Default.Save_Directory))
            {
                Directory.CreateDirectory(Properties.Settings.Default.Save_Directory);
            }
        }

        private static void SetBackupsDirectory()
        {
            if (!Directory.Exists(Properties.Settings.Default.Backup_Directory))
            {
                Directory.CreateDirectory(Properties.Settings.Default.Backup_Directory);
            }
        }

        private static void SetModPackDirectory()
        {
            if (!Directory.Exists(Properties.Settings.Default.ModPack_Directory))
            {
                Directory.CreateDirectory(Properties.Settings.Default.ModPack_Directory);
            }
        }

        /// <summary>
        /// Resolves a valid TexTools desired FFXIV folder from a given user folder, if at all possible.
        /// </summary>
        /// <param name="path"></param>
        /// <param name="recursive"></param>
        /// <returns></returns>
        public static string ResolveFFXIVFolder(string path, bool recursive = false)
        {
            if (path.EndsWith("ffxiv"))
            {
                return path;
            }


            if (path.EndsWith("SquareEnix"))
            {
                path = Path.GetFullPath(Path.Combine(path, "FINAL FANTASY XIV - A Realm Reborn", "game", "sqpack", "ffxiv"));

                if (Directory.Exists(path))
                {
                    return path;
                }
            }

            if (path.EndsWith("FINAL FANTASY XIV - A Realm Reborn"))
            {
                path = Path.GetFullPath(Path.Combine(path, "game", "sqpack", "ffxiv"));

                if (Directory.Exists(path))
                {
                    return path;
                }
            }

            if (path.EndsWith("game"))
            {
                path = Path.GetFullPath(Path.Combine(path,"sqpack", "ffxiv"));

                if (Directory.Exists(path))
                {
                    return path;
                }
            }

            if (path.EndsWith("sqpack"))
            {
                path = Path.GetFullPath(Path.Combine(path, "ffxiv"));

                if (Directory.Exists(path))
                {
                    return path;
                }
            }

            if (path.EndsWith("steamapps"))
            {
                path = Path.GetFullPath(Path.Combine(path, "common", "FINAL FANTASY XIV Online", "game", "sqpack", "ffxiv"));

                if (Directory.Exists(path))
                {
                    return path;
                }
            }

            if (path.EndsWith("common"))
            {
                path = Path.GetFullPath(Path.Combine(path, "FINAL FANTASY XIV Online", "game", "sqpack", "ffxiv"));

                if (Directory.Exists(path))
                {
                    return path;
                }
            }

            if (path.EndsWith("FINAL FANTASY XIV Online"))
            {
                path = Path.GetFullPath(Path.Combine(path, "game", "sqpack", "ffxiv"));

                if (Directory.Exists(path))
                {
                    return path;
                }
            }

            var test = Path.GetFullPath(Path.Combine(path, "game", "sqpack", "ffxiv"));

            if (Directory.Exists(test))
            {
                return test;
            }

            if (!recursive)
            {
                var parent = IOUtil.GetParentIfExists(path, "game", false);
                if (!string.IsNullOrWhiteSpace(parent))
                {
                    return ResolveFFXIVFolder(parent, true);
                }

                parent = IOUtil.GetParentIfExists(path, "FINAL FANTASY XIV Online", false);
                if (!string.IsNullOrWhiteSpace(parent))
                {
                    return ResolveFFXIVFolder(parent, true);
                }

                parent = IOUtil.GetParentIfExists(path, "FINAL FANTASY XIV - A Realm Reborn", false);
                if (!string.IsNullOrWhiteSpace(parent))
                {
                    return ResolveFFXIVFolder(parent, true);
                }
            }


            return null;
        }

        private void SelectGamePath_Click(object sender, RoutedEventArgs e)
        {
            var ofd = new BetterFolderBrowser()
            {
                Title = "Select FFXIV Folder",
            };

            var previous = Settings.Default.FFXIV_Directory;
            if (!string.IsNullOrWhiteSpace(Settings.Default.FFXIV_Directory))
            {
                ofd.RootFolder = Settings.Default.FFXIV_Directory;
            } else if(!string.IsNullOrWhiteSpace(GetDefaultInstallDirectory()))
            {
                ofd.RootFolder = GetDefaultInstallDirectory();
            }

            var win = ViewHelpers.GetWin32Window(this);

            if (ofd.ShowDialog() != System.Windows.Forms.DialogResult.OK)
            {
                return;
            }

            var path = ResolveFFXIVFolder(ofd.SelectedFolder);

            while (!IsGameDirectoryValid(path))
            {
                FlexibleMessageBox.Show(win, "Invalid FFXIV Install", "Please select a valid FFXIV install folder.", MessageBoxButtons.OK, MessageBoxIcon.Question);
                if (ofd.ShowDialog() != System.Windows.Forms.DialogResult.OK)
                {
                    return;
                }
                path = ResolveFFXIVFolder(ofd.SelectedFolder);
            }

            ViewModel.FFXIV_Directory = path;
        }

        private void SelectSavePath_Click(object sender, RoutedEventArgs e)
        {
            var ofd = new BetterFolderBrowser()
            {
                Title = "Select Default Save Folder",
            };

            Directory.CreateDirectory(Settings.Default.Save_Directory);
            ofd.RootFolder = Path.GetFullPath(Settings.Default.Save_Directory);

            if (ofd.ShowDialog() != System.Windows.Forms.DialogResult.OK)
            {
                return;
            }

            ViewModel.Save_Directory = ofd.SelectedFolder;
        }

        private void SelectModpackPath_Click(object sender, RoutedEventArgs e)
        {
            var ofd = new BetterFolderBrowser()
            {
                Title = "Select Modpack Folder",
            };

            Directory.CreateDirectory(Settings.Default.ModPack_Directory);
            ofd.RootFolder = Path.GetFullPath(Settings.Default.ModPack_Directory);

            if (ofd.ShowDialog() != System.Windows.Forms.DialogResult.OK)
            {
                return;
            }

            ViewModel.ModPack_Directory = ofd.SelectedFolder;
        }

        private void SelectBackupPath_Click(object sender, RoutedEventArgs e)
        {
            var ofd = new BetterFolderBrowser()
            {
                Title = "Select Index Backup Folder",
            };

            Directory.CreateDirectory(Settings.Default.Backup_Directory);
            ofd.RootFolder = Path.GetFullPath(Settings.Default.Backup_Directory);

            if (ofd.ShowDialog() != System.Windows.Forms.DialogResult.OK)
            {
                return;
            }

            ViewModel.Backup_Directory = ofd.SelectedFolder;
        }

        private void UseCase_Changed(object sender, SelectionChangedEventArgs e)
        {
            var value = Settings.Default.LiveDangerously;
            if (value)
            {
                this.ShowWarning("Mod Installer Warning", "Please Note: While TexTools -CAN- operate as a Mod-Loader, it is not the tool's primary purpose.\n\nYou may find some related features cumbersome or awkward when compared to other Mod-Loaders. (Ex. Penumbra)");
            }
        }
    }
}
