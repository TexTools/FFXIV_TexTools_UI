// FFXIV TexTools
// Copyright © 2019 Rafael Gonzalez - All Rights Reserved
// 
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
// 
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
// 
// You should have received a copy of the GNU General Public License
// along with this program.  If not, see <http://www.gnu.org/licenses/>.

using AutoUpdaterDotNET;
using FFXIV_TexTools.Helpers;
using FFXIV_TexTools.Properties;
using FFXIV_TexTools.Resources;
using FFXIV_TexTools.Views;
using FolderSelect;
using ForceUpdateAssembly;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media;
using WK.Libraries.BetterFolderBrowserNS;
using xivModdingFramework.Cache;
using xivModdingFramework.General.Enums;
using xivModdingFramework.Helpers;
using xivModdingFramework.Models.DataContainers;
using xivModdingFramework.Models.FileTypes;
using xivModdingFramework.Models.ModelTextures;

namespace FFXIV_TexTools.ViewModels
{
    public class CustomizeViewModel : INotifyPropertyChanged
    {
        private string _defaultAuthor = Settings.Default.Default_Author;
        private string _defaultModpackUrl = Settings.Default.Default_Modpack_Url;
        const string _bgColorDefault = "#FF777777";
        private CustomizeSettingsView _view;
        private string UserDefBatchExportDirectory = ""; // Workaround: private field

        public ObservableCollection<KeyValuePair<string, string>> ModelingTools { get; set; } = OnboardingViewModel.ModelingToolsList;

        public ObservableCollection<KeyValuePair<string, int>> ImageSizes { get; set; } = new ObservableCollection<KeyValuePair<string, int>>()
        {
            new KeyValuePair<string, int>("None", 0),
            new KeyValuePair<string, int>("512", 512),
            new KeyValuePair<string, int>("1024", 1024),
            new KeyValuePair<string, int>("2048", 2048),
            new KeyValuePair<string, int>("4096", 4096),
        };

        public ObservableCollection<KeyValuePair<string, string>> ImageFormats { get; set; } = new ObservableCollection<KeyValuePair<string, string>>()
        {
            new KeyValuePair<string, string>("DDS", "dds"),
            new KeyValuePair<string, string>("TGA", "tga"),
            new KeyValuePair<string, string>("PNG", "png"),
        };

        public ObservableCollection<KeyValuePair<string, string>> ModpackFormats { get; set; } = new ObservableCollection<KeyValuePair<string, string>>()
        {
            new KeyValuePair<string, string>("PMP", "pmp"),
            new KeyValuePair<string, string>("TTMP2", "ttmp2"),
        };
        public CustomizeViewModel(CustomizeSettingsView view)
        {
            _view = view;
            SkinTypes = new List<string>
            {
                {XivStringRaces.Hyur_M},
                {XivStringRaces.Hyur_H},
                {XivStringRaces.Aura_R},
                {XivStringRaces.Aura_X}
            };

            DefaultRaces = new List<string>
            {
                XivRace.Hyur_Midlander_Male.GetDisplayName(),
                XivRace.Hyur_Midlander_Female.GetDisplayName(),
                XivRace.Hyur_Highlander_Male.GetDisplayName(),
                XivRace.Hyur_Highlander_Female.GetDisplayName(),
                XivRace.Elezen_Male.GetDisplayName(),
                XivRace.Elezen_Female.GetDisplayName(),
                XivRace.Miqote_Male.GetDisplayName(),
                XivRace.Miqote_Female.GetDisplayName(),
                XivRace.Roegadyn_Male.GetDisplayName(),
                XivRace.Roegadyn_Female.GetDisplayName(),
                XivRace.Lalafell_Male.GetDisplayName(),
                XivRace.Lalafell_Female.GetDisplayName(),
                XivRace.AuRa_Male.GetDisplayName(),
                XivRace.AuRa_Female.GetDisplayName(),
                XivRace.Viera_Male.GetDisplayName(),
                XivRace.Viera_Female.GetDisplayName(),
                XivRace.Hrothgar_Male.GetDisplayName(),
                XivRace.Hrothgar_Female.GetDisplayName()
            };

            UpdateBranches  = new List<string>
            {
                UIStrings.Version_Stable,
                UIStrings.Version_Latest
            };
        }

        #region Public Properties

