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
    /// Interaction logic for MaterialEditorAdvancedView.xaml
    /// </summary>
    public partial class MaterialEditorAdvancedView
    {
        private XivMtrl _material;
        public XivMtrl Material
        {
            get
            {
                return _material;
            }
        }

        public MaterialEditorAdvancedView()
        {
            InitializeComponent();
        }

        public void SetMaterial(XivMtrl mtrl)
        {
            if(mtrl == null)
            {
                throw new ArgumentNullException("mtrl");
            }

            _material = (XivMtrl) mtrl.Clone();


        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}
