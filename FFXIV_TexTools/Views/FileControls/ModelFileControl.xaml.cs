using FFXIV_TexTools.Helpers;
using FFXIV_TexTools.Resources;
using FFXIV_TexTools.ViewModels;
using FFXIV_TexTools.Views.Controls;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using xivModdingFramework.Cache;
using xivModdingFramework.Items.Enums;
using xivModdingFramework.Models.DataContainers;
using xivModdingFramework.Models.FileTypes;
using xivModdingFramework.Models.Helpers;
using xivModdingFramework.Mods.Enums;
using xivModdingFramework.Mods;
using xivModdingFramework.General.Enums;
using xivModdingFramework.Items.DataContainers;
using FFXIV_TexTools.Properties;
using Color = SharpDX.Color;
using xivModdingFramework.Items.Interfaces;
using xivModdingFramework.Materials.DataContainers;
using xivModdingFramework.Helpers;
using xivModdingFramework.Materials.FileTypes;
using xivModdingFramework.Variants.FileTypes;
using xivModdingFramework.Models.ModelTextures;
using System.IO;
using Timer = System.Timers.Timer;
using System.Timers;
using FFXIV_TexTools.Views.Models;
using System.Diagnostics;
using static FFXIV_TexTools.ViewModels.ModelViewModel;
using xivModdingFramework.Mods.DataContainers;
using System.Threading;
using xivModdingFramework.SqPack.FileTypes;
using SharpDX;

namespace FFXIV_TexTools.Views.Controls
{
    /// <summary>
    /// Interaction logic for ModelFileControl.xaml
    /// </summary>
    public partial class ModelFileControl : FileViewControl
    {

        private Viewport3DViewModel _ViewportVM;
        public Viewport3DViewModel ViewportVM
        {
            get
            {
                return _ViewportVM;
            }
            set
            {
                _ViewportVM = value;
                OnPropertyChanged(nameof(ViewportVM));
            }
        }

        private TTModel Model;
        private Helpers.ViewportCanvasRenderer _CanvasRenderer = null;


        private HashSet<string> _ChildFiles = new HashSet<string>();

        public ModelFileControl()
        {
            DataContext = this;
            InitializeComponent();
            ViewportVM = new Viewport3DViewModel();

            if (Configuration.EnvironmentConfiguration.TT_Unshared_Rendering)
                _CanvasRenderer = new Helpers.ViewportCanvasRenderer(Viewport, AlternateViewportCanvas);
            ViewType = EFileViewType.Editor;
            ColorsetButtonEnabled = false;

            ViewportVM.TextureUpdateRequested += TextureUpdateRequested;
            ViewportVM.ZoomExtentsRequested += ZoomExtentsRequested;

        }
        public override async Task INTERNAL_ClearFile()
        {
            Model = null;
            ViewportVM.ClearModels();
        }

        protected override async Task<byte[]> INTERNAL_ExternalToUncompressedFile(string externalFile, string internalFile, IItem referenceItem)
        {
            var data = await ShowModelImportDialog(externalFile, internalFile, referenceItem);
            return data;
        }

        protected override async Task<byte[]> INTERNAL_GetUncompressedData()
        {
            var tx = MainWindow.DefaultTransaction;
            var data = await Mdl.MakeUncompressedMdlFile(Model, InternalFilePath, false, tx);
            return data;
        }

        protected override async Task<bool> INTERNAL_LoadFile(byte[] data)
        {

            // The data coming in here is an uncompressed .mdl file.
            Model = Mdl.GetTTModel(data, InternalFilePath);

            _ChildFiles.Clear();
            _ChildFiles.Add(InternalFilePath);

            var root = XivCache.GetFilePathRoot(InternalFilePath);

            var tx = MainWindow.DefaultTransaction;
            _MaterialSet = 1;
            _MaterialPaths = new List<string>();

            try
            {
                // Resolve our child files to track.
                    var set = 1;
                if (ReferenceItem != null && Imc.UsesImc(root))
                {
                    var asIm = ReferenceItem as IItemModel;
                    if (asIm != null)
                    {
                        set = await Imc.GetMaterialSetId(asIm, false, tx);
                    }
                }

                _MaterialSet = set;
                _MaterialPaths = await Mdl.GetReferencedMaterialPaths(Model.Materials, InternalFilePath, set, false, true, tx);
                _ChildFiles.UnionWith(_MaterialPaths);

                for (int i = 0; i < _MaterialPaths.Count; i++)
                {
                    var filePath = _MaterialPaths[i];

                    if (!await tx.FileExists(filePath))
                        continue;

                    var textures = await Mtrl.GetTexturePathsFromMtrlPath(filePath, false, false, tx);
                    _ChildFiles.UnionWith(textures);
                }
            }
            catch(Exception ex)
            {
                // If any of these fail to resolve, we still want to continue.
                Trace.Write(ex);
            }


            // Don't actually wait for the visual update on the model.
            _ = UpdateVisual();

            return true;
        }

