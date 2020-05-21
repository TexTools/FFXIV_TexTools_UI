// FFXIV TexTools
// Copyright © 2019 Rafael Gonzalez - All Rights Reserved
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
using MahApps.Metro;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media;
using xivModdingFramework.General.Enums;
using xivModdingFramework.Helpers;
using xivModdingFramework.Items.Interfaces;
using xivModdingFramework.Models.DataContainers;
using xivModdingFramework.Models.FileTypes;
using Path = System.IO.Path;

namespace FFXIV_TexTools.ViewModels
{
    public class AdvancedImportViewModel : INotifyPropertyChanged
    {
        private Dae _dae;
        private XivMdl _xivMdl;
        private LevelOfDetail _lod;
        private readonly IItemModel _itemModel;
        private readonly XivRace _selectedRace;
        private List<string> _shapesList, _colladaBoneList;
        private List<int> _meshNumbers, _partNumbers;
        private ObservableCollection<string> _materialsList, _materialUsedList, _partAttributeList, _attributeList, _boneList;
        private string _materialsGroupHeader, _attributesGroupHeader, _boneGroupHeader, _shapesHeader, _shapeDescription;
        private string _selectedMaterial, _selectedAttribute, _selectedMaterialUsed, _selectedPartAttribute, _selectedAvailablePartAttribute;
        private string _materialText, _attributeText, _partCountLabel, _partAttributesLabel, _daeLocationText;
        private int _selectedMeshNumber, _selectedMeshNumberIndex, _selectedPartNumber, _selectedPartNumberIndex, _selectedMaterialUsedIndex;
        private bool _shapeDataCheckBoxEnabled, _disableShapeDataChecked, _fromWizard, _flipAlphaChecked, _importButtonEnabled, _forceUV1QuadrantChecked, _cloneUV1toUV2Checked;
        private readonly string _textColor = "Black";
        private Dictionary<string, string> _attributeDictionary, _shapeDictionary;
        private Dictionary<string, int> _attributeMaskDictionary;
        private Dictionary<int, List<int>> _daeMeshPartDictionary;
        private readonly AdvancedModelImportView _view;
        private Dictionary<string, ModelImportSettings> _importDictionary = new Dictionary<string, ModelImportSettings>();
        private int? _meshDiff;
        private XivMdl _modMdl;

        public AdvancedImportViewModel(XivMdl xivMdl,XivMdl modMdl, IItemModel itemModel, XivRace selectedRace, AdvancedModelImportView view, bool fromWizard)
        {
            var appStyle = ThemeManager.DetectAppStyle(System.Windows.Application.Current);
            if (appStyle.Item1.Name.Equals("BaseDark"))
            {
                _textColor = "White";
            }

            _view = view;
            _xivMdl = xivMdl;
            _lod = xivMdl.LoDList[0];
            _itemModel = itemModel;
            _selectedRace = selectedRace;
            _fromWizard = fromWizard;
            _dae = new Dae(new DirectoryInfo(Settings.Default.FFXIV_Directory), itemModel.DataFile, Settings.Default.DAE_Plugin_Target);
            _modMdl = modMdl;
            if (_modMdl != null) {
                var oldMaterialPathSize = _xivMdl.PathData.MaterialList.Sum(it => it.Length) + _xivMdl.PathData.MaterialList.Count;
                var newMaterialPathSize=_modMdl.PathData.MaterialList.Sum(it => it.Length) + _modMdl.PathData.MaterialList.Count;
                _xivMdl.ModelData.MaterialCount=_modMdl.ModelData.MaterialCount;
                _xivMdl.PathData.PathCount -= _xivMdl.PathData.MaterialList.Count;
                _xivMdl.PathData.PathCount += _modMdl.PathData.MaterialList.Count;
                _xivMdl.PathData.PathBlockSize -= oldMaterialPathSize;
                _xivMdl.PathData.PathBlockSize += newMaterialPathSize;
                _xivMdl.PathData.MaterialList.Clear();
                _xivMdl.PathData.MaterialList.AddRange(_modMdl.PathData.MaterialList);

                var oldAttributeSize = _xivMdl.PathData.AttributeList.Sum(it => it.Length) + _xivMdl.PathData.AttributeList.Count;
                var newAttributeSize = _modMdl.PathData.AttributeList.Sum(it => it.Length) + _modMdl.PathData.AttributeList.Count;
                _xivMdl.ModelData.AttributeCount = _modMdl.ModelData.AttributeCount;
                _xivMdl.PathData.PathCount -= _xivMdl.PathData.AttributeList.Count;
                _xivMdl.PathData.PathCount += _modMdl.PathData.AttributeList.Count;
                _xivMdl.PathData.PathBlockSize -= oldAttributeSize;
                _xivMdl.PathData.PathBlockSize += newAttributeSize;
                _xivMdl.PathData.AttributeList.Clear();
                _xivMdl.PathData.AttributeList.AddRange(_modMdl.PathData.AttributeList);
            }

            Initialize(false);
        }

