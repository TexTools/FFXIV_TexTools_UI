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
