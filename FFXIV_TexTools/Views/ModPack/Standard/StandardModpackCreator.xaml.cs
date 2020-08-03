using FFXIV_TexTools.Helpers;
using FFXIV_TexTools.Properties;
using FFXIV_TexTools.Resources;
using FFXIV_TexTools.ViewModels;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using MahApps.Metro;
using MahApps.Metro.Controls.Dialogs;
using xivModdingFramework.Cache;
using xivModdingFramework.General.Enums;
using xivModdingFramework.Helpers;
using xivModdingFramework.Items.Interfaces;
using xivModdingFramework.Mods;
using xivModdingFramework.Mods.DataContainers;
using xivModdingFramework.Mods.FileTypes;
using xivModdingFramework.SqPack.FileTypes;

namespace FFXIV_TexTools.Views
{
    /// <summary>
    /// Interaction logic for CompleteModpackCreator.xaml
    /// </summary>
    public partial class StandardModpackCreator
    {

        private static readonly Thickness _MARGIN = new Thickness(5);

        ProgressDialogController _lockProgressController;
        Progress<string> _lockProgress;
        private Page _page
        {
            get
            {
                return (Page)this.Content;
            }
            set
            {
                this.Content = value;
                _page.Margin = _MARGIN;
            }
        }
        public static string GetNiceLevelName(XivDependencyLevel level)
        {
            switch (level)
            {
                case XivDependencyLevel.Root:
                    return "Metadata";
                case XivDependencyLevel.Model:
                    return "Model";
                case XivDependencyLevel.Material:
                    return "Material";
                case XivDependencyLevel.Texture:
                    return "Texture";
                default:
                    return "Unknown";
            }
        }

        public readonly StandardModpackViewModel ViewModel;

        private IItem _inProgressItem;
        private XivDependencyLevel _inProgressLevel;
        public StandardModpackCreator()
        {
            ViewModel = new StandardModpackViewModel();
            InitializeComponent();

            ShowItemSelect();
        }
        public async Task LockUi(string title, string message, object requestor)
        {
            _lockProgressController = await this.ShowProgressAsync(title, message);

            _lockProgressController.SetIndeterminate();

            _lockProgress = new Progress<string>((update) =>
            {
                _lockProgressController.SetMessage(update);
            });
        }
        public async Task UnlockUi(object requestor)
        {
            await _lockProgressController.CloseAsync();
            _lockProgressController = null;
            _lockProgress = null;
        }

        // Show initial page.
        private void ShowItemSelect()
        {
            var homePage = new StandardModpackCreatorItemSelect(this, ViewModel);
            homePage.ItemSelected += HomePage_ItemSelected;
            homePage.FinalizeRequested += HomePage_FinalizeRequested;
            _page = homePage;
        }

        // They selected an item (or pressed cancel)
        private void HomePage_ItemSelected(object sender, xivModdingFramework.Items.Interfaces.IItem e)
        {
            // Get rid of the item select screen fully.
            if(e == null)
            {
                // Cancel was pressed, exit.
                Close();
                return;
            }

            _inProgressItem = e;
            var levelSelect = new StandardModpackLevelSelect(_inProgressItem);
            levelSelect.LevelSelected += LevelSelect_LevelSelected;
            _page = levelSelect;
        }

        // They selected a level (or pressed back)
        private void LevelSelect_LevelSelected(object sender, xivModdingFramework.Cache.XivDependencyLevel e)
        {
            if (e == XivDependencyLevel.Invalid) {
                // Back button was pressed, return to item screen.
                ShowItemSelect();
                return;
            }
            _inProgressLevel = e;

            var levelReview = new StandardModpackSharedItems(_inProgressItem, _inProgressLevel);
            levelReview.ConfirmedSharedItems += LevelReview_ConfirmedSharedItems;
            _page = levelReview;

        }

