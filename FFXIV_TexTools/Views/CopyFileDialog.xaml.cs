using FFXIV_TexTools.Helpers;
using FFXIV_TexTools.Resources;
using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using xivModdingFramework.Cache;
using xivModdingFramework.SqPack.FileTypes;

using Index = xivModdingFramework.SqPack.FileTypes.Index;

namespace FFXIV_TexTools.Views
{
    /// <summary>
    /// Interaction logic for CopyFileDialog.xaml
    /// </summary>
    public partial class CopyFileDialog : Window
    {
        public CopyFileDialog()
        {
            InitializeComponent();
            this.WindowStartupLocation = WindowStartupLocation.CenterOwner;
        }

        private void CopyButton_Click(object sender, RoutedEventArgs e)
        {
            DoCopy();
        }

        private async Task DoCopy()
        {

            var from = FromBox.Text;
            var to = ToBox.Text;

            if (String.IsNullOrWhiteSpace(to) || String.IsNullOrWhiteSpace(to)) return;

            var _dat = new Dat(XivCache.GameInfo.GameDirectory);
            var _index = new Index(XivCache.GameInfo.GameDirectory);


            try
            {
                var exists = await _index.FileExists(from);
                if(!exists)
                {
                    throw new InvalidDataException("Source file does not exist.");
                }

                exists = await _index.FileExists(to);
                if (exists)
                {
                    var cancel = false;
                    Dispatcher.Invoke(() =>
                    {
                        var result = FlexibleMessageBox.Show("Destination file already exists.  Overwrite?", "Overwrite Confirmation", System.Windows.Forms.MessageBoxButtons.YesNo, System.Windows.Forms.MessageBoxIcon.Warning);
                        cancel = result == System.Windows.Forms.DialogResult.No;
                    });

                    if (cancel) return;
                }

                await _dat.CopyFile(from, to, XivStrings.TexTools, true);

                Dispatcher.Invoke(() =>
                {
                    FlexibleMessageBox.Show("File Copied Successfully.", "Copy Success", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Information);
                    Close();
                });
            } catch(Exception ex)
            {
                FlexibleMessageBox.Show("File Copy Failed:\n" + ex.Message, "Copy Failure", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Error);
            }
        }
    }
}
