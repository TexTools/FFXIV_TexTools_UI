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

using FFXIV_TexTools.Resources;
using ImageMagick;
using MahApps.Metro.Controls.Dialogs;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Windows;
using Xceed.Wpf.Toolkit;
using xivModdingFramework.Mods.DataContainers;
using xivModdingFramework.Mods.FileTypes;

namespace FFXIV_TexTools.Views
{
    /// <summary>
    /// Interaction logic for ImportModPackWizard.xaml
    /// </summary>
    public partial class ImportModPackWizard
    {
        private int _currentPage;
        private readonly int _pageCount;
        private readonly DirectoryInfo _modPackDirectory;
        private ProgressDialogController _progressController;
        private readonly Dictionary<string, MagickImage> _imageDictionary;
        private ModPack _modPackEntry;

        public ImportModPackWizard(ModPackJson modPackJson, Dictionary<string, MagickImage> imageDictionary, DirectoryInfo modPackDirectory)
        {
            InitializeComponent();

            _imageDictionary = imageDictionary;
            _modPackDirectory = modPackDirectory;

            ModPackNameLabel.Content = modPackJson.Name;
            ModPackAuthorLabel.Content = modPackJson.Author;
            ModPackVersionLabel.Content = modPackJson.Version;
            ModPackDescription.Text = modPackJson.Description;

            _modPackEntry = new ModPack{name = modPackJson.Name, author = modPackJson.Author, version = modPackJson.Version};

            _pageCount = modPackJson.ModPackPages.Count;

            var wizPages = importModPackWizard.Items;

            for (var i = 0; i < _pageCount; i++)
            {
                wizPages.Add(new WizardPage
                {
                    Content = new ImportWizardModPackControl(modPackJson.ModPackPages[i], imageDictionary),
                    PageType = WizardPageType.Blank
                });
            }
        }

        #region Public Properties

        /// <summary>
        /// The total mods imported
        /// </summary>
        public int TotalModsImported { get; private set; }

        #endregion


        #region Event Handlers

        /// <summary>
        /// The event handler for the next button
        /// </summary>
        private void ImportModPackWizard_Next(object sender, Xceed.Wpf.Toolkit.Core.CancelRoutedEventArgs e)
        {
            _currentPage++;

            if (_currentPage == _pageCount)
            {
                importModPackWizard.FinishButtonVisibility = Visibility.Visible;
                importModPackWizard.CanFinish = true;
            }
        }

        /// <summary>
        /// The event handler for the window closing
        /// </summary>
        private void MetroWindow_Closing(object sender, CancelEventArgs e)
        {
            foreach (var magickImage in _imageDictionary.Values)
            {
                magickImage.Dispose();
            }

            Owner.Activate();
        }

        /// <summary>
        /// The event handler for the finish button
        /// </summary>
        private async void ImportModPackWizard_Finish(object sender, Xceed.Wpf.Toolkit.Core.CancelRoutedEventArgs e)
        {
            _progressController = await this.ShowProgressAsync("Importing ModPack", "Please Stand By...");

            var texToolsModPack = new TTMP(new DirectoryInfo(Properties.Settings.Default.ModPack_Directory), XivStrings.TexTools);

            var importList = new List<ModsJson>();

            foreach (var wizPageItem in importModPackWizard.Items)
            {
                var wizPage = wizPageItem as WizardPage;

                if (wizPage.Content is ImportWizardModPackControl control)
                {
                    var optionList = control.OptionsList;

                    foreach (var optionListItem in optionList.Items)
                    {
                        if (((ModOptionJson)optionListItem).IsChecked)
                        {
                            importList.AddRange(((ModOptionJson)optionListItem).ModsJsons);
                        }
                    }
                }
            }

            foreach (var modsJson in importList)
            {
                modsJson.ModPackEntry = _modPackEntry;
            }

            var gameDirectory = new DirectoryInfo(Properties.Settings.Default.FFXIV_Directory);
            var modListDirectory = new DirectoryInfo(Path.Combine(gameDirectory.Parent.Parent.FullName, XivStrings.ModlistFilePath));

            var progressIndicator = new Progress<double>(ReportProgress);

            TotalModsImported = await texToolsModPack.ImportModPackAsync(_modPackDirectory, importList, gameDirectory, modListDirectory, progressIndicator);

            await _progressController.CloseAsync();

            DialogResult = true;
        }

        /// <summary>
        /// Event handler for previous page
        /// </summary>
        private void ImportModPackWizard_Previous(object sender, Xceed.Wpf.Toolkit.Core.CancelRoutedEventArgs e)
        {
            _currentPage--;

            importModPackWizard.FinishButtonVisibility = Visibility.Collapsed;
            importModPackWizard.CanFinish = false;
        }

        #endregion


        #region Private Methods

        /// <summary>
        /// Updates the progress bar
        /// </summary>
        /// <param name="value">The progress value</param>
        private void ReportProgress(double value)
        {
            _progressController.SetProgress(value);
        }

        #endregion
    }
}
