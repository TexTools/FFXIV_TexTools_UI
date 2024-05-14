// xivModdingFramework
// Copyright © 2018 Rafael Gonzalez - All Rights Reserved
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

using FFXIV_TexTools.Resources;
using FFXIV_TexTools.Views;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Media.Animation;
using System.Windows.Navigation;
using xivModdingFramework.General.Enums;
using xivModdingFramework.Models.FileTypes;
using xivModdingFramework.Mods.Enums;
using xivModdingFramework.Textures.DataContainers;
using xivModdingFramework.Textures.Enums;
using FFXIV_TexTools;

namespace xivModdingFramework.Mods.DataContainers
{

    // This is a UI class, -NOT- a raw data class.  It does -NOT- belong in the framework project.
    public class SimpleModpackEntry : IComparable<SimpleModpackEntry>, INotifyPropertyChanged
    {

        /// <summary>
        /// The raw index from whatever list we came from.
        /// For re-resolution/book keeeping.
        /// </summary>
        private string _path;

        private Mod? _mod;
        private SimpleModPackCreator _creatorView;
        private SimpleModPackImporter _importerView;

        public SimpleModpackEntry(string path, SimpleModPackCreator view)
        {
            _path = path;
            _creatorView = view;
        }
        public SimpleModpackEntry(string index, SimpleModPackImporter view)
        {
            _path = index;
            _importerView = view;
        }
        public SimpleModpackEntry(Mod mod)
        {
            _mod = mod;
        }


        public Mod? Mod
        {
            get
            {
                if (_mod != null) return _mod;

                if (_creatorView == null) return null;
                return _creatorView.ModList.GetMod(_path);
            }
        }
        public ModsJson Json
        {
            get
            {
                if (_importerView == null) return null;
                return _importerView.JsonEntries[Path];
            }
        }

        /// <summary>
        /// Raises the property changed flag for the UI.
        /// </summary>
        public void MarkDirty()
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
        }

        /// <summary>
        /// Selected function automatically adds/removes us from the parent object's hash set.
        /// This also updates the associated ModSize value in the views (which is responsible for 
        /// controlling some logic in those views, with regards to size display and buttons being
        /// enabled)
        /// </summary>
        public bool IsSelected {
            get {
                if (_creatorView != null)
                {
                    return _creatorView.SelectedMods.Contains(Path);
                } else
                {
                    return _importerView.SelectedEntries.Contains(Path);
                }
            }
            set
            {
                if (_creatorView != null)
                {
                    if (value)
                    {
                        var result = _creatorView.SelectedMods.Add(Path);
                        if (result)
                        {
                            _creatorView.ModpackSize += Mod.Value.FileSize;
                        }
                    } else
                    {
                        var result = _creatorView.SelectedMods.Remove(Path);
                        if (result)
                        {
                            _creatorView.ModpackSize -= Mod.Value.FileSize;
                        }
                    }
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
                }
                else
                {
                    if (value)
                    {
                        var result = _importerView.SelectedEntries.Add(Path);
                        if (result)
                        {
                            _importerView.ModSize += Json.ModSize;
                        }
                    }
                    else
                    {
                        var result = _importerView.SelectedEntries.Remove(Path);
                        if (result)
                        {
                            _importerView.ModSize -= Json.ModSize;
                        }
                    }
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
                }
            }
        }

        /// <summary>
        /// The name of the item
        /// </summary>
        public string ItemName { get
            {
                var name = "";
                if (Mod == null || !Mod.Value.Valid)
                {
                    name = Json.Name;
                }
                else
                {
                    name = Mod?.ItemName;
                }

                return GetFancyName(name, FileName);
            }
        }

        public string Extension
        {
            get
            {
                try
                {
                    return System.IO.Path.GetExtension(FilePath).Substring(1);
                }
                catch
                {
                    return "Unknown";
                }
            }
        }

        /// <summary>
        /// File path of the first parent file we have cached.
        /// </summary>
        public string ParentFilePath
        {
            get
            {
                if (_creatorView != null)
                {
                    if (_creatorView.ParentsDictionary != null && _creatorView.ParentsDictionary.ContainsKey(FilePath))
                    {
                        var parents = _creatorView.ParentsDictionary[FilePath];
                        if (parents.Count > 0)
                        {
                            // TOOD - Need to choose same-root parent when multiple parents are present.
                            return parents[0];
                        }
                    }
                }
                return null;
            }
        }

        public string FilePath
        {
            get
            {
                if (Mod == null)
                {
                    return Json.FullPath;
                }
                else
                {
                    return Mod?.FilePath;
                }
            }
        }

        /// <summary>
        /// The name of the item
        /// </summary>
        public string FileName
        {
            get
            {
                return System.IO.Path.GetFileName(FilePath);
            }
        }

