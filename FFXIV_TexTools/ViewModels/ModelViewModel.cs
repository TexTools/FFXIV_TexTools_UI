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
using FFXIV_TexTools.Views;
using FFXIV_TexTools.Views.Models;
using ImageMagick;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Timers;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Input;
using xivModdingFramework.General.Enums;
using xivModdingFramework.Helpers;
using xivModdingFramework.Items.Categories;
using xivModdingFramework.Items.DataContainers;
using xivModdingFramework.Items.Enums;
using xivModdingFramework.Items.Interfaces;
using xivModdingFramework.Materials.DataContainers;
using xivModdingFramework.Materials.FileTypes;
using xivModdingFramework.Models.DataContainers;
using xivModdingFramework.Models.FileTypes;
using xivModdingFramework.Models.ModelTextures;
using xivModdingFramework.Mods;
using xivModdingFramework.Mods.Enums;
using xivModdingFramework.SqPack.FileTypes;
using Color = SharpDX.Color;
using ColorConverter = System.Windows.Media.ColorConverter;
using Timer = System.Timers.Timer;
using WinColor = System.Windows.Media.Color;

namespace FFXIV_TexTools.ViewModels
{
    public class ModelViewModel : INotifyPropertyChanged
    {
        private readonly ModelView _modelView;
        private ObservableCollection<ComboBoxData> _raceComboBoxData = new ObservableCollection<ComboBoxData>();
        private ObservableCollection<ComboBoxData> _partComboBoxData = new ObservableCollection<ComboBoxData>();
        private ObservableCollection<ComboBoxData> _numberComboBoxData = new ObservableCollection<ComboBoxData>();
        private ObservableCollection<ComboBoxData> _meshComboBoxData = new ObservableCollection<ComboBoxData>();

        private int _raceIndex, _partIndex, _numberIndex, _meshIndex, _partCount, _numberCount, _meshCount, _raceCount, _reflectionValue, _checkedLight;
        private float _lightingXValue, _lightingYValue, _lightingZValue;
        private bool _raceEnabled, _partEnabled, _numberEnabled, _meshEnabled, _exportEnabled, _importEnabled, _basicImportEnabled, _modStatusToggleEnabled, _updateTexEnabled, _flyoutOpen;
        private string _partWatermark = XivStrings.Part, _numberWatermark = XivStrings.Number, _raceWatermark = XivStrings.Race, 
            _meshWatermark = XivStrings.Mesh, _pathString;

        private string _lightXLabel = "X  |  0", _lightYLabel = "Y  |  0", _lightZLabel = "Z  |  0", _reflectionLabel = "Reflection  |  1", _modToggleText = "Enable/Disable", _modelStatusLabel;
        private ComboBoxData _selectedRace, _selectedPart, _selectedNumber, _selectedMesh;
        private Visibility _numberVisibility, _partVisibility, _lightToggleVisibility = Visibility.Collapsed;
        private bool _light1Check = true, _light2Check, _light3Check, _lightRenderToggle, _transparencyToggle, _cullModeToggle;

        private IItemModel _item;
        private Dictionary<XivRace, int[]> _charaRaceAndNumberDictionary;
        private Mdl _mdl;
        private XivMdl _mdlData;
        private Viewport3DViewModel _viewPortVM;
        private Timer _modelStatusTimer;

        private Dictionary<int, ModelTextureData> _materialDictionary;

        private DirectoryInfo _gameDirectory;

        public ModelViewModel(ModelView modelView)
        {
            ViewPortVM = new Viewport3DViewModel(this);
            _modelView = modelView;
        }

        /// <summary>
        /// Updates the model to display
        /// </summary>
        /// <remarks>
        /// This is also where the races are obtained
        /// </remarks>
        /// <param name="itemModel">The model to update to</param>
        public void UpdateModel(IItemModel itemModel)
        {
            ClearAll();

            _item = itemModel;

            _gameDirectory = new DirectoryInfo(Settings.Default.FFXIV_Directory);
            _mdl = new Mdl(_gameDirectory, _item.DataFile);

            if (itemModel.Category.Equals(XivStrings.Gear))
            {
                var gear = new Gear(_gameDirectory, GetLanguage());

                var xivGear = itemModel as XivGear;

                var raceList = gear.GetRacesForModels(xivGear, _item.DataFile);

                foreach (var xivRace in raceList)
                {
                    var raceCBD = new ComboBoxData { Name = xivRace.GetDisplayName(), XivRace = xivRace };

                    Races.Add(raceCBD);
                }
            }
            else if (itemModel.Category.Equals(XivStrings.Companions))
            {
                var companions = new Companions(_gameDirectory, GetLanguage());

                Races.Add(_item.ModelInfo.ModelType == XivItemType.demihuman
                    ? new ComboBoxData { Name = XivRace.DemiHuman.GetDisplayName(), XivRace = XivRace.DemiHuman }
                    : new ComboBoxData { Name = XivRace.Monster.GetDisplayName(), XivRace = XivRace.Monster });
            }
            else if (itemModel.Category.Equals(XivStrings.Character))
            {
                var character = new Character(_gameDirectory);

                _charaRaceAndNumberDictionary = character.GetRacesAndNumbersForModels(_item as XivCharacter);

                foreach (var racesAndNumber in _charaRaceAndNumberDictionary)
                {
                    Races.Add(new ComboBoxData { Name = racesAndNumber.Key.GetDisplayName(), XivRace = racesAndNumber.Key });
                }
            }
            else if (itemModel.Category.Equals(XivStrings.Housing))
            {
                Races.Add(new ComboBoxData { Name = XivRace.All_Races.GetDisplayName(), XivRace = XivRace.All_Races });
            }

            _raceCount = Races.Count;

            RaceComboboxEnabled = _raceCount > 1;

            SelectedRaceIndex = 0;

            CullModeToggle = Settings.Default.Cull_Mode == "None";

            ShowModelStatus("Model Loaded Successfully");
        }

        #region Race

        /// <summary>
        /// The collection of races that goes into the race combobox
        /// </summary>
        public ObservableCollection<ComboBoxData> Races
        {
            get => _raceComboBoxData;
            set { _raceComboBoxData = value; NotifyPropertyChanged(nameof(Races)); }
        }
        
