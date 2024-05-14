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
using FFXIV_TexTools.Views.Controls;
using FFXIV_TexTools.Views.Textures;
using HelixToolkit.Wpf.SharpDX;
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
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using Xceed.Wpf.Toolkit.PropertyGrid.Attributes;
using xivModdingFramework.Cache;
using xivModdingFramework.Exd.FileTypes;
using xivModdingFramework.General.Enums;
using xivModdingFramework.Helpers;
using xivModdingFramework.Items;
using xivModdingFramework.Items.Categories;
using xivModdingFramework.Items.DataContainers;
using xivModdingFramework.Items.Enums;
using xivModdingFramework.Items.Interfaces;
using xivModdingFramework.Materials.DataContainers;
using xivModdingFramework.Materials.FileTypes;
using xivModdingFramework.Models.FileTypes;
using xivModdingFramework.Mods;
using xivModdingFramework.Mods.Enums;
using xivModdingFramework.SqPack.FileTypes;
using xivModdingFramework.Textures.DataContainers;
using xivModdingFramework.Textures.Enums;
using xivModdingFramework.Textures.FileTypes;
using xivModdingFramework.Variants.DataContainers;
using xivModdingFramework.Variants.FileTypes;

using BitmapSource = System.Windows.Media.Imaging.BitmapSource;

namespace FFXIV_TexTools.ViewModels
{
    public class TextureViewModel : INotifyPropertyChanged
    {
        public class MapComboBoxEntry
        {
            public string TexturePath;
            public XivTexType Usage;

            public MapComboBoxEntry() { }
            public MapComboBoxEntry(string texturePath, XivTexType type)
            {
                TexturePath = texturePath;
                Usage = type;
            }
            public static List<MapComboBoxEntry> FromTextures(List<MtrlTexture> list, XivMtrl _mtrl = null)
            {
                if (_mtrl != null)
                {
                    // Use the smarter resolution path if we have the available data.
                    return list.Select(x => new MapComboBoxEntry(
                        x.TexturePath, 
                        _mtrl.ResolveFullUsage(x)
                        )).ToList();
                }
                else
                {
                    return list.Select(x => new MapComboBoxEntry(x.TexturePath, x.Usage)).ToList();
                }
            }

            public MtrlTexture GetTexture(XivMtrl mtrl)
            {
                return mtrl.Textures.FirstOrDefault(x => x.TexturePath == TexturePath);
            }
        }

        // The actual underlying observeables for the UI boxes.
        private ObservableCollection<KeyValuePair<string, int>> _primaryComboBoxData = new ObservableCollection<KeyValuePair<string, int>>();
        private ObservableCollection<KeyValuePair<string, string>> _materialComboBoxData = new ObservableCollection<KeyValuePair<string, string>>();
        private ObservableCollection<KeyValuePair<string, MapComboBoxEntry>> _mapComboBoxData = new ObservableCollection<KeyValuePair<string, MapComboBoxEntry>>();

        public ObservableCollection<KeyValuePair<string, int>> Races { get { return _primaryComboBoxData; } }
        public ObservableCollection<KeyValuePair<string, string>> Materials { get { return _materialComboBoxData; } }
        public ObservableCollection<KeyValuePair<string, MapComboBoxEntry>> Maps { get { return _mapComboBoxData; } }

        private string _lastCategory = null;
        private int _lastPrimary = -1;



        private int SelectedPrimary
        {
            get
            {
                var val = _textureView.RaceComboBox.SelectedValue;
                if (val == null)
                {
                    return -1;
                }
                return (int)val;
            }
            set
            {
                var idx = -1;
                for (int i = 0; i < _primaryComboBoxData.Count; i++)
                {
                    if (_primaryComboBoxData[i].Value == value)
                    {
                        idx = i;
                        break;
                    }
                }

                _textureView.RaceComboBox.SelectedIndex = idx;
            }
        }

        private XivRace SelectedRace
        {
            get {
                var val = _textureView.RaceComboBox.SelectedValue;
                if(val == null)
                {
                    return XivRace.All_Races;
                }
                return XivRaces.GetXivRace((int)val);
            }
            set
            {
                var code = value.GetRaceCodeInt();
                var idx = -1;
                for(int i = 0; i < _primaryComboBoxData.Count; i++)
                {
                    if(_primaryComboBoxData[i].Value == code)
                    {
                        idx = i;
                        break;
                    }
                }

                _textureView.RaceComboBox.SelectedIndex = idx;
            }
        }

        private string SelectedMaterial
        {
            get
            {
                var val = _textureView.MaterialComboBox.SelectedValue;
                if (val == null)
                {
                    return null;
                }
                return (string)val;
            }
            set
            {
                if (value == null)
                {
                    _textureView.MaterialComboBox.SelectedIndex = -1;
                    return;
                }

                var idx = -1;
                for (int i = 0; i < _materialComboBoxData.Count; i++)
                {
                    if (_materialComboBoxData[i].Value == value)
                    {
                        idx = i;
                        break;
                    }
                }

                _textureView.MaterialComboBox.SelectedIndex = idx;
            }
        }
        public MapComboBoxEntry SelectedMap
        {
            get
            {
                var val = _textureView.MapComboBox.SelectedValue;
                if (val == null)
                {
                    return null;
                }

                return (MapComboBoxEntry)val;
            }
            set
            {
                if(value == null)
                {
                    _textureView.MapComboBox.SelectedIndex = -1;
                    return;
                }

                var idx = -1;
                for (int i = 0; i < _mapComboBoxData.Count; i++)
                {
                    if (_mapComboBoxData[i].Value.TexturePath == value.TexturePath)
                    {
                        idx = i;
                        break;
                    }
                }

                _textureView.MapComboBox.SelectedIndex = idx;
            }
        }

        private bool _primaryIsRace = true;

        private XivMtrl _xivMtrl;
        private XivUi _uiItem;
        private BitmapSource _imageDisplay;
        private ColorChannels _imageEffect;
        private readonly TextureView _textureView;
        private MapData _mapData;

        private Dictionary<XivRace, int[]> _charaRaceAndNumberDictionary;

        private IItemModel _item;
        private XivDependencyRoot _root;

        private string _pathString, _textureFormat, _textureDimensions, _category, _mipMapInfo;
        private string _partWatermark = XivStrings.Material, _typeWatermark = XivStrings.Type, _raceWatermark = XivStrings.Race,
            _typePartWatermark = XivStrings.TypePart, _textureMapWatermark = XivStrings.Texture_Map;
        private string _modToggleText = UIStrings.Enable_Disable;

        private bool _raceEnabled, _materialEnabled, _mapEnabled, _channelsEnabled, _hiResEnabled, _hiResChecked;
        private bool _exportEnabled, _importEnabled, _modStatusEnabled, _moreOptionsEnabled, _addMaterialEnabled=false;
        private bool _materialEditorEnabled = false;
        private bool _redChecked = true, _greenChecked = true, _blueChecked = true, _alphaChecked;

        private bool _hasVfx = false;
        private string _vfxPath = null;

        public event EventHandler LoadingComplete;


