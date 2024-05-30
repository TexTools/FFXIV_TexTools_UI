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

using FFXIV_TexTools.Helpers;
using FFXIV_TexTools.Resources;
using MahApps.Metro.Controls;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Bmp;
using SixLabors.ImageSharp.Formats.Png;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Forms;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using xivModdingFramework.Mods.DataContainers;
using UserControl = System.Windows.Controls.UserControl;

namespace FFXIV_TexTools.Views
{
    /// <summary>
    /// Interaction logic for WizardModPackControl.xaml
    /// </summary>
    public partial class ExportWizardPageControl : UserControl
    {
        public List<ModGroup> ModGroupList { get; } = new List<ModGroup>();
        public List<string> ModGroupNames { get; } = new List<string>();

        public bool HasData
        {
            get
            {
                return ModGroupList.Count > 0;
            }
        }

        public ExportWizardPageControl()
        {
            InitializeComponent();

            OptionsList.ItemsSource = new List<ModOption>();
        }

        #region Event Handler

        private void ResetSelection()
        {
            if (OptionsList.Items.Count == 0)
            {
                EditGroupButton.IsEnabled = false;
                DeleteGroupButton.IsEnabled = false;
            }
            else
            {
                OptionsList.SelectedIndex = 0;
            }
        }

        /// <summary>
        /// The event handler for the add group button clicked
        /// </summary>
        private void AddGroupButton_Click(object sender, RoutedEventArgs e)
        {
            var wizardAddGroupWindow = new EditWizardGroupWindow(ModGroupNames) { Owner = Window.GetWindow(this) };

            var result = wizardAddGroupWindow.ShowDialog();

            if (result != true)
            {
                return;
            }

            var results = wizardAddGroupWindow.GetResults();
            var optionsList = results.OptionList;

            if (optionsList.Count > 0)
            {
                ModGroupList.Add(results);
                ModGroupNames.Add(results.GroupName);

                if (results.SelectionType.Equals("Single")&&optionsList.Count(it=>it.IsChecked)==0)
                {
                    optionsList[0].IsChecked = true;
                }

                ((List<ModOption>)OptionsList.ItemsSource).AddRange(optionsList);

                var view = (CollectionView)CollectionViewSource.GetDefaultView(OptionsList.ItemsSource);
                var groupDescription = new PropertyGroupDescription("GroupName");
                view.GroupDescriptions.Clear();
                view.GroupDescriptions.Add(groupDescription);
            }
        }

        /// <summary>
        /// The event handler for the edit group button clicked
        /// </summary>
        private void EditGroupButton_Click(object sender, RoutedEventArgs e)
        {
            var option = OptionsList.SelectedItem as ModOption;
            ModGroup editModGroup = null;

            foreach (var modGroup in ModGroupList)
            {
                if (modGroup.GroupName.Equals(option.GroupName))
                {
                    editModGroup = modGroup;
                    break;
                }
            }

            var wizardAddGroupWindow = new EditWizardGroupWindow(ModGroupNames)
            {
                Owner = Window.GetWindow(this),
                Title = UIStrings.Edit_Group
            };
            wizardAddGroupWindow.EditMode(editModGroup);

            var result = wizardAddGroupWindow.ShowDialog();

            if (result != true)
            {
                return;
            } 

            wizardAddGroupWindow.UpdateModGroup(editModGroup);

            OptionsList.ItemsSource = new List<ModOption>();

            foreach (var modGroup in ModGroupList)
            {
                if (modGroup.SelectionType.Equals("Single") && modGroup.OptionList.Count(it => it.IsChecked) == 0)
                {
                    modGroup.OptionList[0].IsChecked = true;
                }

                ((List<ModOption>)OptionsList.ItemsSource).AddRange(modGroup.OptionList);

                var view = (CollectionView)CollectionViewSource.GetDefaultView(OptionsList.ItemsSource);
                var groupDescription = new PropertyGroupDescription("GroupName");
                view.GroupDescriptions.Clear();
                view.GroupDescriptions.Add(groupDescription);
            }
        }