        protected override async Task<bool> INTERNAL_SaveAs(string externalFilePath)
        {
            // We don't support editing our current TTModel in a permanent way in the viewer.
            // So just export the internal game state.
            await Mdl.ExportMdlToFile(InternalFilePath, externalFilePath, 1, true, false, MainWindow.DefaultTransaction);
            return true;
        }

        protected override async Task<bool> ShouldUpdateOnFileChange(string changedFile)
        {
            if(changedFile == InternalFilePath)
            {
                return true;
            }

            if (UnsavedChanges)
            {
                return false;
            }

            if(Model == null)
            {
                return true;
            }

            if (Model == null)
            {
                return true;
            }

            if (_MaterialPaths == null)
            {
                return true;
            }

            if(_ChildFiles.Contains(changedFile))
            {
                return true;
            }
            return false;
        }

        public override string GetNiceName()
        {
            return "Model";
        }
        public override Dictionary<string, string> GetValidFileExtensions()
        {
            var ret = new Dictionary<string, string>();
            try
            {
                var exts = Mdl.GetAvailableExporters();
                foreach(var e in exts)
                {
                    ret.Add("." + e, e.ToUpper() + " Model");
                }

            }
            catch(Exception ex)
            {
                //No-Op
            }
            ret.Add(".mdl","FFXIV Model");
            return ret;
        }

        protected override void FreeUnmanaged()
        {
            // Ensure VM gets properly disposed.
            if (_ViewportVM != null)
            {
                _ViewportVM.Dispose();
            }
        }



        #region Core Visual Update Functions

        public ModelTextureData GetPlaceholderTexture(string materialFileName)
        {
            var tex = new ModelTextureData()
            {
                Alpha = new byte[] { 255, 255, 255, 255 },
                Diffuse = new byte[] { 128, 128, 128, 255 },
                Emissive = null,
                MaterialPath = materialFileName,
                Height = 1,
                Width = 1,
                Normal = new byte[] { 128, 128, 255, 255 },
                Specular = new byte[] { 64, 64, 64, 255 }
            };
            return tex;
        }
        public List<ModelTextureData> GetPlaceholderTextures(TTModel model)
        {
            var textures = new List<ModelTextureData>();
            foreach(var mat in model.Materials)
            {
                textures.Add(GetPlaceholderTexture(mat));
            }

            return textures;
        }

        /// <summary>
        /// Core function to refresh the visual model.
        /// Called once the actual TTModel is loaded, etc.
        /// </summary>
        /// <returns></returns>
        protected async Task UpdateVisual()
        {
            if (Model == null)
            {
                return;
            }

            try
            {
                ViewportVM.ClearModels();

                ModelStatusLabel = UIStrings.ModelStatus_Loading;

                // Might as well just make sure we have these updated.
                CustomizeViewModel.UpdateFrameworkColors();

                ViewportVM.TransparencyToggle = false;
                FmvButtonEnabled = false;
                ColorsetButtonEnabled = false;

                List<ModelTextureData> textureData = null;
                textureData = GetPlaceholderTextures(Model);

                ViewportVM.UpdateModel(Model, textureData);


                FmvButtonEnabled = true;
                // Disable FMV button if we're an unsupported type.
                if (Model.IsInternal)
                {
                    var modelRoot = await XivCache.GetFirstRoot(Model.Source);
                    if (modelRoot == null ||
                        modelRoot.Info.PrimaryType == XivItemType.demihuman
                        || modelRoot.Info.PrimaryType == XivItemType.monster
                        || modelRoot.Info.PrimaryType == XivItemType.weapon)
                    {
                        FmvButtonEnabled = false;
                    }
                }


                ShowModelStatus("Loading Textures...");

                await Task.Run(async () =>
                {
                    _ = UpdateTextures(Model, _MaterialPaths);
                });
            }
            catch (Exception ex)
            {
                this.ShowError("Model Render Error", "There was an error while rendering the model:\n\n" + ex.Message);
            }
            finally
            {
                //OnLoadingComplete();
            }
        }

        private int _MaterialSet = -1;
        private List<string> _MaterialPaths = null;