        /// <summary>
        /// The FFXIV directory
        /// </summary>
        public string FFXIV_Directory
        {
            get => Path.GetFullPath(Settings.Default.FFXIV_Directory);
            set => NotifyPropertyChanged(nameof(FFXIV_Directory));
        }

        /// <summary>
        /// The save directory
        /// </summary>
        public string Save_Directory
        {
            get => Path.GetFullPath(Settings.Default.Save_Directory);
            set => NotifyPropertyChanged(nameof(Save_Directory));
        }

        /// <summary>
        /// The backups directory
        /// </summary>
        public string Backups_Directory
        {
            get => Path.GetFullPath(Settings.Default.Backup_Directory);
            set => NotifyPropertyChanged(nameof(Backups_Directory));
        }

        /// <summary>
        /// The modpack directory
        /// </summary>
        public string ModPack_Directory
        {
            get => Path.GetFullPath(Settings.Default.ModPack_Directory);
            set => NotifyPropertyChanged(nameof(ModPack_Directory));
        }

        /// <summary>
        /// The batch export directory
        /// </summary>
        public string BatchExport_Directory
        {
            get
            {
                // Ensure directory exists or return empty string to avoid errors with Path.GetFullPath on null/empty
                // if (string.IsNullOrEmpty(Settings.Default.BatchExportDirectory)) // Original
                if (string.IsNullOrEmpty(UserDefBatchExportDirectory)) // Workaround
                {
                    // Optionally, create a default directory here if one doesn't exist.
                    // For now, just return empty or a placeholder if not set.
                    // Consider returning a user-friendly placeholder like "Not Set" or an actual default path.
                    // For binding, an actual path or empty string is better than null.
                    return ""; // Or Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) for a fallback.
                }
                // return Path.GetFullPath(Settings.Default.BatchExportDirectory); // Original
                return Path.GetFullPath(UserDefBatchExportDirectory); // Workaround
            }
            set
            {
                // Settings.Default.BatchExportDirectory = value; // Original
                // Settings.Default.Save(); // Original
                UserDefBatchExportDirectory = value; // Workaround
                NotifyPropertyChanged(nameof(BatchExport_Directory));
            }
        }


        /// <summary>
        /// The default author
        /// </summary>
        public string DefaultAuthor
        {
            get => _defaultAuthor;
            set
            {
                _defaultAuthor = value;
                NotifyPropertyChanged(nameof(DefaultAuthor));
            }
        }

        ///<summary>
        /// The default modpack url
        /// </summary>
        public string DefaultModpackUrl
        {
            get => _defaultModpackUrl;
            set
            {
                _defaultModpackUrl = value;
                NotifyPropertyChanged(nameof(DefaultModpackUrl));

                if (String.IsNullOrWhiteSpace(value) || IOUtil.ValidateUrl(value) != null)
                {
                    Settings.Default.Default_Modpack_Url = value?.Trim();
                    Settings.Default.Save();
                }
            }
        }
        public string DefaultModpackFormat
        {
            get => Settings.Default.Default_Modpack_Format;
            set
            {
                Settings.Default.Default_Modpack_Format = value;
                Settings.Default.Save();
                NotifyPropertyChanged(nameof(DefaultModpackFormat));
            }
        }
        public string DefaultImageFormat
        {
            get => Settings.Default.Default_Image_Format;
            set
            {
                Settings.Default.Default_Image_Format = value;
                Settings.Default.Save();
                NotifyPropertyChanged(nameof(DefaultImageFormat));
            }
        }

        /// <summary>
        /// The selected skin color
        /// </summary>
        public Color Selected_SkinColor
        {
            get => (Color)ColorConverter.ConvertFromString(Settings.Default.Skin_Color);
            set
            {
                SetSkinColor(value);
                NotifyPropertyChanged(nameof(Selected_SkinColor));
            }
        }

        /// <summary>
        /// The selected hair color
        /// </summary>
        public Color Selected_HairColor
        {
            get => (Color)ColorConverter.ConvertFromString(Settings.Default.Hair_Color);
            set
            {
                SetHairColor(value);
                NotifyPropertyChanged(nameof(Selected_HairColor));
            }
        }
        /// <summary>
        /// The selected hair color
        /// </summary>
        public Color Selected_HairHighlightColor
        {
            get => (Color)ColorConverter.ConvertFromString(Settings.Default.Hair_Highlight_Color);
            set
            {
                SetHairHighlightColor(value);
                NotifyPropertyChanged(nameof(Selected_HairHighlightColor));
            }
        }

