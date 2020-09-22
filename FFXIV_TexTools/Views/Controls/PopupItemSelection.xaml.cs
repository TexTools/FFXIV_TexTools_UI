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
using xivModdingFramework.Items.Interfaces;

namespace FFXIV_TexTools.Views.Controls
{
    /// <summary>
    /// Interaction logic for PopupItemSelection.xaml
    /// </summary>
    public partial class PopupItemSelection
    {
        public static PopupItemSelection CurrentPopup;
        public IItem SelectedItem;
        private Func<IItem, bool> _selectFunction;


        public static ItemSelectControl ItemSelect;

        // UI Locking Boilerplate
        #region UI Lock Boilerplate
        private ProgressDialogController _lockProgressController;
        private IProgress<string> _lockProgress;
        public async Task LockUi(string title, string message, object sender)
        {
            _lockProgressController = await this.ShowProgressAsync(title,
                                                                   message);

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
        #endregion

        public PopupItemSelection(Func<IItem, bool> ExtraFilterFunction = null, Func<IItem, bool> AllowSelectFunction = null)
        {
            InitializeComponent();
            CurrentPopup = this;

            if (ItemSelect == null)
            {
                ItemSelect = new ItemSelectControl();
                ItemSelect.RawItemSelected += ItemSelect_RawItemSelected;
                ItemSelect.ItemConfirmed += ItemSelect_ItemConfirmed;
                ItemSelect.LockUiFunction = LockUi;
                ItemSelect.UnlockUiFunction = UnlockUi;
                ItemSelect.ExpandCharacterMenu = true;
            }

            _selectFunction = AllowSelectFunction;
            ItemSelect.ExtraSearchFunction = ExtraFilterFunction;

            ItemSelect.ClearSelection();
            ItemSelect.DoFilter();


            Loaded += PopupItemSelection_Loaded;
            Unloaded += PopupItemSelection_Unloaded;
        }


        private void PopupItemSelection_Unloaded(object sender, RoutedEventArgs e)
        {
            Content = null;
            ItemSelect.LockUiFunction = null;
            ItemSelect.UnlockUiFunction = null;
        }

        private void PopupItemSelection_Loaded(object sender, RoutedEventArgs e)
        {
            Content = ItemSelect;

            // Believe it or not, NaN is the correct way to set auto width/height.
            ItemSelect.Width = Double.NaN;
            ItemSelect.Height = Double.NaN;
            ItemSelect.LockUiFunction = LockUi;
            ItemSelect.UnlockUiFunction = UnlockUi;
        }

        private static void ItemSelect_ItemConfirmed(object sender, IItem e)
        {
            CurrentPopup.SelectedItem = e;
            CurrentPopup.DialogResult = true;
        }

        private static void ItemSelect_RawItemSelected(object sender, IItem e)
        {
            if (CurrentPopup._selectFunction != null)
            {
                var result = CurrentPopup._selectFunction(e);
                ItemSelect.SelectButton.IsEnabled = result;

                if (result)
                {
                    ItemSelect.SelectButton.Content = "Select Item";
                } else
                {
                    ItemSelect.SelectButton.Content = "Invalid Selection";
                }
            }
        }

        public static IItem ShowItemSelection(Func<IItem, bool> ExtraFilterFunction = null, Func<IItem, bool> AllowSelectFunction = null)
        {
            var wind = new PopupItemSelection(ExtraFilterFunction, AllowSelectFunction);
            var result = wind.ShowDialog();
            if (result != true) return null;

            return wind.SelectedItem;
        }
    }
}
