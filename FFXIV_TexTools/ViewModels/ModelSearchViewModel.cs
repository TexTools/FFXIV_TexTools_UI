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
using FFXIV_TexTools.Views;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Windows.Data;
using System.Windows.Input;
using xivModdingFramework.General.DataContainers;
using xivModdingFramework.General.Enums;
using xivModdingFramework.Items.Categories;
using xivModdingFramework.Items.DataContainers;
using xivModdingFramework.Items.Enums;

namespace FFXIV_TexTools.ViewModels
{
    public class ModelSearchViewModel: INotifyPropertyChanged
    {
        private string _selectedCategory, _modelSearchID, _statusLabel, _currentCategory;
        //esrinzou for chinese UI
        //private string _modelSearchWaterMark = "Enter a Model ID...";
        //esrinzou begin
        private string _modelSearchWaterMark = UIStrings.Enter_a_Model_ID_dot;
        //esrinzou end
        private int _selectedCategoryIndex, _currentID;
        private List<SearchResults> _resultList;
        private SearchResults _searchResult;
        private readonly MainWindow _mainView;

        public ModelSearchViewModel(MainWindow mw)
        {
            _mainView = mw;
        }

        public List<string> SearchCategories => SearchCategoriesList;

        /// <summary>
        /// The currently selected category
        /// </summary>
        public string SelectedCategory
        {
            get => _selectedCategory;
            set
            {
                _selectedCategory = value;
                NotifyPropertyChanged(nameof(SelectedCategory));
            }
        }

        /// <summary>
        /// The currently selected category index
        /// </summary>
        public int SelectedCategoryIndex
        {
            get => _selectedCategoryIndex;
            set
            {
                _selectedCategoryIndex = value;
                NotifyPropertyChanged(nameof(SelectedCategoryIndex));
            }
        }

        /// <summary>
        /// The ID of the mob search
        /// </summary>
        public string ModelSearchID
        {
            get => _modelSearchID;
            set
            {
                _modelSearchID = value;
                NotifyPropertyChanged(nameof(ModelSearchID));
            }
        }

        /// <summary>
        /// The watermark in the mob search text box
        /// </summary>
        public string ModelSearchWaterMark
        {
            get => _modelSearchWaterMark;
            set
            {
                _modelSearchWaterMark = value;
                NotifyPropertyChanged(nameof(ModelSearchWaterMark));
            }
        }

        /// <summary>
        /// The current search status
        /// </summary>
        public string StatusLabel
        {
            get => _statusLabel;
            set
            {
                _statusLabel = value;
                NotifyPropertyChanged(nameof(StatusLabel));
            }
        }

        /// <summary>
        /// The list of search results
        /// </summary>
        public List<SearchResults> ResultList
        {
            get => _resultList;
            set
            {
                _resultList = value;
                NotifyPropertyChanged(nameof(ResultList));
            }
        }

        /// <summary>
        /// The currently selected search result
        /// </summary>
        public SearchResults SelectedItem
        {
            get => _searchResult;
            set
            {
                _searchResult = value;
                NotifyPropertyChanged(nameof(SelectedItem));
            }
        }

        public ICommand ModelSearchCommand => new RelayCommand(Search);
        public ICommand OpenItemCommand => new RelayCommand(OpenItem);

        /// <summary>
        /// Initiates a search for a given ID
        /// </summary>
        private async void Search(object obj)
        {
            var id = 0;

            try
            {
                id = int.Parse(ModelSearchID);
                if(_currentID == id && _currentCategory == SelectedCategory) return;
                _currentID = id;
                _currentCategory = SelectedCategory;
            }
            catch (Exception e)
            {
                ModelSearchID = string.Empty;
                ModelSearchWaterMark = UIStrings.ModelSearch_Numeric;
                return;
            }

            var gameDirectory = new DirectoryInfo(Properties.Settings.Default.FFXIV_Directory);

            if (SelectedCategory.Equals(XivStrings.Equipment) || SelectedCategory.Equals(XivStrings.Accessory) ||
                SelectedCategory.Equals(XivStrings.Weapon))
            {
                var gear = new Gear(gameDirectory, GetLanguage());
                ResultList = await gear.SearchGearByModelID(id, SelectedCategory);
            }
            else if (SelectedCategory.Equals(XivStrings.Monster))
            {
                var companion = new Companions(gameDirectory, GetLanguage());
                ResultList = await companion.SearchMonstersByModelID(id, XivItemType.monster);
            }
            else if (SelectedCategory.Equals(XivStrings.DemiHuman))
            {
                var demiH = new Companions(gameDirectory, GetLanguage());
                ResultList = await demiH.SearchMonstersByModelID(id, XivItemType.demihuman);
            }
            else if (SelectedCategory.Equals(XivStrings.Furniture))
            {
                var furniture = new Housing(gameDirectory, GetLanguage());
                ResultList = await furniture.SearchHousingByModelID(id, XivItemType.furniture);
            }

            StatusLabel = ResultList.Count > 0 ? string.Format(UIStrings.ModelSearch_ItemsFound, ResultList.Count) : UIStrings.ModelSearch_NoItemsFound;

            ModelSearchWaterMark = UIStrings.ModelSearch_EnterID;
        }

