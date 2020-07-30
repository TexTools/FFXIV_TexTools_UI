using FFXIV_TexTools.ViewModels;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
using System.Windows.Navigation;
using System.Windows.Shapes;
using xivModdingFramework.Items.Interfaces;

namespace FFXIV_TexTools.Views
{
    /// <summary>
    /// Interaction logic for StandardModpackCreatorItemSelect.xaml
    /// </summary>
    public partial class StandardModpackCreatorItemSelect : Page
    {
        private StandardModpackViewModel _vm;

        public event EventHandler<IItem> ItemSelected;
        public event EventHandler FinalizeRequested;

        public StandardModpackCreatorItemSelect(StandardModpackViewModel vm)
        {
            _vm = vm;
            DataContext = _vm;

            InitializeComponent();

            ItemSelect.ItemConfirmed += ItemSelect_ItemConfirmed;
            CancelButton.Click += CancelButton_Click;
            FinalReviewButton.Click += FinalReviewButton_Click;


            foreach(var entry in vm.Entries)
            {
                var control = new StandardModpackEntryControl(entry);
                control.RemoveEntry += Control_RemoveEntry;
                AddedItemsPanel.Children.Add(control);
            }
            UpdateTotalFiles();
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
            TotalFilesLabel.Content = _vm.TotalFileCount + " Total File(s)";
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
