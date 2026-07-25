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
using FFXIV_TexTools.Resources;
using FFXIV_TexTools.ViewModels;
using System;
using System.Collections;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Media;
using MahApps.Metro.Controls.Dialogs;
using xivModdingFramework.Mods;
using ListBox = System.Windows.Controls.ListBox;
using System.Threading.Tasks;
using xivModdingFramework.Mods.Enums;
using System.Windows.Shapes;
using System.Collections.Generic;
using SharpDX;
using FFXIV_TexTools.Views.Controls;
using static FFXIV_TexTools.ViewModels.ModListViewModel;
using xivModdingFramework.Helpers;

namespace FFXIV_TexTools.Views
{
    /// <summary>
    /// Interaction logic for ModListView.xaml
    /// </summary>
    public partial class ModListView
    {

        private ProgressDialogController _lockProgressController;
        private IProgress<string> _lockProgress;
        public async Task LockUi(string title, string message, object sender)
        {
            _lockProgressController = await this.ShowProgressAsync(title, message);

            _lockProgressController.SetIndeterminate();

            _lockProgress = new Progress<string>((update) =>
            {
                _lockProgressController.SetMessage(update);
            });
        }
        public async Task UnlockUi(object sender)
        {
            await _lockProgressController.CloseAsync();
            _lockProgressController = null;
            _lockProgress = null;
        }


        private CancellationTokenSource _cts;

        public ModListView()
        {
            InitializeComponent();
            Closing += ModListView_Closing;
        }

        private void ModListView_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if(Owner != null)
            {
                Owner.Activate();
            }
        }

        /// <summary>
        /// Event handler for treeview item changed
        /// </summary>
        private async void TreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            var selectedItem = e.NewValue as Category;

            if (e.OldValue != null)
            {
                _cts.Cancel();
                _cts.Dispose();
            }

