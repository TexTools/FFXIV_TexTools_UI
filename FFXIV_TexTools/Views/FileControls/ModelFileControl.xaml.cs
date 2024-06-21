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
using xivModdingFramework.Mods.DataContainers;
using System.Threading;
using xivModdingFramework.SqPack.FileTypes;
using SharpDX;
using ControlzEx.Standard;
using System.Windows.Media.Media3D;
using System.Runtime.CompilerServices;
using WK.Libraries.BetterFolderBrowserNS;
using System.Text.RegularExpressions;

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

        public bool ShowMaterialEnabled
        {
            get {
                return GetVisibleMaterial() == null ? false : true;
            }
        }

        private TTModel Model;
        private Helpers.ViewportCanvasRenderer _CanvasRenderer = null;

        private bool _ViewportLoaded = false;
        private HashSet<string> _ChildFiles = new HashSet<string>();

        public ModelFileControl()
        {
            DataContext = this;
            InitializeComponent();
            ViewportVM = new Viewport3DViewModel();

            if (Configuration.EnvironmentConfiguration.TT_Unshared_Rendering)
                _CanvasRenderer = new Helpers.ViewportCanvasRenderer(Viewport, AlternateViewportCanvas);
            ViewType = EFileViewType.Editor;
            ViewportVM.ColorsetButtonEnabled = false;

            ViewportVM.TextureUpdateRequested += TextureUpdateRequested;
            ViewportVM.ZoomExtentsRequested += ZoomExtentsRequested;
            ViewportVM.VisibleMeshChanged += VisibleMeshChanged;

            Viewport.Loaded += Viewport_Loaded;

        }

        private void Viewport_Loaded(object sender, RoutedEventArgs e)
        {
            _ViewportLoaded = true;
        }

        public override void INTERNAL_ClearFile()
        {
            Model = null;
            ViewportVM.ClearModels();
        }

        protected override async Task<byte[]> INTERNAL_ExternalToUncompressedFile(string externalFile, string internalFile, IItem referenceItem, ModTransaction tx)
        {
            if (externalFile.ToLower().EndsWith(".mdl"))
            {
                return await SmartImport.CreateUncompressedFile(externalFile, internalFile, tx);
            }

            byte[] data = null;
            // Have to main thread this, booo.
            await await Dispatcher.InvokeAsync(async () =>
            {
                data = await ShowModelImportDialog(externalFile, internalFile, referenceItem);
            });
            return data;
        }

        protected override async Task<byte[]> INTERNAL_GetUncompressedData()
        {
            var tx = MainWindow.DefaultTransaction;
            var data = await Mdl.MakeUncompressedMdlFile(Model, InternalFilePath, false, tx);
            return data;
        }

        protected override async Task<bool> INTERNAL_LoadFile(byte[] data, string path, IItem referenceItem, ModTransaction tx)
        {
            ShowModelStatus(UIStrings.ModelStatus_Loading);
            // The data coming in here is an uncompressed .mdl file.
            return await Task.Run(async () =>
            {
                return await LoadModel(Mdl.GetTTModel(data, path));
            });
        }
        protected async Task<bool> LoadModel(TTModel model) {
            Model = model;

            var maxDelay = 3000;
            var delay = 0;
            while (!_ViewportLoaded)
            {
                await Task.Delay(10);
                delay += 10;
                if(delay > maxDelay)
                {
                    throw new Exception("Viewport failed to initialize.");
                }
            }

            _ChildFiles.Clear();
            _ChildFiles.Add(InternalFilePath);
            _MaterialPaths = new List<string>();

            if (Model == null)
            {
                return false;
            }

            var root = XivCache.GetFilePathRoot(InternalFilePath);

            var tx = MainWindow.DefaultTransaction;

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
                Trace.WriteLine(ex);
            }


            // Don't actually wait for the visual update on the model.
            _ = await Dispatcher.InvokeAsync(async () =>
            {
                await UpdateVisual();
            });

            return true;
        }

        protected override async Task<bool> INTERNAL_SaveAs(string externalFilePath)
        {
            if (externalFilePath.ToLower().EndsWith(".mdl"))
            {
                return await SaveAsRaw(externalFilePath);
            }

            var tx = MainWindow.DefaultTransaction;
            var version = 1;

            var asIm = ReferenceItem as IItemModel;
            if(asIm != null && Imc.UsesImc(asIm))
            {
                version = await Imc.GetMaterialSetId(asIm, false, tx);
            }

            var settings = new ModelExportSettings()
            {
                 IncludeTextures = true,
                 ShiftUVs = Settings.Default.ShiftExportUV,
                 PbrTextures = false,
            };
            await Mdl.ExportTTModelToFile(Model, externalFilePath, version, settings, tx);
            return true;
        }

        protected override async Task<bool> INTERNAL_WriteModFile(ModTransaction tx)
        {
            // We override this to perform material validation first.
            Model.Source = InternalFilePath;
            await Mdl.FillMissingMaterials(Model, ReferenceItem, XivStrings.TexTools, tx);
            return await base.INTERNAL_WriteModFile(tx);   
        }
        protected override async Task<bool> ShouldUpdateOnFileChange(string changedFile)
        {
            if(changedFile == InternalFilePath)
            {
                return true;
            }

            if(Model == null || _MaterialPaths == null)
            {
                return true;
            }

            if(_ChildFiles.Contains(changedFile))
            {
                // Queue visual update if one of our constituent files chnaged, doesn't really matter if it fails.
                _ = await Dispatcher.InvokeAsync(async () =>
                {
                    await UpdateVisual();
                });
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

        protected override void FreeManaged()
        {
            // Ensure VM gets properly disposed.
            if (_ViewportVM != null)
            {
                ViewportVM.TextureUpdateRequested -= TextureUpdateRequested;
                ViewportVM.ZoomExtentsRequested -= ZoomExtentsRequested;
                ViewportVM.VisibleMeshChanged -= VisibleMeshChanged;
                _ViewportVM.Dispose();
            }
        }

        protected override void FreeUnmanaged()
        {
        }



        #region Core Visual Update Functions

        public static ModelTextureData GetPlaceholderTexture(string materialFileName)
        {
            var tex = new ModelTextureData()
            {
                Alpha = new byte[] { 255, 255, 255, 255 },
                Diffuse = new byte[] { 128, 128, 128, 255 },
                Emissive = null,
                MaterialPath = "/" + Path.GetFileName(materialFileName),
                Height = 1,
                Width = 1,
                Normal = new byte[] { 128, 128, 255, 255 },
                Specular = new byte[] { 64, 64, 64, 255 }
            };
            return tex;
        }
        public static List<ModelTextureData> GetPlaceholderTextures(TTModel model)
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

                ShowModelStatus(UIStrings.ModelStatus_Loading);

                // Might as well just make sure we have these updated.
                CustomizeViewModel.UpdateFrameworkColors();

                FmvButtonEnabled = false;
                ViewportVM.ColorsetButtonEnabled = false;

                List<ModelTextureData> textureData = null;
                textureData = GetPlaceholderTextures(Model);

                await ViewportVM.UpdateModel(Model, textureData);


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

                _ = Task.Run(async () =>
                {
                    await UpdateTextures(Model, _MaterialPaths);

                    Dispatcher.Invoke(() =>
                    {
                        ShowModelStatus("Model Loaded Successfully.");
                    });
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
                    mtrl = Regex.Replace(mtrl, "c[0-9]{4}", "c" + raceString);
                    mtrl = Regex.Replace(mtrl, "b[0-9]{4}", "b" + settingsBody);
                }
                else
                {
                    // Just use item race.
                    mtrl = Regex.Replace(mtrl, "c[0-9]{4}", "c" + raceString);
                    mtrl = Regex.Replace(mtrl, "b[0-9]{4}", "b0001");
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

                    mtrlData.MTRLPath = originalFilePath;
                    mtrlList.Add(mtrlData);
                }


                List<Task> tasks = new List<Task>();
                for (int i = 0; i < mtrlList.Count; i++)
                {
                    var xivMtrl = mtrlList[i];
                    // ModelMap generation needs to go on another thread always.
                    tasks.Add(Task.Run(async () =>
                    {
                        var colors = ModelTexture.GetCustomColors();
                        colors.InvertNormalGreen = false;

                        var modelMaps = await ModelTexture.GetModelMaps(xivMtrl, false, colors, ViewportVM.HighlightedColorsetRow, tx);

                        lock (textureList)
                        {
                            textureList.Add(modelMaps);
                        }
                    }));
                }

                await Task.WhenAll(tasks);

                for (int i = 0; i < mtrlList.Count; i++)
                {
                    var xivMtrl = mtrlList[i];
                    if (xivMtrl.ColorSetData.Count > 0)
                    {
                        ViewportVM.ColorsetButtonEnabled = true;
                    }
                }


                await await Dispatcher.InvokeAsync(async () =>
                {
                    if (Model == null || InternalFilePath != Model.Source)
                    {
                        // User changed models.
                        return;
                    }

                    _Textures = textureList;
                    await ViewportVM.UpdateModel(Model, _Textures);
                });
            }
            catch(Exception ex)
            {
                // No-Op.
                Trace.WriteLine(ex);
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
            //var data = await Mdl.MakeUncompressedMdlFile(Model, InternalFilePath, false, tx);
            //return Mdl.GetXivMdl(data, InternalFilePath);
            return await Mdl.GetXivMdl(InternalFilePath, false, tx);

        }

        public override string GetDefaultSaveDirectory()
        {
            var start = base.GetDefaultSaveDirectory();
            var ext = "3D\\";
            var comb = Path.Combine(start, ext);
            var full = Path.GetFullPath(comb);
            Directory.CreateDirectory(full);
            return full;
        }

        protected override KeyValuePair<string, string> GetDefaultExtension()
        {
            return new KeyValuePair<string, string>(".fbx", "FBX Model");
        }


        #endregion

        private void FullModel_Click(object sender, RoutedEventArgs e)
        {
            if (Model == null || !Model.IsInternal) return;
            try
            {
                // Load a clean copy of the model.
                var ttmdl = (TTModel) Model.Clone();
                FullModelView.AddModel(ttmdl, _Textures, ReferenceItem as IItemModel);
            } catch (Exception ex)
            {
                this.ShowError("FMV Error", "An error occurred while loading the model to the FMV:\n\n" + ex.Message);
            }
        }


        private async Task<byte[]> ShowModelImportDialog(string externalPath, string internalPath, IItem referenceItem)
        {
            var race = IOUtil.GetRaceFromPath(InternalFilePath);
            var result = await ImportModelView.ImportModel(internalPath, referenceItem, externalPath, false, MainWindow.GetMainWindow());
            if ( result != null && result.Success)
            {
                var options = new SmartImportOptions();
                options.ModelOptions = result.ImportOptions;
                LastImportOptions = options;
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
        private void ZoomExtentsRequested(Viewport3DViewModel requestor, double animationTime, Rect3D? boundingBox)
        {
            try
            {
                if (boundingBox != null)
                {
                    Viewport.ZoomExtents(boundingBox.Value, animationTime);
                }
                else
                {
                    Viewport.ZoomExtents(animationTime);
                }
            }
            catch(Exception ex)
            {
                Trace.WriteLine(ex);
            }
        }
        #endregion

        private async void EditModel_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var tx = MainWindow.DefaultTransaction;
                var newModel = (TTModel)Model.Clone();

                // This is mostly used for powering the model scaling comparisons,
                // And as a file-path source for resolving a few root-related settings.
                var oldModel = await Mdl.GetTTModel(InternalFilePath, false, tx);

                var editorWindow = new ImportModelEditView(newModel, oldModel) { Owner = Window.GetWindow(this) };
                var result = editorWindow.ShowDialog();
                if (result == true)
                {
                    UnsavedChanges = true;
                    await LoadModel(newModel);
                }
            } catch(Exception ex)
            {
                // No-op.  SHould be handled internally already.
                Trace.WriteLine(ex);
            }
        }

        private void VisibleMeshChanged(object sender, int e)
        {
            OnPropertyChanged(nameof(ShowMaterialEnabled));
        }

        private string GetVisibleMaterial()
        {

            if (_ViewportVM == null || Model == null)
            {
                return null;
            }

            var totalNonSkinMaterials = Model.Materials.Where(x => !ModelModifiers.IsSkinMaterial(x)).ToList();

            string file = null;
            if(totalNonSkinMaterials.Count == 1)
            {
                // Only one viable material.
                file = _ChildFiles.FirstOrDefault(x => x.EndsWith(totalNonSkinMaterials[0]));
                if (string.IsNullOrEmpty(file))
                {
                    return null;
                }
                return file;
            }

            if (_ViewportVM.VisibleMesh < 0 && Model.MeshGroups.Count > 1)
            {
                return null;
            }

            var meshIdx = _ViewportVM.VisibleMesh >= 0 ? _ViewportVM.VisibleMesh : 0;
            if (meshIdx >= Model.MeshGroups.Count)
            {
                return null;
            }

            var mg = Model.MeshGroups[meshIdx];
            if (ModelModifiers.IsSkinMaterial(mg.Material))
            {
                // Resolving skin paths is sort of nonsensical since it can vary based on equipped character's race.
                return null;
            }

            file = _ChildFiles.FirstOrDefault(x => x.EndsWith(mg.Material));
            if (string.IsNullOrEmpty(file))
            {
                return null;
            }
            return file;
        }
        private async void ShowMaterial_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var file = GetVisibleMaterial();
                if (file == null)
                {
                    return;
                }
                await SimpleFileViewWindow.OpenFile(file);
            }
            catch(Exception ex)
            {
                Trace.WriteLine(ex);
            }
        }

        private async void ExportTextures_Click(object sender, RoutedEventArgs e)
        {
            var bf = new BetterFolderBrowser();
            bf.Title = "Select Export Folder";

            var path = Path.GetFullPath(Path.Combine(GetDefaultSaveDirectory() + "../RawTextures/"));
            Directory.CreateDirectory(path);
            bf.RootFolder = path;

            if (bf.ShowDialog() != DialogResult.OK)
            {
                return;
            }
            path = bf.SelectedFolder;


            try
            {
                await Task.Run(async () =>
                {
                    var set = 1;
                    var im = ReferenceItem as IItemModel;
                    if (im != null)
                    {
                        set = await Imc.GetMaterialSetId(im, false, MainWindow.DefaultTransaction);
                    }
                    Model.Source = InternalFilePath;
                    await Mdl.ExportAllTextures(Model, bf.SelectedPath, set, MainWindow.DefaultTransaction);
                });
            } catch(Exception ex)
            {
                this.ShowError("Export Error", "An error occurred while exporting the textures:\n\n" + ex.Message);
            }
        }

        private async void AddModel_Click(object sender, RoutedEventArgs e)
        {
            AddModelContextMenu.PlacementTarget = AddModelGrid;
            AddModelContextMenu.IsOpen = true;
        }
        private async void AddExternalModel_Click(object sender, RoutedEventArgs e)
        {
            var ofd = GetOpenDialog();
            if (ofd.ShowDialog() != DialogResult.OK)
            {
                return;
            }

            try
            {
                var result = await ImportModelView.ImportModel(InternalFilePath, ReferenceItem, ofd.FileName, true, Window.GetWindow(this));
                if (result == null || !result.Success || result.Model == null)
                {
                    return;
                }

                var nonSkinMap = Model.Materials.FirstOrDefault(m => !ModelModifiers.IsSkinMaterial(m));
                if (nonSkinMap == null)
                {
                    nonSkinMap = Model.Materials[0];
                }

                foreach (var m in result.Model.MeshGroups)
                {
                    Model.MeshGroups.Add(m);
                    m.Material = nonSkinMap;
                }

                UnsavedChanges = true;
                _ = await Dispatcher.InvokeAsync(async () =>
                {
                    await UpdateVisual();
                });
            }
            catch (Exception ex)
            {
                // Don't think we can ever hit this, but safety.
                Trace.WriteLine(ex);
            }
        }

        private void AddFfxivModel_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(InternalFilePath) || Model == null)
            {
                return;
            }

            if (UnsavedChanges)
            {
                if (!this.ConfirmDiscardChanges(InternalFilePath))
                {
                    return;
                }
            }

            var wind = new MergeModelsDialog(InternalFilePath)
            {
                Owner = Window.GetWindow(this)
            };

            wind.ShowDialog();
        }

        private void OtherActions_Click(object sender, RoutedEventArgs e)
        {

            OtherActionsContextMenu.PlacementTarget = OtherActionsGrid;
            OtherActionsContextMenu.IsOpen = true;
        }

        private async void ExportPbrTextures_Click(object sender, RoutedEventArgs e)
        {
            var bf = new BetterFolderBrowser();
            bf.Title = "Select Export Folder";

            var path = Path.GetFullPath(Path.Combine(GetDefaultSaveDirectory(), "PbrTextures"));
            Directory.CreateDirectory(path);
            bf.RootFolder = path;

            if (bf.ShowDialog() != DialogResult.OK)
            {
                return;
            }

            path = bf.SelectedFolder;

            // Because the export for model expects an actual file path, not a folder path.
            path = Path.GetFullPath(Path.Combine(path, "asdf.fbx"));


            try
            {
                await Task.Run(async () =>
                {
                    var set = 1;
                    var im = ReferenceItem as IItemModel;
                    if (im != null)
                    {
                        set = await Imc.GetMaterialSetId(im, false, MainWindow.DefaultTransaction);
                    }
                    Model.Source = InternalFilePath;

                    await Mdl.ExportMaterialsForModel(Model, path, true, set, XivRace.All_Races, MainWindow.DefaultTransaction);
                });
            }
            catch (Exception ex)
            {
                this.ShowError("Export Error", "An error occurred while exporting the textures:\n\n" + ex.Message);
            }
        }
    }
}