        public string Material
        {
            get
            {
                if (Extension == "tex")
                {
                    var parent = ParentFilePath;
                    if (parent != null)
                    {
                        // We have an associated MTRL file.
                        return GetMaterialId(parent);
                    } else
                    {
                        // A null parent means we are either orphaned,
                        // still processing, or not a dependency enabled file.
                        return "-";
                    }
                } else if (Extension == "mtrl")
                {
                    // We are an MTRL file.
                    return GetMaterialId(FilePath);
                }

                // We don't have an associated MTRL file, or we don't use one.
                return "-";
            }
        }

        /// <summary>
        /// The race associated with the mod
        /// </summary>
        public string Race
        {
            get
            {
                var race = XivRace.All_Races;
                if (Extension == "tex")
                {
                    var parent = ParentFilePath;
                    if (parent != null)
                    {
                        // We have an associated MTRL file.
                        race = GetRace(parent);
                    }
                    else
                    {
                        // We have no associated MTRL file, hopefully Race Data is available in our own path.
                        // (Race data is indeterminable for orphaned custom file path textures)
                        race = GetRace(FilePath);
                    }
                }
                else
                {
                    // Everything else either has Race Data in its own path, or is all races.
                    race = GetRace(FilePath);
                }


                if (race == XivRace.All_Races)
                {
                    return "-";
                } else
                {
                    return race.GetDisplayName();
                }
            }
        }

        private bool? _isActive;


        private async Task<EModState> GetModStatus()
        {
            if(Json != null)
            {
                var tx = _creatorView == null ? _importerView.Transaction : _creatorView.Transaction;
                return await Modding.GetModState(Json.FullPath, tx);
            } else if (Mod != null)
            {
                var tx = _creatorView == null ? _importerView.Transaction : _creatorView.Transaction;
                return await Modding.GetModState(Mod.Value.FilePath, tx);
            } else
            {
                return EModState.UnModded;
            }
        }

        public string ActiveText
        {
            get {
                if (Active) {
                    return "Active";
                } else
                {
                    return "-";
                }
            }
        }
        public bool Active
        {
            get
            {
                if (_isActive == null)
                {
                    var tx = _creatorView == null ? _importerView.Transaction : _creatorView.Transaction;
                    var task = Task.Run(GetModStatus);
                    task.Wait();
                    var status = task.Result;
                    _isActive = status == Enums.EModState.Enabled ? true : false;
                    return (bool)_isActive;
                } else
                {
                    return (bool)_isActive;
                }
            }
        }

        /// <summary>
        /// The item type
        /// </summary>
        public string Type
        {
            get
            {
                return GetType(FilePath);
            }
        }