        /// <summary>
        /// Opens the currently selected item
        /// </summary>
        /// <param name="obj"></param>
        private async void OpenItem(object obj)
        {
            _mainView.ModelTabItem.IsEnabled = true;

            CollectionViewSource.GetDefaultView(_mainView.ItemTreeView.ItemsSource).Refresh();

            var textureView = _mainView.TextureTabItem.Content as TextureView;
            var textureViewModel = textureView.DataContext as TextureViewModel;

            var modelView = _mainView.ModelTabItem.Content as ModelView;
            var modelViewModel = modelView.DataContext as ModelViewModel;

            if(SelectedItem == null) return;

            int.TryParse(SelectedItem.Body, out var body);
            var variant = SelectedItem.Variant;

            if (SelectedCategory.Equals(XivStrings.Equipment) || SelectedCategory.Equals(XivStrings.Accessory) ||
                SelectedCategory.Equals(XivStrings.Weapon))
            {
                var xivGear = new XivGear
                {
                    Name = $"{SelectedCategory.ToLower()[0]}{_currentID.ToString().PadLeft(4, '0')}",
                    Category = XivStrings.Gear,
                    ItemCategory = SelectedItem.Slot,
                    DataFile = XivDataFile._04_Chara,
                    ModelInfo = new XivModelInfo
                    {
                        ModelID = _currentID,
                        Body = body,
                        Variant = variant
                    }
                };

                await textureViewModel.UpdateTexture(xivGear);
                await modelViewModel.UpdateModel(xivGear);
            }
            else if (SelectedCategory.Equals(XivStrings.Monster))
            {
                var xivMonster = new XivGenericItemModel
                {
                    Name = $"{SelectedCategory.ToLower()[0]}{_currentID.ToString().PadLeft(4, '0')}",
                    Category = XivStrings.Companions,
                    ItemCategory = XivStrings.Monster,
                    DataFile = XivDataFile._04_Chara,
                    ModelInfo = new XivModelInfo
                    {
                        ModelID = _currentID,
                        Body = body,
                        Variant = variant,
                        ModelType = XivItemType.monster
                    }
                };

                await textureViewModel.UpdateTexture(xivMonster);
                await modelViewModel.UpdateModel(xivMonster);
            }
            else if (SelectedCategory.Equals(XivStrings.DemiHuman))
            {
                var xivDemiHuman = new XivMount
                {
                    Name = $"{SelectedCategory.ToLower()[0]}{_currentID.ToString().PadLeft(4, '0')}",
                    Category = XivStrings.Companions,
                    ItemCategory = XivStrings.Monster,
                    DataFile = XivDataFile._04_Chara,
                    ModelInfo = new XivModelInfo
                    {
                        ModelID = _currentID,
                        Body = body,
                        Variant = variant,
                        ModelType = XivItemType.demihuman
                    }
                };

                await textureViewModel.UpdateTexture(xivDemiHuman);
                await modelViewModel.UpdateModel(xivDemiHuman);
            }
            else if (SelectedCategory.Equals(XivStrings.Furniture))
            {
                var xivFurniture = new XivFurniture
                {
                    Name = $"{SelectedCategory.ToLower()[0]}{_currentID.ToString().PadLeft(4, '0')}",
                    Category = XivStrings.Housing,
                    ItemCategory = SelectedItem.Slot,
                    DataFile = XivDataFile._01_Bgcommon,
                    ModelInfo = new XivModelInfo
                    {
                        ModelID = _currentID
                    }
                };

                await textureViewModel.UpdateTexture(xivFurniture);
                await modelViewModel.UpdateModel(xivFurniture);
            }
        }

        private static readonly List<string> SearchCategoriesList = new List<string>()
        {
            XivStrings.Equipment,
            XivStrings.Accessory,
            XivStrings.Weapon,
            XivStrings.Monster,
            XivStrings.DemiHuman,
            XivStrings.Furniture
        };

        private static XivLanguage GetLanguage()
        {
            return XivLanguages.GetXivLanguage(Properties.Settings.Default.Application_Language);
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void NotifyPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}