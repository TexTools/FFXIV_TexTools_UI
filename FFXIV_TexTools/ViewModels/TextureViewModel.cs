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
using FFXIV_TexTools.Resources;
using FFXIV_TexTools.Textures;
using FFXIV_TexTools.Views;
using ImageMagick;
using SharpDX;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Input;
using FFXIV_TexTools.Properties;
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
        private MagickImage _magickImage;
        private readonly TextureView _textureView;

        private Dictionary<XivRace, int[]> _charaRaceAndNumberDictionary;

        private Visibility _typePartVisibility, _typeVisibility, _partVisibility;
        private IItemModel _item;

        private string _pathString, _textureFormat, _textureDimensions, _category;
        private string _partWatermark = XivStrings.Part, _typeWatermark = XivStrings.Type, _raceWatermark = XivStrings.Race,
            _typePartWatermark = XivStrings.TypePart, _textureMapWatermark = XivStrings.Texture_Map;
        private string _modToggleText = "Enable/Disable";

        private bool _raceEnabled, _partEnabled, _typeEnabled, _typePartEnabled, _mapEnabled, _channelsEnabled;
        private bool _exportEnabled, _importEnabled, _ddsImportEnabled, _bmpImportEnabled, _modStatusEnabled, _moreOptionsEnabled, _translucencyEnabled, _translucencyCheck;
        private bool _redChecked = true, _greenChecked = true, _blueChecked = true, _alphaChecked;

        private int _raceIndex, _partIndex, _typeIndex, _typePartIndex, _mapIndex, _partCount, _typeCount, _typePartCount, _mapCount, _raceCount;


        public TextureViewModel(TextureView textureView)
        {
            _textureView = textureView;
            ChannelsEnabled = false;
        }

        /// <summary>
        /// Updates the texture
        /// </summary>
        /// <param name="item">The item to update the texture for</param>
        public void UpdateTexture(IItem item)
        {
            ClearAll();

            var gameDirectory = new DirectoryInfo(Properties.Settings.Default.FFXIV_Directory);
            _mtrl = new Mtrl(gameDirectory, item.DataFile);
            _tex = new Tex(gameDirectory);
            _modList = new Modding(gameDirectory);

            if (item.Category.Equals(XivStrings.Gear))
            {
                _category = XivStrings.Gear;
                _item = item as IItemModel;

                _gear = new Gear(gameDirectory, GetLanguage());

                var gearItem = item as XivGear;

                var raceList = _gear.GetRacesForTextures(gearItem, item.DataFile);

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
                _character = new Character(gameDirectory);
                _item = item as IItemModel;

                if (_item.ItemCategory.Equals(XivStrings.Face_Paint) ||
                    _item.ItemCategory.Equals(XivStrings.Equip_Decals))
                {
                    Races.Add(new ComboBoxData{Name = XivRace.All_Races.GetDisplayName(), XivRace = XivRace.All_Races});
                }
                else
                {
                    _charaRaceAndNumberDictionary = _character.GetRacesAndNumbersForTextures(item as XivCharacter);

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

                _item = item as IItemModel;
            }

            _raceCount = Races.Count;

            RaceComboboxEnabled = _raceCount > 1;

            SelectedRaceIndex = 0;
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
        private void GetParts()
        {
            if (_item != null)
            {
                // For character, the part list is used as a number list
                List<string> partList;

                if (_item.Category.Equals(XivStrings.Character))
                {
                    if (_item.ItemCategory.Equals(XivStrings.Face_Paint) ||
                        _item.ItemCategory.Equals(XivStrings.Equip_Decals))
                    {
                        partList = _character.GetDecalNums(_item).Select(part => part.ToString()).ToList();
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
                    var equipParts = _companions.GetDemiHumanMountTextureEquipPartList(_item);

                    foreach (var equipPart in equipParts)
                    {
                        Parts.Add(new ComboBoxData { Name = equipPart.Key, TypeParts = equipPart.Value });
                    }

                    _partCount = equipParts.Count;
                }
                else
                {
                    partList = _tex.GetTexturePartList(_item, SelectedRace.XivRace, _item.DataFile);

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
        private void GetTexType()
        {
            if (_item.Category.Equals(XivStrings.Character))
            {
                TypeVisibility = Visibility.Visible;
                // Create the model info for character
                _item.ModelInfo = new XivModelInfo
                {
                    Body = int.Parse(SelectedPart.Name)
                };

                // For hair and face we get the type (Hair, Accessory, Face, Iris, Etc)
                if (_item.ItemCategory.Equals(XivStrings.Hair) || _item.ItemCategory.Equals(XivStrings.Face))
                {
                    TypePartVisibility = Visibility.Visible;
                    var charaTypeParts = _character.GetTypePartForTextures(_item as XivCharacter, SelectedRace.XivRace,
                        int.Parse(SelectedPart.Name));

                    _typeCount = charaTypeParts.Count;
                    foreach (var charaTypePart in charaTypeParts)
                    {
                        Types.Add(new ComboBoxData{Name = charaTypePart.Key, TypeParts = charaTypePart.Value});
                    }
                }
                // For Body and Tail we use the type as a part list
                else if (_item.ItemCategory.Equals(XivStrings.Body) || _item.ItemCategory.Equals(XivStrings.Tail))
                {
                    var parts = _character.GetPartForTextures(_item as XivCharacter, SelectedRace.XivRace, int.Parse(SelectedPart.Name));

                    _typeCount = parts.Length;
                    foreach (var part in parts)
                    {
                        Types.Add(new ComboBoxData{Name = part.ToString()});
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
                    if (_item.ItemCategory.Equals(XivStrings.Hair) || _item.ItemCategory.Equals(XivStrings.Face))
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
        private void GetTypeParts()
        {
            var typeParts = SelectedType.TypeParts;

            if (_item.Category.Equals(XivStrings.Character))
            {
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
        private void GetMaps()
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
                    else if (_item.ItemCategory.Equals(XivStrings.Equip_Decals))
                    {
                        var path = $"{XivStrings.EquipDecalFolder}/{string.Format(XivStrings.EquipDecalFile, SelectedPart.Name)}";

                        ttpList = new List<TexTypePath> { new TexTypePath { DataFile = _item.DataFile, Path = path, Type = XivTexType.Mask } };
                    }
                    else
                    {
                        if (TypePartVisibility == Visibility.Visible)
                        {
                            _xivMtrl = _mtrl.GetMtrlData(_item, SelectedRace.XivRace, SelectedTypePart.Name[0], dxVersion);
                        }
                        else
                        {
                            _xivMtrl = _mtrl.GetMtrlData(_item, SelectedRace.XivRace, SelectedType.Name[0], dxVersion);
                        }

                        ttpList = _xivMtrl.TextureTypePathList;

                        // Removes the type if there is no texture maps for it.
                        // This specifically was added because Miqote hair 104 and 109 have an mtrl that points to non existant textures and
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
                        _xivMtrl = _mtrl.GetMtrlData(_item, SelectedRace.XivRace, SelectedType.Name[0], dxVersion);
                    }
                    else
                    {
                        _xivMtrl = _mtrl.GetMtrlData(_item, SelectedRace.XivRace, SelectedPart.Name[0], dxVersion);
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
                        var mapNamePaths = _tex.GetMapAvailableTex(_uiItem.UiPath);

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
                    else if (_uiItem.ItemCategory.Equals(XivStrings.LoadingScreen))
                    {
                        path = $"{_uiItem.UiPath}/{_uiItem.Name.ToLower()}.tex";

                        ttpList = new List<TexTypePath> { new TexTypePath { DataFile = _uiItem.DataFile, Path = path, Type = XivTexType.Diffuse } };
                    }
                    else
                    {
                        ttpList = null;
                    }

                }
                else
                {
                    var iconNum = _uiItem.IconNumber;
                    var baseNum = 0;

                    if (iconNum > 1000)
                    {
                        baseNum = (int)Math.Truncate(iconNum / 1000f) * 1000;
                    }

                    var path = $"ui/icon/{baseNum.ToString().PadLeft(6, '0')}/{iconNum.ToString().PadLeft(6, '0')}.tex";

                    ttpList = new List<TexTypePath> { new TexTypePath { DataFile = _uiItem.DataFile, Path = path, Type = XivTexType.Icon } };
                }

                PartVisibility = Visibility.Collapsed;
            }

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
        public void UpdateImage()
        {
            ImageDisplay = null;
            ChannelsEnabled = true;

            if (SelectedMap.TexType.Type != XivTexType.ColorSet)
            {
                var texData = _tex.GetTexData(SelectedMap.TexType);

                var mapBytes = _tex.GetImageData(texData);

                var pixelSettings =
                    new PixelReadSettings(texData.Width, texData.Height, StorageType.Char, PixelMapping.RGBA);

                using (var magickImage = new MagickImage(mapBytes, pixelSettings))
                {
                    _magickImage = new MagickImage(magickImage);

                    ImageEffect = new ColorChannels
                    {
                        Channel = new System.Windows.Media.Media3D.Point4D(1.0f, 1.0f, 1.0f, 0.0f)
                    };

                    SetColorChannelFilter();
                }            

                _textureView.ImageZoombox.CenterContent();
                _textureView.ImageZoombox.FitToBounds();

                PathString = texData.TextureTypeAndPath.Path;
                TextureFormat = texData.TextureFormat.GetTexDisplayName();
                TextureDimensions = $"{texData.Height} x {texData.Width}";
            }
            else
            {
                var pixelSettings =
                    new PixelReadSettings(4, 16, StorageType.Float, PixelMapping.RGBA);

                var floats = Half.ConvertToFloat(_xivMtrl.ColorSetData.ToArray());

                var floatArray = Utilities.ToByteArray(floats);

                using (var magickImage = new MagickImage(floatArray, pixelSettings))
                {
                    _magickImage = new MagickImage(magickImage);

                    ImageEffect = new ColorChannels
                    {
                        Channel = new System.Windows.Media.Media3D.Point4D(1.0f, 1.0f, 1.0f, 0.0f)
                    };

                    SetColorChannelFilter();
                }

                _textureView.ImageZoombox.CenterContent();
                _textureView.ImageZoombox.FitToBounds();

                PathString = SelectedMap.TexType.Path;
                TextureFormat = XivTexFormat.A16B16G16R16F.GetTexDisplayName();
                TextureDimensions = "4 x 16";
            }

            var modStatus = _modList.IsModEnabled(PathString, false);

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

            ExportEnabled = true;
            ImportEnabled = true;
            DDSImportEnabled = DDSFileExists();
            MoreOptionsEnabled = true;

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
        }

        /// <summary>
        /// Checks whether a DDS file exists
        /// </summary>
        /// <returns>The status of the DDS files existence</returns>
        private bool DDSFileExists()
        {
            var savePath = new DirectoryInfo(Settings.Default.Save_Directory);

            if (_item != null)
            {
                return IOUtil.DDSFileExists(_item, savePath, Path.GetFileNameWithoutExtension(SelectedMap.TexType.Path), SelectedRace.XivRace);
            }

            if (_uiItem != null)
            {
                return IOUtil.DDSFileExists(_uiItem, savePath, Path.GetFileNameWithoutExtension(SelectedMap.TexType.Path));
            }

            return false;
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
            get => _imageEffect;
            set
            {
                _imageEffect = value;
                NotifyPropertyChanged(nameof(ImageEffect));
            }
        }

        #endregion

        #region Buttons

        /// <summary>
        /// Command for the Export DDS Button
        /// </summary>
        public ICommand ExportDDS => new RelayCommand(SaveDDS);

        private void SaveDDS(object obj)
        {
            XivTex texData;
            var savePath = new DirectoryInfo(Settings.Default.Save_Directory);


            if (SelectedMap.TexType.Type == XivTexType.ColorSet)
            {
                texData = _mtrl.MtrlToXivTex(_xivMtrl);

                _mtrl.SaveColorSetExtraData(_item, _xivMtrl, savePath, SelectedRace.XivRace);
            }
            else
            {
                texData = _tex.GetTexData(SelectedMap.TexType);
            }


            if (_uiItem != null)
            {
                _tex.SaveTexAsDDS(_uiItem, texData, savePath);
            }
            else
            {
                _tex.SaveTexAsDDS(_item, texData, savePath, SelectedRace.XivRace);
            }

            DDSImportEnabled = DDSFileExists();
            _textureView.BottomFlyout.IsOpen = false;
        }


        /// <summary>
        /// Command for the Export Bmp Button
        /// </summary>
        public ICommand ExportBmp => new RelayCommand(SaveBmp);

        private void SaveBmp(object obj)
        {
            var savePath = new DirectoryInfo(Settings.Default.Save_Directory);

            var path = _uiItem != null ? IOUtil.MakeItemSavePath(_uiItem, savePath) : IOUtil.MakeItemSavePath(_item, savePath, SelectedRace.XivRace);

            Directory.CreateDirectory(path);

            _magickImage.Write($"{path}/{Path.GetFileNameWithoutExtension(SelectedMap.TexType.Path)}.bmp", MagickFormat.Bmp);
            _textureView.BottomFlyout.IsOpen = false;
        }

        /// <summary>
        /// Command for the Export All Button
        /// </summary>
        public ICommand ExportAll => new RelayCommand(SaveAll);

        private void SaveAll(object obj)
        {
            var texData = _tex.GetTexData(SelectedMap.TexType);

            var savePath = new DirectoryInfo(Settings.Default.Save_Directory);

            if (_uiItem != null)
            {
                _tex.SaveTexAsDDS(_uiItem, texData, savePath);
            }
            else
            {
                _tex.SaveTexAsDDS(_item, texData, savePath, SelectedRace.XivRace);
            }

            var path = _uiItem != null ? IOUtil.MakeItemSavePath(_uiItem, savePath) : IOUtil.MakeItemSavePath(_item, savePath, SelectedRace.XivRace);

            Directory.CreateDirectory(path);

            _magickImage.Write($"{path}/{Path.GetFileNameWithoutExtension(SelectedMap.TexType.Path)}.bmp", MagickFormat.Bmp);

            _textureView.BottomFlyout.IsOpen = false;

            DDSImportEnabled = true;
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
        /// Command for the Open Folder Button
        /// </summary>
        public ICommand ImportDDSButton => new RelayCommand(ImportDDS);

        /// <summary>
        /// Imports a DDS file
        /// </summary>
        private void ImportDDS(object obj)
        {
            var dxVersion = int.Parse(Settings.Default.DX_Version);

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

            var fullPath = new DirectoryInfo($"{path}\\{Path.GetFileNameWithoutExtension(SelectedMap.TexType.Path)}.dds");

            if (SelectedMap.TexType.Type != XivTexType.ColorSet)
            {
                var texData = _tex.GetTexData(SelectedMap.TexType);

                if (File.Exists(fullPath.FullName))
                {
                    try
                    {
                        if (_item != null)
                        {
                            _tex.TexDDSImporter(texData, _item, fullPath, XivStrings.TexTools);
                        }
                        else if (_uiItem != null)
                        {
                            _tex.TexDDSImporter(texData, _uiItem, fullPath, XivStrings.TexTools);
                        }
                    }
                    catch(Exception ex)
                    {
                        FlexibleMessageBox.Show(
                            $"There was an error Importing the Texture.\n\nError Message:\n{ex.Message}", "Error Importing Texture",
                            MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }

                }
            }
            else
            {
                var newColorSetOffset = _tex.TexColorImporter(_xivMtrl, fullPath, _item, XivStrings.TexTools);
                _xivMtrl = _mtrl.GetMtrlData(newColorSetOffset, _xivMtrl.MTRLPath, dxVersion);
            }

            _textureView.BottomFlyout.IsOpen = false;
            UpdateImage();
        }

        /// <summary>
        /// Command for the Import From Button
        /// </summary>
        public ICommand ImportFromButton => new RelayCommand(ImportFrom);

        /// <summary>
        /// Imports a texture file 
        /// </summary>
        /// <remarks>
        /// Import a texture file from any location
        /// </remarks>
        private void ImportFrom(object obj)
        {
            var saveDir = new DirectoryInfo(Settings.Default.Save_Directory);
            var path = new DirectoryInfo(IOUtil.MakeItemSavePath(_item, saveDir, SelectedRace.XivRace)); 

            var openFileDialog = new OpenFileDialog {InitialDirectory = path.FullName, Filter = "Texture Files(*.DDS;*.BMP) |*.DDS;*.BMP"};

            if (openFileDialog.ShowDialog() == DialogResult.OK)
            {
                var fileDir = new DirectoryInfo(openFileDialog.FileName);
                var dxVersion = int.Parse(Settings.Default.DX_Version);

                if (fileDir.FullName.ToLower().Contains(".dds"))
                {
                    if (SelectedMap.TexType.Type != XivTexType.ColorSet)
                    {
                        var texData = _tex.GetTexData(SelectedMap.TexType);

                        try
                        {
                            if (_item != null)
                            {
                                _tex.TexDDSImporter(texData, _item, fileDir, XivStrings.TexTools);
                            }
                            else if (_uiItem != null)
                            {
                                _tex.TexDDSImporter(texData, _uiItem, fileDir, XivStrings.TexTools);
                            }
                        }
                        catch (Exception ex)
                        {
                            FlexibleMessageBox.Show(
                                $"There was an error Importing the Texture.\n\nError Message:\n{ex.Message}", "Error Importing Texture",
                                MessageBoxButtons.OK, MessageBoxIcon.Error);
                            return;
                        }

                    }
                    else
                    {
                        var newColorSetOffset = _tex.TexColorImporter(_xivMtrl, fileDir, _item, XivStrings.TexTools);
                        _xivMtrl = _mtrl.GetMtrlData(newColorSetOffset, _xivMtrl.MTRLPath, dxVersion);
                    }
                }
                else
                {
                    //TODO: BMP Import
                }

                UpdateImage();
            }

            _textureView.BottomFlyout.IsOpen = false;
        }

        /// <summary>
        /// Command for the Mod Status Toggle Button
        /// </summary>
        public ICommand ModStatusToggleButton => new RelayCommand(ModStatusToggle);

        /// <summary>
        /// Toggles the mod ON or OFF
        /// </summary>
        private void ModStatusToggle(object obj)
        {
            var gameDirectory = new DirectoryInfo(Properties.Settings.Default.FFXIV_Directory);
            var modlist = new Modding(gameDirectory);

            if (ModToggleText.Equals("Enable"))
            {
                modlist.ToggleModStatus(SelectedMap.TexType.Path, true);
            }
            else if (ModToggleText.Equals("Disable"))
            {
                modlist.ToggleModStatus(SelectedMap.TexType.Path, false);
            }
            else
            {
                FlexibleMessageBox.Show(
                    $"There was an error attempting to toggle mod status.", "Error Toggling Mod",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            UpdateImage();
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
        /// The enabled status for the BMP import button
        /// </summary>
        public bool BMPImportEnabled
        {
            get => _bmpImportEnabled;
            set
            {
                _bmpImportEnabled = value;
                NotifyPropertyChanged(nameof(BMPImportEnabled));
            }
        }

        /// <summary>
        /// The enabled status for the DDS import button
        /// </summary>
        public bool DDSImportEnabled
        {
            get => _ddsImportEnabled;
            set
            {
                _ddsImportEnabled = value;
                NotifyPropertyChanged(nameof(DDSImportEnabled));
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
        private void TranslucencyChanged()
        {
            if (_xivMtrl == null) return;

            if (TranslucencyCheck)
            {
                if (_xivMtrl.ShaderNumber == 0x0D)
                {
                    _mtrl.ToggleTranslucency(_xivMtrl, _item, TranslucencyCheck, XivStrings.TexTools);
                }
            }
            else
            {
                if (_xivMtrl.ShaderNumber == 0x1D)
                {
                    _mtrl.ToggleTranslucency(_xivMtrl, _item, TranslucencyCheck, XivStrings.TexTools);
                }
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

            if (_magickImage != null)
            {
                _magickImage.Dispose();
            }
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

                TypeWatermark = TypePartVisibility == Visibility.Visible ? $"{XivStrings.Type}  |  {_typeCount}" : $"{XivStrings.Part}  |  {_typeCount}";

                if (TypePartVisibility == Visibility.Visible)
                {
                    TypePartWatermark = $"{XivStrings.TypePart}  |  {_typePartCount}";
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

            ImageEffect.Channel = new System.Windows.Media.Media3D.Point4D(r, g, b, a);

            _magickImage.Alpha(AlphaChecked ? AlphaOption.Activate : AlphaOption.Deactivate);

            ImageDisplay = _magickImage.ToBitmapSource();
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
    }
}