        private string AdjustSkinMaterial(string mtrl)
        {
            if (ModelModifiers.IsSkinMaterial(mtrl))
            {
                var race = IOUtil.GetRaceFromPath(InternalFilePath);
                // Adjust skin materials to point to the user's preferred race.

                var file = "/" + Path.GetFileName(mtrl);
                var body = file.Substring(file.IndexOf("b") + 1, 4);
                var raceString = file.Substring(file.IndexOf("c") + 1, 4);

                // XIV automatically forces skin materials to instead reference the appropiate one for the character wearing it.
                race = XivRaceTree.GetSkinRace(race);

                var gender = 0;
                if (int.Parse(XivRaces.GetRaceCode(race).Substring(0, 2)) % 2 == 0)
                {
                    gender = 1;
                }

                var userRace = ViewHelpers.GetUserRace(gender);

                // Get the actual skin the user's preferred race uses.
                var settingsRace = XivRaceTree.GetSkinRace(userRace.Race);
                var settingsBody = settingsRace == userRace.Race ? userRace.BodyID : "0001";

                // If the user's race is a child of the item's race, we can show the user skin instead.
                var useSettings = XivRaceTree.IsChildOf(settingsRace, race);
                if (useSettings)
                {
                    mtrl = mtrl.Replace("c" + raceString, "c" + settingsRace.GetRaceCode()).Replace("b" + body, "b" + settingsBody);
                }
                else
                {
                    // Just use item race.
                    mtrl = mtrl.Replace("c" + raceString, "c" + race.GetRaceCode()).Replace("b" + body, "b0001");
                }
            }
            return mtrl;
        }

        private List<ModelTextureData> _Textures;
        /// <summary>
        /// Gets the processed textures for the model, in usage order for the mdl.
        /// </summary>
        private async Task UpdateTextures(TTModel model, List<string> materials)
        {
            try
            {
                var textureList = new List<ModelTextureData>();
                if (Model == null) return;

                var tx = MainWindow.DefaultTransaction;

                var mtrlList = new List<XivMtrl>();


                var root = await XivCache.GetFirstRoot(InternalFilePath);
                if (root == null)
                {
                    return;
                }


                for (int i = 0; i < materials.Count; i++)
                {
                    var originalFilePath = materials[i];
                    var filePath = AdjustSkinMaterial(originalFilePath);

                    var exists = await tx.FileExists(filePath);
                    if (!exists && filePath != originalFilePath)
                    {
                        // Retry with original file path.
                        filePath = originalFilePath;
                        if (!await tx.FileExists(filePath))
                        {
                            var fakeName = "/" + Path.GetFileName(originalFilePath);
                            textureList.Add(GetPlaceholderTexture(fakeName));
                            continue;
                        }
                    }
                    else if (!exists)
                    {
                        var fakeName = "/" + Path.GetFileName(originalFilePath);
                        textureList.Add(GetPlaceholderTexture(fakeName));
                        continue;
                    }


                    var mtrlData = await Mtrl.GetXivMtrl(filePath, false, tx);

                    if (mtrlData == null)
                    {
                        var fakeName = "/" + Path.GetFileName(originalFilePath);
                        textureList.Add(GetPlaceholderTexture(fakeName));
                        continue;
                    }

                    if (mtrlData.ShaderPackRaw.Contains("colorchange"))
                    {
                        //hasColorChangeShader = true;
                    }


                    mtrlData.MTRLPath = originalFilePath;
                    mtrlList.Add(mtrlData);
                }


                for (int i = 0; i < mtrlList.Count; i++)
                {
                    var xivMtrl = mtrlList[i];
                    if (xivMtrl.ColorSetData.Count > 0)
                    {
                        ColorsetButtonEnabled = true;
                    }

                    var colors = ModelTexture.GetCustomColors();
                    colors.InvertNormalGreen = false;

                    var modelMaps = await ModelTexture.GetModelMaps(xivMtrl, colors, ViewportVM.HighlightedColorsetRow, tx);
                    textureList.Add(modelMaps);
                }



                if (InternalFilePath != Model.Source)
                {
                    // User changed models.
                    return;
                }
                _Textures = textureList;

                // Synchronous invoke here because we want to freeze the UI while we change the camera panel, to avoid any insanity.
                Dispatcher.Invoke(() =>
                {
                    ViewportVM.UpdateModel(Model, textureList);
                });
            }
            catch(Exception ex)
            {
                // No-Op.
                Trace.Write(ex);
            }
        }
        #endregion


        #region UI Properties and Basic Accessors

        private string _ModelStatusLabel;
        public string ModelStatusLabel
        {
            get => _ModelStatusLabel;
            set
            {
                _ModelStatusLabel = value;
                OnPropertyChanged(nameof(ModelStatusLabel));
            }
        }
        private Timer _modelStatusTimer;
        public void ShowModelStatus(string status)
        {
            ModelStatusLabel = status;

            if (_modelStatusTimer != null)
            {
                _modelStatusTimer.Stop();
                _modelStatusTimer.Dispose();
            }

            _modelStatusTimer = new Timer(3000);
            _modelStatusTimer.AutoReset = false;
            _modelStatusTimer.Enabled = true;
            _modelStatusTimer.Elapsed += ModelStatusTimerOnElapsed;
        }
        private void ModelStatusTimerOnElapsed(object sender, ElapsedEventArgs e)
        {
            if (_modelStatusTimer != null)
            {
                _modelStatusTimer.Stop();
                _modelStatusTimer.Dispose();
            }

            ModelStatusLabel = string.Empty;
        }