        public TextureViewModel(TextureView textureView)
        {
            _textureView = textureView;
            ChannelsEnabled = false;

            _textureView.RaceComboBox.SelectionChanged += PrimaryComboBox_SelectionChanged;
            _textureView.MaterialComboBox.SelectionChanged += MaterialComboBox_SelectionChanged;
            _textureView.MapComboBox.SelectionChanged += MapComboBox_SelectionChanged;
            _textureView.ItemInfoButton.Click += ItemInfoButton_Click;

            var mw = MainWindow.GetMainWindow();
            mw.SelectedPrimaryItemValueChanged += Mw_SelectedPrimaryItemValueChanged;
            _textureView.ColorsetEditor.MaterialSaved += ColorsetEditor_MaterialSaved;

        }

        private void ColorsetEditor_MaterialSaved(object sender, EventArgs e)
        {
            UpdateImage();
        }

        /// <summary>
        /// Called when the main window fires the number changed event.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Mw_SelectedPrimaryItemValueChanged(object sender, int e)
        {
            if (e < 0) return;
            if (SelectedPrimary == e) return;
            if (_item == null) return;
            if (_tab.IsSelected) return;
            if (!_primaryComboBoxData.Any(x => x.Value == e)) return;
            SelectedPrimary = e;
        }

        private System.Windows.Controls.TabItem _tab
        {
            get
            {
                var mw = MainWindow.GetMainWindow();
                return mw.TextureTabItem;
            }
        }

        /// <summary>
        /// Updates the texture
        /// </summary>
        /// <param name="item">The item to update the texture for</param>
        public async Task UpdateTexture(IItem item)
        {
            ClearAll();

            _root = item.GetRoot();

            if(_root == null)
            {
                _primaryIsRace = false;
                await HandleTexOnlyItem(item);
                return;
            }

            HiResEnabled = false;

            _item = (IItemModel) item;
            var gameDirectory = new DirectoryInfo(Properties.Settings.Default.FFXIV_Directory);

            var _eqp = new Eqp(gameDirectory);
            var races = new List<XivRace>() { XivRace.All_Races };

            if(_root.Info.PrimaryType != XivItemType.human)
            {
                _textureView.ItemInfoButton.IsEnabled = true;
            }

            if (_root.Info.PrimaryType == XivItemType.human && (_root.Info.SecondaryId == null || _root.Info.SecondaryId == 0))
            {
                _primaryIsRace = false;
                RaceWatermark = _item.SecondaryCategory + " Number";
                // In this case, we switch the main race toggle over to be the item number.
                // Really *either* is wrong, as each race + sub-item # has its own root, but
                // that's just a really awful user experience.


                var _character = new Character(XivCache.GameInfo.GameDirectory, XivCache.GameInfo.GameLanguage);
                var numbers = await _character.GetNumbersForCharacterItem((XivCharacter) _item);

                foreach (var i in numbers)
                {
                    var name = item.SecondaryCategory + " - " + i.ToString().PadLeft(4, '0');
                    _primaryComboBoxData.Add(new KeyValuePair<string, int>(name, i));
                }

                if(numbers.Length == 0 && _root.Info.SecondaryType == XivItemType.body)
                {
                    var race = _root == null ? XivRace.All_Races : XivRaces.GetXivRace(_root.Info.PrimaryId);
                    _textureView.SharedMaterialLabel.Visibility = Visibility.Visible;
                    var skinRace = XivRaceTree.GetSkinRace(race);
                    _textureView.SharedMaterialLabel.Content = $"This race uses {skinRace.GetDisplayName()._()}'s body material(s).".L();
                }
            }
            else
            {
                RaceWatermark = XivStrings.Race;
                _primaryIsRace = true;

                races = await _eqp.GetAvailableRacialModels(item, false, true);
                if(races.Count == 0)
                {
                    // This root has the race pre-specified within it.
                    if (_root.Info.PrimaryType == XivItemType.human)
                    {
                        races = new List<XivRace>()
                        {
                            XivRaces.GetXivRace(_root.Info.PrimaryId)
                        };
                    }
                    else
                    {
                        // Just use default.
                        races = new List<XivRace>() { XivRace.All_Races };
                    }
                }

                foreach (var race in races)
                {
                    _primaryComboBoxData.Add(new KeyValuePair<string, int>(race.GetDisplayName(), race.GetRaceCodeInt()));
                }
            }


            RaceComboboxEnabled = _primaryComboBoxData.Count > 0;



            // Change selected race, triggering the selection event below this.
            if (_primaryComboBoxData.Count > 0)
            {

                var userRace = XivRaces.GetXivRaceFromDisplayName(Settings.Default.Default_Race_Selection);

                var defaultValue = _primaryComboBoxData[0].Value;

                // ONly use default race selection if selection memory is off.
                if (userRace != XivRace.All_Races && !Settings.Default.Remember_Race_Selection)
                {
                    var val = userRace.GetRaceCodeInt();
                    if (_primaryComboBoxData.Any(x => x.Value == val)) {
                        defaultValue = val;
                    }
                }

                bool tryReselect = _lastCategory == _item.PrimaryCategory;
                tryReselect = tryReselect && Settings.Default.Remember_Race_Selection;
                _lastCategory = _item.PrimaryCategory;
                
                if (tryReselect)
                {
                    if(_primaryComboBoxData.Any(x => x.Value == _lastPrimary))
                    {
                        SelectedPrimary = _lastPrimary;
                    } else
                    {
                        SelectedPrimary = defaultValue;
                    }
                }
                else
                {
                    SelectedPrimary = defaultValue;
                }
            }
            else
            {
                MaterialComboBoxEnabled = false;
                MapComboBoxEnabled = false;
                OnLoadingComplete();
            }
        }

        private async void PrimaryComboBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            _materialComboBoxData.Clear();
            _mapComboBoxData.Clear();
            _textureView.SharedVariantLabel.Visibility = Visibility.Collapsed;
            _textureView.SharedTextureLabel.Visibility = Visibility.Collapsed;
            _textureView.SharedMaterialLabel.Visibility = Visibility.Collapsed;
            var tx = MainWindow.DefaultTransaction;

            if (_item == null)
            {
                _xivMtrl = null;
                RaceComboboxEnabled = false;
                MaterialComboBoxEnabled = false;
                MapComboBoxEnabled = false;
                OnLoadingComplete();
                return;
            }

            if (SelectedPrimary == -1)
            {
                _xivMtrl = null;
                OnLoadingComplete();
                return;
            }

            _lastPrimary = SelectedPrimary;
            var mw = MainWindow.GetMainWindow();
            mw.SelectedPrimaryItemValue = SelectedPrimary;

            // Need to get materials for the current race.
            var race = SelectedRace;

            if (!_primaryIsRace)
            {
                // For these items, the main selection is technically their secondary type id
                // ex. face number, body number.
                var info = new XivDependencyRootInfo()
                {
                    PrimaryId = _root.Info.PrimaryId,
                    PrimaryType = _root.Info.PrimaryType,
                    SecondaryId = SelectedPrimary,
                    SecondaryType = _root.Info.SecondaryType,
                    Slot = _root.Info.Slot
                };
                race = XivRaces.GetXivRace(info.PrimaryId);
                _root = info.ToFullRoot();
            }
            // First of all, need to get the material set number.
            var materialSet = await Mtrl.GetMaterialSetId(_item);

