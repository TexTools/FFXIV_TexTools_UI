using FFXIV_TexTools.Properties;
using FFXIV_TexTools.ViewModels;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using xivModdingFramework.General.Enums;
using xivModdingFramework.Helpers;
using xivModdingFramework.Items;
using xivModdingFramework.Items.Interfaces;
using xivModdingFramework.Models.DataContainers;
using xivModdingFramework.Models.FileTypes;

namespace FFXIV_TexTools.Views.Models
{
    /// <summary>
    /// Interaction logic for ImportModelView.xaml
    /// </summary>
    public partial class ImportModelView
    {
        private ImportModelViewModel _viewModel;
        private static byte[] _data;

        // Height to expand to when opening the log window.

        public ImportModelView(IItemModel item, XivRace race, Action onComplete, string submeshId = null, bool dataOnly = false)
        {
            InitializeComponent();
            _viewModel = new ImportModelViewModel(this, item, race, submeshId, dataOnly, onComplete);
            DataContext = _viewModel;
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }

        /// <summary>
        /// Enables just the close buttons.
        /// </summary>
        public void EnableClose()
        {
            ImportButton.IsEnabled = true;
            ImportButton.Content = "Close";
            ImportButton.Click += Close_Click;
            IsCloseButtonEnabled = true;
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        /// <summary>
        /// Sets the static level Data accessor.
        /// </summary>
        /// <param name="data"></param>
        public void SetData(byte[] data)
        {
            _data = data;
        }

        /// <summary>
        /// Returns the data from the last call of ImportModel()
        /// </summary>
        /// <returns></returns>
        public static byte[] GetData()
        {
            return _data;
        }

        /// <summary>
        /// Spawns the import model dialog and walks through the full steps to import a model,
        /// with user interaction.
        /// </summary>
        /// <param name="item">Item to import to</param>
        /// <param name="race">Race to import to</param>
        /// <param name="windowOwner">Window parent, default uses TexTools main window.</param>
        /// <param name="onComplete">Function to be called after import completes, but before user has closed the window. (Task handler returns when window is closed)</param>
        /// <param name="dataOnly">If this should be just load the data to memory and not import the resultant MDL.  Data can be accessed with ImportModelView.GetData()</param>
        /// <returns></returns>
        public static async Task<bool> ImportModel(IItemModel item, XivRace race, string submeshId = null, Window windowOwner = null, Action onComplete = null, bool dataOnly = false)
        {

            if (windowOwner == null)
            {
                // Default to the main root window if we don't have an owner.
                windowOwner = MainWindow.GetMainWindow();
            }

            var imView = new ImportModelView(item, race, onComplete, submeshId, dataOnly) { Owner = windowOwner };

            // This blocks until the dialog closes.
            var result = imView.ShowDialog();

            // Coalesce
            bool ret = result == true ? true : false;

            return ret;
        }


        public void EnableAll(bool enabled)
        {
            CancelButton.IsEnabled = enabled;
            ImportButton.IsEnabled = enabled;
            EditButton.IsEnabled = enabled;
            ClearUV2Button.IsEnabled = enabled;
            CloneUV1Button.IsEnabled = enabled;
            ClearVColorButton.IsEnabled = enabled;
            ClearVAlphaButton.IsEnabled = enabled;
            EnableShapeDataButton.IsEnabled = enabled;
            ForceUVsButton.IsEnabled = enabled;
            FileNameTextBox.IsEnabled = enabled;
            UseExistingButton.IsEnabled = enabled;
            IsCloseButtonEnabled = enabled;
            SelectFileButton.IsEnabled = enabled;
        }

        private void UseExistingButton_Click(object sender, RoutedEventArgs e)
        {
            FileNameTextBox.Text = "";
        }
    }
}
