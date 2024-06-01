
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
        public string OriginalSuffix = "a";
        public CreateMaterialDialog(XivMtrl material)
        {
            _material = material;
            InitializeComponent();
            PresetName.IsEnabled = false;
            Suffix = material.GetSuffix();
            OriginalSuffix = material.GetSuffix();

            Setup();
        }

        private void Setup()
        {
            PresetName.Text = Suffix;
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


        /// <summary>
        /// Just creates the material ID.
        /// </summary>
        /// <param name="baseMaterial"></param>
        /// <param name="item"></param>
        /// <param name="owner"></param>
        /// <returns></returns>
        public static async Task<XivMtrl> ShowCreateMaterialDialogSimple(XivMtrl baseMaterial, string targetModel = null, Window owner = null)
        {
            if(targetModel == null)
            {
                // Yolo.  Should work fine 99% of the time.  The other 1% The user did something phenomenally stupid with their
                // material naming/pathing, and they can deal with it.
                targetModel = baseMaterial.MTRLPath.Replace(".mtrl", ".mdl");
            }

            //var tx = MainWindow.DefaultTransaction;
            var newMtrl = (XivMtrl)baseMaterial.Clone();

            var race = IOUtil.GetRaceFromPath(targetModel);
            var root = await XivCache.GetFirstRoot(targetModel);

            var mtrlRoot = await XivCache.GetFirstRoot(baseMaterial.MTRLPath);

            var tx = MainWindow.DefaultTransaction;
            var newPath = await root.Info.GetNextAvailableMaterial(race, (int) newMtrl.GetVersion(), newMtrl.GetFakeSlot(), tx);

            newMtrl.MTRLPath = newPath;

            // We only actually open the user-input dialog IF we're creating something that has an extension to change.
            var wind = new CreateMaterialDialog(newMtrl);
            wind.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            wind.Owner = owner != null ? owner : System.Windows.Application.Current.MainWindow;
            var result = wind.ShowDialog();
            if (result != true)
            {
                return null;
            }


            return newMtrl;
        }
    }
}
