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
using System.Windows.Shapes;

namespace FFXIV_TexTools.Views.Upgrades
{
    /// <summary>
    /// Interaction logic for PenumbraLibraryUpgradeHelp.xaml
    /// </summary>
    public partial class PenumbraLibraryUpgradeHelp : Window
    {
        public PenumbraLibraryUpgradeHelp()
        {
            InitializeComponent();
            Closing += PenumbraLibraryUpgradeHelp_Closing;
        }

        private void PenumbraLibraryUpgradeHelp_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            Owner?.Activate();
        }
    }
}
