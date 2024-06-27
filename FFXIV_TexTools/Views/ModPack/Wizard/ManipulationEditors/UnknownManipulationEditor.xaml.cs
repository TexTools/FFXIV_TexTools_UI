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
    /// Interaction logic for UnknownManipulationEditor.xaml
    /// </summary>
    public partial class UnknownManipulationEditor : UserControl
    {
        public UnknownManipulationEditor(PMPManipulationWrapperJson manipulation)
        {
            InitializeComponent();
        }
    }
}
