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
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Navigation;
using xivModdingFramework.General.Enums;
using xivModdingFramework.Models.FileTypes;
using xivModdingFramework.Mods.Enums;
using xivModdingFramework.Textures.Enums;

namespace xivModdingFramework.Mods.DataContainers
{

    // This is a UI class, -NOT- a raw data class.  It does -NOT- belong in the framework project.
    public class SimpleModpackEntry : IComparable<SimpleModpackEntry>, INotifyPropertyChanged
    {

        /// <summary>
        /// The raw index from whatever list we came from.
        /// For re-resolution/book keeeping.
        /// </summary>
        private int _index;

        private SimpleModPackCreator _creatorView;
        private SimpleModPackImporter _importerView;

        public SimpleModpackEntry(int index, SimpleModPackCreator view)
        {
            _index = index;
            _creatorView = view;
        }
        public SimpleModpackEntry(int index, SimpleModPackImporter view)
        {
            _index = index;
            _importerView = view;
        }


        public Mod Mod
        {
            get
            {
                if (_creatorView == null) return null;
                return _creatorView.ModList.Mods[Index];
            }
        }
        public ModsJson Json
        {
            get
            {
                if (_importerView == null) return null;
                return _importerView.JsonEntries[Index];
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
                if(_creatorView != null)
                {
                    return _creatorView.SelectedMods.Contains(Index);
                } else
                {
                    return _importerView.SelectedEntries.Contains(Index);
                }
            } 
            set
            {
                if (_creatorView != null)
                {
                    if (value)
                    {
                        var result = _creatorView.SelectedMods.Add(Index);
                        if(result)
                        {
                            _creatorView.ModpackSize += Mod.data.modSize;
                        }
                    } else
                    {
                        var result = _creatorView.SelectedMods.Remove(Index);
                        if (result)
                        {
                            _creatorView.ModpackSize -= Mod.data.modSize;
                        }
                    }
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
                }
                else
                {
                    if (value)
                    {
                        var result = _importerView.SelectedEntries.Add(Index);
                        if (result)
                        {
                            _importerView.ModSize += Json.ModSize;
                        }
                    }
                    else
                    {
                        var result = _importerView.SelectedEntries.Remove(Index);
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
        public string Name { get
            {
                if (Mod == null)
                {
                    return Json.Name;
                }
                else
                {
                    return Mod.name;
                }
            }
        }

        /// <summary>
        /// The category of the item
        /// </summary>
        public string Category
        {
            get
            {
                if (Mod == null)
                {
                    return Json.Category;
                }
                else
                {
                    return Mod.category;
                }
            }
        }
        /// <summary>
        /// The race associated with the mod
        /// </summary>
        public string Race
        {
            get
            {
                if (Mod == null)
                {
                    return GetRace(Json.FullPath).GetDisplayName();
                }
                else
                {
                    return GetRace(Mod.fullPath).GetDisplayName();
                }
            }
        }
        /// <summary>
        /// The item part
        /// </summary>
        public string Part
        {
            get
            {
                if (Mod == null)
                {
                    return GetPart(Json.FullPath);
                }
                else
                {
                    return GetPart(Mod.fullPath);
                }
            }
        }

        private bool? _isActive;


        private async Task<XivModStatus> GetModStatus()
        {
            return await _importerView._modding.IsModEnabled(Json.FullPath, false);
        }

        /// <summary>
        /// The item part
        /// </summary>
        public bool Active
        {
            get
            {
                if (Mod == null)
                {
                    if (_isActive == null)
                    {
                        var task = Task.Run(GetModStatus);
                        task.Wait();
                        var status = task.Result;
                        _isActive = status == Enums.XivModStatus.Enabled || status == XivModStatus.MatAdd ? true : false;
                        return (bool)_isActive;
                    } else
                    {
                        return (bool)_isActive;
                    }
                }
                else
                {
                    return Mod.enabled;
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
                if (Mod == null)
                {
                    return GetType(Json.FullPath);
                }
                else
                {
                    return GetType(Mod.fullPath);
                }
            }
        }

        /// <summary>
        /// The item number
        /// </summary>
        public string Num
        {
            get
            {
                if (Mod == null)
                {
                    return GetNumber(Json.FullPath);
                }
                else
                {
                    return GetNumber(Mod.fullPath);
                }
            }
        }

        /// <summary>
        /// The item texture map
        /// </summary>
        public string Map
        {
            get
            {
                if (Mod == null)
                {
                    return GetMap(Json.FullPath);
                }
                else
                {
                    return GetMap(Mod.fullPath);
                }
            }
        }



        /// <summary>
        /// The actual raw index to the modlist entry.
        /// </summary>
        public int Index
        {
            get {
                return _index;
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public int CompareTo(SimpleModpackEntry obj)
        {
            return Name.CompareTo(obj.Name);
        }

        /// <summary>
        /// Gets the race from the path
        /// </summary>
        /// <param name="modPath">The mod path</param>
        /// <returns>The race as XivRace</returns>
        private static XivRace GetRace(string modPath)
        {
            XivRace xivRace = XivRace.All_Races;

            if (modPath.Contains("ui/") || modPath.Contains(".avfx"))
            {
                xivRace = XivRace.All_Races;
            }
            else if (modPath.Contains("monster"))
            {
                xivRace = XivRace.Monster;
            }
            else if (modPath.Contains("bgcommon"))
            {
                xivRace = XivRace.All_Races;
            }
            else if (modPath.Contains(".tex") || modPath.Contains(".mdl") || modPath.Contains(".atex"))
            {
                if (modPath.Contains("accessory") || modPath.Contains("weapon") || modPath.Contains("/common/"))
                {
                    xivRace = XivRace.All_Races;
                }
                else
                {
                    if (modPath.Contains("demihuman"))
                    {
                        xivRace = XivRace.DemiHuman;
                    }
                    else if (modPath.Contains("/v"))
                    {
                        string raceCode = modPath.Substring(modPath.IndexOf("_c") + 2, 4);
                        xivRace = XivRaces.GetXivRace(raceCode);
                    }
                    else
                    {
                        string raceCode = modPath.Substring(modPath.IndexOf("/c") + 2, 4);
                        xivRace = XivRaces.GetXivRace(raceCode);
                    }
                }
            }

            return xivRace;
        }


        /// <summary>
        /// Gets the number from the path
        /// </summary>
        /// <param name="modPath">The mod path</param>
        /// <returns>The number</returns>
        public static string GetNumber(string modPath)
        {
            string number = "-";

            if (modPath.Contains("/human/") && modPath.Contains("/body/"))
            {
                number = modPath.Substring(modPath.LastIndexOf("/b") + 2, 4).TrimStart('0');
            }
            else if (modPath.Contains("/face/"))
            {
                number = modPath.Substring(modPath.LastIndexOf("/f") + 2, 4).TrimStart('0');
            }
            else if (modPath.Contains("decal_face"))
            {
                number = modPath.Substring(modPath.IndexOf("/decal_face") + 19, 2).TrimEnd('.');
            }
            else if (modPath.Contains("decal_equip"))
            {
                if (modPath.Contains("stigma"))
                {
                    number = "stigma";
                }
                else
                {
                    number = modPath.Substring(modPath.LastIndexOf("_") + 1, 3).TrimStart('0');
                }
            }
            else if (modPath.Contains("/hair/"))
            {
                number = modPath.Substring(modPath.LastIndexOf("/h") + 2, 4).TrimStart('0');
            }
            else if (modPath.Contains("/tail/"))
            {
                number = modPath.Substring(modPath.LastIndexOf("l/t") + 3, 4).TrimStart('0');
            }
            else if (modPath.Contains("/zear/"))
            {
                number = modPath.Substring(modPath.IndexOf("/zear") + 8, 3).TrimStart('0');
            }

            return number;
        }

        /// <summary>
        /// Gets the type from the path
        /// </summary>
        /// <param name="modPath">The mod path</param>
        /// <returns>The type</returns>
        private static string GetType(string modPath)
        {
            string type = "-";

            if (modPath.Contains(".tex") || modPath.Contains(".mdl") || modPath.Contains(".atex"))
            {
                if (modPath.Contains("demihuman"))
                {
                    type = Mdl.SlotAbbreviationDictionary[modPath.Substring(modPath.LastIndexOf("/") + 16, 3)];
                }

                if (modPath.Contains("/face/"))
                {
                    if (modPath.Contains(".tex"))
                    {
                        string fileName = Path.GetFileNameWithoutExtension(modPath);
                        try
                        {
                            type = FaceTypes[fileName.Substring(fileName.IndexOf("_") + 1, 3)];
                        }
                        catch (Exception ex)
                        {
                            type = "Unknown";
                        }
                    }
                }

                if (modPath.Contains("/hair/"))
                {
                    if (modPath.Contains(".tex"))
                    {
                        string fileName = Path.GetFileNameWithoutExtension(modPath);
                        try
                        {
                            type = HairTypes[fileName.Substring(fileName.IndexOf("_") + 1, 3)];
                        }
                        catch
                        {
                            type = "Unknown";
                        }
                    }
                }

                if (modPath.Contains("/vfx/"))
                {
                    type = "VFX";
                }

            }
            else if (modPath.Contains(".avfx"))
            {
                type = "AVFX";
            }

            return type;
        }

        /// <summary>
        /// Gets the part from the path
        /// </summary>
        /// <param name="modPath">The mod path</param>
        /// <returns>The part</returns>
        public static string GetPart(string modPath)
        {
            string part = "-";
            string[] parts = new[] { "a", "b", "c", "d", "e", "f" };

            if (modPath.Contains("/texture/"))
            {
                part = modPath.Substring(modPath.LastIndexOf("_") - 1, 1);
                foreach (string letter in parts)
                {
                    if (part == letter) return part;
                }
                return "a";
            }
            else if (modPath.Contains("/material/"))
            {
                return modPath.Substring(modPath.LastIndexOf("_") + 1, 1);
            }

            return part;
        }


        /// <summary>
        /// Gets the map from the path
        /// </summary>
        /// <param name="modPath">The mod path</param>
        /// <returns>The map</returns>
        private static string GetMap(string modPath)
        {
            XivTexType xivTexType = XivTexType.Other;

            if (modPath.Contains(".mdl"))
            {
                return "3D";
            }

            if (modPath.Contains(".mtrl"))
            {
                return "ColorSet";
            }

            if (modPath.Contains("ui/"))
            {
                string subString = modPath.Substring(modPath.IndexOf("/") + 1);
                return subString.Substring(0, subString.IndexOf("/"));
            }

            if (modPath.Contains("_s.tex") || modPath.Contains("skin_m"))
            {
                xivTexType = XivTexType.Specular;
            }
            else if (modPath.Contains("_d.tex"))
            {
                xivTexType = XivTexType.Diffuse;
            }
            else if (modPath.Contains("_n.tex"))
            {
                xivTexType = XivTexType.Normal;
            }
            else if (modPath.Contains("_m.tex"))
            {
                xivTexType = XivTexType.Multi;
            }
            else if (modPath.Contains(".atex"))
            {
                string atex = Path.GetFileNameWithoutExtension(modPath);
                return atex.Substring(0, 4);
            }
            else if (modPath.Contains("decal"))
            {
                xivTexType = XivTexType.Mask;
            }

            return xivTexType.ToString();
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