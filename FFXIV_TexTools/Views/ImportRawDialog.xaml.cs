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
using xivModdingFramework.Mods;
using xivModdingFramework.SqPack.FileTypes;


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
            try
            {
                await SmartImport.Import(od.FileName, path, XivStrings.TexTools);
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