        /// <summary>
        /// The selected iris color
        /// </summary>
        public Color Selected_IrisColor
        {
            get => (Color)ColorConverter.ConvertFromString(Settings.Default.Eye_Color);
            set
            {
                SetIrisColor(value);
                NotifyPropertyChanged(nameof(Selected_IrisColor));
            }
        }

        /// <summary>
        /// The selected etc. color
        /// </summary>
        public Color Selected_TattooColor
        {
            get => (Color)ColorConverter.ConvertFromString(Settings.Default.Tattoo_Color);
            set
            {
                SetTattooColor(value);
                NotifyPropertyChanged(nameof(Selected_TattooColor));
            }
        }

        public Color Selected_FurnitureColor
        {
            get => (Color)ColorConverter.ConvertFromString(Settings.Default.Furniture_Color);
            set
            {
                SetFurnitureColor(value);
                NotifyPropertyChanged(nameof(Selected_FurnitureColor));
            }
        }



        /// <summary>
        /// The selected etc. color
        /// </summary>
        public Color Selected_LipColor
        {
            get => (Color)ColorConverter.ConvertFromString(Settings.Default.Lip_Color);
            set
            {
                SetLipColor(value);
                NotifyPropertyChanged(nameof(Selected_LipColor));
            }
        }

        /// <summary>
        /// The selected background color for the 3D viewport
        /// </summary>
        public Color Selected_BgColor
        {
            get => (Color)ColorConverter.ConvertFromString(Settings.Default.BG_Color);
            set
            {
                SetBgColor(value);
                NotifyPropertyChanged(nameof(Selected_BgColor));
            }
        }

        /// <summary>
        /// The list of skin types
        /// </summary>
        public List<string> SkinTypes { get; }

        public List<string> Target3DPrograms { get; }

        /// <summary>
        /// The selected skin type
        /// </summary>
        public string Selected_SkinType
        {
            get => Settings.Default.Default_Race;
            set
            {
                if (Selected_SkinType != value)
                {
                    SetSkin(value);
                    NotifyPropertyChanged(nameof(Selected_SkinType));
                }
            }
        }
        
        public bool AutoFixDawntrail
        {
            get => Settings.Default.FixPreDawntrailOnImport;
            set
            {
                if (AutoFixDawntrail != value)
                {
                    Settings.Default.FixPreDawntrailOnImport = value;
                    Settings.Default.Save();
                    NotifyPropertyChanged(nameof(AutoFixDawntrail));
                }
            }
        }

        public bool AutoFixPartialDawntrail
        {
            get => Settings.Default.FixPreDawntrailPartialOnImport;
            set
            {
                if (AutoFixPartialDawntrail != value)
                {
                    Settings.Default.FixPreDawntrailPartialOnImport = value;
                    Settings.Default.Save();
                    NotifyPropertyChanged(nameof(AutoFixPartialDawntrail));
                }
            }
        }
        public int MaxImageSize
        {
            get => Settings.Default.MaxImageSize;
            set
            {
                if (MaxImageSize != value)
                {
                    Settings.Default.MaxImageSize = value;
                    Settings.Default.Save();
                    NotifyPropertyChanged(nameof(MaxImageSize));
                }
            }
        }
        public bool UnsafeMode
        {
            get => Settings.Default.LiveDangerously;
            set
            {
                if (UnsafeMode != value)
                {
                    Settings.Default.LiveDangerously = value;
                    Settings.Default.Save();
                    NotifyPropertyChanged(nameof(UnsafeMode));
                }
            }
        }
        public bool ShiftExportUV
        {
            get => Settings.Default.ShiftExportUV;
            set
            {
                if (ShiftExportUV != value)
                {
                    Settings.Default.ShiftExportUV = value;
                    Settings.Default.Save();
                    NotifyPropertyChanged(nameof(ShiftExportUV));
                }
            }
        }
        public bool OpenTxByDefault
        {
            get => Settings.Default.OpenTransactionOnStart;
            set
            {
                if (OpenTxByDefault != value)
                {
                    Settings.Default.OpenTransactionOnStart = value;
                    Settings.Default.Save();
                    NotifyPropertyChanged(nameof(OpenTxByDefault));
                }
            }
        }

