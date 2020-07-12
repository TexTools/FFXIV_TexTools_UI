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

        // Height to expand to when opening the log window.

        public ImportModelView(IItemModel item, XivRace race, bool dataOnly = false)
        {
            InitializeComponent();
            _viewModel = new ImportModelViewModel(this, item, race, dataOnly);
            DataContext = _viewModel;
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        /// <summary>
        /// Spawns the import model dialog and walks through the full steps to import a model,
        /// with user interaction.
        /// </summary>
        /// <param name="item"></param>
        /// <param name="xivMdl"></param>
        /// <param name="windowOwner"></param>
        /// <returns></returns>
        public static async Task<bool> ImportModel(IItemModel item, XivRace race, Window windowOwner = null, bool dataOnly = false)
        {

            if (windowOwner == null)
            {
                // Default to the main root window if we don't have an owner.
                windowOwner = MainWindow.GetMainWindow();
            }

            var imView = new ImportModelView(item, race) { Owner = windowOwner };

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
            IsCloseButtonEnabled = enabled;
            SelectFileButton.IsEnabled = enabled;
        }
    }
}
