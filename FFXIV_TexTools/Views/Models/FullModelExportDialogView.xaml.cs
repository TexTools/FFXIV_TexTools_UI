// FFXIV TexTools
// Copyright © 2020 Rafael Gonzalez - All Rights Reserved
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

using FFXIV_TexTools.Properties;
using System.IO;
using System.Windows.Controls;
using System.Windows.Media;

namespace FFXIV_TexTools.Views.Models
{
    /// <summary>
    /// Interaction logic for FullModelExportDialogView.xaml
    /// </summary>
    public partial class FullModelExportDialogView
    {
        private readonly string _savePath;

        public string ModelName => ModelNameTextBox.Text;

        public FullModelExportDialogView(string skeleton)
        {
            InitializeComponent();

            _savePath = new DirectoryInfo(Settings.Default.Save_Directory).FullName;

            if (_savePath.Contains("TexTools"))
            {
                var partialPath = _savePath.Substring(_savePath.IndexOf("TexTools"));
                ExportLocationLabel.Content = $"...\\{partialPath}\\FullModel\\[Name]\\[Name].fbx";
            }
            else
            {
                ExportLocationLabel.Content = $"{_savePath}\\FullModel\\[Name]\\[Name].fbx";
            }


            ModelSkeletonLabel.Content = skeleton;

        }

        /// <summary>
        /// Updates the output file path when name is entered in text box
        /// </summary>
        private void ModelNameTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {

            if (_savePath.Contains("TexTools"))
            {
                var partialPath = _savePath.Substring(_savePath.IndexOf("TexTools"));

                ExportLocationLabel.Content = $"...\\{partialPath}\\FullModel\\{ModelNameTextBox.Text}\\{ModelNameTextBox.Text}.fbx";
            }
            else
            {
                ExportLocationLabel.Content = $"{_savePath}\\FullModel\\{ModelNameTextBox.Text}\\{ModelNameTextBox.Text}.fbx"; ;
            }

            ModelNameTextBox.BorderBrush = (SolidColorBrush)new BrushConverter().ConvertFrom("#FFCCCCCC");
        }

        /// <summary>
        /// Event handler for cancel button
        /// </summary>
        private void Cancel_Button_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            Close();
        }

        /// <summary>
        /// Event handler for import button
        /// </summary>
        private void Import_Button_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            if (ModelNameTextBox.Text.Equals(string.Empty))
            {
                ModelNameTextBox.BorderBrush = new SolidColorBrush(Colors.Red);
            }
            else
            {
                DialogResult = true;
                Close();
            }
        }
    }
}
