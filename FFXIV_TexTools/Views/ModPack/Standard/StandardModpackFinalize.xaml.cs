using FFXIV_TexTools.Helpers;
using FFXIV_TexTools.Properties;
using FFXIV_TexTools.Resources;
using FFXIV_TexTools.ViewModels;
using System;
using System.Collections.Generic;
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
using System.Windows.Shapes;
using xivModdingFramework.Helpers;
using xivModdingFramework.Mods.DataContainers;

namespace FFXIV_TexTools.Views
{
    /// <summary>
    /// Interaction logic for StandardModpackFinalize.xaml
    /// </summary>
    public partial class StandardModpackFinalize : Page
    {
        private StandardModpackViewModel _vm;

        public event EventHandler<StandardModpackViewModel> CreateModpack;
        public StandardModpackFinalize(StandardModpackViewModel vm)
        {
            _vm = vm;
            InitializeComponent();

            var firstItem = _vm.Entries[0].Item;
            ModPackName.Text = firstItem.Name + " Modpack";
            ModPackAuthor.Text = String.IsNullOrWhiteSpace(Settings.Default.Default_Author) ? "TexTools User" : Settings.Default.Default_Author;
            ModPackUrl.Text = Settings.Default.Default_Modpack_Url;
            ModPackVersion.Text = "1.0.0";
            BackButton.Click += BackButton_Click;
            CreateModpackButton.Click += CreateModpackButton_Click;
        }

        private void CreateModpackButton_Click(object sender, RoutedEventArgs e)
        {
            if (ModPackName.Text.Equals(string.Empty))
            {
                if (FlexibleMessageBox.Show(null,
                        UIMessages.DefaultModPackNameMessage,
                        UIMessages.NoNameFoundTitle, MessageBoxButtons.OKCancel, MessageBoxIcon.Warning) ==
                    System.Windows.Forms.DialogResult.OK)
                {
                    ModPackName.Text = "ModPack";
                }
                else
                {
                    return;
                }
            }

            char[] invalidChars = { '/', '\\', ':', '*', '?', '"', '<', '>', '|' };

            if (ModPackName.Text.IndexOfAny(invalidChars) >= 0)
            {
                if (FlexibleMessageBox.Show(null,
                        UIMessages.InvalidCharacterModpackNameMessage,
                        UIMessages.InvalidCharacterModpackNameTitle, MessageBoxButtons.OK, MessageBoxIcon.Warning) ==
                    System.Windows.Forms.DialogResult.OK)
                {
                    return;
                }
            }

            string verString = ModPackVersion.Text.Replace("_", "0");

            // Replace commas with periods for different culture formats such as FR
            if (verString.Contains(","))
            {
                verString = verString.Replace(",", ".");
            }

            Version versionNumber = Version.Parse(verString);

            if (versionNumber.ToString().Equals("0.0.0"))
            {
                if (FlexibleMessageBox.Show(null,
                        UIMessages.DefaultModPackVersionMessage,
                        UIMessages.NoVersionFoundTitle, MessageBoxButtons.OKCancel, MessageBoxIcon.Warning) ==
                    System.Windows.Forms.DialogResult.OK)
                {
                    versionNumber = new Version(1, 0, 0);
                }
                else
                {
                    return;
                }
            }

            if (ModPackAuthor.Text.Equals(string.Empty))
            {
                if (FlexibleMessageBox.Show(null,
                        UIMessages.DefaultModPackAuthorMessage,
                        UIMessages.NoAuthorFoundTitle, MessageBoxButtons.OKCancel, MessageBoxIcon.Warning) ==
                    System.Windows.Forms.DialogResult.OK)
                {
                    ModPackAuthor.Text = "TexTools User";
                }
                else
                {
                    return;
                }
            }

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
            _vm.SaveAdvanced = ModPackType.SelectedIndex == 1;

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
    }
}
