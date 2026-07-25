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

namespace FFXIV_TexTools.Views.Item
{
    /// <summary>
    /// Interaction logic for SimpleItemViewWindow.xaml
    /// </summary>
    public partial class SimpleItemViewWindow
    {
        public static List<SimpleItemViewWindow> OpenItemWindows = new List<SimpleItemViewWindow>();
        public bool _IgnoreUnsaved = false;
        public SimpleItemViewWindow()
        {
            InitializeComponent();
            Closing += SimpleItemViewWindow_Closing;
        }

        private void SimpleItemViewWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (_IgnoreUnsaved)
            {
                OpenItemWindows.Remove(this);
                return;
            }

            var res = ItemView.HandleUnsaveConfirmation(null, null);

            if(res == false)
            {
                e.Cancel = true;
                return;
            }

            ItemView.Dispose();

            OpenItemWindows.Remove(this);
            if (null != Owner)
            {
                Owner.Activate();
            }
        }

        public async Task<bool> SetItem(IItem item)
        {
            return await ItemView.SetItem(item);
        }

        public static async Task<bool> ShowItem(IItem item, Window owner = null)
        {
            if(owner == null)
            {
                owner = MainWindow.GetMainWindow();
            }
            var wind = new SimpleItemViewWindow();
            wind.Owner = owner;
            wind.WindowStartupLocation = WindowStartupLocation.CenterOwner;

            OpenItemWindows.Add(wind);
            wind.Show();

            return await wind.SetItem(item);
        }
        public static async Task<bool> ShowModel(string model = null, Window owner = null)
        {
            if (owner == null)
            {
                owner = MainWindow.GetMainWindow();
            }


            var wind = new SimpleItemViewWindow();
            wind.Owner = owner;
            wind.WindowStartupLocation = WindowStartupLocation.CenterOwner;

            OpenItemWindows.Add(wind);
            wind.Show();

            if (model == null)
            {
                await wind.ChooseFile();
            } else
            {
                var item = new SimpleItemModel(model);
                var success = await wind.SetItem(item);
                return success;
            }

            return true;
        }

        private async Task ChooseFile()
        {
            var file = await this.ShowInputAsync("Input Model File Path", "Input an FFXIV MDL file path to view the model...");

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
                        ViewHelpers.ShowError(this, "File Not Found", "The given file does not currently exist:\n\n" + file);
                        return;
                    }
                }

                var item = new SimpleItemModel(file);
                var success = await SetItem(item);
                if (!success)
                {
                    ViewHelpers.ShowError(this, "Unable to Display File", "Unable to load or display the file:\n\n" + file);
                }
            }
            catch (Exception ex)
            {
                ViewHelpers.ShowError(this, "File Load Error", "An error occurred when trying to load the file:\n\n" + ex.Message);

            }
        }
    }
}
