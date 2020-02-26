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
using MahApps.Metro.Controls.Dialogs;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Forms;
using Xceed.Wpf.Toolkit;
using xivModdingFramework.Mods.DataContainers;
using xivModdingFramework.Mods.FileTypes;
using Image = SixLabors.ImageSharp.Image;

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

            ModPackAuthor.Text = Settings.Default.Default_Author;
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

            if (ModPackName.Text.Contains('/') || ModPackName.Text.Contains('\\'))
            {
                if (FlexibleMessageBox.Show(new Wpf32Window(this),
                        UIMessages.InvalidCharacterModpackNameMessage,
                        UIMessages.InvalidCharacterModpackNameTitle, MessageBoxButtons.OKCancel, MessageBoxIcon.Warning) ==
                    System.Windows.Forms.DialogResult.OK)
                {
                    ModPackName.Text = ModPackName.Text.Replace('/', '_');
                    ModPackName.Text = ModPackName.Text.Replace('\\', '_');
                }
                else
                {
                    e.Cancel = true;
                    return;
                }
            }

            VersionNumber = Version.Parse(verString);

            if (VersionNumber.ToString().Equals("0.0.0"))
            {
                if (FlexibleMessageBox.Show(new Wpf32Window(this),
                        UIMessages.DefaultModPackVersionMessage,
                        UIMessages.NoVersionFoundTitle, MessageBoxButtons.OKCancel, MessageBoxIcon.Warning) ==
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
                        UIMessages.DefaultModPackAuthorMessage,
                        UIMessages.NoAuthorFoundTitle, MessageBoxButtons.OKCancel, MessageBoxIcon.Warning) ==
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
                        UIMessages.DefaultDescriptionMessage,
                        UIMessages.NoDescriptionFoundTitle, MessageBoxButtons.OKCancel, MessageBoxIcon.Warning) ==
                    System.Windows.Forms.DialogResult.Cancel)
                {
                    e.Cancel = true;
                    return;
                }
            }


            var index = wizPages.IndexOf(modPackWizard.CurrentPage);
            if (index == wizPages.Count - 2)
            {
                var newPage = new WizardPage
                {
                    Content = new WizardModPackControl(),
                    PageType = WizardPageType.Blank,
                    Background = null,
                    HeaderBackground = null
                };
                wizPages.Add(newPage);
            }

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
            _progressController = await this.ShowProgressAsync(UIMessages.ModPackCreationMessage, UIMessages.PleaseStandByMessage);

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

                var modPackPath = Path.Combine(Properties.Settings.Default.ModPack_Directory, $"{modPackData.Name}.ttmp2");
                var overwriteModpack = false;

                if (File.Exists(modPackPath))
                {
                    var overwriteDialogResult = FlexibleMessageBox.Show(new Wpf32Window(this), UIMessages.ModPackOverwriteMessage,
                                                UIMessages.OverwriteTitle, MessageBoxButtons.YesNoCancel, MessageBoxIcon.Warning);
                    if (overwriteDialogResult == System.Windows.Forms.DialogResult.Yes)
                    {
                        overwriteModpack = true;
                    }
                    else if (overwriteDialogResult == System.Windows.Forms.DialogResult.Cancel)
                    {
                        await _progressController.CloseAsync();
                        return;
                    }
                }

                await texToolsModPack.CreateWizardModPack(modPackData, progressIndicator, overwriteModpack);

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

        private async void LoadFromButton_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            var openFileDialog = new OpenFileDialog { Filter = "Texture Files(*.ttmp2)|*.ttmp2", InitialDirectory = Settings.Default.ModPack_Directory };
            if (openFileDialog.ShowDialog() != System.Windows.Forms.DialogResult.OK)
            {
                return;
            }
            var ttmp = new TTMP(new DirectoryInfo(Settings.Default.ModPack_Directory), XivStrings.TexTools);
            var ttmpData = await ttmp.GetModPackJsonData(new DirectoryInfo(openFileDialog.FileName));
            if (!ttmpData.ModPackJson.TTMPVersion.Contains("w"))
            {
                FlexibleMessageBox.Show(new Wpf32Window(this),
                        UIMessages.NotWizardModPack,
                        UIMessages.ModPackLoadingTitle, MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            var tempMPD = Path.GetTempFileName();
            using (var archive = ZipFile.OpenRead(openFileDialog.FileName))
            {
                using (var zipStream = archive.GetEntry("TTMPD.mpd").Open())
                {
                    using (var fileStream = new FileStream(tempMPD, FileMode.OpenOrCreate))
                    {
                        await zipStream.CopyToAsync(fileStream);
                    }
                }
            }
            this.ModPackAuthor.Text = ttmpData.ModPackJson.Author;
            this.ModPackName.Text = ttmpData.ModPackJson.Name;
            this.ModPackVersion.Text= ttmpData.ModPackJson.Version;
            this.ModPackDescription.Text = ttmpData.ModPackJson.Description;
            for (var i = modPackWizard.Items.Count - 1; i > 0; i--)
            {
                modPackWizard.Items.RemoveAt(i);
            }
            //var previousPage = modPackWizard.CurrentPage;
            foreach (var wizPageItemJson in ttmpData.ModPackJson.ModPackPages)
            {
                var wizPage = new WizardPage();
                wizPage.Background = null;
                wizPage.HeaderBackground = null;
                var wizModPackControl = new WizardModPackControl();
                wizPage.Content = wizModPackControl;
                wizPage.PageType = WizardPageType.Blank;
                foreach (var groupJson in wizPageItemJson.ModGroups)
                {
                    var modGroup = new ModGroup();
                    modGroup.OptionList = new List<ModOption>();
                    modGroup.GroupName = groupJson.GroupName;
                    modGroup.SelectionType = groupJson.SelectionType;
                    wizModPackControl.ModGroupList.Add(modGroup);
                    wizModPackControl.ModGroupNames.Add(modGroup.GroupName);
                    foreach (var optionJson in groupJson.OptionList) {
                        var modOption = new ModOption();
                        modGroup.OptionList.Add(modOption);
                        modOption.Name = optionJson.Name;
                        modOption.GroupName = optionJson.GroupName;
                        modOption.IsChecked = optionJson.IsChecked;
                        modOption.SelectionType = optionJson.SelectionType;
                        modOption.Description = optionJson.Description;
                        if (optionJson.ImagePath.Length > 0)
                        {
                            
                            using (var zipFile = ZipFile.OpenRead(openFileDialog.FileName))
                            {
                                using (var stream = zipFile.GetEntry(optionJson.ImagePath).Open())
                                {
                                    var tmpImage = Path.GetTempFileName();                                    
                                    using (var imageStream = File.Open(tmpImage,FileMode.OpenOrCreate))
                                    {
                                        await stream.CopyToAsync(imageStream);
                                        imageStream.Position = 0;
                                    }
                                    var fileNameBak = openFileDialog.FileName;
                                    openFileDialog.FileName = tmpImage;
                                    modOption.Image = Image.Load(openFileDialog.FileName);
                                    modOption.ImageFileName = openFileDialog.FileName;
                                    openFileDialog.FileName = fileNameBak;
                                }
                            }
                        }
                        foreach (var modJson in optionJson.ModsJsons)
                        {
                            var modData = new ModData();
                            modData.Category = modJson.Category;
                            modData.FullPath = modJson.FullPath;
                            modData.Name = modJson.Name;
                            using (var br = new BinaryReader(File.OpenRead(tempMPD)))
                            {
                                br.BaseStream.Seek(modJson.ModOffset, SeekOrigin.Begin);
                                modData.ModDataBytes = br.ReadBytes(modJson.ModSize);
                            }
                            modOption.Mods.Add(modData.FullPath, modData);
                        }
                        ((List<ModOption>)wizModPackControl.OptionsList.ItemsSource).Add(modOption);
                        var view = (CollectionView)CollectionViewSource.GetDefaultView(wizModPackControl.OptionsList.ItemsSource);
                        var groupDescription = new PropertyGroupDescription("GroupName");
                        view.GroupDescriptions.Clear();
                        view.GroupDescriptions.Add(groupDescription);
                    }
                    if (modGroup.OptionList.Count > 0&&modGroup.OptionList.Count(it=>it.IsChecked)==0) modGroup.OptionList[0].IsChecked = true;
                }

                modPackWizard.Items.Add(wizPage);
                modPackWizard.CanHelp = true;
            }
            modPackWizard.Items.Add(new WizardPage()
            {
                Content = new WizardModPackControl(),
                PageType = WizardPageType.Blank,
                Background = null,
                HeaderBackground = null
            });
        }
    }
}
