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
using System.Windows;
using System.Windows.Forms;
using FFXIV_TexTools.Helpers;
using FFXIV_TexTools.Resources;
using MahApps.Metro.Controls.Dialogs;
using xivModdingFramework.General.Enums;
using xivModdingFramework.Items.Interfaces;
using xivModdingFramework.Models.DataContainers;
using xivModdingFramework.Models.FileTypes;
using xivModdingFramework.Variants.FileTypes;
using FFXIV_TexTools.Views.Controls;
using xivModdingFramework.Models.Helpers;
using System.Threading;

namespace FFXIV_TexTools.Views.Models
{
    /// <summary>
    /// Interaction logic for FullModelView.xaml
    /// </summary>
    public partial class FullModelView
    {
        private readonly FullModelViewModel _fmvm;
        private static FullModelView Instance;

        private int _LockCount = 0;
        private SemaphoreSlim _lockScreenSemaphore = new SemaphoreSlim(1);
        private ProgressDialogController _lockProgressController;

        public async Task LockUi(string title = null, string msg = null)
        {
            await _lockScreenSemaphore.WaitAsync();
            try
            {
                _LockCount++;
                if(_lockProgressController != null)
                {
                    return;
                }

                if (title == null)
                {
                    title = UIStrings.Loading;
                }

                if (msg == null)
                {
                    msg = UIStrings.Please_Wait;
                }

                _lockProgressController = await this.ShowProgressAsync(title, msg);
            }
            finally
            {
                _lockScreenSemaphore.Release();
            }
        }

        public async Task UnlockUi()
        {
            await _lockScreenSemaphore.WaitAsync();
            try
            {
                _LockCount--;
                if (_LockCount < 0)
                {
                    _LockCount = 0;
                }

                if (_LockCount > 0)
                {
                    return;
                }

                if (_lockProgressController == null)
                {
                    return;
                }

                await _lockProgressController.CloseAsync();
                _lockProgressController = null;
            }
            finally
            {
                _lockScreenSemaphore.Release();
            }
        }



        public static void ShowFmv()
        {
            if (Instance == null || !Instance.IsLoaded)
            {
                Instance = new FullModelView();
                Instance.Owner = MainWindow.GetMainWindow();
            }
            Instance.Show();
        }
        public static void AddModel(TTModel model, List<ModelTextureData> textures, IItemModel item)
        {
            // Jank conversion to dictionary for the FMV that needs updating badly.
            var dict = new Dictionary<int, ModelTextureData>();
            var i = 0;
            foreach (var material in model.Materials)
            {
                var tex = textures.FirstOrDefault(x => x.MaterialPath == material);
                if (tex == null)
                {
                    
                    tex = ModelFileControl.GetPlaceholderTexture(material);
                }
                dict.Add(i, tex);
                i++;
            }

            ShowFmv();
            Instance.AddModel(model, dict, item);

        }



        private Helpers.ViewportCanvasRenderer canvasRenderer = null;

        private FullModelView()
        {
            InitializeComponent();

            if (Configuration.EnvironmentConfiguration.TT_Unshared_Rendering)
                canvasRenderer = new Helpers.ViewportCanvasRenderer(viewport3DX, AlternateViewportCanvas);

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
        public void AddModel(TTModel ttModel, Dictionary<int, ModelTextureData> materialDictionary, IItemModel item)
        {
            try
            {
                _ = _fmvm.AddModelToView(ttModel, materialDictionary, item);
            }
            catch(Exception ex)
            {
                FlexibleMessageBox.Show(ex.Message, UIMessages.ModelAddErrorTitle, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }


        /// <summary>
        /// Event handler for export button
        /// </summary>
        private async void ExportModelButton_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            // Show the export dialog
            var fullModelExportDialog = new FullModelExportDialogView(_fmvm.SelectedSkeleton.Name) {Owner = this};

            try
            {
                if (fullModelExportDialog.ShowDialog() == true)
                {
                    await Export(fullModelExportDialog.ModelName);
                }
            } catch(Exception ex)
            {
                this.ShowError("Model Export Error", "An error occurred while exporting the model:\n\n" + ex.Message);
            }
        }

        /// <summary>
        /// Exports the model
        /// </summary>
        /// <param name="fullModelName">The name chosen by the user for the full model export</param>
        private async Task Export(string fullModelName)
        {
            var pc = await this.ShowProgressAsync(UIMessages.ExportingFullModelTitle, UIMessages.PleaseStandByMessage);

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
                    var im = model.Value.ItemModel;
                    if (im != null && Imc.UsesImc(im))
                    {
                        var imc = (await Imc.GetImcInfo(im));
                        if (imc != null)
                        {
                            mtrlVariant = imc.MaterialSet;
                        }
                    }
                }
                catch (Exception ex)
                {
                    // No-op, defaulted to 1.
                }

                await Mdl.ExportMaterialsForModel(model.Value.TtModel, outputFilePath, false, mtrlVariant, _fmvm.SelectedSkeleton.XivRace, MainWindow.DefaultTransaction);

                // Save model to DB
            }


            // Create a cloned list as the model may be modified during export process.
            List<TTModel> models = new List<TTModel>();
            foreach(var mdl in fmViewPortVM.shownModels.Select(x => x.Value.TtModel))
            {
                var m = (TTModel) mdl.Clone();
                models.Add(m);
            }

            

            TTModel.SaveFullToFile(dbPath, _fmvm.SelectedSkeleton.XivRace, models, null, MainWindow.DefaultTransaction, Settings.Default.ShiftExportUV);

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
                throw new Exception("Exporter threw error code: ".L() + proc.ExitCode);
            }

            var outputFile = converterFolder + "\\result." + fileFormat;

            // Just move the result file if we need to.
            if (!Path.Equals(outputFilePath, outputFile))
            {
                File.Delete(outputFilePath);
                File.Move(outputFile, outputFilePath);
            }

            await pc.CloseAsync();

            await this.ShowMessageAsync(UIMessages.FullModelExportSuccessTitle, string.Format(UIMessages.FullModelExportSuccessMessage, outputFilePath));
        }

        /// <summary>
        /// Event handler when the window is closing
        /// </summary>
        private void MetroWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            Instance = null;
            _fmvm.CleanUp();
        }
    }
}