            // Get all models in this root.
            List<string> materials = new List<string>();


            var sharedRace = XivRace.All_Races;
            // Kick this into a new thread since it can take a bit.
            await Task.Run(async () =>
            {
                try
                {
                    materials = await _root.GetMaterialFiles(materialSet, tx);
                }
                catch(Exception ex)
                {
                    materials = new List<string>();
                }

                if (_root.Info.PrimaryType == XivItemType.equipment || _root.Info.PrimaryType == XivItemType.weapon)
                {
                    // Need to test to see if they have VFX files too.
                    var vfxPath = await ATex.GetVfxPath(_item);
                    _vfxPath = vfxPath.Folder + "/" + vfxPath.File;
                    _hasVfx = await tx.FileExists(_vfxPath);
                }

            });


            var finalList = new SortedSet<string>();
            var rex = new Regex("\\/[^/]+c([0-9]{4})[^/]+\\.mtrl$");
            foreach (var material in materials)
            {

                // Safety check.  If any materials are referenced in the item which do not exist in the Racial menu, add the race to the menu.
                var match = rex.Match(material);
                if (match.Success)
                {
                    var matCode = match.Groups[1].Value;
                    var matRace = XivRaces.GetXivRace(matCode);
                    if (_primaryIsRace && !_primaryComboBoxData.Any(x => x.Value == matRace.GetRaceCodeInt()))
                    {
                        _primaryComboBoxData.Add(new KeyValuePair<string, int>(matRace.GetDisplayName(), matRace.GetRaceCodeInt()));
                    }
                }

                // If we have a specific race, narrow things down more.
                if (race != XivRace.All_Races)
                {
                    if (material.Contains("c" + race.GetRaceCode()))
                    {
                        finalList.Add(material);
                    }
                } else
                {
                    finalList.Add(material);
                }
            }

            var _tex = new Tex(XivCache.GameInfo.GameDirectory);
            var icons = await _tex.GetItemIcons(_item, MainWindow.UserTransaction);
            if (icons.Count > 0)
            {
                finalList.Add("icons.ui");
            }


            sharedRace = await GetMaterialSharedRace(race);
            if (sharedRace != XivRace.All_Races && sharedRace != race)
            {
                _textureView.SharedMaterialLabel.Content = $"This race's model uses {sharedRace.GetDisplayName()._()}'s material(s).".L();
                _textureView.SharedMaterialLabel.Visibility = Visibility.Visible;
                MoreOptionsEnabled = true;
                AddMaterialEnabled = true;
                MaterialEditorEnabled = false;
            }
            else
            {
                _textureView.SharedMaterialLabel.Content = "";
                _textureView.SharedMaterialLabel.Visibility = Visibility.Collapsed;
            }

            if (_hasVfx)
            {
                finalList.Add(_vfxPath);
            }

            var mSetExtract = new Regex("v([0-9]{4})/");
            bool showMsets = false;
            if (finalList.Count > 0)
            {
                var mSet = -1;

                // Get the first material set
                finalList.FirstOrDefault(x =>
                {
                    var match = mSetExtract.Match(x);
                    if(match.Success)
                    {
                        mSet = Int32.Parse(match.Groups[1].Value);
                        return true;
                    }
                    return false;
                });

                // See if any of the material sets don't match.  If they don't, we need to show the mset values.
                showMsets = finalList.Any(x =>
                {
                    var match = mSetExtract.Match(x);
                    if (match.Success)
                    {
                        var set = Int32.Parse(match.Groups[1].Value);
                        if (set != mSet)
                        {
                            return true;
                        } 
                    }
                    return false;
                });

            }

            foreach(var material in finalList)
            {
                
                var mName = Path.GetFileNameWithoutExtension(material);
                var ext = Path.GetExtension(material);
                if (ext == ".avfx")
                {

                    _materialComboBoxData.Add(new KeyValuePair<string, string>("VFX: ".L() + mName, material));
                } else if(ext == ".ui")
                {
                    _materialComboBoxData.Add(new KeyValuePair<string, string>("UI Elements".L(), material));
                }
                else if(ext == ".mtrl")
                {
                    var displayedSuffix = mName;
                    var mSetString = "";
                    if (showMsets)
                    {
                        var match = mSetExtract.Match(material);
                        if (match.Success)
                        {
                            var mSet = Int32.Parse(match.Groups[1].Value);
                            mSetString = "v" + mSet.ToString().PadLeft(4, '0') + "/";
                        }

                    }

                    if (mName.IndexOf('_') != -1)
                    {
                        var idx = mName.LastIndexOf('_') + 1;
                        var rem = mName.Length - idx;
                        if (rem > 0 && idx > 1)
                        {
                            var materialIdentifier = displayedSuffix.Substring(idx, rem);

                            // If this doesn't actually have any suffix, just show the slot.
                            if (materialIdentifier == _root.Info.Slot)
                            {
                                displayedSuffix = _root.Info.Slot;
                            } else
                            {
                                var rest = mName.Substring(0, idx-1);

                                // Keep walking back through to find the slot.
                                if (rest.IndexOf('_') != -1)
                                {
                                    idx = rest.LastIndexOf('_') + 1;
                                    rem = rest.Length - idx;
                                    if (rem > 0)
                                    {
                                        var slot = displayedSuffix.Substring(idx, rem);
                                        if (slot.Length == 3)
                                        {
                                            displayedSuffix = slot + " " + materialIdentifier;
                                        } else
                                        {
                                            displayedSuffix = materialIdentifier;
                                        }
                                    } else
                                    {
                                        displayedSuffix = materialIdentifier;
                                    }
                                } else
                                {
                                    displayedSuffix = materialIdentifier;
                                }
                            }
                        }
                    }

                    if (displayedSuffix != mName)
                    {
                        displayedSuffix = "Material: ".L() + displayedSuffix.ToUpper() + " - " + mSetString + mName;
                    }


                    _materialComboBoxData.Add(new KeyValuePair<string, string>(displayedSuffix, material));
                }
            }

            MaterialComboBoxEnabled = _materialComboBoxData.Count > 0;

            // Change selected material, triggering the selection event below this.
            if (finalList.Count > 0)
            {
                SelectedMaterial = finalList.First();
            }
            else
            {
                MapComboBoxEnabled = false;
                OnLoadingComplete();
            }
        }

        private async void MaterialComboBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            _mapComboBoxData.Clear();
            if (SelectedMaterial == null)
            {
                _xivMtrl = null;
                return;
            }
            var ext = Path.GetExtension(SelectedMaterial);

