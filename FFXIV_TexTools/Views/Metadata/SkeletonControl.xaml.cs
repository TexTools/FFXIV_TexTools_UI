using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using xivModdingFramework.General.Enums;
using xivModdingFramework.Items.Enums;
using xivModdingFramework.Models.DataContainers;
using xivModdingFramework.Models.FileTypes;
using xivModdingFramework.Mods.FileTypes;

namespace FFXIV_TexTools.Views.Metadata
{
    /// <summary>
    /// Interaction logic for SkeletonControl.xaml
    /// </summary>
    public partial class SkeletonControl : UserControl
    {
        private ItemMetadata _metadata;
        private Dictionary<XivRace, ComboBox> RacialComboBoxes;
        private Dictionary<XivRace, ObservableCollection<KeyValuePair<int, string>>> RacialItemSources;


        private bool IsRaceEnabled(XivRace race)
        {
            if(_metadata.EqdpEntries.ContainsKey(race))
            {
                return _metadata.EqdpEntries[race].bit1;
            }
            return false;
        }

        private bool SingleRaceMode
        {
            get
            {
                if (_metadata == null) return false;

                return _metadata.EstEntries.Count == 1;
            }
        }
        public SkeletonControl()
        {
            InitializeComponent();
            RacialComboBoxes = new Dictionary<XivRace, ComboBox>();
            RacialItemSources = new Dictionary<XivRace, ObservableCollection<KeyValuePair<int, string>>>();

            MetadataView.CurrentView.EqdpView.RaceChanged += EqdpView_RaceChanged;

        }

        private void EqdpView_RaceChanged(object sender, (XivRace Race, bool Enabled) e)
        {
            if (RacialComboBoxes.ContainsKey(e.Race))
            {
                RacialComboBoxes[e.Race].IsEnabled = e.Enabled;
            }
        }

        private void SetupSingleRaceMenu()
        {
            RacialGrid.Children.Clear();
            RacialComboBoxes.Clear();
            RacialItemSources.Clear();


            var entry = _metadata.EstEntries.First().Value;
            var race = entry.Race;

            var label = new Label();
            label.Content = race.GetDisplayName() + ":";
            label.Width = 150;
            label.HorizontalAlignment = HorizontalAlignment.Right;
            label.HorizontalContentAlignment = HorizontalAlignment.Right;

            var cb = new ComboBox();
            cb.Width = 150;
            cb.SelectionChanged += RaceBoxChanged;

            label.SetValue(Grid.RowProperty, 0);
            cb.SetValue(Grid.RowProperty, 0);

            label.SetValue(Grid.ColumnProperty, 2);
            cb.SetValue(Grid.ColumnProperty, 3);

            var dict = new ObservableCollection<KeyValuePair<int, string>>();
            RacialItemSources.Add(race, dict);

            cb.IsEnabled = IsRaceEnabled(race);

            cb.DataContext = race;
            cb.ItemsSource = dict;
            cb.DisplayMemberPath = "Value";
            cb.SelectedValuePath = "Key";

            RacialComboBoxes.Add(race, cb);

            if(_metadata.Root.Info.PrimaryType == XivItemType.human && _metadata.Root.Info.PrimaryId == 0)
            {
                // Disable stuff here to ensure we can't accidentally set a 
                // skeleton on a null type.
                cb.IsEnabled = false;
            }


            RacialGrid.Children.Add(new Label());
            RacialGrid.Children.Add(label);
            RacialGrid.Children.Add(cb);
            RacialGrid.Children.Add(new Label());


            // Empty Row
            RacialGrid.Children.Add(new Label());
            RacialGrid.Children.Add(new Label());
            RacialGrid.Children.Add(new Label());
            RacialGrid.Children.Add(new Label());
        }
        private void SetupMultiRaceMenu()
        {
            RacialGrid.Children.Clear();
            RacialComboBoxes.Clear();
            RacialItemSources.Clear();

            {
                var label = new Label();
                label.Content = "Set All Together:";
                label.Width = 150;
                label.HorizontalAlignment = HorizontalAlignment.Right;
                label.HorizontalContentAlignment = HorizontalAlignment.Right;

                var cb = new ComboBox();
                cb.Width = 150;
                cb.ToolTip = "Note: Some skeletons are not available for some races.";
                cb.SelectionChanged += AllBoxChanged;

                label.SetValue(Grid.RowProperty, 0);
                cb.SetValue(Grid.RowProperty, 0);

                label.SetValue(Grid.ColumnProperty, 2);
                cb.SetValue(Grid.ColumnProperty, 3);

                var dict = new ObservableCollection<KeyValuePair<int, string>>();
                RacialItemSources.Add(XivRace.All_Races, dict);
                cb.ItemsSource = dict;
                cb.DisplayMemberPath = "Value";
                cb.SelectedValuePath = "Key";

                RacialComboBoxes.Add(XivRace.All_Races, cb);


                RacialGrid.Children.Add(new Label());
                RacialGrid.Children.Add(label);
                RacialGrid.Children.Add(cb);
                RacialGrid.Children.Add(new Label());

                // Empty Row
                RacialGrid.Children.Add(new Label());
                RacialGrid.Children.Add(new Label());
                RacialGrid.Children.Add(new Label());
                RacialGrid.Children.Add(new Label());
            }


            var idx = 0;
            foreach (var race in Eqp.PlayableRaces)
            {
                var label = new Label();
                label.Content = race.GetDisplayName() + ":";
                label.Width = 150;
                label.HorizontalAlignment = HorizontalAlignment.Right;
                label.HorizontalContentAlignment = HorizontalAlignment.Right;

                var cb = new ComboBox();
                cb.Width = 150;

                label.SetValue(Grid.RowProperty, (idx / 4) + 1);
                cb.SetValue(Grid.RowProperty, (idx / 4) + 1);

                label.SetValue(Grid.ColumnProperty, idx % 4);
                cb.SetValue(Grid.ColumnProperty, (idx % 4) + 1);

                var dict = new ObservableCollection<KeyValuePair<int, string>>();
                cb.DataContext = race;

                cb.IsEnabled = IsRaceEnabled(race);
                RacialItemSources.Add(race, dict);
                cb.ItemsSource = dict;
                cb.DisplayMemberPath = "Value";
                cb.SelectedValuePath = "Key";

                cb.SelectionChanged += RaceBoxChanged;

                RacialComboBoxes.Add(race, cb);

                RacialGrid.Children.Add(label);
                RacialGrid.Children.Add(cb);
                idx++;
            }
        }

