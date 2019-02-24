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
        public List<XivGear> GetGearList()
        {
            var gear = new Gear(_gameDirectory, GetLanguage());

            return gear.GetGearList();
        }

        /// <summary>
        /// Gets the character list
        /// </summary>
        /// <returns>A list containing character data</returns>
        public List<XivCharacter> GetCharacterList()
        {
            var character = new Character(_gameDirectory);

            return character.GetCharacterList();
        }

        /// <summary>
        /// Gets the companion list
        /// </summary>
        /// <returns>A tuple containing lists of all companion data</returns>
        public (List<XivMinion> MinionList, List<XivMount> MountList, List<XivPet> PetList) GetCompanionList()
        {
            var companions = new Companions(_gameDirectory, GetLanguage());

            return (companions.GetMinionList(), companions.GetMountList(), companions.GetPetList());
        }

        /// <summary>
        /// Gets the UI list
        /// </summary>
        /// <returns>A list containing UI data</returns>
        public IEnumerable<XivUi> GetUIList()
        {
            var ui = new UI(_gameDirectory, GetLanguage());

            var uiMasterList = ui.GetActionList().Concat(ui.GetLoadingImageList()).Concat(ui.GetMapList())
                .Concat(ui.GetMapSymbolList()).Concat(ui.GetOnlineStatusList()).Concat(ui.GetStatusList())
                .Concat(ui.GetUldList()).Concat(ui.GetWeatherList());

            return uiMasterList;
        }

        /// <summary>
        /// Gets the Furniture List
        /// </summary>
        /// <returns>A list containing furniture data</returns>
        public List<XivFurniture> GetHousingList()
        {
            var housing = new Housing(_gameDirectory, GetLanguage());

            return housing.GetFurnitureList();
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