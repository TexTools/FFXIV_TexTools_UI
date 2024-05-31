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
using xivModdingFramework.Mods.FileTypes;

namespace FFXIV_TexTools.Views.Wizard.ManipulationEditors
{
    /// <summary>
    /// Interaction logic for GlobalEqpEditor.xaml
    /// </summary>
    public partial class GlobalEqpEditor : UserControl, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        private PMPGlobalEqpManipulationJson Manipulation;

        private ObservableCollection<KeyValuePair<string, GlobalEqpType>> _TypeSource = new ObservableCollection<KeyValuePair<string, GlobalEqpType>>();
        public ObservableCollection<KeyValuePair<string, GlobalEqpType>> TypeSource
        {
            get => _TypeSource;
            set
            {
                _TypeSource = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(TypeSource)));
            }
        }

        private bool _SetIdEnabled;
        public bool SetIdEnabled
        {
            get => _SetIdEnabled;
            set
            {
                _SetIdEnabled = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SetIdEnabled)));
            }
        }


        private int _SetId;
        public int SetId
        {
            get => _SetId;
            set
            {
                _SetId = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SetId)));
                Manipulation.Condition = SetId;
            }
        }

        private GlobalEqpType _TypeValue;
        public GlobalEqpType TypeValue
        {
            get => _TypeValue;
            set
            {
                _TypeValue = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(TypeValue)));
                Manipulation.Type = TypeValue;
                SetIdEnabled = TypeValue != GlobalEqpType.DoNotHideVieraHats && TypeValue != GlobalEqpType.DoNotHideHrothgarHats;
            }
        }



        public GlobalEqpEditor(PMPManipulationWrapperJson manipulation)
        {
            foreach(GlobalEqpType val in Enum.GetValues(typeof(GlobalEqpType)))
            {
                TypeSource.Add(new KeyValuePair<string, GlobalEqpType>(val.ToString(), val));
            }

            DataContext = this;
            InitializeComponent();
            var wrapper = manipulation as PMPGlobalEqpManipulationWrapperJson;
            Manipulation = wrapper.Manipulation;

            SetId = Manipulation.Condition;
            TypeValue = Manipulation.Type;
        }

    }
}
