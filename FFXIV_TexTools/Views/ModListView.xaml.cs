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
using FFXIV_TexTools.Models;
using FFXIV_TexTools.ViewModels;
using System.Collections;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Media;
using FFXIV_TexTools.Resources;
using xivModdingFramework.Items.DataContainers;
using xivModdingFramework.Mods;
using ListBox = System.Windows.Controls.ListBox;

namespace FFXIV_TexTools.Views
{
    /// <summary>
    /// Interaction logic for ModListView.xaml
    /// </summary>
    public partial class ModListView
    {
        public ModListView()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Event handler for treeview item changed
        /// </summary>
        private void TreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            var selectedItem = e.NewValue as Category;

            if (selectedItem?.ParentCategory != null)
            {
                if (selectedItem.ParentCategory.Name.Equals("ModPacks"))
                {
                    (DataContext as ModListViewModel).UpdateInfoGrid(selectedItem);
                    modToggleButton.IsEnabled = true;
                    modDeleteButton.IsEnabled = true;
                }
                else
                {
                    (DataContext as ModListViewModel).UpdateList(selectedItem.Item as XivGenericItemModel);
                }
            }
            else
            {
                (DataContext as ModListViewModel).ClearList();
                modToggleButton.IsEnabled = false;
                modDeleteButton.IsEnabled = false;
            }
        }

        /// <summary>
        /// Event handler for listbox selection changed
        /// </summary>
        private void ListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var listbox = sender as ListBox;

            if (listbox.SelectedItem is ModListViewModel.ModListModel selectedModItem)
            {
                //esrinzou for chinese UI
                //(DataContext as ModListViewModel).ModToggleText = selectedModItem.ModItem.enabled ? "Disable" : "Enable";
                //esrinzou begin
                (DataContext as ModListViewModel).ModToggleText = selectedModItem.ModItem.enabled ? FFXIV_TexTools.Resources.UIStrings.Disable : FFXIV_TexTools.Resources.UIStrings.Enable;
                //esrinzou end

                modToggleButton.IsEnabled = true;
                modDeleteButton.IsEnabled = true;
            }

        }

        /// <summary>
        /// Event handler for mod toggle button changed
        /// </summary>
        private void modToggleButton_Click(object sender, RoutedEventArgs e)
        {
            var gameDirectory = new DirectoryInfo(Properties.Settings.Default.FFXIV_Directory);
            var modding = new Modding(gameDirectory);

            if ((ModListTreeView.SelectedItem as Category).ParentCategory.Name.Equals("ModPacks"))
            {
                var selectedItem = (ModListTreeView.SelectedItem as Category);
                //esrinzou for chinese UI
                //if ((DataContext as ModListViewModel).ModToggleText == "Enable")
                //esrinzou begin
                if ((DataContext as ModListViewModel).ModToggleText == FFXIV_TexTools.Resources.UIStrings.Enable)
                //esrinzou end
                {
                    modding.ToggleModPackStatus(selectedItem.Name, true);
                    //esrinzou for chinese UI
                    //(DataContext as ModListViewModel).ModToggleText = "Disable";
                    //esrinzou begin
                    (DataContext as ModListViewModel).ModToggleText = FFXIV_TexTools.Resources.UIStrings.Disable;
                    //esrinzou end
                }
                else
                {
                    modding.ToggleModPackStatus(selectedItem.Name, false);
                    //esrinzou for chinese UI
                    //(DataContext as ModListViewModel).ModToggleText = "Enable";
                    //esrinzou begin
                    (DataContext as ModListViewModel).ModToggleText = FFXIV_TexTools.Resources.UIStrings.Enable;
                    //esrinzou end
                }

                (DataContext as ModListViewModel).UpdateInfoGrid(selectedItem);
            }
            else
            {
                foreach (ModListViewModel.ModListModel selectedModItem in ModItemList.SelectedItems)
                {
                    if (selectedModItem.ModItem.enabled)
                    {
                        modding.ToggleModStatus(selectedModItem.ModItem.fullPath, false);
                        //esrinzou for chinese UI
                        //(DataContext as ModListViewModel).ModToggleText = "Enable";
                        //esrinzou begin
                        (DataContext as ModListViewModel).ModToggleText = FFXIV_TexTools.Resources.UIStrings.Enable;
                        //esrinzou end
                        selectedModItem.ActiveBorder = Brushes.Red;
                        selectedModItem.Active = Brushes.Gray;
                        selectedModItem.ActiveOpacity = 0.5f;
                        selectedModItem.ModItem.enabled = false;
                    }
                    else
                    {
                        modding.ToggleModStatus(selectedModItem.ModItem.fullPath, true);
                        //esrinzou for chinese UI
                        //(DataContext as ModListViewModel).ModToggleText = "Disable";
                        //esrinzou begin
                        (DataContext as ModListViewModel).ModToggleText = FFXIV_TexTools.Resources.UIStrings.Disable;
                        //esrinzou end
                        selectedModItem.ActiveBorder = Brushes.Green;
                        selectedModItem.Active = Brushes.Transparent;
                        selectedModItem.ActiveOpacity = 1;
                        selectedModItem.ModItem.enabled = true;
                    }
                }
            }
        }

        /// <summary>
        /// Event handler for mod delete button
        /// </summary>
        private void modDeleteButton_Click(object sender, RoutedEventArgs e)
        {
            var gameDirectory = new DirectoryInfo(Properties.Settings.Default.FFXIV_Directory);
            var modding = new Modding(gameDirectory);

            if ((ModListTreeView.SelectedItem as Category).ParentCategory.Name.Equals("ModPacks"))
            {
                if (FlexibleMessageBox.Show(
                        UIMessages.DeleteModPackMessage, 
                        UIMessages.DeleteModPackTitle,
                        MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == System.Windows.Forms.DialogResult.Yes)
                {
                    modding.DeleteModPack((ModListTreeView.SelectedItem as Category).Name);
                    (DataContext as ModListViewModel).RemoveModPack();
                }

            }
            else
            {
                var enumerable = ModItemList.SelectedItems as IEnumerable;
                var selectedItems = enumerable.OfType<ModListViewModel.ModListModel>().ToArray();

                foreach (var selectedModItem in selectedItems)
                {
                    modding.DeleteMod(selectedModItem.ModItem.fullPath);
                    (DataContext as ModListViewModel).RemoveItem(selectedModItem, (Category)ModListTreeView.SelectedItem);
                }
            }
        }
    }
}
