
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
using Newtonsoft.Json;

namespace FFXIV_TexTools.Views.Controls
{
    public partial class ShaderKeysEditor : Window
    {
        private List<ShaderKey> Keys;

        private XivMtrl Material;
        public ShaderKeysEditor(XivMtrl material)
        {
            Material = material;
            InitializeComponent();
        }

        private void Confirm_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }

        public static bool ShowKeysEditor(XivMtrl material, Window owner = null)
        {
            var wind = new ShaderKeysEditor(material);
            wind.Owner = owner != null ? owner : System.Windows.Application.Current.MainWindow;
            wind.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            var result = wind.ShowDialog();
            if(result != true)
            {
                return false;
            }
            return true;
        }
    }
}
