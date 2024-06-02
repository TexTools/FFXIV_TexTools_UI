using System;
using System.Collections.Generic;
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
    /// Interaction logic for EqpManipulationEditor.xaml
    /// </summary>
    public partial class EqpManipulationEditor : UserControl
    {
        PMPEqpManipulationJson Manipulation;
        public EqpManipulationEditor(PMPManipulationWrapperJson manipulation)
        {
            var wrapper = manipulation as PMPEqpManipulationWrapperJson;
            Manipulation = wrapper.Manipulation;
            InitializeComponent();
        }
    }
}
