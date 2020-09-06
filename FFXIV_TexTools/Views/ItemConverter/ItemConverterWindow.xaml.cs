using FFXIV_TexTools.Annotations;
using FFXIV_TexTools.Helpers;
using FFXIV_TexTools.Resources;
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
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Xceed.Wpf.Toolkit.PropertyGrid.Attributes;
using xivModdingFramework.Cache;
using xivModdingFramework.Items.Enums;
using xivModdingFramework.Items.Interfaces;
using xivModdingFramework.Mods;
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
                _lockProgressController.SetMessage(update);
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
        private XivDependencyRoot Destination;

        public ItemConverterWindow()
        {
            InitializeComponent();

            ItemSelect.RawItemSelected += ItemSelect_RawItemSelected;
            ItemSelect.ItemConfirmed += ItemSelect_ItemConfirmed;
            ItemSelect.ExtraSearchFunction = Filter;
            ItemSelect.LockUiFunction = LockUi;
            ItemSelect.UnlockUiFunction = UnlockUi;

            SetState(ItemConverterState.SourceSelect);
        }

        private void SetState(ItemConverterState state)
        {
            State = state;

            if(State == ItemConverterState.SourceSelect)
            {
                TitleLabel.Content = "Select Source Item";
                ItemSelectGrid.Visibility = Visibility.Visible;
                ConfirmationGrid.Visibility = Visibility.Collapsed;

                ItemSelect.SelectButton.Content = "Select Source Item";


                ItemSelect.ClearSelection();

                BackButton.Content = "Cancel";
                NextButton.Visibility = Visibility.Collapsed;

            } else if(State == ItemConverterState.DestinationSelect)
            {
                TitleLabel.Content = "Select Destination Item";
                ItemSelectGrid.Visibility = Visibility.Visible;
                ConfirmationGrid.Visibility = Visibility.Collapsed;

                ItemSelect.ClearSelection();

                ItemSelect.SelectButton.Content = "Select Destination Item";

                BackButton.Content = "Back";
                NextButton.Visibility = Visibility.Collapsed;


            } else if(State == ItemConverterState.Confirmation)
            {
                TitleLabel.Content = "Final Confirmation";
                ItemSelectGrid.Visibility = Visibility.Collapsed;
                ConfirmationGrid.Visibility = Visibility.Visible;

                BackButton.Content = "Back";
                NextButton.Visibility = Visibility.Visible;

                ShowConversionStats();
            } else
            {
                TitleLabel.Content = "Loading...";
                ItemSelectGrid.Visibility = Visibility.Collapsed;
                ConfirmationGrid.Visibility = Visibility.Collapsed;

                BackButton.Content = "Cancel";
                NextButton.Visibility = Visibility.Collapsed;
                return;
            }
        }

        private bool IsSupported(XivDependencyRoot fullRoot)
        {

            if (fullRoot == null) return false;

            var root = fullRoot.Info;
            if (root == null) return false;
            if (root.PrimaryType == XivItemType.monster) return false;
            if (root.PrimaryType == XivItemType.demihuman) return false;
            if (root.PrimaryType == XivItemType.indoor) return false;
            if (root.PrimaryType == XivItemType.outdoor) return false;

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

            if (root.PrimaryType != src.PrimaryType) return false;
            if (root.SecondaryType != src.SecondaryType) return false;
            if (root.Slot != src.Slot) return false;

            if(root.PrimaryType == XivItemType.human)
            {
                if (root.PrimaryId != src.PrimaryId) return false;
            }

            // Don't let people copy onto set equipment set 0.  It's special and doesn't like some of the metadata elements.
            if (root.PrimaryType == XivItemType.equipment && root.PrimaryId == 0) return false;

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
                ItemSelect.SelectButton.Content = "Unsupported";
                return;
            }

            if(State == ItemConverterState.DestinationSelect && !DestinationOk(root))
            {
                ItemSelect.SelectButton.IsEnabled = false;
                ItemSelect.SelectButton.Content = "Invalid Destination";
                return;
            }

            ItemSelect.SelectButton.IsEnabled = true;
            if(State == ItemConverterState.SourceSelect)
            {
                ItemSelect.SelectButton.Content = "Select Source Item";
            } else
            {
                ItemSelect.SelectButton.Content = "Select Destination Item";
            }
        }

        #region Item List Filters
        private bool Filter(IItem item)
        {
            if (item.PrimaryCategory == XivStrings.Gear) return true;
            if (item.PrimaryCategory == XivStrings.Character)
            {
                if (item.SecondaryCategory == XivStrings.Hair) return true;
            }
            return false;
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
            SetState(ItemConverterState.DestinationSelect);
        }
        private void DestinationSelected(object sender, IItem e)
        {
            Destination = e.GetRoot();
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

            var imc = new Imc(XivCache.GameInfo.GameDirectory);


            SourceBox.Text = Source.Info.GetBaseFileName();
            DestinationBox.Text = Destination.Info.GetBaseFileName();

            if (Imc.UsesImc(Source))
            {
                var sourceInfo = await imc.GetFullImcInfo(Source.GetRawImcFilePath());
                var destInfo = await imc.GetFullImcInfo(Destination.GetRawImcFilePath());
                SourceVariantsBox.Text = (sourceInfo.SubsetCount + 1).ToString();
                DestinationVariantsBox.Text = (destInfo.SubsetCount + 1).ToString();
            } else
            {
                SourceVariantsBox.Text = "1";
                DestinationVariantsBox.Text = "1";

            }

            var items = await Destination.GetAllItems();

            AffectedItemsBox.Items.Clear();
            foreach (var item in items)
            {
                AffectedItemsBox.Items.Add(item.Name);
            }
        }

        private async void NextButton_Click(object sender, RoutedEventArgs e)
        {
            await LockUi("Cloning Items", "Please wait...", this);
            try
            {
                await RootCloner.CloneRoot(Source, Destination, XivStrings.TexTools, _lockProgress);
            }
            catch(Exception ex)
            {
                await UnlockUi(this);
                FlexibleMessageBox.Show("Unable to convert items:\n\nError: " + ex.Message, "Item Conversion Error", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Error);
                return;

            }
            await UnlockUi(this);
            FlexibleMessageBox.Show("Items converted successfully.", "Item Conversion Successful", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Information);
            Close();
        }
    }
}