        /// <summary>
        /// The selected race within the race combobox
        /// </summary>
        public ComboBoxData SelectedRace
        {
            get => _selectedRace;
            set
            {
                _selectedRace = value;
                NotifyPropertyChanged(nameof(SelectedRace));
                if (SelectedRaceIndex > -1)
                {
                    Parts.Clear();
                    Numbers.Clear();

                    GetNumbers();
                }
            }
        }

        /// <summary>
        /// The selected index of the race within the race combobox
        /// </summary>
        public int SelectedRaceIndex
        {
            get => _raceIndex;
            set
            {
                _raceIndex = value;
                NotifyPropertyChanged(nameof(SelectedRaceIndex));
            }
        }

        /// <summary>
        /// The enabled status of the race combobox
        /// </summary>
        public bool RaceComboboxEnabled
        {
            get => _raceEnabled;
            set { _raceEnabled = value; NotifyPropertyChanged(nameof(RaceComboboxEnabled)); }
        }

        #endregion

        #region Numbers

        /// <summary>
        /// Gets the numbers for applicable models
        /// </summary>
        private void GetNumbers()
        {
            if (_item.Category.Equals(XivStrings.Gear) || _item.Category.Equals(XivStrings.Companions) || _item.Category.Equals(XivStrings.Housing))
            {
                GetParts();
            }
            else if (_item.Category.Equals(XivStrings.Character))
            {
                NumberVisibility = Visibility.Visible;
                PartVisibility = Visibility.Visible;
                var numbers = _charaRaceAndNumberDictionary[SelectedRace.XivRace];

                Array.Sort(numbers);

                foreach (var number in numbers)
                {
                    Numbers.Add(new ComboBoxData{Name = number.ToString()});
                }

                _numberCount = numbers.Length;
                NumberComboboxEnabled = _numberCount > 1;
                SelectedNumberIndex = 0;
            }
            else
            {
                GetMeshes();
            }
        }

        /// <summary>
        /// The collection of numbers that go into the number combobox
        /// </summary>
        public ObservableCollection<ComboBoxData> Numbers
        {
            get => _numberComboBoxData;
            set { _numberComboBoxData = value; NotifyPropertyChanged(nameof(Numbers)); }
        }

        /// <summary>
        /// The selected number within the number combobox
        /// </summary>
        public ComboBoxData SelectedNumber
        {
            get => _selectedNumber;
            set
            {
                _selectedNumber = value;
                NotifyPropertyChanged(nameof(SelectedNumber));
                if (SelectedNumberIndex > -1)
                {
                    Parts.Clear();
                    GetParts();
                }
            }
        }

        /// <summary>
        /// The selected index within the number combobox
        /// </summary>
        public int SelectedNumberIndex
        {
            get => _numberIndex;
            set
            {
                _numberIndex = value;
                NotifyPropertyChanged(nameof(SelectedNumberIndex));
            }
        }

        /// <summary>
        /// The enabled status of the number combobox
        /// </summary>
        public bool NumberComboboxEnabled
        {
            get => _numberEnabled;
            set { _numberEnabled = value; NotifyPropertyChanged(nameof(NumberComboboxEnabled)); }
        }

        /// <summary>
        /// The visibility of the number combobox
        /// </summary>
        public Visibility NumberVisibility
        {
            get => _numberVisibility;
            set
            {
                _numberVisibility = value;
                NotifyPropertyChanged(nameof(NumberVisibility));
            }
        }

        #endregion

        #region Part

        /// <summary>
        /// Gets the parts for applicable models
        /// </summary>
        private void GetParts()
        {
            if (_item.Category.Equals(XivStrings.Gear))
            {
                var xivGear = _item as XivGear;

                if (xivGear.ItemCategory.Equals("Rings"))
                {
                    Parts.Add(new ComboBoxData { Name = "Right" });
                    Parts.Add(new ComboBoxData { Name = "Left" });
                }
                else
                {
                    Parts.Add(new ComboBoxData { Name = "Primary" });

                    if (xivGear.SecondaryModelInfo != null && xivGear.SecondaryModelInfo.ModelID > 0)
                    {
                        Parts.Add(new ComboBoxData { Name = "Secondary" });
                    }
                }

                PartVisibility = Visibility.Visible;
            }
            else if (_item.Category.Equals(XivStrings.Character))
            {
                var character = new Character(_gameDirectory);

                var parts = character.GetTypeForModels(_item as XivCharacter, SelectedRace.XivRace,
                    int.Parse(SelectedNumber.Name));

                foreach (var part in parts)
                {
                    Parts.Add(new ComboBoxData{ Name = part });
                }
            }
            else if (_item.Category.Equals(XivStrings.Companions))
            {
                if (_item.ModelInfo.ModelType == XivItemType.demihuman)
                {
                    var companions = new Companions(_gameDirectory, GetLanguage());
                    var parts = companions.GetDemiHumanMountModelEquipPartList(_item);

                    foreach (var part in parts)
                    {
                        Parts.Add(new ComboBoxData{Name = part});
                    }

                    PartVisibility = Visibility.Visible;
                }
                else
                {
                    GetMeshes();
                }
            }
            else if (_item.Category.Equals(XivStrings.Housing))
            {
                var housing = new Housing(_gameDirectory, GetLanguage());
                var partsDictionary = housing.GetFurnitureModelParts(_item);

                foreach (var part in partsDictionary)
                {
                    Parts.Add(new ComboBoxData { Name = part.Key, MdlPath = part.Value });
                }

                PartVisibility = Visibility.Visible;
            }

            _partCount = Parts.Count;
            PartComboboxEnabled = _partCount > 1;
            SelectedPartIndex = 0;
        }

        /// <summary>
        /// The collection of parts that go into the part combobox
        /// </summary>
        public ObservableCollection<ComboBoxData> Parts
        {
            get => _partComboBoxData;
            set
            {
                _partComboBoxData = value;
                NotifyPropertyChanged(nameof(Parts));
            }
        }

        /// <summary>
        /// The selected part in the part combobox
        /// </summary>
        public ComboBoxData SelectedPart
        {
            get => _selectedPart;
            set
            {
                _selectedPart = value;
                NotifyPropertyChanged(nameof(SelectedPart));
                if (SelectedPartIndex > -1)
                {
                    GetMeshes();
                }
            }
        }

