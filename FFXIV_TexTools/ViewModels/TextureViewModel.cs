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

using ControlzEx.Standard;
using FFXIV_TexTools.Helpers;
using FFXIV_TexTools.Properties;
using FFXIV_TexTools.Resources;
using FFXIV_TexTools.Textures;
using FFXIV_TexTools.Views;
using SharpDX;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Bmp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using xivModdingFramework.Exd.FileTypes;
using xivModdingFramework.General.Enums;
using xivModdingFramework.Helpers;
using xivModdingFramework.Items.Categories;
using xivModdingFramework.Items.DataContainers;
using xivModdingFramework.Items.Enums;
using xivModdingFramework.Items.Interfaces;
using xivModdingFramework.Materials.DataContainers;
using xivModdingFramework.Materials.FileTypes;
using xivModdingFramework.Mods;
using xivModdingFramework.Mods.Enums;
using xivModdingFramework.SqPack.FileTypes;
using xivModdingFramework.Textures.DataContainers;
using xivModdingFramework.Textures.Enums;
using xivModdingFramework.Textures.FileTypes;
using BitmapSource = System.Windows.Media.Imaging.BitmapSource;

namespace FFXIV_TexTools.ViewModels
{
    public class TextureViewModel : INotifyPropertyChanged
    {
        private ObservableCollection<ComboBoxData> _raceComboBoxData = new ObservableCollection<ComboBoxData>();
        private ObservableCollection<ComboBoxData> _partComboBoxData = new ObservableCollection<ComboBoxData>();
        private ObservableCollection<ComboBoxData> _typeComboBoxData = new ObservableCollection<ComboBoxData>();
        private ObservableCollection<ComboBoxData> _typePartComboBoxData = new ObservableCollection<ComboBoxData>();
        private ObservableCollection<ComboBoxData> _mapComboBoxData = new ObservableCollection<ComboBoxData>();

        private ComboBoxData _selectedRace, _selectedPart, _selectedType, _selectedTypePart, _selectedMap;
        private Gear _gear;
        private Character _character;
        private Companions _companions;
        private Mtrl _mtrl;
        private Tex _tex;
        private XivMtrl _xivMtrl;
        private Modding _modList;
        private XivUi _uiItem;
        private BitmapSource _imageDisplay;
        private ColorChannels _imageEffect;
        private readonly TextureView _textureView;
        private MapData _mapData;

        private Dictionary<XivRace, int[]> _charaRaceAndNumberDictionary;

        private Visibility _typePartVisibility, _typeVisibility, _partVisibility;
        private IItemModel _item;

        private string _pathString, _textureFormat, _textureDimensions, _category, _mipMapInfo;
        private string _partWatermark = XivStrings.Part, _typeWatermark = XivStrings.Type, _raceWatermark = XivStrings.Race,
            _typePartWatermark = XivStrings.TypePart, _textureMapWatermark = XivStrings.Texture_Map;
        private string _modToggleText = UIStrings.Enable_Disable;

        private bool _raceEnabled, _partEnabled, _typeEnabled, _typePartEnabled, _mapEnabled, _channelsEnabled;
        private bool _exportEnabled, _importEnabled, _modStatusEnabled, _moreOptionsEnabled, _translucencyEnabled, _translucencyCheck, _addNewTexturePartEnabled=true;
        private bool _redChecked = true, _greenChecked = true, _blueChecked = true, _alphaChecked;

        private int _raceIndex, _partIndex, _typeIndex, _typePartIndex, _mapIndex, _partCount, _typeCount, _typePartCount, _mapCount, _raceCount;

        public event EventHandler LoadingComplete;

        public TextureViewModel(TextureView textureView)
        {
            _textureView = textureView;
            ChannelsEnabled = false;
        }

        /// <summary>
        /// Updates the texture
        /// </summary>
        /// <param name="item">The item to update the texture for</param>
        public async Task UpdateTexture(IItem item)
        {
            ClearAll();

            var gameDirectory = new DirectoryInfo(Properties.Settings.Default.FFXIV_Directory);
            _mtrl = new Mtrl(gameDirectory, item.DataFile, GetLanguage());
            _tex = new Tex(gameDirectory);
            _modList = new Modding(gameDirectory);

            if (item.Category.Equals(XivStrings.Gear))
            {
                _category = XivStrings.Gear;
                _item = item as IItemModel;

                _gear = new Gear(gameDirectory, GetLanguage());

                var gearItem = item as XivGear;

                var raceList = await _gear.GetRacesForTextures(gearItem, item.DataFile);

                foreach (var xivRace in raceList)
                {
                    var raceCBD = new ComboBoxData{Name = xivRace.GetDisplayName(), XivRace = xivRace};

                    Races.Add(raceCBD);
                }
            }
            else if (item.Category.Equals(XivStrings.Companions))
            {
                _companions = new Companions(gameDirectory, GetLanguage());
                _category = XivStrings.Companions;
                _item = item as IItemModel;

                Races.Add(_item.ModelInfo.ModelType == XivItemType.demihuman
                    ? new ComboBoxData {Name = XivRace.DemiHuman.GetDisplayName(), XivRace = XivRace.DemiHuman}
                    : new ComboBoxData {Name = XivRace.Monster.GetDisplayName(), XivRace = XivRace.Monster});
            }
            else if (item.Category.Equals(XivStrings.Character))
            {
                _character = new Character(gameDirectory, GetLanguage());
                _item = item as IItemModel;

                if (_item.ItemCategory.Equals(XivStrings.Face_Paint) ||
                    _item.ItemCategory.Equals(XivStrings.Equipment_Decals))
                {
                    Races.Add(new ComboBoxData{Name = XivRace.All_Races.GetDisplayName(), XivRace = XivRace.All_Races});
                }
                else
                {
                    _charaRaceAndNumberDictionary = await _character.GetRacesAndNumbersForTextures(item as XivCharacter);

                    foreach (var racesAndNumber in _charaRaceAndNumberDictionary)
                    {
                        Races.Add(new ComboBoxData { Name = racesAndNumber.Key.GetDisplayName(), XivRace = racesAndNumber.Key });
                    }
                }
            }
            else if (item.Category.Equals(XivStrings.UI))
            {
                Races.Add(new ComboBoxData { Name = XivRace.All_Races.GetDisplayName(), XivRace = XivRace.All_Races });

                _uiItem = item as XivUi;
            }
            else if (item.Category.Equals(XivStrings.Housing))
            {
                Races.Add(new ComboBoxData { Name = XivRace.All_Races.GetDisplayName(), XivRace = XivRace.All_Races });

                if (item.ItemCategory.Equals(XivStrings.Paintings))
                {
                    _uiItem = new XivUi
                    {
                        Name = item.Name,
                        Category = item.Category,
                        ItemCategory = item.ItemCategory,
                        IconNumber = ((IItemModel)item).ModelInfo.ModelID,
                        DataFile = XivDataFile._06_Ui
                    };
                }
                else
                {
                    _item = item as IItemModel;
                }
            }

            _raceCount = Races.Count;

            RaceComboboxEnabled = _raceCount > 1;

            var defaultRace = (from race in Races
                where race.XivRace.GetDisplayName().Equals(Settings.Default.Default_Race_Selection)
                select race).ToList();

            var raceIndex = 0;
            if (defaultRace.Count > 0)
            {
                raceIndex = Races.IndexOf(defaultRace[0]);
            }

            SelectedRaceIndex = raceIndex;
        }

        /// <summary>
        /// The collection of data for the race combobox
        /// </summary>
        public ObservableCollection<ComboBoxData> Races
        {
            get => _raceComboBoxData;
            set { _raceComboBoxData = value; NotifyPropertyChanged(nameof(Races)); }
        }

        /// <summary>
        /// The selected race in the race combobox
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
                    Types.Clear();
                    TypeParts.Clear();
                    Maps.Clear();

