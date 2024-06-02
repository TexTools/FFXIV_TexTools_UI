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



        public XivDependencyRoot Root
        {
            get => Manipulation.GetRoot();
            set
            {
                if (value == null) return;
                var id = PmpIdentifierJson.FromRoot(value.Info);
                
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
        }

        private bool ItemSelectFunc(IItem item)
        {
            var asIm = item as IItemModel;
            if (asIm == null) return false;
            if (!Imc.UsesImc(asIm)) return false;
            return true;
        }

        private bool ItemFilterFunc(IItem item)
        {
            var asIm = item as IItemModel;
            if (asIm == null) return false;
            if (!Imc.UsesImc(asIm)) return false;
            return true;
        }
    }
}
