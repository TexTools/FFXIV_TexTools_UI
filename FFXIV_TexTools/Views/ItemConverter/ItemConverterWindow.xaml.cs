using FFXIV_TexTools.Annotations;
using FFXIV_TexTools.Helpers;
using FFXIV_TexTools.Properties;
using FFXIV_TexTools.Resources;
using FolderSelect;
using MahApps.Metro.Controls;
using MahApps.Metro.Controls.Dialogs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Xceed.Wpf.Toolkit.PropertyGrid.Attributes;
using xivModdingFramework.Cache;
using xivModdingFramework.Items.DataContainers;
using xivModdingFramework.Items.Enums;
using xivModdingFramework.Items.Interfaces;
using xivModdingFramework.Models.DataContainers;
using xivModdingFramework.Mods;
using xivModdingFramework.Mods.FileTypes;
using xivModdingFramework.Variants.FileTypes;

namespace FFXIV_TexTools.Views.ItemConverter
{

    /// <summary>
    /// Interaction logic for ItemConverterWindow.xaml
    /// </summary>
    public partial class ItemConverterWindow : MetroWindow
    {
        private enum ItemConverterState
        {
            Invalid,
            SourceSelect,
            DestinationSelect,
            Confirmation
        };

        private ProgressDialogController _lockProgressController;
        private IProgress<string> _lockProgress;
        public async Task LockUi(string title, string message, object sender)
        {
            _lockProgressController = await this.ShowProgressAsync(title, message);

            _lockProgressController.SetIndeterminate();

            _lockProgress = new Progress<string>((update) =>
            {
                _lockProgressController.SetMessage(update.L());
            });
        }
        public async Task UnlockUi(object sender)
        {
            await _lockProgressController.CloseAsync();
            _lockProgressController = null;
            _lockProgress = null;
        }

        private ItemConverterState State;

        private XivDependencyRoot Source;
        private IItem SourceItem;
        private XivDependencyRoot Destination;
        private IItem DestinationItem;

        public ItemConverterWindow()
        {
            InitializeComponent();

            ItemSelect.RawItemSelected += ItemSelect_RawItemSelected;
            ItemSelect.ItemConfirmed += ItemSelect_ItemConfirmed;
            ItemSelect.ExtraSearchFunction = Filter;
            ItemSelect.LockUiFunction = LockUi;
            ItemSelect.UnlockUiFunction = UnlockUi;

            SetState(ItemConverterState.SourceSelect);

            Closing += ItemConverterWindow_Closing;
        }

        private void ItemConverterWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (null != Owner)
            {
                Owner.Activate();
            }
        }

        private void SetState(ItemConverterState state)
        {
            State = state;

            if(State == ItemConverterState.SourceSelect)
            {
                TitleLabel.Content = "Select Source Item".L();
                ItemSelectGrid.Visibility = Visibility.Visible;
                ConfirmationGrid.Visibility = Visibility.Collapsed;

                ItemSelect.SelectButton.Content = "Select Source Item".L();


                ItemSelect.ClearSelection();

                BackButton.Content = "Cancel".L();
                NextButton.Visibility = Visibility.Collapsed;

            } else if(State == ItemConverterState.DestinationSelect)
            {
                TitleLabel.Content = "Select Destination Item".L();
                ItemSelectGrid.Visibility = Visibility.Visible;
                ConfirmationGrid.Visibility = Visibility.Collapsed;

                ItemSelect.ClearSelection();

                ItemSelect.SelectButton.Content = "Select Destination Item".L();

                BackButton.Content = "Back".L();
                NextButton.Visibility = Visibility.Collapsed;


            } else if(State == ItemConverterState.Confirmation)
            {
                TitleLabel.Content = "Final Confirmation".L();
                ItemSelectGrid.Visibility = Visibility.Collapsed;
                ConfirmationGrid.Visibility = Visibility.Visible;

                BackButton.Content = "Back".L();
                NextButton.Visibility = Visibility.Visible;

                ShowConversionStats();
            } else
            {
                TitleLabel.Content = "Loading...".L();
                ItemSelectGrid.Visibility = Visibility.Collapsed;
                ConfirmationGrid.Visibility = Visibility.Collapsed;

                BackButton.Content = "Cancel".L();
                NextButton.Visibility = Visibility.Collapsed;
                return;
            }
        }

