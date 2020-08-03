// FFXIV TexTools
// Copyright © 2020 Rafael Gonzalez - All Rights Reserved
// 
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
// 
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
// 
// You should have received a copy of the GNU General Public License
// along with this program.  If not, see <http://www.gnu.org/licenses/>.

using FFXIV_TexTools.Properties;
using FFXIV_TexTools.ViewModels;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using xivModdingFramework.General.Enums;
using xivModdingFramework.Items.Interfaces;
using xivModdingFramework.Models.DataContainers;
using xivModdingFramework.Models.FileTypes;
using xivModdingFramework.Variants.FileTypes;

namespace FFXIV_TexTools.Views.Models
{
    /// <summary>
    /// Interaction logic for FullModelView.xaml
    /// </summary>
    public partial class FullModelView
    {
        private readonly DirectoryInfo _gameDirectory;
        private readonly FullModelViewModel _fmvm;

        public FullModelView()
        {
            InitializeComponent();

            _gameDirectory = new DirectoryInfo(Settings.Default.FFXIV_Directory);
            _fmvm = new FullModelViewModel(this);

            this.DataContext = _fmvm;
        }

        /// <summary>
        /// Adds the model to the viewport
        /// </summary>
        /// <param name="ttModel">The model to add</param>
        /// <param name="materialDictionary">The dictionary of texture data for the model</param>
        /// <param name="item">The item associated with the model</param>
        /// <param name="race">The race of the model</param>
        public async Task AddModel(TTModel ttModel, Dictionary<int, ModelTextureData> materialDictionary, IItemModel item, XivRace race)
        {
            // Because the full model is skinned, it requires the bones to exist so we check them here
            var sklb = new Sklb(_gameDirectory);
            var skel = await sklb.CreateParsedSkelFile(ttModel.Source);

            // If we have weights, but can't find a skel, bad times.
            if (skel == null)
            {
                throw new InvalidDataException("Unable to resolve model skeleton.");
            }

            _fmvm.AddModelToView(ttModel, materialDictionary, item, race);
        }


        /// <summary>
        /// Event handler for export button
        /// </summary>
        private async void ExportModelButton_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            // Show the export dialog
            var fullModelExportDialog = new FullModelExportDialogView(_fmvm.SelectedSkeleton.Name) {Owner = this};

            if (fullModelExportDialog.ShowDialog() == true)
            {
                await Export(fullModelExportDialog.ModelName);
            }
        }

        /// <summary>
        /// Exports the model
        /// </summary>
        /// <param name="fullModelName">The name chosen by the user for the full model export</param>
        private async Task Export(string fullModelName)
        {
            var fileFormat = "fbx";
            var savePath = new DirectoryInfo(Settings.Default.Save_Directory);
            var outputFilePath = $"{savePath}\\FullModel\\{fullModelName}\\{fullModelName}.{fileFormat}";

            // Create output directory
            Directory.CreateDirectory($"{savePath}\\FullModel\\{fullModelName}");

            var fmViewPortVM = viewport3DX.DataContext as FullModelViewport3DViewModel;

            var converterFolder = Directory.GetCurrentDirectory() + "\\converters\\" + fileFormat;
            Directory.CreateDirectory(converterFolder);
            var dbPath = converterFolder + "\\input.db";

            File.Delete(dbPath);

            // Create the DB where all models will be added and fill the metadata
            fmViewPortVM.shownModels.FirstOrDefault().Value.TtModel.SetFullModelDBMetaData(dbPath, fullModelName);

            // Export the materials for each model and save model to DB
            foreach (var model in fmViewPortVM.shownModels)
            {
                var mtrlVariant = 1;
                try
                {
                    var imc = new Imc(_gameDirectory);
                    mtrlVariant = (await imc.GetImcInfo(model.Value.ItemModel)).Variant;
                }
                catch (Exception ex)
                {
                    // No-op, defaulted to 1.
                }

                await Mdl.ExportMaterialsForModel(model.Value.TtModel, outputFilePath, _gameDirectory, mtrlVariant, _fmvm.SelectedSkeleton.XivRace);

                // Save model to DB
                model.Value.TtModel.SaveFullToFile(dbPath, $"c{_fmvm.SelectedSkeleton.XivRace.GetRaceCode()}");
            }

            var proc = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = converterFolder + "\\converter.exe",
                    Arguments = "\"" + dbPath + "\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    WorkingDirectory = "" + converterFolder + "",
                    CreateNoWindow = true
                }
            };

            proc.Start();
            proc.WaitForExit();
            var code = proc.ExitCode;

            if (code != 0)
            {
                throw new Exception("Exporter threw error code: " + proc.ExitCode);
            }

            var outputFile = converterFolder + "\\result." + fileFormat;

            // Just move the result file if we need to.
            if (!Path.Equals(outputFilePath, outputFile))
            {
                File.Delete(outputFilePath);
                File.Move(outputFile, outputFilePath);
            }
        }

        /// <summary>
        /// Event handler when the window is closed
        /// </summary>
        private void MetroWindow_Closed(object sender, EventArgs e)
        {
            // Clean up resources when the window is closed
            _fmvm.CleanUp();
        }
    }
}
