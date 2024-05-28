using FFXIV_TexTools.Helpers;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Media;
using xivModdingFramework.Cache;
using xivModdingFramework.Exd.FileTypes;
using xivModdingFramework.Helpers;
using xivModdingFramework.Models.FileTypes;
using xivModdingFramework.Mods;
using xivModdingFramework.SqPack.FileTypes;

namespace FFXIV_TexTools.Views
{
    /// <summary>
    /// Interaction logic for ExtractRawDialog.xaml
    /// </summary>
    public partial class ExtractRawDialog : Window
    {
        public ExtractRawDialog()
        {
            InitializeComponent();
            FromBox_TextChanged(null, null);
        }

        private async void ExtractButton_Click(object sender, RoutedEventArgs e)
        {
            var path = FromBox.Text.ToLower().Trim();
            if (!IOUtil.IsFFXIVInternalPath(path)) return;

            var ext = Path.GetExtension(path);
            byte[] data = null;

            var tx = MainWindow.DefaultTransaction;

            if(!await tx.FileExists(path))
            {
                FlexibleMessageBox.Show($"File does not exist:\n{path._()}".L(), "File Not Found".L(), MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }


            var sd = new SaveFileDialog();
            if (ext.Length > 0)
            {
                ext = ext.Substring(1);

                if (DecompressBox.IsChecked == false)
                {
                    // Add SQPack extension onto the end.
                    ext += ".sqpack";
                }

                sd.Filter = $"{ext.ToUpper()._()} Files (*.{ext._()})|*.{ext._()}".L();
            }

            sd.FileName = Path.GetFileName(path);

            sd.RestoreDirectory = true;
            if (sd.ShowDialog() != System.Windows.Forms.DialogResult.OK)
            {
                return;
            }

            try
            {
                var offset = await tx.GetRawDataOffset(path);
                var df = IOUtil.GetDataFileFromPath(path);
                if (offset <= 0)
                {
                    FlexibleMessageBox.Show("File does not exist.\n\nFile: ".L() + path, "File Not Found".L(), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                if (DecompressBox.IsChecked == true)
                {
                    data = await tx.ReadFile(path);
                } else
                {
                    data = await tx.ReadFile(path, false, true );
                }


                using (var stream = new BinaryWriter(sd.OpenFile()))
                {
                    stream.Write(data);
                }

                FlexibleMessageBox.Show("Raw file extracted successfully to path:\n".L() + sd.FileName, "Extraction Success".L(), MessageBoxButtons.OK, MessageBoxIcon.Information);
                this.Close();

            } catch(Exception Ex)
            {
                FlexibleMessageBox.Show($"Unable to decompress or read file:\n{path._()}\n\nError: ".L() + Ex.Message, "File Not Found".L(), MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
        }

        private async void FromBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {

            try
            {
                var text = FromBox.Text.ToLower().Trim();
                if (!IOUtil.IsFFXIVInternalPath(text))
                {
                    ExtractButton.IsEnabled = false;
                    InfoLabel.Foreground = Brushes.DarkRed;
                    InfoLabel.Content = "Path is not a valid FFXIV file path.";
                    return;
                }
                var tx = MainWindow.DefaultTransaction;

                var exists = await tx.FileExists(text);
                if (exists)
                {
                    ExtractButton.IsEnabled = true;
                    InfoLabel.Foreground = Brushes.DarkGreen;
                    InfoLabel.Content = "Valid path, and file exists.";
                    return;
                }
                else
                {
                    ExtractButton.IsEnabled = false;
                    InfoLabel.Foreground = Brushes.DarkRed;
                    InfoLabel.Content = "Valid path, but no file exists at the destination.";
                    return;
                }
            }
            catch (Exception ex)
            {
                Trace.WriteLine(ex);
            }

        }
    }
}