            var items = 0;
            if (ext == ".avfx")
            {
                var _atex = new ATex(XivCache.GameInfo.GameDirectory);
                var paths = await _atex.GetAtexPaths(_item, false, MainWindow.UserTransaction);

                foreach (var path in paths)
                {
                    var mi = new MapComboBoxEntry();
                    mi.TexturePath = path.Path;
                    mi.Usage = XivTexType.Vfx;
                    _mapComboBoxData.Add(new KeyValuePair<string, MapComboBoxEntry>("VFX - ".L() + Path.GetFileNameWithoutExtension(mi.TexturePath), mi));
                    items++;
                }

            }
            else if (ext == ".ui")
            {
                var _tex = new Tex(XivCache.GameInfo.GameDirectory);
                var icons = await _tex.GetItemIcons(_item, MainWindow.UserTransaction);

                foreach (var ttp in icons)
                {
                    var mi = new MapComboBoxEntry();
                    mi.TexturePath = ttp.Path;
                    mi.Usage = XivTexType.UI;
                    _mapComboBoxData.Add(new KeyValuePair<string, MapComboBoxEntry>(ttp.Name.L(), mi));
                    items++;
                }
            }
            else if (ext == ".mtrl")
            {
                var _mtrl = new Mtrl(XivCache.GameInfo.GameDirectory);
                try
                {
                    // Material doesn't exist for this material set.
                    var exists = await MainWindow.DefaultTransaction.FileExists(SelectedMaterial);
                    if(!exists)
                    {
                        OnLoadingComplete();
                        return;
                    }

                    _xivMtrl = await _mtrl.GetXivMtrl(SelectedMaterial);

                    if(_xivMtrl == null)
                    {
                        OnLoadingComplete();
                        return;
                    }
                }
                catch (Exception ex)
                {
                    FlexibleMessageBox.Show("An error occured while loading the material:\n".L() + ex.Message, "Item Load Error".L(), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    OnLoadingComplete();
                    return;
                }
                var maps = MapComboBoxEntry.FromTextures(_xivMtrl.Textures, _xivMtrl);
                if (_xivMtrl.ColorSetDataSize > 0)
                {
                    var cSetMap = new MapComboBoxEntry();
                    cSetMap.TexturePath = SelectedMaterial;
                    cSetMap.Usage = XivTexType.ColorSet;
                    maps.Add(cSetMap);
                }

                foreach (var map in maps)
                {
                    _mapComboBoxData.Add(new KeyValuePair<string, MapComboBoxEntry>(map.Usage.ToString().L() + " - " + Path.GetFileNameWithoutExtension(map.TexturePath), map));
                }


                items = maps.Count;
            }

            MapComboBoxEnabled = items > 0;

            // Set the map, triggering the changed event below.
            if (items > 0)
            {
                SelectedMap = _mapComboBoxData[0].Value;
            }
            else
            {
                OnLoadingComplete();
            }
        }

        private void MapComboBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            ClearImage();
            if(SelectedMap == null)
            {
                return;
            }

