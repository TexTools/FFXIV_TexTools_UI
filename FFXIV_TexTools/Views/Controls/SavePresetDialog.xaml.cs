
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
    public partial class SavePresetDialog : Window
    {
        public const string _PresetsPath = "./Resources/MaterialPresets";

        public string SelectedPath = "";
        private EShaderPack _Shpk;

        public SavePresetDialog(EShaderPack shpk, string defaultName = "New Preset")
        {
            _Shpk = shpk;
            InitializeComponent();
            PresetName.Text = defaultName;
            PresetName.Focus();
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            var folder = GetEnumDescription(_Shpk);

            // Remove the ".shpk" part of the shader name.
            folder = folder.Substring(0, folder.Length - 5);

            SelectedPath = LoadPresetDialog._PresetsPath + "/" + folder + "/" + PresetName.Text + ".mtrl";
            DialogResult = true;
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            SelectedPath = "";
            Close();
            DialogResult = false;
        }

        public static string ShowSavePresetDialog(EShaderPack shpk, string defaultName = "New Preset")
        {
            var wind = new SavePresetDialog(shpk, defaultName);
            wind.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            var result = wind.ShowDialog();

            return wind.SelectedPath;
        }
    }
}