        /// <summary>
        /// The event handler for the delete group button clicked
        /// </summary>
        private void DeleteGroupButton_Click(object sender, RoutedEventArgs e)
        {
            if (FlexibleMessageBox.Show(
                    UIMessages.DeleteGroupMessage,
                    UIMessages.DeleteGroupTitle, MessageBoxButtons.OKCancel, MessageBoxIcon.Warning) != DialogResult.OK)
            {
                return;
            }

            var option = OptionsList.SelectedItem as ModOption;

            ModGroup modGroupToDelete = null;

            foreach (var modGroup in ModGroupList)
            {
                if (modGroup.GroupName.Equals(option.GroupName))
                {
                    modGroupToDelete = modGroup;
                    break;
                }
            }

            ModGroupList.Remove(modGroupToDelete);
            ModGroupNames.Remove(option.GroupName);

            OptionsList.ItemsSource = new List<ModOption>();

            foreach (var modGroup in ModGroupList)
            {
                if (modGroup.SelectionType.Equals("Single")&& modGroup.OptionList.Count(it=>it.IsChecked)==0)
                {
                    modGroup.OptionList[0].IsChecked = true;
                }

                ((List<ModOption>)OptionsList.ItemsSource).AddRange(modGroup.OptionList);

                var view = (CollectionView)CollectionViewSource.GetDefaultView(OptionsList.ItemsSource);
                var groupDescription = new PropertyGroupDescription("GroupName");
                view.GroupDescriptions.Clear();
                view.GroupDescriptions.Add(groupDescription);
            }

            ResetSelection();
        }

        /// <summary>
        /// The event handler for the option list selection changed
        /// </summary>
        private void OptionsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var option = OptionsList.SelectedItem as ModOption;
            if(option == null)
            {
                return;
            }

            OptionDescriptionTextBox.Text = option.Description ?? string.Empty;

            if (option.Image != null)
            {
                try
                {
                    BitmapImage bmp;

                    using (var ms = new MemoryStream())
                    {
                        option.Image.Save(ms, new BmpEncoder());

                        bmp = new BitmapImage();
                        bmp.BeginInit();
                        bmp.StreamSource = ms;
                        bmp.CacheOption = BitmapCacheOption.OnLoad;
                        bmp.EndInit();
                        bmp.Freeze();
                    }

                    OptionPreviewImage.Source = bmp;
                }
                catch(ObjectDisposedException)
                {
                    OptionPreviewImage.Source = null;
                }
            }
            else
            {
                OptionPreviewImage.Source = null;
            }

            EditGroupButton.IsEnabled = true;
            DeleteGroupButton.IsEnabled = true;
        }

        #endregion

        #region Dispose

        /// <summary>
        /// Dispose
        /// </summary>
        public void Dispose()
        {
            if (OptionsList != null && OptionsList.ItemsSource != null)
            {
                foreach (ModOption modOption in OptionsList.ItemsSource)
                {
                    if (modOption.Image != null)
                    {
                        modOption.Image.Dispose();
                    }
                }
            }
        }

        #endregion

        private void Option_Toggled(object sender, RoutedEventArgs e)
        {
            var s = (FrameworkElement)sender;
            var modOption = (ModOption)s.DataContext;
            OptionsList.SelectedItem = modOption;
        }
    }

    /// <summary>
    /// Template selector class for items in the options list
    /// </summary>
    public class SelectionTemplateSelector : DataTemplateSelector
    {
        /// <summary>
        /// Template for radio button
        /// </summary>
        public DataTemplate RadioButtonTemplate { get; set; }

        /// <summary>
        /// Template for check box
        /// </summary>
        public DataTemplate CheckBoxTemplate { get; set; }

        /// <summary>
        /// Selects the data template
        /// </summary>
        public override DataTemplate SelectTemplate(object item, DependencyObject container)
        {
            if (item == null) return null;

            if (container is FrameworkElement frameworkElement)
            {
                var option = (ModOption)item;
                var selectionType = option.SelectionType;

                if (selectionType.Equals("Single"))
                {
                    RadioButtonTemplate = frameworkElement.FindResource("RadioButtonTemplate") as DataTemplate;
                    return RadioButtonTemplate;
                }
                else
                {
                    CheckBoxTemplate = frameworkElement.FindResource("CheckBoxTemplate") as DataTemplate;
                    return CheckBoxTemplate;
                }
            }

            return null;
        }
    }
}
