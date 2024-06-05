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
using xivModdingFramework.Items.Interfaces;

namespace FFXIV_TexTools.Views.SharedItems
{
    /// <summary>
    /// Interaction logic for SharedItemsWindow.xaml
    /// </summary>
    public partial class SharedItemsWindow : Window
    {
        public SharedItemsWindow()
        {
            InitializeComponent();
            Closing += SharedItemsWindow_Closing;
        }

        private void SharedItemsWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (null != Owner)
            {
                Owner.Activate();
            }
        }

        public static async Task<bool> ShowSharedItems(IItem item, Window owner = null)
        {
            if(owner == null)
            {
                owner = MainWindow.GetMainWindow();
            }

            var wind = new SharedItemsWindow();
            wind.Owner = owner;
            wind.WindowStartupLocation = WindowStartupLocation.CenterOwner;

            var res = await wind.SharedItemsControl.SetItem(item);
            if (res)
            {
                wind.Show();
            }
            return res;
        }
    }
}
