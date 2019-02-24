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
using FFXIV_TexTools.Resources;
using MahApps.Metro.Controls.Dialogs;
using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Controls;
using System.Windows.Forms;
using Xceed.Wpf.Toolkit;
using xivModdingFramework.Mods.DataContainers;
using xivModdingFramework.Mods.FileTypes;

namespace FFXIV_TexTools.Views
{
    /// <summary>
    /// Interaction logic for ModPackWizard.xaml
    /// </summary>
    public partial class ModPackWizard
    {
        private ProgressDialogController _progressController;

        public ModPackWizard()
        {
            InitializeComponent();
            modPackWizard.CanSelectNextPage = false;
            modPackWizard.CanHelp = false;
            ModPackName.Focus();
        }

        #region Private Properties

        /// <summary>
        /// The version number
        /// </summary>
        private Version VersionNumber { get; set; }

        /// <summary>
        /// The mod pack file name
        /// </summary>
        public string ModPackFileName { get; set; }

        #endregion


        #region Event Handlers

        /// <summary>
        /// Event handler for next page
        /// </summary>
        private void modPackWizard_Next(object sender, Xceed.Wpf.Toolkit.Core.CancelRoutedEventArgs e)
        {
            var wizPages = modPackWizard.Items;

            var verString = ModPackVersion.Text.Replace("_", "0");

            if (verString.Contains(","))
            {
                verString = verString.Replace(",", ".");
            }

            VersionNumber = Version.Parse(verString);

            if (VersionNumber.ToString().Equals("0.0.0"))
            {
                if (FlexibleMessageBox.Show(new Wpf32Window(this),
                        $"Nothing was entered for ModPack Version\n\nVersion will default to \"1.0.0\"",
                        "No Version Found", MessageBoxButtons.OKCancel, MessageBoxIcon.Warning) ==
                    System.Windows.Forms.DialogResult.OK)
                {
                    VersionNumber = new Version(1, 0, 0);
                }
                else
                {
                    e.Cancel = true;
                    return;
                }
            }

            if (ModPackAuthor.Text.Equals(string.Empty))
            {
                if (FlexibleMessageBox.Show(new Wpf32Window(this),
                        $"Nothing was entered for ModPack Author\n\nAuthor will default to \"TexTools User\"",
                        "No Author Found", MessageBoxButtons.OKCancel, MessageBoxIcon.Warning) ==
                    System.Windows.Forms.DialogResult.OK)
                {
                    ModPackAuthor.Text = "TexTools User";
                }
                else
                {
                    e.Cancel = true;
                    return;
                }
            }

            if (ModPackDescription.Text.Equals(string.Empty))
            {
                if (FlexibleMessageBox.Show(new Wpf32Window(this),
                        $"Nothing was entered for ModPack Description\n\nDescription will be left empty.",
                        "No Description Found", MessageBoxButtons.OKCancel, MessageBoxIcon.Warning) ==
                    System.Windows.Forms.DialogResult.Cancel)
                {
                    e.Cancel = true;
                    return;
                }
            }

            wizPages.Add(new WizardPage
            {
                Content = new WizardModPackControl(),
                PageType = WizardPageType.Blank
            });

            modPackWizard.CanHelp = true;
        }


        /// <summary>
        /// The event handler for mod pack name text changed
        /// </summary>
        private void ModPackName_TextChanged(object sender, TextChangedEventArgs e)
        {
            modPackWizard.CanSelectNextPage = ModPackName.Text.Length > 0;
        }

        /// <summary>
        /// The event handler for the window closing
        /// </summary>
        private void MetroWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            Owner.Activate();
            foreach (WizardPage wizardPage in modPackWizard.Items)
            {
                if (wizardPage.Content is WizardModPackControl control)
                {
                    control.Dispose();
                }
            }
        }

        /// <summary>
        /// The event handler for the create mod pack button
        /// </summary>
        /// <remarks>
        /// This is originally the help button, but has been repurposed
        /// </remarks>
        private async void ModPackWizard_CreateModPack(object sender, System.Windows.RoutedEventArgs e)
        {
            _progressController = await this.ShowProgressAsync("Creating ModPack", "Please Stand By...");

            var wizPages = modPackWizard.Items;

            var modPackData = new ModPackData
            {
                Name = ModPackName.Text,
                Author = ModPackAuthor.Text,
                Version = VersionNumber,
                Description = ModPackDescription.Text,
                ModPackPages = new List<ModPackData.ModPackPage>()
            };

            var pageIndex = 0;
            foreach (var wizPageItem in wizPages)
            {
                var wizPage = wizPageItem as WizardPage;

                if (wizPage.Content is WizardModPackControl control)
                {
                    if (control.ModGroupList.Count > 0)
                    {
                        modPackData.ModPackPages.Add(new ModPackData.ModPackPage
                        {
                            PageIndex = pageIndex,
                            ModGroups = control.ModGroupList
                        });
                    }
                }
                pageIndex++;
            }

            if (modPackData.ModPackPages.Count > 0)
            {
                var progressIndicator = new Progress<double>(ReportProgress);
                var texToolsModPack = new TTMP(new DirectoryInfo(Properties.Settings.Default.ModPack_Directory), XivStrings.TexTools);
                await texToolsModPack.CreateWizardModPack(modPackData, progressIndicator);

                ModPackFileName = $"{ModPackName.Text}";
            }
            else
            {
                ModPackFileName = "NoData";
            }

            await _progressController.CloseAsync();

            DialogResult = true;
        }


        /// <summary>
        /// The event handler when clicking on the mod pack version text box
        /// </summary>
        /// <remarks>
        /// This sets the caret to the start of the text box
        /// </remarks>
        private void ModPackVersion_PreviewMouseUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            ModPackVersion.CaretIndex = 0;
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