        /// <summary>
        /// The actual raw index to the modlist entry.
        /// </summary>
        public string Path
        {
            get {
                return _path;
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public int CompareTo(SimpleModpackEntry obj)
        {
            return FileName.CompareTo(obj.ItemName);
        }

        private static readonly Regex _extractRaceRegex = new Regex(".*c([0-9]{4})");

        /// <summary>
        /// Gets the race from the path
        /// </summary>
        /// <param name="modPath">The mod path</param>
        /// <returns>The race as XivRace</returns>
        public static XivRace GetRace(string modPath)
        {
            // I'm 99% sure we have another function somewhere that already does this, but whatever.
            var match = _extractRaceRegex.Match(modPath);
            if(match.Success)
            {
                return XivRaces.GetXivRace(match.Groups[1].Value);
            }
            return XivRace.All_Races;
        }


        // This is the old ""Number"" field TexTools used to expose.
        // It's actually just the secondary ID for everything but Demihumans.
        private static readonly Regex _extractSecondaryId = new Regex("(?:c[0-9]{4}).*(?:f|z|b|h|t)([0-9]{4})");
        private static readonly Regex _extractSlot = new Regex("_([a-z]{3})[_\\.]");

        public static string GetFancyName(string itemName, string internalPath, string slotPath = null)
        {
            var id = GetFancyNameId(internalPath);
            var name = itemName;
            if(id != null)
            {
                name += " - " + id;


                var suffix = System.IO.Path.GetExtension(internalPath);
                if (string.IsNullOrEmpty(suffix))
                {
                    suffix = "Unknown";
                }
                else
                {
                    suffix = suffix.Substring(1);
                }

                if (suffix == "mtrl" || suffix == "tex")
                {
                    // So if we have an ID # to display, we're one of the funny "Character" subtype items
                    // where we bash the actual identifiers and list them all as just "Face" or "Tail", etc.
                    // This means we also need to scrape the slot information.
                    slotPath = slotPath != null ? slotPath : internalPath;

                    var match = _extractSlot.Match(slotPath);
                    if (match.Success)
                    {
                        var niceSlotName = Mdl.SlotAbbreviationDictionary.FirstOrDefault(x => x.Value == match.Groups[1].Value).Key;
                        if (String.IsNullOrEmpty(niceSlotName))
                        {
                            niceSlotName = match.Groups[1].Value;
                        }

                        name += " - " + niceSlotName;
                    }
                }


            }
            return name;
        }

        /// <summary>
        /// Gets the Secondary Id for the path; at least for the cases where we use
        /// non-unique Item Names, like Faces, Tails, etc.
        /// </summary>
        /// <param name="modPath">The mod path</param>
        /// <returns>The number</returns>
        public static string GetFancyNameId(string modPath)
        {
            var match = _extractSecondaryId.Match(modPath);
            if(match.Success)
            {
                return match.Groups[1].Value;
            }
            return null;
        }

        /// <summary>
        /// Gets the human readable type from the path
        /// </summary>
        /// <param name="modPath">The mod path</param>
        /// <returns>The type</returns>
        public static string GetType(string modPath)
        {
            var exRaw = System.IO.Path.GetExtension(modPath);
            if(string.IsNullOrEmpty(exRaw))
            {
                return "Unknown".L();
            }
            var ext = exRaw.Substring(1);
            if(ext == "mdl")
            {
                return "Model".L();
            } else if ( ext == "meta") {
                return "Metadata".L();
            } else if (ext == "mtrl") {
                return "Material".L();
            } else if(ext == "tex")
            {
                return "Texture - ".L() + GuessTextureUsage(modPath).ToString().L();
            } else
            {
                return ext.ToUpper().L();
            }
        }


        private static Regex _materialExtractionRegex = new Regex("(?:v([0-9]{4}).*)?(?:_([a-z]+)\\.mtrl$)");

        /// <summary>
        /// Extracts the Material Variant and Material Identifier from a material path.
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        public static string GetMaterialId(string path)
        {
            var match = _materialExtractionRegex.Match(path);
            if (match.Success)
            {
                var ret = "";
                if(match.Groups[1].Success)
                {
                    ret += "v" + match.Groups[1].Value + " - ";
                }

                return  ret + match.Groups[2].Value;
            } else
            {
                return "-";
            }
        }

        private static readonly Regex _normRegex = new Regex("(_n(\\.|_))|(norm)");
        private static readonly Regex _diffuseRegex = new Regex("(_d(\\.|_))|(diff)");
        private static readonly Regex _specRegex = new Regex("(_s(\\.|_))|(spec)");
        private static readonly Regex _multiRegex = new Regex("(_m(\\.|_))|(mul)|(mask)");
        private static readonly Regex _reflectionRegex = new Regex("(catchlight|refl)");
        private static readonly Regex _iconRegex = new Regex("^ui/icon/");
        private static readonly Regex _mapRegex = new Regex("^ui/map/");
        private static readonly Regex _loadingImageRegex = new Regex("^ui/loadingimage/");
        private static readonly Regex _uldRegex = new Regex("^ui/uld/");

        /// <summary>
        /// This function *Guesses* the texture usage type of a texture file, 
        /// based on its file name.  This is not 100% accurate, but it's significantly
        /// cheaper than ripping open the parent MTRL file to pull its usage type.
        /// 
        /// Long term, we may want to consider having the cache queue calculate this for us.
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        public static XivTexType GuessTextureUsage(string path) {
            
            if(_normRegex.IsMatch(path))
            {
                return XivTexType.Normal;
            } else if(_diffuseRegex.IsMatch(path))
            {
                return XivTexType.Diffuse;
            }
            else if (_specRegex.IsMatch(path))
            {
                return XivTexType.Specular;
            }
            else if (_multiRegex.IsMatch(path))
            {
                return XivTexType.Mask;
            }
            else if (_reflectionRegex.IsMatch(path))
            {
                return XivTexType.Reflection;
            } else if(_iconRegex.IsMatch(path))
            {
                return XivTexType.Icon;

            }
            else if (_mapRegex.IsMatch(path))
            {
                return XivTexType.Map;

            }
            else if (_loadingImageRegex.IsMatch(path))
            {
                return XivTexType.UI;

            }
            else if (_uldRegex.IsMatch(path))
            {
                return XivTexType.UI;

            }
            else
            {
                return XivTexType.Other;
            }

        }

        private static readonly Dictionary<string, string> FaceTypes = new Dictionary<string, string>
        {
            {"fac", XivStrings.Face},
            {"iri", XivStrings.Iris},
            {"etc", XivStrings.Etc},
            {"acc", XivStrings.Accessory}
        };

        private static readonly Dictionary<string, string> HairTypes = new Dictionary<string, string>
        {
            {"acc", XivStrings.Accessory},
            {"hir", XivStrings.Hair},
        };

        private static readonly Dictionary<string, string> slotAbr = new Dictionary<string, string>
        {
            {"met", XivStrings.Head},
            {"glv", XivStrings.Hands},
            {"dwn", XivStrings.Legs},
            {"sho", XivStrings.Feet},
            {"top", XivStrings.Body},
            {"ear", XivStrings.Earring},
            {"nek", XivStrings.Neck},
            {"rir", XivStrings.Ring_Right},
            {"ril", XivStrings.Ring_Left},
            {"wrs", XivStrings.Wrists},
        };
    }
}