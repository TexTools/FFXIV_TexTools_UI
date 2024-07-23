using FFXIV_TexTools.Annotations;
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
using System.Reflection;
using System.Security.Principal;
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
using xivModdingFramework.Mods;

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
            MainWindow.GetMainWindow().Restart();
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

            try
            {
                InitializeSettings();
            }
            catch
            {
                ViewHelpers.ShowError("Initialization Failure", "TexTools was unable to initialize all startup directories properly.\n\nPlease check your folder paths are valid and accessible.");
                DoOnboarding();
                return;
            }

            CheckRerunAdmin();
            try
            {
                ValidateModlist();
            }
            catch(Exception ex)
            {
                ViewHelpers.ShowError("Invalid FFXIV Path", "TexTools was unable to initialize properly with the given FFXIV path.");

                // Clear the FFXIV directory and reset them back to the init.
                Settings.Default.FFXIV_Directory = null;
                Settings.Default.Save();
                OnboardAndInitialize();
                return;
            }
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

            UpdateConsoleConfig();
            Properties.Settings.Default.SettingsSaving += (object sender, System.ComponentModel.CancelEventArgs e) => {
                UpdateConsoleConfig();
            };

        }

        private static void UpdateConsoleConfig()
        {
            ConsoleConfig.Update(x =>
            {
                x.XivPath = Settings.Default.FFXIV_Directory;
                x.Language = Settings.Default.Application_Language;
            });
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

        public static void ValidateModlist()
        {
            if (!Modding.ValidateModlist(Settings.Default.FFXIV_Directory))
            {
                ViewHelpers.ShowWarning(MainWindow.GetMainWindow(), "Invalid Modlist Error", "The Modlist file was invalid, corrupt, or from an incompatible TexTools version, and will be removed.\n\nPlease use Help => Download Index Backups/Start Over after TexTools has finished starting.");
            }
        }

        public static bool IsRunningAsAdministrator()
        {
            // Get current Windows user
            WindowsIdentity windowsIdentity = WindowsIdentity.GetCurrent();

            // Get current Windows user principal
            WindowsPrincipal windowsPrincipal = new WindowsPrincipal(windowsIdentity);

            // Return TRUE if user is in role "Administrator"
            return windowsPrincipal.IsInRole(WindowsBuiltInRole.Administrator);
        }
        public static bool CheckRerunAdminSimple()
        {
            var cwd = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetEntryAssembly().Location);
            var converterFolder = Path.GetFullPath(Path.Combine(cwd, "converters"));

            var allSuccess = true;
            allSuccess = allSuccess && TestDirectory(converterFolder);



            if (!allSuccess && !IsRunningAsAdministrator())
            {
                // Setting up start info of the new process of the same application
                ProcessStartInfo processStartInfo = new ProcessStartInfo(Assembly.GetEntryAssembly().CodeBase);

                // Using operating shell and setting the ProcessStartInfo.Verb to “runas” will let it run as admin
                processStartInfo.UseShellExecute = true;
                processStartInfo.Verb = "runas";
                processStartInfo.Arguments = GetRejoinedArgs();

                // Start the application as new process
                Process.Start(processStartInfo);

                // Shut down the current (old) process
                if (System.Windows.Application.Current != null)
                {
                    System.Windows.Application.Current.Shutdown();
                }
                return false;
            }
            return true;
        }
        public static void CheckRerunAdmin(bool throwErrors = false)
        {
            var cwd = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetEntryAssembly().Location);
            var converterFolder = Path.GetFullPath(Path.Combine(cwd, "converters"));

            var allSuccess = true;
            allSuccess = allSuccess && TestDirectory(Settings.Default.FFXIV_Directory);
            allSuccess = allSuccess && TestDirectory(Settings.Default.Backup_Directory);
            allSuccess = allSuccess && TestDirectory(Settings.Default.ModPack_Directory);
            allSuccess = allSuccess && TestDirectory(Settings.Default.Save_Directory);
            allSuccess = allSuccess && TestDirectory(converterFolder);

            if (!allSuccess && !IsRunningAsAdministrator()) 
            {
                if (throwErrors)
                {
                    throw new ApplicationException("Application must be run as administrator for proper file access due to current folder configurations.");
                }

                // Setting up start info of the new process of the same application
                ProcessStartInfo processStartInfo = new ProcessStartInfo(Assembly.GetEntryAssembly().CodeBase);

                // Using operating shell and setting the ProcessStartInfo.Verb to “runas” will let it run as admin
                processStartInfo.UseShellExecute = true;
                processStartInfo.Verb = "runas";
                processStartInfo.Arguments = GetRejoinedArgs();

                // Start the application as new process
                Process.Start(processStartInfo);

                // Shut down the current (old) process
                System.Windows.Application.Current.Shutdown();
            }
        }

        private static string GetRejoinedArgs()
        {
            var args = MainWindow._Args;
            if(args == null || args.Length == 0)
            {
                return "";
            }

            var st = "";
            foreach(var s in args)
            {
                st += '"' + s + '"' + ' ';
            }

            return st;
        }

        private static bool TestDirectory(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
            {
                return true;
            }

            var tempFile = Path.GetFullPath(Path.Combine(path, "tt_write_test.temp"));
            try
            {
                using (var fs = File.Create(tempFile))
                {

                }
                File.Delete(tempFile);
                return true;
            }
            catch
            {
                return false;
            }
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
            var qlDir = PenumbraAPI.GetQuickLauncherGameDirectory();

            // If the user has the quick launcher configured, use that.
            if (!string.IsNullOrWhiteSpace(qlDir))
            {
                return qlDir;
            }

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

        public static bool IsGameDirectoryValid(string dir)
        {
            if (string.IsNullOrWhiteSpace(dir))
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

            try
            {
                var di = new DirectoryInfo(dir);
                var par = di.Parent.Parent;
                var path = Path.GetFullPath(Path.Combine(par.FullName, "ffxiv_dx11.exe"));
                if (!File.Exists(path))
                {
                    return false;
                }
            }
            catch
            {
                return false;
            }


            return true;
        }


        /// <summary>
        /// Resolves a valid TexTools desired FFXIV folder from a given user folder, if at all possible.
        /// </summary>
        /// <param name="path"></param>
        /// <param name="recursive"></param>
        /// <returns></returns>
        public static string ResolveFFXIVFolder(string path, bool recursive = false)
        {
            const string _exe = "ffxiv_dx11.exe";

            // Only allow base ffxiv folder selection if it has the EXE in it,
            // to avoid issues with users having a parent folder name 'ffxiv'.
            if (path.EndsWith("ffxiv"))
            {
                if (File.Exists(Path.Combine(path, _exe)))
                {
                    return path;
                }
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
