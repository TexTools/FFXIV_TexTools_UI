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

        private byte[] RawMdl;

        public ModelFileControl()
        {
            DataContext = this;
            InitializeComponent();
            ViewportVM = new Viewport3DViewModel(null);

            Viewport.DataContext = ViewportVM;

            if (Configuration.EnvironmentConfiguration.TT_Unshared_Rendering)
                _CanvasRenderer = new Helpers.ViewportCanvasRenderer(Viewport, AlternateViewportCanvas);
            ViewType = EFileViewType.Editor;

        }

        public override async Task INTERNAL_ClearFile()
        {
            Model = null;
            ViewportVM.ClearModels();
        }

        protected override async Task<byte[]> INTERNAL_CreateUncompressedFile(string externalFile, string internalFile, IItem referenceItem)
        {
            var data = await ImportModel(externalFile, internalFile, referenceItem);
            return data;
        }

        protected override async Task<byte[]> INTERNAL_GetUncompressedData()
        {
            return RawMdl;
        }

        protected override async Task<bool> INTERNAL_LoadFile(byte[] data)
        {
            RawMdl = data;

            // The data coming in here is an uncompressed .mdl file.
            Model = Mdl.GetTTModel(data, InternalFilePath);

            await UpdateVisual();
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

            var tx = MainWindow.DefaultTransaction;

            foreach(var mat in _MaterialPaths)
            {
                if (changedFile.EndsWith(mat))
                {
                    return true;
                }

                var textures = await XivCache.GetChildFiles(mat, tx);
                foreach(var tex in textures)
                {
                    if(changedFile == tex)
                    {
                        return true;
                    }
                }
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
                
                TransparencyToggle = false;
                FmvEnabled = false;


                ModelModifiers.ApplyShapes(Model, ActiveShapes);

                var materials = await GetMaterials();

                ViewportVM.UpdateModel(Model, materials);

                ReflectionValue = ViewportVM.SpecularShine;

                Viewport.ZoomExtents();


                FmvEnabled = true;
                // Disable FMV button if we're an unsupported type.
                if (Model.IsInternal)
                {
                    var modelRoot = await XivCache.GetFirstRoot(Model.Source);
                    if (modelRoot == null ||
                        modelRoot.Info.PrimaryType == XivItemType.demihuman
                        || modelRoot.Info.PrimaryType == XivItemType.monster
                        || modelRoot.Info.PrimaryType == XivItemType.weapon)
                    {
                        FmvEnabled = false;
                    }
                }

                if (!KeepCameraChecked)
                {
                    Viewport.ZoomExtents();
                }

                ShowModelStatus(UIStrings.ModelStatus_UpdateSuccess);
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

        /// <summary>
        /// Gets the materials for the model
        /// </summary>
        /// <returns>A dictionary containing the mesh number(key) and the associated texture data (value)</returns>
        private async Task<Dictionary<int, ModelTextureData>> GetMaterials()
        {
            var textureDataDictionary = new Dictionary<int, ModelTextureData>();
            if (Model == null) return textureDataDictionary;

            var tx = MainWindow.DefaultTransaction;

            var mtrlDictionary = new Dictionary<int, XivMtrl>();

            var race = IOUtil.GetRaceFromPath(InternalFilePath);

            var root = await XivCache.GetFirstRoot(InternalFilePath);
            if(root == null)
            {
                return textureDataDictionary;
            }

            var set = 1;
            if (ReferenceItem != null && Imc.UsesImc(root))
            {
                var asIm = ReferenceItem as IItemModel;
                if(asIm != null)
                {
                    set = await Imc.GetMaterialSetId(asIm);
                }
            }

            _MaterialSet = set;
            _MaterialPaths = await Mdl.GetReferencedMaterialPaths(Model.Materials, InternalFilePath, set, false, true, tx);
            var fullMats = _MaterialPaths;

            var materialNum = 0;
            foreach (var mtrlFilePath in fullMats)
            {
                var filePath = mtrlFilePath;

                if (ModelModifiers.IsSkinMaterial(mtrlFilePath)) {
                    // Adjust skin materials to point to the user's preferred race.

                    var file = "/" + Path.GetFileName(mtrlFilePath);
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
                        filePath = mtrlFilePath.Replace("c" + raceString, "c" + settingsRace.GetRaceCode()).Replace("b" +body, "b" +settingsBody);
                    }
                    else
                    {
                        // Just use item race.
                        filePath = mtrlFilePath.Replace("c" + raceString, "c" + race.GetRaceCode()).Replace("b" + body, "b0001");
                    }
                }


                var mtrlData = await Mtrl.GetXivMtrl(filePath, false, tx);

                if (mtrlData == null)
                {
                    continue;
                }

                if (mtrlData.ShaderPackRaw.Contains("colorchange"))
                {
                    //hasColorChangeShader = true;
                }

                mtrlDictionary.Add(materialNum, mtrlData);
                materialNum++;
            }


            //ColorsetVisibility = Visibility.Collapsed;
            foreach (var xivMtrl in mtrlDictionary)
            {
                if (xivMtrl.Value.ColorSetData.Count > 0)
                {
                    ColorsetVisibility = Visibility.Visible;
                }

                var colors = ModelTexture.GetCustomColors();
                colors.InvertNormalGreen = false;

                var modelMaps = await ModelTexture.GetModelMaps(xivMtrl.Value, colors, HighlightedColorsetRow, tx);
                textureDataDictionary.Add(xivMtrl.Key, modelMaps);
            }

            return textureDataDictionary;
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

        public int HighlightedColorsetRow = -1;
        private List<string> ActiveShapes = new List<string>();
        private async void HighlightColorsetButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var wind = new HighilightedColorsetSelection(HighlightedColorsetRow) { Owner = MainWindow.GetMainWindow() };
                wind.WindowStartupLocation = WindowStartupLocation.CenterOwner;

                var result = wind.ShowDialog();

                if (result != true) return;

                HighlightedColorsetRow = wind.SelectedRow;
                await UpdateVisual();
            }
            catch
            {
                //No Op
            }
        }
        private async void OpenShapesMenu_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (Model == null || !Model.HasShapeData) return;

                var wind = new ApplyShapesView(Model, ActiveShapes) { Owner = MainWindow.GetMainWindow() };
                wind.WindowStartupLocation = WindowStartupLocation.CenterOwner;

                var result = wind.ShowDialog();

                if (result != true) return;

                ActiveShapes = wind.SelectedShapes;
                await UpdateVisual();
            }
            catch
            {
                //No op
            }
        }

        private void ToggleFlyout(object sender, RoutedEventArgs e)
        {
            FlyoutOpen = !FlyoutOpen;
        }

        private bool _FlyoutOpen;
        public bool FlyoutOpen
        {
            get => _FlyoutOpen;
            set
            {
                _FlyoutOpen = value;
                OnPropertyChanged(nameof(FlyoutOpen));
            }
        }


        private float _LightingXValue;
        public float LightingXValue
        {
            get => _LightingXValue;
            set
            {
                _LightingXValue = value;
                ViewportVM.UpdateLighting(value, _CheckedLight, "X");
                LightXLabel = $"X  |  {value:0.#}";
                OnPropertyChanged(nameof(LightingXValue));
            }
        }

        public float _LightingYValue;
        public float LightingYValue
        {
            get => _LightingYValue;
            set
            {
                _LightingYValue = value;
                ViewportVM.UpdateLighting(value, _CheckedLight, "Y");
                LightYLabel = $"Y  |  {value:0.#}";
                OnPropertyChanged(nameof(LightingYValue));
            }
        }

        public float _lightingZValue;
        public float LightingZValue
        {
            get => _lightingZValue;
            set
            {
                _lightingZValue = value;
                ViewportVM.UpdateLighting(value, _CheckedLight, "Z");
                LightZLabel = $"Z  |  {value:0.#}";
                OnPropertyChanged(nameof(LightingZValue));
            }
        }

        public int _ReflectionValue;
        public int ReflectionValue
        {
            get => _ReflectionValue;
            set
            {
                _ReflectionValue = value;
                ViewportVM.UpdateReflection(value);
                ReflectionLabel = $"{UIStrings.Reflection}  |  {value}";
                OnPropertyChanged(nameof(ReflectionValue));
            }
        }

        private int _CheckedLight;

        public bool _Light1Check;
        public bool Light1Check
        {
            get => _Light1Check;
            set
            {
                _Light1Check = value;
                if (value)
                {
                    _CheckedLight = 0;
                    UpdateLights();
                }
            }
        }

        public bool _Light2Check;
        public bool Light2Check
        {
            get => _Light2Check;
            set
            {
                _Light2Check = value;
                if (value)
                {
                    _CheckedLight = 1;
                    UpdateLights();
                }
            }
        }

        public bool _Light3Check;
        public bool Light3Check
        {
            get => _Light3Check;
            set
            {
                _Light3Check = value;
                if (value)
                {
                    _CheckedLight = 2;
                    UpdateLights();
                }
            }
        }

        public bool _LightRenderToggle;
        public bool LightRenderToggle
        {
            get => _LightRenderToggle;
            set
            {
                _LightRenderToggle = value;
                ViewportVM.RenderLight3 = value;
                OnPropertyChanged(nameof(LightRenderToggle));
            }
        }

        public Visibility _LightToggleVisibility;
        public Visibility LightToggleVisibility
        {
            get => _LightToggleVisibility;
            set
            {
                _LightToggleVisibility = value;
                OnPropertyChanged(nameof(LightToggleVisibility));
            }
        }

        public string _LightXLabel;
        public string LightXLabel
        {
            get => _LightXLabel;
            set
            {
                _LightXLabel = value;
                OnPropertyChanged(nameof(LightXLabel));
            }
        }

        public string _LightYLabel;
        public string LightYLabel
        {
            get => _LightYLabel;
            set
            {
                _LightYLabel = value;
                OnPropertyChanged(nameof(LightYLabel));
            }
        }

        public string _LightZLabel;
        public string LightZLabel
        {
            get => _LightZLabel;
            set
            {
                _LightZLabel = value;
                OnPropertyChanged(nameof(LightZLabel));
            }
        }

        public string _reflectionLabel;
        public string ReflectionLabel
        {
            get => _reflectionLabel;
            set
            {
                _reflectionLabel = value;
                OnPropertyChanged(nameof(ReflectionLabel));
            }
        }

        public bool _TransparencyToggle;
        public bool TransparencyToggle
        {
            get => _TransparencyToggle;
            set
            {
                _TransparencyToggle = value;
                ViewportVM.UpdateTransparency(value);
                OnPropertyChanged(nameof(TransparencyToggle));
            }
        }

        public bool _CullModeToggle;
        public bool CullModeToggle
        {
            get => _CullModeToggle;
            set
            {
                _CullModeToggle = value;
                UpdateCullMode(value);
                OnPropertyChanged(nameof(CullModeToggle));
            }
        }

        public bool _KeepCameraChecked;
        public bool KeepCameraChecked
        {
            get => _KeepCameraChecked;
            set
            {
                _KeepCameraChecked = value;
                OnPropertyChanged(nameof(KeepCameraChecked));
            }
        }

        public Visibility _FmvVisibility;
        public Visibility FmvVisibility
        {
            get => _FmvVisibility;
            set
            {
                _FmvVisibility = value;
                OnPropertyChanged(nameof(FmvVisibility));
            }
        }

        public Visibility _ColorsetVisibility;
        public Visibility ColorsetVisibility
        {
            get => _ColorsetVisibility;
            set
            {
                _ColorsetVisibility = value;
                OnPropertyChanged(nameof(ColorsetVisibility));
            }
        }
        /// <summary>
        /// Updates the Cull Mode
        /// </summary>
        private void UpdateCullMode(bool noneCull)
        {
            ViewportVM.UpdateCullMode(noneCull);

            Settings.Default.Cull_Mode = noneCull ? UIStrings.None : UIStrings.Back;

            Settings.Default.Save();
        }

        /// <summary>
        /// Update the light position values on the sliders
        /// </summary>
        private void UpdateLights()
        {
            var lightValues = ViewportVM.GetLightOffset(_CheckedLight);

            LightingXValue = (float)lightValues.x;
            LightXLabel = $"X  |  {lightValues.x:0.#}";
            LightingYValue = (float)lightValues.y;
            LightYLabel = $"Y  |  {lightValues.y:0.#}";
            LightingZValue = (float)lightValues.z;
            LightZLabel = $"Z  |  {lightValues.z:0.#}";

            LightToggleVisibility = _CheckedLight == 2 ? Visibility.Visible : Visibility.Collapsed;
        }

        /// <summary>
        /// Resets the light position values for the sliders
        /// </summary>
        public void ResetLightValues()
        {
            if (_CheckedLight == 2) return;

            LightingXValue = 0;
            LightXLabel = "X  |  0";
            LightingYValue = 0;
            LightYLabel = "Y  |  0";
            LightingZValue = 0;
            LightZLabel = "Z  |  0";
        }


        private bool _FmvEnabled;
        public bool FmvEnabled
        {
            get => _FmvEnabled;
            set
            {
                _FmvEnabled = value;
                OnPropertyChanged(nameof(FmvEnabled));
            }
        }

        private string GetItem3DFolder()
        {
            return Path.Combine(GetDefaultSaveDirectory(), "/3D/");
        }


        private async Task<XivMdl> GetRawMdl()
        {
            return (await Model.GetRawMdl(MainWindow.DefaultTransaction));
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

        private void OpenModelInspector(object obj)
        {
            if (Model != null)
            {
                var task = Task.Run(GetRawMdl);
                task.Wait();
                var mdl = task.Result;
                var modelInspector = new ModelInspector(mdl);
                modelInspector.Owner = Window.GetWindow(this);
                modelInspector.ShowDialog();
            }
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

                await fmv.AddModel(ttmdl, await GetMaterials(), ReferenceItem as IItemModel, race);
            } catch (Exception ex)
            {
                this.ShowError("FMV Error", "An error occurred while loading the model to the FMV:\n\n" + ex.Message);
            }
        }


        private async Task<byte[]> ImportModel(string externalPath, string internalPath, IItem referenceItem)
        {
            var gameDirectory = new DirectoryInfo(Settings.Default.FFXIV_Directory);

            var race = IOUtil.GetRaceFromPath(InternalFilePath);

            var result = await ImportModelView.ImportModel(internalPath, referenceItem, true, externalPath, MainWindow.GetMainWindow());

            if ( result != null && result.Success)
            {
                return result.Data;
            }
            return null;
        }

    }
}
