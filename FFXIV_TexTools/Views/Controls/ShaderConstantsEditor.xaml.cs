
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using static xivModdingFramework.Materials.DataContainers.ShaderHelpers;
using System.IO;
using System.Linq;
using HelixToolkit.SharpDX.Core.Shaders;
using xivModdingFramework.Materials.DataContainers;

namespace FFXIV_TexTools.Views.Controls
{
    /// <summary>
    /// Interaction logic for RawFloatValueDisplay.xaml
    /// </summary>
    public partial class ShaderConstantsEditor : Window
    {
        public List<ShaderConstant> Constants;
        public ShaderConstantsEditor(List<ShaderConstant> constants)
        {
            InitializeComponent();
            Constants = constants.ToList();
        }

        private void Confirm_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        public static List<ShaderConstant> ShowConstantsEditor(List<ShaderConstant> constants, Window owner = null)
        {
            var wind = new ShaderConstantsEditor(constants);
            wind.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            wind.Owner = owner != null ? owner : System.Windows.Application.Current.MainWindow;
            var result = wind.ShowDialog();
            if(result != true)
            {
                return null;
            }

            return wind.Constants;
        }
    }
}
