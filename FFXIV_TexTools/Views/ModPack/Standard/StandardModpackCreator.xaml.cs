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
using System.Windows.Markup;
using System.Diagnostics;
using FFXIV_TexTools.Views.Wizard;
using xivModdingFramework.General;

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

            Closing += StandardModpackCreator_Closing;
        }

        private void StandardModpackCreator_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if(Owner != null)
            {
                Owner.Activate();
            }
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
                _ = SelectAllFiles();
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

            _ = CreateModpack();
        }

        private async Task CreateModpack()
        {

            var success = false;
            await LockUi("Creating Modpack", "Please wait...", this);
            try
            {
                await Create();
                success = true;
            }
            catch(Exception ex)
            {
                ViewHelpers.ShowError(this, "Mod Creation Error", "An error occurred while creating the mod:\n\n" + ex.Message);
            }
            finally
            {
                await UnlockUi(this);
                if (success)
                {
                    DialogResult = true;
                }
            }
        }
        private async Task Create()
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

            SimpleModPackData simpleModPackData = new SimpleModPackData
            {
                Name = ViewModel.Name,
                Author = ViewModel.Author,
                Version = ViewModel.Version,
                Description = ViewModel.Description,
                Url = ViewModel.Url,
                SimpleModDataList = new List<SimpleModData>()
            };

            var tx = MainWindow.DefaultTransaction;
            try
            {
                // Create the data to write.
                var wizardData = new WizardData();

                wizardData.MetaPage.Author = ViewModel.Author;
                wizardData.MetaPage.Name = ViewModel.Name;
                wizardData.MetaPage.Description = ViewModel.Description;
                wizardData.MetaPage.Url = ViewModel.Url;
                wizardData.MetaPage.Version = ViewModel.Version.ToString();
                wizardData.MetaPage.Image = ViewModel.Image;

                var page = new WizardPageEntry();
                var group = new WizardGroupEntry();
                group.OptionType = EOptionType.Multi;
                page.Groups.Add(group);
                wizardData.DataPages.Add(page);

                group.Name = "Items";


                foreach (var entry in ViewModel.Entries)
                {
                    var option = new WizardOptionEntry(group);
                    option.Name = "Default Option";
                    group.Options.Add(option);
                    option.Selected = true;
                    foreach (var file in entry.AllFiles)
                    {
                        // Need to compose these first, since they may not exist in packable form yet.
                        if (file.EndsWith(".meta") || file.EndsWith(".rgsp")) {
                            byte[] data;
                            if (file.EndsWith(".meta"))
                            {
                                var meta = await ItemMetadata.GetMetadata(file, false, tx);
                                data = await ItemMetadata.Serialize(meta);
                            } else
                            {
                                var rg = CMP.GetRaceGenderFromRgspPath(file);
                                var rgsp = await CMP.GetScalingParameter(rg.Race, rg.Gender, false, tx);
                                data = rgsp.GetBytes();
                            }

                            var path = IOUtil.GetFrameworkTempFile();
                            File.WriteAllBytes(path, data);

                            var info = new FileStorageInformation()
                            {
                                FileSize = 0,
                                RealOffset = 0,
                                RealPath = path,
                                StorageType = EFileStorageType.UncompressedIndividual
                            };
                            option.StandardData.Files.Add(file, info);
                        }
                        else
                        {
                            var info = await tx.UNSAFE_GetStorageInfo(file);
                            option.StandardData.Files.Add(file, info);
                        }
                    }
                }

                if (ViewModel.ModpackPath.ToLower().EndsWith(".ttmp2"))
                {
                    var option = group.Options[0];
                    option.Image = ViewModel.Image;
                    await wizardData.WriteWizardPack(ViewModel.ModpackPath);
                }
                else
                {
                    await wizardData.WritePmp(ViewModel.ModpackPath);
                }

            }
            catch(Exception ex)
            {

                FlexibleMessageBox.Show(new Wpf32Window(this), "An Error occured while creating the modpack.\n\n".L()+ ex.Message,
                                               "Modpack Creation Error".L(), MessageBoxButtons.OK, MessageBoxIcon.Error);
            }


        }


        private void ReportProgressAdv(double value)
        {
            // No-op
        }


    }
}
