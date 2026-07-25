using System;
using System.Collections.Generic;
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
using xivModdingFramework.Items;
using xivModdingFramework.Items.Enums;
using xivModdingFramework.Items.Interfaces;
using xivModdingFramework.Mods.FileTypes;
using xivModdingFramework.Mods.FileTypes.PMP;
using xivModdingFramework.Variants.DataContainers;
using xivModdingFramework.Variants.FileTypes;

namespace FFXIV_TexTools.Views.Wizard.ManipulationEditors
{
    /// <summary>
    /// Interaction logic for ImcManipulationEditor.xaml
    /// </summary>
    public partial class ImcManipulationEditor : UserControl, INotifyPropertyChanged
    {
        PMPImcManipulationJson Manipulation;
        public event PropertyChangedEventHandler PropertyChanged;

        public XivImc ImcEntry
        {
            get
            {
                return Manipulation.Entry.ToXivImc();
            }
        }

        public uint Variant
        {
            get => Manipulation.Variant;
            set
            {
                Manipulation.Variant = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Variant)));
            }
        }



        public XivDependencyRoot Root
        {
            get => Manipulation.GetRoot();
            set
            {
                if (value == null) return;
                var id = PmpIdentifierJson.FromRoot(value.Info);

                Manipulation.ObjectType = PMPExtensions.XivItemTypeToPenumbraObject[value.Info.PrimaryType];
                Manipulation.EquipSlot = id.EquipSlot;
                Manipulation.BodySlot = id.BodySlot;
                Manipulation.PrimaryId = id.PrimaryId;
                Manipulation.SecondaryId = id.SecondaryId;

                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Root)));
            }
        }
        public ImcManipulationEditor(PMPManipulationWrapperJson manipulation)
        {
            var wrapper = manipulation as PMPImcManipulationWrapperJson;
            Manipulation = wrapper.Manipulation;
            DataContext = this;
            InitializeComponent();
            RootControl.ItemFilter = ItemFilterFunc;
            RootControl.ItemSelect = ItemSelectFunc;

            var xImc = Manipulation.Entry.ToXivImc();

            VariantEditor.ImcEntry = xImc;

            VariantEditor.ValueChanged += VariantEditor_ValueChanged;
            
        }

        private void VariantEditor_ValueChanged(object sender, XivImc e)
        {
            Manipulation.Entry = PMPImcManipulationJson.PMPImcEntry.FromXivImc(e);
        }

        private bool ItemSelectFunc(IItem item, XivDependencyRoot root)
        {
            var asIm = item as IItemModel;
            if (asIm == null) return false;
            if (!Imc.UsesImc(asIm)) return false;
            return true;
        }

        private bool ItemFilterFunc(IItem item, XivDependencyRoot root)
        {
            var asIm = item as IItemModel;
            if (asIm == null) return false;
            if (!Imc.UsesImc(asIm)) return false;
            return true;
        }
    }
}
