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
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Forms;
using Xceed.Wpf.Toolkit;
using xivModdingFramework.Helpers;
using xivModdingFramework.Mods.DataContainers;
using xivModdingFramework.Mods.FileTypes;
using xivModdingFramework.Mods.Interfaces;
using Image = SixLabors.ImageSharp.Image;

namespace FFXIV_TexTools.Views
{
    /// <summary>
    /// Interaction logic for ModPackWizard.xaml
    /// </summary>
    public partial class ExportWizardWindow
    {
        private ProgressDialogController _progressController;

        private static ExportWizardWindow _wizard = null;
        public static ExportWizardWindow GetWizard()
        {
            return _wizard;
        }

        private ProgressDialogController _lockProgressController;
        private IProgress<string> _lockProgress;

        public async Task LockUi()
        {
            _lockProgressController = await this.ShowProgressAsync("Loading".L(), "Please Wait...".L());

            _lockProgressController.SetIndeterminate();

            _lockProgress = new Progress<string>((update) =>
            {
                _lockProgressController.SetMessage(update);
            });
        }
        public async Task UnlockUi()
        {
            await _lockProgressController.CloseAsync();
            _lockProgressController = null;
            _lockProgress = null;
        }

        public ExportWizardWindow()
        {
            _wizard = this;
            InitializeComponent();
            modPackWizard.CanSelectNextPage = false;
            modPackWizard.CanHelp = false;
            ModPackName.Focus();

            ModPackAuthor.Text = String.IsNullOrWhiteSpace(Settings.Default.Default_Author) ? "TexTools User".L() : Settings.Default.Default_Author;
            ModPackUrl.Text = Settings.Default.Default_Modpack_Url;
            ModPackVersion.Text = "1.0.0";
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

        private void CleanupInput()
        {
            var verString = ModPackVersion.Text.Replace("_", "0");

            if (verString.Contains(","))
            {
                verString = verString.Replace(",", ".");
            }

            char[] invalidChars = { '/', '\\', ':', '*', '?', '"', '<', '>', '|' };

            foreach (var ch in invalidChars)
            {
                ModPackName.Text.Replace(ch.ToString(), "");
            }

            VersionNumber = Version.Parse(verString);

            if (VersionNumber.ToString().Equals("0.0.0"))
            {
                VersionNumber = new Version(1, 0, 0);
            }

            if (ModPackAuthor.Text.Equals(string.Empty))
            {
                ModPackAuthor.Text = "Unknown".L();
            }

            if (!String.IsNullOrWhiteSpace(ModPackUrl.Text))
            {
                var url = IOUtil.ValidateUrl(ModPackUrl.Text);
                if (url != null)
                {
                    ModPackUrl.Text = url;
                }
                else
                {
                    ModPackUrl.Text = "";
                }
            }
            else
            {
                ModPackUrl.Text = "";
            }

        }

        /// <summary>
        /// Event handler for next page
        /// </summary>
        private void modPackWizard_Next(object sender, Xceed.Wpf.Toolkit.Core.CancelRoutedEventArgs e)
        {
            CleanupInput();

            var wizPages = modPackWizard.Items;
            var index = wizPages.IndexOf(modPackWizard.CurrentPage);
            if (index == wizPages.Count - 2)
            {
                var newPage = new WizardPage
                {
                    Content = new ExportWizardPageControl(),
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
                if (wizardPage.Content is ExportWizardPageControl control)
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
            var wizPages = modPackWizard.Items;
            var anyContent = false;
            foreach (var wizPageItem in wizPages)
            {
                var wizPage = wizPageItem as WizardPage;
                var content = wizPage.Content as ExportWizardPageControl;
                if(content.HasData)
                {
                    anyContent = true;
                    break;
                }
            }

            if (!anyContent)
            {
                return;
            }

            var modPackPath = Path.Combine(Settings.Default.ModPack_Directory, $"{ModPackName.Text}.ttmp2");
            if (File.Exists(modPackPath))
            {
                var overwriteDialogResult = FlexibleMessageBox.Show(new Wpf32Window(this), UIMessages.ModPackOverwriteMessage,
                                            UIMessages.OverwriteTitle, MessageBoxButtons.YesNoCancel, MessageBoxIcon.Warning);
                if (overwriteDialogResult != System.Windows.Forms.DialogResult.OK)
                {
                    return;
                }
            }

            _progressController = await this.ShowProgressAsync(UIMessages.ModPackCreationMessage, UIMessages.PleaseStandByMessage);
            try
            {
                var modPackData = new ModPackData
                {
                    Name = ModPackName.Text,
                    Author = ModPackAuthor.Text,
                    Version = VersionNumber,
                    Description = ModPackDescription.Text,
                    Url = ModPackUrl.Text,
                    ModPackPages = new List<ModPackData.ModPackPage>()
                };

                var pageIndex = 0;
                foreach (var wizPageItem in wizPages)
                {
                    var wizPage = wizPageItem as WizardPage;

                    if (wizPage.Content is ExportWizardPageControl control)
                    {
                        //throw new NotImplementedException("asdf");
                        
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

                var progressIndicator = new Progress<double>(ReportProgress);


                await TTMP.CreateWizardModPack(modPackData, Properties.Settings.Default.ModPack_Directory, progressIndicator, true);

                ModPackFileName = $"{ModPackName.Text}";

                await _progressController.CloseAsync();
                DialogResult = true;
            }
            catch(Exception ex)
            {
                FlexibleMessageBox.Show("Failed to create modpack.\n\nError: ".L() + ex.Message, "Modpack Creation Error".L(), MessageBoxButtons.OK, MessageBoxIcon.Error);
                await _progressController.CloseAsync();
                return;
            }
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
            var openFileDialog = new OpenFileDialog { Filter = "Modpack Files(*.ttmp2)|*.ttmp2".L(), InitialDirectory = Settings.Default.ModPack_Directory };
            if (openFileDialog.ShowDialog() != System.Windows.Forms.DialogResult.OK)
            {
                return;
            }

            await LockUi();
            try
            {
                var ttmpData = await TTMP.LEGACY_GetModPackJsonData(new DirectoryInfo(openFileDialog.FileName));
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
                    var ttmpd = archive.Entries.First(x => x.FullName.EndsWith(".mpd"));
                    using (var zipStream = ttmpd.Open())
                    {
                        using (var fileStream = new FileStream(tempMPD, FileMode.OpenOrCreate))
                        {
                            await zipStream.CopyToAsync(fileStream);
                        }
                    }
                }

                ModPackAuthor.Text = ttmpData.ModPackJson.Author;
                ModPackName.Text = ttmpData.ModPackJson.Name;
                ModPackVersion.Text= ttmpData.ModPackJson.Version;
                VersionNumber = Version.Parse(ttmpData.ModPackJson.Version);
                ModPackDescription.Text = ttmpData.ModPackJson.Description;
                ModPackUrl.Text = ttmpData.ModPackJson.Url;
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
                    var wizModPackControl = new ExportWizardPageControl();
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
                                    var entry = zipFile.GetEntry(optionJson.ImagePath);
                                    if (entry != null)
                                    {
                                        using (var stream = entry.Open())
                                        {
                                            var tmpImage = Path.GetTempFileName();
                                            using (var imageStream = File.Open(tmpImage, FileMode.OpenOrCreate))
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

                        if (modGroup.OptionList.Count > 0 && modGroup.OptionList.Count(it => it.IsChecked) == 0)
                        {
                            if (modGroup.SelectionType != "Multi")
                            {
                                modGroup.OptionList[0].IsChecked = true;
                            }
                        }
                    }

                    modPackWizard.Items.Add(wizPage);
                    modPackWizard.CanHelp = true;
                }
                modPackWizard.Items.Add(new WizardPage()
                {
                    Content = new ExportWizardPageControl(),
                    PageType = WizardPageType.Blank,
                    Background = null,
                    HeaderBackground = null
                });
            } finally
            {
                await UnlockUi();
            }
        }
    }
}