        public bool CompressUpgradeTextures
        {
            get => Settings.Default.CompressEndwalkerUpgradeTextures;
            set
            {
                if (CompressUpgradeTextures != value)
                {
                    Settings.Default.CompressEndwalkerUpgradeTextures = value;
                    Settings.Default.Save();
                    XivCache.FrameworkSettings.DefaultTextureFormat = Settings.Default.CompressEndwalkerUpgradeTextures ? xivModdingFramework.Textures.Enums.XivTexFormat.BC7 : xivModdingFramework.Textures.Enums.XivTexFormat.A8R8G8B8;
                    NotifyPropertyChanged(nameof(CompressUpgradeTextures));
                }
            }
        }

        public string ModelingTool
        {
            get => Settings.Default.ModelingTool;
            set
            {
                if(Enum.TryParse<EModelingTool>(value, false, out var tool)) {
                    Settings.Default.ModelingTool = value;
                    UpdateFrameworkColors();
                    NotifyPropertyChanged(nameof(ModelingTool));
                    Settings.Default.Save();
                }
            }
        }

        public string UpdateBranch
        {
            get => MainWindow.IsBetaVersion ? UIStrings.Version_Latest : UIStrings.Version_Stable;
            set
            {

                if (UpdateBranch != value)
                {
                    SetUpdateBranch(value);
                    NotifyPropertyChanged(nameof(UpdateBranch));
                }
            }
        }

        public void SetUpdateBranch(string v)
        {
            MainWindow.MakeHighlander();

            var result = FlexibleMessageBox.Show("TexTools will now change to the selected update branch.".L(), "Branch Change Notice".L(), MessageBoxButtons.OKCancel, MessageBoxIcon.Information);
            if (result != DialogResult.OK) return;

            var beta = false;
            if (v == UIStrings.Version_Latest)
            {
                beta = true;
            }

            // Force an update when changing branches.
            var assembly = typeof(ForceUpdateAssemblyStub).Assembly;

            AutoUpdater.Mandatory = true;
            AutoUpdater.Synchronous = true;
            AutoUpdater.UpdateMode = Mode.ForcedDownload;
            try
            {
                if (beta)
                {
                    AutoUpdater.Start(WebUrl.TexTools_Beta_Update_Url, assembly);
                }
                else
                {
                    AutoUpdater.Start(WebUrl.TexTools_Update_Url, assembly);
                }
            }
            catch
            {
                AutoUpdater.Start(WebUrl.TexTools_Update_Url, assembly);
            }


            if (System.Windows.Application.Current != null)
            {
                System.Windows.Application.Current.Shutdown();
            }
        }

        public bool ExportAllBones
        {
            get
            {
                return Settings.Default.ExportAllBones;
            }
            set
            {
                if (ExportAllBones != value)
                {
                    SetExportAllBones(value);
                    NotifyPropertyChanged(nameof(ExportAllBones));
                }
            }
        }
        public void SetExportAllBones(bool value)
        {
            Settings.Default.ExportAllBones = value;
            Settings.Default.Save();
            UpdateCacheSettings();
        }


        public bool AutoMaterialFix
        {
            get => Settings.Default.AutoMaterialFix;
            set
            {
                if (AutoMaterialFix != value)
                {
                    SetAutoMaterialFix(value);
                    NotifyPropertyChanged(nameof(AutoMaterialFix));
                }

            }
        }

        public void SetAutoMaterialFix(bool value)
        {
            Settings.Default.AutoMaterialFix = value;
            Settings.Default.Save();
        }


        /// <summary>
        /// The list of default races
        /// </summary>
        public List<string> DefaultRaces { get; }

        public List<string> UpdateBranches { get; }

        /// <summary>
        /// The selected default race
        /// </summary>
        public string SelectedDefaultRace
        {
            get
            {
                try
                {
                    return Settings.Default.Default_Race_Selection;
                }
                catch
                {
                    return XivRace.Hyur_Midlander_Male.GetDisplayName();
                }
            }
            set
            {
                if (SelectedDefaultRace != value)
                {
                    SetDefaultRace(value);
                }
            }
        }

