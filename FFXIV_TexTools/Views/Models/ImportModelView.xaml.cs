using FFXIV_TexTools.Properties;
using FFXIV_TexTools.ViewModels;
using HelixToolkit.Wpf.SharpDX.Elements2D;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
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
using System.Windows.Threading;
using xivModdingFramework.Cache;
using xivModdingFramework.General.Enums;
using xivModdingFramework.Helpers;
using xivModdingFramework.Items;
using xivModdingFramework.Items.Interfaces;
using xivModdingFramework.Models.DataContainers;
using xivModdingFramework.Models.FileTypes;
using xivModdingFramework.Models.Helpers;
using xivModdingFramework.Mods;

namespace FFXIV_TexTools.Views.Models
{

    public class ModelImportResult {
        public bool Success;
        public string Path;
        public byte[] Data;
        public TTModel Model;
        public ModelImportOptions ImportOptions;
    }
    /// <summary>
    /// Interaction logic for ImportModelView.xaml
    /// </summary>
    public partial class ImportModelView
    {
        private ImportModelViewModel _viewModel;
        private byte[] _data;

        // Height to expand to when opening the log window.

        public ImportModelView(string internalPath, IItem referenceItem, Action<ModelImportResult> onComplete = null, string startingFilePath = null, bool simpleMode = false, bool clearEmptyMaterials = false)
        {
            InitializeComponent();
            _viewModel = new ImportModelViewModel(this, internalPath, referenceItem, onComplete, startingFilePath, simpleMode, clearEmptyMaterials);
            DataContext = _viewModel;
            Closing += ImportModelView_Closing;
        }

        private void ImportModelView_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (null != Owner)
            {
                Owner.Activate();
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            var wind = Window.GetWindow(this);
            wind.Close();
        }

        /// <summary>
        /// Enables just the close buttons.
        /// </summary>
        public void EnableClose()
        {
            ImportButton.IsEnabled = true;
            ImportButton.Content = "Close".L();
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
        public byte[] GetData()
        {
            return _data;
        }


        public static async Task<ModelImportResult> ImportModel(string path, IItem referenceItem = null, string startingFilePath = null, bool simpleMode = false, Window windowOwner = null, bool clearEmptyMaterials = false)
        {

            if (windowOwner == null)
            {
                // Default to the main root window if we don't have an owner.
                windowOwner = MainWindow.GetMainWindow();
            }
            if (referenceItem == null)
            {
                var root = await XivCache.GetFirstRoot(path);
                if(root != null)
                {
                    referenceItem = root.GetFirstItem();
                }
            }

            var imView = new ImportModelView(path, referenceItem, OnComplete, startingFilePath, simpleMode, clearEmptyMaterials) { Owner = windowOwner };
            imView.WindowStartupLocation = WindowStartupLocation.CenterOwner;


            imView.Show();

            // OnComplete will be called here when either the import is successful or the user closes the window.
            _Result = null;
            while(_Result == null)
            {
                await Task.Delay(10);
            }

            return _Result;
        }

        private static ModelImportResult _Result;
        private static void OnComplete(ModelImportResult result)
        {
            _Result = result;
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
            UseImportedTangentButton.IsEnabled = enabled;
            UseOriginalShapeDataButton.IsEnabled = enabled;
            ShiftUVsButton.IsEnabled = enabled;
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
