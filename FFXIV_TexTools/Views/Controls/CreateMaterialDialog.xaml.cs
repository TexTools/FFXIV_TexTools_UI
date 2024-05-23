
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
using xivModdingFramework.Mods.DataContainers;
using xivModdingFramework.Materials.FileTypes;
using xivModdingFramework.General.Enums;
using System.Diagnostics;

namespace FFXIV_TexTools.Views.Controls
{
    /// <summary>
    /// Interaction logic for RawFloatValueDisplay.xaml
    /// </summary>
    public partial class CreateMaterialDialog : Window
    {
        private XivMtrl _material;
        public string Suffix = "a";
        private string OriginalSuffix = "";
        public CreateMaterialDialog(XivMtrl material, string suffix)
        {
            _material = material;
            InitializeComponent();
            PresetName.IsEnabled = false;
            OriginalSuffix = suffix;
            //PresetName.Focus();

            Setup();
        }

        private async void Setup()
        {
            var id = await GetNewMaterialIdentifier(_material.MTRLPath);
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


        private async void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var val = SanitizeSuffix(PresetName.Text);

                var tx = MainWindow.DefaultTransaction;

                Suffix = val;

                var newPath = _material.MTRLPath.Replace(OriginalSuffix + ".mtrl", Suffix + ".mtrl");

                if (await tx.FileExists(newPath))
                {
                    this.ShowError("Material Exists Error", "The given material already exists:\n\n" + newPath);
                    return;
                }

                _material.MTRLPath = newPath;
                DialogResult = true;
            }
            catch(Exception ex)
            {
                Trace.WriteLine(ex);
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }


        public static async Task<string> GetNewMaterialIdentifier(string mtrl)
        {
            var tx = MainWindow.DefaultTransaction;
            // Get new Material Identifier
            var alphabet = Constants.Alphabet;
            List<string> partList = new List<string>();


            var res = IOUtil.MtrlSuffixExtractionRegex.Match(mtrl);
            if (!res.Success)
            {
                return "a";
            }
            var originalSuffix = res.Groups[1].Value;

            for (var i = 0; i < alphabet.Length; i++)
            {
                var identifier = alphabet[i];

                var newPath = mtrl.Replace(originalSuffix + ".mtrl", identifier + ".mtrl");
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
                    var newPath = mtrl.Replace(originalSuffix + ".mtrl", identifier + ".mtrl");
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

        /// <summary>
        /// Just creates the material ID.
        /// </summary>
        /// <param name="baseMaterial"></param>
        /// <param name="item"></param>
        /// <param name="owner"></param>
        /// <returns></returns>
        public static async Task<XivMtrl> ShowCreateMaterialDialogSimple(string targetModel, XivMtrl baseMaterial, IItem item, Window owner = null)
        {
            var tx = MainWindow.DefaultTransaction;
            var newMtrl = (XivMtrl)baseMaterial.Clone();

            var targetFolder = Path.GetDirectoryName(newMtrl.MTRLPath).Replace("\\","/");

            var modelRoot = IOUtil.GetPrimarySecondaryFromFilename(targetModel);
            var materialRoot = IOUtil.GetPrimarySecondaryFromFilename(baseMaterial.MTRLPath);

            if (!string.IsNullOrEmpty(materialRoot) && !string.IsNullOrEmpty(modelRoot))
            {
                // Swap root part of the filename if needed.
                newMtrl.MTRLPath = newMtrl.MTRLPath.Replace(materialRoot, modelRoot);
            }

            var materialRace = IOUtil.GetRaceFromPath(newMtrl.MTRLPath);
            var modelRace = IOUtil.GetRaceFromPath(targetModel);

            if(materialRace != modelRace && (int)modelRace > 100)
            {
                // Replace races as necessary, since they're not always part of the root information.
                newMtrl.MTRLPath = newMtrl.MTRLPath.Replace("c" + materialRace.GetRaceCode(), "c" + modelRace.GetRaceCode());
            }

            var mdlRoot = await XivCache.GetFirstRoot(targetModel);

            var originalSuffix = IOUtil.GetMaterialSuffix(newMtrl.MTRLPath);
            var suffix = "";


            if (string.IsNullOrWhiteSpace(originalSuffix))
            {
                originalSuffix = "a";
            }

            var tempName = Mtrl.GetMtrlNameByRootRaceSlotSuffix(mdlRoot.Info, modelRace, mdlRoot.Info.Slot, "a");
            var path = targetFolder + tempName;

            // Find first available suffix using our correct name.
            suffix = await GetNewMaterialIdentifier(path);

            // Create the final fully qualified start material name.
            var baseName = Mtrl.GetMtrlNameByRootRaceSlotSuffix(mdlRoot.Info, modelRace, mdlRoot.Info.Slot, suffix);

            var targetMaterial = targetFolder  + baseName;
            newMtrl.MTRLPath = targetMaterial;

            // We now have a valid, non-existing material name.
            // Ship it to the popup

            // We only actually open the user-input dialog IF we're creating something that has an extension to change.
            var wind = new CreateMaterialDialog(newMtrl, suffix);
            wind.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            wind.Owner = owner != null ? owner : System.Windows.Application.Current.MainWindow;
            var result = wind.ShowDialog();
            if (result != true)
            {
                return null;
            }


            return newMtrl;
        }


        public static async Task<XivMtrl> ShowCreateMaterialDialog(XivMtrl material, IItemModel item, Window owner = null)
        {
            throw new NotImplementedException("Out of date function");
            return null;
        }
    }
}
