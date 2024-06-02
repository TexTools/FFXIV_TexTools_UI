using FFXIV_TexTools.Views.Wizard;
using FFXIV_TexTools.Views.Wizard.ManipulationEditors;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows.Controls;
using xivModdingFramework.Mods.FileTypes;

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

        private ObservableCollection<KeyValuePair<string, PMPManipulationWrapperJson>> _Manipulations = new ObservableCollection<KeyValuePair<string, PMPManipulationWrapperJson>>();
        public ObservableCollection<KeyValuePair<string, PMPManipulationWrapperJson>> Manipulations
        {
            get => _Manipulations;
            set
            {
                _Manipulations = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Manipulations)));
            }
        }

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

        public ManipulationEditorWindow(WizardStandardOptionData data)
        {
            DataContext = this;
            InitializeComponent();
            Data = data;
            if (Data.Manipulations == null)
            {
                Data.Manipulations = new List<PMPManipulationWrapperJson>();
            }

            RebuildList();
        }

        private void RebuildList()
        {
            Manipulations.Clear();
            foreach (var m in Data.Manipulations)
            {
                Manipulations.Add(new KeyValuePair<string, PMPManipulationWrapperJson>(m.GetNiceName(), m));
            }

            SelectedManipulation = Data.Manipulations.FirstOrDefault();
        }

        private void RemoveManipulation_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            Data.Manipulations.Remove(SelectedManipulation);
            RebuildList();
        }

        private void Done_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            DialogResult = true;
        }

        private void ManipulationChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
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
    }
}
