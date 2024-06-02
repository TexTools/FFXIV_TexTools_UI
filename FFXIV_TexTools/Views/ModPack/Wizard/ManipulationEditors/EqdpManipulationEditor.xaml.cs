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
using xivModdingFramework.Cache;
using xivModdingFramework.Mods.FileTypes;
using xivModdingFramework.Mods.FileTypes.PMP;

namespace FFXIV_TexTools.Views.Wizard.ManipulationEditors
{
    /// <summary>
    /// Interaction logic for EqdpManipulationEditor.xaml
    /// </summary>
    public partial class EqdpManipulationEditor : UserControl, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        PMPEqdpManipulationJson Manipulation;
        public PMPModelRace Race
        {
            get => Manipulation.Race;
            set
            {
                Manipulation.Race = value;
                HasMaterial = false;
                HasModel = false;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Race)));
            }
        }
        public PMPGender Gender
        {
            get => Manipulation.Gender;
            set
            {
                Manipulation.Gender = value;
                HasMaterial = false;
                HasModel = false;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Gender)));
            }
        }
        public bool HasModel
        {
            get => (Manipulation.ShiftedEntry & 2) != 0;
            set
            {
                if (value)
                {

                    Manipulation.ShiftedEntry |= 2;
                }
                else
                {
                    var mask = ~2;
                    Manipulation.ShiftedEntry &= (ushort) mask;
                }
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HasModel)));
            }
        }
        public bool HasMaterial
        {
            get => (Manipulation.ShiftedEntry & 1) != 0;
            set
            {
                if (value)
                {

                    Manipulation.ShiftedEntry |= 1;
                }
                else
                {
                    var mask = ~1;
                    Manipulation.ShiftedEntry &= (ushort)mask;
                }
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HasMaterial)));
            }
        }



        public XivDependencyRoot Root
        {
            get => Manipulation.GetRoot();
            set
            {
                if (value == null) return;
                var id = PmpIdentifierJson.FromRoot(value.Info);
                Manipulation.Slot = id.EquipSlot;
                Manipulation.SetId = (ushort)id.PrimaryId;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Root)));
            }
        }

        public ObservableCollection<KeyValuePair<string, PMPModelRace>> Races { get; set; } = ViewHelpers.GetEnumSource<PMPModelRace>();
        public ObservableCollection<KeyValuePair<string, PMPGender>> Genders { get; set; } = ViewHelpers.GetEnumSource<PMPGender>();


        public EqdpManipulationEditor(PMPManipulationWrapperJson manipulation)
        {
            var wrapper = manipulation as PMPEqdpManipulationWrapperJson;
            DataContext = this;
            Manipulation = wrapper.Manipulation;
            InitializeComponent();
        }

    }
}
