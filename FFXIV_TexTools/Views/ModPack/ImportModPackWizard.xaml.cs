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

using FFXIV_TexTools.Helpers;
using FFXIV_TexTools.Resources;
using FFXIV_TexTools.ViewModels;
using MahApps.Metro.Controls.Dialogs;
using SixLabors.ImageSharp;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Forms;
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
        private readonly Dictionary<string, Image> _imageDictionary;
        private readonly ModPack _modPackEntry;
        private readonly bool _messageInImport;
        private TextureViewModel _textureViewModel;
        private ModelViewModel _modelViewModel;

        public ImportModPackWizard(ModPackJson modPackJson, Dictionary<string, Image> imageDictionary, DirectoryInfo modPackDirectory, TextureViewModel textureViewModel, ModelViewModel modelViewModel, bool messageInImport = false)
        {
            InitializeComponent();

            _imageDictionary = imageDictionary;
            _modPackDirectory = modPackDirectory;
            _messageInImport = messageInImport;
            _textureViewModel = textureViewModel;
            _modelViewModel = modelViewModel;

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
                    PageType = WizardPageType.Blank,
                    Background = null,
                    HeaderBackground = null
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
            if (_imageDictionary != null && _imageDictionary.Count > 0)
            {
                foreach (var magickImage in _imageDictionary.Values)
                {
                    try
                    {
                        if (magickImage != null)
                        {
                            magickImage.Dispose();
                        }
                    }
                    catch (Exception ex)
                    {
                        
                    }
                }
            }

            try
            {
                if (_messageInImport)
                {
                    Owner.Activate();
                }
            }
            catch (Exception ex)
            {

            }

            // When the window is closed force an update of the Texture/Model tabs by setting the selected parts
            if (_textureViewModel != null && _modelViewModel != null)
            {
                if (_textureViewModel.SelectedPart != null)
                {
                    _textureViewModel.SelectedPart = _textureViewModel.SelectedPart;
                }
                if (_modelViewModel.SelectedPart != null)
                {
                    _modelViewModel.SelectedPart = _modelViewModel.SelectedPart;
                }
            }
        }

        /// <summary>
        /// The event handler for the finish button
        /// </summary>
        private void ImportModPackWizard_Finish(object sender, Xceed.Wpf.Toolkit.Core.CancelRoutedEventArgs e)
        {
            FinalizeImport();
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
        private void ReportProgress((int current, int total, string message) report)
        {
            if (!report.message.Equals(string.Empty))
            {
                _progressController.SetMessage(report.message);
                _progressController.SetIndeterminate();
            }
            else
            {
                _progressController.SetMessage(
                    $"{UIMessages.PleaseStandByMessage} ({report.current} / {report.total})");

                var value = (double)report.current / (double)report.total;
                _progressController.SetProgress(value);
            }
        }

        /// <summary>
        /// Writes all selected mods to game data
        /// </summary>
        private async void FinalizeImport()
        {
            _progressController = await this.ShowProgressAsync(UIMessages.ModPackImportTitle, UIMessages.PleaseStandByMessage);

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

            var progressIndicator = new Progress<(int current, int total, string message)>(ReportProgress);

            try
            {
                var importResults = await texToolsModPack.ImportModPackAsync(_modPackDirectory, importList,
                    gameDirectory, modListDirectory, progressIndicator);

                TotalModsImported = importResults.ImportCount;

                if (!string.IsNullOrEmpty(importResults.Errors))
                {
                    FlexibleMessageBox.Show(
                        $"{UIMessages.ErrorImportingModsMessage}\n\n{importResults.Errors}", UIMessages.ErrorImportingModsTitle,
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            catch (Exception ex)
            {
                FlexibleMessageBox.Show(
                    $"{UIMessages.ErrorImportingModsMessage}\n\n{ex.Message}", UIMessages.ErrorImportingModsTitle,
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

            await _progressController.CloseAsync();

            if (_messageInImport)
            {
                await this.ShowMessageAsync(UIMessages.ImportCompleteTitle,
                    string.Format(UIMessages.SuccessfulImportCountMessage, TotalModsImported));
            }

            DialogResult = true;
        }

        #endregion
    }
}
