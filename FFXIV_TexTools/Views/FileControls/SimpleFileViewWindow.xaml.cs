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
using xivModdingFramework.Cache;
using xivModdingFramework.Items.Interfaces;

namespace FFXIV_TexTools.Views.Controls
{
    /// <summary>
    /// Interaction logic for SimpleFileViewWindow.xaml
    /// </summary>
    public partial class SimpleFileViewWindow
    {
        public static List<SimpleFileViewWindow> OpenFileWindows = new List<SimpleFileViewWindow>();
        public bool _IgnoreUnsaved = false;
        public SimpleFileViewWindow()
        {
            InitializeComponent();

            Closing += SimpleFileViewWindow_Closing;
        }

        private void SimpleFileViewWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (_IgnoreUnsaved)
            {
                OpenFileWindows.Remove(this);
                return;
            }

            if(FileWrapper != null && FileWrapper.FileControl != null &&  FileWrapper.FileControl.UnsavedChanges && !string.IsNullOrWhiteSpace(FileWrapper.FileControl.InternalFilePath))
            {
                if (!FileWrapper.ConfirmDiscardChanges(FileWrapper.FileControl.InternalFilePath))
                {
                    e.Cancel = true;
                    return;
                }
            }
            OpenFileWindows.Remove(this);
        }

        public async Task<bool> LoadFile(string filePath, IItem referenceItem = null, byte[] data = null, Type forcedControlType = null)
        {
            return await FileWrapper.LoadInternalFile(filePath, referenceItem, data, true, forcedControlType);
        }


        public static async Task<bool> OpenFile(string filePath, IItem referenceItem = null, byte[] data = null, Type forcedControlType = null, Window owner = null)
        {
            if(owner == null)
            {
                owner = MainWindow.GetMainWindow();
            }
            var wind = new SimpleFileViewWindow();
            wind.Owner = owner;
            wind.WindowStartupLocation = WindowStartupLocation.CenterOwner;

            var success = await wind.LoadFile(filePath, referenceItem, data, forcedControlType);
            if (success)
            {
                OpenFileWindows.Add(wind);
                wind.Show();
            }
            return success;
        }

        private async void ChangeFile_Click(object sender, RoutedEventArgs e)
        {
            var file = await this.ShowInputAsync("New File Path", "Input a new FFXIV file path to view the file...");

            if (string.IsNullOrWhiteSpace(file))
            {
                return;
            }
            var tx = MainWindow.DefaultTransaction;
            try
            {

                if (!await tx.FileExists(file))
                {
                    var root = await XivCache.GetFirstRoot(file);
                    if (root != null && root.Info.GetRootFile() == file)
                    {
                        // This is a valid metadata file even if doesn't exist yet.
                    }
                    else
                    {
                        ViewHelpers.ShowError(FileWrapper, "File Not Found", "The given file does not currently exist:\n\n" + file);
                        return;
                    }
                }

                var success = await LoadFile(file);
                if (!success)
                {
                    ViewHelpers.ShowError(FileWrapper, "Unable to Display File", "Unable to load or display the file:\n\n" + file);
                }
            }
            catch(Exception ex)
            {
                ViewHelpers.ShowError(FileWrapper, "File Load Error", "An error occurred when trying to load the file:\n\n" + ex.Message);

            }
        }
    }
}
