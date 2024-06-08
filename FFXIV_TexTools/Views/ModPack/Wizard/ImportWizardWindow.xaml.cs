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
using FolderSelect;
using MahApps.Metro.Controls.Dialogs;
using SixLabors.ImageSharp;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Interop;
using System.Windows.Shapes;
using TeximpNet.DDS;
using Xceed.Wpf.Toolkit;
using xivModdingFramework.Cache;
using xivModdingFramework.Helpers;
using xivModdingFramework.Mods.DataContainers;
using xivModdingFramework.Mods.FileTypes;
using xivModdingFramework.Mods.FileTypes.PMP;
namespace FFXIV_TexTools.Views.Wizard
{
    /// <summary>
    /// Interaction logic for WizardControl.xaml
    /// </summary>
    public partial class ImportWizardWindow : INotifyPropertyChanged
    {
        private int _CurrentPage;
        private readonly int _PageCount;
        private ProgressDialogController _ProgressController;

        private readonly WizardData _Data;
        private readonly string _Path;

        private int CurrentIndex
        {
            get
            {
                var curIndex = WizardControl.Items.IndexOf(WizardControl.CurrentPage);
                return curIndex;
            }
            set
            {
                if (value < 0 || value >= WizardControl.Items.Count)
                {
                    return;
                }
                WizardControl.CurrentPage = WizardControl.Items[value] as WizardPage;
                UpdateButtons();
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CurrentIndex)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(NextVisible)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(FinalizeVisible)));
            }
        }

        public Visibility NextVisible
        {
            get
            {
                return NextEnabled ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        public Visibility FinalizeVisible
        {
            get
            {
                return NextEnabled ? Visibility.Collapsed : Visibility.Visible;
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

        public event PropertyChangedEventHandler PropertyChanged;

        public bool FinalizeEnabled
        {
            get => _FinalizeEnabled;
            set
            {
                _FinalizeEnabled = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(FinalizeEnabled)));
            }
        }
        public ImportWizardWindow(WizardData data, string path)
        {
            DataContext = this;
            InitializeComponent();

            _Data = data;
            _Path = path;


            ModPackNameLabel.Content = data.MetaPage.Name;
            ModPackAuthorLabel.Content = data.MetaPage.Author;
            ModPackVersionLabel.Content = data.MetaPage.Version;
            ModPackDescription.Text = data.MetaPage.Description;
            ModPackUrlLabel.Text = data.MetaPage.Url;
            ModPackUrlLabel.PreviewMouseLeftButtonDown += ModPackUrlLabel_PreviewMouseLeftButtonDown;

            _PageCount = data.DataPages.Count;

            var wizPages = WizardControl.Items;

            for (var i = 0; i < _PageCount; i++)
            {
                wizPages.Add(new WizardPage
                {
                    Content = new WizardPageControl(data.DataPages[i]),
                    PageType = WizardPageType.Blank,
                    Background = null,
                    HeaderBackground = null
                });
            }
            CurrentIndex = 0;
            Closing += ImportWizardWindow_Closing;
        }

        private void ImportWizardWindow_Closing(object sender, CancelEventArgs e)
        {
            if (null != Owner)
            {
                Owner.Activate();
            }
        }

        private void ModPackUrlLabel_PreviewMouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            var url = IOUtil.ValidateUrl(ModPackUrlLabel.Text);
            if (url == null)
            {
                return;
            }

            Process.Start(new ProcessStartInfo(url));
            e.Handled = true;
        }

        /// <summary>
        /// Writes all selected mods to game data
        /// </summary>
        private async Task FinalizeImport()
        {
            (List<string> Imported, List<string> NotImported, float Duration) res = (null, null, 0);
            _ProgressController = await this.ShowProgressAsync(UIMessages.ModPackImportTitle, UIMessages.PleaseStandByMessage);
            try
            {
                var settings = ViewHelpers.GetDefaultImportSettings(_ProgressController);
                if (_Data.ModpackType == TTMP.EModpackType.Pmp)
                {
                    res = await FinalizePmp(_Data, _Path, settings);
                }
                else if (_Data.ModpackType == TTMP.EModpackType.TtmpWizard)
                {
                    res = await FinalizeTtmp(_Data, _Path, settings);
                }

            }
            catch(Exception ex)
            {
                this.ShowError("Modpack Import Error", "An Error occured while importing the mod:\n\n" + ex.Message);
                return;
            }
            finally
            {
                await _ProgressController.CloseAsync();
            }
            if(res.Imported == null)
            {
                // User cancelled import or there were 0 items in the modpack.
                DialogResult = false;
                return;
            }
            float ImportDuration = res.Duration;
            int TotalModsImported = res.Imported.Count;
            int TotalModsErrored = res.NotImported.Count;

            var durationString = ImportDuration.ToString("0.00");
            await this.ShowMessageAsync(UIMessages.ImportCompleteTitle,
                string.Format(UIMessages.SuccessfulImportCountMessage, TotalModsImported, TotalModsErrored, durationString));
            DialogResult = true;
        }


        /// <summary>
        /// Core static function for displaying the import wizard, fully self contained, including the final modpack import.
        /// </summary>
        /// <param name="path"></param>
        /// <param name="owner"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public static async Task ImportModpack(string path, Window owner, bool asDialog = false)
        {
            var modpackType = TTMP.GetModpackType(path);

            // Load the pages.
            WizardData data;
            if (modpackType == TTMP.EModpackType.Pmp)
            {
                data = await SetupPMP(path);
            }
            else if (modpackType == TTMP.EModpackType.TtmpWizard)
            {
                data = await SetupTtmp(path);
            }
            else
            {
                throw new Exception("Cannot import non-wizard capable modpack with the wizard modpack importer.");
            }

            var wind = new ImportWizardWindow(data, path);
            if (ViewHelpers.IsWindowOpen(owner))
            {
                wind.Owner = owner;
            }

            if (owner != null)
            {
                wind.WindowStartupLocation = System.Windows.WindowStartupLocation.CenterOwner;
            }
            else
            {
                wind.WindowStartupLocation = System.Windows.WindowStartupLocation.CenterScreen;
            }
            try
            {
                if (asDialog)
                {
                    var result = wind.ShowDialog();
                    if (result != true)
                    {
                        // User cancelled import process.
                        return;
                    }
                }
                else
                {
                    wind.Show();
                }
            }
            catch
            {
                if (wind != null)
                {
                    wind.Close();
                }
            }
        }

        private static async Task<WizardData> SetupTtmp(string path)
        {
            var mpl = await TTMP.GetModpackList(path);
            var imageFolder = await TTMP.GetModpackImages(path);
            var data = await WizardData.FromWizardTtmp(mpl, imageFolder);
            return data;
        }

        private static async Task<WizardData> SetupPMP(string path)
        {
            //var data = WizardData.FromWizardPack(mpl, imageFolder);
            var pmp = await PMP.LoadPMP(path, true);
            var data = await WizardData.FromPmp(pmp.pmp, pmp.path);
            return data;
        }

        private async Task<(List<string> Imported, List<string> NotImported, float Duration)> FinalizeTtmp(WizardData data, string path, ModPackImportSettings settings)
        {
            var mods = data.FinalizeTttmpSelections();
            // Time for the chaos that is the TTMP import function.
            var mpl = await TTMP.GetModpackList(path);

            var res = await Task.Run(async () =>
            {
                return await TTMP.ImportModPackAsync(path, mods, settings, MainWindow.UserTransaction);
            });
            return res;
        }
        private async Task<(List<string> Imported, List<string> NotImported, float Duration)> FinalizePmp(WizardData data, string path, ModPackImportSettings settings)
        {
            try
            {
                data.FinalizePmpSelections();
                var pmp = data.RawSource as PMPJson;
                var res = await Task.Run(async () =>
                {
                    return await PMP.ImportPMP(pmp, path, settings, MainWindow.UserTransaction);
                });
                return res;
            }
            finally {
                // Clean up the temp directory if we used one.
                IOUtil.DeleteTempDirectory(path);
            }

        }

        private void UpdateButtons()
        {
            PreviousEnabled = CurrentIndex > 0;
            NextEnabled = CurrentIndex < WizardControl.Items.Count - 1;
            FinalizeEnabled = !NextEnabled;
        }

        private void Cancel_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            DialogResult = false;
        }

        private void PrevPage_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            CurrentIndex--;
        }

        private void NextPage_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            CurrentIndex++;
        }

        private void Finalize_Click(object sender, RoutedEventArgs e)
        {
            _ = FinalizeImport();
        }
    }
}
