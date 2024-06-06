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
using xivModdingFramework.Models.DataContainers;
using xivModdingFramework.Mods.FileTypes;
using xivModdingFramework.Mods.FileTypes.PMP;

namespace FFXIV_TexTools.Views.Wizard.ManipulationEditors
{
    /// <summary>
    /// Interaction logic for EqpManipulationEditor.xaml
    /// </summary>
    public partial class EqpManipulationEditor : UserControl, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        PMPEqpManipulationJson Manipulation;

        private EquipmentParameter Eqp
        {
            get => Manipulation.ToEquipmentParameter();
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

        public EqpManipulationEditor(PMPManipulationWrapperJson manipulation)
        {
            var wrapper = manipulation as PMPEqpManipulationWrapperJson;
            Manipulation = wrapper.Manipulation;
            DataContext = this;
            InitializeComponent();
            RootControl.ItemFilter = ItemFilterFunc;
            RootControl.ItemSelect = ItemSelectFunc;


            var flags = Eqp.GetFlags();

            // Advanced Flag Setup.
            var idx = 0;
            foreach (var flag in flags)
            {
                var cb = new CheckBox();
                cb.Content = flag.Key.ToString().L();
                cb.DataContext = flag.Key;
                cb.IsChecked = flag.Value;

                cb.SetValue(Grid.RowProperty, idx / 4);
                cb.SetValue(Grid.ColumnProperty, idx % 4);

                cb.Checked += Cb_Checked;
                cb.Unchecked += Cb_Checked;

                RawGrid.Children.Add(cb);
                idx++;
            }
        }
        private void Cb_Checked(object sender, RoutedEventArgs e)
        {
            var cb = (CheckBox)sender;
            var enabled = cb.IsChecked == true ? true : false;
            var flag = (EquipmentParameterFlag)cb.DataContext;

            var eqp = Eqp;
            eqp.SetFlag(flag, enabled);

            var manip = PMPEqpManipulationJson.FromEqpEntry(eqp, Root.Info);
            Manipulation.Entry = manip.Entry;
        }


        private bool ItemSelectFunc(IItem item)
        {
            var type = item.GetPrimaryItemType();
            if (type != XivItemType.equipment) return false;
            return true;
        }

        private bool ItemFilterFunc(IItem item)
        {
            var type = item.GetPrimaryItemType();
            if (type != XivItemType.equipment) return false;
            return true;
        }
    }
}