            try
            {
                _cts = new CancellationTokenSource();

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
                        await (DataContext as ModListViewModel).UpdateList(selectedItem, _cts);
                        modToggleButton.IsEnabled = false;
                        modDeleteButton.IsEnabled = false;
                    }
                }
                else
                {
                    (DataContext as ModListViewModel).ClearList();
                    modToggleButton.IsEnabled = false;
                    modDeleteButton.IsEnabled = false;
                }
            }
            catch(Exception ex)
            {
                Debug.WriteLine($"Loading Canceled\n\n{ex.Message._()}".L());
            }

        }

        /// <summary>
        /// Event handler for listbox selection changed
        /// </summary>
        private async void ListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var listbox = sender as ListBox;

            if (listbox.SelectedItem is ModListViewModel.ModListModel selectedModItem)
            {
                var tx = MainWindow.DefaultTransaction;
                var enabled = await selectedModItem.ModItem.GetState(tx) == EModState.Enabled;

                (DataContext as ModListViewModel).ModToggleText = enabled ? FFXIV_TexTools.Resources.UIStrings.Disable : FFXIV_TexTools.Resources.UIStrings.Enable;

                modToggleButton.IsEnabled = true;
                modDeleteButton.IsEnabled = true;
            }

        }

        /// <summary>
        /// Event handler for mod toggle button changed
        /// </summary>
        private async void modToggleButton_Click(object sender, RoutedEventArgs e)
        {
            if (!this.CheckFileWrite())
            {
                return;
            }

            await LockUi("Changing Mod Status".L(), "Please wait...".L(), this);

            Category selectedItem = null;

            var tx = MainWindow.UserTransaction;
            var boiler = await TxBoiler.BeginWrite(tx);
            tx = boiler.Transaction;
            try
            {
                if ((ModListTreeView.SelectedItem as Category).ParentCategory.Name.Equals("ModPacks"))
                {
                    selectedItem = (ModListTreeView.SelectedItem as Category);

                    if ((DataContext as ModListViewModel).ModToggleText == FFXIV_TexTools.Resources.UIStrings.Enable)
                    {
                        try
                        {
                            await Modding.SetModpackState(EModState.Enabled, selectedItem.Name, tx);
                            (DataContext as ModListViewModel).ModToggleText = FFXIV_TexTools.Resources.UIStrings.Disable;
                        }
                        catch (Exception ex)
                        {
                            FlexibleMessageBox.Show("Unable to fully disable Modpack.\nPlease use Help => Download Index Backups/Help => Start Over\nError: ".L() + ex.Message, "Mod Disable Error".L(), MessageBoxButtons.OK, MessageBoxIcon.Error);
                        }
                    }
                    else
                    {
                        try
                        {
                            await Modding.SetModpackState(EModState.Disabled, selectedItem.Name, tx);
                            (DataContext as ModListViewModel).ModToggleText = FFXIV_TexTools.Resources.UIStrings.Enable;
                        }
                        catch (Exception ex)
                        {
                            FlexibleMessageBox.Show("Unable to fully enable Modpack.\n\nError: ".L() + ex.Message, "Mod Enable Error".L(), MessageBoxButtons.OK, MessageBoxIcon.Error);
                        }
                    }
                }
                else
                {
                    var items = ModItemList.SelectedItems;
                    foreach (ModListViewModel.ModListModel selectedModItem in items)
                    {
                        if (await selectedModItem.ModItem.GetState(tx) == EModState.Enabled)
                        {

                            var success = false;
                            try
                            {
                                await Modding.SetModState(EModState.Disabled, selectedModItem.ModItem.FilePath, tx);
                                success = true;
                            }
                            catch (Exception ex)
                            {
                                FlexibleMessageBox.Show("Unable to disable mod.\nPlease use Help => Download Index Backups/Help => Start Over\nError: ".L() + ex.Message, "Mod Disable Error".L(), MessageBoxButtons.OK, MessageBoxIcon.Error);
                            }
                            if (success)
                            {
                                (DataContext as ModListViewModel).ModToggleText = FFXIV_TexTools.Resources.UIStrings.Enable;
                                selectedModItem.ActiveBorder = Brushes.Red;
                                selectedModItem.Active = Brushes.Gray;
                                selectedModItem.ActiveOpacity = 0.5f;
                                var m = selectedModItem.ModItem;
                                selectedModItem.ModItem = m;
                            }
                        }
                        else
                        {
                            var success = false;
                            try
                            {
                                await Modding.SetModState(EModState.Enabled, selectedModItem.ModItem.FilePath, tx);
                                success = true;
                            }
                            catch (Exception ex)
                            {
                                FlexibleMessageBox.Show("Unable to enable mod.\n\nError: ".L() + ex.Message, "Mod Enable Error".L(), MessageBoxButtons.OK, MessageBoxIcon.Error);
                            }
                            if (success)
                            {
                                (DataContext as ModListViewModel).ModToggleText = FFXIV_TexTools.Resources.UIStrings.Disable;
                                selectedModItem.ActiveBorder = Brushes.Green;
                                selectedModItem.Active = Brushes.Transparent;
                                selectedModItem.ActiveOpacity = 1;
                                var m = selectedModItem.ModItem;
                                selectedModItem.ModItem = m;
                            }
                        }
                    }
                }

                await boiler.Commit();
            }
            catch(Exception ex)
            {
                await boiler.Cancel();
                this.ShowError("Enable/Disable Mod Error", "An error occurred while Enabling/Disabling the mod:" + ex.Message);
            }
            finally
            {

                if (selectedItem != null)
                {
                    (DataContext as ModListViewModel).UpdateInfoGrid(selectedItem);
                }
                await UnlockUi(this);
            }
        }

        /// <summary>
        /// Event handler for mod delete button
        /// </summary>
        private async void modDeleteButton_Click(object sender, RoutedEventArgs e)
        {
            if (!this.CheckFileWrite())
            {
                return;
            }

            var wind = ViewHelpers.GetWin32Window(this);
            await LockUi("Deleting Mod".L(), "Please wait...".L(), this);
            try
            {
                var cat = (ModListTreeView.SelectedItem as Category);

                if (cat == null) return;

                var catName = cat.Name;
                if (catName == UIStrings.Standalone_Non_ModPack)
                {
                    catName = "";
                }

                if (cat.ParentCategory.Name.Equals("ModPacks"))
                {
                    if (FlexibleMessageBox.Show(wind,
                            UIMessages.DeleteModPackMessage,
                            UIMessages.DeleteModPackTitle,
                            MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == System.Windows.Forms.DialogResult.Yes)
                    {

                        await Modding.DeleteModPack(catName, MainWindow.UserTransaction);
                        (DataContext as ModListViewModel).RemoveModPack();
                    }

                }
                else
                {
                    var enumerable = ModItemList.SelectedItems as IEnumerable;
                    var selectedItems = enumerable.OfType<ModListViewModel.ModListModel>().ToArray();

                    foreach (var selectedModItem in selectedItems)
                    {
                        await Modding.DeleteMod(selectedModItem.ModItem.FilePath, MainWindow.UserTransaction);
                        await (DataContext as ModListViewModel).RemoveItem(selectedModItem, cat);
                    }
                }
            }
            catch (Exception Ex)
            {
                FlexibleMessageBox.Show(wind,"Unable to delete Mod or Modpack.\n\nError: ".L() + Ex.Message, "Mod Delete Error".L(), MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally {
                await UnlockUi(this);
            }
        }

        private void MetroWindow_Closed(object sender, EventArgs e)
        {
            (DataContext as ModListViewModel).Dispose();
            _cts?.Dispose();
        }

        private void CopyPath_Click(object sender, RoutedEventArgs e)
        {
            if (sender == null) return;

            var ml = (sender as FrameworkElement).DataContext as ModListModel;
            if (ml == null) return;


            try
            {
                System.Windows.Clipboard.SetText(ml.FilePath);
            }
            catch
            {
                // No-Op
            }

        }

        private async void OpenFile_Click(object sender, RoutedEventArgs e)
        {
            if (sender == null) return;

            var ml = (sender as FrameworkElement).DataContext as ModListModel;
            if (ml == null) return;


            try
            {
                await SimpleFileViewWindow.OpenFile(ml.FilePath);
            } catch (Exception Ex)
            {
                Trace.WriteLine(Ex);
            }
        }
    }
}
