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
using System.Collections.ObjectModel;
using System.ComponentModel;
using xivModdingFramework.Items.Interfaces;

namespace FFXIV_TexTools.Models
{
    public class Category : INotifyPropertyChanged
    {
        /// <summary>
        /// The expanded status of the category
        /// </summary>
        private bool _isExpanded;

        /// <summary>
        /// The Name of the category
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// The item data
        /// </summary>
        public IItem Item { get; set; }

        /// <summary>
        /// The collection of categories within the category
        /// </summary>
        public ObservableCollection<Category> Categories { get; set; }

        /// <summary>
        /// The list of category names
        /// </summary>
        public List<string> CategoryList { get; set; }

        /// <summary>
        /// The expanded status of the category
        /// </summary>
        public bool IsExpanded
        {
            get => _isExpanded;
            set
            {
                _isExpanded = value;
                NotifyPropertyChanged(nameof(IsExpanded));
            }
        }

        /// <summary>
        /// The parent category 
        /// </summary>
        public Category ParentCategory { get; set; }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void NotifyPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}