        private void ToggleFlyout(object sender, RoutedEventArgs e)
        {
            ViewerOptionsFlyout.IsOpen = !ViewerOptionsFlyout.IsOpen;
        }

        private bool _FmvButtonEnabled;
        public bool FmvButtonEnabled
        {
            get => _FmvButtonEnabled;
            set
            {
                _FmvButtonEnabled = value;
                OnPropertyChanged(nameof(FmvButtonEnabled));
            }
        }


        private bool _ColorsetButtonEnabled;
        public bool ColorsetButtonEnabled
        {
            get => _ColorsetButtonEnabled;
            set
            {
                _ColorsetButtonEnabled = value;
                OnPropertyChanged(nameof(ColorsetButtonEnabled));
            }
        }

        private string GetItem3DFolder()
        {
            return Path.Combine(GetDefaultSaveDirectory(), "/3D/");
        }


        /// <summary>
        /// Converts the current TTModel into XivMdl, in a similar structured state to how it would be on file import.
        /// Mostly used to power the mdl detail panel for debugging/analysis.
        /// </summary>
        /// <returns></returns>
        private async Task<XivMdl> GetXivMdl()
        {

            var tx = MainWindow.DefaultTransaction;
            var data = await Mdl.MakeUncompressedMdlFile(Model, InternalFilePath, false, tx);

            return Mdl.GetXivMdl(data, InternalFilePath);
        }

        public override string GetDefaultSaveDirectory()
        {
            var start = base.GetDefaultSaveDirectory();
            var ext = "3D\\";
            var comb = Path.Combine(start, ext);
            var full = Path.GetFullPath(comb);
            return full;
        }

        protected override KeyValuePair<string, string> GetDefaultExtension()
        {
            return new KeyValuePair<string, string>(".FBX", "FBX Model");
        }


        #endregion

        private async void FullModel_Click(object sender, RoutedEventArgs e)
        {
            if (Model == null || !Model.IsInternal) return;
            try
            {
                // Load a clean copy of the model.
                var ttmdl = await Mdl.GetTTModel(InternalFilePath);
                var race = IOUtil.GetRaceFromPath(InternalFilePath);
                
                // Weird usage pattern but apparently this is how liinko structured it.
                var fmv = FullModelView.Instance;
                fmv.Owner = MainWindow.GetMainWindow();
                fmv.Show();

                // Jank conversion to dictionary for the FMV that needs updating badly.
                var dict = new Dictionary<int, ModelTextureData>();
                var i = 0;
                foreach(var material in Model.Materials)
                {
                    var tex = _Textures.FirstOrDefault(x => x.MaterialPath == material);
                    if(tex == null)
                    {
                        tex = GetPlaceholderTexture(material);
                    }
                    dict.Add(i, tex);
                    i++;
                }


                await fmv.AddModel(ttmdl, dict, ReferenceItem as IItemModel, race);
            } catch (Exception ex)
            {
                this.ShowError("FMV Error", "An error occurred while loading the model to the FMV:\n\n" + ex.Message);
            }
        }


        private async Task<byte[]> ShowModelImportDialog(string externalPath, string internalPath, IItem referenceItem)
        {
            var race = IOUtil.GetRaceFromPath(InternalFilePath);
            var result = await ImportModelView.ImportModel(internalPath, referenceItem, true, externalPath, MainWindow.GetMainWindow());
            if ( result != null && result.Success)
            {
                return result.Data;
            }
            return null;
        }

        private async void ModelInfo_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (Model != null)
                {
                    var mdl = await Task.Run(GetXivMdl);
                    var modelInspector = new ModelInspector(mdl);
                    modelInspector.Owner = Window.GetWindow(this);
                    modelInspector.ShowDialog();
                }
            }
            catch(Exception ex)
            {
                //No-Op.
                Trace.WriteLine(ex);
            }
        }

        #region Viewport Function Routing
        private async void TextureUpdateRequested(Viewport3DViewModel requestor)
        {
            try
            {
                await UpdateTextures(Model, _MaterialPaths);
            }
            catch
            {
                // No-Op
            }
        }
        private void ZoomExtentsRequested(Viewport3DViewModel requestor)
        {
            Viewport.ZoomExtents();
        }
        #endregion

    }
}
