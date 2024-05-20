
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
using xivModdingFramework.Items.Interfaces;
using System.Threading.Tasks;
using xivModdingFramework.Helpers;
using System.Text.RegularExpressions;
using xivModdingFramework.SqPack.FileTypes;
using xivModdingFramework.Cache;
using AutoUpdaterDotNET;
using FFXIV_TexTools.Views.MaterialEditor;

namespace FFXIV_TexTools.Views.Controls
{
    /// <summary>
    /// Interaction logic for RawFloatValueDisplay.xaml
    /// </summary>
    public partial class CreateMaterialDialog : Window
    {
        private XivMtrl _material;
        public string Suffix = "a";
        public CreateMaterialDialog(XivMtrl material)
        {
            _material = material;
            InitializeComponent();
            PresetName.IsEnabled = false;
            //PresetName.Focus();

            Setup();
        }

        private async void Setup()
        {
            var id = await GetNewMaterialIdentifier();
            PresetName.Text = id;
            PresetName.IsEnabled = true;
            PresetName.Focus();
        }

        /// <summary>
        /// Sanitizes a path string
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        private string SanitizeSuffix(string path)
        {
            path = path.ToLower();
            path = Regex.Replace(path, "[^a-z0-9-_]", "");
            return path;
        }


        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            var val = SanitizeSuffix(PresetName.Text);
            Suffix = val;
            DialogResult = true;
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }

        public static Regex SuffixRegex = new Regex("_([a-z0-9])+\\.mtrl");

        public async Task<string> GetNewMaterialIdentifier()
        {
            var tx = MainWindow.DefaultTransaction;
            // Get new Material Identifier
            var alphabet = Constants.Alphabet;
            List<string> partList = new List<string>();


            if (!SuffixRegex.IsMatch(_material.MTRLPath))
            {
                return "a";
            }
            for (var i = 0; i < alphabet.Length; i++)
            {
                var identifier = alphabet[i];
                
                var newPath = SuffixRegex.Replace(_material.MTRLPath, "_" + identifier + ".mtrl");
                var exists = await tx.FileExists(newPath);
                if (!exists)
                {
                    return identifier.ToString();
                }
            }

            // Really? Fine... Twoooo alphabets
            for (var a = 0; a < alphabet.Length; a++)
            {
                for (var b = 0; b < alphabet.Length; b++)
                {
                    var identifier = alphabet[a] + alphabet[b];
                    var newPath = SuffixRegex.Replace(_material.MTRLPath, "_" + identifier + ".mtrl");
                    var exists = await tx.FileExists(newPath);
                    if (!exists)
                    {
                        return identifier.ToString();
                    }
                }
            }

            // If you got this far, you can suffer memes.
            return "why";
        }

        public static async Task<XivMtrl> ShowCreateMaterialDialog(XivMtrl material, IItemModel item, Window owner = null)
        {

            var exists = await MainWindow.DefaultTransaction.FileExists(material.MTRLPath);


            var newMtrl = (XivMtrl)material.Clone();
            MaterialEditorMode mode = MaterialEditorMode.NewMulti;
            if (exists)
            {
                // We only actually open the user-input dialog IF we're creating something that has an extension to change.
                var wind = new CreateMaterialDialog(material);
                wind.WindowStartupLocation = WindowStartupLocation.CenterScreen;
                wind.Owner = owner != null ? owner : System.Windows.Application.Current.MainWindow;
                var result = wind.ShowDialog();
                if (result != true)
                {
                    return null;
                }

                newMtrl.MTRLPath = SuffixRegex.Replace(material.MTRLPath, "_" + wind.Suffix + ".mtrl");
                if (newMtrl.MTRLPath == material.MTRLPath)
                {
                    // Failed to replace suffix, just add one on.
                    newMtrl.MTRLPath = Regex.Replace(material.MTRLPath, "\\.mtrl$", "_" + wind.Suffix + ".mtrl");
                }
            }
            else
            {
                // If the file doesn't exist, set the mode accordingly and just pass in the clone of the material we were given.
                mode = MaterialEditorMode.NewRace;
            }


            // Call into the editor with our new material.
            var editor = new MaterialEditorView() { Owner = System.Windows.Application.Current.MainWindow };
            var open = await editor.SetMaterial(newMtrl, item, mode);
            if(!open)
            {
                return null;
            }

            var r = editor.ShowDialog();
            if(r != true) {
                return null;
            }
            return editor.Material;
        }
    }
}
