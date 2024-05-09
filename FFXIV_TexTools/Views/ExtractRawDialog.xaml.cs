using FFXIV_TexTools.Helpers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Forms;
using xivModdingFramework.Cache;
using xivModdingFramework.Exd.FileTypes;
using xivModdingFramework.Helpers;
using xivModdingFramework.Models.FileTypes;
using xivModdingFramework.Mods;
using xivModdingFramework.SqPack.FileTypes;

using Index = xivModdingFramework.SqPack.FileTypes.Index;

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
        }

        private async void ExtractButton_Click(object sender, RoutedEventArgs e)
        {
            if (String.IsNullOrWhiteSpace(FromBox.Text)) return;

            var path = FromBox.Text;
            var ext = Path.GetExtension(path);
            var _dat = new Dat(XivCache.GameInfo.GameDirectory);
            var _index = new Index(XivCache.GameInfo.GameDirectory);
            byte[] data = null;

            var tx = ModTransaction.BeginTransaction(true);

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
                var offset = await _index.GetDataOffset(path);
                var df = IOUtil.GetDataFileFromPath(path);
                if (offset <= 0)
                {
                    FlexibleMessageBox.Show("File does not exist.\n\nFile: ".L() + path, "File Not Found".L(), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                if (DecompressBox.IsChecked == true)
                {
                    data = await _dat.GetUncompressedData(path);
                } else
                {
                    data = await _dat.GetCompressedData(path);
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
    }
}
