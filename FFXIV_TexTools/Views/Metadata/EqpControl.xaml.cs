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
using xivModdingFramework.Models.DataContainers;
using xivModdingFramework.Mods.FileTypes;

namespace FFXIV_TexTools.Views.Metadata
{
    /// <summary>
    /// Interaction logic for EqpView.xaml
    /// </summary>
    public partial class EqpControl : UserControl
    {
        private ItemMetadata _metadata;
        private EquipmentParameter entry;
        public EqpControl()
        {
            InitializeComponent();
        }

        public async Task SetMetadata(ItemMetadata m)
        {
            _metadata = m;

            entry = _metadata.EqpEntry;

            RawGrid.Children.Clear();
            if (entry == null) return;

            var flags = entry.GetFlags();

            var idx = 0;
            foreach(var flag in flags)
            {
                var cb = new CheckBox();
                cb.Content = flag.Key.ToString();
                cb.DataContext = flag.Key;
                cb.IsChecked = flag.Value;

                cb.SetValue(Grid.RowProperty, idx / 4);
                cb.SetValue(Grid.ColumnProperty, idx % 4);

                cb.Checked += Cb_Checked;
                cb.Unchecked += Cb_Checked;

                RawGrid.Children.Add(cb);
                idx++;
            }




        }

        private void Cb_Checked(object sender, RoutedEventArgs e)
        {
            var cb = (CheckBox)sender;
            var enabled = cb.IsChecked == true ? true : false;
            var flag = (EquipmentParameterFlag)cb.DataContext;

            entry.SetFlag(flag, enabled);
        }
    }
}
