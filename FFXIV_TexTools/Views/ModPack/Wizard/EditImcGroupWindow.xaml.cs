using FFXIV_TexTools.Views.Controls;
using FFXIV_TexTools.Views.Wizard;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
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
using xivModdingFramework.Cache;
using xivModdingFramework.Items.Interfaces;
using xivModdingFramework.Variants.FileTypes;

namespace FFXIV_TexTools.Views.Wizard
{
    /// <summary>
    /// Interaction logic for ImcGroupEditWindow.xaml
    /// </summary>
    public partial class EditImcGroupWindow : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private WizardGroupEntry Group;

        public string GroupName
        {
            get => Group.Name;
            set
            {
                Group.Name = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(GroupName)));
            }
        }
        public ushort Variant
        {
            get => Group.ImcData.Variant;
            set
            {
                Group.ImcData.Variant = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Variant)));
            }
        }
        public string ItemSetText
        {
            get {
                return Group.ImcData.Root.Info.GetBaseFileName();
            }
        }
        public bool AllVariants
        {
            get => Group.ImcData.AllVariants;
            set
            {
                Group.ImcData.AllVariants = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(AllVariants)));
            }
        }

        private ObservableCollection<WrappedImcOption> _Options = new ObservableCollection<WrappedImcOption>();
        public ObservableCollection<WrappedImcOption> Options
        {
            get => _Options;
            set
            {
                _Options = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Options)));
            }
        }

        public EditImcGroupWindow(WizardGroupEntry g)
        {
            Group = g;
            DataContext = this;
            InitializeComponent();
            VariantEditor.ImcEntry = Group.ImcData.BaseEntry;
            RebuildOptions();

            IncludeDisableBox.IsChecked = Group.Options.Any(x => x.ImcData.IsDisableOption);
        }
        private void RebuildOptions()
        {
            foreach(var wo in Options)
            {
                wo.RemoveRequested -= Option_RemoveRequested;
                wo.MaskChanged -= Option_MaskChanged;
                wo.MoveUpRequested -= Option_MoveUp;
                wo.MoveDownRequested -= Option_MoveDown;
            }
            Options.Clear();
            foreach (var option in Group.Options)
            {
                if (option.ImcData.IsDisableOption) continue;
                var wo = new WrappedImcOption(option);
                wo.RemoveRequested += Option_RemoveRequested;
                wo.MaskChanged += Option_MaskChanged;
                wo.MoveUpRequested += Option_MoveUp;
                wo.MoveDownRequested += Option_MoveDown;
                Options.Add(wo);
            }
        }


        private bool _UPDATING_MASKS;
        private void Option_MaskChanged(WrappedImcOption option)
        {
            if (_UPDATING_MASKS) return;
            _UPDATING_MASKS = true;
            int mask = option.Mask;
            foreach (var o in Options)
            {
                if (o == option) continue;
                o.Mask &= (ushort)~mask;
            }
            _UPDATING_MASKS = false;
        }

        private void Option_RemoveRequested(WrappedImcOption obj)
        {
            Group.Options.Remove(obj.Option);
            RebuildOptions();
        }

        private void ChangeItemSet_Click(object sender, RoutedEventArgs e)
        {
            var item = PopupItemSelection.ShowItemSelection(ItemFilterFunc, ItemSelectFunc, this);
            if (item == null) return;

            var root = item.GetRoot();
            if (root == null) return;

            Group.ImcData.Root = root;

            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ItemSetText)));
        }

        private bool ItemSelectFunc(IItem item)
        {
            if (item == null) return false;
            if (item.GetRoot() == null) return false;
            return true;
        }

        private bool ItemFilterFunc(IItem item)
        {
            var root = item.GetRootInfo();
            return Imc.UsesImc(root);
        }

        private void AddOption_Click(object sender, RoutedEventArgs e)
        {
            var op = new WizardOptionEntry(Group);

            op.Name = "New Option";
            op.ImcData = new WizardImcOptionData();

            Group.Options.Add(op);
            RebuildOptions();
        }

        private void IncludeDisable_Checked(object sender, RoutedEventArgs e)
        {
            var hasDisable = Group.Options.Any(x => x.ImcData.IsDisableOption);
            if (hasDisable) return;

            var op = new WizardOptionEntry(Group);
            op.ImcData = new WizardImcOptionData();
            op.Name = "Disable";
            op.ImcData.IsDisableOption = true;
            op.Description = "Disable this option.";

            Group.Options.Insert(0, op);

        }

        private void IncludeDisable_Unchecked(object sender, RoutedEventArgs e)
        {
            var hasDisable = Group.Options.Any(x => x.ImcData.IsDisableOption);
            if (!hasDisable) return;

            Group.Options.RemoveAll(x => x.ImcData.IsDisableOption);
        }

        private void Option_MoveUp(WrappedImcOption option)
        {
            var minOp = 0;
            if (Group.Options[0].ImcData.IsDisableOption)
            {
                minOp = 1;
            }

            var idx = Group.Options.IndexOf(option.Option);
            if (idx == minOp) return;

            var otherOption = Group.Options[idx - 1];
            Group.Options[idx] = otherOption;
            Group.Options[idx - 1] = option.Option;
            RebuildOptions();
        }

        private void Option_MoveDown(WrappedImcOption option)
        {
            var idx = Group.Options.IndexOf(option.Option);
            if (idx >= Group.Options.Count - 1) return;

            var otherOption = Group.Options[idx + 1];
            Group.Options[idx] = otherOption;
            Group.Options[idx + 1] = option.Option;
            RebuildOptions();
        }

        private void Done_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
        }
    }
}
