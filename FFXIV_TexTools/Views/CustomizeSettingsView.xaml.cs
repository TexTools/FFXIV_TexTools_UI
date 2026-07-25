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

using System;
using System.Globalization;
using FFXIV_TexTools.ViewModels;
using System.Windows;
using System.Windows.Controls;
using xivModdingFramework.Helpers;
using Microsoft.Win32;
using FFXIV_TexTools.Helpers;
using xivModdingFramework.Exd.FileTypes;
using System.Threading.Tasks;
using xivModdingFramework.Models.FileTypes;
using xivModdingFramework.Cache;
using xivModdingFramework.General.Enums;
using System.Collections.Generic;
using System.IO;
using MahApps.Metro.Controls.Dialogs;

namespace FFXIV_TexTools.Views
{
    /// <summary>
    /// Interaction logic for CustomizeSettingsView.xaml
    /// </summary>
    public partial class CustomizeSettingsView
    {
        public CustomizeSettingsView()
        {
            InitializeComponent();

            DataContext = new CustomizeViewModel(this);
        }

        /// <summary>
        /// Event handler for close button
        /// </summary>
        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void EnableLongPaths_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                
                RegistryKey myKey = Registry.LocalMachine.OpenSubKey("SYSTEM\\CurrentControlSet\\Control\\FileSystem", true);
                if (myKey != null)
                {
                    myKey.SetValue("LongPathsEnabled", "1", RegistryValueKind.DWord);
                    myKey.Close();
                    FlexibleMessageBox.Show(ViewHelpers.GetWin32Window(this), "Windows Long-Path support has been successfully enabled.", "Registry Change Success");
                } else
                {
                    ViewHelpers.ShowError("Registry Change Error", "An error occurred while attempting to set Long-Paths enabled:\n\nRegistry SubKey did not exist.");
                }
            }
            catch(Exception ex)
            {
                ViewHelpers.ShowError("Registry Change Error", "An error occurred while attempting to set Long-Paths enabled:\n\n" + ex.Message);
            }
        }

        private async void RegenSkeletons_Click(object sender, RoutedEventArgs e)
        {
            if(!this.ShowConfirmation("Regenerate Skeletons Confirmation", "This will purge the skeleton cache, and regenerate it based on the CURRENT system state.\n\nIf you have custom Skeletons installed such as Skelomae, NFLB, or IVCS, those will be reflected in the new skeleton cache.\n\nAre you sure you wish to continue?"))
            {
                return;
            }
            var _lock = await this.ShowProgressAsync("Regenerating Skeletons", "Please wait...");
            try
            {
                await RegenerateSkeletons();
            } catch(Exception ex)
            {
                this.ShowError("Skeleton Error", "Failed to regenerate one or more skeletons:\n\n" + ex.Message);
            }
            finally
            {
                await _lock.CloseAsync();
            }
        }

        public static async Task RegenerateSkeletons()
        {
            var cwd = Path.GetDirectoryName(System.Reflection.Assembly.GetEntryAssembly().Location);
            var skelFolder = Path.Combine(cwd, Sklb.SkeletonsFolder);
            IOUtil.RecursiveDeleteDirectory(skelFolder);

            var tx = MainWindow.DefaultTransaction;
            var root = new XivDependencyRootInfo() {
                PrimaryId = 0,
                PrimaryType = xivModdingFramework.Items.Enums.XivItemType.equipment
            };

            var tasks = new List<Task>();
            foreach(var race in XivRaces.PlayableRaces)
            {
                tasks.Add(Task.Run(async () => { await Sklb.GetBaseSkeletonFile(root, race, tx); }));
            }
            await Task.WhenAll(tasks);
        }
    }

    public class UrlValidator : ValidationRule
    {
        public override ValidationResult Validate(object value, CultureInfo cultureInfo)
        {
            return value != null && !String.IsNullOrWhiteSpace(value.ToString()) && IOUtil.ValidateUrl(value.ToString()) == null
                ? new ValidationResult(false, "Invalid URL.".L())
                : ValidationResult.ValidResult;
        }
    }
}