        private bool IsSupported(XivDependencyRoot fullRoot)
        {

            if (fullRoot == null) return false;

            var root = fullRoot.Info;
            if (root == null) return false;
            if (!root.IsValid()) return false;

            if (root.PrimaryType == XivItemType.monster) return false;
            if (root.PrimaryType == XivItemType.demihuman) return false;
            if (root.PrimaryType == XivItemType.indoor) return false;
            if (root.PrimaryType == XivItemType.outdoor) return false;
            if (root.PrimaryType == XivItemType.fish) return false;

            if (root.PrimaryType == XivItemType.human)
            {
                if (root.SecondaryType == XivItemType.body) return false;
                if (root.SecondaryType == XivItemType.face) return false;
            }

            return true;
        }

        private bool DestinationOk(XivDependencyRoot fullRoot)
        {
            if (fullRoot == null) return false;
            if (Source == null) return false;

            var root = fullRoot.Info;
            var src = Source.Info;

            // Convert To Accessory Handling
            if((src.PrimaryType == XivItemType.equipment || src.PrimaryType == XivItemType.accessory) && root.PrimaryType == XivItemType.accessory)
            {
                // Allow swapping Most things to Accessories.
                return true;
            }

            if (root.PrimaryType != src.PrimaryType) return false;
            if (root.SecondaryType != src.SecondaryType) return false;
            if (root.Slot != src.Slot) return false;

            if(root.PrimaryType == XivItemType.human)
            {
                if (root.PrimaryId != src.PrimaryId) return false;
            }

            return true;
        }
        private void ItemSelect_RawItemSelected(object sender, IItem item)
        {
            if(item == null)
            {
                ItemSelect.SelectButton.IsEnabled = false;
                ItemSelect.SelectButton.Content = "--";
                return;
            }

            var root = item.GetRoot();
            if (!IsSupported(root))
            {
                ItemSelect.SelectButton.IsEnabled = false;
                ItemSelect.SelectButton.Content = "Unsupported".L();
                return;
            }

            if(State == ItemConverterState.DestinationSelect && !DestinationOk(root))
            {
                ItemSelect.SelectButton.IsEnabled = false;
                ItemSelect.SelectButton.Content = "Invalid Destination".L();
                return;
            }

            ItemSelect.SelectButton.IsEnabled = true;
            if(State == ItemConverterState.SourceSelect)
            {
                ItemSelect.SelectButton.Content = "Select Source Item".L();
            } else
            {
                ItemSelect.SelectButton.Content = "Select Destination Item".L();
            }
        }

        #region Item List Filters
        private bool Filter(IItem item)
        {
            if (item == null)
            {
                return false;
            }

            var root = item.GetRoot();
            if (!IsSupported(root))
            {
                return false;
            }

            if (State == ItemConverterState.DestinationSelect && !DestinationOk(root))
            {
                return false;
            }
            return true;
        }
        #endregion

