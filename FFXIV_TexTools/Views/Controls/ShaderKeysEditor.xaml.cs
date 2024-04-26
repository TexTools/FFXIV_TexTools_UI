
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
    public partial class ShaderKeysEditor : Window
    {
        private List<ShaderKey> Keys;
        public ShaderKeysEditor(List<ShaderKey> keys)
        {
            InitializeComponent();
            Keys = keys.ToList();
        }

        private void Confirm_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }

        public static List<ShaderKey> ShowKeysEditor(List<ShaderKey> keys, Window owner = null)
        {
            var wind = new ShaderKeysEditor(keys);
            wind.Owner = owner != null ? owner : System.Windows.Application.Current.MainWindow;
            wind.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            var result = wind.ShowDialog();
            if(result != true)
            {
                return null;
            }
            return wind.Keys;
        }
    }
}
