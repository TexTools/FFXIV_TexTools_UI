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

using FFXIV_TexTools.Helpers;
using FFXIV_TexTools.Properties;
using FFXIV_TexTools.Resources;
using FFXIV_TexTools.Views.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using xivModdingFramework.General.Enums;
using xivModdingFramework.Helpers;
using xivModdingFramework.Items.Interfaces;
using xivModdingFramework.Materials.FileTypes;
using xivModdingFramework.Models.DataContainers;
using xivModdingFramework.Models.ModelTextures;
using xivModdingFramework.SqPack.FileTypes;
using xivModdingFramework.Variants.FileTypes;

namespace FFXIV_TexTools.ViewModels
{
    public class FullModelViewModel : INotifyPropertyChanged
    {

        private FullModelViewport3DViewModel _viewPortVM;
        private float _lightingXValue, _lightingYValue, _lightingZValue;
        private string _lightXLabel = "X  |  0", _lightYLabel = "Y  |  0", _lightZLabel = "Z  |  0", _reflectionLabel = $"{UIStrings.Reflection}  |  1", _modToggleText = UIStrings.Enable_Disable, _modelStatusLabel;
        private int _checkedLight, _reflectionValue, _selectedSkeletonIndex, _selectedModelIndex;
        private bool _flyoutOpen, _lightRenderToggle, _light1Check = true, _light2Check, _light3Check, _transparencyToggle, _cullModeToggle, _showSkeleton, _skeletonComboboxEnabled, _exportEnabled, _removeEnabled, _isFirstModel;
        private Visibility _lightToggleVisibility = Visibility.Collapsed;
        private FullModelView _fullModelView;
        private ObservableCollection<ComboBoxData> _skeletonComboBoxData = new ObservableCollection<ComboBoxData>();
        private ObservableCollection<string> _modelList = new ObservableCollection<string>();
        private ComboBoxData _selectedSkeleton;
        private XivRace _previousRace;

        public FullModelViewModel(FullModelView fullModelView)
        {
            _fullModelView = fullModelView;
            ViewPortVM = new FullModelViewport3DViewModel(this);
            FillSkeletonComboBox();
            SelectedSkeletonIndex = 0;
            SkeletonComboboxEnabled = true;
        }

        #region Properties

        /// <summary>
        /// The 3D viewport viewmodel which handles the model display
        /// </summary>
        public FullModelViewport3DViewModel ViewPortVM
        {
            get => _viewPortVM;
            set
            {
                _viewPortVM = value;
                NotifyPropertyChanged(nameof(ViewPortVM));
            }
        }

        /// <summary>
        /// The collection of skeletons available to apply deforms
        /// </summary>
        public ObservableCollection<ComboBoxData> Skeletons
        {
            get => _skeletonComboBoxData;
            set { _skeletonComboBoxData = value; NotifyPropertyChanged(nameof(Skeletons)); }
        }

        /// <summary>
        /// The selected skeleton
        /// </summary>
        public ComboBoxData SelectedSkeleton
        {
            get => _selectedSkeleton;
            set
            {
                // save the previously selected skeleton for future comparison
                if (_selectedSkeleton != null)
                {
                    _previousRace = _selectedSkeleton.XivRace;
                }

                _selectedSkeleton = value;
                NotifyPropertyChanged(nameof(SelectedSkeleton));

                // update the skeleton to the new selected skeleton
                if (value != null)
                {
                    UpdateSkeleton(value.XivRace);
                }
            }
        }

        /// <summary>
        /// The selected skeleton index
        /// </summary>
        public int SelectedSkeletonIndex
        {
            get => _selectedSkeletonIndex;
            set
            {
                _selectedSkeletonIndex = value;
                NotifyPropertyChanged(nameof(SelectedSkeletonIndex));
            }
        }

        /// <summary>
        /// Flag for enabling or disabling the Skeleton ComboBox
        /// </summary>
        public bool SkeletonComboboxEnabled
        {
            get => _skeletonComboboxEnabled;
            set
            {
                _skeletonComboboxEnabled = value;
                NotifyPropertyChanged(nameof(SkeletonComboboxEnabled));
            }
        }

