﻿// FFXIV TexTools
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
using FFXIV_TexTools.Views.Wizard;
using MahApps.Metro.Controls.Dialogs;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Forms;
using System.Windows.Media;
using Xceed.Wpf.Toolkit;
using xivModdingFramework.Helpers;
using xivModdingFramework.Mods.DataContainers;
using xivModdingFramework.Mods.FileTypes;
using xivModdingFramework.Mods.FileTypes.PMP;
using xivModdingFramework.Mods.Interfaces;
using Image = SixLabors.ImageSharp.Image;
using xivModdingFramework.Mods;

namespace FFXIV_TexTools.Views
{
    /// <summary>
    /// Interaction logic for ModPackWizard.xaml
    /// </summary>
    public partial class ExportWizardWindow : INotifyPropertyChanged
    {
        private ProgressDialogController _lockProgressController;
        private WizardData _Data;

        private string TempFolder;

        public event PropertyChangedEventHandler PropertyChanged;

        private int CurrentIndex
        {
            get
            {
                var curIndex = WizardControl.Items.IndexOf(WizardControl.CurrentPage);
                return curIndex;
            }
            set
            {
                if(value < 0 || value >= WizardControl.Items.Count)
                {
                    return;
                }
                WizardControl.CurrentPage = WizardControl.Items[value] as WizardPage;
                UpdateButtons();
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CurrentIndex)));
            }
        }

        private bool _PreviousEnabled;
        public bool PreviousEnabled
        {
            get => _PreviousEnabled;
            set
            {
                _PreviousEnabled = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(PreviousEnabled)));
            }
        }
        private bool _NextEnabled;
        public bool NextEnabled
        {
            get => _NextEnabled;
            set
            {
                _NextEnabled = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(NextEnabled)));
            }
        }
        private bool _FinalizeEnabled;
        public bool FinalizeEnabled
        {
            get => _FinalizeEnabled;
            set
            {
                _FinalizeEnabled = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(FinalizeEnabled)));
            }
        }

        private ImageSource _HeaderSource;
        public ImageSource HeaderSource
        {
            get => _HeaderSource;
            set
            {
                _HeaderSource = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HeaderSource)));
            }
        }

        private string ImagePath;

        public WizardData Data {
            get => _Data;
            set
            {
                _Data = value;
                OnDataChanged();
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Data)));
            }
        }

        public async Task LockUi(string title = "Loading", string message = "Please Wait...")
        {
            if (_lockProgressController != null) return;

            _lockProgressController = await this.ShowProgressAsync(title.L(), message.L());
            _lockProgressController.SetIndeterminate();

        }
        public async Task UnlockUi()
        {
            if (_lockProgressController == null) return;

            await _lockProgressController.CloseAsync();
            _lockProgressController = null;
        }

        public ExportWizardWindow()
        {
            TempFolder = Path.Combine(IOUtil.GetFrameworkTempFolder(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(TempFolder);

            DataContext = this;
            InitializeComponent();
            Data = new WizardData();
            WizardControl.CanSelectNextPage = true;
            WizardControl.CanHelp = false;
            ModPackName.Focus();

            ModPackAuthor.Text = String.IsNullOrWhiteSpace(Settings.Default.Default_Author) ? "TexTools User".L() : Settings.Default.Default_Author;
            ModPackUrl.Text = Settings.Default.Default_Modpack_Url;
            ModPackVersion.Text = "1.0.0";
            UpdateButtons();
            SetTitle();
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
        /// The event handler for the window closing
        /// </summary>
        private void MetroWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (null != Owner)
            {
                Owner.Activate();
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


        private async void LoadFromButton_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            var openFileDialog = new OpenFileDialog { 
                Filter = "Modpack Files|*.ttmp2;*.pmp;*.json;*.ttmp".L(), 
                InitialDirectory = Path.GetFullPath(Settings.Default.ModPack_Directory)
            };

            if (openFileDialog.ShowDialog() != System.Windows.Forms.DialogResult.OK)
            {
                return;
            }

            await LockUi("Loading Modpack", "This may take a moment for older modpacks...");
            try
            {
                var oldTemp = TempFolder;
                var path = openFileDialog.FileName;
                var modpackType = TTMP.GetModpackType(path);


                if (modpackType == TTMP.EModpackType.Pmp)
                {
                    Data = await SetupPMP(path);
                }
                else if (modpackType == TTMP.EModpackType.TtmpWizard)
                {
                    Data = await SetupTtmp(path);
                }
                else if(modpackType == TTMP.EModpackType.TtmpSimple || modpackType == TTMP.EModpackType.TtmpOriginal)
                {
                    Data = await SetupTtmp(path, false);
                }
                else
                {
                    throw new Exception("Cannot import non-wizard capable modpack with the wizard modpack importer.");
                }

                IOUtil.DeleteTempDirectory(oldTemp);
            }
            catch (Exception ex)
            {
                this.ShowError("Modpack Read Error".L(), "An error occurred while reading the modpack:\n\n" + ex.Message);
            }
            finally
            {
                await UnlockUi();
            }
        }

        private void OnDataChanged()
        {
            try
            {

                ModPackName.Text = Data.MetaPage.Name;
                ModPackAuthor.Text = Data.MetaPage.Author;
                ModPackVersion.Text = Data.MetaPage.Version;
                ModPackDescription.Text = Data.MetaPage.Description;
                ModPackUrl.Text = Data.MetaPage.Url;

                ImagePath = Data.MetaPage.Image;
                if (string.IsNullOrWhiteSpace(ImagePath) || !File.Exists(ImagePath))
                {
                    HeaderSource = ViewHelpers.GetDefaultModImage();
                }
                else
                {
                    HeaderSource = ViewHelpers.SafeBitmapFromFile(ImagePath);
                }

                var pageCount = Data.DataPages.Count;

                var wizPages = WizardControl.Items;
                while (wizPages.Count > 1)
                {
                    wizPages.RemoveAt(1);
                }

                for (var i = 0; i < pageCount; i++)
                {
                    wizPages.Add(new WizardPage
                    {
                        Content = new WizardPageControl(Data.DataPages[i], true),
                        PageType = WizardPageType.Blank,
                        Background = null,
                        HeaderBackground = null
                    });
                }
                UpdateButtons();
            }
            catch(Exception ex)
            {
                this.ShowError("Modpack Read Error".L(), "An error occurred while reading the modpack:\n\n" + ex.Message);
            }
        }


        private async Task<WizardData> SetupTtmp(string path, bool wizard = true)
        {
            return await Task.Run(async () =>
            {

                var ttmp = await TTMP.UnzipTtmp(path);

                WizardData data;
                if (wizard)
                {
                    data = await WizardData.FromWizardTtmp(ttmp.Mpl, ttmp.UnzipFolder);
                } else
                {
                    data = await WizardData.FromSimpleTtmp(ttmp.Mpl, ttmp.UnzipFolder);
                }

                return data;
            });
        }

        private async Task<WizardData> SetupPMP(string path)
        {
            return await Task.Run(async () =>
            {
                //var data = WizardData.FromWizardPack(mpl, imageFolder);
                var pmp = await PMP.LoadPMP(path, false, true);
                TempFolder = pmp.path;
                var data = await WizardData.FromPmp(pmp.pmp, pmp.path);
                return data;
            });
        }



        private void UpdateButtons()
        {
            PreviousEnabled = CurrentIndex > 0;
            //NextEnabled = CurrentIndex < WizardControl.Items.Count - 1;
            NextEnabled = true;
            FinalizeEnabled = WizardControl.Items.Count > 1;
        }

        private void Cancel_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            DialogResult = false;
        }

        private void PrevPage_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            CurrentIndex--;
            SetTitle();
        }

        private void NextPage_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            if (CurrentIndex == WizardControl.Items.Count - 1)
            {
                var newPage = new WizardPageEntry();
                Data.DataPages.Add(newPage);

                WizardControl.Items.Add(new WizardPage
                {
                    Content = new WizardPageControl(newPage, true),
                    PageType = WizardPageType.Blank,
                    Background = null,
                    HeaderBackground = null
                });
            }
            CurrentIndex++;
            SetTitle();
        }

        private async void Finalize_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            Data.MetaPage.Name = ModPackName.Text.Trim();
            Data.MetaPage.Author = ModPackAuthor.Text.Trim();
            Data.MetaPage.Version = ModPackVersion.Text.Trim();
            Data.MetaPage.Description = ModPackDescription.Text.Trim();
            Data.MetaPage.Url = ModPackUrl.Text.Trim();
            Data.MetaPage.Image = ImagePath;

            if (string.IsNullOrEmpty(Data.MetaPage.Name))
            {
                return;
            }


            var sfd = new SaveFileDialog();
            sfd.Filter = ViewHelpers.ModpackFileFilter;
            sfd.FileName = Data.MetaPage.Name + "." + Settings.Default.Default_Modpack_Format;
            sfd.InitialDirectory = Path.GetFullPath(Settings.Default.ModPack_Directory);

            if(sfd.ShowDialog() != System.Windows.Forms.DialogResult.OK)
            {
                return;
            }

            var path = sfd.FileName;

            var ext = Path.GetExtension(path).ToLower();

            var success = false;
            await LockUi("Creating Modpack");
            try
            {
                await Task.Run(async () =>
                {
                    if (ext == ".pmp")
                    {
                        await Data.WritePmp(path);
                    }
                    else if (ext == ".ttmp2")
                    {
                        if (!string.IsNullOrWhiteSpace(ImagePath))
                        {
                            if(Data.DataPages.Count > 0 && Data.DataPages[0].Groups.Count > 0 && Data.DataPages[0].Groups[0].Options.Count > 0) {
                                var op = Data.DataPages[0].Groups[0].Options[0];

                                if (string.IsNullOrWhiteSpace(op.Image))
                                {
                                    op.Image = ImagePath;
                                }
                            }
                        }

                        await Data.WriteWizardPack(path);
                    }
                    else
                    {
                        this.ShowError("Export Error".L(), "Modpacks can only be exported in .ttmp2 or .pmp format".L());
                        return;
                    }
                });
                success = true;
            }
            catch (Exception ex)
            {
                this.ShowError("Export Error".L(), "An error occurred while exporting the modpack:\n\n" + ex.Message);
            }
            finally
            {

                CleanupInvalidData();
                await UnlockUi();
            }

            if (success)
            {
                var res = await this.ShowMessageAsync("Modpack Created", "The modpack was created successfully.");
                //this.Close();
            }


        }

        private void CleanupInvalidData()
        {
            var idx = CurrentIndex;
            CurrentIndex = 0;
            Data = Data;
            CurrentIndex = idx;
        }

        private void RemoveImage_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            HeaderSource = null;
            ImagePath = null;
        }

        private void ChangeImage_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            var imgInfo = this.LoadUserImage();
            if (string.IsNullOrWhiteSpace(imgInfo.File)) return;


            ImagePath = imgInfo.File;
            HeaderSource = imgInfo.Image;
        }
        private void SetTitle()
        {
            if (CurrentIndex == 0)
            {
                Title = "Create Modpack";
            }
            else
            {
                Title = "Create Modpack - Page " + CurrentIndex;
            }
        }

        private async void ShrinkModpack_Click(object sender, System.Windows.RoutedEventArgs e)
        {

            var imgSize = Settings.Default.MaxImageSize > 0 ? Settings.Default.MaxImageSize.ToString() : "No Limit";
            if(!this.ShowConfirmation("Shrink Confirmation", "This will:\n"
                + "\n- Shrink all Textures to your current Max Image Size: " + imgSize
                + "\n- Resave/Repack all MDLs in the modpack."
                + "\n- Generate missing Mipmaps, and remove unnecessary Mipmaps."
                + "\n- Resize invalid-sized textures."
                + "\n- Repack all Textures in the modpack (for TTMPs)."
                + "\n- Remove unused files from the modpack."
                + "\n\n This will NOT update Endwalker files (other than MDLs) for Dawntrail."))
            {
                return;
            }

            var settings = new ShrinkRay.ShrinkRaySettings();
            settings.MaxTextureSize = Settings.Default.MaxImageSize;

            try
            {
                await LockUi();
                var res = await ShrinkRay.ShrinkModpack(Data, settings);
                Data = res;
            }
            catch(Exception ex)
            {
                this.ShowError("Shrink Ray Failure", "Unable to shrink modpack:\n\n" + ex.Message);
            }
            finally
            {
                await UnlockUi();
            }
        }
    }
}
