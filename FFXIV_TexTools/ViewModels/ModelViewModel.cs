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
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Bmp;
using SixLabors.ImageSharp.PixelFormats;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Resources;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Input;
using xivModdingFramework.Cache;
using xivModdingFramework.General.Enums;
using xivModdingFramework.Helpers;
using xivModdingFramework.Items;
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
        private readonly ModelView _view;
        private ObservableCollection<ComboBoxData> _raceComboBoxData = new ObservableCollection<ComboBoxData>();
        private ObservableCollection<ComboBoxData> _partComboBoxData = new ObservableCollection<ComboBoxData>();
        private ObservableCollection<ComboBoxData> _numberComboBoxData = new ObservableCollection<ComboBoxData>();
        private ObservableCollection<ComboBoxData> _meshComboBoxData = new ObservableCollection<ComboBoxData>();

        private int _raceIndex, _partIndex, _numberIndex, _meshIndex, _partCount, _numberCount, _meshCount, _raceCount, _reflectionValue, _checkedLight;
        private float _lightingXValue, _lightingYValue, _lightingZValue;
        private bool _raceEnabled, _partEnabled, _numberEnabled, _meshEnabled, _exportEnabled, _importEnabled, _modStatusToggleEnabled, _updateTexEnabled, _flyoutOpen, _fmvEnabled;
        private string _partWatermark = XivStrings.Part, _numberWatermark = XivStrings.Number, _raceWatermark = XivStrings.Race,
            _meshWatermark = XivStrings.Mesh, _pathString;

        private string _lightXLabel = "X  |  0", _lightYLabel = "Y  |  0", _lightZLabel = "Z  |  0", _reflectionLabel = $"{UIStrings.Reflection}  |  1", _modToggleText = UIStrings.Enable_Disable, _modelStatusLabel;
        private ComboBoxData _selectedRace, _selectedPart, _selectedNumber, _selectedMesh;
        private Visibility _numberVisibility, _partVisibility, _lightToggleVisibility = Visibility.Collapsed, _fmvVisibility;
        private Visibility _raceVisibility = Visibility.Visible;
        private bool _light1Check = true, _light2Check, _light3Check, _lightRenderToggle, _transparencyToggle, _cullModeToggle, _keepCameraChecked;

        private IItemModel _item;
        private Mdl _mdl;

        private TTModel _model;
        private Viewport3DViewModel _viewPortVM;
        private Timer _modelStatusTimer;

        public event EventHandler LoadingComplete;
        public event EventHandler AddToFullModelEvent;

        private Dictionary<int, ModelTextureData> _materialDictionary;

        private DirectoryInfo _gameDirectory;

        private bool _updateNeeded = false;

        public ModelViewModel(ModelView modelView)
        {
            ViewPortVM = new Viewport3DViewModel(this);
            _view = modelView;

            _view.ExportModelButton.Click += ExportModelButton_Click;
            _view.ExportContextButton.Click += ExportContextButton_Click;


            var mw = MainWindow.GetMainWindow();
            mw.SelectedPrimaryItemValueChanged += Mw_SelectedPrimaryItemValueChanged;
            //_modelView.ExportContextMenu.Items.Add();
        }

        private void ExportContextButton_Click(object sender, RoutedEventArgs e)
        {
            _view.ExportContextMenu.PlacementTarget = _view.ExportModelButton;
            _view.ExportContextMenu.IsOpen = true;
        }

        private void ExportModelButton_Click(object sender, RoutedEventArgs e)
        {
            ExportModel("fbx");
        }


        private void Mw_SelectedPrimaryItemValueChanged(object sender, int e)
        {
            // Called whenever the main window has the selected primary value changed (this is either Race or Number).
            if (e < 0) return;
            if (_item == null) return;

            // If we were the ones to trigger it, there's no need to adjust this.
            if (_tab.IsSelected) return;

            if(_item.PrimaryCategory == XivStrings.Character)
            {
                // Character type items this means the ""Number""
                if (SelectedNumber == null) return;

                var selectedNum = 0;
                var ok = Int32.TryParse(SelectedNumber.Name, out selectedNum);
                if (!ok) return;


                // These menus are so jank...
                var idx = -1;
                for (int i = 0; i < Numbers.Count; i++)
                {
                    var num = 0;
                    ok = Int32.TryParse(Numbers[i].Name, out num);
                    if (!ok) continue;

                    if (num == e)
                    {
                        idx = i;
                        break;
                    }
                }

                // Don't have the number in question.
                if (idx == -1) return;


                SelectedNumberIndex = idx;
                _updateNeeded = true;
            }
            else
            {
                // Otherwise it's race.
                var race = XivRaces.GetXivRace(e);
                if (SelectedRace == null) return;
                if (SelectedRace.XivRace == race) return;

                // These menus are so jank...
                var idx = -1;
                for(int i = 0; i < Races.Count; i++)
                {
                    if(Races[i].XivRace == race)
                    {
                        idx = i;
                        break;
                    }
                }

                // Don't have the race in question.
                if (idx == -1) return;

                SelectedRaceIndex = idx;
                _updateNeeded = true;
            }

        }

        /// <summary>
        /// Called whenever the user selects the Model tab.
        /// </summary>
        /// <returns></returns>
        public async Task OnTabShown()
        {
            if (_updateNeeded)
            {
                _updateNeeded = false;
                await UpdateViewPort();
            }
        }

        private TabItem _tab
        {
            get
            {
                var mw = MainWindow.GetMainWindow();
                return mw.ModelTabItem;
            }
        }


        /// <summary>
        /// Updates the model to display
        /// </summary>
        /// <remarks>
        /// This is also where the races are obtained
        /// </remarks>
        /// <param name="itemModel">The model to update to</param>
        public async Task UpdateModel(IItemModel itemModel)
        {

            ClearAll();

            // Might as well just make sure we have these updated.
            CustomizeViewModel.UpdateFrameworkColors();

            _item = (IItemModel)itemModel.Clone();

            _gameDirectory = new DirectoryInfo(Settings.Default.FFXIV_Directory);
            _mdl = new Mdl(_gameDirectory, _item.DataFile);


            // Add the list of exporters to the menu.
            var exporters = _mdl.GetAvailableExporters();
            _view.ExportContextMenu.Items.Clear();
            foreach(var format in exporters)
            {
                var button = new System.Windows.Controls.Button();
                button.Content = format;
                button.Width = 100;
                button.Margin = new System.Windows.Thickness(0);
                button.Click += (object sender, RoutedEventArgs e) =>
                {
                    ExportModel(format);
                };

                _view.ExportContextMenu.Items.Add(button);

            }

            var _eqp = new Eqp(_gameDirectory);
            var races = await _eqp.GetAvailableRacialModels(_item, false, true);

            var root = _item.GetRoot();
            if(root == null)
            {
                OnLoadingComplete();
                return;
            }

            if (races.Count == 0)
            {
                // This is a type that doesn't use EQDP files for identifying racial models.
                // This means either is has no racial models (monster, demi, furniture, etc.)
                // Or is the human/character tab and has special treatment.

                // This type has a pre-defined race.
                if (root.Info.PrimaryType == XivItemType.human)
                {
                    var race = XivRaces.GetXivRace(root.Info.PrimaryId);
                    races.Add(race);

                    // In these cases, hide the race bar entirely so we can show the number bar instead.
                    RaceVisibility = Visibility.Collapsed;
                }
                else
                {
                    RaceVisibility = Visibility.Visible;
                    races.Add(XivRace.All_Races);
                }

            }
            else
            {

                RaceVisibility = Visibility.Visible;
            }

            foreach (var race in races)
            {
                Races.Add(new ComboBoxData { Name = race.GetDisplayName(), XivRace = race });
            }
            _raceCount = Races.Count;

            RaceComboboxEnabled = _raceCount > 1;

            var defaultRace = (from race in Races
                               where race.XivRace.GetDisplayName().Equals(Settings.Default.Default_Race_Selection)
                               select race).ToList();

            var lastRaceIdx = -1;
            for (int i = 0; i < Races.Count; i++)
            {
                if(Races[i].XivRace == _lastRace)
                {
                    lastRaceIdx = i;
                    break;
                }
            }

            var tryReselect = Settings.Default.Remember_Race_Selection;
            var raceIndex = 0;
            if(lastRaceIdx >= 0 && tryReselect)
            {
                // Re-select last.
                raceIndex = lastRaceIdx;
            }
            else if (defaultRace.Count > 0 && !tryReselect)
            {
                // Use default.
                raceIndex = Races.IndexOf(defaultRace[0]);
            }

            else if(Races.Count == 0)
            {
                // If there are no races, we're done.
                if (LoadingComplete != null)
                {
                    LoadingComplete.Invoke(this, null);
                }
            }

            SelectedRaceIndex = raceIndex;

            CullModeToggle = Settings.Default.Cull_Mode == "None";

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
                    _lastRace = value.XivRace;

                    if (_item.PrimaryCategory != XivStrings.Character)
                    {
                        var race = value.XivRace;
                        var mw = MainWindow.GetMainWindow();
                        mw.SelectedPrimaryItemValue = race.GetRaceCodeInt();
                    }


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
        private async void GetNumbers()
        {
            if (_item.PrimaryCategory.Equals(XivStrings.Gear) || _item.PrimaryCategory.Equals(XivStrings.Companions) || _item.PrimaryCategory.Equals(XivStrings.Housing))
            {
                GetParts();
            }
            else if (_item.PrimaryCategory.Equals(XivStrings.Character))
            {
                NumberVisibility = Visibility.Visible;
                PartVisibility = Visibility.Visible;
                var _chara = new Character(XivCache.GameInfo.GameDirectory, XivCache.GameInfo.GameLanguage);
                var charaItem = (XivCharacter)_item;
                int[] numbers = new int[0];
                numbers = await _chara.GetNumbersForCharacterItem(charaItem, false);

                var toSelect = 0;
                var idx = 0;
                var tryReselect = Settings.Default.Remember_Race_Selection;

                if (charaItem.ModelInfo.SecondaryID > 0)
                {
                    _lastNumber = charaItem.ModelInfo.SecondaryID;
                    Numbers.Add(new ComboBoxData { Name = charaItem.ModelInfo.SecondaryID.ToString() });
                    _numberCount = 1;
                }
                else
                {
                    foreach (var number in numbers)
                    {

                        
                        if (tryReselect && number == _lastNumber)
                        {
                            toSelect = idx;
                        }

                        Numbers.Add(new ComboBoxData { Name = number.ToString() });
                        idx++;
                    }
                    _numberCount = numbers.Length;
                }
                NumberComboboxEnabled = _numberCount > 1;

                SelectedNumberIndex = toSelect;

                if(numbers.Length == 0)
                {
                    Parts.Clear();
                    Meshes.Clear();
                    _model = new TTModel();
                    PartComboboxEnabled = false;
                    MeshComboboxEnabled = false;
                    NumberComboboxEnabled = false;
                    UpdateViewPort();
                    OnLoadingComplete();
                    return;
                }
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

        private XivRace _lastRace = XivRace.All_Races;
        private int _lastNumber = -1;
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
                    var num = 0;
                    var ok = Int32.TryParse(value.Name, out num);
                    if(ok)
                    {
                        _lastNumber = num;
                    }

                    var valNum = -1;
                    ok = Int32.TryParse(value.Name, out num);
                    if (ok)
                    {
                        valNum = num;
                    }

                    if (_item.PrimaryCategory == XivStrings.Character && valNum >= 0)
                    {
                        var mw = MainWindow.GetMainWindow();
                        mw.SelectedPrimaryItemValue = valNum;
                    }

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
        public Visibility RaceVisibility
        {
            get => _raceVisibility;
            set
            {
                _raceVisibility = value;
                NotifyPropertyChanged(nameof(RaceVisibility));
            }
        }

        #endregion

        #region Part

        /// <summary>
        /// Gets the parts for applicable models
        /// </summary>
        private async void GetParts()
        {
            if (_item.PrimaryCategory.Equals(XivStrings.Gear))
            {
                var xivGear = _item as XivGear;

                if (xivGear.SecondaryCategory.Equals(XivStrings.Rings))
                {
                    Parts.Add(new ComboBoxData { Name = XivStrings.Right });
                    Parts.Add(new ComboBoxData { Name = XivStrings.Left });
                }
                else
                {
                    Parts.Add(new ComboBoxData { Name = XivStrings.Primary });
                }

                PartVisibility = Visibility.Visible;
            }
            else if (_item.PrimaryCategory.Equals(XivStrings.Character))
            {
                var character = new Character(_gameDirectory, GetLanguage());

                var parts = await character.GetTypeForModels(_item as XivCharacter, SelectedRace.XivRace,
                    int.Parse(SelectedNumber.Name));

                foreach (var part in parts)
                {
                    Parts.Add(new ComboBoxData { Name = part });
                }
            }
            else if (_item.PrimaryCategory.Equals(XivStrings.Companions))
            {
                if (_item.GetPrimaryItemType() == XivItemType.demihuman)
                {
                    var companions = new Companions(_gameDirectory, GetLanguage());
                    var parts = await companions.GetDemiHumanMountModelEquipPartList(_item);

                    foreach (var part in parts)
                    {
                        Parts.Add(new ComboBoxData { Name = part });
                    }

                    PartVisibility = Visibility.Visible;
                }
                else
                {
                    GetMeshes();
                }
            }
            else if (_item.PrimaryCategory.Equals(XivStrings.Housing))
            {
                var housing = new Housing(_gameDirectory, GetLanguage());
                var partsDictionary = await housing.GetFurnitureModelParts(_item);

                foreach (var part in partsDictionary)
                {
                    Parts.Add(new ComboBoxData { Name = part.Key, MdlPath = part.Value });
                }

                PartVisibility = Visibility.Visible;
            }

            _partCount = Parts.Count;
            PartComboboxEnabled = _partCount > 1;
            SelectedPartIndex = 0;

            if(Parts.Count == 0)
            {
                Meshes.Clear();
                _model = new TTModel();
                MeshComboboxEnabled = false;

                if (_tab.IsSelected)
                {
                    await UpdateViewPort();
                }
                else
                {
                    _updateNeeded = true;
                    OnLoadingComplete();
                }
            }
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
        private async void GetMeshes()
        {
            Meshes.Clear();

            if(SelectedRace == null)
            {
                // Edge case safety checks.
                OnLoadingComplete();
                return;
            }

            try
            {
                if (_item.PrimaryCategory.Equals(XivStrings.Gear))
                {
                    string submeshId = GetSubmeshId();
                    _model = await _mdl.GetModel(_item, SelectedRace.XivRace, submeshId);
                }
                else if (_item.PrimaryCategory.Equals(XivStrings.Character))
                {
                    if (SelectedNumber == null || SelectedPart == null)
                    {
                        // Edge case safety checks.
                        OnLoadingComplete();
                        return;
                    }

                    _item.ModelInfo = new XivModelInfo { SecondaryID = int.Parse(SelectedNumber.Name) };

                    ((XivCharacter)_item).TertiaryCategory = SelectedPart.Name;

                    _model = await _mdl.GetModel(_item, SelectedRace.XivRace);
                }
                else if (_item.PrimaryCategory.Equals(XivStrings.Companions))
                {
                    if (SelectedPart == null)
                    {
                        // Edge case safety checks.
                        OnLoadingComplete();
                        return;
                    }

                    if (_item.GetPrimaryItemType() == XivItemType.demihuman)
                    {
                        ((XivMount)_item).TertiaryCategory = SelectedPart.Name;
                    }

                    _model = await _mdl.GetModel(_item, SelectedRace.XivRace);
                }
                else if (_item.PrimaryCategory.Equals(XivStrings.Housing))
                {
                    string submeshId = GetSubmeshId();
                    _model = await _mdl.GetModel(_item, SelectedRace.XivRace, submeshId);
                }
            }
            catch (Exception ex)
            {
                FlexibleMessageBox.Show(
                    string.Format(UIMessages.MDLReadErrorMessage, ex.Message), UIMessages.MDLReadErrorTitle,
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);

                OnLoadingComplete();
                return;
            }

            if(_model == null)
            {
                _model = new TTModel();
                OnLoadingComplete();
                return;
            }

            _meshCount = _model.MeshGroups.Count;

            PathString = _model.Source;

            Meshes.Add(new ComboBoxData { Name = XivStrings.All });
            for (var i = 0; i < _meshCount; i++)
            {
                Meshes.Add(new ComboBoxData { Name = i.ToString() });
            }

            MeshComboboxEnabled = _meshCount > 1;
            SelectedMeshIndex = 0;

            var modList = new Modding(_gameDirectory);

            var mod = await modList.TryGetModEntry(PathString);
            //var modStatus = await modList.IsModEnabled(PathString, false);

            if(mod == null)
            {

                ModStatusToggleEnabled = false;
                ModToggleText = UIStrings.Enable_Disable;
            } else if(!mod.enabled)
            {
                ModStatusToggleEnabled = true;
                ModToggleText = UIStrings.Enable;

            } else if(mod.enabled)
            {
                ModToggleText = UIStrings.Disable;
                if(mod.IsCustomFile())
                {
                    // Don't let users disable custom racial models from this menu since it'll blow up the sun.
                    ModStatusToggleEnabled = false;
                } else
                {
                    ModStatusToggleEnabled = true;
                }
            }
            else
            {
                ModStatusToggleEnabled = false;
                ModToggleText = UIStrings.Enable_Disable;

            }

            SetComboBoxWatermarks();

            if (_tab.IsSelected)
            {
                await UpdateViewPort();
            } else
            {
                _updateNeeded = true;
                OnLoadingComplete();
            }
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

            if (_item.PrimaryCategory.Equals(XivStrings.Character))
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
                ReflectionLabel = $"{UIStrings.Reflection}  |  {value}";
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

        public bool KeepCameraChecked
        {
            get => _keepCameraChecked;
            set
            {
                _keepCameraChecked = value;
                NotifyPropertyChanged(nameof(KeepCameraChecked));
            }
        }

        /// <summary>
        /// Flag for visibility of full model view button
        /// </summary>
        public Visibility FMVVisibility
        {
            get => _fmvVisibility;
            set
            {
                _fmvVisibility = value;
                NotifyPropertyChanged(nameof(FMVVisibility));
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

        public bool FMVEnabled
        {
            get => _fmvEnabled;
            set
            {
                _fmvEnabled = value;
                NotifyPropertyChanged(nameof(FMVEnabled));
            }
        }

        public ICommand ViewOptionsCommand => new RelayCommand(ViewerOptions);
        public ICommand ModStatusToggleButton => new RelayCommand(ModStatusToggle);
        public ICommand UpdateTexButton => new RelayCommand(UpdateTex);

        public ICommand ImportCommand => new RelayCommand(Import);
        public ICommand OpenFolder => new RelayCommand(OpenSavedFolder);
        public ICommand ModelInspector => new RelayCommand(OpenModelInspector);
        public ICommand AddToFullModelViewerCommand => new RelayCommand(AddToFullModelView);

        /// <summary>
        /// Triggers event when add to FMV button is clicked
        /// </summary>
        private void AddToFullModelView(object obj)
        {
            OnFullModelClick();
        }

        private string GetItem3DFolder()
        {
            var savePath = new DirectoryInfo(Settings.Default.Save_Directory);
            if (_item == null)
            {
                return savePath.FullName;
            }
            return $"{IOUtil.MakeItemSavePath(_item, savePath, SelectedRace.XivRace)}\\3D\\";
        }
        /// <summary>
        /// Opens the folder in which the exported data is saved
        /// </summary>
        /// <remarks>
        /// Creates the save directory if it does not exist
        /// </remarks>
        private void OpenSavedFolder(object obj)
        {

            var path = GetItem3DFolder();

            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }

            Process.Start(path);
        }

        private async Task<XivMdl> GetRawMdl()
        {
            return (await _model.GetRawMdl(_mdl));
        }

        private void OpenModelInspector(object obj)
        {
            if (_model != null)
            {
                var task = Task.Run(GetRawMdl);
                task.Wait();
                var mdl = task.Result;
                var modelInspector = new ModelInspector(mdl);
                modelInspector.Owner = Window.GetWindow(_view);
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
        private async void ModStatusToggle(object obj)
        {
            var modlist = new Modding(_gameDirectory);

            try
            {
                if (ModToggleText.Equals(UIStrings.Enable))
                {
                    await modlist.ToggleModStatus(PathString, true);
                }
                else if (ModToggleText.Equals(UIStrings.Disable))
                {
                    await modlist.ToggleModStatus(PathString, false);
                }
            }
            catch (Exception ex)
            {
                FlexibleMessageBox.Show(
                    string.Format(UIMessages.ModToggleErrorMessage, ex.Message), UIMessages.ModToggleErrorTitle,
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            GetMeshes();
        }

        #endregion

        #region Export

        private string GetSubmeshId()
        {
            string submeshId = null;
            if (_item.PrimaryCategory.Equals(XivStrings.Housing))
            {
                if (PartVisibility == Visibility.Visible)
                {
                    submeshId = _selectedPart.Name;
                }

            }
            else if (_item.SecondaryCategory.Equals(XivStrings.Rings))
            {
                if (PartVisibility == Visibility.Visible)
                {
                    submeshId = _selectedPart.Name == XivStrings.Left ? "ril" : "rir";
                }
            }

            return submeshId;
        }

        /// <summary>
        /// Exports the DAE file and Materials for the current model
        /// </summary>
        private void ExportModel(string format)
        {
            DisableButtons();
            Task.Run(async () =>
            {
               try
               {
                    var path = GetItem3DFolder() + Path.GetFileNameWithoutExtension(_model.Source) + "." + format;
                    string submeshId = GetSubmeshId();
                    await _mdl.ExportMdlToFile(_item, SelectedRace.XivRace, path, submeshId);

                }
               catch (Exception e)
                {
                    while(e.InnerException != null)
                    {
                        e = e.InnerException;
                    }
                    // We're not guaranteed to be on the main thread here,
                    // so ensure we call the message box on that thread so it doesn't
                    // get eaten by access errors.
                    await _view.Dispatcher.BeginInvoke((ThreadStart)delegate ()
                    {
                        FlexibleMessageBox.Show(
                            string.Format(UIMessages.ExportErrorMessage + "\n\n" + e.Message), UIMessages.ExportErrorTitle,
                            MessageBoxButtons.OK, MessageBoxIcon.Error);
                    });
                }

                await _view.Dispatcher.BeginInvoke((ThreadStart)delegate ()
                {
                    EnableButtons();
                });
            });

        }

        private void DisableButtons()
        {
            ExportEnabled = false;
            ImportEnabled = false;
        }

        private void EnableButtons()
        {
            ExportEnabled = true;
            ImportEnabled = true;
        }



        #endregion

        #region Import

        /// <summary>
        /// Opens the import dialog.
        /// </summary>
        /// <remarks>
        /// This will import the DAE file with the same name and location as the exported model
        /// </remarks>
        public async void Import(object obj)
        {
            var gameDirectory = new DirectoryInfo(Settings.Default.FFXIV_Directory);
            var index = new Index(gameDirectory);

            if (index.IsIndexLocked(XivDataFile._0A_Exd))
            {
                FlexibleMessageBox.Show(UIMessages.IndexLockedErrorMessage,
                    UIMessages.IndexLockedErrorTitle, MessageBoxButtons.OK, MessageBoxIcon.Error);

                return;
            }

            var type = _item.GetPrimaryItemType();
            string submeshId = GetSubmeshId();
            bool success = await ImportModelView.ImportModel(_item, SelectedRace.XivRace, submeshId, null, () =>
            {
                _view.Dispatcher.BeginInvoke((ThreadStart)delegate ()
                {
                    // Go ahead and reload the model as soon as the import process is done, even if they haven't closed the window.
                    ModStatusToggleEnabled = true;

                    GetMeshes();
                });
            });
            

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
        private async void UpdateTex(object obj)
        {
            await UpdateViewPort();
        }

        /// <summary>
        /// Updates the viewport to the selected model
        /// </summary>
        public async Task UpdateViewPort()
        {
            try
            {
                ModelStatusLabel = UIStrings.ModelStatus_Loading;
                // Might as well just make sure we have these updated.
                CustomizeViewModel.UpdateFrameworkColors();

                ViewPortVM.ClearModels();
                TransparencyToggle = false;
                FMVEnabled = false;

                if (_model == null) return;

                _materialDictionary = await GetMaterials();

                ViewPortVM.UpdateModel(_model, _materialDictionary);

                ReflectionValue = ViewPortVM.SpecularShine;

                _view.viewport3DX.ZoomExtents();

                ExportEnabled = true;

                ImportEnabled = true;
                UpdateTexEnabled = true;
                FMVEnabled = true;

                if (!KeepCameraChecked)
                {
                    _view.viewport3DX.ZoomExtents();
                }

                ShowModelStatus(UIStrings.ModelStatus_UpdateSuccess);
            }
            catch (Exception ex)
            {
                FlexibleMessageBox.Show(
                     string.Format(UIMessages.ViewportErrorMessage, ex.Message), UIMessages.ViewportErrorTitle,
                    MessageBoxButtons.OK, MessageBoxIcon.Error);

                var modlist = new Modding(_gameDirectory);

                try
                {
                    await modlist.ToggleModStatus(PathString, false);
                    GetMeshes();
                }
                catch (Exception e)
                {
                    FlexibleMessageBox.Show(
                        string.Format(UIMessages.ModToggleErrorMessage, e.Message), UIMessages.ModToggleErrorTitle,
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }                

                return;
            }
            finally
            {
                OnLoadingComplete();
            }
        }
        #endregion

        #region Materials

        /// <summary>
        /// Gets the materials for the model
        /// </summary>
        /// <returns>A dictionary containing the mesh number(key) and the associated texture data (value)</returns>
        private async Task<Dictionary<int, ModelTextureData>> GetMaterials()
        {
            var textureDataDictionary = new Dictionary<int, ModelTextureData>();
            if (_model == null) return textureDataDictionary;
            var mtrlDictionary = new Dictionary<int, XivMtrl>();
            var mtrl = new Mtrl(_gameDirectory, _item.DataFile, GetLanguage());
            var mtrlFilePaths = _model.Materials;
            var hasColorChangeShader = false;
            Color? customColor = null;
            WinColor winColor;

            if (SelectedRace == null) return textureDataDictionary;

            var race = SelectedRace.XivRace;

            var materialNum = 0;
            foreach (var mtrlFilePath in mtrlFilePaths)
            {
                var mtrlItem = (IItemModel) _item.Clone();

                var modelID = mtrlItem.ModelInfo.PrimaryID;
                var bodyID = mtrlItem.ModelInfo.SecondaryID;
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

                        // XIV automatically forces skin materials to instead reference the appropiate one for the character wearing it.
                        race = XivRaceTree.GetSkinRace(_selectedRace.XivRace);


                        var gender = 0;
                        if (int.Parse(XivRaces.GetRaceCode(race).Substring(0, 2)) % 2 == 0)
                        {
                            gender = 1;
                        }

                        // Get the actual skin the user's preferred race uses.
                        var settingsRace = XivRaceTree.GetSkinRace(GetSettingsRace(gender).Race);
                        var settingsBody = settingsRace == GetSettingsRace(gender).Race ? GetSettingsRace(gender).BodyID : "0001";

                        // If the user's race is a child of the item's race, we can show the user skin instead.
                        var useSettings = XivRaceTree.IsChildOf(settingsRace, race);
                        if(useSettings)
                        {
                            filePath = mtrlFilePath.Replace(raceString, settingsRace.GetRaceCode()).Replace(body, settingsBody);
                            race = settingsRace;
                            body = settingsBody;
                        } else
                        {
                            // Just use item race.
                            filePath = mtrlFilePath.Replace(raceString, race.GetRaceCode()).Replace(body, "0001");
                        }




                        mtrlItem = new XivGenericItemModel
                        {
                            PrimaryCategory = XivStrings.Character,
                            SecondaryCategory = XivStrings.Body,
                            Name = XivStrings.Body,
                            ModelInfo = new XivModelInfo
                            {
                                SecondaryID = int.Parse(body)
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
                            PrimaryCategory = XivStrings.Character,
                            SecondaryCategory = XivStrings.Face,
                            Name = XivStrings.Face,
                            ModelInfo = new XivModelInfo
                            {
                                SecondaryID = bodyID
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
                            PrimaryCategory = XivStrings.Character,
                            SecondaryCategory = XivStrings.Hair,
                            Name = XivStrings.Hair,
                            ModelInfo = new XivModelInfo
                            {
                                SecondaryID = bodyID
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
                            PrimaryCategory = XivStrings.Character,
                            SecondaryCategory = XivStrings.Tail,
                            Name = XivStrings.Tail,
                            ModelInfo = new XivModelInfo
                            {
                                SecondaryID = bodyID
                            }
                        };

                        winColor = (WinColor)ColorConverter.ConvertFromString(Settings.Default.Hair_Color);
                        customColor = new Color(winColor.R, winColor.G, winColor.B, winColor.A);

                        break;
                    // Ears
                    case "cz":
                        var tPath = mtrlFilePath.Substring(4);
                        bodyID = int.Parse(tPath.Substring(tPath.IndexOf("z") + 1, 4));
                        raceString = mtrlFilePath.Substring(mtrlFilePath.IndexOf("c") + 1, 4);
                        race = XivRaces.GetXivRace(raceString);

                        mtrlItem = new XivGenericItemModel
                        {
                            PrimaryCategory = XivStrings.Character,
                            SecondaryCategory = XivStrings.Ear,
                            Name = XivStrings.Ear,
                            ModelInfo = new XivModelInfo
                            {
                                SecondaryID = bodyID
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

                        mtrlItem.ModelInfo.PrimaryID = modelID;
                        break;
                    // Accessory
                    case "ca":
                        modelID = int.Parse(mtrlFilePath.Substring(mtrlFilePath.IndexOf("a") + 1, 4));
                        raceString = mtrlFilePath.Substring(mtrlFilePath.IndexOf("c") + 1, 4);
                        race = XivRaces.GetXivRace(raceString);

                        mtrlItem.ModelInfo.PrimaryID = modelID;
                        break;
                    // Weapon
                    case "wb":
                        modelID = int.Parse(mtrlFilePath.Substring(mtrlFilePath.IndexOf("w") + 1, 4));
                        bodyID = int.Parse(mtrlFilePath.Substring(mtrlFilePath.IndexOf("b") + 1, 4));
                        mtrlItem.ModelInfo.PrimaryID = modelID;
                        mtrlItem.ModelInfo.SecondaryID = bodyID;
                        break;
                    // Monster
                    case "mb":
                        modelID = int.Parse(mtrlFilePath.Substring(mtrlFilePath.IndexOf("_m") + 2, 4));
                        bodyID = int.Parse(mtrlFilePath.Substring(mtrlFilePath.IndexOf("b") + 1, 4));
                        mtrlItem.ModelInfo.PrimaryID = modelID;
                        mtrlItem.ModelInfo.SecondaryID = bodyID;
                        break;
                    // DemiHuman
                    case "de":
                        modelID = int.Parse(mtrlFilePath.Substring(mtrlFilePath.IndexOf("d") + 1, 4));
                        bodyID = int.Parse(mtrlFilePath.Substring(mtrlFilePath.IndexOf("e") + 1, 4));
                        mtrlItem.ModelInfo.PrimaryID = modelID;
                        mtrlItem.ModelInfo.SecondaryID = bodyID;
                        break;
                    default:
                        break;
                }

                var dxVersion = int.Parse(Settings.Default.DX_Version);
                var mtrlData = await mtrl.GetMtrlData(mtrlItem, filePath, dxVersion);

                if(mtrlData == null)
                {
                    continue;
                }

                if (mtrlData.Shader.Contains("colorchange"))
                {
                    hasColorChangeShader = true;
                }

                mtrlDictionary.Add(materialNum, mtrlData);

                materialNum++;
            }

            foreach (var xivMtrl in mtrlDictionary)
            {

                if (hasColorChangeShader)
                {
                    var modelMaps = await ModelTexture.GetModelMaps(_gameDirectory, xivMtrl.Value);
                    textureDataDictionary.Add(xivMtrl.Key, modelMaps);
                }
                else
                {
                    var modelMaps = await ModelTexture.GetModelMaps(_gameDirectory, xivMtrl.Value);
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


        /// <summary>
        /// Command for the Details button
        /// </summary>
        public ICommand OpenFileDetails => new RelayCommand(ShowFileDetails);

        /// <summary>
        /// Opens the dependency dialog.
        /// </summary>
        private void ShowFileDetails(object obj)
        {
            if (_model != null)
            {
                var path = _model.Source;
                var view = new DependencyInfoView(path);
                view.ShowDialog();
            }
        }

        /// <summary>
        /// Event fired when add to FMV button is clicked
        /// </summary>
        protected virtual void OnFullModelClick()
        {
            var fmea = new fullModelEventArgs { TTModelData = _model, TextureData = _materialDictionary, Item = _item, XivRace = SelectedRace.XivRace};

            AddToFullModelEvent?.Invoke(this, fmea);
        }

        /// <summary>
        /// Class containing properties for full model event arguments
        /// </summary>
        public class fullModelEventArgs : EventArgs
        {
            public TTModel TTModelData { get; set; }
            public Dictionary<int, ModelTextureData> TextureData { get; set; }

            public IItemModel Item { get; set; }

            public XivRace XivRace { get; set; }
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

        protected virtual void OnLoadingComplete()
        {
            LoadingComplete?.Invoke(this, EventArgs.Empty);
        }
    }
}