        /// <summary>
        /// The list of models by item type
        /// </summary>
        public ObservableCollection<string> ModelList
        {
            get => _modelList;
            set
            {
                _modelList = value;
                NotifyPropertyChanged(nameof(ModelList));
            }
        }

        /// <summary>
        /// The selected model index
        /// </summary>
        public int SelectedModelIndex
        {
            get => _selectedModelIndex;
            set
            {
                _selectedModelIndex = value;
                NotifyPropertyChanged(nameof(SelectedModelIndex));
            }
        }

        /// <summary>
        /// Flag for enabling or disabling the export button
        /// </summary>
        public bool ExportEnabled
        {
            get => _exportEnabled;
            set
            {
                _exportEnabled = value;
                NotifyPropertyChanged(nameof(ExportEnabled));
            }
        }

        /// <summary>
        /// Flag for enabling or disabling the remove button
        /// </summary>
        public bool RemoveEnabled
        {
            get => _removeEnabled;
            set
            {
                _removeEnabled = value;
                NotifyPropertyChanged(nameof(RemoveEnabled));
            }
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Adds a model to the view
        /// </summary>
        /// <param name="ttModel">The model to add</param>
        /// <param name="_materialDictionary">The dictionary with materials</param>
        /// <param name="item">The item associated with the model being added</param>
        /// <param name="race">The race of the model being added</param>
        public async void AddModelToView(TTModel ttModel, Dictionary<int, ModelTextureData> _materialDictionary, IItemModel item, XivRace modelRace)
        {
            // Sets the skeleton to the same as the race of the first model added
            if (ViewPortVM.Models.Count < 1)
            {
                _isFirstModel = true;
                var firstModelSkeleton = from s in Skeletons where s.XivRace == modelRace select s;
                SelectedSkeleton = firstModelSkeleton.FirstOrDefault();
            }

            // Add the item type to the model list
            var itemType = $"{item.PrimaryCategory}_{item.SecondaryCategory}";
            var itemDisplay = $"{item.Name} ({itemType})";

            if (!ModelList.Any(x => x.Contains(itemType)))
            {
                ModelList.Add(itemDisplay);
                SelectedModelIndex = ModelList.Count - 1;
            }
            else
            {
                var removeItem = (from iDisplay in ModelList where iDisplay.Contains(itemType) select iDisplay).FirstOrDefault();
                ModelList.Remove(removeItem);
                ModelList.Add(itemDisplay);
                SelectedModelIndex = ModelList.IndexOf(itemDisplay);
            }

            // Disable changing the skeleton while model and viewport update
            SkeletonComboboxEnabled = false;

            // Update body textures if the model being added is a different race than the selected skeleton race
            var skinRace = SelectedSkeleton.XivRace.GetSkinRace();

            if (modelRace != skinRace)
            {
                await UpdateBodyTextures(ttModel, item, _materialDictionary);
            }

            ViewPortVM.UpdateModel(ttModel, _materialDictionary, item, modelRace, SelectedSkeleton.XivRace);
            SkeletonComboboxEnabled = true;

            ExportEnabled = true;
            RemoveEnabled = true;
            _isFirstModel = false;
            _fullModelView.viewport3DX.ZoomExtents();
        }

        #endregion


        #region Private Methods
        /// <summary>
        /// Fills the combo box with the available race deforms
        /// </summary>
        private void FillSkeletonComboBox()
        {
            var deformRaceList = new List<XivRace>
            {
                XivRace.Hyur_Midlander_Male,
                XivRace.Hyur_Midlander_Female,
                XivRace.Hyur_Highlander_Male,
                XivRace.Hyur_Highlander_Female,
                XivRace.Elezen_Male,
                XivRace.Elezen_Female,
                XivRace.Miqote_Male,
                XivRace.Miqote_Female,
                XivRace.Roegadyn_Male,
                XivRace.Roegadyn_Female,
                XivRace.Lalafell_Male,
                XivRace.Lalafell_Female,
                XivRace.AuRa_Male,
                XivRace.AuRa_Female,
                XivRace.Hrothgar,
                XivRace.Viera
            };

            foreach (var xivRace in deformRaceList)
            {
                Skeletons.Add(new ComboBoxData
                {
                    Name = $"{xivRace.GetDisplayName()} ({xivRace.GetRaceCode()})",
                    XivRace = xivRace
                });
            }
        }

        /// <summary>
        /// Updates the skeleton
        /// </summary>
        /// <param name="selectedSkeleton">The race of the selected skeleton</param>
        private async Task UpdateSkeleton(XivRace selectedSkeleton)
        {
            // Update only if races are different and it is not the first model being added
            if (_previousRace != selectedSkeleton  && !_isFirstModel)
            {
                // Disable changing the skeleton while model and viewport update
                SkeletonComboboxEnabled = false;

                // Update Body Textures for each model to the new race
                var skinRace = selectedSkeleton.GetSkinRace();
                if (_previousRace != skinRace)
                {
                    foreach (var shownModel in ViewPortVM.shownModels.Values)
                    {
                        await UpdateBodyTextures(shownModel.TtModel, shownModel.ItemModel, shownModel.ModelTextureData);
                    }
                }

                ViewPortVM.UpdateSkeleton(_previousRace, selectedSkeleton);

                SkeletonComboboxEnabled = true;
            }
        }

        /// <summary>
        /// Updates the body textures for a given model
        /// </summary>
        /// <param name="ttModel">The model to update the body textures for</param>
        /// <param name="item">The item associated with the model</param>
        /// <param name="materialDictionary">The dictionary of materials for the current model</param>
        private async Task UpdateBodyTextures(TTModel ttModel, IItemModel item, Dictionary<int, ModelTextureData> materialDictionary)
        {

            var gameDirectory = new DirectoryInfo(Settings.Default.FFXIV_Directory);
            var _imc = new Imc(gameDirectory);
            var _mtrl = new Mtrl(gameDirectory, IOUtil.GetDataFileFromPath(ttModel.Source), XivLanguage.None);
            var _index = new Index(gameDirectory);

            // Determine which materials in the model need to be replaced
            var materialToReplace = string.Empty;
            var bodyMaterialIndex = 0;
            foreach (var material in ttModel.Materials)
            {
                if (material.Contains("b0001"))
                {
                    materialToReplace = material;
                    bodyMaterialIndex = ttModel.Materials.IndexOf(material);
                }
            }

            // Replace only if the model has a body texture
            if (!string.IsNullOrEmpty(materialToReplace))
            {
                // Current Race Code
                var raceCode = materialToReplace.Substring(materialToReplace.LastIndexOf('c') + 1, 4);
                // The closest race with a skin texture
                var skinRaceCode = SelectedSkeleton.XivRace.GetSkinRace().GetRaceCode();
                // New material path after replacing with target race
                var newMaterial = materialToReplace.Replace(raceCode, skinRaceCode);
                // Temp MDL path so that races match when getting mtrl path
                var tempMdlPath = ttModel.Source.Replace(raceCode, skinRaceCode);

                var mtrlVariant = 1;
                try
                {
                    mtrlVariant = (await _imc.GetImcInfo(item)).Variant;
                }
                catch (Exception ex)
                {
                    // No-op, defaulted to 1.
                }

                var mtrlPath = _mtrl.GetMtrlPath(tempMdlPath, newMaterial, mtrlVariant);
                var mtrlOffset = await _index.GetDataOffset(mtrlPath);
                var mtrl = await _mtrl.GetMtrlData(mtrlOffset, mtrlPath, 11);
                var modelMaps = await ModelTexture.GetModelMaps(gameDirectory, mtrl);

                materialDictionary[bodyMaterialIndex] = modelMaps;
            }
        }

        /// <summary>
        /// Clean up when window is closed
        /// </summary>
        public void CleanUp()
        {
            _skeletonComboBoxData.Clear();
            _modelList.Clear();
            ViewPortVM.CleanUp();
        }

        #endregion


        #region ViewPortOptions
        /// <summary>
        /// Resets the light position values for the sliders
        /// </summary>
        public void ResetLightValues()
        {
            if (_checkedLight == 2) return;

            LightingXValue = 0;
            LightXLabel = "X  |  0";
            LightingYValue = 0;
            LightYLabel = "Y  |  0";
            LightingZValue = 0;
            LightZLabel = "Z  |  0";
        }

        /// <summary>
        /// Update the light position values on the sliders
        /// </summary>
        private void UpdateLights()
        {
            var lightValues = ViewPortVM.GetLightOffset(_checkedLight);

            LightingXValue = (float)lightValues.x;
            LightXLabel = $"X  |  {lightValues.x:0.#}";
            LightingYValue = (float)lightValues.y;
            LightYLabel = $"Y  |  {lightValues.y:0.#}";
            LightingZValue = (float)lightValues.z;
            LightZLabel = $"Z  |  {lightValues.z:0.#}";

            LightToggleVisibility = _checkedLight == 2 ? Visibility.Visible : Visibility.Collapsed;
        }

        /// <summary>
        /// The X position of the selected light
        /// </summary>
        public float LightingXValue
        {
            get => _lightingXValue;
            set
            {
                _lightingXValue = value;
                ViewPortVM.UpdateLighting(value, _checkedLight, "X");
                LightXLabel = $"X  |  {value:0.#}";
                NotifyPropertyChanged(nameof(LightingXValue));
            }
        }

        /// <summary>
        /// The Y position of the selected light
        /// </summary>
        public float LightingYValue
        {
            get => _lightingYValue;
            set
            {
                _lightingYValue = value;
                ViewPortVM.UpdateLighting(value, _checkedLight, "Y");
                LightYLabel = $"Y  |  {value:0.#}";
                NotifyPropertyChanged(nameof(LightingYValue));
            }
        }

        /// <summary>
        /// The Z position of the selected light
        /// </summary>
        public float LightingZValue
        {
            get => _lightingZValue;
            set
            {
                _lightingZValue = value;
                ViewPortVM.UpdateLighting(value, _checkedLight, "Z");
                LightZLabel = $"Z  |  {value:0.#}";
                NotifyPropertyChanged(nameof(LightingZValue));
            }
        }

        /// <summary>
        /// The X position value label
        /// </summary>
        public string LightXLabel
        {
            get => _lightXLabel;
            set
            {
                _lightXLabel = value;
                NotifyPropertyChanged(nameof(LightXLabel));
            }
        }

        /// <summary>
        /// The Y position value label
        /// </summary>
        public string LightYLabel
        {
            get => _lightYLabel;
            set
            {
                _lightYLabel = value;
                NotifyPropertyChanged(nameof(LightYLabel));
            }
        }

        /// <summary>
        /// The Z position value label
        /// </summary>
        public string LightZLabel
        {
            get => _lightZLabel;
            set
            {
                _lightZLabel = value;
                NotifyPropertyChanged(nameof(LightZLabel));
            }
        }

        /// <summary>
        /// The open status of the flyout
        /// </summary>
        public bool FlyoutOpen
        {
            get => _flyoutOpen;
            set
            {
                _flyoutOpen = value;
                NotifyPropertyChanged(nameof(FlyoutOpen));
            }
        }

        /// <summary>
        /// The status of the light render toggle
        /// </summary>
        public bool LightRenderToggle
        {
            get => _lightRenderToggle;
            set
            {
                _lightRenderToggle = value;
                ViewPortVM.RenderLight3 = value;
                NotifyPropertyChanged(nameof(LightRenderToggle));
            }
        }

        /// <summary>
        /// The visibility of the light render toggle
        /// </summary>
        public Visibility LightToggleVisibility
        {
            get => _lightToggleVisibility;
            set
            {
                _lightToggleVisibility = value;
                NotifyPropertyChanged(nameof(LightToggleVisibility));
            }
        }

        /// <summary>
        /// The checked status of the light 1 radio button
        /// </summary>
        public bool Light1Check
        {
            get => _light1Check;
            set
            {
                _light1Check = value;
                if (value)
                {
                    _checkedLight = 0;
                    UpdateLights();
                }
            }
        }

        /// <summary>
        /// The checked status of the light 2 radio button
        /// </summary>
        public bool Light2Check
        {
            get => _light2Check;
            set
            {
                _light2Check = value;
                if (value)
                {
                    _checkedLight = 1;
                    UpdateLights();
                }
            }
        }

        /// <summary>
        /// The checked status of the light 3 radio button
        /// </summary>
        public bool Light3Check
        {
            get => _light3Check;
            set
            {
                _light3Check = value;
                if (value)
                {
                    _checkedLight = 2;
                    UpdateLights();
                }
            }
        }

        /// <summary>
        /// The current reflection value of the model
        /// </summary>
        public int ReflectionValue
        {
            get => _reflectionValue;
            set
            {
                _reflectionValue = value;
                ViewPortVM.UpdateReflection(value);
                ReflectionLabel = $"{UIStrings.Reflection}  |  {value}";
                NotifyPropertyChanged(nameof(ReflectionValue));
            }
        }

        /// <summary>
        /// The reflection value label
        /// </summary>
        public string ReflectionLabel
        {
            get => _reflectionLabel;
            set
            {
                _reflectionLabel = value;
                NotifyPropertyChanged(nameof(ReflectionLabel));
            }
        }

        /// <summary>
        /// The status of the transparency toggle
        /// </summary>
        public bool TransparencyToggle
        {
            get => _transparencyToggle;
            set
            {
                _transparencyToggle = value;
                ViewPortVM.UpdateTransparency(value);
                NotifyPropertyChanged(nameof(TransparencyToggle));
            }
        }

        /// <summary>
        /// The status of the cull mode toggle
        /// </summary>
        public bool CullModeToggle
        {
            get => _cullModeToggle;
            set
            {
                _cullModeToggle = value;
                UpdateCullMode(value);
                NotifyPropertyChanged(nameof(CullModeToggle));
            }
        }

        /// <summary>
        /// Updates the Cull Mode
        /// </summary>
        private void UpdateCullMode(bool noneCull)
        {
            ViewPortVM.UpdateCullMode(noneCull);

            Settings.Default.Cull_Mode = noneCull ? UIStrings.None : UIStrings.Back;

            Settings.Default.Save();
        }

        public bool ShowSkeleton
        {
            get => _showSkeleton;
            set
            {
                _showSkeleton = value;
                ViewPortVM.ToggleSkeleton(value);

                NotifyPropertyChanged(nameof(ShowSkeleton));
            }
        }

        #endregion


        #region Buttons

        public ICommand ViewOptionsCommand => new RelayCommand(ViewerOptions);

        public ICommand RemoveCommand => new RelayCommand(Remove);

        /// <summary>
        /// Remove a model from the viewport
        /// </summary>
        private void Remove(object obj)
        {
            if (ModelList.Count == 1)
            {
                ViewPortVM.ClearAll();
                ModelList.Clear();
                RemoveEnabled = false;
                ExportEnabled = false;
            }
            else
            {
                var modelToRemove = ModelList[SelectedModelIndex].Substring(ModelList[SelectedModelIndex].IndexOf('('))
                    .Trim('(').Trim(')');
                ViewPortVM.RemoveModel(modelToRemove);
                ModelList.RemoveAt(SelectedModelIndex);
                SelectedModelIndex = 0;
            }

        }


        /// <summary>
        /// Opens and closes the viewer option flyout
        /// </summary>
        private void ViewerOptions(object obj)
        {
            FlyoutOpen = !FlyoutOpen;
        }

        #endregion


        public class ComboBoxData
        {
            /// <summary>
            /// The name to display
            /// </summary>
            public string Name { get; set; }

            /// <summary>
            /// The selected race
            /// </summary>
            public XivRace XivRace { get; set; }
        }



        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void NotifyPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}