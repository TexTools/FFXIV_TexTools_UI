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
