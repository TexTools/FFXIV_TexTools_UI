using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
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
using xivModdingFramework.Items;
using xivModdingFramework.Items.Enums;
using xivModdingFramework.Items.Interfaces;
using xivModdingFramework.Mods.FileTypes;
using xivModdingFramework.Mods.FileTypes.PMP;

namespace FFXIV_TexTools.Views.Wizard.ManipulationEditors
{
    /// <summary>
    /// Interaction logic for GmpManipulationEditor.xaml
    /// </summary>
    public partial class GmpManipulationEditor : UserControl, INotifyPropertyChanged
    {
        PMPGmpManipulationJson Manipulation;
        public event PropertyChangedEventHandler PropertyChanged;


        public bool GmpEnabled
        {
            get => Manipulation.Entry.Enabled;
            set
            {
                Manipulation.Entry.Enabled = value;
                UpdateValue();
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(GmpEnabled)));
            }
        }
        public bool Animated
        {
            get => Manipulation.Entry.Animated;
            set
            {
                Manipulation.Entry.Animated = value;
                UpdateValue();
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Animated)));
            }
        }
        public ushort RotationA
        {
            get => Manipulation.Entry.RotationA;
            set
            {
                Manipulation.Entry.RotationA = value;
                UpdateValue();
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(RotationA)));
            }
        }
        public ushort RotationB
        {
            get => Manipulation.Entry.RotationB;
            set
            {
                Manipulation.Entry.RotationB = value;
                UpdateValue();
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(RotationB)));
            }
        }
        public ushort RotationC
        {
            get => Manipulation.Entry.RotationC;
            set
            {
                Manipulation.Entry.RotationC = value;
                UpdateValue();
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(RotationC)));
            }
        }
        public byte UnknownA
        {
            get => Manipulation.Entry.UnknownA;
            set
            {
                Manipulation.Entry.UnknownA = value;
                UpdateValue();
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(UnknownA)));
            }
        }
        public byte UnknownB
        {
            get => Manipulation.Entry.UnknownB;
            set
            {
                Manipulation.Entry.UnknownB = value;
                UpdateValue();
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(UnknownB)));
            }
        }

        public XivDependencyRoot Root
        {
            get => Manipulation.GetRoot();
            set
            {
                if (value == null) return;
                var id = PmpIdentifierJson.FromRoot(value.Info);
                Manipulation.SetId = (ushort)id.PrimaryId;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Root)));
            }
        }
        public GmpManipulationEditor(PMPManipulationWrapperJson manipulation)
        {
            DataContext = this;
            var wrapper = manipulation as PMPGmpManipulationWrapperJson;
            Manipulation = wrapper.Manipulation;
            InitializeComponent();
            RootControl.ItemFilter = ItemFilterFunc;
            RootControl.ItemSelect = ItemSelectFunc;

        }


        private void UpdateValue()
        {
            var gmp = Manipulation.ToGmp();
            Manipulation.Entry.Value = (ulong) gmp.ToLong();
            Manipulation.Entry.UnknownTotal = gmp.Byte4;
        }
        private bool ItemSelectFunc(IItem item)
        {
            var type = item.GetPrimaryItemType();
            if (type != XivItemType.equipment) return false;
            if (item.GetItemSlotAbbreviation() != "met") return false;
            return true;
        }

        private bool ItemFilterFunc(IItem item)
        {
            var type = item.GetPrimaryItemType();
            if (type != XivItemType.equipment) return false;
            if (item.GetItemSlotAbbreviation() != "met") return false;
            return true;
        }

        private static readonly Regex _nonNumericRegex = new Regex("[^0-9]");
        private void ValidateNumericInput(object sender, TextCompositionEventArgs e)
        {
            if (_nonNumericRegex.IsMatch(e.Text))
            {
                e.Handled = true;
            }
        }
    }
}
