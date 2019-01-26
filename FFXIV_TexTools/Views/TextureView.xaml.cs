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

using FFXIV_TexTools.ViewModels;
using System.Windows;
using System.Windows.Controls;

namespace FFXIV_TexTools.Views
{
    /// <summary>
    /// Interaction logic for TextureView.xaml
    /// </summary>
    public partial class TextureView : UserControl
    {
        public TextureView()
        {
            InitializeComponent();

            this.DataContext = new TextureViewModel(this);
        }

        /// <summary>
        /// Event handler for export button
        /// </summary>
        private void ExportTextureButton_Click(object sender, RoutedEventArgs e)
        {
            BottomFlyout.Content = new ExportTextureOptionsView();
            BottomFlyout.IsOpen = true;
        }

        /// <summary>
        /// Event handler for import button
        /// </summary>
        private void ImportTextureButton_Click(object sender, RoutedEventArgs e)
        {
            BottomFlyout.Content = new ImportTextureOptionsView();
            BottomFlyout.IsOpen = true;
        }

        /// <summary>
        /// Event handler for more texture options button
        /// </summary>
        private void MoreTextureOptionsButton_Click(object sender, RoutedEventArgs e)
        {
            BottomFlyout.Content = new MoreTextureOptionsView();
            BottomFlyout.IsOpen = true;
        }
    }
}
