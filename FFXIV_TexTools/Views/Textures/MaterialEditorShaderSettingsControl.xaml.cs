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
using xivModdingFramework.Materials.DataContainers;

namespace FFXIV_TexTools.Views.Textures
{
    /// <summary>
    /// Interaction logic for MaterialEditorShaderSettingsControl.xaml
    /// </summary>
    public partial class MaterialEditorShaderSettingsControl : UserControl
    {
        private XivMtrl _material;

        public void SetMatrial(XivMtrl mtrl)
        {
            _material = mtrl;
        }

        public MaterialEditorShaderSettingsControl()
        {
            InitializeComponent();
        }
    }
}
