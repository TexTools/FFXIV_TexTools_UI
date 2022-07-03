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
    /// Interaction logic for ModpackItemLevelEntryControl.xaml
    /// </summary>
    public partial class StandardModpackEntryControl : UserControl
    {
        public readonly StandardModpackItemEntry Entry;

        public event EventHandler<StandardModpackItemEntry> RemoveEntry;

        public StandardModpackEntryControl(StandardModpackItemEntry entry)
        {
            Entry = entry;
            InitializeComponent();

            ItemNameLabel.Content = entry.Item.Name;
            if (entry.Level == xivModdingFramework.Cache.XivDependencyLevel.Root)
            {
                ItemLevelLabel.Content = $"Selected All Files - [{Entry.AllFiles.Count._()}] Total File(s)".L();
            } else
            {

                ItemLevelLabel.Content = $"Selected [{Entry.MainFiles.Count._()}] {StandardModpackCreator.GetNiceLevelName(entry.Level)._()} File(s) - [{Entry.AllFiles.Count._()}] Total File(s)".L();
            }

            RemoveButton.Click += RemoveButton_Click;
        }

        private void RemoveButton_Click(object sender, RoutedEventArgs e)
        {
            if (RemoveEntry != null)
            {
                RemoveEntry.Invoke(this, Entry);
            }
        }
    }
}
