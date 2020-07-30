using FFXIV_TexTools.Properties;
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
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

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
            ModPackAuthor.Text = Settings.Default.Default_Author;
            ModPackVersion.Text = "1.0.0";

            BackButton.Click += BackButton_Click;
            CreateModpackButton.Click += CreateModpackButton_Click;
        }

        private void CreateModpackButton_Click(object sender, RoutedEventArgs e)
        {
            _vm.Author = ModPackAuthor.Text;
            _vm.Name = ModPackName.Text;
            _vm.Version = new Version(ModPackVersion.Text);
            if(CreateModpack != null)
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
