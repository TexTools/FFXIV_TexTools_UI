using FFXIV_TexTools.Views.Wizard;
using FFXIV_TexTools.Views.Wizard.ManipulationEditors;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows.Controls;
using System.Windows.Input;
using xivModdingFramework.Mods.FileTypes;
using xivModdingFramework.Mods;

namespace FFXIV_TexTools.Views.Wizard
{
    /// <summary>
    /// Interaction logic for ManipulationEditorWindow.xaml
    /// </summary>
    public partial class ManipulationEditorWindow : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        private WizardStandardOptionData _Data;
        public WizardStandardOptionData Data
        {
            get => _Data;
            set
            {
                _Data = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Data)));
            }
        }

        private ObservableCollection<ManipulationKv> _Manipulations = new ObservableCollection<ManipulationKv>();
        public ObservableCollection<ManipulationKv> Manipulations
        {
            get => _Manipulations;
            set
            {
                _Manipulations = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Manipulations)));
            }
        }

        private PMPManipulationWrapperJson _LastManipulation;

        private PMPManipulationWrapperJson _SelectedManipulation;
        public PMPManipulationWrapperJson SelectedManipulation
        {
            get => _SelectedManipulation;
            set
            {
                _SelectedManipulation = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectedManipulation)));
            }
        }

        private static Dictionary<Type, Type> EditorTypes = new Dictionary<Type, Type>()
        {
            { typeof(PMPEstManipulationWrapperJson), typeof(EstManipulationEditor) },
            { typeof(PMPEqpManipulationWrapperJson), typeof(EqpManipulationEditor) },
            { typeof(PMPEqdpManipulationWrapperJson), typeof(EqdpManipulationEditor) },
            { typeof(PMPImcManipulationWrapperJson), typeof(ImcManipulationEditor) },
            { typeof(PMPGmpManipulationWrapperJson), typeof(GmpManipulationEditor) },
            { typeof(PMPRspManipulationWrapperJson), typeof(RspManipulationEditor) },
            { typeof(PMPGlobalEqpManipulationWrapperJson), typeof(GlobalEqpEditor) },
        };
        private static Dictionary<string, Type> ManipulationTypes = new Dictionary<string, Type>()
        {
            { "Imc", typeof(PMPImcManipulationWrapperJson) },
            { "Eqp", typeof(PMPEqpManipulationWrapperJson) },
            { "Eqdp", typeof(PMPEqdpManipulationWrapperJson) },
            { "Est", typeof(PMPEstManipulationWrapperJson) },
            { "Gmp", typeof(PMPGmpManipulationWrapperJson) },
            { "Rsp", typeof(PMPRspManipulationWrapperJson) },
            { "GlobalEqp", typeof(PMPGlobalEqpManipulationWrapperJson) },
        };

        public class ManipulationKv : INotifyPropertyChanged
        {
            public event PropertyChangedEventHandler PropertyChanged;
            private string _Key;
            public string Key
            {
                get => _Key;
                set
                {
                    _Key = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Key)));
                }
            }
            public PMPManipulationWrapperJson Value { get; set; }

            public ManipulationKv(string key, PMPManipulationWrapperJson value)
            {
                Key = key;
                Value = value;
            }

        }

        public ManipulationEditorWindow(WizardStandardOptionData data)
        {
            DataContext = this;
            InitializeComponent();
            Data = data;
            if (Data.Manipulations == null)
            {
                Data.Manipulations = new List<PMPManipulationWrapperJson>();
            }

            foreach(var kv in ManipulationTypes)
            {
                var mi = new MenuItem();
                mi.Header = kv.Key;
                mi.Click += (object sender, System.Windows.RoutedEventArgs e) =>
                {
                    var manip = (PMPManipulationWrapperJson) Activator.CreateInstance(ManipulationTypes[kv.Key]);
                    manip.Type = kv.Key;
                    AddManipulation(manip);
                };

                AddContextMenu.Items.Add(mi);
            }

            RebuildList();
            Closing += ManipulationEditorWindow_Closing;
        }

        private void ManipulationEditorWindow_Closing(object sender, CancelEventArgs e)
        {
            if (null != Owner)
            {
                Owner.Activate();
            }
        }

        private void AddManipulation(PMPManipulationWrapperJson manip)
        {
            Data.Manipulations.Add(manip);
            Data.SortManipulations();
            RebuildList();

            var kv = Manipulations.FirstOrDefault(x => x.Value == manip);

            ManipList.ScrollIntoView(kv);
            SelectedManipulation = manip;
        }

        private void RebuildList()
        {
            Manipulations.Clear();
            foreach (var m in Data.Manipulations)
            {
                Manipulations.Add(new ManipulationKv(m.GetNiceName(), m));
            }

            SelectedManipulation = Data.Manipulations.FirstOrDefault();
        }

        private void RemoveManipulation_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            if (SelectedManipulation != null && Data.Manipulations != null)
            {
                Data.Manipulations.Remove(SelectedManipulation);
                RebuildList();
            }
        }

        private void Done_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            DialogResult = true;
        }

        private void ManipulationChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (_LastManipulation != null)
            {
                UpdateName(_LastManipulation);
            }
            EditorBox.Content = null;

            if(SelectedManipulation == null)
            {
                return;
            }

            Type t;
            if (!EditorTypes.ContainsKey(SelectedManipulation.GetType()))
            {
                t = typeof(UnknownManipulationEditor);
            } else
            {
                t = EditorTypes[SelectedManipulation.GetType()];
            }

            var control = Activator.CreateInstance(t, SelectedManipulation) as UserControl;

            EditorBox.Content = control;
            _LastManipulation = SelectedManipulation;
        }

        private void ClearManipulations_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            if(!this.ShowConfirmation("Clear Manipulations Confirmation", "Are you sure you wish to clear ALL manipulations for this option?"))
            {
                return;
            }

            Data.Manipulations.Clear();
            RebuildList();
        }

        public void UpdateName(object manipulation)
        {
            var kv = Manipulations.FirstOrDefault(x => x.Value.GetManipulation() == manipulation);
            if (kv == null) return;
            UpdateName(kv);
        }
        public void UpdateName(PMPManipulationWrapperJson wrapper)
        {
            var kv = Manipulations.FirstOrDefault(x => x.Value == wrapper);
            if (kv == null) return;
            UpdateName(kv);
        }
        public void UpdateName(ManipulationKv kv)
        {
            kv.Key = kv.Value.GetNiceName();
        }

        private void AddManipulation_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            AddContextMenu.IsOpen = true;
            AddContextMenu.PlacementTarget = AddButton;
        }
    }
}
