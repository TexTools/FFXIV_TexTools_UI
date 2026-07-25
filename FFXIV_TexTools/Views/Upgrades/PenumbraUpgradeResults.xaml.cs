using FFXIV_TexTools.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
    /// Interaction logic for PenumbraUpgradeResults.xaml
    /// </summary>
    public partial class PenumbraUpgradeResults : Window
    {
        public ObservableCollection<string> Successful { get; set; } = new ObservableCollection<string>();
        public ObservableCollection<string> Failed { get; set; } = new ObservableCollection<string>();
        public ObservableCollection<string> Unchanged { get; set; } = new ObservableCollection<string>();

        public PenumbraUpgradeResults(PenumbraUpgradeStatus results)
        {
            DataContext = this;
            InitializeComponent();
            Closing += PenumbraUpgradeResults_Closing;

            foreach(var r in results.Upgrades)
            {
                if(r.Value == PenumbraUpgradeStatus.EUpgradeResult.Success)
                {
                    Successful.Add(r.Key);
                }
                else if (r.Value == PenumbraUpgradeStatus.EUpgradeResult.Failure)
                {
                    Failed.Add(r.Key);
                }
                else if (r.Value == PenumbraUpgradeStatus.EUpgradeResult.Unchanged)
                {
                    Unchanged.Add(r.Key);
                }
            }
        }

        private void PenumbraUpgradeResults_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            Owner?.Activate();
        }
    }
}