                    GetParts();
                }
            }
        }

        /// <summary>
        /// The selected race index in the race combobox
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
        /// The enabled status for the race combobox
        /// </summary>
        public bool RaceComboboxEnabled
        {
            get => _raceEnabled;
            set { _raceEnabled = value; NotifyPropertyChanged(nameof(RaceComboboxEnabled)); }
        }

        /// <summary>
        /// Gets the parts for the selected item
        /// </summary>
        private async void GetParts()
        {
            if (_item != null)
            {
                // For character, the part list is used as a number list
                List<string> partList;

                if (_item.Category.Equals(XivStrings.Character))
                {
                    if (_item.ItemCategory.Equals(XivStrings.Face_Paint) ||
                        _item.ItemCategory.Equals(XivStrings.Equipment_Decals))
                    {
                        partList = (await _character.GetDecalNums(_item)).Select(part => part.ToString()).ToList();

                        if (_item.ItemCategory.Equals(XivStrings.Equipment_Decals))
                        {
                            partList.Add("_stigma");
                        }
                    }
                    else
                    {
                        partList = _charaRaceAndNumberDictionary[SelectedRace.XivRace].Select(part => part.ToString()).ToList();
                    }

                    foreach (var part in partList)
                    {
                        Parts.Add(new ComboBoxData { Name = part });
                    }

                    _partCount = partList.Count;
                }
                else if ((_item.ItemCategory.Equals(XivStrings.Mounts) || _item.ItemCategory.Equals(XivStrings.Monster)) && _item.ModelInfo.ModelType == XivItemType.demihuman)
                {
                    var equipParts = await _companions.GetDemiHumanMountTextureEquipPartList(_item);

                    foreach (var equipPart in equipParts)
                    {
                        Parts.Add(new ComboBoxData { Name = equipPart.Key, TypeParts = equipPart.Value });
                    }

                    _partCount = equipParts.Count;
                }
                else if (_item.Category.Equals(XivStrings.Gear))
                {
                    var xivGear = _item as XivGear;

                    Parts.Add(new ComboBoxData { Name = XivStrings.Primary });

                    if (xivGear.SecondaryModelInfo != null && xivGear.SecondaryModelInfo.ModelID > 0)
                    {
                        Parts.Add(new ComboBoxData{Name = XivStrings.Secondary});
                    }
                    else
                    {
                        PartVisibility = Visibility.Collapsed;
                    }

                    _partCount = Parts.Count;
                }
                else
                {
                    partList = await _tex.GetTexturePartList(_item, SelectedRace.XivRace, _item.DataFile);

                    foreach (var part in partList)
                    {
                        Parts.Add(new ComboBoxData { Name = part });
                    }

                    _partCount = partList.Count;
                }
            }
            else if (_uiItem != null)
            {
                GetMaps();
            }

            PartComboboxEnabled = _partCount > 1;
            SelectedPartIndex = 0;
        }

        /// <summary>
        /// The collection of data for the parts combobox
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
                    Types.Clear();
                    TypeParts.Clear();
                    Maps.Clear();
                    GetTexType();
                }
            }
        }

        /// <summary>
        /// The selected index for the part combobox
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
        /// The enabled status for the part combobox
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
        /// <summary>
        /// Gets the type for the selected item
        /// </summary>
        private async void GetTexType()
        {
            if (_item.Category.Equals(XivStrings.Character))
            {
                TypeVisibility = Visibility.Visible;
                // Create the model info for character
                _item.ModelInfo = new XivModelInfo();

                if (!SelectedPart.Name.Equals("_stigma"))
                {
                    _item.ModelInfo.Body = int.Parse(SelectedPart.Name);
                }

                // For hair and face we get the type (Hair, Accessory, Face, Iris, Etc)
                if (_item.ItemCategory.Equals(XivStrings.Hair) || _item.ItemCategory.Equals(XivStrings.Face) || _item.ItemCategory.Equals(XivStrings.Ears))
                {
                    TypePartVisibility = Visibility.Visible;
                    var charaTypeParts = await _character.GetTypePartForTextures(_item as XivCharacter, SelectedRace.XivRace,
                        int.Parse(SelectedPart.Name));

                    _typeCount = charaTypeParts.Count;
                    foreach (var charaTypePart in charaTypeParts)
                    {
                        Types.Add(new ComboBoxData{Name = charaTypePart.Key, TypeParts = charaTypePart.Value});
                    }
                }
                else if (_item.ItemCategory.Equals(XivStrings.Body) || _item.ItemCategory.Equals(XivStrings.Tail))
                {
                    TypePartVisibility = Visibility.Visible;
                    var parts = await _character.GetVariantsForTextures(_item as XivCharacter, SelectedRace.XivRace, int.Parse(SelectedPart.Name));

                    _typeCount = parts.Count;
                    foreach (var part in parts)
                    {
                        Types.Add(new ComboBoxData { Name = part.ToString() });
                    }
                }
                else
                {
                    TypeVisibility = Visibility.Collapsed;

                    GetMaps();
                }

                _typeCount = Types.Count;

                TypeComboboxEnabled = _typeCount > 1;

                SelectedTypeIndex = 0;
            }
            else if ((_item.ItemCategory.Equals(XivStrings.Mounts) || _item.ItemCategory.Equals(XivStrings.Monster)) && _item.ModelInfo.ModelType == XivItemType.demihuman)
            {
                TypeVisibility = Visibility.Visible;

                ((XivMount)_item).ItemSubCategory = SelectedPart.Name;

                var parts = SelectedPart.TypeParts;
                _typeCount = parts.Length;

                foreach (var part in parts)
                {
                    Types.Add(new ComboBoxData{Name = part.ToString()});
                }

                _typeCount = Types.Count;

                TypeComboboxEnabled = _typeCount > 1;

                SelectedTypeIndex = 0;
            }
            else if(_item.Category.Equals(XivStrings.Gear))
            {
                TypeVisibility = Visibility.Visible;

                var typesList = await _tex.GetTexturePartList(_item, SelectedRace.XivRace, _item.DataFile, SelectedPart.Name);

                foreach (var type in typesList)
                {
                    Types.Add(new ComboBoxData { Name = type });
                }

                _typeCount = Types.Count;

                TypeComboboxEnabled = _typeCount > 1;

                SelectedTypeIndex = 0;
            }
            else
            {
                GetMaps();
            }
        }

        /// <summary>
        /// The collection of type data for the type combobox
        /// </summary>
        public ObservableCollection<ComboBoxData> Types
        {
            get => _typeComboBoxData;
            set { _typeComboBoxData = value; NotifyPropertyChanged(nameof(Types)); }
        }

        /// <summary>
        /// The selected type in the type combobox
        /// </summary>
        public ComboBoxData SelectedType
        {
            get => _selectedType;
            set
            {
                _selectedType = value;
                NotifyPropertyChanged(nameof(SelectedType));
                if (SelectedTypeIndex > -1)
                {
                    Maps.Clear();
                    if (_item.Category.Equals(XivStrings.Character) && (_item.ItemCategory.Equals(XivStrings.Hair) || _item.ItemCategory.Equals(XivStrings.Face) ||
                        _item.ItemCategory.Equals(XivStrings.Ears) || _item.ItemCategory.Equals(XivStrings.Body) || _item.ItemCategory.Equals(XivStrings.Tail)))
                    {
                        TypeParts.Clear();
                        GetTypeParts();
                    }
                    else
                    {
                        GetMaps();
                    }
                }
            }
        }

        /// <summary>
        /// The selected index in the type combobox
        /// </summary>
        public int SelectedTypeIndex
        {
            get => _typeIndex;
            set
            {
                _typeIndex = value;
                NotifyPropertyChanged(nameof(SelectedTypeIndex));
            }
        }

        /// <summary>
        /// The enabled status of the type combobox
        /// </summary>
        public bool TypeComboboxEnabled
        {
            get => _typeEnabled;
            set { _typeEnabled = value; NotifyPropertyChanged(nameof(TypeComboboxEnabled)); }
        }

        /// <summary>
        /// The visibility of the type combobox
        /// </summary>
        public Visibility TypeVisibility
        {
            get => _typeVisibility;
            set
            {
                _typeVisibility = value;
                NotifyPropertyChanged(nameof(TypeVisibility));
            }
        }

        /// <summary>
        /// Gets the type parts for the selected item
        /// </summary>
        private async void GetTypeParts()
        {
            var typeParts = SelectedType.TypeParts;

            if (_item.Category.Equals(XivStrings.Character))
            {
                if (_item.ItemCategory.Equals(XivStrings.Body) || _item.ItemCategory.Equals(XivStrings.Tail))
                {
                    typeParts = await _character.GetPartForTextures(_item as XivCharacter, SelectedRace.XivRace, int.Parse(SelectedPart.Name), int.Parse(SelectedType.Name));
                }

                ((XivCharacter)_item).ItemSubCategory = SelectedType.Name;
            }

            foreach (var typePart in typeParts)
            {
                TypeParts.Add(new ComboBoxData{Name = typePart.ToString()});
            }

            _typePartCount = typeParts.Length;

            TypePartComboboxEnabled = _typePartCount > 1;

            SelectedTypePartIndex = 0;
        }

        /// <summary>
        /// The collection of type part data for the type part combobox
        /// </summary>
        public ObservableCollection<ComboBoxData> TypeParts
        {
            get => _typePartComboBoxData;
            set { _typePartComboBoxData = value; NotifyPropertyChanged(nameof(TypeParts)); }
        }

        /// <summary>
        /// The selected type part in the type part combobox
        /// </summary>
        public ComboBoxData SelectedTypePart
        {
            get => _selectedTypePart;
            set
            {
                _selectedTypePart = value;
                NotifyPropertyChanged(nameof(SelectedTypePart));
                if (SelectedTypePartIndex > -1)
                {
                    Maps.Clear();
                    GetMaps();
                }
            }
        }

        /// <summary>
        /// The selected index in the type part combobox
        /// </summary>
        public int SelectedTypePartIndex
        {
            get => _typePartIndex;
            set
            {
                _typePartIndex = value;
                NotifyPropertyChanged(nameof(SelectedTypePartIndex));
            }
        }

        /// <summary>
        /// The enabled status for the type part combobox
        /// </summary>
        public bool TypePartComboboxEnabled
        {
            get => _typePartEnabled;
            set { _typePartEnabled = value; NotifyPropertyChanged(nameof(TypePartComboboxEnabled)); }
        }

        /// <summary>
        /// The visibility of the type part combobox
        /// </summary>
        public Visibility TypePartVisibility
        {
            get => _typePartVisibility;
            set
            {
                _typePartVisibility = value;
                NotifyPropertyChanged(nameof(TypePartVisibility));
            }
        }
        /// <summary>
        /// Gets the texture maps for the given item
        /// </summary>
        private async void GetMaps()
        {
            var dxVersion = int.Parse(Properties.Settings.Default.DX_Version);

            List<TexTypePath> ttpList;
            if (_item != null)
            {
                if (_item.Category.Equals(XivStrings.Character))
                {
                    if (_item.ItemCategory.Equals(XivStrings.Face_Paint))
                    {
                        var path = $"{XivStrings.FacePaintFolder}/{string.Format(XivStrings.FacePaintFile, SelectedPart.Name)}";

                        ttpList = new List<TexTypePath> { new TexTypePath { DataFile = _item.DataFile, Path = path, Type = XivTexType.Mask } };
                    }
                    else if (_item.ItemCategory.Equals(XivStrings.Equipment_Decals))
                    {
                        var path = $"{XivStrings.EquipDecalFolder}/{string.Format(XivStrings.EquipDecalFile, SelectedPart.Name)}";

                        if (SelectedPart.Name.Equals("_stigma"))
                        {
                            path = $"{XivStrings.EquipDecalFolder}/_stigma.tex";
                        }

                        ttpList = new List<TexTypePath> { new TexTypePath { DataFile = _item.DataFile, Path = path, Type = XivTexType.Mask } };
                    }
                    else
                    {
                        if (TypePartVisibility == Visibility.Visible)
                        {
                            _xivMtrl = await _mtrl.GetMtrlData(_item, SelectedRace.XivRace, SelectedTypePart.Name[0], dxVersion, SelectedType.Name);
                        }
                        else
                        {
                            _xivMtrl = await _mtrl.GetMtrlData(_item, SelectedRace.XivRace, SelectedType.Name[0], dxVersion);
                        }

                        ttpList = _xivMtrl.TextureTypePathList;

                        // Removes the type if there is no texture maps for it.
                        // This specifically was added because Miqote hair 104 and 109 have an mtrl that points to non existent textures and
                        // there may be others that do the same.
                        if (ttpList.Count == 0)
                        {
                            Types.Remove(SelectedType);
                            SelectedTypeIndex = 0;
                            _typeCount -= 1;
                        }
                    }
                }
                else
                {
                    if ((_item.ItemCategory.Equals(XivStrings.Mounts) || _item.ItemCategory.Equals(XivStrings.Monster)) && _item.ModelInfo.ModelType == XivItemType.demihuman)
                    {
                        _xivMtrl = await _mtrl.GetMtrlData(_item, SelectedRace.XivRace, SelectedType.Name[0], dxVersion);
                    }
                    else if (_item.Category.Equals(XivStrings.Gear))
                    {
                        _xivMtrl = await _mtrl.GetMtrlData(_item, SelectedRace.XivRace, SelectedType.Name[0], dxVersion, SelectedPart.Name);

                        var xivGear = _item as XivGear;

                        if (xivGear.IconNumber != 0)
                        {
                            _xivMtrl.TextureTypePathList.AddRange(await _gear.GetIconInfo(xivGear));
                        }
                    }
                    else
                    {
                        _xivMtrl = await _mtrl.GetMtrlData(_item, SelectedRace.XivRace, SelectedPart.Name[0], dxVersion);
                    }

                    ttpList = _xivMtrl.TextureTypePathList;
                }
            }
            else
            {
                if (_uiItem.UiPath != null)
                {
                    string path;
                    if (_uiItem.ItemCategory.Equals(XivStrings.Maps))
                    {
                        var mapNamePaths = await _tex.GetMapAvailableTex(_uiItem.UiPath);

                        ttpList = new List<TexTypePath>();

                        foreach (var mapNamePath in mapNamePaths)
                        {
                            var ttp = new TexTypePath
                            {
                                DataFile = _uiItem.DataFile,
                                Path = mapNamePath.Value,
                                Type = XivTexType.Map,
                                Name = mapNamePath.Key
                            };

                            ttpList.Add(ttp);
                        }
                    }
                    else if(_uiItem.ItemCategory.Equals(XivStrings.HUD))
                    {
                        path = $"{_uiItem.UiPath}/{_uiItem.Name.ToLower()}.tex";

                        ttpList = new List<TexTypePath> { new TexTypePath { DataFile = _uiItem.DataFile, Path = path, Type = XivTexType.Mask } };
                    }
                    else if (_uiItem.ItemCategory.Equals(XivStrings.Loading_Screen))
                    {
                        path = $"{_uiItem.UiPath}/{_uiItem.Name.ToLower()}.tex";

                        ttpList = new List<TexTypePath> { new TexTypePath { DataFile = _uiItem.DataFile, Path = path, Type = XivTexType.Diffuse } };
                    }
                    else
                    {
                        if (_uiItem.ItemCategory.Equals("Icon"))
                        {
                            var index = new Index(new DirectoryInfo(Properties.Settings.Default.FFXIV_Directory));
                            var languages = new[] {"en", "ja", "fr", "de"};
                            var iconFile = $"{_uiItem.IconNumber.ToString().PadLeft(6, '0')}.tex";
                            var iconFolder = $"{Path.GetDirectoryName(_uiItem.UiPath).Replace("\\", "/")}";

                            ttpList = new List<TexTypePath>();

                            if (await index.FileExists(HashGenerator.GetHash(iconFile), HashGenerator.GetHash(iconFolder), XivDataFile._06_Ui))
                            {
                                var ttp = new TexTypePath { DataFile = _uiItem.DataFile, Path = _uiItem.UiPath, Type = XivTexType.Icon };
                                ttpList.Add(ttp);
                            }

                            foreach (var language in languages)
                            {
                                var iconLangFolder = $"{iconFolder}/{language}";

                                if (await index.FileExists(HashGenerator.GetHash(iconFile), HashGenerator.GetHash(iconLangFolder), XivDataFile._06_Ui))
                                {
                                    var ttp = new TexTypePath { DataFile = _uiItem.DataFile, Path = $"{iconLangFolder}/{iconFile}", Type = XivTexType.Icon, Name = $"Icon {language}"};
                                    ttpList.Add(ttp);
                                }
                            }

                            var iconHQFolder = $"{iconFolder}/hq";

                            if (await index.FileExists(HashGenerator.GetHash(iconFile), HashGenerator.GetHash(iconHQFolder), XivDataFile._06_Ui))
                            {
                                var ttp = new TexTypePath { DataFile = _uiItem.DataFile, Path = $"{iconHQFolder}/{iconFile}", Type = XivTexType.Icon, Name = "Icon HQ"};
                                ttpList.Add(ttp);
                            }
                        }
                        else
                        {
                            ttpList = null;
                        }
                    }

                }
                else
                {
                    var iconNum = _uiItem.IconNumber;
                    var baseNum = 0;

                    if (iconNum >= 1000)
                    {
                        baseNum = (int)Math.Truncate(iconNum / 1000f) * 1000;
                    }

                    var path = $"ui/icon/{baseNum.ToString().PadLeft(6, '0')}/{iconNum.ToString().PadLeft(6, '0')}.tex";

                    ttpList = new List<TexTypePath> { new TexTypePath { DataFile = _uiItem.DataFile, Path = path, Type = XivTexType.Icon } };
                }

                PartVisibility = Visibility.Collapsed;
            }
            if (ttpList == null)
                ttpList = new List<TexTypePath>();
            foreach (var texTypePath in ttpList)
            {
                if (texTypePath.Name != null)
                {
                    Maps.Add(new ComboBoxData { Name = texTypePath.Name, TexType = texTypePath });
                }
                else
                {
                    Maps.Add(new ComboBoxData { Name = texTypePath.Type.ToString(), TexType = texTypePath });
                }
            }

            _mapCount = Maps.Count;

            MapComboboxEnabled = _mapCount > 1;

            SetComboBoxWatermarks();
            SelectedMapIndex = 0;
        }

        /// <summary>
        /// The collection of map data for the map combobox
        /// </summary>
        public ObservableCollection<ComboBoxData> Maps
        {
            get => _mapComboBoxData;
            set { _mapComboBoxData = value; NotifyPropertyChanged(nameof(Maps)); }
        }

        /// <summary>
        /// THe selected map in the map combobox
        /// </summary>
        public ComboBoxData SelectedMap
        {
            get => _selectedMap;
            set
            {
                _selectedMap = value;
                NotifyPropertyChanged(nameof(SelectedMap));
                if (SelectedMapIndex > -1)
                {
                    UpdateImage();
                }
            }
        }

        /// <summary>
        /// The selected map index in the map combobox
        /// </summary>
        public int SelectedMapIndex
        {
            get => _mapIndex;
            set
            {
                _mapIndex = value;
                NotifyPropertyChanged(nameof(SelectedMapIndex));
            }
        }

        /// <summary>
        /// The enabled status of the map combobox
        /// </summary>
        public bool MapComboboxEnabled
        {
            get => _mapEnabled;
            set { _mapEnabled = value; NotifyPropertyChanged(nameof(MapComboboxEnabled)); }
        }

        /// <summary>
        /// Updates the texture image for the selected item
        /// </summary>
        public async void UpdateImage()
        {
            if (!CheckMapIsOK())
            {
                OnLoadingComplete();
                return;
            }
            ImageDisplay = null;
            ChannelsEnabled = true;

            if (SelectedMap.TexType.Type != XivTexType.ColorSet)
            {
                var texData = await _tex.GetTexData(SelectedMap.TexType);

                var mapBytes = await _tex.GetImageData(texData);

                _mapData = new MapData
                {
                    MapBytes = mapBytes,
                    Height = texData.Height,
                    Width = texData.Width
                };

                _imageEffect = new ColorChannels
                {
                    Channel = new System.Windows.Media.Media3D.Point4D(1.0f, 1.0f, 1.0f, 0.0f)
                };

                SetColorChannelFilter();

                _textureView.ImageZoombox.CenterContent();
                _textureView.ImageZoombox.FitToBounds();

                PathString = texData.TextureTypeAndPath.Path;
                TextureFormat = texData.TextureFormat.GetTexDisplayName();
                TextureDimensions = $"{texData.Height} x {texData.Width}";
                MipMapInfo = texData.MipMapCount != 0 ? $"Yes ({texData.MipMapCount})" : "No";
            }
            else
            {
                var dxVersion = int.Parse(Properties.Settings.Default.DX_Version);

                if (_item.Category.Equals(XivStrings.Character))
                {
                    if (TypePartVisibility == Visibility.Visible)
                    {
                        _xivMtrl = await _mtrl.GetMtrlData(_item, SelectedRace.XivRace, SelectedTypePart.Name[0], dxVersion);
                    }
                    else
                    {
                        _xivMtrl = await _mtrl.GetMtrlData(_item, SelectedRace.XivRace, SelectedType.Name[0], dxVersion);
                    }
                }
                else
                {
                    if ((_item.ItemCategory.Equals(XivStrings.Mounts) || _item.ItemCategory.Equals(XivStrings.Monster)) && _item.ModelInfo.ModelType == XivItemType.demihuman)
                    {
                        _xivMtrl = await _mtrl.GetMtrlData(_item, SelectedRace.XivRace, SelectedType.Name[0], dxVersion);
                    }
                    else if (_item.Category.Equals(XivStrings.Gear))
                    {
                        _xivMtrl = await _mtrl.GetMtrlData(_item, SelectedRace.XivRace, SelectedType.Name[0], dxVersion, SelectedPart.Name);
                    }
                    else
                    {
                        _xivMtrl = await _mtrl.GetMtrlData(_item, SelectedRace.XivRace, SelectedPart.Name[0], dxVersion);
                    }
                }

                var floats = Half.ConvertToFloat(_xivMtrl.ColorSetData.ToArray());

                var floatArray = Utilities.ToByteArray(floats);

                _mapData = new MapData
                {
                    MapBytes = floatArray,
                    Height = 16,
                    Width = 4,
                    IsColorSet = true
                };

                _imageEffect = new ColorChannels
                {
                    Channel = new System.Windows.Media.Media3D.Point4D(1.0f, 1.0f, 1.0f, 0.0f)
                };

                SetColorChannelFilter();

                _textureView.ImageZoombox.CenterContent();
                _textureView.ImageZoombox.FitToBounds();

                PathString = SelectedMap.TexType.Path;
                TextureFormat = XivTexFormat.A16B16G16R16F.GetTexDisplayName();
                TextureDimensions = "4 x 16";
            }

            var modStatus = await _modList.IsModEnabled(PathString, false);

            switch (modStatus)
            {
                case XivModStatus.Enabled:
                    ModStatusToggleEnabled = true;
                    ModToggleText = UIStrings.Disable;
                    break;
                case XivModStatus.Disabled:
                    ModStatusToggleEnabled = true;
                    ModToggleText = UIStrings.Enable;
                    break;
                case XivModStatus.MatAdd:
                case XivModStatus.Original:
                default:
                    ModStatusToggleEnabled = false;
                    ModToggleText = UIStrings.Enable_Disable;
                    break;
            }

            ExportEnabled = true;
            ImportEnabled = true;
            MoreOptionsEnabled = true;

            if (_item != null)
            {
                if (_item.ItemCategory.Equals(XivStrings.Hair) || _item.ItemCategory.Equals(XivStrings.Equipment_Decals) || _item.ItemCategory.Equals(XivStrings.Face_Paint))
                {
                    AddNewTexturePartEnabled = false;
                }
                else
                {
                    AddNewTexturePartEnabled = true;
                }
            }
            else
            {
                AddNewTexturePartEnabled = false;
            }


            if (_xivMtrl != null)
            {
                TranslucencyEnabled = true;
                if (_xivMtrl.ShaderNumber == 0x0D)
                {
                    TranslucencyCheck = false;
                }
                else if (_xivMtrl.ShaderNumber == 0x1D)
                {
                    TranslucencyCheck = true;
                }
                else
                {
                    TranslucencyCheck = false;
                    TranslucencyEnabled = false;
                }
            }
            else
            {
                TranslucencyCheck = false;
                TranslucencyEnabled = false;
            }

            OnLoadingComplete();
        }

        /// <summary>
        /// The string for the current texture path
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

        /// <summary>
        /// The string for the current texture format
        /// </summary>
        public string TextureFormat
        {
            get => _textureFormat;
            set
            {
                _textureFormat = value;
                NotifyPropertyChanged(nameof(TextureFormat));
            }
        }

        /// <summary>
        /// The string for the current textures dimensions
        /// </summary>
        public string TextureDimensions
        {
            get => _textureDimensions;
            set
            {
                _textureDimensions = value;
                NotifyPropertyChanged(nameof(TextureDimensions));
            }
        }

        /// <summary>
        /// The string for the current textures mipmaps
        /// </summary>
        public string MipMapInfo
        {
            get => _mipMapInfo;
            set
            {
                _mipMapInfo = "MipMaps: " + value;
                NotifyPropertyChanged(nameof(MipMapInfo));
            }
        }

        #region Image

        /// <summary>
        /// The image to display
        /// </summary>
        public BitmapSource ImageDisplay
        {
            get => _imageDisplay;
            set
            {
                _imageDisplay = value;
                NotifyPropertyChanged(nameof(ImageDisplay));
            }
        }

        /// <summary>
        /// The color channels effect for the image
        /// </summary>
        public ColorChannels ImageEffect
        {
            get
            {
                if (this.RedChecked && this.GreenChecked && this.BlueChecked && this.AlphaChecked)
                    return null;

                return _imageEffect;
            }
            set
            {
                _imageEffect = value;
                NotifyPropertyChanged(nameof(ImageEffect));
            }
        }

        #endregion

        #region Buttons

        public enum TextureFormats
        {
            DDS,
            BMP,
            PNG
        }

        public async Task Export(TextureFormats format)
        {
            if (!CheckMtrlIsOK())
                return;

            if (format == TextureFormats.DDS)
            {
                DirectoryInfo savePath = new DirectoryInfo(Settings.Default.Save_Directory);
                XivTex texData;

                if (SelectedMap.TexType.Type == XivTexType.ColorSet)
                {
                    texData = await _mtrl.MtrlToXivTex(_xivMtrl, SelectedMap.TexType);
                    _mtrl.SaveColorSetExtraData(_item, _xivMtrl, savePath, SelectedRace.XivRace);
                }
                else
                {
                    texData = await _tex.GetTexData(SelectedMap.TexType);
                }

                if (_uiItem != null)
                {
                    _tex.SaveTexAsDDS(_uiItem, texData, savePath);
                }
                else
                {
                    _tex.SaveTexAsDDS(_item, texData, savePath, SelectedRace.XivRace);
                }
            }
            else
            {
                IImageEncoder encoder;
                if (format == TextureFormats.BMP)
                {
                    encoder = new BmpEncoder()
                    {
                        SupportTransparency = true,
                        BitsPerPixel = BmpBitsPerPixel.Pixel32
                    };
                }
                else if (format == TextureFormats.PNG)
                {
                    encoder = new PngEncoder()
                    {
                        BitDepth = PngBitDepth.Bit16
                    };
                }
                else
                {
                    throw new Exception($"Texture format not supported: {format}");
                }

                Image img;
                if (_mapData.IsColorSet)
                {
                    img = Image.LoadPixelData<RgbaVector>(_mapData.MapBytes, _mapData.Width, _mapData.Height);
                }
                else
                {
                    img = Image.LoadPixelData<Rgba32>(_mapData.MapBytes, _mapData.Width, _mapData.Height);
                }

                DirectoryInfo path = GetDefaultPath(format);

                if (!path.Parent.Exists)
                    path.Parent.Create();

                img.Save(path.FullName, encoder);
                img.Dispose();
            }
        }

        /// <summary>
        /// Command for the Open Folder Button
        /// </summary>
        public ICommand OpenFolder => new RelayCommand(OpenSavedFolder);

        /// <summary>
        /// Opens the save folder for the item, creates one if it doesn't exist
        /// </summary>
        private void OpenSavedFolder(object obj)
        {
            var savePath = new DirectoryInfo(Settings.Default.Save_Directory);
            var path = savePath.FullName;

            if (_item != null)
            {
                path = IOUtil.MakeItemSavePath(_item, savePath, SelectedRace.XivRace);
            }
            else if (_uiItem != null)
            {
                path = IOUtil.MakeItemSavePath(_uiItem, savePath);
            }

            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }

            Process.Start(path);
        }

        /// <summary>
        /// Imports a texture file 
        /// </summary>
        /// <remarks>
        /// Import a texture file from any location
        /// </remarks>
        public async Task ImportFrom()
        {
            if (!CheckMtrlIsOK())
                return;

            DirectoryInfo path = this.GetDefaultPath();
            if (path == null)
                return;

            var openFileDialog = new OpenFileDialog { InitialDirectory = path.FullName, Filter = "Texture Files(*.DDS;*.BMP;*.PNG) |*.DDS;*.BMP;*.PNG" };

            if (openFileDialog.ShowDialog() != DialogResult.OK)
                return;

            await this.Import(openFileDialog.FileName);
        }

        public async Task Import(TextureFormats format)
        {
            DirectoryInfo path = GetDefaultPath(format);
            await this.Import(path.FullName);
        }

        public bool GetDefaultFileExists(TextureFormats format)
        {
            DirectoryInfo path = GetDefaultPath(format);
            return File.Exists(path.FullName);
        }

        private DirectoryInfo GetDefaultPath(TextureFormats format)
        {
            DirectoryInfo path = this.GetDefaultPath();
            if (path == null)
                return null;

            string extension = "dds";
            if (format == TextureFormats.BMP)
                extension = "bmp";

            if (format == TextureFormats.PNG)
                extension = "png";

            DirectoryInfo fullPath = new DirectoryInfo($"{path}\\{Path.GetFileNameWithoutExtension(SelectedMap.TexType.Path)}.{extension}");
            return fullPath;
        }

        private DirectoryInfo GetDefaultPath()
        {
            DirectoryInfo gameDirectory = new DirectoryInfo(Settings.Default.FFXIV_Directory);
            Index index = new Index(gameDirectory);

            if (index.IsIndexLocked(XivDataFile._0A_Exd))
            {
                FlexibleMessageBox.Show(UIMessages.IndexLockedErrorMessage, UIMessages.IndexLockedErrorTitle, MessageBoxButtons.OK, MessageBoxIcon.Error);
                return null;
            }

            DirectoryInfo savePath = new DirectoryInfo(Settings.Default.Save_Directory);
            DirectoryInfo path;

            if (_item != null)
            {
                path = new DirectoryInfo(IOUtil.MakeItemSavePath(_item, savePath, SelectedRace.XivRace));
            }
            else if (_uiItem != null)
            {
                path = new DirectoryInfo(IOUtil.MakeItemSavePath(_uiItem, savePath));
            }
            else
            {
                throw new Exception("Unsupported item type");
            }

            return path;
        }

        public async Task Import(string fileName)
        {
            var fileDir = new DirectoryInfo(fileName);
            var dxVersion = int.Parse(Settings.Default.DX_Version);

            if (fileDir.FullName.ToLower().Contains(".dds"))
            {
                if (SelectedMap.TexType.Type != XivTexType.ColorSet)
                {
                    var texData = await _tex.GetTexData(SelectedMap.TexType);

                    try
                    {
                        if (_item != null)
                        {
                            await _tex.TexDDSImporter(texData, _item, fileDir, XivStrings.TexTools);
                        }
                        else if (_uiItem != null)
                        {
                            await _tex.TexDDSImporter(texData, _uiItem, fileDir, XivStrings.TexTools);
                        }
                    }
                    catch (Exception ex)
                    {
                        FlexibleMessageBox.Show(
                            string.Format(UIMessages.TextureImportErrorMessage, ex.Message), UIMessages.TextureImportErrorTitle,
                            MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }
                }
                else
                {
                    try
                    {
                        var newColorSetOffset = await _tex.TexColorImporter(_xivMtrl, fileDir, _item, XivStrings.TexTools, GetLanguage());
                        _xivMtrl = await _mtrl.GetMtrlData(newColorSetOffset, _xivMtrl.MTRLPath, dxVersion);
                    }
                    catch (Exception ex)
                    {
                        FlexibleMessageBox.Show(
                            string.Format(UIMessages.TextureImportErrorMessage, ex.Message), UIMessages.TextureImportErrorTitle,
                            MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }                        
                }
            }
            else
            {
                if (SelectedMap.TexType.Type != XivTexType.ColorSet)
                {
                    var texData = await _tex.GetTexData(SelectedMap.TexType);

                    try
                    {
                        if (_item != null)
                        {
                            await _tex.TexImporter(texData, _item, fileDir, XivStrings.TexTools);
                        }
                        else if (_uiItem != null)
                        {
                            await _tex.TexImporter(texData, _uiItem, fileDir, XivStrings.TexTools);
                        }
                    }
                    catch (Exception ex)
                    {
                        FlexibleMessageBox.Show(
                            string.Format(UIMessages.TextureImportErrorMessage, ex.Message), UIMessages.TextureImportErrorTitle,
                            MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }
                }
                else
                {
                    FlexibleMessageBox.Show(
                        UIMessages.ColorSetBMPNotSupportedMessage, UIMessages.TextureImportErrorTitle,
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
            }

            UpdateImage();
            
        }


        /// <summary>
        /// Command for the Mod Status Toggle Button
        /// </summary>
        public ICommand ModStatusToggleButton => new RelayCommand(ModStatusToggle);

        /// <summary>
        /// Toggles the mod ON or OFF
        /// </summary>
        private async void ModStatusToggle(object obj)
        {
            if (!CheckMtrlIsOK())
                return;
            var gameDirectory = new DirectoryInfo(Properties.Settings.Default.FFXIV_Directory);
            var modlist = new Modding(gameDirectory);

            if (ModToggleText.Equals(UIStrings.Enable))
            {
                await modlist.ToggleModStatus(SelectedMap.TexType.Path, true);
            }
            else if (ModToggleText.Equals(UIStrings.Disable))
            {
                await modlist.ToggleModStatus(SelectedMap.TexType.Path, false);
            }
            else
            {
                FlexibleMessageBox.Show(
                    UIMessages.ModToggleErrorMessage, UIMessages.ModToggleErrorTitle,
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            UpdateImage();
        }

        /// <summary>
        /// Command for the AddNewTexturePart Button
        /// </summary>
        public ICommand AddNewTexturePartButton => new RelayCommand(AddNewTexturePart);

        /// <summary>
        /// Add New Texture Part
        /// </summary>
        private async void AddNewTexturePart(object obj)
        {
            try
            {
                if (!CheckMtrlIsOK())
                    return;
                if (_item.Category != XivStrings.Gear && _item.Category != XivStrings.Character)
                {
                    FlexibleMessageBox.Show(UIMessages.AddNewTexturePartErrorMessageWrongCategoryOfItem,
                        UIMessages.AddNewTexturePartErrorTitle, MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
                if (_item.ItemCategory == XivStrings.Face_Paint || _item.ItemCategory == XivStrings.Equipment_Decals || _item.ItemCategory == XivStrings.Hair)
                {
                    FlexibleMessageBox.Show(UIMessages.AddNewTexturePartErrorMessageWrongItemCategoryOfItem,
                        UIMessages.AddNewTexturePartErrorTitle, MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
                //Legitimacy check
                var partChars = new char[] { 'a', 'b', 'c', 'd', 'e', 'f', 'g', 'h', 'i', 'j', 'k', 'l', 'm', 'n', 'o', 'p', 'q', 'r', 's', 't', 'u', 'v', 'w', 'x', 'y', 'z' };
                List<string> partList = new List<string>();
                if (_item.Category.Equals(XivStrings.Gear))
                {
                    partList = Types.Select(it => it.Name).ToList();
                }
                else if (_item.Category.Equals(XivStrings.Character))
                {
                    partList = TypeParts.Select(it => it.Name).ToList();
                }
                if (partList.Count >= 6)
                {
                    FlexibleMessageBox.Show(UIMessages.AddNewTexturePartErrorMessage,
                        UIMessages.AddNewTexturePartErrorTitle, MessageBoxButtons.OK, MessageBoxIcon.Error);
                    //AddNewTexturePartEnabled = false;
                    return;
                }
                //Get the new part name
                var newPartName = '\0';
                for (var i = 1; i < partChars.Length; i++)
                {
                    newPartName = partChars[i];
                    if (!partList.Any(it => it == newPartName.ToString()))
                    {
                        break;
                    }
                }
                if (newPartName == '\0')
                {
                    FlexibleMessageBox.Show(UIMessages.AddNewTexturePartErrorMessage,
                        UIMessages.AddNewTexturePartErrorTitle, MessageBoxButtons.OK, MessageBoxIcon.Error);
                    //AddNewTexturePartEnabled = false;
                    return;
                }
                //Update the new part path;
                var gameDirectory = new DirectoryInfo(Settings.Default.FFXIV_Directory);
                var index = new Index(gameDirectory);
                if (index.IsIndexLocked(XivDataFile._0A_Exd))
                {
                    FlexibleMessageBox.Show(UIMessages.IndexLockedErrorMessage,
                        UIMessages.IndexLockedErrorTitle, MessageBoxButtons.OK, MessageBoxIcon.Error);

                    return;
                }
                var mtrlOffset = await index.GetDataOffset(HashGenerator.GetHash(Path.GetDirectoryName(_xivMtrl.MTRLPath).Replace("\\", "/")), HashGenerator.GetHash(Path.GetFileName(_xivMtrl.MTRLPath)), _item.DataFile);
                var xivMtrl = await _mtrl.GetMtrlData(mtrlOffset, _xivMtrl.MTRLPath, 11);
                var oldTexturePathSize = xivMtrl.TexturePathList.Select(it => it.Replace("--", String.Empty)).Sum(it => it.Length) + xivMtrl.TexturePathList.Count;
                var oldTexturePathOffsetDataSize = xivMtrl.TexturePathOffsetList.Count * 4;
                var oldStructSize = xivMtrl.DataStruct1Count * 8 + xivMtrl.DataStruct2Count * 8 + xivMtrl.ParameterStructCount * 12;
                for (var i = xivMtrl.TextureTypePathList.Count; i < _xivMtrl.TextureTypePathList.Count; i++)
                {
                    var tmpTypePath = _xivMtrl.TextureTypePathList[i];
                    xivMtrl.TextureTypePathList.Add(new TexTypePath() { DataFile = tmpTypePath.DataFile, Name = tmpTypePath.Name, Path = tmpTypePath.Path, Type = tmpTypePath.Type });
                }
                var textureTypePathListBak = new List<TexTypePath>();
                for (var i = 0; i < xivMtrl.TextureTypePathList.Count; i++)
                {
                    var tmpTypePath = xivMtrl.TextureTypePathList[i];
                    textureTypePathListBak.Add(new TexTypePath() { DataFile = tmpTypePath.DataFile, Name = tmpTypePath.Name, Path = tmpTypePath.Path, Type = tmpTypePath.Type });
                }
                bool tplNeedAdd = xivMtrl.TexturePathList.Count == 2 && xivMtrl.TexturePathList[0].EndsWith("_n.tex") && xivMtrl.TexturePathList[1].EndsWith("_m.tex");
                if (tplNeedAdd
                    && FlexibleMessageBox.Show(UIMessages.AddNewTexturePartQuestionMessage,
                        UIMessages.AddNewTexturePartQuestionTitle, MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes
                )
                {
                    tplNeedAdd = false;
                }
                if (tplNeedAdd)
                {
                    var tmpTpl = xivMtrl.TexturePathList[0];
                    xivMtrl.TexturePathList.Insert(0, tmpTpl.Replace("_n.tex", "_d.tex"));
                    xivMtrl.TexturePathOffsetList.Add(0);
                    xivMtrl.TexturePathUnknownList.Add(0);
                    xivMtrl.TexturePathList[2] = xivMtrl.TexturePathList[2].Replace("_m.tex", "_s.tex");
                    var tmpTexTypePath = xivMtrl.TextureTypePathList[0];
                    xivMtrl.TextureTypePathList.Insert(0, new TexTypePath() { DataFile = tmpTexTypePath.DataFile, Path = tmpTexTypePath.Path.Replace("_n.tex", "_d.tex"), Type = XivTexType.Diffuse });
                    xivMtrl.TextureTypePathList[2].Path = xivMtrl.TextureTypePathList[2].Path.Replace("_m.tex", "_s.tex");
                    xivMtrl.TextureTypePathList[2].Type = XivTexType.Specular;

                    xivMtrl.DataStruct1Count = 3;
                    xivMtrl.DataStruct1List.Clear();
                    xivMtrl.DataStruct1List.Add(new DataStruct1() { ID = 0xf52ccf05, Unknown1 = 0xa7d2ff60 });
                    xivMtrl.DataStruct1List.Add(new DataStruct1() { ID = 0xb616dc5a, Unknown1 = 0x600ef9df });
                    xivMtrl.DataStruct1List.Add(new DataStruct1() { ID = 0xd2777173, Unknown1 = 0xf35f5131 });

                    xivMtrl.DataStruct2Count = 3;
                    xivMtrl.DataStruct2List.Clear();
                    xivMtrl.DataStruct2List.Add(new DataStruct2() { ID = 0x29ac0223, Offset = 0x0000, Size = 0x0004 });
                    xivMtrl.DataStruct2List.Add(new DataStruct2() { ID = 0x575abfb2, Offset = 0x0004, Size = 0x0004 });
                    xivMtrl.DataStruct2List.Add(new DataStruct2() { ID = 0x15b70e35, Offset = 0x0008, Size = 0x0004 });


                    xivMtrl.ParameterStructCount = 3;
                    xivMtrl.ParameterStructList.Clear();
                    xivMtrl.ParameterStructList.Add(new ParameterStruct() { ID = 0x115306be, TextureIndex = 0x00000000, Unknown1 = -31936, Unknown2 = 0x000f });
                    xivMtrl.ParameterStructList.Add(new ParameterStruct() { ID = 0x0c5ec1f1, TextureIndex = 0x00000001, Unknown1 = -32768, Unknown2 = 0x000f });
                    xivMtrl.ParameterStructList.Add(new ParameterStruct() { ID = 0x2b99e025, TextureIndex = 0x00000002, Unknown1 = -31936, Unknown2 = 0x000f });
                }
                for (var i = 0; i < xivMtrl.TexturePathList.Count; i++)
                {
                    var tmps = xivMtrl.TexturePathList[i].Split('_');
                    var typeName = tmps[tmps.Length - 1];
                    var oldPartName = tmps[tmps.Length - 2];
                    if (partChars.Any(it => it.ToString() == oldPartName))
                    {
                        xivMtrl.TexturePathList[i] = xivMtrl.TexturePathList[i].Replace($"_{oldPartName}_{typeName}", $"_{newPartName}_{typeName}");
                    }
                    else
                    {
                        xivMtrl.TexturePathList[i] = xivMtrl.TexturePathList[i].Replace($"_{typeName}", $"_{newPartName}_{typeName}");
                    }
                    xivMtrl.TexturePathOffsetList[i] = 0;
                    if (i > 0)
                    {
                        xivMtrl.TexturePathOffsetList[i] = xivMtrl.TexturePathOffsetList[i - 1] + xivMtrl.TexturePathList[i - 1].Replace("--","").Length + 1;
                    }
                }
                //Adjust data size
                var newTexturePathSize = xivMtrl.TexturePathList.Select(it=>it.Replace("--","")).Sum(it => it.Length) + xivMtrl.TexturePathList.Count;
                var valueOfSizeChange = newTexturePathSize - oldTexturePathSize;
                xivMtrl.FileSize += (short)(valueOfSizeChange + xivMtrl.TexturePathOffsetList.Count * 4 - oldTexturePathOffsetDataSize);
                var newStructSize = xivMtrl.DataStruct1Count * 8 + xivMtrl.DataStruct2Count * 8 + xivMtrl.ParameterStructCount * 12;
                xivMtrl.FileSize += (short)(newStructSize - oldStructSize);
                xivMtrl.TextureCount = (byte)xivMtrl.TexturePathList.Count;
                if (valueOfSizeChange > 0)
                {
                    xivMtrl.MaterialDataSize += (ushort)valueOfSizeChange;
                    xivMtrl.TexturePathsDataSize += (ushort)valueOfSizeChange;
                }
                else
                {
                    xivMtrl.MaterialDataSize -= (ushort)(valueOfSizeChange * -1);
                    xivMtrl.TexturePathsDataSize -= (ushort)(valueOfSizeChange * -1);
                }
                for (var i = 0; i < xivMtrl.ColorSetPathOffsetList.Count; i++)
                {
                    xivMtrl.ColorSetPathOffsetList[i] += valueOfSizeChange;
                }
                for (var i = 0; i < xivMtrl.MapPathOffsetList.Count; i++)
                {
                    xivMtrl.MapPathOffsetList[i] += valueOfSizeChange;
                }
                //add new mtrl                       
                var sameModelItems = await GetSameModelList();
                var oldVersionStr = $"/v{_item.ModelInfo.Variant.ToString().PadLeft(4, '0')}/";
                var oldMTRLPath = xivMtrl.MTRLPath;
                foreach (var item in sameModelItems)
                {
                    var dxVersion = int.Parse(Properties.Settings.Default.DX_Version);
                    XivMtrl itemXivMtrl;
                    if (TypePartVisibility == Visibility.Visible)
                    {
                        itemXivMtrl = await _mtrl.GetMtrlData(item, SelectedRace.XivRace, SelectedTypePart.Name[0], dxVersion, SelectedType.Name);
                    }
                    else
                    {
                        itemXivMtrl = await _mtrl.GetMtrlData(item, SelectedRace.XivRace, SelectedType.Name[0], dxVersion);
                    }
                    var tmps2 = xivMtrl.MTRLPath.Split('_');
                    xivMtrl.MTRLPath = xivMtrl.MTRLPath.Replace($"_{tmps2[tmps2.Length - 1]}", $"_{newPartName}.mtrl");
                    xivMtrl.MTRLPath = xivMtrl.MTRLPath.Replace(oldVersionStr, $"/v{item.ModelInfo.Variant.ToString().PadLeft(4, '0')}/");
                    xivMtrl.ColorSetDataSize = itemXivMtrl.ColorSetDataSize;
                    xivMtrl.ColorSetData = itemXivMtrl.ColorSetData == null ? new List<Half>() : itemXivMtrl.ColorSetData;
                    xivMtrl.ColorSetExtraData = itemXivMtrl.ColorSetExtraData == null ? new byte[0] : itemXivMtrl.ColorSetExtraData;
                    oldVersionStr = $"/v{item.ModelInfo.Variant.ToString().PadLeft(4, '0')}/";
                    var newMtrlOffset = await _mtrl.ImportMtrl(xivMtrl, item, "FilesAddedByTexTools");
                }

                //add new tex
                if (Directory.Exists("AddNewTexturePartTexTmps"))
                {
                    Directory.Delete("AddNewTexturePartTexTmps", true);
                }
                var dirInfo = Directory.CreateDirectory("AddNewTexturePartTexTmps");
                for (var i = 0; i < xivMtrl.TexturePathList.Count; i++)
                {
                    var typePathIndex = i;
                    if (tplNeedAdd)
                    {
                        switch (i)
                        {
                            case 0:
                                typePathIndex = 1;
                                break;
                            case 1:
                                typePathIndex = 0;
                                break;
                            case 2:
                                typePathIndex = 1;
                                break;
                        }
                    }
                    var xivTex = await _tex.GetTexData(textureTypePathListBak[typePathIndex]);
                    _tex.SaveTexAsDDS(_item, xivTex, dirInfo, SelectedRace.XivRace);
                    var oldPath = textureTypePathListBak[typePathIndex].Path;
                    xivTex.TextureTypeAndPath.Path = xivMtrl.TexturePathList[i];
                    xivTex.TextureTypeAndPath.Type = xivMtrl.TextureTypePathList[i].Type;
                    var newOffset = await _tex.TexDDSImporter(xivTex, _item, new DirectoryInfo(Directory.GetFiles("AddNewTexturePartTexTmps", $"{Path.GetFileNameWithoutExtension(oldPath)}.dds", SearchOption.AllDirectories)[0]), "AddNewTexturePart");
                }
                if (Directory.Exists("AddNewTexturePartTexTmps"))
                {
                    Directory.Delete("AddNewTexturePartTexTmps", true);
                }
                //update ui    
                void SetToNewTexturePart(object sender, EventArgs e)
                {
                    LoadingComplete -= SetToNewTexturePart;
                    if (_item.Category.Equals(XivStrings.Gear))
                    {
                        SelectedType = Types[Types.Count - 1];
                    }
                    else if (_item.Category.Equals(XivStrings.Character))
                    {
                        SelectedTypePart = TypeParts[TypeParts.Count - 1];
                    }
                }
                LoadingComplete += SetToNewTexturePart;
                SelectedRace = SelectedRace;
            }
            catch(Exception ex)
            {
                FlexibleMessageBox.Show($"{UIMessages.AddNewTexturePartErrorTitle}:{ex.Message}",
                        UIMessages.AddNewTexturePartErrorTitle, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

            this._textureView.BottomFlyout.IsOpen = false;
        }
        async Task<List<IItemModel>> GetSameModelList()
        {
            var gameDirectory = new DirectoryInfo(Settings.Default.FFXIV_Directory);
            var sameModelItems=new List<IItemModel>();
            var gear = new Gear(gameDirectory, GetLanguage());
            var character = new Character(gameDirectory, GetLanguage());
            var companions = new Companions(gameDirectory, GetLanguage());
            var ui = new UI(gameDirectory, GetLanguage());
            var housing = new Housing(gameDirectory, GetLanguage());
            //gear
            if (_item.Category.Equals(XivStrings.Gear))
            {
                sameModelItems.AddRange(
                    (await gear.GetGearList())
                    .Where(it =>
                    it.ModelInfo.ModelID == _item.ModelInfo.ModelID
                    && it.ItemCategory == _item.ItemCategory).Select(it => it as IItemModel).ToList()
                );
            }
            else if (_item.Category.Equals(XivStrings.Character))
            {
                //character
                sameModelItems.Add(
                    new XivGenericItemModel
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
                    }
                    as IItemModel
                );
            }
            //companions
            //sameModelItems.AddRange(
            //    (await companions.GetMinionList())
            //    .Where(it =>
            //    it.ModelInfo.ModelID == _item.ModelInfo.ModelID
            //    && it.ItemCategory == _item.ItemCategory).Select(it => it as IItemModel).ToList()
            //);
            //sameModelItems.AddRange(
            //    (await companions.GetMountList())
            //    .Where(it =>
            //    it.ModelInfo.ModelID == _item.ModelInfo.ModelID
            //    && it.ItemCategory == _item.ItemCategory).Select(it => it as IItemModel).ToList()
            //);
            //sameModelItems.AddRange(
            //    (await companions.GetPetList())
            //    .Where(it =>
            //    it.ModelInfo.ModelID == _item.ModelInfo.ModelID
            //    && it.ItemCategory == _item.ItemCategory).Select(it => it as IItemModel).ToList()
            //);
            //housing
            //sameModelItems.AddRange(
            //    (await housing.GetFurnitureList())
            //    .Where(it =>
            //    it.ModelInfo.ModelID == _item.ModelInfo.ModelID
            //    && it.ItemCategory == _item.ItemCategory).Select(it => it as IItemModel).ToList()
            //);
            return sameModelItems;
        }

        /// <summary>
        /// The enabled status for the export button
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
        /// The enabled status for the import button
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
        /// The enabled status for the mod status button
        /// </summary>
        public bool ModStatusToggleEnabled
        {
            get => _modStatusEnabled;
            set
            {
                _modStatusEnabled = value;
                NotifyPropertyChanged(nameof(ModStatusToggleEnabled));
            }
        }

        /// <summary>
        /// The text in the mod toggle button
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
        /// THe enabled status of the more options button
        /// </summary>
        public bool MoreOptionsEnabled
        {
            get => _moreOptionsEnabled;
            set
            {
                _moreOptionsEnabled = value;
                NotifyPropertyChanged(nameof(MoreOptionsEnabled));
            }
        }

        /// <summary>
        /// The enabled status of the translucency toggle
        /// </summary>
        public bool TranslucencyEnabled
        {
            get => _translucencyEnabled;
            set
            {
                _translucencyEnabled = value;
                NotifyPropertyChanged(nameof(TranslucencyEnabled));
            }
        }

        /// <summary>
        /// The enabled status of the add new texture part button        
        /// </summary>
        public bool AddNewTexturePartEnabled
        {
            get => _addNewTexturePartEnabled;
            set
            {
                _addNewTexturePartEnabled = value;
                NotifyPropertyChanged(nameof(AddNewTexturePartEnabled));
            }
        }

        /// <summary>
        /// The checked status of the translucency toggle 
        /// </summary>
        public bool TranslucencyCheck
        {
            get => _translucencyCheck;
            set
            {
                _translucencyCheck = value;
                NotifyPropertyChanged(nameof(TranslucencyCheck));
                TranslucencyChanged();
            }
        }
        #endregion

        /// <summary>
        /// Modifies the material file when the translucency toggle is changed
        /// </summary>
        private async void TranslucencyChanged()
        {
            if (_xivMtrl == null) return;

            try
            {
                if (TranslucencyCheck)
                {
                    if (_xivMtrl.ShaderNumber == 0x0D)
                    {
                        await _mtrl.ToggleTranslucency(_xivMtrl, _item, TranslucencyCheck, XivStrings.TexTools);
                    }
                }
                else
                {
                    if (_xivMtrl.ShaderNumber == 0x1D)
                    {
                        await _mtrl.ToggleTranslucency(_xivMtrl, _item, TranslucencyCheck, XivStrings.TexTools);
                    }
                }
            }
            catch (Exception ex)
            {
                FlexibleMessageBox.Show(
                    string.Format(UIMessages.TranslucencyToggleErrorMessage, ex.Message), UIMessages.TranslucencyToggleErrorTitle,
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

        }

        /// <summary>
        /// Clears All the UI elements
        /// </summary>
        private void ClearAll()
        {
            Races.Clear();
            SelectedRaceIndex = -1;
            Parts.Clear();
            SelectedPartIndex = -1;
            Types.Clear();
            SelectedTypeIndex = -1;
            TypeParts.Clear();
            SelectedTypePartIndex = -1;
            Maps.Clear();
            SelectedMapIndex = -1;
            _item = null;
            _uiItem = null;
            _xivMtrl = null;

            TypePartVisibility = Visibility.Collapsed;
            TypeVisibility = Visibility.Collapsed;
            PartVisibility = Visibility.Visible;
        }

        /// <summary>
        /// Sets the watermarks for the combobox
        /// </summary>
        private void SetComboBoxWatermarks()
        {
            if (_item == null) return;

            RaceWatermark = $"{XivStrings.Race}  |  {_raceCount}";
            TextureMapWatermark = $"{XivStrings.Texture_Map}  |  {_mapCount}";

            if (_item.Category.Equals(XivStrings.Character))
            {
                PartWatermark = $"{XivStrings.Number}  |  {_partCount}";

                if (_item.ItemCategory.Equals(XivStrings.Body))
                {
                    TypeWatermark = XivStrings.Variant;
                    TypePartWatermark = XivStrings.Part;
                }
                else
                {
                    TypeWatermark = TypePartVisibility == Visibility.Visible ? $"{XivStrings.Type}  |  {_typeCount}" : $"{XivStrings.Part}  |  {_typeCount}";

                    if (TypePartVisibility == Visibility.Visible)
                    {
                        TypePartWatermark = $"{XivStrings.TypePart}  |  {_typePartCount}";
                    }
                }
            }
            else if (_item.ItemCategory.Equals(XivStrings.Mounts) && _item.ModelInfo.ModelType == XivItemType.demihuman)
            {
                PartWatermark = $"{XivStrings.Type}  |  {_partCount}";
                TypeWatermark = $"{XivStrings.Part}  |  {_typeCount}";
            }
            else
            {
                PartWatermark = $"{XivStrings.Part}  |  {_partCount}";
                TypeWatermark = $"{XivStrings.Type}  |  {_typeCount}";
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
        /// The watermark for the type combobox
        /// </summary>
        public string TypeWatermark
        {
            get => _typeWatermark;
            set
            {
                _typeWatermark = value;
                NotifyPropertyChanged(nameof(TypeWatermark));
            }
        }

        /// <summary>
        /// The watermark for the type part combobox
        /// </summary>
        public string TypePartWatermark
        {
            get => _typePartWatermark;
            set
            {
                _typePartWatermark = value;
                NotifyPropertyChanged(nameof(TypePartWatermark));
            }
        }

        /// <summary>
        /// The watermark for the texture map combobox
        /// </summary>
        public string TextureMapWatermark
        {
            get => _textureMapWatermark;
            set
            {
                _textureMapWatermark = value;
                NotifyPropertyChanged(nameof(TextureMapWatermark));
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

        // Color Channels
        #region ColorChannels

        public bool ChannelsEnabled
        {
            get => _channelsEnabled;
            set
            {
                _channelsEnabled = value;
                NotifyPropertyChanged(nameof(ChannelsEnabled));
            }
        }

        /// <summary>
        /// Red color channel checked status
        /// </summary>
        public bool RedChecked
        {
            get => _redChecked;
            set
            {
                _redChecked = value;
                NotifyPropertyChanged(nameof(RedChecked));
                SetColorChannelFilter();
            }
        }

        /// <summary>
        /// Blue color channel checked status
        /// </summary>
        public bool BlueChecked
        {
            get => _blueChecked;
            set
            {
                _blueChecked = value;
                NotifyPropertyChanged(nameof(BlueChecked));
                SetColorChannelFilter();
            }
        }

        /// <summary>
        /// Green color channel checked status
        /// </summary>
        public bool GreenChecked
        {
            get => _greenChecked;
            set
            {
                _greenChecked = value;
                NotifyPropertyChanged(nameof(GreenChecked));
                SetColorChannelFilter();
            }
        }

        /// <summary>
        /// Alpha color channel checked status
        /// </summary>
        public bool AlphaChecked
        {
            get => _alphaChecked;
            set
            {
                _alphaChecked = value;
                NotifyPropertyChanged(nameof(AlphaChecked));
                SetColorChannelFilter();
            }
        }

        /// <summary>
        /// Sets the color channel filter on currently displayed image based on selected color checkboxes.
        /// </summary>
        private void SetColorChannelFilter()
        {
            var r = RedChecked   ? 1.0f : 0.0f;
            var g = GreenChecked ? 1.0f : 0.0f;
            var b = BlueChecked  ? 1.0f : 0.0f;
            var a = AlphaChecked ? 1.0f : 0.0f;

            _imageEffect.Channel = new System.Windows.Media.Media3D.Point4D(r, g, b, a);
            NotifyPropertyChanged(nameof(ImageEffect));

            IImageEncoder imageEncoder;

            if (AlphaChecked)
            {
                imageEncoder = new PngEncoder();
            }
            else
            {
                imageEncoder = new BmpEncoder();
            }

            if (!_mapData.IsColorSet)
            {
                using (var img = Image.LoadPixelData<Rgba32>(_mapData.MapBytes, _mapData.Width, _mapData.Height))
                {
                    using (var ms = new MemoryStream())
                    {
                        img.Save(ms, imageEncoder);

                        var bmp = new BitmapImage();
                        bmp.BeginInit();
                        bmp.StreamSource = ms;
                        bmp.CacheOption = BitmapCacheOption.OnLoad;
                        bmp.EndInit();
                        bmp.Freeze();

                        ImageDisplay = bmp;
                    }
                }
            }
            else
            {
                using (var img = Image.LoadPixelData<RgbaVector>(_mapData.MapBytes, _mapData.Width, _mapData.Height))
                {
                    using (var ms = new MemoryStream())
                    {
                        img.Save(ms, imageEncoder);

                        var bmp = new BitmapImage();
                        bmp.BeginInit();
                        bmp.StreamSource = ms;
                        bmp.CacheOption = BitmapCacheOption.OnLoad;
                        bmp.EndInit();
                        bmp.Freeze();

                        ImageDisplay = bmp;
                    }
                }
            }
        }
        #endregion

        private static XivLanguage GetLanguage()
        {
            return XivLanguages.GetXivLanguage(Properties.Settings.Default.Application_Language);
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void NotifyPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public class ComboBoxData
        {
            public string Name { get; set; }

            public XivRace XivRace { get; set; }

            public TexTypePath TexType { get; set; }

            public char[] TypeParts { get; set; }
        }

        protected virtual void OnLoadingComplete()
        {
            LoadingComplete?.Invoke(this, EventArgs.Empty);
        }
        private bool CheckMtrlIsOK()
        {
            if (_xivMtrl != null)
            {
                var gameDirectory = new DirectoryInfo(Settings.Default.FFXIV_Directory);
                var index = new Index(gameDirectory);
                var offset = index.GetDataOffset(HashGenerator.GetHash(Path.GetDirectoryName(_xivMtrl.MTRLPath).Replace("\\", "/")), HashGenerator.GetHash(Path.GetFileName(_xivMtrl.MTRLPath)), _item.DataFile).GetAwaiter().GetResult();
                if (offset == 0)
                {
                    SelectedPart = SelectedPart;
                    return false;
                }
            }
            return CheckMapIsOK();
        }
        private bool CheckMapIsOK()
        {
            var path = SelectedMap.TexType.Path;
            var gameDirectory = new DirectoryInfo(Settings.Default.FFXIV_Directory);
            var index = new Index(gameDirectory);

            XivDataFile? dataFile = null;
            if (path.StartsWith("ui/"))
                dataFile = XivDataFile._06_Ui;
            else if (_item != null)
                dataFile = _item.DataFile;
            if (dataFile == null)
                return true;
            var offset = index.GetDataOffset(HashGenerator.GetHash(Path.GetDirectoryName(path).Replace("\\", "/")), HashGenerator.GetHash(Path.GetFileName(path)),dataFile.Value).GetAwaiter().GetResult();
            if (offset>0)
            {
                return true;
            }
            SelectedPart = SelectedPart;
            return false;
        }

        private class MapData
        {
            public byte[] MapBytes;
            public int Width;
            public int Height;
            public bool IsColorSet = false;
        }
    }
}