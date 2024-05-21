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

namespace FFXIV_TexTools.Views.Controls
{
    /// <summary>
    /// Interaction logic for SimpleFileViewWindow.xaml
    /// </summary>
    public partial class SimpleFileViewWindow
    {
        public SimpleFileViewWindow()
        {
            InitializeComponent();

        }

        public async Task<bool> LoadFile(string filePath)
        {
            return await FileWrapper.LoadInternalFile(filePath);
        }


        public static async Task<bool> OpenFile(string filePath, Window owner = null)
        {
            if(owner == null)
            {
                owner = MainWindow.GetMainWindow();
            }
            var wind = new SimpleFileViewWindow();
            wind.Owner = owner;
            wind.WindowStartupLocation = WindowStartupLocation.CenterOwner;

            var success = await wind.LoadFile(filePath);
            if (success)
            {
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
                    ViewHelpers.ShowError(FileWrapper, "File Not Found", "The given file does not currently exist:\n\n" + file);
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
