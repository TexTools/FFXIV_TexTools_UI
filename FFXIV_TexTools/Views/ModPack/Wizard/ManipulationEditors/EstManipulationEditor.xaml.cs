using FFXIV_TexTools.Views.Controls;
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
using xivModdingFramework.General.Enums;
using xivModdingFramework.Items;
using xivModdingFramework.Items.Enums;
using xivModdingFramework.Items.Interfaces;
using xivModdingFramework.Mods.FileTypes;
using xivModdingFramework.Mods.FileTypes.PMP;

namespace FFXIV_TexTools.Views.Wizard.ManipulationEditors
{
    /// <summary>
    /// Interaction logic for EstManipulationEditor.xaml
    /// </summary>
    public partial class EstManipulationEditor : UserControl, INotifyPropertyChanged
    {


        PMPEstManipulationJson Manipulation;

        public event PropertyChangedEventHandler PropertyChanged;

        public XivDependencyRoot Root
        {
            get => Manipulation.GetRoot();
            set
            {
                if (value == null) return;
                var id = PmpIdentifierJson.FromRoot(value.Info);
                Manipulation.Slot = id.EquipSlot;

                if (Root.Info.PrimaryType != XivItemType.human)
                {
                    Manipulation.SetId = (ushort)value.Info.PrimaryId;
                } else
                {
                    Manipulation.SetId = (ushort)value.Info.SecondaryId;
                }
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Root)));
            }
        }

        public PMPGender Gender
        {
            get => Manipulation.Gender;
            set
            {
                Manipulation.Gender = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Gender)));
            }
        }

        public PMPModelRace Race
        {
            get => Manipulation.Race;
            set
            {
                Manipulation.Race = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Race)));
            }
        }

        public ushort SkeletonId
        {
            get {
                return Manipulation.Entry;
            }
            set
            {
                Manipulation.Entry = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SkeletonId)));
            }
        }

        public ObservableCollection<KeyValuePair<string, PMPGender>> Genders { get; set; } = ViewHelpers.GetEnumSource<PMPGender>();
        public ObservableCollection<KeyValuePair<string, PMPModelRace>> Races { get; set; } = ViewHelpers.GetEnumSource<PMPModelRace>();

        public EstManipulationEditor(PMPManipulationWrapperJson manipulation)
        {
            var estWrapper = manipulation as PMPEstManipulationWrapperJson;
            Manipulation = estWrapper.Manipulation;
            DataContext = this;
            InitializeComponent();
            RootControl.ItemFilter = ItemFilterFunc;
            RootControl.ItemSelect = ItemSelectFunc;
        }

        private bool ItemSelectFunc(IItem item)
        {
            var type = item.GetPrimaryItemType();
            var secondary = item.GetSecondaryItemType();
            if (type != XivItemType.equipment
                && secondary != XivItemType.hair
                && secondary != XivItemType.face) return false;
            return true;
        }

        private bool ItemFilterFunc(IItem item)
        {
            var type = item.GetPrimaryItemType();
            var secondary = item.GetSecondaryItemType();
            if (type != XivItemType.equipment
                && secondary != XivItemType.hair
                && secondary != XivItemType.face) return false;
            return true;
        }
    }
}
