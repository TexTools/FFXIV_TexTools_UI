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
using AutoUpdaterDotNET;

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
        public static string GetNiceLevelName(XivDependencyLevel level, bool pluralize = false, bool everything = false)
        {
            string ret = "Unknown".L();
            switch (level)
            {
                case XivDependencyLevel.Root:
                    ret = everything ? "Everything".L() : "Metadata".L();
                    break;
                case XivDependencyLevel.Model:
                    ret = "Model".L();
                    break;
                case XivDependencyLevel.Material:
                    ret = "Material".L();
                    break;
                case XivDependencyLevel.Texture:
                    ret = "Texture".L();
                    break;
                default:
                    ret = "Unknown".L();
                    break;
            }

            if(pluralize && level != XivDependencyLevel.Invalid && level != XivDependencyLevel.Root)
            {
                ret += "(s)";
            }
            return ret;
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
            var metaFile = root.Info.GetRootFile();
            var files = new ObservableCollection<string>();
            files.Add(metaFile);
            var entry = new StandardModpackItemEntry(_inProgressItem, _inProgressLevel, files);
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
                    var result = FlexibleMessageBox.Show($"Adding this item will overwrite your existing entry for item: {entry.Item.Name._()}\nAre you sure you wish to proceed?".L(), "Item Overwrite Confirmation".L(), System.Windows.Forms.MessageBoxButtons.OKCancel, System.Windows.Forms.MessageBoxIcon.Warning, System.Windows.Forms.MessageBoxDefaultButton.Button1);
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

            if(ViewModel.SaveAdvanced)
            {
                await CreateAdvanced();
            } else
            {
                await CreateBasic();
            }
        }
        private async Task CreateAdvanced()
        {
            var tx = MainWindow.DefaultTransaction;

            string modPackPath = Path.Combine(Properties.Settings.Default.ModPack_Directory, $"{ViewModel.Name}.ttmp2");

            if (File.Exists(modPackPath))
            {
                DialogResult overwriteDialogResult = FlexibleMessageBox.Show(new Wpf32Window(this), UIMessages.ModPackOverwriteMessage,
                                            UIMessages.OverwriteTitle, MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                if (overwriteDialogResult != System.Windows.Forms.DialogResult.Yes)
                {
                    return;
                }
            }

            await LockUi(UIStrings.Creating_Modpack, null, null);
            try
            {
                var ModList = await tx.GetModList();

                var wizardData = new ModPackData()
                {
                    Name = ViewModel.Name,
                    Author = ViewModel.Author,
                    Version = ViewModel.Version,
                    Description = ViewModel.Description,
                    Url = ViewModel.Url,
                    ModPackPages = new List<ModPackData.ModPackPage>()
                };

                var page = new ModPackData.ModPackPage()
                {
                    PageIndex = 1,
                    ModGroups = new List<ModGroup>()
                };

                wizardData.ModPackPages.Add(page);

                foreach (var e in ViewModel.Entries)
                {
                    var item = e.Item;
                    var files = e.AllFiles;

                    var group = new ModGroup()
                    {
                        GroupName = item.Name,
                        SelectionType = "Multi",
                        OptionList = new List<ModOption>()
                    };
                    page.ModGroups.Add(group);

                    var option = new ModOption
                    {
                        GroupName = group.GroupName,
                        IsChecked = true,
                        Name = GetNiceLevelName(e.Level, true, true),
                        Description = $"Item: {item.Name._()}\nInclusion Level: {GetNiceLevelName(e.Level)._()}\nPrimary Files:{e.MainFiles.Count._()}\nTotal Files:{e.AllFiles.Count._()}".L(),
                        SelectionType = "Multi",
                    };
                    group.OptionList.Add(option);

                    foreach (var file in e.AllFiles)
                    {
                        var exists = await tx.FileExists(file);

                        // This is a funny case where in order to create the modpack we actually have to write a default meta entry to the dats first.
                        // If we had the right functions we could just load and serialize the data, but we don't atm.
                        if (!exists && Path.GetExtension(file) == ".meta")
                        {
                            var meta = await ItemMetadata.GetMetadata(file);
                            await ItemMetadata.SaveMetadata(meta, XivStrings.TexTools);
                        }

                        var isDef = await Modding.GetModState(file) == xivModdingFramework.Mods.Enums.EModState.UnModded;

                        var fData = new ModData
                        {
                            Name = e.Item.Name,
                            Category = e.Item.SecondaryCategory,
                            FullPath = file,
                            IsDefault = isDef,
                            ModDataBytes = await tx.ReadFile(file, false, true)
                        };
                        option.Mods.Add(file, fData);
                    }
                }

                // Okay modpack is now created internally, just need to save it.
                var progressIndicator = new Progress<double>(ReportProgressAdv);
                await TTMP.CreateWizardModPack(wizardData, Properties.Settings.Default.ModPack_Directory, progressIndicator , true);
                FlexibleMessageBox.Show(new Wpf32Window(this), "Modpack Created Successfully.".L(),
                                               "Modpack Created".L(), MessageBoxButtons.OK, MessageBoxIcon.Information);
                await UnlockUi(this);
                DialogResult = true;
            } catch(Exception ex)
            {
                FlexibleMessageBox.Show(new Wpf32Window(this), "An Error occured while creating the modpack.\n\n".L() + ex.Message,
                                               "Modpack Creation Error".L(), MessageBoxButtons.OK, MessageBoxIcon.Error);
                await UnlockUi(this);
            }
        }
        private async Task CreateBasic()
        {
            string modPackPath = Path.Combine(Properties.Settings.Default.ModPack_Directory, $"{ViewModel.Name}.ttmp2");

            if (File.Exists(modPackPath))
            {
                DialogResult overwriteDialogResult = FlexibleMessageBox.Show(new Wpf32Window(this), UIMessages.ModPackOverwriteMessage,
                                            UIMessages.OverwriteTitle, MessageBoxButtons.YesNoCancel, MessageBoxIcon.Warning);
                if (overwriteDialogResult != System.Windows.Forms.DialogResult.Yes)
                {
                    return;
                }
            }

            var tx = MainWindow.DefaultTransaction;

            var ModList = await tx.GetModList();

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
                    var exists = await tx.FileExists(file);

                    // This is a funny case where in order to create the modpack we actually have to write a default meta entry to the dats first.
                    // If we had the right functions we could just load and serialize the data, but we don't atm.
                    if (!exists && Path.GetExtension(file) == ".meta")
                    {
                        var meta = await ItemMetadata.GetMetadata(file);
                        await ItemMetadata.SaveMetadata(meta, XivStrings.TexTools);
                    }

                    var offset = await tx.Get8xDataOffset(file);
                    var compressedSize = await tx.GetCompressedFileSize(file);
                    var dataFile = IOUtil.GetDataFileFromPath(file);
                    ModList.Mods.TryGetValue(file, out var modEntry);


                    var isDef = await Modding.GetModState(file) == xivModdingFramework.Mods.Enums.EModState.UnModded;


                    SimpleModData simpleData = new SimpleModData
                    {
                        Name = entry.Item.Name,
                        Category = entry.Item.SecondaryCategory,
                        FullPath = file,
                        ModOffset = offset,
                        ModSize = compressedSize,
                        IsDefault = isDef,
                        DatFile = dataFile.GetFileName()
                    };

                    simpleModPackData.SimpleModDataList.Add(simpleData);

                }
            }



            try
            {
                await LockUi(UIStrings.Creating_Modpack, null, null);
                await TTMP.CreateSimpleModPack(simpleModPackData, Properties.Settings.Default.ModPack_Directory, ViewHelpers.BindReportProgress(_lockProgressController), true);

                FlexibleMessageBox.Show(new Wpf32Window(this), "Modpack Created Successfully.".L(),
                                               "Modpack Created".L(), MessageBoxButtons.OK, MessageBoxIcon.Information);
                await UnlockUi(this);
                DialogResult = true;
            }
            catch(Exception ex)
            {
                FlexibleMessageBox.Show(new Wpf32Window(this), "An Error occured while creating the modpack.\n\n".L()+ ex.Message,
                                               "Modpack Creation Error".L(), MessageBoxButtons.OK, MessageBoxIcon.Error);
                await UnlockUi(this);
            }


        }

        private void ReportProgressAdv(double value)
        {
            // No-op
        }


    }
}