            UpdateImage();
        }

        /// <summary>
        /// Handle resolution for items that don't have models/materials.
        /// </summary>
        /// <returns></returns>
        private async Task HandleTexOnlyItem(IItem item)
        {
            if (item == null)
            {
                OnLoadingComplete();
                return;
            }

            try 
            {
                var uiItem = (XivUi)item;
                _uiItem = uiItem;

                var paths = await uiItem.GetTexPaths(!HiResChecked, HiResChecked);
                foreach(var kv in paths)
                {
                    var mapInfo = new MapComboBoxEntry();


                    mapInfo.TexturePath = kv.Value;
                    mapInfo.Usage = XivTexType.UI;

                    _mapComboBoxData.Add(new KeyValuePair<string, MapComboBoxEntry>(kv.Key, mapInfo));

                }

                HiResEnabled = uiItem.HasHiRes;
            } catch
            {
                try
                {
                    _item = (IItemModel)item;
                } catch
                {

                }
                 
                if (item.SecondaryCategory == XivStrings.Paintings)
                {
                    // Okay, try and see if it's a gimpy furniture painting thing then.
                    var paintingItem = (XivFurniture)item;
                    _item = paintingItem;

                    // There has got to be a function somewhere that does this already, but I couldn't find it.
                    var mapInfo = new MapComboBoxEntry();
                    var block = paintingItem.ModelInfo.PrimaryID - (paintingItem.ModelInfo.PrimaryID % 1000);
                    mapInfo.TexturePath = "ui/icon/" + block.ToString().PadLeft(6, '0') + "/" + paintingItem.ModelInfo.PrimaryID.ToString().PadLeft(6, '0') + ".tex";
                    mapInfo.Usage = XivTexType.UI;

                    _mapComboBoxData.Add(new KeyValuePair<string, MapComboBoxEntry>(item.Name, mapInfo));
                    SelectedMap = mapInfo;
                }
                else if (item.SecondaryCategory == XivStrings.Face_Paint)
                {
                    var _character = new Character(XivCache.GameInfo.GameDirectory, XivCache.GameInfo.GameLanguage);

                    var paths = await _character.GetDecalPaths(Character.XivDecalType.FacePaint);

                    foreach (var path in paths)
                    {
                        var mapInfo = new MapComboBoxEntry();


                        mapInfo.TexturePath = path;
                        mapInfo.Usage = XivTexType.UI;

                        _mapComboBoxData.Add(new KeyValuePair<string, MapComboBoxEntry>(Path.GetFileNameWithoutExtension(path), mapInfo));

                    }

                }
                else if (item.SecondaryCategory == XivStrings.Equipment_Decals)
                {
                    var _character = new Character(XivCache.GameInfo.GameDirectory, XivCache.GameInfo.GameLanguage);
                    var paths = await _character.GetDecalPaths(Character.XivDecalType.Equipment);
                    foreach (var path in paths)
                    {
                        var mapInfo = new MapComboBoxEntry();

                        mapInfo.TexturePath = path;
                        mapInfo.Usage = XivTexType.UI;

                        _mapComboBoxData.Add(new KeyValuePair<string, MapComboBoxEntry>(Path.GetFileNameWithoutExtension(path), mapInfo));
                    }
                }

                HiResEnabled = false;
            }

            if (_mapComboBoxData.Count > 0)
            {
                MapComboBoxEnabled = true;
                SelectedMap = _mapComboBoxData[0].Value;
            }
            else
            {
                MapComboBoxEnabled = false;
            }

            OnLoadingComplete();

        }


        /// <summary>
        /// The enabled status for the race combobox
        /// </summary>
        public bool MaterialComboBoxEnabled
        {
            get => _materialEnabled;
            set { _materialEnabled = value; NotifyPropertyChanged(nameof(MaterialComboBoxEnabled)); }
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
        /// The enabled status for the race combobox
        /// </summary>
        public bool MapComboBoxEnabled
        {
            get => _mapEnabled;
            set { _mapEnabled = value; NotifyPropertyChanged(nameof(MapComboBoxEnabled)); }
        }

        /// <summary>
        /// Gets the texture maps for the given item
        /// </summary>
        private async void GetMaps()
        {
            //_mapCount = 0;


            _materialEditorEnabled = _xivMtrl != null;
        }
        public void ClearImage()
        {
            PathString = "No Texture Selected".L();
            TextureFormat = "N/A";
            TextureDimensions = "0x0";
            MipMapInfo = "No";

            ImageDisplay = null;
            ChannelsEnabled = false;
            ExportEnabled = false;
            ImportEnabled = false;
            MoreOptionsEnabled = false;
            AddMaterialEnabled = false;
            _mapData = new MapData()
            {
                MapBytes = new byte[0],
                Height = 0,
                Width = 0
            };

            _imageEffect = new ColorChannels
            {
                Channel = new System.Windows.Media.Media3D.Point4D(1.0f, 1.0f, 1.0f, 0.0f)
            };
        }

        /// <summary>
        /// Asynchronously loads the parent file information for a given texture.
        /// </summary>
        /// <returns></returns>
        private async Task LoadParentFileInformation(string path)
        {

            var item = _item;
            if (item == null)
            {
                _textureView.SharedVariantLabel.Visibility = Visibility.Collapsed;
                _textureView.SharedTextureLabel.Visibility = Visibility.Collapsed;
                return;
            }
            try
            {

                var root = item.GetRoot();
                if (root == null || root.Info.PrimaryType == XivItemType.human || root.Info.PrimaryType == XivItemType.outdoor || root.Info.PrimaryType == XivItemType.indoor || !Imc.UsesImc(item))
                {
                    _textureView.SharedVariantLabel.Visibility = Visibility.Collapsed;
                    _textureView.SharedTextureLabel.Visibility = Visibility.Collapsed;
                    return;
                }



                _textureView.SharedVariantLabel.Visibility = Visibility.Visible;
                _textureView.SharedTextureLabel.Visibility = Visibility.Collapsed;
                _textureView.SharedVariantLabel.Content = "Loading usage data...".L();

                List<string> parents = new List<string>();
                List<XivImc> entries = null;
                await Task.Run(async () =>
                {
                    var _imc = new Imc(XivCache.GameInfo.GameDirectory);
                    var info = (await _imc.GetFullImcInfo(item));
                    entries = info.GetAllEntries(item.GetItemSlotAbbreviation(), true);

                    if (Path.GetExtension(path) == ".mtrl")
                    {
                        parents = new List<string>() { path };
                    }
                    else
                    {
                        parents = await XivCache.GetParentFiles(path);
                    }
                });

                // Invalid IMC set, cancel.
                if (item.ModelInfo.ImcSubsetID > entries.Count)
                {
                    if (SelectedMap != null && SelectedMap.TexturePath == path)
                    {
                        _textureView.SharedVariantLabel.Visibility = Visibility.Collapsed;
                        _textureView.SharedTextureLabel.Visibility = Visibility.Collapsed;
                    }
                    return;
                }

                var vCount = entries.Count;
                Dictionary<int, int> variantsPerMset = new Dictionary<int, int>();
                foreach (var e in entries)
                {
                    if (!variantsPerMset.ContainsKey(e.MaterialSet))
                    {
                        variantsPerMset[e.MaterialSet] = 0;
                    }
                    variantsPerMset[e.MaterialSet]++;
                }

                if (variantsPerMset.ContainsKey(0))
                {
                    // Material set 0 is the null set.
                    vCount -= variantsPerMset[0];
                }

                if (parents == null || parents.Count == 0)
                {
                    if (SelectedMap != null && SelectedMap.TexturePath == path)
                    {
                        _textureView.SharedVariantLabel.Content = "";
                        _textureView.SharedVariantLabel.Visibility = Visibility.Collapsed;
                    }
                    return;
                }

                var mymSet = entries[item.ModelInfo.ImcSubsetID].MaterialSet;

                // Check if we're just used in some amount of variants of the same material.
                var firstName = Path.GetFileName(parents[0]);
                var sameMaterials = parents.Where(x => Path.GetFileName(x) == firstName);

                var mSetExctraction = new Regex("v([0-9]{4})");
                List<int> representedMaterialSets = new List<int>();
                foreach (var x in sameMaterials)
                {
                    var match = mSetExctraction.Match(x);
                    if (!match.Success) continue;

                    representedMaterialSets.Add(Int32.Parse(match.Groups[1].Value));
                }

                var variantSum = 0;
                foreach (var i in representedMaterialSets)
                {
                    if (!variantsPerMset.ContainsKey(i))
                    {
                        variantSum++;
                    }
                    else
                    {
                        variantSum += variantsPerMset[i];
                    }
                }

                if (SelectedMap != null && SelectedMap.TexturePath == path)
                {
                    _textureView.SharedVariantLabel.Content = $"Used by {variantSum._()}/{vCount._()} Variants".L();
                    _textureView.SharedVariantLabel.Visibility = Visibility.Visible;
                }

                var allSame = sameMaterials.Count() == parents.Count;
                if (!allSame)
                {
                    var differentFiles = parents.Select(x => Path.GetFileName(x)).ToHashSet();
                    var count = differentFiles.Count - 1;
                    if (SelectedMap != null && SelectedMap.TexturePath == path)
                    {
                        _textureView.SharedTextureLabel.Content = $"Used by {count._()} Other Materials".L();
                        _textureView.SharedTextureLabel.Visibility = Visibility.Visible;
                    }
                }
            } catch (Exception ex)
            {
                // No-op.  Lacking this data is not a critical failure.
            }
        }

        /// <summary>
        /// Updates the texture image for the selected item
        /// </summary>
        public async void UpdateImage()
        {
            ImageDisplay = null;
            ChannelsEnabled = true;

            _textureView.ColorsetEditor.Visibility = Visibility.Collapsed;
            _textureView.StandardTextureDisplay.Visibility = Visibility.Visible;

            if (SelectedMap == null || SelectedMap.TexturePath == null)
            {
                return;
            }

            var _mtrl = new Mtrl(XivCache.GameInfo.GameDirectory);
            var _tex = new Tex(XivCache.GameInfo.GameDirectory);

            // This is intentionally an async/deferred call here.
            LoadParentFileInformation(SelectedMap.TexturePath);


            try
            {
                if (SelectedMap.Usage != XivTexType.ColorSet)
                {
                    var texData = await _tex.GetXivTex(SelectedMap.TexturePath, SelectedMap.Usage);

                    var mapBytes = await texData.GetRawPixels();

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
                    // Colorset entry.
                    PathString = SelectedMap.TexturePath;
                    _xivMtrl = await _mtrl.GetXivMtrl(SelectedMap.TexturePath, _item);
                    await _textureView.ColorsetEditor.SetMaterial(_xivMtrl);

                    _textureView.ColorsetEditor.Visibility = Visibility.Visible;
                    _textureView.StandardTextureDisplay.Visibility = Visibility.Collapsed;
                }

                var tx = MainWindow.DefaultTransaction;
                var mod = await tx.GetMod(PathString);
                var state = await Modding.GetModState(PathString, tx);

                if (mod != null && state == EModState.Disabled)
                {
                    ModStatusToggleEnabled = true;
                    ModToggleText = UIStrings.Enable;

                }
                else if (mod != null)
                {
                    if (mod.Value.IsCustomFile())
                    {
                        // This is a file addition material or texture.
                        // Don't let them disable it via this menu, because it'll blow the UI the fuck up.
                        ModStatusToggleEnabled = false;
                    }
                    else
                    {
                        ModStatusToggleEnabled = true;
                    }

                    ModToggleText = UIStrings.Disable;
                }
                else
                {

                    ModStatusToggleEnabled = false;
                    ModToggleText = UIStrings.Enable;
                }

                ExportEnabled = true;
                ImportEnabled = true;
                MoreOptionsEnabled = true;

                if (_item != null)
                {
                    if (_item.SecondaryCategory.Equals(XivStrings.Equipment_Decals) || _item.SecondaryCategory.Equals(XivStrings.Face_Paint))
                    {
                        AddMaterialEnabled = false;
                        MaterialEditorEnabled = false;
                    }
                    else
                    {
                        MaterialEditorEnabled = true;
                        AddMaterialEnabled = true;
                    }
                }
                else
                {
                    MaterialEditorEnabled = false;
                    AddMaterialEnabled = false;
                }
            } catch (Exception ex)
            {
                FlexibleMessageBox.Show("Unable to load texture file:\n\nError:".L() + ex.Message, "Texture File Error".L(), MessageBoxButtons.OK, MessageBoxIcon.Warning);
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
                var df = IOUtil.GetDataFileFromPath(SelectedMap.TexturePath);
                var _mtrl = new Mtrl(XivCache.GameInfo.GameDirectory);
                var _tex = new Tex(XivCache.GameInfo.GameDirectory);

                var ttp = new TexTypePath
                {
                    Path = SelectedMap.TexturePath,
                    Type = SelectedMap.Usage,
                    DataFile = df
                };

                if (SelectedMap.Usage== XivTexType.ColorSet)
                {

                    texData = await _mtrl.GetColorsetXivTex(_xivMtrl);
                    if (_primaryIsRace)
                    {
                        _mtrl.SaveColorsetDyeData(_item, _xivMtrl, savePath, SelectedRace);
                    }
                    else
                    {
                        var saveItem = (XivCharacter)((XivCharacter)_item).Clone();
                        saveItem.ModelInfo.SecondaryID = SelectedPrimary;
                        saveItem.Name = saveItem.SecondaryCategory;
                        var race = _root == null ? XivRace.All_Races : XivRaces.GetXivRace(_root.Info.PrimaryId);
                        _mtrl.SaveColorsetDyeData(saveItem, _xivMtrl, savePath, race);
                    }
                }
                else
                {
                    texData = await _tex.GetXivTex(ttp.Path, ttp.Type);
                }

                if (_uiItem != null)
                {
                    _tex.SaveTexAsDDS(_uiItem, texData, savePath);
                }
                else
                {
                    if (_primaryIsRace)
                    {
                        _tex.SaveTexAsDDS(_item, texData, savePath, SelectedRace);
                    } else if( typeof(XivFurniture) == _item.GetType())
                    {
                        // Fucking Paintings are cancer.
                        _tex.SaveTexAsDDS(_item, texData, savePath, SelectedRace);
                    } else
                    {
                        var saveItem = (XivCharacter)((XivCharacter)_item).Clone();
                        saveItem.Name = saveItem.SecondaryCategory;
                        saveItem.ModelInfo.SecondaryID = SelectedPrimary;
                        var race = _root == null ? XivRace.All_Races : XivRaces.GetXivRace(_root.Info.PrimaryId);
                        _tex.SaveTexAsDDS(saveItem, texData, savePath, race);
                    }
                }
            }
            else
            {
                if(SelectedMap.Usage == XivTexType.ColorSet)
                {
                    return;
                }

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
                    throw new Exception($"Texture format not supported: {format._()}".L());
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
                if (((!_primaryIsRace) && typeof(XivFurniture) != _item.GetType()))
                {
                    var race = _root == null ? XivRace.All_Races : XivRaces.GetXivRace(_root.Info.PrimaryId);
                    path = IOUtil.MakeItemSavePath(_item, savePath, race, SelectedPrimary);
                } else
                {
                    path = IOUtil.MakeItemSavePath(_item, savePath, SelectedRace);
                }
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
        /// Command for the Details button
        /// </summary>
        public ICommand OpenFileDetails => new RelayCommand(ShowFileDetails);

        /// <summary>
        /// Opens the dependency dialog.
        /// </summary>
        private void ShowFileDetails(object obj)
        {
            if (SelectedMap != null && SelectedMap != null) 
            { 
                var path = SelectedMap.TexturePath;
                var view = new DependencyInfoView(path);
                view.ShowDialog();
            }
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

            var openFileDialog = new OpenFileDialog { InitialDirectory = path.FullName, Filter = "Texture Files(*.DDS;*.BMP;*.PNG) |*.DDS;*.BMP;*.PNG".L() };

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

            DirectoryInfo fullPath = new DirectoryInfo($"{path}\\{Path.GetFileNameWithoutExtension(SelectedMap.TexturePath)}.{extension}");
            return fullPath;
        }

        private DirectoryInfo GetDefaultPath()
        {


            DirectoryInfo savePath = new DirectoryInfo(Settings.Default.Save_Directory);
            DirectoryInfo path;

            if (_item != null)
            {
                // Did I mention I hate our paintings implementation?
                if ((!_primaryIsRace) && typeof(XivFurniture) != _item.GetType())
                {
                    var race = _root == null ? XivRace.All_Races : XivRaces.GetXivRace(_root.Info.PrimaryId);
                    var saveItem = (XivCharacter)((XivCharacter)_item).Clone();
                    saveItem.Name = saveItem.SecondaryCategory;
                    saveItem.ModelInfo.SecondaryID = SelectedPrimary;

                    path = new DirectoryInfo(IOUtil.MakeItemSavePath(saveItem, savePath, race, SelectedPrimary));
                }
                else
                {
                    path = new DirectoryInfo(IOUtil.MakeItemSavePath(_item, savePath, SelectedRace));
                }
            }
            else if (_uiItem != null)
            {
                path = new DirectoryInfo(IOUtil.MakeItemSavePath(_uiItem, savePath));
            }
            else
            {
                throw new Exception("Unsupported item type".L());
            }

            return path;
        }

        public async Task Import(string fileName)
        {
            var _mtrl = new Mtrl(XivCache.GameInfo.GameDirectory);
            var _tex = new Tex(XivCache.GameInfo.GameDirectory);

            ImportEnabled = false;
            var fileDir = new DirectoryInfo(fileName);
            var dxVersion = int.Parse(Settings.Default.DX_Version);


            if (fileDir.FullName.ToLower().Contains(".dds"))
            {
                if (SelectedMap.Usage != XivTexType.ColorSet)
                {
                    
                    var texData = await _tex.GetXivTex(SelectedMap.TexturePath, SelectedMap.Usage);

                    try
                    {
                        if (_item != null)
                        {
                            var saveItem = _item;

                            if ((!_primaryIsRace) && typeof(XivFurniture) != _item.GetType())
                            {
                                var temp = (XivCharacter)((XivCharacter)_item).Clone();
                                temp.Name = saveItem.SecondaryCategory;
                                temp.ModelInfo.SecondaryID = SelectedPrimary;
                                saveItem = temp;
                            }

                            await _tex.ImportTex(texData.TextureTypeAndPath.Path, fileDir.FullName, saveItem, XivStrings.TexTools);
                        }
                        else if (_uiItem != null)
                        {
                            await _tex.ImportTex(texData.TextureTypeAndPath.Path, fileDir.FullName, _uiItem, XivStrings.TexTools);
                        }
                    }
                    catch (Exception ex)
                    {
                        FlexibleMessageBox.Show(
                            string.Format(UIMessages.TextureImportErrorMessage, ex.Message), UIMessages.TextureImportErrorTitle,
                            MessageBoxButtons.OK, MessageBoxIcon.Error);
                        ImportEnabled = true;
                        return;
                    }
                }
                else
                {
                    try
                    {
                        var newColorSetOffset = await _tex.ImportColorsetTexture(_xivMtrl, fileDir.FullName, _item, XivStrings.TexTools);
                        _xivMtrl = await _mtrl.GetXivMtrl(_xivMtrl.MTRLPath, false, MainWindow.DefaultTransaction);
                    }
                    catch (Exception ex)
                    {
                        FlexibleMessageBox.Show(
                            string.Format(UIMessages.TextureImportErrorMessage, ex.Message), UIMessages.TextureImportErrorTitle,
                            MessageBoxButtons.OK, MessageBoxIcon.Error);
                        ImportEnabled = true;
                        return;
                    }                        
                }
            }
            else
            {
                if (SelectedMap.Usage != XivTexType.ColorSet)
                {
                    var texData = await _tex.GetXivTex(SelectedMap.TexturePath, SelectedMap.Usage);

                    try
                    {
                        if (_item != null)
                        {
                            var saveItem = _item;

                            if ((!_primaryIsRace) && typeof(XivFurniture) != _item.GetType())
                            {
                                var temp = (XivCharacter)((XivCharacter)_item).Clone();
                                temp.Name = saveItem.SecondaryCategory;
                                temp.ModelInfo.SecondaryID = SelectedPrimary;
                                saveItem = temp;
                            }

                            await _tex.ImportTex(texData.TextureTypeAndPath.Path, fileDir.FullName, _item, XivStrings.TexTools);
                        }
                        else if (_uiItem != null)
                        {
                            await _tex.ImportTex(texData.TextureTypeAndPath.Path, fileDir.FullName, _uiItem, XivStrings.TexTools);
                        }
                    }
                    catch (Exception ex)
                    {
                        FlexibleMessageBox.Show(
                            string.Format(UIMessages.TextureImportErrorMessage, ex.Message), UIMessages.TextureImportErrorTitle,
                            MessageBoxButtons.OK, MessageBoxIcon.Error);
                        ImportEnabled = true;
                        return;
                    }
                }
                else
                {
                    FlexibleMessageBox.Show(
                        UIMessages.ColorSetBMPNotSupportedMessage, UIMessages.TextureImportErrorTitle,
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                    ImportEnabled = true;
                    return;
                }
            }

            UpdateImage();

        }

        private void ItemInfoButton_Click(object sender, RoutedEventArgs e)
        {
            if (_item == null) return;
            var wind = new ItemInfoDisplay(_item);
            wind.Show();
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

            try
            {
                if (ModToggleText.Equals(UIStrings.Enable))
                {
                    await Modding.ToggleModStatus(SelectedMap.TexturePath, true);
                }
                else if (ModToggleText.Equals(UIStrings.Disable))
                {
                    await Modding.ToggleModStatus(SelectedMap.TexturePath, false);
                }
            }
            catch (Exception ex)
            {
                FlexibleMessageBox.Show(
                    string.Format(UIMessages.ModToggleErrorMessage, ex.Message), UIMessages.ModToggleErrorTitle,
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            UpdateImage();
        }

        private async Task<XivRace> GetMaterialSharedRace(XivRace race)
        {
            var raceCode = race.GetRaceCode();
            //SharedMaterialLabel
            var racialModels = (await _root.GetModelFiles()).Where(x => x.Contains("c" + raceCode)).ToList();
            if (racialModels.Count > 0)
            {
                // This item has no materials, but it *does* have a racial model, which has materials...
                var mats = await XivCache.GetChildFiles(racialModels[0]);

                if (mats.Count > 0)
                {
                    // Scan for a used race code that's not a skin material
                    var regex = new Regex("c([0-9]{4})[^b]");
                    foreach (var mat in mats)
                    {
                        var match = regex.Match(mat);
                        if (match.Success)
                        {
                            var foundRace = XivRaces.GetXivRace(match.Groups[1].Value);
                            if (race != foundRace)
                            {
                                return foundRace;
                            }
                        }
                    }
                }
            }

            return XivRace.All_Races;
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
            await OpenMaterialEditor(_xivMtrl, _item, MaterialEditorMode.NewMulti);

        }


        /// <summary>
        /// Command for the Material Editor Button
        /// </summary>
        public ICommand OpenMaterialEditorButton => new RelayCommand(OpenMaterialEditor);
        private async void OpenMaterialEditor(object obj)
        {
            await OpenMaterialEditor(_xivMtrl, _item, MaterialEditorMode.EditSingle);
        }


        /// <summary>
        /// Command for the Shaderd Material Editor Button
        /// </summary>
        public ICommand OpenSharedMaterialEditorButton => new RelayCommand(OpenSharedMaterialEditor);
        private async void OpenSharedMaterialEditor(object obj)
        {
            await OpenMaterialEditor(_xivMtrl, _item, MaterialEditorMode.EditMulti);
        }

        /// <summary>
        /// Opens the Material Editor with the given material/item in the given mode.
        /// </summary>
        /// <param name="material"></param>
        /// <param name="item"></param>
        /// <param name="mode"></param>
        /// <returns></returns>
        public async Task OpenMaterialEditor(XivMtrl material, IItemModel item, MaterialEditorMode mode = MaterialEditorMode.EditSingle)
        {
            try
            {

                var race = SelectedRace;
                var sharedRace = await GetMaterialSharedRace(race);

                if(material == null && sharedRace != race && sharedRace != XivRace.All_Races && race != XivRace.All_Races)
                {
                    // Need to get the shared race's material, then replace the race code.
                    var original = "c" + sharedRace.GetRaceCode();
                    var target = "c" + race.GetRaceCode();

                    var materialSet = await Mtrl.GetMaterialSetId(_item);
                    var materials = await _root.GetMaterialFiles(materialSet);

                    string matPath = null;
                    foreach(var mat in materials)
                    {
                        if(mat.Contains(original))
                        {
                            matPath = mat;
                            break;
                        }
                    }

                    if (matPath == null) return;
                    var _mtrl = new Mtrl(XivCache.GameInfo.GameDirectory);

                    // Load the original material.
                    material = await _mtrl.GetXivMtrl(matPath);

                    // And replace the path.
                    material.MTRLPath = material.MTRLPath.Replace(original, target);
                    mode = MaterialEditorMode.NewRace;
                }

                XivMtrl newMaterial;
                if (mode == MaterialEditorMode.EditSingle || mode == MaterialEditorMode.EditMulti)
                {
                    var editor = new Views.Textures.MaterialEditorView() { Owner = System.Windows.Application.Current.MainWindow };
                    var created = await editor.SetMaterial(material, item, mode);
                    // If we failed to create the dialog, just cancel entirely.
                    // We probably weren't done loading.
                    if (!created)
                    {
                        return;
                    }
                    _textureView.BottomFlyout.IsOpen = false;
                    var result = editor.ShowDialog();
                    if(result != true)
                    {
                        // User cancelled the menu.
                        return;
                    }
                    newMaterial = editor.Material;
                } else
                {
                    newMaterial = await CreateMaterialDialog.ShowCreateMaterialDialog(material, item, System.Windows.Application.Current.MainWindow);
                }

                var prim = SelectedPrimary;


                // Just re-set the primary selector to update the view.
                LoadingComplete += SetToNewTexturePart;
                SelectedPrimary = -1;

                // Reload UI and then switch to material.
                void SetToNewTexturePart(object sender, EventArgs e)
                {
                    if (SelectedPrimary == -1)
                    {
                        SelectedPrimary = prim;
                        return;
                    }

                    LoadingComplete -= SetToNewTexturePart;
                    if (newMaterial != null)
                    {
                        SelectMaterial(newMaterial.GetMaterialIdentifier().ToString());
                    }
                }

            }
            catch (Exception ex)
            {
                FlexibleMessageBox.Show(
                        UIMessages.MaterialEditorErrorMessage + "\n\nError:" + ex.Message, UIMessages.MaterialEditorErrorTitle, MessageBoxButtons.OK, MessageBoxIcon.Error);
                await UpdateTexture(_item);

            }
        }

        /// <summary>
        /// Attempts to show a given material in the viewport.
        /// </summary>
        /// <param name="materialIdentifier"></param>
        private void SelectMaterial(string materialIdentifier)
        {

            var result = _materialComboBoxData.FirstOrDefault(x => Path.GetFileNameWithoutExtension(x.Value).EndsWith(materialIdentifier) && Path.GetExtension(x.Value) == ".mtrl").Value;
            if(result != null)
            {
                SelectedMaterial = result;
            }
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
        /// The enabled status of the add new texture part button        
        /// </summary>
        public bool AddMaterialEnabled
        {
            get => _addMaterialEnabled;
            set
            {
                _addMaterialEnabled = value;
                NotifyPropertyChanged(nameof(AddMaterialEnabled));
            }
        }

        /// <summary>
        /// The enabled status of the add new texture part button        
        /// </summary>
        public bool MaterialEditorEnabled
        {
            get => _materialEditorEnabled;
            set
            {
                _materialEditorEnabled = value;
                NotifyPropertyChanged(nameof(MaterialEditorEnabled));
            }
        }
        #endregion


        /// <summary>
        /// Clears All the UI elements
        /// </summary>
        private void ClearAll()
        {
            _textureView.ColorsetEditor.Visibility = Visibility.Collapsed;
            _textureView.StandardTextureDisplay.Visibility = Visibility.Visible;
            _hasVfx = false;
            _item = null;
            _uiItem = null;
            _xivMtrl = null;
            _primaryComboBoxData.Clear();
            _materialComboBoxData.Clear();
            _mapComboBoxData.Clear();

            _textureView.SharedMaterialLabel.Content = "";
            _textureView.SharedMaterialLabel.Visibility = Visibility.Collapsed;

            _textureView.SharedVariantLabel.Visibility = Visibility.Collapsed;
            _textureView.SharedTextureLabel.Visibility = Visibility.Collapsed;
            _textureView.ItemInfoButton.IsEnabled = false;

            ClearImage();
        }

        /// <summary>
        /// The watermark for the part combobox
        /// </summary>
        public string MaterialWatermark
        {
            get => _partWatermark;
            set
            {
                _partWatermark = value;
                NotifyPropertyChanged(nameof(MaterialWatermark));
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

        // HD Assets
        #region HiResTextures

        public bool HiResEnabled
        {
            get => _hiResEnabled;
            set
            {
                _hiResEnabled = value;
                NotifyPropertyChanged(nameof(HiResEnabled));
            }
        }

        public bool HiResChecked
        {
            get => _hiResChecked;
            set
            {
                _hiResChecked = value;
                NotifyPropertyChanged(nameof(HiResChecked));
                UpdateTexture(_uiItem);
            }
        }

        #endregion

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

        private static void SwapRedBlue(byte[] imageData)
        {
            for (int i = 0; i < imageData.Length; i += 4)
            {
                byte x = imageData[i];
                byte y = imageData[i + 2];
                imageData[i] = y;
                imageData[i + 2] = x;
            }
        }

        private static void MultiplyAlpha(byte[] imageData)
        {
            for (int i = 0; i < imageData.Length; i += 4)
            {
                byte a = imageData[i + 3];
                imageData[i] = (byte)(imageData[i] * a / 256);
                imageData[i + 1] = (byte)(imageData[i + 1] * a / 256);
                imageData[i + 2] = (byte)(imageData[i + 2] * a / 256);
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

            var bmpFormat = _mapData.IsColorSet ? PixelFormats.Rgba128Float : PixelFormats.Pbgra32;
            var mapBytes = _mapData.MapBytes;

            if (bmpFormat == PixelFormats.Pbgra32)
            {
                mapBytes = (byte[])mapBytes.Clone();
                SwapRedBlue(mapBytes);

                if (AlphaChecked)
                    MultiplyAlpha(mapBytes);
            }

            ImageDisplay = BitmapSource.Create(_mapData.Width, _mapData.Height, 96.0, 96.0, bmpFormat, null, mapBytes, _mapData.Width * bmpFormat.BitsPerPixel / 8);
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


        protected virtual void OnLoadingComplete()
        {
            LoadingComplete?.Invoke(this, EventArgs.Empty);
        }
        private bool CheckMtrlIsOK()
        {
            if (_xivMtrl != null)
            {
                // Task.Run wrapper to ensure we don't hardlock the render thread.
                var task = Task.Run(async () =>
                {
                    using (var tx = ModTransaction.BeginTransaction(true))
                    {
                        return await tx.FileExists(_xivMtrl.MTRLPath);
                    }
                });
                return task.Result;
            }
            return CheckMapIsOK();
        }
        private bool CheckMapIsOK()
        {
            if(SelectedMap == null)
            {
                // We shouldn't really ever get here, but if we do, it's definitely f*d.
                TextureFormat = "ERROR: FILE DOES NOT EXIST/HAS NO INDEX ENTRY -- PLEASE RE-ENABLE MODDED TEXTURE, OR DISABLE MODDED MATERIAL REFERENCING IT.".L();
                return false;
            }

            // Task.Run wrapper to ensure we don't hardlock the render thread.
            var task = Task.Run(async () =>
            {
                using (var tx = ModTransaction.BeginTransaction(true))
                {
                    return await tx.FileExists(SelectedMap.TexturePath);
                }
            });
            return task.Result;
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