        /// <summary>
        /// The selected index in the part combobox
        /// </summary>
        public int SelectedPartIndex
        {
            get => _partIndex;
            set
            {
                _partIndex = value;
                NotifyPropertyChanged(nameof(SelectedPartIndex));
            }
        }

        /// <summary>
        /// The enabled status of the part combobox
        /// </summary>
        public bool PartComboboxEnabled
        {
            get => _partEnabled;
            set { _partEnabled = value; NotifyPropertyChanged(nameof(PartComboboxEnabled)); }
        }

        /// <summary>
        /// The visibility of the part combobox
        /// </summary>
        public Visibility PartVisibility
        {
            get => _partVisibility;
            set
            {
                _partVisibility = value;
                NotifyPropertyChanged(nameof(PartVisibility));
            }
        }

        #endregion

        #region Meshes

        /// <summary>
        /// Gets the meshes for the selected model
        /// </summary>
        private void GetMeshes()
        {
            Meshes.Clear();

            try
            {
                if (_item.Category.Equals(XivStrings.Gear))
                {
                    var xivGear = _item as XivGear;

                    if (SelectedPart.Name.Equals("Primary"))
                    {
                        _mdlData = _mdl.GetMdlData(xivGear, SelectedRace.XivRace);
                    }
                    else if (SelectedPart.Name.Equals("Secondary"))
                    {
                        _mdlData = _mdl.GetMdlData(xivGear, SelectedRace.XivRace, xivGear.SecondaryModelInfo);
                    }
                    else
                    {
                        _mdlData = _mdl.GetMdlData(xivGear, SelectedRace.XivRace, null, null, 0, SelectedPart.Name);
                    }
                }
                else if (_item.Category.Equals(XivStrings.Character))
                {
                    _item.ModelInfo = new XivModelInfo { Body = int.Parse(SelectedNumber.Name) };

                    ((XivCharacter)_item).ItemSubCategory = SelectedPart.Name;

                    _mdlData = _mdl.GetMdlData(_item, SelectedRace.XivRace);
                }
                else if (_item.Category.Equals(XivStrings.Companions))
                {
                    if (_item.ModelInfo.ModelType == XivItemType.demihuman)
                    {
                        ((XivMount)_item).ItemSubCategory = SelectedPart.Name;
                    }

                    _mdlData = _mdl.GetMdlData(_item, SelectedRace.XivRace);
                }
                else if (_item.Category.Equals(XivStrings.Housing))
                {
                    if (PartVisibility == Visibility.Visible)
                    {
                        ((XivFurniture)_item).ItemSubCategory = SelectedPart.Name;
                    }

                    _mdlData = _mdl.GetMdlData(_item, SelectedRace.XivRace, null, SelectedPart.MdlPath);
                }
            }
            catch(Exception ex)
            {
                var message =
                    $"There was an error reading the MDL file.\n\n{ex.Message}\n\nIf this error appeared after importing, please submit a bug report with the DAE file attached.";
                FlexibleMessageBox.Show(
                    message, "Error Reading Model Data",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            _meshCount = _mdlData.LoDList[0].MeshCount + _mdlData.LoDList[0].ExtraMeshCount;

            PathString = $"{_mdlData.MdlPath.Folder}/{_mdlData.MdlPath.File}";

            Meshes.Add(new ComboBoxData{Name = XivStrings.All});
            for (var i = 0; i < _meshCount; i++)
            {
                Meshes.Add(new ComboBoxData{Name = i.ToString()});
            }

            MeshComboboxEnabled = _meshCount > 1;
            SelectedMeshIndex = 0;

            var modList = new Modding(_gameDirectory);

            var modStatus = modList.IsModEnabled(PathString, false);

            switch (modStatus)
            {
                case XivModStatus.Enabled:
                    ModStatusToggleEnabled = true;
                    ModToggleText = "Disable";
                    break;
                case XivModStatus.Disabled:
                    ModStatusToggleEnabled = true;
                    ModToggleText = "Enable";
                    break;
                case XivModStatus.Original:
                default:
                    ModStatusToggleEnabled = false;
                    ModToggleText = "Enable/Disable";
                    break;
            }

            SetComboBoxWatermarks();

            UpdateViewPort();
        }

        /// <summary>
        /// The collection of meshes that go into the mesh combobox
        /// </summary>
        public ObservableCollection<ComboBoxData> Meshes
        {
            get => _meshComboBoxData;
            set
            {
                _meshComboBoxData = value;
                NotifyPropertyChanged(nameof(Meshes));
            }
        }

        /// <summary>
        /// The selected mesh in the mesh combobox
        /// </summary>
        public ComboBoxData SelectedMesh
        {
            get => _selectedMesh;
            set
            {
                _selectedMesh = value;
                NotifyPropertyChanged(nameof(SelectedMesh));
                if (SelectedMeshIndex > -1)
                {
                    ViewPortVM.VisibleModels(_selectedMesh.Name);
                }
            }
        }

        /// <summary>
        /// The selected mesh index in the mesh combobox
        /// </summary>
        public int SelectedMeshIndex
        {
            get => _meshIndex;
            set
            {
                _meshIndex = value;
                NotifyPropertyChanged(nameof(SelectedMeshIndex));
            }
        }

        /// <summary>
        /// The enabled status of the mesh combobox
        /// </summary>
        public bool MeshComboboxEnabled
        {
            get => _meshEnabled;
            set { _meshEnabled = value; NotifyPropertyChanged(nameof(MeshComboboxEnabled)); }
        }

        #endregion

        #region WaterMarks

        /// <summary>
        /// Sets the watermark for the comboboxes
        /// </summary>
        private void SetComboBoxWatermarks()
        {
            if (_item == null) return;

            RaceWatermark = $"{XivStrings.Race}  |  {_raceCount}";
            MeshWatermark = $"{XivStrings.Mesh}  |  {_meshCount}";
            PartWatermark = $"{XivStrings.Part}  |  {_partCount}";

            if (_item.Category.Equals(XivStrings.Character))
            {
                NumberWatermark = $"{XivStrings.Number}  |  {_numberCount}";
            }
        }

        /// <summary>
        /// The watermark for the race combobox
        /// </summary>
        public string RaceWatermark
        {
            get => _raceWatermark;
            set
            {
                _raceWatermark = value;
                NotifyPropertyChanged(nameof(RaceWatermark));
            }
        }

        /// <summary>
        /// The watermark for the part combobox
        /// </summary>
        public string PartWatermark
        {
            get => _partWatermark;
            set
            {
                _partWatermark = value;
                NotifyPropertyChanged(nameof(PartWatermark));
            }
        }

        /// <summary>
        /// The watermark for the number combobox
        /// </summary>
        public string NumberWatermark
        {
            get => _numberWatermark;
            set
            {
                _numberWatermark = value;
                NotifyPropertyChanged(nameof(NumberWatermark));
            }
        }

        /// <summary>
        /// The watermark for the mesh combobox
        /// </summary>
        public string MeshWatermark
        {
            get => _meshWatermark;
            set
            {
                _meshWatermark = value;
                NotifyPropertyChanged(nameof(MeshWatermark));
            }
        }

        #endregion

        #region ViewPort Options

        /// <summary>
        /// Opens and closes the viewer option flyout
        /// </summary>
        private void ViewerOptions(object obj)
        {
            FlyoutOpen = !FlyoutOpen;
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
        /// The current reflection value of the model
        /// </summary>
        public int ReflectionValue
        {
            get => _reflectionValue;
            set
            {
                _reflectionValue = value;
                ViewPortVM.UpdateReflection(value);
                ReflectionLabel = $"Reflection  |  {value}";
                NotifyPropertyChanged(nameof(ReflectionValue));
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
        /// The visiblity of the light render toggle
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

            Settings.Default.Cull_Mode = noneCull ? "None" : "Back";

            Settings.Default.Save();
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

        #endregion

        #region Buttons

        /// <summary>
        /// The enabled status of the export button
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
        /// The enabled status of the import button
        /// </summary>
        public bool ImportEnabled
        {
            get => _importEnabled;
            set
            {
                _importEnabled = value;
                NotifyPropertyChanged(nameof(ImportEnabled));
            }
        }

        /// <summary>
        /// The enabled status of the basic import button
        /// </summary>
        public bool BasicImportEnabled
        {
            get => _basicImportEnabled;
            set
            {
                _basicImportEnabled = value;
                NotifyPropertyChanged(nameof(BasicImportEnabled));
            }
        }

        /// <summary>
        /// The enabled status of the mod status button
        /// </summary>
        public bool ModStatusToggleEnabled
        {
            get => _modStatusToggleEnabled;
            set
            {
                _modStatusToggleEnabled = value;
                NotifyPropertyChanged(nameof(ModStatusToggleEnabled));
            }
        }

        /// <summary>
        /// The enabled status of the update texture button
        /// </summary>
        public bool UpdateTexEnabled
        {
            get => _updateTexEnabled;
            set
            {
                _updateTexEnabled = value;
                NotifyPropertyChanged(nameof(UpdateTexEnabled));
            }
        }

        // Button Commands
        public ICommand ExportDaePlusMaterialsCommand => new RelayCommand(ExportDaePlusMaterials);
        public ICommand ExportDaeCommand => new RelayCommand(ExportDae);
        public ICommand ExportObjCommand => new RelayCommand(ExportObj);
        public ICommand ViewOptionsCommand => new RelayCommand(ViewerOptions);
        public ICommand ModStatusToggleButton => new RelayCommand(ModStatusToggle);
        public ICommand UpdateTexButton => new RelayCommand(UpdateTex);

        public ICommand ImportCommand => new RelayCommand(Import);
        public ICommand ImportFromCommand => new RelayCommand(ImportFrom);
        public ICommand AdvancedImportCommand => new RelayCommand(AdvancedImport);
        public ICommand OpenFolder => new RelayCommand(OpenSavedFolder);
        public ICommand ModelInspector => new RelayCommand(OpenModelInspector);

        /// <summary>
        /// Opens the folder in which the exported data is saved
        /// </summary>
        /// <remarks>
        /// Creates the save directory if it does not exist
        /// </remarks>
        private void OpenSavedFolder(object obj)
        {
            var savePath = new DirectoryInfo(Settings.Default.Save_Directory);
            var path = savePath.FullName;

            if (_item != null)
            {
                path = $"{IOUtil.MakeItemSavePath(_item, savePath, SelectedRace.XivRace)}\\3D";
            }

            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }

            Process.Start(path);
        }

        private void OpenModelInspector(object obj)
        {
            if (_mdlData != null)
            {
                var modelInspector = new ModelInspector(_mdlData);
                modelInspector.Owner = Window.GetWindow(_modelView);
                modelInspector.ShowDialog();
            }
        }

        #endregion

        #region ModStatus

        /// <summary>
        /// The text for the mod toggle button
        /// </summary>
        public string ModToggleText
        {
            get => _modToggleText;
            set
            {
                _modToggleText = value;
                NotifyPropertyChanged(nameof(ModToggleText));
            }
        }

        /// <summary>
        /// THe label for the model status
        /// </summary>
        public string ModelStatusLabel
        {
            get => _modelStatusLabel;
            set
            {
                _modelStatusLabel = value;
                NotifyPropertyChanged(nameof(ModelStatusLabel));
            }
        }

        /// <summary>
        /// Shows the model status and starts timer
        /// </summary>
        /// <param name="status">The status message</param>
        private void ShowModelStatus(string status)
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

        /// <summary>
        /// Event handler for model status timer elapsed, which clears the text
        /// </summary>
        private void ModelStatusTimerOnElapsed(object sender, ElapsedEventArgs e)
        {
            if (_modelStatusTimer != null)
            {
                _modelStatusTimer.Stop();
                _modelStatusTimer.Dispose();
            }

            ModelStatusLabel = string.Empty;
        }

        /// <summary>
        /// Toggles the mod for the selected item ON or OFF
        /// </summary>
        private void ModStatusToggle(object obj)
        {
            var modlist = new Modding(_gameDirectory);

            if (ModToggleText.Equals("Enable"))
            {
                modlist.ToggleModStatus(PathString, true);
            }
            else if (ModToggleText.Equals("Disable"))
            {
                modlist.ToggleModStatus(PathString, false);
            }
            else
            {
                FlexibleMessageBox.Show(
                    $"There was an error attempting to toggle mod status.", "Error Toggling Mod",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            GetMeshes();
        }

        #endregion

        #region Export

        /// <summary>
        /// Exports the DAE file and Materials for the current model
        /// </summary>
        /// <param name="obj"></param>
        private void ExportDaePlusMaterials(object obj)
        {
            try
            {
                var appVersion = FileVersionInfo.GetVersionInfo(System.Reflection.Assembly.GetExecutingAssembly().Location).FileVersion;
                var dae = new Dae(_gameDirectory, _item.DataFile, Settings.Default.DAE_Plugin_Target, appVersion);
                var saveDir = new DirectoryInfo(Settings.Default.Save_Directory);
                dae.MakeDaeFileFromModel(_item, _mdlData, saveDir, SelectedRace.XivRace);
                _modelView.BottomFlyout.IsOpen = false;
                ImportEnabled = true;
                BasicImportEnabled = true;
                ExportMaterials();
            }
            catch (Exception e)
            {
                FlexibleMessageBox.Show(
                    $"There was an error attempting to export the model as a dae.\n\n{e.Message}", "Error exporting Dae",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

        }

        /// <summary>
        /// Exports the materials for the current model
        /// </summary>
        private void ExportMaterials()
        {
            var modelName = Path.GetFileNameWithoutExtension(_mdlData.MdlPath.File);

            foreach (var materialDict in _materialDictionary)
            {
                var modelMaps = materialDict.Value;
                var matNum = materialDict.Key;

                var saveDir = new DirectoryInfo(Settings.Default.Save_Directory);

                var path = $"{IOUtil.MakeItemSavePath(_item, saveDir, SelectedRace.XivRace)}\\3D";

                var pixelSettings =
                    new PixelReadSettings(modelMaps.Width, modelMaps.Height, StorageType.Char, PixelMapping.RGBA);

                if (modelMaps.Diffuse != null && modelMaps.Diffuse.Length > 0)
                {
                    using (var magickImage = new MagickImage(modelMaps.Diffuse, pixelSettings))
                    {
                        magickImage.Settings.SetDefine("bmp3:alpha", "true");
                        magickImage.Format = MagickFormat.Bmp3;
                        magickImage.Write($"{path}\\{modelName}_{matNum}_Diffuse.bmp");
                    }
                }

                if (modelMaps.Normal != null && modelMaps.Normal.Length > 0)
                {
                    using (var magickImage = new MagickImage(modelMaps.Normal, pixelSettings))
                    {
                        magickImage.Settings.SetDefine("bmp3:alpha", "true");
                        magickImage.Format = MagickFormat.Bmp3;
                        magickImage.Write($"{path}\\{modelName}_{matNum}_Normal.bmp");
                    }
                }

                if (modelMaps.Specular != null && modelMaps.Specular.Length > 0)
                {
                    using (var magickImage = new MagickImage(modelMaps.Specular, pixelSettings))
                    {
                        magickImage.Format = MagickFormat.Bmp3;
                        magickImage.Write($"{path}\\{modelName}_{matNum}_Specular.bmp");
                    }
                }

                if (modelMaps.Alpha != null && modelMaps.Alpha.Length > 0)
                {
                    using (var magickImage = new MagickImage(modelMaps.Alpha, pixelSettings))
                    {
                        magickImage.Format = MagickFormat.Bmp3;
                        magickImage.Write($"{path}\\{modelName}_{matNum}_Alpha.bmp");
                    }
                }

                if (modelMaps.Emissive != null && modelMaps.Emissive.Length > 0)
                {
                    using (var magickImage = new MagickImage(modelMaps.Emissive, pixelSettings))
                    {
                        magickImage.Format = MagickFormat.Bmp3;
                        magickImage.Write($"{path}\\{modelName}_{matNum}_Emissive.bmp");
                    }
                }
            }
        }

        /// <summary>
        /// Exports the DAE file for the current model
        /// </summary>
        private void ExportDae(object obj)
        {
            try
            {
                var appVersion = FileVersionInfo.GetVersionInfo(System.Reflection.Assembly.GetExecutingAssembly().Location).FileVersion;
                var dae = new Dae(_gameDirectory, _item.DataFile, Settings.Default.DAE_Plugin_Target, appVersion);
                var saveDir = new DirectoryInfo(Settings.Default.Save_Directory);
                dae.MakeDaeFileFromModel(_item, _mdlData, saveDir, SelectedRace.XivRace);
            }
            catch (Exception e)
            {
                FlexibleMessageBox.Show(
                    $"There was an error attempting to export the model as a dae.\n\n{e.Message}", "Error Exporting Dae",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

            _modelView.BottomFlyout.IsOpen = false;
            ImportEnabled = true;
            BasicImportEnabled = true;
        }

        /// <summary>
        /// Exports the OBJ file for the current model
        /// </summary>
        private void ExportObj(object o)
        {
            var obj = new Obj(_gameDirectory);
            var saveDir = new DirectoryInfo(Settings.Default.Save_Directory);

            _modelView.BottomFlyout.IsOpen = false;
            obj.ExportObj(_item, _mdlData, saveDir, SelectedRace.XivRace);
        }

        #endregion

        #region Import

        /// <summary>
        /// Imports the DAE for the model
        /// </summary>
        /// <remarks>
        /// This will import the DAE file with the same name and location as the exported model
        /// </remarks>
        private void Import(object obj)
        {
            var gameDirectory = new DirectoryInfo(Settings.Default.FFXIV_Directory);
            var index = new Index(gameDirectory);

            if (index.IsIndexLocked(XivDataFile._0A_Exd))
            {
                FlexibleMessageBox.Show("Error Accessing Index File\n\n" +
                                        "Please exit the game before proceeding.\n" +
                                        "-----------------------------------------------------\n\n",
                    "Index Access Error", MessageBoxButtons.OK, MessageBoxIcon.Error);

                return;
            }

            var saveDir = new DirectoryInfo(Settings.Default.Save_Directory);
            var path = $"{IOUtil.MakeItemSavePath(_item, saveDir, SelectedRace.XivRace)}\\3D";
            var modelName = Path.GetFileNameWithoutExtension(_mdlData.MdlPath.File);
            var savePath = new DirectoryInfo(Path.Combine(path, modelName) + ".dae");

            var modlist = new Modding(_gameDirectory);

            var mdlPath = Path.Combine(_mdlData.MdlPath.Folder, _mdlData.MdlPath.File);

            Dictionary<string, string> warnings;

            var modData = modlist.TryGetModEntry(mdlPath);

            try
            {
                // pass in the original mdl if the current one is modded
                if (modData != null && modData.enabled)
                {
                    var originalMdl = _mdl.GetMdlData(_item, SelectedRace.XivRace, null, modData.fullPath,
                        modData.data.originalOffset);

                    warnings = _mdl.ImportModel(_item, originalMdl, savePath, null, XivStrings.TexTools, Settings.Default.DAE_Plugin_Target);
                }
                else
                {
                    warnings = _mdl.ImportModel(_item, _mdlData, savePath, null, XivStrings.TexTools, Settings.Default.DAE_Plugin_Target);
                }
            }
            catch (Exception ex)
            {
                FlexibleMessageBox.Show(
                    $"There was an error attempting to import the model.\n\n{ex.Message}", "Error Importing Dae",
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

            ModStatusToggleEnabled = true;

            _modelView.BottomFlyout.IsOpen = false;
            GetMeshes();
        }

        /// <summary>
        /// Imports a DAE for the model
        /// </summary>
        /// <remarks>
        /// This will import a DAE file from any location
        /// </remarks>
        private void ImportFrom(object obj)
        {
            var gameDirectory = new DirectoryInfo(Settings.Default.FFXIV_Directory);
            var index = new Index(gameDirectory);

            if (index.IsIndexLocked(XivDataFile._0A_Exd))
            {
                FlexibleMessageBox.Show("Error Accessing Index File\n\n" +
                                        "Please exit the game before proceeding.\n" +
                                        "-----------------------------------------------------\n\n",
                    "Index Access Error", MessageBoxButtons.OK, MessageBoxIcon.Error);

                return;
            }

            var saveDir = new DirectoryInfo(Settings.Default.Save_Directory);
            var path = new DirectoryInfo($"{IOUtil.MakeItemSavePath(_item, saveDir, SelectedRace.XivRace)}\\3D");

            var modlist = new Modding(_gameDirectory);
            var mdlPath = Path.Combine(_mdlData.MdlPath.Folder, _mdlData.MdlPath.File);
            var modData = modlist.TryGetModEntry(mdlPath);

            var openFileDialog = new OpenFileDialog();
            openFileDialog.InitialDirectory = path.FullName;
            openFileDialog.Filter = "Collada DAE (*.dae)|*.dae";

            if (openFileDialog.ShowDialog() == DialogResult.OK)
            {
                Dictionary<string, string> warnings;

                try
                {
                    if (modData != null && modData.enabled)
                    {
                        var originalMdl = _mdl.GetMdlData(_item, SelectedRace.XivRace, null, modData.fullPath,
                            modData.data.originalOffset);

                        warnings = _mdl.ImportModel(_item, originalMdl, new DirectoryInfo(openFileDialog.FileName), null,
                            XivStrings.TexTools, Settings.Default.DAE_Plugin_Target);
                    }
                    else
                    {
                        warnings = _mdl.ImportModel(_item, _mdlData, new DirectoryInfo(openFileDialog.FileName), null,
                            XivStrings.TexTools, Settings.Default.DAE_Plugin_Target);
                    }
                }
                catch (Exception ex)
                {
                    FlexibleMessageBox.Show(
                        $"There was an error attempting to import the model.\n\n{ex.Message}", "Error Importing Dae",
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

                ModStatusToggleEnabled = true;

                GetMeshes();
            }

            _modelView.BottomFlyout.IsOpen = false;
        }

        /// <summary>
        /// Opens the advanced model import options
        /// </summary>
        /// <param name="obj"></param>
        private void AdvancedImport(object obj)
        {
            var gameDirectory = new DirectoryInfo(Settings.Default.FFXIV_Directory);
            var index = new Index(gameDirectory);

            if (index.IsIndexLocked(XivDataFile._0A_Exd))
            {
                FlexibleMessageBox.Show("Error Accessing Index File\n\n" +
                                        "Please exit the game before proceeding.\n" +
                                        "-----------------------------------------------------\n\n",
                    "Index Access Error", MessageBoxButtons.OK, MessageBoxIcon.Error);

                return;
            }

            var modlist = new Modding(_gameDirectory);
            var mdlPath = Path.Combine(_mdlData.MdlPath.Folder, _mdlData.MdlPath.File);
            bool? result = false;

            var modData = modlist.TryGetModEntry(mdlPath);

            try
            {
                // pass in the original mdl if the current one is modded
                if (modData != null && modData.enabled)
                {
                    var originalMdl = _mdl.GetMdlData(_item, SelectedRace.XivRace, null, modData.fullPath,
                        modData.data.originalOffset);

                    var advImportedView = new AdvancedModelImportView(originalMdl, _item, SelectedRace.XivRace, false)
                        { Owner = Window.GetWindow(_modelView) };
                    result = advImportedView.ShowDialog();
                }
                else
                {
                    var advImportedView = new AdvancedModelImportView(_mdlData, _item, SelectedRace.XivRace, false)
                        { Owner = Window.GetWindow(_modelView) };
                    result = advImportedView.ShowDialog();
                }
            }
            catch (Exception ex)
            {
                FlexibleMessageBox.Show(
                    $"There was an error building Advanced Import Window.\n\n{ex.Message}", "Advanced Import Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

            if (result == true)
            {
                GetMeshes();
            }

            _modelView.BottomFlyout.IsOpen = false;
        }

        #endregion

        #region Text

        /// <summary>
        /// The full path of the model file as a string
        /// </summary>
        public string PathString
        {
            get => _pathString;
            set
            {
                _pathString = value;
                NotifyPropertyChanged(nameof(PathString));
            }
        }

        #endregion

        #region ModelViewPort

        /// <summary>
        /// The 3D viewport viewmodel which handles the model display
        /// </summary>
        public Viewport3DViewModel ViewPortVM
        {
            get => _viewPortVM;
            set
            {
                _viewPortVM = value;
                NotifyPropertyChanged(nameof(ViewPortVM));
            }
        }

        /// <summary>
        /// Refreshes the viewport with the updated textures
        /// </summary>
        private void UpdateTex(object obj)
        {
            UpdateViewPort();
        }

        /// <summary>
        /// Updates the viewport to the selected model
        /// </summary>
        public void UpdateViewPort()
        {
            ViewPortVM.ClearModels();
            TransparencyToggle = false;

            _materialDictionary = GetMaterials();

            ViewPortVM.UpdateModel(_mdlData, _materialDictionary);

            ReflectionValue = ViewPortVM.SpecularShine;

            _modelView.viewport3DX.ZoomExtents();

            ExportEnabled = true;

            var saveDir = new DirectoryInfo(Settings.Default.Save_Directory);
            var path = $"{IOUtil.MakeItemSavePath(_item, saveDir, SelectedRace.XivRace)}\\3D";
            var modelName = Path.GetFileNameWithoutExtension(_mdlData.MdlPath.File);
            var savePath = Path.Combine(path, modelName) + ".dae";

            BasicImportEnabled = File.Exists(savePath);
            ImportEnabled = true;
            UpdateTexEnabled = true;

            ShowModelStatus("Model Updated Successfully");
        }
        #endregion

        #region Materials

        /// <summary>
        /// Gets the materials for the model
        /// </summary>
        /// <returns>A dictionary containing the mesh number(key) and the associated texture data (value)</returns>
        private Dictionary<int, ModelTextureData> GetMaterials()
        {
            var textureDataDictionary = new Dictionary<int, ModelTextureData>();
            var mtrlDictionary = new Dictionary<int, XivMtrl>();
            var mtrl = new Mtrl(_gameDirectory, _item.DataFile);
            var mtrlFilePaths = _mdlData.PathData.MaterialList;
            var hasColorChangeShader = false;
            Color? customColor = null;
            WinColor winColor;

            var race = SelectedRace.XivRace;

            var materialNum = 0;
            foreach (var mtrlFilePath in mtrlFilePaths)
            {
                var mtrlItem = new XivGenericItemModel
                {
                    Category = _item.Category,
                    ItemCategory = _item.ItemCategory,
                    ItemSubCategory = _item.ItemSubCategory,
                    ModelInfo = new XivModelInfo
                    {
                        Body = _item.ModelInfo.Body,
                        ModelID = _item.ModelInfo.ModelID,
                        ModelType = _item.ModelInfo.ModelType,
                        Variant = _item.ModelInfo.Variant
                    },
                    Name = _item.Name
                };

                var modelID = mtrlItem.ModelInfo.ModelID;
                var bodyID = mtrlItem.ModelInfo.Body;
                var filePath = mtrlFilePath;

                if (!filePath.Contains("hou") && mtrlFilePath.Count(x => x == '/') > 1)
                {
                    filePath = mtrlFilePath.Substring(mtrlFilePath.LastIndexOf("/"));
                }

                var typeChar = $"{mtrlFilePath[4]}{mtrlFilePath[9]}";

                var raceString = "";
                switch (typeChar)
                {
                    // Character Body
                    case "cb":
                        var body = mtrlFilePath.Substring(mtrlFilePath.IndexOf("b") + 1, 4);
                        raceString = mtrlFilePath.Substring(mtrlFilePath.IndexOf("c") + 1, 4);
                        race = XivRaces.GetXivRace(raceString);

                        if (!raceString.Equals("0901") && !raceString.Equals("1001") && !raceString.Equals("1101"))
                        {
                            var gender = 0;
                            if (int.Parse(raceString.Substring(0, 2)) % 2 == 0)
                            {
                                gender = 1;
                            }

                            var settingsRace = GetSettingsRace(gender);

                            race = settingsRace.Race;

                            filePath = mtrlFilePath.Replace(raceString, race.GetRaceCode()).Replace(body, settingsRace.BodyID);

                            body = settingsRace.BodyID;
                        }


                        mtrlItem = new XivGenericItemModel
                        {
                            Category = XivStrings.Character,
                            ItemCategory = XivStrings.Body,
                            Name = XivStrings.Body,
                            ModelInfo = new XivModelInfo
                            {
                                Body = int.Parse(body)
                            }
                        };

                        winColor = (WinColor)ColorConverter.ConvertFromString(Settings.Default.Skin_Color);
                        customColor = new Color(winColor.R, winColor.G, winColor.B, winColor.A);

                        break;
                    // Face
                    case "cf":
                        bodyID = int.Parse(mtrlFilePath.Substring(mtrlFilePath.IndexOf("f") + 1, 4));
                        raceString = mtrlFilePath.Substring(mtrlFilePath.IndexOf("c") + 1, 4);
                        race = XivRaces.GetXivRace(raceString);

                        mtrlItem = new XivGenericItemModel
                        {
                            Category = XivStrings.Character,
                            ItemCategory = XivStrings.Face,
                            Name = XivStrings.Face,
                            ModelInfo = new XivModelInfo
                            {
                                Body = bodyID
                            }
                        };

                        break;
                    // Hair
                    case "ch":
                        bodyID = int.Parse(mtrlFilePath.Substring(mtrlFilePath.IndexOf("h") + 1, 4));
                        raceString = mtrlFilePath.Substring(mtrlFilePath.IndexOf("c") + 1, 4);
                        race = XivRaces.GetXivRace(raceString);

                        mtrlItem = new XivGenericItemModel
                        {
                            Category = XivStrings.Character,
                            ItemCategory = XivStrings.Hair,
                            Name = XivStrings.Hair,
                            ModelInfo = new XivModelInfo
                            {
                                Body = bodyID
                            }
                        };

                        winColor = (WinColor)ColorConverter.ConvertFromString(Settings.Default.Hair_Color);
                        customColor = new Color(winColor.R, winColor.G, winColor.B, winColor.A);

                        break;
                    // Tail
                    case "ct":
                        var tempPath = mtrlFilePath.Substring(4);
                        bodyID = int.Parse(tempPath.Substring(tempPath.IndexOf("t") + 1, 4));
                        raceString = mtrlFilePath.Substring(mtrlFilePath.IndexOf("c") + 1, 4);
                        race = XivRaces.GetXivRace(raceString);

                        mtrlItem = new XivGenericItemModel
                        {
                            Category = XivStrings.Character,
                            ItemCategory = XivStrings.Tail,
                            Name = XivStrings.Tail,
                            ModelInfo = new XivModelInfo
                            {
                                Body = bodyID
                            }
                        };

                        winColor = (WinColor)ColorConverter.ConvertFromString(Settings.Default.Hair_Color);
                        customColor = new Color(winColor.R, winColor.G, winColor.B, winColor.A);

                        break;
                    // Equipment
                    case "ce":
                        modelID = int.Parse(mtrlFilePath.Substring(mtrlFilePath.IndexOf("e") + 1, 4));
                        raceString = mtrlFilePath.Substring(mtrlFilePath.IndexOf("c") + 1, 4);
                        race = XivRaces.GetXivRace(raceString);

                        mtrlItem.ModelInfo.ModelID = modelID;
                        break;
                    // Accessory
                    case "ca":
                        modelID = int.Parse(mtrlFilePath.Substring(mtrlFilePath.IndexOf("a") + 1, 4));
                        raceString = mtrlFilePath.Substring(mtrlFilePath.IndexOf("c") + 1, 4);
                        race = XivRaces.GetXivRace(raceString);

                        mtrlItem.ModelInfo.ModelID = modelID;
                        break;
                    // Weapon
                    case "wb":
                        modelID = int.Parse(mtrlFilePath.Substring(mtrlFilePath.IndexOf("w") + 1, 4));
                        bodyID = int.Parse(mtrlFilePath.Substring(mtrlFilePath.IndexOf("b") + 1, 4));
                        mtrlItem.ModelInfo.ModelID = modelID;
                        mtrlItem.ModelInfo.Body = bodyID;
                        break;
                    // Monster
                    case "mb":
                        modelID = int.Parse(mtrlFilePath.Substring(mtrlFilePath.IndexOf("_m") + 2, 4));
                        bodyID = int.Parse(mtrlFilePath.Substring(mtrlFilePath.IndexOf("b") + 1, 4));
                        mtrlItem.ModelInfo.ModelID = modelID;
                        mtrlItem.ModelInfo.Body = bodyID;
                        break;
                    // DemiHuman
                    case "de":
                        modelID = int.Parse(mtrlFilePath.Substring(mtrlFilePath.IndexOf("d") + 1, 4));
                        bodyID = int.Parse(mtrlFilePath.Substring(mtrlFilePath.IndexOf("e") + 1, 4));
                        mtrlItem.ModelInfo.ModelID = modelID;
                        mtrlItem.ModelInfo.Body = bodyID;
                        break;
                    default:
                        break;
                }

                var dxVersion = int.Parse(Settings.Default.DX_Version);

                var mtrlData = mtrl.GetMtrlData(mtrlItem, race, filePath.Remove(0, 1), dxVersion);

                if (mtrlData.Shader.Contains("colorchange"))
                {
                    hasColorChangeShader = true;
                }

                mtrlDictionary.Add(materialNum, mtrlData);

                materialNum++;
            }

            foreach (var xivMtrl in mtrlDictionary)
            {
                var modelTexture = new ModelTexture(_gameDirectory, xivMtrl.Value);

                if (hasColorChangeShader)
                {
                    var modelMaps = modelTexture.GetModelMaps(null, true);

                    textureDataDictionary.Add(xivMtrl.Key, modelMaps);
                }
                else
                {
                    if (_item.ItemCategory.Equals(XivStrings.Face))
                    {
                        var path = xivMtrl.Value.MTRLPath;

                        if (path.Contains("_iri_"))
                        {
                            winColor = (WinColor)ColorConverter.ConvertFromString(Settings.Default.Iris_Color);
                        }
                        else if (path.Contains("_etc_"))
                        {
                            winColor = (WinColor)ColorConverter.ConvertFromString(Settings.Default.Etc_Color);
                        }
                        else
                        {
                            winColor = (WinColor)ColorConverter.ConvertFromString(Settings.Default.Skin_Color);
                        }

                        customColor = new Color(winColor.R, winColor.G, winColor.B, winColor.A);
                    }

                    var modelMaps = modelTexture.GetModelMaps(customColor);

                    textureDataDictionary.Add(xivMtrl.Key, modelMaps);
                }
            }

            return textureDataDictionary;
        }

        /// <summary>
        /// Gets the race from the settings
        /// </summary>
        /// <param name="gender">The gender of the currently selected race</param>
        /// <returns>A tuple containing the race and body</returns>
        private (XivRace Race, string BodyID) GetSettingsRace(int gender)
        {
            var settingsRace = Settings.Default.Default_Race;
            var defaultBody = "0001";

            if (settingsRace.Equals(XivStringRaces.Hyur_M))
            {
                if (gender == 0)
                {
                    return (XivRaces.GetXivRace("0101"), defaultBody);
                }
            }

            if (settingsRace.Equals(XivStringRaces.Hyur_H))
            {
                if (gender == 0)
                {
                    return (XivRaces.GetXivRace("0301"), defaultBody);
                }

                return (XivRaces.GetXivRace("0401"), defaultBody);
            }

            if (settingsRace.Equals(XivStringRaces.Aura_R))
            {
                if (gender == 0)
                {
                    return (XivRaces.GetXivRace("1301"), defaultBody);
                }

                return (XivRaces.GetXivRace("1401"), defaultBody);
            }

            if (settingsRace.Equals(XivStringRaces.Aura_X))
            {
                if (gender == 0)
                {
                    return (XivRaces.GetXivRace("1301"), "0101");
                }

                return (XivRaces.GetXivRace("1401"), "0101");
            }

            return (XivRaces.GetXivRace("0201"), defaultBody);
        }

        #endregion

        /// <summary>
        /// Clears all data
        /// </summary>
        private void ClearAll()
        {
            Races.Clear();
            SelectedRaceIndex = -1;
            Parts.Clear();
            SelectedPartIndex = -1;
            Meshes.Clear();
            SelectedMeshIndex = -1;
            Numbers.Clear();
            SelectedNumberIndex = -1;
            _item = null;
            _materialDictionary = null;

            NumberVisibility = Visibility.Collapsed;
            PartVisibility = Visibility.Collapsed;
            TransparencyToggle = false;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void NotifyPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

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

            /// <summary>
            /// The MDL path
            /// </summary>
            public string MdlPath { get; set; }
        }

        /// <summary>
        /// Gets the language for the application
        /// </summary>
        /// <returns>The application language as XivLanguage</returns>
        private static XivLanguage GetLanguage()
        {
            return XivLanguages.GetXivLanguage(Properties.Settings.Default.Application_Language);
        }
    }
}