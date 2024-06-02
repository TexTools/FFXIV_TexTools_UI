using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
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
using xivModdingFramework.Mods.FileTypes;

namespace FFXIV_TexTools.Views.Wizard.ManipulationEditors
{
    /// <summary>
    /// Interaction logic for RspManipulationEditor.xaml
    /// </summary>
    public partial class RspManipulationEditor : UserControl, INotifyPropertyChanged
    {
        PMPRspManipulationJson Manipulation;


        public event PropertyChangedEventHandler PropertyChanged;
        public PMPSubRace Race
        {
            get => Manipulation.SubRace;
            set
            {
                Manipulation.SubRace = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Race)));
            }
        }
        public PMPRspAttribute Attribute
        {
            get => Manipulation.Attribute;
            set
            {
                Manipulation.Attribute = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Attribute)));
            }
        }

        public float Value
        {
            get => Manipulation.Entry;
            set
            {
                Manipulation.Entry = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Value)));
            }
        }

        public ObservableCollection<KeyValuePair<string, PMPSubRace>> Races { get; set; } = ViewHelpers.GetEnumSource<PMPSubRace>();
        public ObservableCollection<KeyValuePair<string, PMPRspAttribute>> Attributes { get; set; } = ViewHelpers.GetEnumSource<PMPRspAttribute>();

        public RspManipulationEditor(PMPManipulationWrapperJson manipulation)
        {
            var wrapper = manipulation as PMPRspManipulationWrapperJson;
            Manipulation = wrapper.Manipulation;
            DataContext = this;
            InitializeComponent();
        }
    }
}
