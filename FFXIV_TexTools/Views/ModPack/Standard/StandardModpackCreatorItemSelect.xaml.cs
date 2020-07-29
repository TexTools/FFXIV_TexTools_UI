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
        private ObservableCollection<StandardModpackEntryControl> EntryControls = new ObservableCollection<StandardModpackEntryControl>();

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

        private void ItemSelect_ItemConfirmed(object sender, EventArgs e)
        {
            if(ItemSelected != null)
            {
                ItemSelected.Invoke(this, ItemSelect.SelectedItem);
            }
        }
    }
}