        public bool ExportTextureAsDDS
        {
            get => Settings.Default.ExportTexDDS;
            set
            {
                Settings.Default.ExportTexDDS = value;
                Settings.Default.Save();
                NotifyPropertyChanged(nameof(ExportTextureAsDDS));
                NotifyPropertyChanged(nameof(ExportTexDisplay));
                EnsureValidExportFormat();
            }
        }

        public bool ExportTextureAsBMP
        {
            get => Settings.Default.ExportTexBMP;
            set
            {
                Settings.Default.ExportTexBMP = value;
                Settings.Default.Save();
                NotifyPropertyChanged(nameof(ExportTextureAsBMP));
                NotifyPropertyChanged(nameof(ExportTexDisplay));
                EnsureValidExportFormat();
            }
        }

        public bool ExportTextureAsPNG
        {
            get => Settings.Default.ExportTexPNG;
            set
            {
                Settings.Default.ExportTexPNG = value;
                Settings.Default.Save();
                NotifyPropertyChanged(nameof(ExportTextureAsPNG));
                NotifyPropertyChanged(nameof(ExportTexDisplay));
                EnsureValidExportFormat();
            }
        }

        private void EnsureValidExportFormat()
        {
            if (!Settings.Default.ExportTexPNG &&
                !Settings.Default.ExportTexBMP &&
                !Settings.Default.ExportTexDDS)
            {
                this.ExportTextureAsDDS = true;
            }
        }

        public string ExportTexDisplay
        {
            get
            {
                string val = string.Empty;

                if (this.ExportTextureAsDDS)
                    val += "DDS";

                if (this.ExportTextureAsBMP)
                    val += string.IsNullOrEmpty(val) ? "BMP" : ", BMP";

                 if (this.ExportTextureAsPNG)
                    val += string.IsNullOrEmpty(val) ? "PNG" : ", PNG";

                return val;
            }
        }

        #endregion

        #region Commands

        // Button commands
        public ICommand FFXIV_SelectDir => new RelayCommand(FFXIVSelectDir);
        public ICommand Save_SelectDir => new RelayCommand(SaveSelectDir);
        public ICommand Backup_SelectDir => new RelayCommand(BackupSelectDir);
        public ICommand ModPack_SelectDir => new RelayCommand(ModPackSelectDir);
        public ICommand BatchExport_SelectDir => new RelayCommand(BatchExportSelectDir);
        public ICommand Customize_Reset => new RelayCommand(ResetToDefault);
        public ICommand CloseCustomize => new RelayCommand(CustomizeClose);

