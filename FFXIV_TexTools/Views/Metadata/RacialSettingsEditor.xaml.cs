using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Remoting.Contexts;
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
using xivModdingFramework.General.Enums;

namespace FFXIV_TexTools.Views.Metadata
{
    /// <summary>
    /// Interaction logic for RacialSettingsEditor.xaml
    /// </summary>
    public partial class RacialSettingsEditor : Window
    {
        private struct ButtonContext
        {
            public XivSubRace Race;
            public XivGender Gender;
        }

        public RacialSettingsEditor()
        {
            InitializeComponent();

            var races  = Enum.GetValues(typeof(XivSubRace)).Cast<XivSubRace>();

            var rowIdx = 0;
            foreach(var race in races)
            {
                var clanId = race.GetSubRaceId();
                if (clanId == 0)
                {
                    MaleGrid.RowDefinitions.Add(new RowDefinition() { Height = new GridLength(40) });
                    FemaleGrid.RowDefinitions.Add(new RowDefinition() { Height = new GridLength(40) });
                    rowIdx++;
                }

                var maleButton = MakeButton(race, XivGender.Male);
                maleButton.SetValue(Grid.RowProperty, rowIdx);
                maleButton.SetValue(Grid.ColumnProperty, clanId);
                MaleGrid.Children.Add(maleButton);

                var baseRace = race.GetBaseRace();
#if ENDWALKER
                if (baseRace != XivBaseRace.Hrothgar)
                {
                    var femaleButton = MakeButton(race, XivGender.Female);
                    femaleButton.SetValue(Grid.RowProperty, rowIdx);
                    femaleButton.SetValue(Grid.ColumnProperty, clanId);
                    FemaleGrid.Children.Add(femaleButton);
                }
#else
                var femaleButton = MakeButton(race, XivGender.Female);
                femaleButton.SetValue(Grid.RowProperty, rowIdx);
                femaleButton.SetValue(Grid.ColumnProperty, clanId);
                FemaleGrid.Children.Add(femaleButton);
#endif
            }
        }

        private Button MakeButton(XivSubRace race, XivGender gender)
        {
            var btn = new Button();
            var context = new ButtonContext() { Race = race, Gender = gender };
            btn.DataContext = context;

            var text = race.GetDisplayName();

            btn.Content = text;
            btn.Margin = new Thickness(5);

            btn.Click += Btn_Click;

            return btn;
        }

        private void Btn_Click(object sender, RoutedEventArgs e)
        {
            var context = ((ButtonContext)((Button)e.Source).DataContext);
            var wind = new RaceGenderScalingEditor(context.Race, context.Gender) { Owner = this };
            wind.WindowStartupLocation = WindowStartupLocation.CenterOwner;

            _ = wind.Init();
        }

        private void DoneButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
