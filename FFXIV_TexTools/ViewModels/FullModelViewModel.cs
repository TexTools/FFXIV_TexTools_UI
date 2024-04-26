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
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using MahApps.Metro.Controls.Dialogs;
using xivModdingFramework.General.Enums;
using xivModdingFramework.Helpers;
using xivModdingFramework.Items.Categories;
using xivModdingFramework.Items.DataContainers;
using xivModdingFramework.Items.Interfaces;
using xivModdingFramework.Materials.FileTypes;
using xivModdingFramework.Models.DataContainers;
using xivModdingFramework.Models.Helpers;
using xivModdingFramework.Models.ModelTextures;
using xivModdingFramework.SqPack.FileTypes;
using xivModdingFramework.Variants.FileTypes;

using Index = xivModdingFramework.SqPack.FileTypes.Index;

namespace FFXIV_TexTools.ViewModels
{
    public class FullModelViewModel : INotifyPropertyChanged
    {

        private FullModelViewport3DViewModel _viewPortVM;
        private float _lightingXValue, _lightingYValue, _lightingZValue;
        private string _lightXLabel = "X  |  0", _lightYLabel = "Y  |  0", _lightZLabel = "Z  |  0", _reflectionLabel = $"{UIStrings.Reflection}  |  1", _modToggleText = UIStrings.Enable_Disable, _modelStatusLabel, _selectedFace;
        private int _checkedLight, _reflectionValue, _selectedSkeletonIndex, _selectedModelIndex, _selectedSkinIndex, _selectedSkin, _selectedFaceIndex;
        private bool _flyoutOpen, _lightRenderToggle, _light1Check = true, _light2Check, _light3Check, _transparencyToggle, _cullModeToggle, _showSkeleton, _skeletonComboboxEnabled, _skinComboboxEnabled, _exportEnabled, _removeEnabled, _isFirstModel, _faceComboboxEnabled;
        private Visibility _lightToggleVisibility = Visibility.Collapsed, _faceComboBoxVisibility = Visibility.Collapsed;
        private FullModelView _fullModelView;
        private ObservableCollection<ComboBoxData> _skeletonComboBoxData = new ObservableCollection<ComboBoxData>();
        private ObservableCollection<int> _skins = new ObservableCollection<int>();
        private ObservableCollection<string> _modelList = new ObservableCollection<string>();
        private ObservableCollection<string> _facesList = new ObservableCollection<string>();
        private ComboBoxData _selectedSkeleton;
        private XivRace _previousRace;
        private DirectoryInfo _gameDirectory;
        private Dictionary<XivRace, int[]> _charaRaceAndSkinDictionary;

        private bool _updateNeeded = true;

