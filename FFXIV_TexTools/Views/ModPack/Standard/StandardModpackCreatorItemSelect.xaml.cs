using FFXIV_TexTools.Resources;
using FFXIV_TexTools.ViewModels;
using FFXIV_TexTools.Views.Controls;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using xivModdingFramework.Cache;
using xivModdingFramework.Items.Interfaces;

namespace FFXIV_TexTools.Views
{
    /// <summary>
    /// Interaction logic for StandardModpackCreatorItemSelect.xaml
    /// </summary>
    public partial class StandardModpackCreatorItemSelect : Page
    {
        private StandardModpackViewModel _vm;
        private StandardModpackCreator _window;

        public event EventHandler<IItem> ItemSelected;
        public event EventHandler FinalizeRequested;

        private static ItemSelectControl ItemSelect;
        private bool _disposed;

        public StandardModpackCreatorItemSelect(StandardModpackCreator window, StandardModpackViewModel vm)
        {
            _vm = vm;
            _window = window;
            DataContext = _vm;

            InitializeComponent();
            //< controls:ItemSelectControl x:Name = "ItemSelect" Width = "Auto" MainMenuMode = "False" Margin = "0" Grid.RowSpan = "3" Height = "Auto" />
            if(ItemSelect == null)
            {
                ItemSelect = new ItemSelectControl();
                ItemSelect.DeferLoading = false;
                ItemSelect.ExpandCharacterMenu = true;
                ItemSelect.MainMenuMode = false;
                ItemSelect.SetValue(Grid.RowSpanProperty, 3);
                ItemSelect.Width = Double.NaN;
                ItemSelect.Height = Double.NaN;
            }
            PrimaryGrid.Children.Add(ItemSelect);
            ItemSelect.ItemConfirmed += ItemSelect_ItemConfirmed;
            CancelButton.Click += CancelButton_Click;
            FinalReviewButton.Click += FinalReviewButton_Click;
            ItemSelect.RawItemSelected += ItemSelect_RawItemSelected;

            ItemSelect.LockUiFunction = _window.LockUi;
            ItemSelect.UnlockUiFunction = _window.UnlockUi;
            ItemSelect.ExtraSearchFunction = Filter;

            ItemSelect_RawItemSelected(this, ItemSelect.SelectedItem);

            foreach (var entry in vm.Entries)
            {
                var control = new StandardModpackEntryControl(entry);
                control.RemoveEntry += Control_RemoveEntry;
                AddedItemsPanel.Children.Add(control);
            }

            this.Unloaded += StandardModpackCreatorItemSelect_Unloaded;
            UpdateTotalFiles();
        }

        private void StandardModpackCreatorItemSelect_Unloaded(object sender, RoutedEventArgs e)
        {
            // Remove all of our attached handlers and static connections.
            ItemSelect.ItemConfirmed -= ItemSelect_ItemConfirmed;
            CancelButton.Click -= CancelButton_Click;
            FinalReviewButton.Click -= FinalReviewButton_Click;
            ItemSelect.RawItemSelected -= ItemSelect_RawItemSelected;
            ItemSelect.LockUiFunction = null;
            ItemSelect.UnlockUiFunction = null;
            ItemSelect.ExtraSearchFunction = null;
            PrimaryGrid.Children.Remove(ItemSelect);
        }


        /// <summary>
        /// Extra search filter criterion.  Lets us filter out unsupported items.
        /// </summary>
        private bool Filter(IItem item) {

            // Character is kind of messy and needs a little work to make support work smoothly in this menu.
            // UI Won't ever be supported since there's nothing to connect them together via (that I know of at least -Sel)
            if(item.PrimaryCategory == XivStrings.UI || item.SecondaryCategory == XivStrings.Paintings)
            {
                return false;
            }
            return true;
        }

        private void ItemSelect_RawItemSelected(object sender, IItem e)
        {
            var enable = false;
            if (e != null && e.GetRoot() != null) {
                enable = true;
            }

            ItemSelect.SelectButton.IsEnabled = enable;
        }

        private void FinalReviewButton_Click(object sender, RoutedEventArgs e)
        {
            if(FinalizeRequested != null)
            {
                FinalizeRequested.Invoke(this, null);
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            if (ItemSelected != null)
            {
                ItemSelected.Invoke(this, null);
            }
        }

        private void ItemSelect_ItemConfirmed(object sender, IItem item)
        {
            if(ItemSelected != null)
            {
                ItemSelected.Invoke(this, item);
            }
        }

        private void UpdateTotalFiles()
        {
            TotalFilesLabel.Content = _vm.TotalFileCount + " Total File(s)".L();
            if(_vm.TotalFileCount == 0)
            {
                FinalReviewButton.IsEnabled = false;
            } else
            {
                FinalReviewButton.IsEnabled = true;
            }
        }

        private void Control_RemoveEntry(object sender, StandardModpackItemEntry e)
        {
            _vm.Entries.Remove(e);
            UIElement target = null;
            foreach (var control in AddedItemsPanel.Children)
            {
                var isTarget = ((StandardModpackEntryControl)control).Entry == e;
                if (isTarget)
                {
                    target = (UIElement)control;
                    break;
                }
            }

            if (target != null)
            {
                AddedItemsPanel.Children.Remove(target);
                UpdateTotalFiles();
            }
        }

    }
}
