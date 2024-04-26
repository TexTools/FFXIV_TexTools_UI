
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using static xivModdingFramework.Materials.DataContainers.ShaderHelpers;
using System.IO;
using System.Linq;

namespace FFXIV_TexTools.Views.Controls
{
    /// <summary>
    /// Interaction logic for RawFloatValueDisplay.xaml
    /// </summary>
    public partial class LoadPresetDialog : Window
    {
        public const string _PresetsPath = "./Resources/MaterialPresets";

        public string SelectedPath = "";

        public LoadPresetDialog(EShaderPack shpk)
        {
            InitializeComponent();
            var collection = new ObservableCollection<KeyValuePair<string,string>>();
            var files = Directory.GetFiles(_PresetsPath, "*",SearchOption.AllDirectories).Where(x => x.EndsWith(".mtrl"));
            foreach (var file in files)
            {
                var folderPath = Path.GetDirectoryName(file);
                var parts = folderPath.Split(Path.DirectorySeparatorChar);
                var shaderName = parts.Last() + ".shpk";
                var fName = Path.GetFileNameWithoutExtension(file);
                var displayName = shaderName + " - " + fName;
                var kp = new KeyValuePair<string, string>(displayName, file);
                collection.Add(kp);
            }
            
            PresetsList.ItemsSource = collection.OrderBy(x => x.Key);
            PresetsList.DisplayMemberPath = "Key";
            PresetsList.SelectedValuePath = "Value";
            PresetsList.SelectedIndex = 0;
        }

        private void LoadButton_Click(object sender, RoutedEventArgs e)
        {
            SelectedPath = (string) PresetsList.SelectedValue;
            DialogResult = true;
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            SelectedPath = "";
            Close();
            DialogResult = false;
        }

        public static string ShowLoadPresetDialog(EShaderPack shpk)
        {
            var wind = new LoadPresetDialog(shpk);
            wind.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            var result = wind.ShowDialog();

            return wind.SelectedPath;
        }
    }
}