        public async Task SetMetadata(ItemMetadata m)
        {
            _metadata = m;
            if (m.EstEntries.Count == 0)
            {
                return;
            }

            if(SingleRaceMode)
            {
                SetupSingleRaceMenu();
            } else
            {
                SetupMultiRaceMenu();
            }

            var allSkels = new SortedSet<int>();

            var type = Est.GetEstType(_metadata.Root);

            var options = await Est.GetAllExtraSkeletons(type);

            var prefix = Est.GetSystemPrefix(type);

            foreach (var kv in RacialComboBoxes)
            {
                var race = kv.Key;

                RacialItemSources[race].Clear();

                if (race == XivRace.All_Races) continue;
                if (!options.ContainsKey(race)) continue;

                var allEntries = options[race];

                RacialItemSources[race].Add(new KeyValuePair<int, string>(0, "None"));

                // Add all the entries.
                foreach (var skel in allEntries)
                {
                    allSkels.Add(skel);

                    var text = prefix + skel.ToString().PadLeft(4, '0');
                    var kv2 = new KeyValuePair<int, string>((int)skel, text);
                    RacialItemSources[race].Add(kv2);
                }



                var hasEntry = _metadata.EstEntries.ContainsKey(race);
                if(!hasEntry)
                {
                    _metadata.EstEntries[race] = new ExtraSkeletonEntry(race, (ushort)_metadata.Root.Info.PrimaryId, 0);
                }
                var entry = _metadata.EstEntries[race];

                if(!allEntries.Contains(entry.SkelId))
                {
                    entry.SkelId = 0;
                }

                var cb = RacialComboBoxes[race];
                cb.SelectedValue = (int)entry.SkelId;

            }

            if (!SingleRaceMode)
            {
                RacialItemSources[XivRace.All_Races].Add(new KeyValuePair<int, string>(-1, "--"));
                RacialItemSources[XivRace.All_Races].Add(new KeyValuePair<int, string>(0, "None"));

                RacialComboBoxes[XivRace.All_Races].SelectedIndex = 0;
                // Add all the entries.
                foreach (var skel in allSkels)
                {
                    var text = prefix + skel.ToString().PadLeft(4, '0');
                    var kv2 = new KeyValuePair<int, string>(skel, text);
                    RacialItemSources[XivRace.All_Races].Add(kv2);
                }
            }

            _metadata = m;
        }
        private void RaceBoxChanged(object sender, SelectionChangedEventArgs e)
        {
            var cb = (ComboBox)sender;
            if (cb.SelectedValue == null) return;

            var race = (XivRace)cb.DataContext;
            var skel = (int)cb.SelectedValue;

            _metadata.EstEntries[race].SkelId = (ushort)skel;
        }

        private void AllBoxChanged(object sender, SelectionChangedEventArgs e)
        {
            if (((ComboBox)sender).SelectedValue == null) return;
            var selection = (int)(((ComboBox)sender).SelectedValue);
            if (selection < 0) return;

            foreach (var kv in RacialComboBoxes)
            {
                var race = kv.Key;

                if (race == XivRace.All_Races) continue;

                var collection = RacialItemSources[race];

                if (collection.Any(x => x.Key == selection))
                {
                    kv.Value.SelectedValue = selection;
                }
                else
                {
                    kv.Value.SelectedValue = 0;
                }
            }
            ((ComboBox)sender).SelectedValue = -1;
        }

    }
}