        public FullModelViewModel(FullModelView fullModelView)
        {
            _gameDirectory = new DirectoryInfo(Settings.Default.FFXIV_Directory);
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
        /// The collection of skins available to the skeleton
        /// </summary>
        public ObservableCollection<int> Skins
        {
            get => _skins;
            set
            {
                _skins = value;
                NotifyPropertyChanged(nameof(Skins));
            }
        }

        /// <summary>
        /// The selected skin
        /// </summary>
        public int SelectedSkin
        {
            get => _selectedSkin;
            set
            {
                _selectedSkin = value;
                NotifyPropertyChanged(nameof(SelectedSkin));

                UpdateAllSkin();
            }
        }

        /// <summary>
        /// The selected skin index
        /// </summary>
        public int SelectedSkinIndex
        {
            get => _selectedSkinIndex;
            set
            {
                _selectedSkinIndex = value;
                NotifyPropertyChanged(nameof(SelectedSkinIndex));
            }
        }

        /// <summary>
        /// Flag for enabling or disabling the Skin ComboBox
        /// </summary>
        public bool SkinComboboxEnabled
        {
            get => _skinComboboxEnabled;
            set
            {
                _skinComboboxEnabled = value;
                NotifyPropertyChanged(nameof(SkinComboboxEnabled));
            }
        }

        /// <summary>
        /// The collection of skins available to the skeleton
        /// </summary>
        public ObservableCollection<string> Faces
        {
            get => _facesList;
            set
            {
                _facesList = value;
                NotifyPropertyChanged(nameof(Faces));
            }
        }

        /// <summary>
        /// The selected face
        /// </summary>
        public string SelectedFace
        {
            get => _selectedFace;
            set
            {
                _selectedFace = value;
                NotifyPropertyChanged(nameof(SelectedFace));

                UpdateFaceTextures();
            }
        }

        /// <summary>
        /// The selected face index
        /// </summary>
        public int SelectedFaceIndex
        {
            get => _selectedFaceIndex;
            set
            {
                _selectedFaceIndex = value;
                NotifyPropertyChanged(nameof(SelectedFaceIndex));
            }
        }

        /// <summary>
        /// Flag for enabling or disabling the Face ComboBox
        /// </summary>
        public bool FaceComboboxEnabled
        {
            get => _faceComboboxEnabled;
            set
            {
                _faceComboboxEnabled = value;
                NotifyPropertyChanged(nameof(FaceComboboxEnabled));
            }
        }

        /// <summary>
        /// Flag for visibility of the Face ComboBox
        /// </summary>
        public Visibility FaceComboboxVisibility
        {
            get => _faceComboBoxVisibility;
            set
            {
                _faceComboBoxVisibility = value;
                NotifyPropertyChanged(nameof(FaceComboboxVisibility));
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

            var pc = await _fullModelView.ShowProgressAsync(UIStrings.ModelStatus_Loading, UIMessages.PleaseStandByMessage);
            // Sets the skeleton to the same as the race of the first model added
            if (ViewPortVM.Models.Count < 1)
            {
                _isFirstModel = true;
                if (_charaRaceAndSkinDictionary == null)
                {
                    await GetCharaSkinDictionary();
                }
                var firstModelSkeleton = from s in Skeletons where s.XivRace == modelRace select s;
                SelectedSkeleton = firstModelSkeleton.FirstOrDefault();

                Skins.Clear();

                foreach (var skinNum in _charaRaceAndSkinDictionary[SelectedSkeleton.XivRace.GetSkinRace()])
                {
                    Skins.Add(skinNum);
                }

                SelectedSkinIndex = 0;
            }

            // Show the face combo box if a face with different textures is added
            if (item.SecondaryCategory.Equals(XivStrings.Face) && (SelectedSkeleton.XivRace == XivRace.AuRa_Female || SelectedSkeleton.XivRace == XivRace.AuRa_Male ||
                SelectedSkeleton.XivRace == XivRace.Viera_Female || SelectedSkeleton.XivRace == XivRace.Viera_Male || SelectedSkeleton.XivRace == XivRace.Hrothgar_Male))
            {
                Faces.Clear();
                FaceComboboxVisibility = Visibility.Visible;
                FaceComboboxEnabled = true;
                FillFaceComboBox(SelectedSkeleton.XivRace);
                SelectedFaceIndex = 0;
            }

            await UpdateSkin(ttModel, _materialDictionary, item);

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

            // Disable changes while model and viewport update
            SkeletonComboboxEnabled = false;
            SkinComboboxEnabled = false;

            ViewPortVM.UpdateModel(ttModel, _materialDictionary, item, modelRace, SelectedSkeleton.XivRace);
            SkeletonComboboxEnabled = true;
            SkinComboboxEnabled = true;

            ExportEnabled = true;
            RemoveEnabled = true;
            _isFirstModel = false;
            _fullModelView.viewport3DX.ZoomExtents();

            await pc.CloseAsync();
        }

        /// <summary>
        /// Clean up when window is closed
        /// </summary>
        public void CleanUp()
        {
            _modelList.Clear();
            ViewPortVM.CleanUp();
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
                XivRace.Hrothgar_Male,
                XivRace.Hrothgar_Female,
                XivRace.Viera_Male,
                XivRace.Viera_Female
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
        /// Gets the dictionary of all skins available per race
        /// </summary>
        /// <returns>A dictionary containing an array of skin numbers per race</returns>
        private async Task GetCharaSkinDictionary()
        {
            var character = new Character(_gameDirectory, XivLanguages.GetXivLanguage(Settings.Default.Application_Language));

            var xivChara = new XivCharacter
            {
                DataFile = XivDataFile._04_Chara,
                Name = XivStrings.Body,
                PrimaryCategory = XivStrings.Character,
                SecondaryCategory = XivStrings.Body
            };

            _charaRaceAndSkinDictionary = await character.GetRacesAndNumbersForTextures(xivChara);
        }

        /// <summary>
        /// Fills the Face ComboBox
        /// </summary>
        /// <param name="selectedRace">The selected race</param>
        private void FillFaceComboBox(XivRace selectedRace)
        {
            switch (selectedRace)
            {
                case XivRace.AuRa_Male:
                case XivRace.AuRa_Female:
                    Faces.Add(XivStringRaces.Raen);
                    Faces.Add(XivStringRaces.Xaela);
                    break;
                case XivRace.Viera_Male:
                case XivRace.Viera_Female:
                    Faces.Add(XivStringRaces.Rava);
                    Faces.Add(XivStringRaces.Veena);
                    break;
                case XivRace.Hrothgar_Male:
                case XivRace.Hrothgar_Female:
                    Faces.Add(XivStringRaces.Helion);
                    Faces.Add(XivStringRaces.Lost);
                    break;
            }

        }

        /// <summary>
        /// Updates the skeleton
        /// </summary>
        /// <param name="selectedSkeleton">The race of the selected skeleton</param>
        private async Task UpdateSkeleton(XivRace selectedSkeleton)
        {
            // Update only if races are different and it is not the first model being added
            if (_previousRace != selectedSkeleton && !_isFirstModel)
            {
                var pc = await _fullModelView.ShowProgressAsync(UIMessages.UpdatingSkeletonTitle, UIMessages.PleaseStandByMessage);

                Skins.Clear();

                if (_charaRaceAndSkinDictionary != null)
                {
                    foreach (var skinNum in _charaRaceAndSkinDictionary[selectedSkeleton.GetSkinRace()])
                    {
                        Skins.Add(skinNum);
                    }
                }

                // Disable changes while model and viewport update
                SkeletonComboboxEnabled = false;
                SkinComboboxEnabled = false;
                RemoveEnabled = false;
                ExportEnabled = false;

                if (!_isFirstModel)
                {
                    ViewPortVM.UpdateSkeleton(_previousRace, selectedSkeleton);
                }

                SkeletonComboboxEnabled = true;
                SkinComboboxEnabled = true;
                RemoveEnabled = true;
                ExportEnabled = true;

                await pc.CloseAsync();

                SelectedSkinIndex = 0;
            }
        }

        /// <summary>
        /// Updates the skin for a model to the selected skin
        /// </summary>
        /// <param name="ttModel">The model to update the skin for</param>
        /// <param name="textureData">The texture data of the model</param>
        /// <param name="itemModel">The item model</param>
        private async Task UpdateSkin(TTModel ttModel, Dictionary<int, ModelTextureData> textureData, IItemModel itemModel)
        {
            var originalBodyIndex = GetBodyTextureIndex(ttModel);

            if (originalBodyIndex != -1)
            {
                var bodyReplacement = $"b{SelectedSkin.ToString().PadLeft(4, '0')}";
                ModelModifiers.FixUpSkinReferences(ttModel, SelectedSkeleton.XivRace, null, bodyReplacement);
                await UpdateBodyTextures(ttModel, itemModel, textureData);
            }

            // Change the tail texture for Au Ra
            if (itemModel.Name.Equals(XivStrings.Tail) && (SelectedSkeleton.XivRace == XivRace.AuRa_Female || SelectedSkeleton.XivRace == XivRace.AuRa_Male))
            {
                await UpdateTailTextures(ttModel, textureData);
            }
        }

        /// <summary>
        /// Updates all skin textures for the model to the selected skin
        /// </summary>
        private async Task UpdateAllSkin()
        {

            if (ViewPortVM.shownModels.Any())
            {
                var pc = await _fullModelView.ShowProgressAsync(UIMessages.UpdatingSkinTitle, UIMessages.PleaseStandByMessage);

                foreach (var shownModel in ViewPortVM.shownModels.Values)
                {
                    var originalBodyIndex = GetBodyTextureIndex(shownModel.TtModel);

                    if (originalBodyIndex != -1)
                    {
                        var bodyReplacement = $"b{SelectedSkin.ToString().PadLeft(4, '0')}";
                        ModelModifiers.FixUpSkinReferences(shownModel.TtModel, SelectedSkeleton.XivRace, null, bodyReplacement);
                        await UpdateBodyTextures(shownModel.TtModel, shownModel.ItemModel, shownModel.ModelTextureData);
                    }

                    // Change the tail texture for Au Ra
                    if (shownModel.ItemModel.Name.Equals(XivStrings.Tail) && (SelectedSkeleton.XivRace == XivRace.AuRa_Female || SelectedSkeleton.XivRace == XivRace.AuRa_Male))
                    {
                        await UpdateTailTextures(shownModel.TtModel, shownModel.ModelTextureData);
                    }
                }

                ViewPortVM.UpdateSkin(SelectedSkeleton.XivRace);

                await pc.CloseAsync();
            }
        }

        /// <summary>
        /// Gets the index of the body material
        /// </summary>
        /// <param name="ttModel">The TTModel</param>
        /// <returns>The index of the body material</returns>
        private int GetBodyTextureIndex(TTModel ttModel)
        {
            foreach (var material in ttModel.Materials)
            {
                var bodyRegex = new Regex("(b[0-9]{4})");

                var body = "b0001";

                var result = bodyRegex.Match(material);
                if (result.Success)
                {
                    body = result.Value;
                }

                if (material.Contains(body))
                {
                    return ttModel.Materials.IndexOf(material);
                }
            }

            return -1;
        }

        /// <summary>
        /// Gets the body number from the model
        /// </summary>
        /// <param name="ttModel">The TT model</param>
        /// <returns>The body number</returns>
        private string GetBodyNum(TTModel ttModel)
        {
            foreach (var material in ttModel.Materials)
            {
                var bodyRegex = new Regex("(b[0-9]{4})");

                var result = bodyRegex.Match(material);
                if (result.Success)
                {
                    return result.Value;
                }
            }

            return string.Empty;
        }

        /// <summary>
        /// Gets the tail number from the model
        /// </summary>
        /// <param name="ttModel">The TT model</param>
        /// <returns>The body number</returns>
        private string GetTailNum(TTModel ttModel)
        {
            var bodyRegex = new Regex("(t[0-9]{4})");

            var result = bodyRegex.Match(ttModel.Materials[0]);

            return result.Success ? result.Value : string.Empty;
        }

        /// <summary>
        /// Gets the face number from the model
        /// </summary>
        /// <param name="ttModel">The TT Model</param>
        /// <returns>the face number</returns>
        private string GetFaceNum(TTModel ttModel)
        {
            var faceRegex = new Regex("(f[0-9]{4})");

            var result = faceRegex.Match(ttModel.Materials[0]);
            if (result.Success)
            {
                return result.Value;
            }

            return string.Empty;
        }

        /// <summary>
        /// Updates the body textures for a given model
        /// </summary>
        /// <param name="ttModel">The model to update the body textures for</param>
        /// <param name="item">The item associated with the model</param>
        /// <param name="materialDictionary">The dictionary of materials for the current model</param>
        private async Task UpdateBodyTextures(TTModel ttModel, IItemModel item, Dictionary<int, ModelTextureData> materialDictionary)
        {
            var _imc = new Imc(_gameDirectory);
            var _mtrl = new Mtrl(_gameDirectory);
            var _index = new Index(_gameDirectory);

            // Determine which materials in the model need to be replaced
            var bodyMaterialIndex = GetBodyTextureIndex(ttModel);
            var newMaterial = ttModel.Materials[bodyMaterialIndex];
            var currBodyNum = GetBodyNum(ttModel);
            var newBodyNum = $"b{SelectedSkin.ToString().PadLeft(4, '0')}";
            var newTailNum = string.Empty;

            // Change the tail texture for Au Ra
            if (item.Name.Equals(XivStrings.Tail) && (SelectedSkeleton.XivRace == XivRace.AuRa_Female || SelectedSkeleton.XivRace == XivRace.AuRa_Male))
            {
                var currTailNum = GetTailNum(ttModel);


                newTailNum = SelectedSkin > 100 ? $"t010{currTailNum}" : $"t000{currTailNum}";
            }

            // Replace the body number with the selected one
            if (!currBodyNum.Equals(newBodyNum))
            {
                newMaterial = newMaterial.Replace(currBodyNum, newBodyNum);
            }

            var skinRaceCode = SelectedSkeleton.XivRace.GetSkinRace().GetRaceCode();

            // Current Race Code
            var raceCode = ttModel.Source.Substring(ttModel.Source.LastIndexOf('c') + 1, 4);

            // Temp MDL path so that races match when getting mtrl path
            var tempMdlPath = ttModel.Source.Replace(raceCode, skinRaceCode);

            var mtrlVariant = 1;
            try
            {
                mtrlVariant = (await _imc.GetImcInfo(item)).MaterialSet;
            }
            catch (Exception ex)
            {
                // No-op, defaulted to 1.
            }
            var bodyRegex = new Regex("(b[0-9]{4})");

            var body = "b0001";
            foreach (var m in materialDictionary.Values)
            {
                var result = bodyRegex.Match(m.MaterialPath);
                if (result.Success)
                {
                    body = result.Value;
                }
            }

            try
            {
                var bTex = (from m in materialDictionary.Values where m.MaterialPath.Contains(body) select m)
                    .FirstOrDefault();
                bTex.MaterialPath = newMaterial;

                var mtrlPath = _mtrl.GetMtrlPath(tempMdlPath, newMaterial, mtrlVariant);
                var mtrlOffset = await _index.GetDataOffset(mtrlPath);
                var mtrl = await _mtrl.GetMtrlData(mtrlOffset, mtrlPath);

                var colors = ModelTexture.GetCustomColors();
                colors.InvertNormalGreen = false;
                var modelMaps = await ModelTexture.GetModelMaps(_gameDirectory, mtrl, colors);

                // Reindex the material dictionary as materials may have sorted differently
                ReIndexMaterialDictionary(ttModel, materialDictionary, modelMaps);
            }
            catch (Exception ex)
            {
                throw new Exception(string.Format(UIMessages.UpdateBodyTextureError, ex.Message));
            }
        }

        /// <summary>
        /// Updates the tail textures for a given model
        /// </summary>
        /// <param name="ttModel">The model to update the body textures for</param>
        /// <param name="item">The item associated with the model</param>
        /// <param name="materialDictionary">The dictionary of materials for the current model</param>
        private async Task UpdateTailTextures(TTModel ttModel, Dictionary<int, ModelTextureData> materialDictionary)
        {
            var _imc = new Imc(_gameDirectory);
            var _mtrl = new Mtrl(_gameDirectory);
            var _index = new Index(_gameDirectory);

            // Determine which materials in the model need to be replaced
            var currTailNum = GetTailNum(ttModel);
            var newTailNum = SelectedSkin > 100 ? $"t010{currTailNum.Substring(4)}" : $"t000{currTailNum.Substring(4)}";
            var newMaterial = ttModel.Materials[0].Replace(currTailNum, newTailNum);

            // Temp MDL path so that races match when getting mtrl path
            var tempMdlPath = ttModel.Source.Replace(currTailNum, newTailNum);

            foreach (var meshGroup in ttModel.MeshGroups)
            {
                meshGroup.Material = newMaterial;
            }

            try
            {
                var mtrlVariant = 1;
                materialDictionary[0].MaterialPath = newMaterial;

                var mtrlPath = _mtrl.GetMtrlPath(tempMdlPath, newMaterial, mtrlVariant);
                var mtrlOffset = await _index.GetDataOffset(mtrlPath);
                var mtrl = await _mtrl.GetMtrlData(mtrlOffset, mtrlPath);
                var colors = ModelTexture.GetCustomColors();
                colors.InvertNormalGreen = false;
                var modelMaps = await ModelTexture.GetModelMaps(_gameDirectory, mtrl, colors);

                materialDictionary[0] = modelMaps;
            }
            catch (Exception ex)
            {
                throw new Exception(string.Format(UIMessages.UpdateBodyTextureError, ex.Message));
            }
        }

        /// <summary>
        /// Updates the face textures to the selected clan
        /// </summary>
        private async Task UpdateFaceTextures()
        {
            ProgressDialogController pc = null;
            if (!_fullModelView.IsAnyDialogOpen)
            {
                pc = await _fullModelView.ShowProgressAsync(UIMessages.UpdatingFaceTitle, UIMessages.PleaseStandByMessage);
            }

            TTModel ttModel = null;
            Dictionary<int, ModelTextureData> materialDictionary = null;

            foreach (var shownModel in ViewPortVM.shownModels.Values)
            {
                if (shownModel.ItemModel.SecondaryCategory.Equals(XivStrings.Face))
                {
                    ttModel = shownModel.TtModel;
                    materialDictionary = shownModel.ModelTextureData;
                }
            }

            if (ttModel != null)
            {
                var _mtrl = new Mtrl(_gameDirectory);
                var _index = new Index(_gameDirectory);

                var faceRegex = new Regex("(f[0-9]{4})");
                var facePartRegex = new Regex("(_[a-z]{3}_)");
                var origFaceNum = faceRegex.Match(ttModel.Source).Value;
                var newFaceNum = origFaceNum;

                // Second clan face add 100
                if (SelectedFaceIndex == 1)
                {
                    newFaceNum = $"f{(int.Parse(origFaceNum.Substring(1)) + 100).ToString().PadLeft(4, '0')}";
                }

                var currFaceNum = GetFaceNum(ttModel);

                var tempMdlPath = ttModel.Source.Replace(origFaceNum, newFaceNum);

                if (!currFaceNum.Equals(newFaceNum))
                {
                    foreach (var meshGroup in ttModel.MeshGroups)
                    {
                        meshGroup.Material = meshGroup.Material.Replace(currFaceNum, newFaceNum);
                    }
                }

                foreach (var material in ttModel.Materials)
                {
                    var matPart = facePartRegex.Match(material).Value;

                    var matLoc = from m in materialDictionary where m.Value.MaterialPath.Contains(matPart) select m;

                    try
                    {
                        var mtrlPath = _mtrl.GetMtrlPath(tempMdlPath, material);
                        var mtrlOffset = await _index.GetDataOffset(mtrlPath);
                        var mtrl = await _mtrl.GetMtrlData(mtrlOffset, mtrlPath);
                        var colors = ModelTexture.GetCustomColors();
                        colors.InvertNormalGreen = false;
                        var modelMaps = await ModelTexture.GetModelMaps(_gameDirectory, mtrl, colors);

                        materialDictionary[matLoc.First().Key] = modelMaps;
                    }
                    catch
                    {
                        // Material was not present in new material path
                        // eg. Viera f0101 only has _etc_ and _iri_ in materials, it retains _fac_ from the original f0001
                    }
                }

                ViewPortVM.UpdateSkin(SelectedSkeleton.XivRace);
            }

            if (pc != null)
            {
                await pc.CloseAsync();
            }
        }


        /// <summary>
        /// Reorganizes the texture dictionary to match the model materials
        /// </summary>
        /// <param name="ttModel">The TTModel</param>
        /// <param name="materialDictionary">The material dictionary to reorganize</param>
        /// <param name="newBodyTextures">The new textures that will go into body</param>
        private void ReIndexMaterialDictionary(TTModel ttModel, Dictionary<int, ModelTextureData> materialDictionary, ModelTextureData newBodyTextures = null)
        {
            var tempDict = new Dictionary<int, ModelTextureData>();

            var bodyRegex = new Regex("(b[0-9]{4})");

            for (var i = 0; i < materialDictionary.Count; i++)
            {
                var modelTextureData = materialDictionary[i];

                var isBody = bodyRegex.Match(modelTextureData.MaterialPath).Success;

                // Add the new body textures if they were changed
                if (isBody && newBodyTextures != null)
                {
                    tempDict.Add(ttModel.Materials.IndexOf(modelTextureData.MaterialPath), newBodyTextures);
                }
                else
                {
                    tempDict.Add(ttModel.Materials.IndexOf(modelTextureData.MaterialPath), modelTextureData);
                }
            }

            foreach (var modelTextureData in tempDict)
            {
                materialDictionary[modelTextureData.Key] = modelTextureData.Value;
            }
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
                FaceComboboxVisibility = Visibility.Collapsed;
            }
            else
            {
                var modelToRemove = ModelList[SelectedModelIndex].Substring(ModelList[SelectedModelIndex].IndexOf('('))
                    .Trim('(').Trim(')');
                if (modelToRemove.Equals($"{XivStrings.Character}_{XivStrings.Face}"))
                {
                    FaceComboboxVisibility = Visibility.Collapsed;
                }

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