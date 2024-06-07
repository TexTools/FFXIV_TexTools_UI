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
    /// <summary>
    /// Remains of a previous legacy class, just contains its static helpers at this point.
    /// </summary>
    public static class ModViewHelpers
    {

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