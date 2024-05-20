using FFXIV_TexTools.Views.Controls;
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

namespace FFXIV_TexTools.Views.Controls
{
    /// <summary>
    /// Interaction logic for ModelFileControl.xaml
    /// </summary>
    public partial class ModelFileControl : FileViewControl
    {
        public ModelFileControl()
        {
            InitializeComponent();
        }

        public override Task INTERNAL_ClearFile()
        {
            throw new NotImplementedException();
        }

        protected override Task<byte[]> INTERNAL_GetUncompressedData()
        {
            throw new NotImplementedException();
        }

        protected override Task<bool> INTERNAL_LoadFile(byte[] data)
        {
            throw new NotImplementedException();
        }

        protected override Task<bool> INTERNAL_SaveAs(string externalFilePath)
        {
            throw new NotImplementedException();
        }
    }
}
