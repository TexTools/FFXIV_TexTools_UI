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

using FFXIV_TexTools.Helpers;
using FFXIV_TexTools.Properties;
using FFXIV_TexTools.Resources;
using FolderSelect;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media;

namespace FFXIV_TexTools.ViewModels
{
    public class CustomizeViewModel : INotifyPropertyChanged
    {
        private readonly string _skinDefault = "#FFFFFFFF";
        private readonly string _brownDefault = "#FF603913";
        private readonly string _bgColorDefault = "#FF777777";

        public CustomizeViewModel()
        {
            SkinTypes = new List<string>
            {
                {XivStringRaces.Hyur_M},
                {XivStringRaces.Hyur_H},
                {XivStringRaces.Aura_R},
                {XivStringRaces.Aura_X}
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
        /// The selected iris color
        /// </summary>
        public Color Selected_IrisColor
        {
            get => (Color)ColorConverter.ConvertFromString(Settings.Default.Iris_Color);
            set
            {
                SetIrisColor(value);
                NotifyPropertyChanged(nameof(Selected_IrisColor));
            }
        }

        /// <summary>
        /// The selected etc. color
        /// </summary>
        public Color Selected_EtcColor
        {
            get => (Color)ColorConverter.ConvertFromString(Settings.Default.Etc_Color);
            set
            {
                SetEtcColor(value);
                NotifyPropertyChanged(nameof(Selected_EtcColor));
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
                }

            }
        }

        #endregion

        #region Commands

        // Button commands
        public ICommand FFXIV_SelectDir => new RelayCommand(FFXIVSelectDir);
        public ICommand Save_SelectDir => new RelayCommand(SaveSelectDir);
        public ICommand Backup_SelectDir => new RelayCommand(BackupSelectDir);
        public ICommand ModPack_SelectDir => new RelayCommand(ModPackSelectDir);
        public ICommand Customize_Reset => new RelayCommand(ResetToDefault);

        /// <summary>
        /// The select ffxiv directory command
        /// </summary>
        private void FFXIVSelectDir(object obj)
        {
            var folderSelect = new FolderSelectDialog
            {
                Title = "Select ffxiv folder"
            };

            if (folderSelect.ShowDialog())
            {
                Settings.Default.FFXIV_Directory = folderSelect.FileName;
                Settings.Default.Save();
            }

            //TODO: Logic to restart application once a new game directory is chosen

            FFXIV_Directory = Settings.Default.FFXIV_Directory;
        }

        /// <summary>
        /// The select save directory command
        /// </summary>
        private void SaveSelectDir(object obj)
        {
            var oldSaveLocation = Save_Directory;
            var folderSelect = new FolderSelectDialog
            {
                Title = "Select new location for Saved folder",
                InitialDirectory = oldSaveLocation
            };

            if (folderSelect.ShowDialog())
            {
                if (FlexibleMessageBox.Show("Would you like to move the existing data to the new location?",
                        "Move Data?", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                {
                    try
                    {
                        Directory.Move(oldSaveLocation, folderSelect.FileName);
                    }
                    catch
                    {
                        var newLoc = folderSelect.FileName; ;

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

                FlexibleMessageBox.Show("Location of Saved folder changed.\n\n" +
                                        "New Location: " + folderSelect.FileName, "New Directory", MessageBoxButtons.OK, MessageBoxIcon.Information);

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
                Title = "Select new location of Index_Backups folder",
                InitialDirectory = oldIndexBackupLocation
            };

            if (folderSelect.ShowDialog())
            {
                if (FlexibleMessageBox.Show("Would you like to move the existing data to the new location?", 
                        "Move Data?", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
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

                FlexibleMessageBox.Show("Location of Index Backup folder changed.\n\n" +
                                        "New Location: " + folderSelect.FileName, "New Directory", MessageBoxButtons.OK, MessageBoxIcon.Information);

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
                Title = "Select new location of ModPacks folder",
                InitialDirectory = oldModPackLocation
            };

            if (folderSelect.ShowDialog())
            {
                if (FlexibleMessageBox.Show("Would you like to move the existing data to the new location?", 
                        "Move Data?", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
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

                FlexibleMessageBox.Show("Location of ModPacks folder changed.\n\n" +
                                        "New Location: " + folderSelect.FileName, "New Directory", MessageBoxButtons.OK, MessageBoxIcon.Information);

                ModPack_Directory = Settings.Default.ModPack_Directory;
            }
        }

        /// <summary>
        /// The reset to default command
        /// </summary>
        private void ResetToDefault(object obj)
        {
            Selected_SkinColor = (Color)ColorConverter.ConvertFromString(_skinDefault);
            Selected_HairColor = (Color)ColorConverter.ConvertFromString(_brownDefault);
            Selected_IrisColor = (Color)ColorConverter.ConvertFromString(_brownDefault);
            Selected_EtcColor = (Color)ColorConverter.ConvertFromString(_brownDefault);
            Selected_BgColor = (Color)ColorConverter.ConvertFromString(_bgColorDefault);
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
        }

        /// <summary>
        /// Saves the hair color to the settings
        /// </summary>
        /// <param name="selectedColor">The selected hair color</param>
        private void SetHairColor(Color selectedColor)
        {
            Settings.Default.Hair_Color = selectedColor.ToString();
            Settings.Default.Save();
        }

        /// <summary>
        /// Saves the iris color to the settings
        /// </summary>
        /// <param name="selectedColor">The selected iris color</param>
        private void SetIrisColor(Color selectedColor)
        {
            Settings.Default.Iris_Color = selectedColor.ToString();
            Settings.Default.Save();
        }

        /// <summary>
        /// Saves the etc. color to the settings
        /// </summary>
        /// <param name="selectedColor">The selected etc. color</param>
        private void SetEtcColor(Color selectedColor)
        {
            Settings.Default.Etc_Color = selectedColor.ToString();
            Settings.Default.Save();
        }

        /// <summary>
        /// Saves the 3D viewport background color to the settings
        /// </summary>
        /// <param name="selectedColor">The selected background color</param>
        private void SetBgColor(Color selectedColor)
        {
            Settings.Default.BG_Color = selectedColor.ToString();
            Settings.Default.Save();
        }

        #endregion


        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void NotifyPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
