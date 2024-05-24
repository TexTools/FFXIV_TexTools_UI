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

using FFXIV_TexTools.Properties;
using FFXIV_TexTools.ViewModels;
using MahApps.Metro.IconPacks;
using System;
using System.Windows;
using System.Windows.Controls;
using xivModdingFramework.Textures.Enums;

namespace FFXIV_TexTools.Views
{
    /// <summary>
    /// Interaction logic for TextureView.xaml
    /// </summary>
    public partial class TextureView : UserControl
    {
        private TextureViewModel textureViewModel;

        public TextureView()
        {
            InitializeComponent();

            this.textureViewModel = new TextureViewModel(this);
            this.DataContext = this.textureViewModel;

            CustomizeViewModel customizeViewModel = new CustomizeViewModel(null);
            this.ExportContextMenu.DataContext = customizeViewModel;
            this.ExportFormatLabel.DataContext = customizeViewModel;
        }

        /// <summary>
        /// Event handler for export button
        /// </summary>
        private async void ExportTextureButton_Click(object sender, RoutedEventArgs e)
        {
            await this.Feedback.Show(PackIconFontAwesomeKind.FileExportSolid);


            
            if (textureViewModel.SelectedMap.Usage == XivTexType.ColorSet)
            {
                await this.textureViewModel.Export(TextureViewModel.TextureFormats.DDS);
            } else
            {
                if (Settings.Default.ExportTexDDS)
                    await this.textureViewModel.Export(TextureViewModel.TextureFormats.DDS);

                if (Settings.Default.ExportTexPNG)
                    await this.textureViewModel.Export(TextureViewModel.TextureFormats.PNG);

                if (Settings.Default.ExportTexBMP)
                    await this.textureViewModel.Export(TextureViewModel.TextureFormats.BMP);
            }

            this.Feedback.Hide();
        }

        /// <summary>
        /// Event handler for import button
        /// </summary>
        private async void ImportTextureButton_Click(object sender, RoutedEventArgs e)
        {
            await this.Feedback.Show(PackIconFontAwesomeKind.FileImportSolid);

            this.ImportContextMenu.Items.Clear();

            TextureViewModel.TextureFormats defaultFormat = TextureViewModel.TextureFormats.DDS;

            foreach (TextureViewModel.TextureFormats format in Enum.GetValues(typeof(TextureViewModel.TextureFormats)))
            {
                bool exists = this.textureViewModel.GetDefaultFileExists(format);

                if (exists)
                {
                    defaultFormat = format;

                    MenuItem item = new MenuItem();
                    item.Header = format.ToString();
                    item.Click += async (s, a) =>
                    {
                        await this.textureViewModel.Import(format);
                    };

                    this.ImportContextMenu.Items.Add(item);
                }
            }

            if (this.ImportContextMenu.Items.Count == 0)
            {
                await this.textureViewModel.ImportFrom();
            }
            else if (this.ImportContextMenu.Items.Count == 1)
            {
                // if there is only one item, just do it directly.
                await this.textureViewModel.Import(defaultFormat);
            }
            else if (this.ImportContextMenu.Items.Count > 1)
            {
                // if there is more than one item, show the menu.
                this.ImportContextMenu.PlacementTarget = this.ImportTextureButton;
                this.ImportContextMenu.IsOpen = true;
            }

            this.Feedback.Hide();
        }

        private async void ImportTextureFromButton_Click(object sender, RoutedEventArgs e)
        {
            await this.Feedback.Show(PackIconFontAwesomeKind.FileImportSolid);
            await this.textureViewModel.ImportFrom();
            this.Feedback.Hide();
        }

        /// <summary>
        /// Event handler for more texture options button
        /// </summary>
        private void MoreTextureOptionsButton_Click(object sender, RoutedEventArgs e)
        {
            BottomFlyout.Content = new MoreTextureOptionsView(this);
            BottomFlyout.IsOpen = true;
        }

        private void ExportFormatDropdown_Click(object sender, RoutedEventArgs e)
        {
            this.ExportContextMenu.PlacementTarget = this.ExportTextureButton;
            this.ExportContextMenu.IsOpen = true;
        }

        private void ExportModpack_Click(object sender, RoutedEventArgs e)
        {
            SingleFileModpackCreator.ExportFile(textureViewModel.PathString, MainWindow.GetMainWindow(), MainWindow.DefaultTransaction);
        }

        private void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            textureViewModel.Refresh();
        }

    }
}