        // They accepted the shared items review screen.
        private void LevelReview_ConfirmedSharedItems(object sender, bool e)
        {
            if (e == false)
            {
                // Back button was pressed, return to level select screen.
                HomePage_ItemSelected(this, _inProgressItem);
                return;
            }

            if (_inProgressLevel == XivDependencyLevel.Root)
            {
                SelectAllFiles();
                return;
            }
            else
            {
                var fileSelect = new StandardModpackFileSelect(_inProgressItem, _inProgressLevel);
                fileSelect.FilesSelected += FileSelect_FilesSelected;
                _page = fileSelect;
            }
        }
        private async Task SelectAllFiles()
        {
            // If they hit everything, skip over the file select screen.
            var root = _inProgressItem.GetRoot();
            var models = await root.GetModelFiles();
            var oModels = new ObservableCollection<string>();
            models.ForEach(x => oModels.Add(x));
            var entry = new StandardModpackItemEntry(_inProgressItem, _inProgressLevel, oModels);
            var review = new StandardModpackFilesReview(entry);
            review.ReviewAccepted += Review_ReviewAccepted;
            _page = review;

        }

        // They selected some files (or pressed back)
        private void FileSelect_FilesSelected(object sender, ObservableCollection<string> e)
        {
            if(e == null || e.Count == 0)
            {
                // Back button pressed, return them to the level review screen.
                LevelSelect_LevelSelected(this, _inProgressLevel);
                return;
            }

            var entry = new StandardModpackItemEntry(_inProgressItem, _inProgressLevel, e);
            var review = new StandardModpackFilesReview(entry);
            review.ReviewAccepted += Review_ReviewAccepted;
            _page = review;
        }

        private void Review_ReviewAccepted(object sender, StandardModpackItemEntry e)
        {
            if(e == null)
            {
                if (_inProgressLevel == XivDependencyLevel.Root)
                {

                    // Back button pressed on an 'everything' item, return to the level confirm screen.
                    LevelSelect_LevelSelected(this, _inProgressLevel);
                    return;
                }
                else
                {
                    // Back button pressed, return to the file select screen.
                    LevelReview_ConfirmedSharedItems(this, true);
                    return;
                }
            }
            var root = e.Item.GetRootInfo();

            StandardModpackItemEntry toRemove = null;
            foreach(var entry in ViewModel.Entries)
            {
                var entryRoot = entry.Item.GetRootInfo();
                if (root == entryRoot)
                {
                    var result = FlexibleMessageBox.Show("Adding this item will overwrite your existing entry for item: " + entry.Item.Name + "\nAre you sure you wish to proceed?", "Item Overwrite Confirmation", System.Windows.Forms.MessageBoxButtons.OKCancel, System.Windows.Forms.MessageBoxIcon.Warning, System.Windows.Forms.MessageBoxDefaultButton.Button1);
                    if(result == System.Windows.Forms.DialogResult.Cancel)
                    {
                        return;
                    }
                    toRemove = entry;
                    break;
                }
            }

            ViewModel.Entries.Remove(toRemove);
            ViewModel.Entries.Add(e);

            ShowItemSelect();
        }


        // Transitioning to the final review screen.
        private void HomePage_FinalizeRequested(object sender, EventArgs e)
        {
            /*
            // Decided Final Review was too many rounds of reviewing.
            var review = new StandardModpackFilesReview(ViewModel);
            review.FinalReviewAccepted += Review_FinalReviewAccepted;
            _page = review;*/

            // Go to Create Modpack Screeen.
            var page = new StandardModpackFinalize(ViewModel);
            page.CreateModpack += Page_CreateModpack;
            _page = page;
        }

        // Final review was accepted or they hit back.
        // Not actually used atm, since the final review screen is currently not used.
        private void Review_FinalReviewAccepted(object sender, StandardModpackViewModel e)
        {
            if (e == null)
            {
                // Back button pressed, back to the file select screen.
                ShowItemSelect();
                return;
            }

            // Go to Create Modpack Screeen.
            var page = new StandardModpackFinalize(ViewModel);
            page.CreateModpack += Page_CreateModpack;
            _page = page;
        }