        #region Selection Confirmed
        private void ItemSelect_ItemConfirmed(object sender, IItem e)
        {
            if (State == ItemConverterState.SourceSelect)
            {
                SourceSelected(sender, e);
            }
            else if (State == ItemConverterState.DestinationSelect)
            {
                DestinationSelected(sender, e);
            }
        }
        private void SourceSelected(object sender, IItem e)
        {
            Source = e.GetRoot();
            SourceItem = e;
            SetState(ItemConverterState.DestinationSelect);
        }
        private void DestinationSelected(object sender, IItem e)
        {
            Destination = e.GetRoot();
            DestinationItem = e;
            SetState(ItemConverterState.Confirmation);
        }
        #endregion

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            switch(State)
            {
                case ItemConverterState.SourceSelect:
                    Close();
                    return;
                case ItemConverterState.DestinationSelect:
                    SetState(ItemConverterState.SourceSelect);
                    return;
                case ItemConverterState.Confirmation:
                    SetState(ItemConverterState.DestinationSelect);
                    return;
                default:
                    Close();
                    return;
            }
        }

        public async Task ShowConversionStats()
        {
            if (!DestinationOk(Destination)) return;


            SourceBox.Text = Source.Info.GetBaseFileName() + " (" + SourceItem.Name + ")";
            DestinationBox.Text = Destination.Info.GetBaseFileName() + " (" + DestinationItem.Name + ")";
            var tx = MainWindow.DefaultTransaction;

            if (Imc.UsesImc(Source))
            {
                try
                {
                    var sourceInfo = await Imc.GetFullImcInfo(Source.GetRawImcFilePath(), false, tx);
                    var destInfo = await Imc.GetFullImcInfo(Destination.GetRawImcFilePath(), false, tx);
                    SourceVariantsBox.Text = (sourceInfo.SubsetCount + 1).ToString();
                    DestinationVariantsBox.Text = (destInfo.SubsetCount + 1).ToString();
                    SameVariantBox.IsEnabled = true;
                    SameVariantBox.IsChecked = true;
                }
                catch
                {
                    SameVariantBox.IsEnabled = true;
                    SameVariantBox.IsChecked = true;
                }
            } else
            {
                SameVariantBox.IsEnabled = false;
                SameVariantBox.IsChecked = false;
                SourceVariantsBox.Text = "1";
                DestinationVariantsBox.Text = "1";
            }

            var items = await Destination.GetAllItems(-1, tx);

            AffectedItemsBox.Items.Clear();
            foreach (var item in items)
            {
                AffectedItemsBox.Items.Add(item.Name);
            }
        }

        private async void NextButton_Click(object sender, RoutedEventArgs e)
        {
            var windowHandle = new WindowWrapper(new WindowInteropHelper(this).Handle);
            await LockUi("Cloning Items".L(), "Please wait...".L(), this);

            try
            {
                var variant = -1;
                if (SameVariantBox.IsChecked == true)
                {
                    variant = ((IItemModel)SourceItem).ModelInfo.ImcSubsetID;
                }

                var extraConversions = new Dictionary<XivDependencyRoot, XivDependencyRoot>();
                try
                {
                    extraConversions = await GetExtraConversions();
                    if(extraConversions == null)
                    {
                        return;
                    }
                }
                catch
                {
                    extraConversions.Clear();
                }



                var saveModpack = SaveModpackFileBox.IsChecked == true ? true : false;
                string mpd = saveModpack ? Settings.Default.ModPack_Directory : null;
                await Task.Run(async () =>
                {
                    await RootCloner.CloneRoot(Source, Destination, XivStrings.TexTools, variant, mpd, _lockProgress, MainWindow.UserTransaction);

                    foreach(var kv in extraConversions)
                    {
                        await RootCloner.CloneRoot(kv.Key, kv.Value, XivStrings.TexTools, variant, mpd, _lockProgress, MainWindow.UserTransaction);
                    }
                });
            }
            catch(Exception ex)
            {
                while(ex.InnerException != null)
                {
                    ex = ex.InnerException;
                }

                await UnlockUi(this);
                FlexibleMessageBox.Show(windowHandle, "Unable to convert items:\n\nError: ".L() + ex.Message, "Item Conversion Error".L(), System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Error);
                return;

            }
            await UnlockUi(this);
            FlexibleMessageBox.Show(windowHandle, "Items converted successfully.".L(), "Item Conversion Successful".L(), System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Information);
            Close();
        }

        /// <summary>
        /// Performs a metadata analysis between the two sets to determine what other slots, if any,
        /// need to be converted along with this item to guarantee proper visuals.
        /// </summary>
        /// <returns></returns>
        private async Task<Dictionary<XivDependencyRoot, XivDependencyRoot>> GetExtraConversions()
        {
            var variant = -1;
            if (SameVariantBox.IsChecked == true)
            {
                variant = ((IItemModel)SourceItem).ModelInfo.ImcSubsetID;
            }

            var extraConversions = new Dictionary<XivDependencyRoot, XivDependencyRoot>();

            if(Destination.Info.PrimaryType != XivItemType.equipment)
            {
                // If we're converting off of equipment there's not extras to swap.
                return extraConversions;
            }
            var tx = MainWindow.DefaultTransaction;

            // For top items, we need to check and see if a recursive action is required.
            var meta = await ItemMetadata.GetMetadata(Source, false, tx);
            var eqp = meta.EqpEntry;

            if (eqp != null && eqp.GetFlag(EquipmentParameterFlag.EnableBodyFlags))
            {
                var hideHand = !eqp.GetFlag(EquipmentParameterFlag.BodyShowHand);
                var hideLeg = !eqp.GetFlag(EquipmentParameterFlag.BodyShowLeg);
                var hideHead = !eqp.GetFlag(EquipmentParameterFlag.BodyShowHead);

                // If we don't full-hide any other slots, recursion is never needed.
                if (hideHand || hideLeg || hideHead)
                {
                    var itemConversions = "";

                    if (hideHand)
                    {
                        // Recursive actions are only necessary if the base set actually differs in terms of what accessories are shown.
                        var sourceAltRoot = Source.Info.GetOtherSlot("glv").ToFullRoot();
                        var destAltRoot = Destination.Info.GetOtherSlot("glv").ToFullRoot();

                        var sourceAltMeta = await ItemMetadata.GetMetadata(sourceAltRoot, false, tx);
                        var destAltMeta = await ItemMetadata.GetMetadata(destAltRoot, false, tx);

                        var srcShowRingR = sourceAltMeta.EqpEntry.GetFlag(EquipmentParameterFlag.HandShowRingR);
                        var dstShowRingR = destAltMeta.EqpEntry.GetFlag(EquipmentParameterFlag.HandShowRingR);

                        var srcShowRingL = sourceAltMeta.EqpEntry.GetFlag(EquipmentParameterFlag.HandShowRingL);
                        var dstShowRingL = destAltMeta.EqpEntry.GetFlag(EquipmentParameterFlag.HandShowRingL);

                        var srcShowBrac = sourceAltMeta.EqpEntry.GetFlag(EquipmentParameterFlag.HandShowBracelet);
                        var dstShowBrac = destAltMeta.EqpEntry.GetFlag(EquipmentParameterFlag.HandShowBracelet);


                        if (srcShowRingR != dstShowRingR
                            || srcShowRingL != dstShowRingL
                            || srcShowBrac != dstShowBrac
                            || !destAltMeta.EqpEntry.GetFlag(EquipmentParameterFlag.EnableHandFlags))
                        {

                            var sourceAltItem = sourceAltRoot.GetFirstItem(variant);
                            var destAltItem = destAltRoot.GetFirstItem();

                            itemConversions += sourceAltItem.Name + " => " + destAltItem.Name + "\n";
                            extraConversions.Add(sourceAltRoot, destAltRoot);
                        }
                    }

                    if (hideLeg)
                    {
                        // Recursive actions are only necessary if the base set actually differs in terms whether feet are shown or not.
                        var sourceAltRoot = Source.Info.GetOtherSlot("dwn").ToFullRoot();
                        var destAltRoot = Destination.Info.GetOtherSlot("dwn").ToFullRoot();

                        var sourceAltMeta = await ItemMetadata.GetMetadata(sourceAltRoot, false, tx);
                        var destAltMeta = await ItemMetadata.GetMetadata(destAltRoot, false, tx);

                        var srcShowFoot = sourceAltMeta.EqpEntry.GetFlag(EquipmentParameterFlag.LegShowFoot);
                        var dstShowFoot = destAltMeta.EqpEntry.GetFlag(EquipmentParameterFlag.LegShowFoot);
                        if (srcShowFoot != dstShowFoot
                            || !destAltMeta.EqpEntry.GetFlag(EquipmentParameterFlag.EnableLegFlags))
                        {
                            var sourceAltItem = sourceAltRoot.GetFirstItem(variant);
                            var destAltItem = destAltRoot.GetFirstItem();


                            itemConversions += sourceAltItem.Name + " => " + destAltItem.Name + "\n";
                            extraConversions.Add(sourceAltRoot, destAltRoot);
                        }
                    }

                    if (hideHead)
                    {
                        // Calculation recursion on head is a nightmare of like 12+ flags.
                        // Just always suggest converting the headpiece.
                        var sourceAltRoot = Source.Info.GetOtherSlot("met").ToFullRoot();
                        var destAltRoot = Destination.Info.GetOtherSlot("met").ToFullRoot();

                        var sourceAltItem = sourceAltRoot.GetFirstItem(variant);
                        var destAltItem = destAltRoot.GetFirstItem();

                        itemConversions += sourceAltItem.Name + " => " + destAltItem.Name + "\n";
                        extraConversions.Add(sourceAltRoot, destAltRoot);
                    }

                    if (extraConversions.Count > 0)
                    {


                        var msg = $"In order to fully convert this item, the following items may also need to be converted with it.\n\n{itemConversions._()}\nPerform these conversions as well?".L();

                        var result = FlexibleMessageBox.Show(msg, "Multi-Slot Convert Required".L(), System.Windows.Forms.MessageBoxButtons.YesNoCancel, System.Windows.Forms.MessageBoxIcon.Warning);

                        if (result == System.Windows.Forms.DialogResult.Yes)
                        {
                            return extraConversions;
                        }
                        else if (result == System.Windows.Forms.DialogResult.No)
                        {
                            extraConversions.Clear();
                            return extraConversions;
                        }
                        else
                        {
                            return null;
                        }
                    }
                }
            }

            // Rings
            if(Source.Info.Slot == "ril" || Source.Info.Slot == "rir")
            {
                var itemConversions = "";
                if (Source.Info.Slot == "ril")
                {
                    var sourceAltRoot = Source.Info.GetOtherSlot("rir").ToFullRoot();
                    var destAltRoot = Destination.Info.GetOtherSlot("rir").ToFullRoot();

                    var sourceAltItem = sourceAltRoot.GetFirstItem(variant);
                    var destAltItem = destAltRoot.GetFirstItem();

                    itemConversions += sourceAltItem.Name + " => " + destAltItem.Name + "\n";
                    extraConversions.Add(sourceAltRoot, destAltRoot);
                } else
                {
                    var sourceAltRoot = Source.Info.GetOtherSlot("ril").ToFullRoot();
                    var destAltRoot = Destination.Info.GetOtherSlot("ril").ToFullRoot();

                    var sourceAltItem = sourceAltRoot.GetFirstItem(variant);
                    var destAltItem = destAltRoot.GetFirstItem();

                    itemConversions += sourceAltItem.Name + " => " + destAltItem.Name + "\n";
                    extraConversions.Add(sourceAltRoot, destAltRoot);
                }

                var msg = $"In order to properly convert this item, the following item may also need to be converted with it:\n\n{itemConversions._()}\nPerform these conversions as well?".L();

                var result = FlexibleMessageBox.Show(msg, "Off-Ring Conversion Required".L(), System.Windows.Forms.MessageBoxButtons.YesNoCancel, System.Windows.Forms.MessageBoxIcon.Warning);

                if (result == System.Windows.Forms.DialogResult.Yes)
                {
                    return extraConversions;
                }
                else if (result == System.Windows.Forms.DialogResult.No)
                {
                    extraConversions.Clear();
                    return extraConversions;
                }
                else
                {
                    return null;
                }
            }

            // Dual Wield weapons.
            if(SourceItem.GetType() == typeof(XivGear) && DestinationItem.GetType() == typeof(XivGear))
            {
                var sourceGear = (XivGear)SourceItem;
                var destGear = (XivGear)DestinationItem;

                if(sourceGear.PairedItem != null && destGear.PairedItem != null)
                {
                    var itemConversions = "";
                    var sourceOffhand = sourceGear.PairedItem.GetRoot();
                    var destOffhand = destGear.PairedItem.GetRoot();


                    extraConversions.Add(sourceOffhand, destOffhand);
                    itemConversions += sourceGear.PairedItem.Name + " => " + destGear.PairedItem.Name + "\n";

                    var msg = $"In order to properly convert this item, the following item may also need to be converted with it:\n\n{itemConversions}\nPerform these conversions as well?".L();

                    var result = FlexibleMessageBox.Show(msg, "Dual-Wield Conversion Required".L(), System.Windows.Forms.MessageBoxButtons.YesNoCancel, System.Windows.Forms.MessageBoxIcon.Warning);

                    if (result == System.Windows.Forms.DialogResult.Yes)
                    {
                        return extraConversions;
                    }
                    else if (result == System.Windows.Forms.DialogResult.No)
                    {
                        extraConversions.Clear();
                        return extraConversions;
                    }
                    else
                    {
                        return null;
                    }
                }

            }

            extraConversions.Clear();
            return extraConversions;
        }
    }
}
