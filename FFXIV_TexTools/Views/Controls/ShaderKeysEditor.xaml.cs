
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
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        public static List<ShaderKey> ShowKeysEditor(List<ShaderKey> keys)
        {
            var wind = new ShaderKeysEditor(keys);
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