        // They chose to create the modpack, or clicked back.
        private void Page_CreateModpack(object sender, StandardModpackViewModel e)
        {
            if(e == null)
            {
                // They hit back.
                ShowItemSelect();
                return;
            }

            CreateModpack();
        }

        private async Task CreateModpack()
        {

            TTMP texToolsModPack = new TTMP(new DirectoryInfo(Settings.Default.ModPack_Directory), XivStrings.TexTools);
            var index = new Index(XivCache.GameInfo.GameDirectory);
            var dat = new Dat(XivCache.GameInfo.GameDirectory);
            var modding = new Modding(XivCache.GameInfo.GameDirectory);
            var ModList = modding.GetModList();

            SimpleModPackData simpleModPackData = new SimpleModPackData
            {
                Name = ViewModel.Name,
                Author = ViewModel.Author,
                Version = ViewModel.Version,
                Description = ViewModel.Description,
                Url = ViewModel.Url,
                SimpleModDataList = new List<SimpleModData>()
            };

            foreach (var entry in ViewModel.Entries)
            {
                foreach(var file in entry.AllFiles)
                {
                    var offset = await index.GetDataOffset(file);
                    var dataFile = IOUtil.GetDataFileFromPath(file);
                    var compressedSize = await dat.GetCompressedFileSize(offset, dataFile);
                    var modEntry = ModList.Mods.FirstOrDefault(x => x.fullPath == file);
                    var modded = modEntry != null && modEntry.enabled == true;


                    SimpleModData simpleData = new SimpleModData
                    {
                        Name = entry.Item.Name,
                        Category = entry.Item.SecondaryCategory,
                        FullPath = file,
                        ModOffset = offset,
                        ModSize = compressedSize,
                        IsDefault = !modded,
                        DatFile = dataFile.GetDataFileName()
                    };

                    simpleModPackData.SimpleModDataList.Add(simpleData);

                }
            }


            string modPackPath = Path.Combine(Properties.Settings.Default.ModPack_Directory, $"{simpleModPackData.Name}.ttmp2");

            if (File.Exists(modPackPath))
            {
                DialogResult overwriteDialogResult = FlexibleMessageBox.Show(new Wpf32Window(this), UIMessages.ModPackOverwriteMessage,
                                            UIMessages.OverwriteTitle, MessageBoxButtons.YesNoCancel, MessageBoxIcon.Warning);
                if (overwriteDialogResult == System.Windows.Forms.DialogResult.Cancel)
                {
                    return;
                }
            }

            try
            {
                await LockUi(UIStrings.Creating_Modpack, null, null);
                Progress<(int current, int total, string message)> progressIndicator = new Progress<(int current, int total, string message)>(ReportProgress);
                await texToolsModPack.CreateSimpleModPack(simpleModPackData, XivCache.GameInfo.GameDirectory, progressIndicator, true);
                await UnlockUi(this);
                DialogResult = true;
            }
            catch(Exception ex)
            {
                FlexibleMessageBox.Show(new Wpf32Window(this), "An Error occured while creating the modpack.\n\n"+ ex.Message,
                                               "Modpack Creation Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                await UnlockUi(this);
            }


        }

        /// <summary>
        /// Updates the progress bar
        /// </summary>
        /// <param name="value">The progress value</param>
        private void ReportProgress((int current, int total, string message) report)
        {
            if (!report.message.Equals(string.Empty))
            {

                _lockProgressController.SetMessage(report.message);
                _lockProgressController.SetIndeterminate();
            }
            else
            {
                _lockProgressController.SetMessage(
                    $"{UIMessages.TTMPGettingData} ({report.current} / {report.total})");

                double value = (double)report.current / (double)report.total;
                _lockProgressController.SetProgress(value);
            }
        }

    }
}
