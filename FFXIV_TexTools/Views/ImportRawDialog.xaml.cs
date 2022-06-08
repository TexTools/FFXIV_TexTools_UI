using FFXIV_TexTools.Helpers;
using FFXIV_TexTools.Resources;
using System;
using System.CodeDom;
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
using xivModdingFramework.Cache;
using xivModdingFramework.Items.DataContainers;
using xivModdingFramework.SqPack.FileTypes;

using Index = xivModdingFramework.SqPack.FileTypes.Index;

namespace FFXIV_TexTools.Views
{
    /// <summary>
    /// Interaction logic for ImportRawDialog.xaml
    /// </summary>
    public partial class ImportRawDialog : Window
    {
        public ImportRawDialog()
        {
            InitializeComponent();
        }

        private async void ImportButton_Click(object sender, RoutedEventArgs e)
        {
            var path = PathBox.Text;
            if (String.IsNullOrWhiteSpace(path)) return;

            var od = new OpenFileDialog();
            var result = od.ShowDialog();

            if (result != System.Windows.Forms.DialogResult.OK) return;

            byte[] data = null;

            var _dat = new Dat(XivCache.GameInfo.GameDirectory);
            var _index = new Index(XivCache.GameInfo.GameDirectory);

            if (DecompressedType2.IsChecked == true)
            {
                var temp = File.ReadAllBytes(od.FileName);
                data = await _dat.CreateType2Data(temp);
            } else
            {
                data = File.ReadAllBytes(od.FileName);
            }

            var type = BitConverter.ToInt32(data, 4);
            if (type < 2 || type > 4)
            {
                FlexibleMessageBox.Show("Invalid Data Type.\nGeneric binary files should be imported as decompressed type 2 Data.".L(), "Data Type Error".L(), MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            try
            {
                var dummyItem = new XivGenericItemModel();
                dummyItem.Name = Path.GetFileName(path);
                dummyItem.SecondaryCategory = "Raw File Imports";
                await _dat.WriteModFile(data, path, XivStrings.TexTools, dummyItem);
            }
            catch(Exception Ex)
            {
                FlexibleMessageBox.Show("Unable to import file.\n\nError: ".L() + Ex.Message, "Import Error".L(), MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;

            }

            FlexibleMessageBox.Show("File Imported Successfully.".L(), "Import Success".L(), MessageBoxButtons.OK, MessageBoxIcon.Information);
            this.Close();
        }
    }
}
