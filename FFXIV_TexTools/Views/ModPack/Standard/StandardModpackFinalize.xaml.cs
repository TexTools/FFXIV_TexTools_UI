using FFXIV_TexTools.Helpers;
using FFXIV_TexTools.Properties;
using FFXIV_TexTools.Resources;
using FFXIV_TexTools.ViewModels;
using MahApps.Metro.Behaviours;
using System;
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
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using xivModdingFramework.Helpers;
using xivModdingFramework.Mods.DataContainers;
using static xivModdingFramework.Mods.DataContainers.ModPackData;

namespace FFXIV_TexTools.Views
{
    /// <summary>
    /// Interaction logic for StandardModpackFinalize.xaml
    /// </summary>
    public partial class StandardModpackFinalize : Page
    {
        private StandardModpackViewModel _vm;

        public event EventHandler<StandardModpackViewModel> CreateModpack;

        string ImagePath;
        public StandardModpackFinalize(StandardModpackViewModel vm)
        {
            _vm = vm;
            InitializeComponent();

            var firstItem = _vm.Entries[0].Item;
            ModPackName.Text = firstItem.Name + " Modpack".L();
            ModPackAuthor.Text = String.IsNullOrWhiteSpace(Settings.Default.Default_Author) ? "TexTools User".L() : Settings.Default.Default_Author;
            ModPackUrl.Text = Settings.Default.Default_Modpack_Url;
            ModPackVersion.Text = "1.0.0";
            BackButton.Click += BackButton_Click;
            CreateModpackButton.Click += CreateModpackButton_Click;
        }

        private void CreateModpackButton_Click(object sender, RoutedEventArgs e)
        {


            var pathSafe = IOUtil.MakePathSafe(ModPackName.Text, false);

            var startingFolder = Path.GetFullPath(Settings.Default.ModPack_Directory);
            var sfd = new SaveFileDialog()
            {
                Filter = ViewHelpers.ModpackFileFilter,
                Title = "Save Modpack...",
                InitialDirectory = startingFolder,
                FileName = pathSafe + "." + Settings.Default.Default_Modpack_Format
            };

            if (sfd.ShowDialog() != System.Windows.Forms.DialogResult.OK)
            {
                return;
            }


            string verString = ModPackVersion.Text.Replace("_", "0");

            // Replace commas with periods for different culture formats such as FR
            if (verString.Contains(","))
            {
                verString = verString.Replace(",", ".");
            }

            Version versionNumber = Version.Parse(verString);


            if (!String.IsNullOrWhiteSpace(ModPackUrl.Text))
            {
                var url = IOUtil.ValidateUrl(ModPackUrl.Text);
                if (url != null)
                {
                    ModPackUrl.Text = url;
                }
                else
                {
                    ModPackUrl.Text = "";
                }
            } else
            {
                ModPackUrl.Text = "";
            }



            _vm.Author = ModPackAuthor.Text;
            _vm.Name = ModPackName.Text;
            _vm.Version = versionNumber;
            _vm.Description = ModPackDescription.Text;
            _vm.Url = ModPackUrl.Text;
            _vm.Image = ImagePath;
            _vm.ModpackPath = sfd.FileName;
            

            if (CreateModpack != null)
            {
                CreateModpack.Invoke(this, _vm);
            }
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            if (CreateModpack != null)
            {
                CreateModpack.Invoke(this, null);
            }
        }

        private void ChooseImage_Click(object sender, RoutedEventArgs e)
        {
            var imgInfo = this.LoadUserImage();
            ImagePath = imgInfo.File;
            ImageDisplay.Source = imgInfo.Image;
        }
    }
}