        private void BatchExportSelectDir(object obj)
        {
            // var currentBatchExportDir = Settings.Default.BatchExportDirectory; // Original
            var currentBatchExportDir = UserDefBatchExportDirectory; // Workaround
            if (string.IsNullOrEmpty(currentBatchExportDir) || !Directory.Exists(currentBatchExportDir))
            {
                currentBatchExportDir = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            }

            var folderSelect = new FolderSelectDialog
            {
                Title = "Select Default Batch Export Directory",
                InitialDirectory = currentBatchExportDir
            };

            if (folderSelect.ShowDialog())
            {
                BatchExport_Directory = folderSelect.FileName; // This setter will save and notify
                FlexibleMessageBox.Show($"Default Batch Export directory updated to: {folderSelect.FileName}", "Directory Updated", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private void CustomizeClose(object obj)
        {
            if (!Settings.Default.Default_Author.Equals(DefaultAuthor))
            {
                Settings.Default.Default_Author = DefaultAuthor;
                Settings.Default.Save();
            }
        }

        /// <summary>
        /// The select ffxiv directory command
        /// </summary>
        private void FFXIVSelectDir(object obj)
        {
            var ofd = new BetterFolderBrowser()
            {
                Title = "Select FFXIV Folder",
            };

            var previous = Settings.Default.FFXIV_Directory;
            if (!string.IsNullOrWhiteSpace(Settings.Default.FFXIV_Directory))
            {
                ofd.RootFolder = Settings.Default.FFXIV_Directory;
            }
            else if (!string.IsNullOrWhiteSpace(OnboardingWindow.GetDefaultInstallDirectory()))
            {
                ofd.RootFolder = OnboardingWindow.GetDefaultInstallDirectory();
            }


            if (ofd.ShowDialog() != System.Windows.Forms.DialogResult.OK)
            {
                return;
            }

            var path = OnboardingWindow.ResolveFFXIVFolder(ofd.SelectedFolder);

            while (!OnboardingWindow.IsGameDirectoryValid(path))
            {
                FlexibleMessageBox.Show("Invalid FFXIV Install", "Please select a valid FFXIV install folder.", MessageBoxButtons.OK, MessageBoxIcon.Question);
                if (ofd.ShowDialog() != System.Windows.Forms.DialogResult.OK)
                {
                    return;
                }
                path = OnboardingWindow.ResolveFFXIVFolder(ofd.SelectedFolder);
            }

            Settings.Default.FFXIV_Directory = path;
            FFXIV_Directory = path;
            Settings.Default.Save();

            Helpers.FlexibleMessageBox.Show("TexTools will now restart.".L());
            _view.Close();
            MainWindow.GetMainWindow().Restart();
        }

        /// <summary>
        /// The select save directory command
        /// </summary>
        private void SaveSelectDir(object obj)
        {
            var oldSaveLocation = Save_Directory;
            var folderSelect = new FolderSelectDialog
            {
                Title = UIMessages.NewSaveLocationTitle,
                InitialDirectory = oldSaveLocation
            };

            if (folderSelect.ShowDialog())
            {
                if (FlexibleMessageBox.Show(UIMessages.MoveDataMessage,
                        UIMessages.MoveDataTitle, MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                {
                    try
                    {
                        Directory.Move(oldSaveLocation, folderSelect.FileName);
                    }
                    catch
                    {
                        var newLoc = folderSelect.FileName;

                        foreach (var dirPath in Directory.GetDirectories(oldSaveLocation, "*",
                            SearchOption.AllDirectories))
                        {
                            Directory.CreateDirectory(dirPath.Replace(oldSaveLocation, newLoc));
                        }

                        foreach (var newPath in Directory.GetFiles(oldSaveLocation, "*.*",
                            SearchOption.AllDirectories))
                        {
                            File.Copy(newPath, newPath.Replace(oldSaveLocation, newLoc), true);
                        }

                        DeleteDirectory(oldSaveLocation);
                    }
                }

                Settings.Default.Save_Directory = folderSelect.FileName;
                Settings.Default.Save();

                FlexibleMessageBox.Show(string.Format(UIMessages.SavedLocationChangedMessage, folderSelect.FileName), UIMessages.NewDirectoryTitle, MessageBoxButtons.OK, MessageBoxIcon.Information);

                Save_Directory = Settings.Default.Backup_Directory;
            }
        }

        /// <summary>
        /// The select backup directory command
        /// </summary>
        private void BackupSelectDir(object obj)
        {
            var oldIndexBackupLocation = Backups_Directory;
            var folderSelect = new FolderSelectDialog
            {
                Title = UIMessages.NewBackupLocationTitle,
                InitialDirectory = oldIndexBackupLocation
            };

            if (folderSelect.ShowDialog())
            {
                if (FlexibleMessageBox.Show(UIMessages.MoveDataMessage, 
                        UIMessages.MoveDataTitle, MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                {
                    try
                    {
                        Directory.Move(oldIndexBackupLocation, folderSelect.FileName);
                    }
                    catch
                    {
                        var newLoc = folderSelect.FileName;

                        foreach (var dirPath in Directory.GetDirectories(oldIndexBackupLocation, "*", SearchOption.AllDirectories))
                        {
                            Directory.CreateDirectory(dirPath.Replace(oldIndexBackupLocation, newLoc));
                        }

                        foreach (var newPath in Directory.GetFiles(oldIndexBackupLocation, "*.*", SearchOption.AllDirectories))
                        {
                            File.Copy(newPath, newPath.Replace(oldIndexBackupLocation, newLoc), true);
                        }

                        DeleteDirectory(oldIndexBackupLocation);
                    }
                }

                Settings.Default.Backup_Directory = folderSelect.FileName;
                Settings.Default.Save();

                FlexibleMessageBox.Show(string.Format(UIMessages.IndexBackupLocationChangedMessage, folderSelect.FileName), UIMessages.NewDirectoryTitle, MessageBoxButtons.OK, MessageBoxIcon.Information);

                Backups_Directory = Settings.Default.Backup_Directory;
            }
        }

        /// <summary>
        /// The select mod pack directory command
        /// </summary>
        private void ModPackSelectDir(object obj)
        {
            var oldModPackLocation = ModPack_Directory;
            var folderSelect = new FolderSelectDialog
            {
                Title = UIMessages.newModpacksLocationTitle,
                InitialDirectory = oldModPackLocation
            };

            if (folderSelect.ShowDialog())
            {
                if (FlexibleMessageBox.Show(UIMessages.MoveDataMessage, 
                        UIMessages.MoveDataTitle, MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                {
                    try
                    {
                        Directory.Move(oldModPackLocation, folderSelect.FileName);
                    }
                    catch
                    {
                        var newLoc = folderSelect.FileName;

                        foreach (var dirPath in Directory.GetDirectories(oldModPackLocation, "*", SearchOption.AllDirectories))
                        {
                            Directory.CreateDirectory(dirPath.Replace(oldModPackLocation, newLoc));
                        }

                        foreach (var newPath in Directory.GetFiles(oldModPackLocation, "*.*", SearchOption.AllDirectories))
                        {
                            File.Copy(newPath, newPath.Replace(oldModPackLocation, newLoc), true);
                        }

                        DeleteDirectory(oldModPackLocation);
                    }
                }

                Settings.Default.ModPack_Directory = folderSelect.FileName;
                Settings.Default.Save();

                FlexibleMessageBox.Show(string.Format(UIMessages.ModPacksLocationChangedMessage, folderSelect.FileName), UIMessages.NewDirectoryTitle, MessageBoxButtons.OK, MessageBoxIcon.Information);

                ModPack_Directory = Settings.Default.ModPack_Directory;
            }
        }

        /// <summary>
        /// The reset to default command
        /// </summary>
        private void ResetToDefault(object obj)
        {

            var def = new CustomModelColors();

            Selected_SkinColor = FromSharpDX(def.SkinColor);
            Selected_HairColor = FromSharpDX(def.HairColor);
            Selected_HairHighlightColor = FromSharpDX(def.HairHighlightColor != null ? (SharpDX.Color) def.HairHighlightColor : def.HairColor);
            Selected_IrisColor = FromSharpDX(def.EyeColor);
            Selected_LipColor = FromSharpDX(def.LipColor);
            Selected_TattooColor = FromSharpDX(def.TattooColor);
            Selected_FurnitureColor = FromSharpDX(def.FurnitureColor);
            Selected_BgColor = (Color)ColorConverter.ConvertFromString(_bgColorDefault);

            UpdateFrameworkColors();
        }

        private System.Windows.Media.Color FromSharpDX(SharpDX.Color c)
        {
            var r = new Color() { R = c.R, B = c.B, G = c.G, A = c.A };
            return r;

        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Deletes a directory
        /// </summary>
        /// <param name="target_dir">The target directory to delete</param>
        private static void DeleteDirectory(string target_dir)
        {
            var files = Directory.GetFiles(target_dir);
            var dirs = Directory.GetDirectories(target_dir);

            foreach (var file in files)
            {
                File.Delete(file);
            }

            foreach (var dir in dirs)
            {
                DeleteDirectory(dir);
            }

            Directory.Delete(target_dir, false);
        }

        private void SetDefaultRace(string selectedRace)
        {
            Settings.Default.Default_Race_Selection = selectedRace;
            Settings.Default.Save();
        }

        /// <summary>
        /// Saves the skin to the settings
        /// </summary>
        /// <param name="selectedSkin">The selected skin</param>
        private void SetSkin(string selectedSkin)
        {
            Settings.Default.Default_Race = selectedSkin;
            Settings.Default.Save();
        }

        /// <summary>
        /// Saves the skin color to the settings
        /// </summary>
        /// <param name="selectedColor">The selected skin color</param>
        private void SetSkinColor(Color selectedColor)
        {
            Settings.Default.Skin_Color = selectedColor.ToString();
            Settings.Default.Save();
            UpdateFrameworkColors();
        }

        /// <summary>
        /// Saves the hair color to the settings
        /// </summary>
        /// <param name="selectedColor">The selected hair color</param>
        private void SetHairColor(Color selectedColor)
        {
            Settings.Default.Hair_Color = selectedColor.ToString();
            Settings.Default.Save();
            UpdateFrameworkColors();
        }
        /// <summary>
        /// Saves the hair color to the settings
        /// </summary>
        /// <param name="selectedColor">The selected hair color</param>
        private void SetHairHighlightColor(Color selectedColor)
        {
            Settings.Default.Hair_Highlight_Color = selectedColor.ToString();
            Settings.Default.Save();
            UpdateFrameworkColors();
        }

        /// <summary>
        /// Saves the iris color to the settings
        /// </summary>
        /// <param name="selectedColor">The selected iris color</param>
        private void SetIrisColor(Color selectedColor)
        {
            Settings.Default.Eye_Color = selectedColor.ToString();
            Settings.Default.Save();
            UpdateFrameworkColors();
        }

        /// <summary>
        /// Saves the etc. color to the settings
        /// </summary>
        /// <param name="selectedColor">The selected etc. color</param>
        private void SetLipColor(Color selectedColor)
        {
            Settings.Default.Lip_Color = selectedColor.ToString();
            Settings.Default.Save();
            UpdateFrameworkColors();
        }

        /// <summary>
        /// Saves the etc. color to the settings
        /// </summary>
        /// <param name="selectedColor">The selected etc. color</param>
        private void SetTattooColor(Color selectedColor)
        {
            Settings.Default.Tattoo_Color = selectedColor.ToString();
            Settings.Default.Save();
            UpdateFrameworkColors();
        }

        /// <summary>
        /// Saves the 3D viewport background color to the settings
        /// </summary>
        /// <param name="selectedColor">The selected background color</param>
        private void SetBgColor(Color selectedColor)
        {
            Settings.Default.BG_Color = selectedColor.ToString();
            Settings.Default.Save();
            UpdateFrameworkColors();
        }

        /// <summary>
        /// Saves the 3D viewport background color to the settings
        /// </summary>
        /// <param name="selectedColor">The selected background color</param>
        private void SetFurnitureColor(Color selectedColor)
        {
            Settings.Default.Furniture_Color = selectedColor.ToString();
            Settings.Default.Save();
            UpdateFrameworkColors();
        }

        #endregion


        /// <summary>
        /// Populates the user's color settings into the framework's static storage.
        /// </summary>
        public static void UpdateFrameworkColors()
        {

            var colorSet = new CustomModelColors();

            var c = (System.Windows.Media.Color)ColorConverter.ConvertFromString(Settings.Default.Skin_Color);
            colorSet.SkinColor = new SharpDX.Color(c.R, c.G, c.B, c.A);

            c = (System.Windows.Media.Color)ColorConverter.ConvertFromString(Settings.Default.Hair_Color);
            colorSet.HairColor = new SharpDX.Color(c.R, c.G, c.B, c.A);

            c = (System.Windows.Media.Color)ColorConverter.ConvertFromString(Settings.Default.Hair_Highlight_Color);
            colorSet.HairHighlightColor = new SharpDX.Color(c.R, c.G, c.B, c.A);

            c = (System.Windows.Media.Color)ColorConverter.ConvertFromString(Settings.Default.Eye_Color);
            colorSet.EyeColor = new SharpDX.Color(c.R, c.G, c.B, c.A);

            c = (System.Windows.Media.Color)ColorConverter.ConvertFromString(Settings.Default.Lip_Color);
            colorSet.LipColor= new SharpDX.Color(c.R, c.G, c.B, c.A);

            c = (System.Windows.Media.Color)ColorConverter.ConvertFromString(Settings.Default.Tattoo_Color);
            colorSet.TattooColor = new SharpDX.Color(c.R, c.G, c.B, c.A);

            c = (System.Windows.Media.Color)ColorConverter.ConvertFromString(Settings.Default.Furniture_Color);
            colorSet.FurnitureColor = new SharpDX.Color(c.R, c.G, c.B, c.A);


            if(Enum.TryParse<EModelingTool>(Settings.Default.ModelingTool, true, out var tool))
            {
                XivCache.FrameworkSettings.ModelingTool = tool;
                colorSet.InvertNormalGreen = XivCache.FrameworkSettings.ModelingTool.UsesDirectXNormals();
            }
            

            ModelTexture.SetCustomColors(colorSet);
        }

        public static void UpdateCacheSettings()
        {
            XivCache.SetMetaValue(TTModel._SETTINGS_KEY_EXPORT_ALL_BONES, Settings.Default.ExportAllBones);
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void NotifyPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
