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

namespace FFXIV_TexTools.Views.Wizard.ManipulationEditors
{
    /// <summary>
    /// Interaction logic for GmpManipulationEditor.xaml
    /// </summary>
    public partial class GmpManipulationEditor : UserControl, INotifyPropertyChanged
    {
        PMPGmpManipulationJson Manipulation;
        public event PropertyChangedEventHandler PropertyChanged;
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

    }
}
