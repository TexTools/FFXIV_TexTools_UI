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

namespace FFXIV_TexTools.Views.Item
{
    /// <summary>
    /// Interaction logic for SimpleItemViewWindow.xaml
    /// </summary>
    public partial class SimpleItemViewWindow
    {
        public static List<SimpleItemViewWindow> OpenItemWindows = new List<SimpleItemViewWindow>();
        public bool _IgnoreUnsaved = false;
        public SimpleItemViewWindow()
        {
            InitializeComponent();
            Closing += SimpleItemViewWindow_Closing;
        }

        private void SimpleItemViewWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (_IgnoreUnsaved)
            {
                OpenItemWindows.Remove(this);
                return;
            }

            var res = ItemView.HandleUnsaveConfirmation(null, null);

            if(res == false)
            {
                e.Cancel = true;
                return;
            }

            ItemView.Dispose();

            OpenItemWindows.Remove(this);
            if (null != Owner)
            {
                Owner.Activate();
            }
        }

        public async Task<bool> SetItem(IItem item)
        {
            return await ItemView.SetItem(item);
        }

        public static async Task<bool> ShowItem(IItem item, Window owner = null)
        {
            if(owner == null)
            {
                owner = MainWindow.GetMainWindow();
            }
            var wind = new SimpleItemViewWindow();
            wind.Owner = owner;
            wind.WindowStartupLocation = WindowStartupLocation.CenterOwner;

            OpenItemWindows.Add(wind);
            wind.Show();

            return await wind.SetItem(item);
        }
    }
}