        /// <summary>
        /// Initialize Advanced Import
        /// </summary>
        private void Initialize(bool refresh)
        {
            _meshDiff = null;
            _importDictionary.Clear();

            MaterialsList = new ObservableCollection<string>(_xivMdl.PathData.MaterialList);
            AttributeList = new ObservableCollection<string>(MakeAttributeNameDictionary());
            BoneList      = new ObservableCollection<string>(_xivMdl.PathData.BoneList);
            var extraBoneList = new List<string>();

            MaterialUsed = MaterialsList;

            var meshNumberList = new List<int>();
            for (var i = 0; i < _lod.MeshDataList.Count; i++)
            {
                meshNumberList.Add(i);
            }

            MeshNumbers = meshNumberList;

            if (!refresh)
            {
                var saveDir = new DirectoryInfo(Settings.Default.Save_Directory);
                var path = $"{IOUtil.MakeItemSavePath(_itemModel, saveDir, _selectedRace)}\\3D";
                var modelName = Path.GetFileNameWithoutExtension(_xivMdl.MdlPath.File);
                var savePath = new DirectoryInfo(Path.Combine(path, modelName) + ".dae");

                if (File.Exists(savePath.FullName))
                {
                    DaeLocationText = savePath.FullName;

                    try
                    {
                        var quickColladaData = _dae.QuickColladaReader(savePath, _xivMdl);
                        _daeMeshPartDictionary = quickColladaData.MeshPartDictionary;
                        _colladaBoneList = quickColladaData.BoneList;

                        ImportButtonEnabled = true;
                    }
                    catch (Exception e)
                    {
                        FlexibleMessageBox.Show(
                            string.Format(UIMessages.DAEReadErrorMessage, e.Message), UIMessages.DAEReadErrorTitle,
                            MessageBoxButtons.OK, MessageBoxIcon.Error);

                        DaeLocationText = string.Empty;
                    }

                }
            }

            var extraBoneCountString = "";
            var extraBoneCount = 0;
            if (_colladaBoneList != null)
            {
                if (_colladaBoneList.Count > BoneList.Count)
                {
                    extraBoneCount = _colladaBoneList.Count - BoneList.Count;                    
                }

                foreach (var bone in _colladaBoneList)
                {
                    if (!BoneList.Contains(bone))
                    {
                        // Filter out any numbers in the bone names when there's duplicate skeletons
                        var boneName = Regex.Replace(bone, "[0-9]+$", string.Empty);
                        // If it's not an extra bone after all, subtract one from the extra bone count
                        if(!BoneList.Contains(boneName))
                        {
                            BoneList.Add($"+ {bone}");
                            extraBoneList.Add(bone);
                        }
                        else
                        {
                            extraBoneCount -= 1;
                        }                        
                    }
                }
                // If there are still extra bones after checking possible duplicates set the extra bone count string
                if (extraBoneCount > 0)
                {
                    extraBoneCountString = $"+ { extraBoneCount}";
                }
            }

            MaterialsGroupHeader = $"{UIStrings.Materials} ({UIStrings.Count}: {MaterialsList.Count})";
            AttributesGroupHeader = $"{UIStrings.Attributes} ({UIStrings.Count}: {AttributeList.Count})";
            BonesGroupHeader = $"{UIStrings.Bones} ({UIStrings.Count}: {_xivMdl.PathData.BoneList.Count} {extraBoneCountString})";

            if (_daeMeshPartDictionary != null)
            {
                foreach (var meshNum in _daeMeshPartDictionary.Keys)
                {
                    _importDictionary.Add(meshNum.ToString(), new ModelImportSettings { PartList = _daeMeshPartDictionary[meshNum], ExtraBones = extraBoneList});
                    if (_modMdl != null)
                    {
                        var modLod = _modMdl.LoDList[0];
                        var materiaIndex= modLod.MeshDataList[modLod.MeshDataList.Count-1].MeshInfo.MaterialIndex;
                        if(modLod.MeshDataList.Count > meshNum)
                                materiaIndex = modLod.MeshDataList[meshNum].MeshInfo.MaterialIndex;
                        if (meshNum >= _lod.MeshDataList.Count)
                        {
                            _importDictionary[meshNum.ToString()].MaterialIndex = materiaIndex;
                        }
                        else
                        {
                            _lod.MeshDataList[meshNum].MeshInfo.MaterialIndex = modLod.MeshDataList[meshNum].MeshInfo.MaterialIndex;
                        }
                        foreach (var partNum in _daeMeshPartDictionary[meshNum])
                        {
                            var attributeIndex = 0;
                            if(modLod.MeshDataList.Count>meshNum&&modLod.MeshDataList[meshNum].MeshPartList.Count>partNum)
                                    attributeIndex=modLod.MeshDataList[meshNum].MeshPartList[partNum].AttributeIndex;
                            if (_lod.MeshDataList.Count > meshNum && _lod.MeshDataList[meshNum].MeshPartList.Count > partNum)
                            {
                                _lod.MeshDataList[meshNum].MeshPartList[partNum].AttributeIndex = attributeIndex;
                            }
                            else
                            {
                                var importDictMesh = _importDictionary[meshNum.ToString()];

                                if (importDictMesh.PartAttributeDictionary.ContainsKey(partNum))
                                {
                                    importDictMesh.PartAttributeDictionary[partNum] = attributeIndex;
                                }
                                else
                                {
                                    importDictMesh.PartAttributeDictionary.Add(partNum, attributeIndex);
                                }
                            }                        
                        }

                        if (_xivMdl.HasShapeData)
                        {
                            //Dictionary containing <index data offset, mesh number>
                            var indexMeshNum = new Dictionary<int, int>();

                            var shapeData = _xivMdl.MeshShapeData.ShapeDataList;

                            // Get the index data offsets in each mesh
                            for (var i = 0; i < _xivMdl.LoDList[0].MeshCount; i++)
                            {
                                var indexDataOffset = _xivMdl.LoDList[0].MeshDataList[i].MeshInfo.IndexDataOffset;

                                if (!indexMeshNum.ContainsKey(indexDataOffset))
                                {
                                    indexMeshNum.Add(indexDataOffset, i);
                                }
                            }

                            // Shape info list of both the mod and the original mdl
                            var originalShapeInfoList = _xivMdl.MeshShapeData.ShapeInfoList;
                            var moddedShapeInfoList = _modMdl.MeshShapeData.ShapeInfoList;

                            // Number of shape info in each mesh
                            var perMeshCount = _xivMdl.ModelData.ShapeCount;

                            for (var i = 0; i < perMeshCount; i++)
                            {
                                var originalShapeInfo = originalShapeInfoList[i];
                                var moddedShapeInfo = moddedShapeInfoList[i];

                                var originalIndexPart = originalShapeInfo.ShapeIndexParts[0];
                                var moddedIndexPart = moddedShapeInfo.ShapeIndexParts[0];

                                // The part count
                                var infoPartCount = originalIndexPart.PartCount;

                                for (var j = 0; j < infoPartCount; j++)
                                {
                                    // Gets the data info for the part
                                    var shapeDataInfo = _modMdl.MeshShapeData.ShapeDataInfoList[originalIndexPart.DataInfoIndex + j];

                                    // The offset in the shape data 
                                    var indexDataOffset = shapeDataInfo.IndexDataOffset;

                                    var indexMeshLocation = 0;

                                    // Determine which mesh the shape info belongs to
                                    if (indexMeshNum.ContainsKey(indexDataOffset))
                                    {
                                        indexMeshLocation = indexMeshNum[indexDataOffset];
                                    }

                                    // Get the shape data for the mesh the shape info belonged to
                                    var shapeDataForMesh = shapeData.GetRange(shapeDataInfo.DataIndexOffset, shapeDataInfo.IndexCount);

                                    foreach (var data in shapeDataForMesh)
                                    {
                                        // Check if the shape data has been disabled on the modded mdl
                                        if (moddedIndexPart.DataInfoIndex == 0 && moddedIndexPart.PartCount == 0)
                                        {
                                            // Check if the mesh's import settings has been initialized yet
                                            if (_importDictionary.ContainsKey(indexMeshLocation.ToString()))
                                            {
                                                // If so, set it to disabled in the advanced import window
                                                _importDictionary[indexMeshLocation.ToString()].Disable = true;
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }

            if (_itemModel.Category.Equals(XivStrings.Character))
            {
                FlipAlphaChecked = true;
            }

            if (_itemModel.Category.Equals(XivStrings.Gear) && Settings.Default.ForceUV1Quadrant)
            {
                ForceUV1QuadrantChecked = true;
            }

            if (_itemModel.ItemCategory.Equals(XivStrings.Hair) && Settings.Default.CloneUV1toUV2)
            {
                CloneUV1toUV2Checked = true;
            }

            SelectedMeshNumberIndex = 0;
        }


        #region Properties

        /// <summary>
        /// Header for Materials Group
        /// </summary>
        public string MaterialsGroupHeader
        {
            get => _materialsGroupHeader;
            set
            {
                _materialsGroupHeader = value;
                NotifyPropertyChanged(nameof(MaterialsGroupHeader));
            }
        }

        /// <summary>
        /// Header for Attributes Group
        /// </summary>
        public string AttributesGroupHeader
        {
            get => _attributesGroupHeader;
            set
            {
                _attributesGroupHeader = value;
                NotifyPropertyChanged(nameof(AttributesGroupHeader));
            }
        }

        /// <summary>
        /// Header for Bones Group
        /// </summary>
        public string BonesGroupHeader
        {
            get => _boneGroupHeader;
            set
            {
                _boneGroupHeader = value;
                NotifyPropertyChanged(nameof(BonesGroupHeader));
            }
        }

        /// <summary>
        /// Part Count Label
        /// </summary>
        public string PartCountLabel
        {
            get => _partCountLabel;
            set
            {
                _partCountLabel = value;
                NotifyPropertyChanged(nameof(PartCountLabel));
            }
        }

        /// <summary>
        /// List of Material strings
        /// </summary>
        public ObservableCollection<string> MaterialsList
        {
            get => _materialsList;
            set
            {
                _materialsList = value;
                NotifyPropertyChanged(nameof(MaterialsList));

            }
        }

        /// <summary>
        /// List of attribute strings
        /// </summary>
        public ObservableCollection<string> AttributeList
        {
            get => _attributeList;
            set
            {
                _attributeList = value;
                NotifyPropertyChanged(nameof(AttributeList));

            }
        }

        /// <summary>
        /// List of bone strings
        /// </summary>
        public ObservableCollection<string> BoneList
        {
            get => _boneList;
            set
            {
                _boneList = value;
                NotifyPropertyChanged(nameof(BoneList));

            }
        }

        /// <summary>
        /// Selected Material
        /// </summary>
        public string SelectedMaterial
        {
            get => _selectedMaterial;
            set
            {
                _selectedMaterial = value;
                MaterialStringText = value;
                NotifyPropertyChanged(nameof(SelectedMaterial));
            }
        }

        /// <summary>
        /// Selected Attribute
        /// </summary>
        public string SelectedAttribute
        {
            get => _selectedAttribute;
            set
            {
                _selectedAttribute = value;

                if(value != null)
                {
                    AttributeStringText = _attributeDictionary[value];
                }

                NotifyPropertyChanged(nameof(SelectedAttribute));
            }
        }

        /// <summary>
        /// String in Material TextBox
        /// </summary>
        public string MaterialStringText
        {
            get => _materialText;
            set
            {
                _materialText = value;
                NotifyPropertyChanged(nameof(MaterialStringText));
            }
        }

        /// <summary>
        /// String in Attribute TextBox
        /// </summary>
        public string AttributeStringText
        {
            get => _attributeText;
            set
            {
                _attributeText = value;
                NotifyPropertyChanged(nameof(AttributeStringText));
            }
        }

        /// <summary>
        /// List of Mesh Numbers
        /// </summary>
        public List<int> MeshNumbers
        {
            get => _meshNumbers;
            set
            {
                _meshNumbers = value;
                NotifyPropertyChanged(nameof(MeshNumbers));
            }
        }

        /// <summary>
        /// Selected Mesh Number
        /// </summary>
        public int SelectedMeshNumber
        {
            get => _selectedMeshNumber;
            set
            {
                _selectedMeshNumber = value;
                UpdatePartNumbers();
                UpdateMaterialUsed();
                UpdateShapes();
                NotifyPropertyChanged(nameof(SelectedMeshNumber));
            }
        }

        /// <summary>
        /// Selected Mesh Number Index
        /// </summary>
        public int SelectedMeshNumberIndex
        {
            get => _selectedMeshNumberIndex;
            set
            {
                _selectedMeshNumberIndex = value;
                if (value != -1)
                {
                    UpdatePartNumbers();
                    UpdateMaterialUsed();
                    UpdateShapes();
                }
                NotifyPropertyChanged(nameof(SelectedMeshNumberIndex));
            }
        }

        /// <summary>
        /// Part Numbers
        /// </summary>
        public List<int> PartNumbers
        {
            get => _partNumbers;
            set
            {
                _partNumbers = value;
                NotifyPropertyChanged(nameof(PartNumbers));
            }
        }

        /// <summary>
        /// Selected Part Number
        /// </summary>
        public int SelectedPartNumber
        {
            get => _selectedPartNumber;
            set
            {
                _selectedPartNumber = value;
                UpdateAttributesUsed();
                NotifyPropertyChanged(nameof(SelectedPartNumber));
            }
        }

        /// <summary>
        /// Selected Part Number Index
        /// </summary>
        public int SelectedPartNumberIndex
        {
            get => _selectedPartNumberIndex;
            set
            {
                _selectedPartNumberIndex = value;
                UpdateAttributesUsed();
                NotifyPropertyChanged(nameof(SelectedPartNumberIndex));
            }
        }

        /// <summary>
        /// List of Material Used
        /// </summary>
        public ObservableCollection<string> MaterialUsed
        {
            get => _materialUsedList;
            set
            {
                _materialUsedList = value;
                NotifyPropertyChanged(nameof(MaterialUsed));

            }
        }

        /// <summary>
        /// Selected Material Used
        /// </summary>
        public string SelectedMaterialUsed
        {
            get => _selectedMaterialUsed;
            set
            {
                _selectedMaterialUsed = value;
                UpdateMdlMaterialUsed();
                NotifyPropertyChanged(nameof(SelectedMaterialUsed));
            }
        }

        /// <summary>
        /// Selected Material Used Index
        /// </summary>
        public int SelectedMaterialUsedIndex
        {
            get => _selectedMaterialUsedIndex;
            set
            {
                _selectedMaterialUsedIndex = value;
                NotifyPropertyChanged(nameof(SelectedMaterialUsedIndex));
            }
        }

        /// <summary>
        /// List of part attributes
        /// </summary>
        public ObservableCollection<string> PartAttributes
        {
            get => _partAttributeList;
            set
            {
                _partAttributeList = value;
                NotifyPropertyChanged(nameof(PartAttributes));
            }
        }

        /// <summary>
        /// Selected part attribute
        /// </summary>
        public string SelectedPartAttribute
        {
            get => _selectedPartAttribute;
            set
            {
                _selectedPartAttribute = value;
                NotifyPropertyChanged(nameof(SelectedPartAttribute));
            }
        }

        /// <summary>
        /// Selected Available attribute
        /// </summary>
        public string SelectedAvailableAttribute
        {
            get => _selectedAvailablePartAttribute;
            set
            {
                _selectedAvailablePartAttribute = value;
                NotifyPropertyChanged(nameof(SelectedAvailableAttribute));
            }
        }

        /// <summary>
        /// Part Attribute label
        /// </summary>
        public string PartAttributesLabel
        {
            get => _partAttributesLabel;
            set
            {
                _partAttributesLabel = value;
                NotifyPropertyChanged(nameof(PartAttributesLabel));
            }
        }

        /// <summary>
        /// DAE directory string in Location TextBox
        /// </summary>
        public string DaeLocationText
        {
            get => _daeLocationText;
            set
            {
                _daeLocationText = value;
                NotifyPropertyChanged(nameof(DaeLocationText));
            }
        }

        /// <summary>
        /// Shape description
        /// </summary>
        public string ShapeDescription
        {
            get => _shapeDescription;
            set
            {
                _shapeDescription = value;
                NotifyPropertyChanged(nameof(ShapeDescription));
            }
        }

        /// <summary>
        /// List of shapes
        /// </summary>
        public List<string> ShapesList
        {
            get => _shapesList;
            set
            {
                _shapesList = value;
                NotifyPropertyChanged(nameof(ShapesList));
            }
        }

        /// <summary>
        /// Shape Data Check Box Enabled Status
        /// </summary>
        public bool ShapeDataCheckBoxEnabled
        {
            get => _shapeDataCheckBoxEnabled;
            set
            {
                _shapeDataCheckBoxEnabled = value;
                NotifyPropertyChanged(nameof(ShapeDataCheckBoxEnabled));
            }
        }

        /// <summary>
        /// Shapes Header
        /// </summary>
        public string ShapesHeader
        {
            get => _shapesHeader;
            set
            {
                _shapesHeader = value;
                NotifyPropertyChanged(nameof(ShapesHeader));
            }
        }

        /// <summary>
        /// Status of Shape Data CheckBox
        /// </summary>
        public bool DisableShapeDataChecked
        {
            get => _disableShapeDataChecked;
            set
            {
                _disableShapeDataChecked = value;
                UpdateImportDictionary();
                NotifyPropertyChanged(nameof(DisableShapeDataChecked));
            }
        }

        public bool FlipAlphaChecked
        {
            get => _flipAlphaChecked;
            set
            {
                _flipAlphaChecked = value;
                foreach (var importDictionaryValue in _importDictionary.Values)
                {
                    importDictionaryValue.FlipAlpha = value;
                }
                NotifyPropertyChanged(nameof(FlipAlphaChecked));
            }
        }

        public bool ImportButtonEnabled
        {
            get => _importButtonEnabled;
            set
            {
                _importButtonEnabled = value;
                NotifyPropertyChanged(nameof(ImportButtonEnabled));
            }
        }

        public bool ForceUV1QuadrantChecked
        {
            get => _forceUV1QuadrantChecked;
            set
            {
                _forceUV1QuadrantChecked = value;
                foreach (var importDictionaryValue in _importDictionary.Values)
                {
                    importDictionaryValue.ForceUV1Quadrant = value;
                }
                NotifyPropertyChanged(nameof(ForceUV1QuadrantChecked));
            }
        }

        public bool CloneUV1toUV2Checked
        {
            get => _cloneUV1toUV2Checked;
            set
            {
                _cloneUV1toUV2Checked = value;
                foreach (var importDictionaryValue in _importDictionary.Values)
                {
                    importDictionaryValue.CloneUV1toUV2 = value;
                }
                NotifyPropertyChanged(nameof(CloneUV1toUV2Checked));
            }
        }

        /// <summary>
        /// The raw data for the model
        /// </summary>
        public byte[] RawModelData { get; set; }

        #endregion

        #region Commands

        public ICommand FileSelectCommand => new RelayCommand(SelectDaeFile);
        public ICommand AddRemoveMaterialCommand => new RelayCommand(AddRemoveMaterial);
        public ICommand AddRemoveAttributeCommand => new RelayCommand(AddRemoveAttribute);
        public ICommand AddPartAttributeCommand => new RelayCommand(AddPartAttribute);
        public ICommand RemovePartAttributeCommand => new RelayCommand(RemovePartAttribute);
        public ICommand ImportCommand => new RelayCommand(Import);

        #endregion

        #region Private Methods

        /// <summary>
        /// Update Part Numbers
        /// </summary>
        private void UpdatePartNumbers()
        {
            var partNumberList = new List<int>();
            var partCount = "";
            var partDiff = "";

            if(_daeMeshPartDictionary == null) return;

            if (_lod.MeshDataList.Count > SelectedMeshNumber)
            {
                var originalPartListCount = _lod.MeshDataList[SelectedMeshNumber].MeshPartList.Count;
                partCount = $"{originalPartListCount}";
                if (_daeMeshPartDictionary.ContainsKey(SelectedMeshNumber))
                {
                    var daePartList = _daeMeshPartDictionary[SelectedMeshNumber];

                    if (daePartList.Count > originalPartListCount)
                    {
                        partDiff = $"(+{daePartList.Count - originalPartListCount})";
                        for (var i = 0; i < daePartList.Count; i++)
                        {
                            partNumberList.Add(i);
                        }
                    }
                    else
                    {
                        if (daePartList.Count < originalPartListCount)
                        {
                            partDiff = $"(-{originalPartListCount - daePartList.Count})";
                        }

                        for (var i = 0; i < _lod.MeshDataList[SelectedMeshNumber].MeshPartList.Count; i++)
                        {
                            partNumberList.Add(i);
                        }
                    }
                }
            }
            else
            {
                var daePartList = _daeMeshPartDictionary[SelectedMeshNumber];

                for (var i = 0; i < daePartList.Count; i++)
                {
                    partNumberList.Add(i);
                }

                partCount = $"{partNumberList.Count}";
            }

            PartNumbers = partNumberList;
            PartCountLabel = $"{UIStrings.Part_Count}: {partCount} {partDiff}";
            SelectedPartNumberIndex = 0;

            CheckForDaeDiscrepancy();
        }

        /// <summary>
        /// Update Material Used
        /// </summary>
        private void UpdateMaterialUsed()
        {
            var materialIndex = 0;

            if (_lod.MeshDataList.Count > SelectedMeshNumber)
            {
                materialIndex = _lod.MeshDataList[SelectedMeshNumber].MeshInfo.MaterialIndex;
            }
            else
            {
                materialIndex = _importDictionary[SelectedMeshNumber.ToString()].MaterialIndex;
            }

            SelectedMaterialUsedIndex = materialIndex;
        }

        /// <summary>
        /// Update Mdl Material Used
        /// </summary>
        private void UpdateMdlMaterialUsed()
        {
            // Change the XivMdl data directly for material index if the mesh exists, otherwise add it to the import settings
            if (SelectedMeshNumber >= _lod.MeshDataList.Count)
            {
                _importDictionary[SelectedMeshNumber.ToString()].MaterialIndex = (short)SelectedMaterialUsedIndex;
            }
            else
            {
                _lod.MeshDataList[SelectedMeshNumber].MeshInfo.MaterialIndex = (short)SelectedMaterialUsedIndex;
            }
        }

        /// <summary>
        /// Update Attribute Used
        /// </summary>
        private void UpdateAttributesUsed()
        {
            var attributeNameList = new List<string>();
            var attributeMask = 0;

            if (_lod.MeshDataList.Count > SelectedMeshNumber)
            {
                if (_lod.MeshDataList[SelectedMeshNumber].MeshPartList.Count > SelectedPartNumber)
                {
                    attributeMask = _lod.MeshDataList[SelectedMeshNumber].MeshPartList[SelectedPartNumber].AttributeIndex;                    
                }
                else
                {
                    var importDictMesh = _importDictionary[SelectedMeshNumber.ToString()];
                    if (!importDictMesh.PartAttributeDictionary.ContainsKey(SelectedPartNumber))
                    {
                        importDictMesh.PartAttributeDictionary.Add(SelectedPartNumber, 0);
                    }
                    attributeMask = importDictMesh.PartAttributeDictionary[SelectedPartNumber];
                }
            }
            else
            {
                var importDictMesh = _importDictionary[SelectedMeshNumber.ToString()];
                if (!importDictMesh.PartAttributeDictionary.ContainsKey(SelectedPartNumber))
                {
                    importDictMesh.PartAttributeDictionary.Add(SelectedPartNumber, 0);
                }
                attributeMask = importDictMesh.PartAttributeDictionary[SelectedPartNumber];
            }
            for (var i = 0; i < AttributeList.Count; i++)
            {
                var value = 1 << i;
                if ((attributeMask & value) > 0)
                {
                    attributeNameList.Add($"{AttributeList[i]}");
                }
            }

            PartAttributes = new ObservableCollection<string>(attributeNameList);

            PartAttributesLabel = $"{UIStrings.Part_Attributes} ({UIStrings.Count}: {PartAttributes.Count})";
        }

        /// <summary>
        /// Update Shapes
        /// </summary>
        private void UpdateShapes()
        {
            if (_importDictionary == null || _importDictionary.Count < 1) return;

            MakeShapeNameDictionary();

            var shapePathList = new List<string>();

            if (_lod.MeshDataList.Count > SelectedMeshNumber)
            {
                if (_lod.MeshDataList[SelectedMeshNumber].ShapePathList != null)
                {
                    foreach (var shapePath in _lod.MeshDataList[SelectedMeshNumber].ShapePathList)
                    {
                        shapePathList.Add(_shapeDictionary[shapePath]);
                    }
                }
            }

            if (shapePathList.Count > 0)
            {
                ShapeDataCheckBoxEnabled = true;

                if (_importDictionary.ContainsKey(SelectedMeshNumber.ToString()))
                {
                    DisableShapeDataChecked = _importDictionary[SelectedMeshNumber.ToString()].Disable;
                }
                else
                {
                    DisableShapeDataChecked = false;
                    ShapeDataCheckBoxEnabled = false;
                }

                ShapeDescription =
                            $"{UIStrings.ShapeDescription1_line1}\n" +
                            $"{UIStrings.ShapeDescription1_line2}\n\n" +
                            $"{UIStrings.ShapeDescription1_line3}";
            }
            else
            {
                DisableShapeDataChecked = false;
                ShapeDataCheckBoxEnabled = false;

                ShapeDescription = $"{UIStrings.ShapeDescription2_line1}\n\n" +
                                   $"{UIStrings.ShapeDescription2_line2}";
            }

            ShapesList = shapePathList;

            ShapesHeader = $"{UIStrings.Shapes} ({UIStrings.Count}: {ShapesList.Count})";
        }

        /// <summary>
        /// Update Import Dictionary
        /// </summary>
        private void UpdateImportDictionary()
        {
            if (_importDictionary.ContainsKey(SelectedMeshNumber.ToString()))
            {
                _importDictionary[SelectedMeshNumber.ToString()].Disable = DisableShapeDataChecked;
            }
        }

        /// <summary>
        /// Check DAE for discrepancies
        /// </summary>
        private void CheckForDaeDiscrepancy()
        {
            _view.DaeInfoTextBox.Document.Blocks.Clear();

            var meshCount = _daeMeshPartDictionary.Count;

            // Check for mesh difference
            if (_meshDiff == null)
            {
                if (meshCount > MeshNumbers.Count)
                {
                    var extraCount = meshCount - _lod.MeshDataList.Count;
                    AddText($"{extraCount}", "Green", true);
                    AddText($" {UIStrings.Added_Mesh}\n\n", _textColor, false);

                    // Update mesh number list
                    var meshNumberList = new List<int>();
                    for (var i = 0; i < meshCount; i++)
                    {
                        meshNumberList.Add(i);
                    }

                    _meshDiff = extraCount;
                    MeshNumbers = meshNumberList;
                }
                else if (meshCount < MeshNumbers.Count)
                {
                    var removedCount = meshCount - _lod.MeshDataList.Count;
                    AddText($"{Math.Abs(removedCount)}", "Red", true);
                    AddText($" {UIStrings.Removed_Mesh} ", _textColor, false);

                    foreach (var meshNumber in MeshNumbers)
                    {
                        if (!_daeMeshPartDictionary.ContainsKey(meshNumber))
                        {
                            AddText($"{meshNumber} ", "Red", true);
                        }
                    }

                    AddText($"\n{UIStrings.Removed_Mesh_Note}\n\n", _textColor, true);

                    _meshDiff = removedCount;
                }
                else
                {
                    AddText($"{UIStrings.Mesh_No_Difference}\n\n\n", _textColor, false);

                    _meshDiff = 0;
                }
            }
            else
            {
                if (_meshDiff == 0)
                {
                    AddText($"{UIStrings.Mesh_No_Difference}\n\n\n", _textColor, false);
                }
                else if (_meshDiff > 0)
                {
                    AddText($"{_meshDiff}", "Green", true);
                    AddText($" {UIStrings.Added_Mesh}\n\n", _textColor, false);
                }
                else
                {
                    AddText($"{Math.Abs(_meshDiff.Value)}", "Red", true);
                    AddText($" {UIStrings.Removed_Mesh} ", _textColor, false);
                }
            }

            // Check for mesh part difference
            if (_daeMeshPartDictionary.ContainsKey(SelectedMeshNumber))
            {
                var meshPartList = _daeMeshPartDictionary[SelectedMeshNumber];

                if (_lod.MeshDataList.Count > SelectedMeshNumber)
                {
                    if (meshPartList.Count > _lod.MeshDataList[SelectedMeshNumber].MeshPartList.Count)
                    {
                        var extraCount = meshPartList.Count - _lod.MeshDataList[SelectedMeshNumber].MeshPartList.Count;
                        AddText($"{extraCount}", "Green", true);
                        AddText($" {UIStrings.Added_MeshParts}", _textColor, false);
                    }
                    else if (meshPartList.Count < _lod.MeshDataList[SelectedMeshNumber].MeshPartList.Count)
                    {
                        var removedCount = _lod.MeshDataList[SelectedMeshNumber].MeshPartList.Count - meshPartList.Count;
                        AddText($"{removedCount}", "Red", true);
                        AddText($" {UIStrings.Removed_MeshParts} ", _textColor, false);

                        foreach (var partNumber in PartNumbers)
                        {
                            if (!meshPartList.Contains(partNumber))
                            {
                                AddText($"{partNumber} ", "Red", true);
                            }
                        }

                        AddText($"\n{UIStrings.Removed_MeshParts_Note}", _textColor, false);
                    }
                    else
                    {
                        AddText($"{UIStrings.MeshPart_No_Difference}", _textColor, false);
                    }
                }
                else
                {
                    AddText($"{string.Format(UIStrings.New_Mesh_MeshPart_Count, meshPartList.Count)}", _textColor, false);
                }
            }
        }

        /// <summary>
        /// Event Handler for Add/Remove Material Button
        /// </summary>
        private void AddRemoveMaterial(object obj)
        {
            if (MaterialsList.Contains(MaterialStringText))
            {
                var materialIndex = MaterialsList.IndexOf(MaterialStringText);
                var materialMdlIndex = _xivMdl.PathData.MaterialList.IndexOf(MaterialStringText);
                var materialStringTextLength = MaterialStringText.Length;
                MaterialsList.RemoveAt(materialIndex);
                _xivMdl.PathData.MaterialList.RemoveAt(materialMdlIndex);
                _xivMdl.ModelData.MaterialCount -= 1;
                _xivMdl.PathData.PathCount -= 1;
                _xivMdl.PathData.PathBlockSize -= materialStringTextLength + 1;
            }
            else
            {
                if (!string.IsNullOrEmpty(MaterialStringText))
                {
                    MaterialsList.Add(MaterialStringText);
                    _xivMdl.PathData.MaterialList.Add(MaterialStringText);
                    _xivMdl.ModelData.MaterialCount += 1;
                    _xivMdl.PathData.PathCount += 1;
                    _xivMdl.PathData.PathBlockSize += MaterialStringText.Length + 1;
                }
            }

            MaterialsGroupHeader = $"{UIStrings.Materials} ({UIStrings.Count}: {MaterialsList.Count})";
        }

        /// <summary>
        /// Event Handler for Add/Remove Attribute Button
        /// </summary>
        private void AddRemoveAttribute(object obj)
        {
            if (_attributeDictionary.ContainsValue(AttributeStringText))
            {
                var key = _attributeDictionary.FirstOrDefault(x => x.Value == AttributeStringText).Key;

                var attributeIndex = AttributeList.IndexOf(key);
                var attributeMdlIndex = _xivMdl.PathData.AttributeList.IndexOf(AttributeStringText);

                AttributeList.RemoveAt(attributeIndex);
                _xivMdl.PathData.AttributeList.RemoveAt(attributeMdlIndex);
                _xivMdl.ModelData.AttributeCount -= 1;
                _xivMdl.PathData.PathCount -= 1;
                _xivMdl.PathData.PathBlockSize -= AttributeStringText.Length + 1;
            }
            else
            {
                if (!string.IsNullOrEmpty(AttributeStringText))
                {
                    AttributeList.Add(AttributeStringText);
                    _xivMdl.PathData.AttributeList.Add(AttributeStringText);
                    _xivMdl.ModelData.AttributeCount += 1;
                    _xivMdl.PathData.PathCount += 1;
                    _xivMdl.PathData.PathBlockSize += AttributeStringText.Length + 1;
                }
            }

            AttributeList = new ObservableCollection<string>(MakeAttributeNameDictionary());

            AttributesGroupHeader = $"{UIStrings.Attributes} ({UIStrings.Count}: {AttributeList.Count})";
        }

        /// <summary>
        /// Event Handler to add a part attribute
        /// </summary>
        private void AddPartAttribute(object obj)
        {
            if (string.IsNullOrEmpty(SelectedAvailableAttribute)) return;

            var attributeMask = 0;

            if (_lod.MeshDataList.Count > SelectedMeshNumber && _lod.MeshDataList[SelectedMeshNumber].MeshPartList.Count > SelectedPartNumber)
            {
                attributeMask = _lod.MeshDataList[SelectedMeshNumber].MeshPartList[SelectedPartNumber].AttributeIndex;

                attributeMask += _attributeMaskDictionary[SelectedAvailableAttribute];

                _lod.MeshDataList[SelectedMeshNumber].MeshPartList[SelectedPartNumber].AttributeIndex = attributeMask;
            }
            else
            {
                var importDictMesh = _importDictionary[SelectedMeshNumber.ToString()];

                if (importDictMesh.PartAttributeDictionary.ContainsKey(SelectedPartNumber))
                {
                    attributeMask = importDictMesh.PartAttributeDictionary[SelectedPartNumber];

                    attributeMask += _attributeMaskDictionary[SelectedAvailableAttribute];

                    importDictMesh.PartAttributeDictionary[SelectedPartNumber] = attributeMask;
                }
                else
                {
                    attributeMask += _attributeMaskDictionary[SelectedAvailableAttribute];

                    importDictMesh.PartAttributeDictionary.Add(SelectedPartNumber, attributeMask);
                }
            }

            PartAttributes.Add(SelectedAvailableAttribute);
        }

        /// <summary>
        /// Event Handler to remove a part attribute
        /// </summary>
        private void RemovePartAttribute(object obj)
        {
            if (string.IsNullOrEmpty(SelectedPartAttribute)) return;

            var attributeMask = 0;

            if (_lod.MeshDataList.Count > SelectedMeshNumber && _lod.MeshDataList[SelectedMeshNumber].MeshPartList.Count > SelectedPartNumber)
            {
                attributeMask = _lod.MeshDataList[SelectedMeshNumber].MeshPartList[SelectedPartNumber].AttributeIndex;

                attributeMask -= _attributeMaskDictionary[SelectedPartAttribute];

                _lod.MeshDataList[SelectedMeshNumber].MeshPartList[SelectedPartNumber].AttributeIndex = attributeMask;
            }
            else
            {
                var importDictMesh = _importDictionary[SelectedMeshNumber.ToString()];

                attributeMask = importDictMesh.PartAttributeDictionary[SelectedPartNumber];

                attributeMask -= _attributeMaskDictionary[SelectedPartAttribute];

                importDictMesh.PartAttributeDictionary[SelectedPartNumber] = attributeMask;
            }

            var attributeIndex = PartAttributes.IndexOf(SelectedPartAttribute);

            PartAttributes.RemoveAt(attributeIndex);
        }

        /// <summary>
        /// Event Handler for Import
        /// </summary>
        public async void Import(object obj)
        {
            Dictionary<string, string> warnings;

            var mdl = new Mdl(new DirectoryInfo(Settings.Default.FFXIV_Directory), _itemModel.DataFile);

            try
            {
                if (_fromWizard)
                {
                    warnings = await mdl.ImportModel(_itemModel, _xivMdl, new DirectoryInfo(DaeLocationText), _importDictionary,
                        XivStrings.TexTools, Settings.Default.DAE_Plugin_Target, true);

                    RawModelData = mdl.MDLRawData;
                }
                else
                {
                    warnings = await mdl.ImportModel(_itemModel, _xivMdl, new DirectoryInfo(DaeLocationText), _importDictionary,
                        XivStrings.TexTools, Settings.Default.DAE_Plugin_Target);
                }
            }
            catch (Exception ex)
            {
                FlexibleMessageBox.Show(
                    string.Format(UIMessages.ModelImportErrorMessage, ex.Message), UIMessages.ModelImportErrorTitle,
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            if (warnings.Count > 0)
            {
                foreach (var warning in warnings)
                {
                    FlexibleMessageBox.Show(
                        $"{warning.Value}", $"{warning.Key}",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
        }

        public async Task ImportAsync()
        {
            Dictionary<string, string> warnings;

            var mdl = new Mdl(new DirectoryInfo(Settings.Default.FFXIV_Directory), _itemModel.DataFile);

            try
            {
                if (_fromWizard)
                {
                    warnings = await mdl.ImportModel(_itemModel, _xivMdl, new DirectoryInfo(DaeLocationText), _importDictionary,
                        XivStrings.TexTools, Settings.Default.DAE_Plugin_Target, true);

                    RawModelData = mdl.MDLRawData;
                }
                else
                {
                    warnings = await mdl.ImportModel(_itemModel, _xivMdl, new DirectoryInfo(DaeLocationText), _importDictionary,
                        XivStrings.TexTools, Settings.Default.DAE_Plugin_Target);
                }
            }
            catch (Exception ex)
            {
                FlexibleMessageBox.Show(
                    string.Format(UIMessages.ModelImportErrorMessage, ex.Message), UIMessages.ModelImportErrorTitle,
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            if (warnings.Count > 0)
            {
                foreach (var warning in warnings)
                {
                    FlexibleMessageBox.Show(
                        $"{warning.Value}", $"{warning.Key}",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
        }

        /// <summary>
        /// Make Attribute Name Dictionary
        /// </summary>
        /// <returns></returns>
        private List<string> MakeAttributeNameDictionary()
        {
            _attributeDictionary = new Dictionary<string, string>();
            _attributeMaskDictionary = new Dictionary<string, int>();
            var nameList = new List<string>();

            foreach (var attribute in _xivMdl.PathData.AttributeList)
            {
                var hasNumber = attribute.Any(char.IsDigit);

                if (hasNumber)
                {
                    var attributeNumber = attribute.Substring(attribute.Length - 1);
                    var attributeName = attribute.Substring(0, attribute.Length - 1);

                    var name = $"{attribute} - {AttributeNameDictionary[attributeName]} {attributeNumber}";
                    nameList.Add(name);
                    _attributeDictionary.Add(name, attribute);
                }
                else
                {
                    if (attribute.Count(x => x == '_') > 1)
                    {
                        var attributeName = attribute.Substring(0, attribute.LastIndexOf("_"));
                        var attributePart = attribute.Substring(attribute.LastIndexOf("_") + 1, 1);

                        if (AttributeNameDictionary.ContainsKey(attributeName))
                        {
                            var name = $"{attribute} - {AttributeNameDictionary[attributeName]} {attributePart}";
                            nameList.Add(name);
                            _attributeDictionary.Add(name, attribute);
                        }
                        else
                        {
                            nameList.Add($"{attribute}");
                            _attributeDictionary.Add(attribute, attribute);
                        }
                    }
                    else
                    {
                        if (AttributeNameDictionary.ContainsKey(attribute))
                        {
                            var name = $"{attribute} - {AttributeNameDictionary[attribute]}";
                            nameList.Add(name);
                            _attributeDictionary.Add(name, attribute);
                        }
                        else
                        {
                            nameList.Add(attribute);
                            _attributeDictionary.Add(attribute, attribute);
                        }
                    }
                }

            }

            var mask = 1;
            foreach (var attribute in nameList)
            {
                _attributeMaskDictionary.Add(attribute, mask);

                mask *= 2;
            }

            return nameList;
        }

        /// <summary>
        /// Make Shape Name Dictionary
        /// </summary>
        private void MakeShapeNameDictionary()
        {
            _shapeDictionary = new Dictionary<string, string>();
            var nameList = new List<string>();

            foreach (var shape in _xivMdl.PathData.ShapeList)
            {
                var hasNumber = shape.Any(char.IsDigit);

                if (hasNumber)
                {
                    var shapeNumber = shape.Substring(shape.Length - 1);
                    var shapeName = shape.Substring(0, shape.Length - 1);

                    var name = $"{shape} - {ShapeNameDictionary[shapeName]} {shapeNumber}";
                    nameList.Add(name);
                    _shapeDictionary.Add(shape, name);
                }
                else
                {
                    if (shape.Count(x => x == '_') > 1)
                    {
                        var shapeName = shape.Substring(0, shape.LastIndexOf("_"));
                        var shapePart = shape.Substring(shape.LastIndexOf("_") + 1, 1);

                        if (ShapeNameDictionary.ContainsKey(shapeName))
                        {
                            var name = $"{shape} - {ShapeNameDictionary[shapeName]} {shapePart}";
                            nameList.Add(name);
                            _shapeDictionary.Add(shape, name);
                        }
                        else
                        {
                            nameList.Add($"{shape}");
                            _shapeDictionary.Add(shape, shape);
                        }
                    }
                    else
                    {
                        if (ShapeNameDictionary.ContainsKey(shape))
                        {
                            var name = $"{shape} - {ShapeNameDictionary[shape]}";
                            nameList.Add(name);
                            _shapeDictionary.Add(shape, name);
                        }
                        else
                        {
                            nameList.Add(shape);
                            _shapeDictionary.Add(shape, shape);
                        }
                    }
                }

            }

            if (_shapeDictionary.Count > 0)
            {
                ShapeDataCheckBoxEnabled = true;
            }
        }

        /// <summary>
        /// Event handler for DAE file selector
        /// </summary>
        private void SelectDaeFile(object obj)
        {
            var saveDir = new DirectoryInfo(Settings.Default.Save_Directory);
            var path = new DirectoryInfo($"{IOUtil.MakeItemSavePath(_itemModel, saveDir, _selectedRace)}\\3D");

            var openFileDialog = new OpenFileDialog
            {
                InitialDirectory = path.FullName,
                Filter = "Collada DAE (*.dae)|*.dae"
            };

            if (openFileDialog.ShowDialog() == DialogResult.OK)
            {
                DaeLocationText = openFileDialog.FileName;

                try
                {
                    var quickColladaData = _dae.QuickColladaReader(new DirectoryInfo(openFileDialog.FileName), _xivMdl);
                    _daeMeshPartDictionary = quickColladaData.MeshPartDictionary;
                    _colladaBoneList = quickColladaData.BoneList;
                    ImportButtonEnabled = true;

                    Initialize(true);
                }
                catch (Exception e)
                {
                    FlexibleMessageBox.Show(
                        string.Format(UIMessages.DAEReadErrorMessage, e.Message), UIMessages.DAEReadErrorTitle,
                        MessageBoxButtons.OK, MessageBoxIcon.Error);

                    DaeLocationText = string.Empty;
                }

            }
        }

        /// <summary>
        /// Adds text to the text box
        /// </summary>
        /// <param name="text">The text to add</param>
        /// <param name="color">The color of the text</param>
        private void AddText(string text, string color, bool bold)
        {
            var bc = new BrushConverter();
            var tr = new TextRange(_view.DaeInfoTextBox.Document.ContentEnd, _view.DaeInfoTextBox.Document.ContentEnd) { Text = text };
            try
            {
                tr.ApplyPropertyValue(TextElement.ForegroundProperty, bc.ConvertFromString(color));

                tr.ApplyPropertyValue(TextElement.FontWeightProperty, bold ? FontWeights.Bold : FontWeights.Normal);
            }
            catch (FormatException) { }
        }
        #endregion

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void NotifyPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        /// <summary>
        /// Attribute Name Dictionary
        /// </summary>
        private static readonly Dictionary<string, string> AttributeNameDictionary = new Dictionary<string, string>
        {
            {"none", "None" },
            {"atr_arm", "Arm"},
            {"atr_arrow", "Arrow"},
            {"atr_attach", "Attachment"},
            {"atr_hair", "Hair"},
            {"atr_hig", "Facial Hair"},
            {"atr_hij", "Lower Arm"},
            {"atr_hiz", "Upper Leg"},
            {"atr_hrn", "Horns"},
            {"atr_inr", "Neck"},
            {"atr_kam", "Hair"},
            {"atr_kao", "Face"},
            {"atr_kod", "Waist"},
            {"atr_leg", "Leg"},
            {"atr_lod", "LoD"},
            {"atr_lpd", "Feet Pads"},
            {"atr_mim", "Ear"},
            {"atr_nek", "Neck"},
            {"atr_sne", "Lower Leg"},
            {"atr_sta", "STA"},
            {"atr_tlh", "Tail Hide"},
            {"atr_tls", "Tail Show"},
            {"atr_top", "Top"},
            {"atr_ude", "Upper Arm"},
            {"atr_bv", "Body Part "},
            {"atr_dv", "Leg Part "},
            {"atr_mv", "Head Part "},
            {"atr_gv", "Hand Part "},
            {"atr_sv", "Feet Part "},
            {"atr_tv", "Top Part "},
            {"atr_fv", "Face Part "},
            {"atr_hv", "Hair Part "},
            {"atr_nv", "Neck Part "},
            {"atr_parts", "Part "},
            {"atr_rv", "RV Part "},
            {"atr_wv", "WV Part "},
            {"atr_ev", "EV Part "},
            {"atr_cn_ankle", "CN Ankle"},
            {"atr_cn_neck", "CN Neck"},
            {"atr_cn_waist", "CN Waist"},
            {"atr_cn_wrist", "CN Wrist"}
        };

        /// <summary>
        /// Shape Name Dictionary
        /// </summary>
        private static readonly Dictionary<string, string> ShapeNameDictionary = new Dictionary<string, string>
        {
            {"none", "None" },
            {"shp_hiz", "Upper Leg"},
            {"shp_kos", "Waist"},
            {"shp_mom", "Leg?"},
            {"shp_sne", "Lower Leg"},
            {"shp_leg", "Leg"},
            {"shp_sho", "Feet"},
            {"shp_arm", "Arm"},
            {"shp_kat", "Body?"},
            {"shp_hij", "Lower Arm"},
            {"shp_ude", "Upper Arm"},
            {"shp_nek", "Neck"},
            {"shp_brw", "Brow"},
            {"shp_chk", "Cheek"},
            {"shp_eye", "Eye"},
            {"shp_irs", "Iris"},
            {"shp_mth", "Mouth"},
            {"shp_nse", "Nose"}
        };
    }
}