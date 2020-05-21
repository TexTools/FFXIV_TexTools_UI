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

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using xivModdingFramework.General.Enums;
using xivModdingFramework.Items.Categories;
using xivModdingFramework.Items.DataContainers;

namespace FFXIV_TexTools.Models
{
    public class ItemsList
    {
        private readonly DirectoryInfo _gameDirectory;

        public ItemsList(DirectoryInfo gameDirectory)
        {
            _gameDirectory = gameDirectory;
        }

        /// <summary>
        /// Gets the gear list 
        /// </summary>
        /// <returns>A list containing gear data</returns>
        public async Task<List<XivGear>> GetGearList()
        {
            var gear = new Gear(_gameDirectory, GetLanguage());

            return await gear.GetGearList();
        }

        /// <summary>
        /// Gets the character list
        /// </summary>
        /// <returns>A list containing character data</returns>
        public async Task<List<XivCharacter>> GetCharacterList()
        {
            var character = new Character(_gameDirectory, GetLanguage());

            return await character.GetCharacterList();
        }

        /// <summary>
        /// Gets the companion list
        /// </summary>
        /// <returns>A tuple containing lists of all companion data</returns>
        public async Task<(List<XivMinion> MinionList, List<XivMount> MountList, List<XivPet> PetList, List<XivMount> OrnamentList)> GetCompanionList()
        {
            var companions = new Companions(_gameDirectory, GetLanguage());
            if (GetLanguage() == XivLanguage.Chinese)
            {
                return (await companions.GetMinionList(), await companions.GetMountList(), await companions.GetPetList(),new List<XivMount>());
            }
            return (await companions.GetMinionList(), await companions.GetMountList(), await companions.GetPetList(), await companions.GetOrnamentList());
        }

        /// <summary>
        /// Gets the UI list
        /// </summary>
        /// <returns>A list containing UI data</returns>
        public async Task<IEnumerable<XivUi>> GetUIList()
        {
            var ui = new UI(_gameDirectory, GetLanguage());

            var uiMasterList = (await ui.GetActionList()).Concat(await ui.GetLoadingImageList()).Concat(await ui.GetMapList())
                .Concat(await ui.GetMapSymbolList()).Concat(await ui.GetOnlineStatusList()).Concat(await ui.GetStatusList())
                .Concat(await ui.GetUldList()).Concat(await ui.GetWeatherList());

            return uiMasterList;
        }

        /// <summary>
        /// Gets the Furniture List
        /// </summary>
        /// <returns>A list containing furniture data</returns>
        public async Task<List<XivFurniture>> GetHousingList()
        {
            var housing = new Housing(_gameDirectory, GetLanguage());

            return await housing.GetFurnitureList();
        }

        /// <summary>
        /// Gets the Language from application settings
        /// </summary>
        /// <returns>The language as XivLanguage</returns>
        private static XivLanguage GetLanguage()
        {
            return XivLanguages.GetXivLanguage(Properties.Settings.Default.Application_Language);
        }
    }
}