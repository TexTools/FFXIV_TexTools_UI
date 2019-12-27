using FFXIV_TexTools.Properties;
using FFXIV_TexTools.Resources;
using FFXIV_TexTools.ViewModels;
using MahApps.Metro.Controls.Dialogs;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Forms;
using xivModdingFramework.Items.Interfaces;
using xivModdingFramework.Mods.DataContainers;
using Image = SixLabors.ImageSharp.Image;

namespace FFXIV_TexTools.Views
{
    /// <summary>
    /// ModConverterView.xaml 的交互逻辑
    /// </summary>
    public partial class ModConverterView
    {
        ProgressDialogController _progressController;
        public ModConverterView(List<IItem> itemList,string ttmpPath, (ModPackJson ModPackJson, Dictionary<string, Image> ImageDictionary) ttmpData)
        {
            var vm = new ModConverterViewModel(ttmpData);
            vm.ItemList = itemList;
            vm.TTMPPath = ttmpPath;
            vm.GetNewModPackPath = () => {
                var modPackDirectory = new DirectoryInfo(Settings.Default.ModPack_Directory);
                var sfd = new SaveFileDialog { InitialDirectory = modPackDirectory.FullName, Filter = "TexToolsModPack TTMP (*.ttmp;*.ttmp2)|*.ttmp;*.ttmp2" };
                if (sfd.ShowDialog() != System.Windows.Forms.DialogResult.OK)
                    return null;
                return sfd.FileName;
            };
            vm.ShowProgress = async () =>
            {
                _progressController = await this.ShowProgressAsync(UIStrings.Mod_Converter, UIMessages.PleaseStandByMessage);
            };
            vm.CloseProgress = async ()=>
            {
                await _progressController.CloseAsync();
            };
            vm.Close = () =>
            {
                this.Close();
            };
            vm.ReportProgress = ReportProgress;
            this.DataContext = vm;
            InitializeComponent();
        }
        /// <summary>
        /// Updates the progress bar
        /// </summary>
        /// <param name="value">The progress value</param>
        private async Task ReportProgress((int current, int total, string message) report)
        {
            await Task.Run(() => {
                //Dispatcher.InvokeAsync(() => { });
                if (!report.message.Equals(string.Empty))
                {
                    _progressController.SetMessage(report.message);
                    _progressController.SetIndeterminate();
                }
                else
                {
                    _progressController.SetMessage(
                        $"{UIMessages.PleaseStandByMessage} ({report.current} / {report.total})");

                    var value = (double)report.current / (double)report.total;
                    _progressController.SetProgress(value);
                }
            });
        }

        private void FromItemListComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            (this.DataContext as ModConverterViewModel).SetToConverterItemListCommand.Execute(null);
        }
    }
}
