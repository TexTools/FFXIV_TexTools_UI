using FFXIV_TexTools.Helpers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Forms;
using xivModdingFramework.Cache;
using xivModdingFramework.Helpers;
using xivModdingFramework.Models.FileTypes;
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

            var sd = new SaveFileDialog();
            if (ext.Length > 0)
            {
                ext = ext.Substring(1);

                sd.Filter = $"{ext.ToUpper()} Files (*.{ext})|*.{ext}";
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
                    FlexibleMessageBox.Show("File does not exist.\n\nFile: " + path, "File Not Found", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                var type = _dat.GetFileType(offset, df);
                if (type < 2 || type > 4)
                {
                    throw new InvalidDataException("Invalid or Unknown Data Type.");
                }

                var size = await _dat.GetCompressedFileSize(offset, df);

                if (type == 2)
                {
                    if (DecompressType2Box.IsChecked == true)
                    {
                        data = await _dat.GetType2Data(offset, df);
                    } else
                    {
                        data = _dat.GetRawData(offset, df, size);
                    }
                }
                if (type == 3)
                {
                    data = _dat.GetRawData(offset, df, size);
                }
                if (type == 4)
                {
                    data = _dat.GetRawData(offset, df, size);
                }


                using (var stream = new BinaryWriter(sd.OpenFile()))
                {
                    stream.Write(data);
                }

                FlexibleMessageBox.Show("Raw file extracted successfully to path:\n" + sd.FileName, "Extraction Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                this.Close();

            } catch(Exception Ex)
            {
                FlexibleMessageBox.Show("Unable to decompress or read file:\n" + path + "\n\nError: " + Ex.Message, "File Not Found", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
        }
    }
}
