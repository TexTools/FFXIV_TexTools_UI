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
using xivModdingFramework.General.Enums;
using xivModdingFramework.Models.DataContainers;
using xivModdingFramework.Models.FileTypes;
using xivModdingFramework.Mods.FileTypes;

namespace FFXIV_TexTools.Views.Metadata
{
    /// <summary>
    /// Interaction logic for EqdpControl.xaml
    /// </summary>
    public partial class EqdpControl : UserControl
    {
        private ItemMetadata _metadata;

        private Dictionary<XivRace, CheckBox> RacialCheckboxes = new Dictionary<XivRace, CheckBox>();

        public event Action FileChanged;

        public event EventHandler<(XivRace Race, bool Enabled)> RaceChanged;
        public EqdpControl()
        {
            InitializeComponent();

            var idx = 0;
            foreach(var race in Eqp.PlayableRaces)
            {
                var cb = new CheckBox();
                cb.Content = race.GetDisplayName();
                cb.DataContext = race;
                cb.Checked += Race_Checked;
                cb.Unchecked += Race_Checked;

                cb.SetValue(Grid.RowProperty, idx / 2);
                cb.SetValue(Grid.ColumnProperty, idx % 2);

                RacialGrid.Children.Add(cb);
                RacialCheckboxes.Add(race, cb);
                idx++;
            }
        }

        private void Race_Checked(object sender, RoutedEventArgs e)
        {
            var race = (XivRace)((CheckBox)sender).DataContext;
            var enabled = ((CheckBox)sender).IsChecked == true;

            if (race != XivRace.All_Races && _metadata != null)
            {
                _metadata.EqdpEntries[race].HasMaterial = enabled;
                _metadata.EqdpEntries[race].HasModel = enabled;

                if(RaceChanged != null)
                {
                    RaceChanged.Invoke(this, (race, enabled));
                }
            }
            FileChanged?.Invoke();

        }

        public async Task SetMetadata(ItemMetadata m)
        {
            _metadata = null;
            if(m.EqdpEntries.Count == 0)
            {
                _metadata = m;
                return;
            }

            foreach (var kv in RacialCheckboxes)
            {
                if (m.EqdpEntries.ContainsKey(kv.Key))
                {
                    var entry = m.EqdpEntries[kv.Key];
                    kv.Value.IsChecked = entry.HasModel;
                }
            }

            _metadata = m;
        }
    }
}
