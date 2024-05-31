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

using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using xivModdingFramework.Mods.DataContainers;
using Image = SixLabors.ImageSharp.Image;

namespace FFXIV_TexTools.Views.Wizard
{
    public partial class WizardPageControl : UserControl, INotifyPropertyChanged
    {
        private bool Editable;

        public event PropertyChangedEventHandler PropertyChanged;
        private WizardPageEntry Data;

        public bool HasData
        {
            //TODO: Fix this up?
            get
            {
                return true;
            }
        }

        public Visibility EditorVisibility
        {
            get
            {
                return Editable ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        private bool OptionSelected
        {
            get
            {
                var opt = OptionsList.SelectedItem as WizardOptionEntry;
                if (opt == null) {
                    return false;
                }
                return true;
            }
        }

        public WizardPageControl(WizardPageEntry data, bool editable = false)
        {
            Data = data;
            Editable = editable;
            DataContext = this;
            InitializeComponent();

            SetupUi();
        }

        private int GetCurrentIndex()
        {
            var opt = OptionsList.SelectedItem as WizardOptionEntry;
            if (opt == null) return 0;

            var owningGroup = Data.Groups.FirstOrDefault(x => x.Options.Any(o => o == opt));
            if (owningGroup == null) return 0;

            var idx = Data.Groups.IndexOf(owningGroup);
            if (idx < 0) return 0;

            return idx;
        }

        private void SetupUi()
        {
            var opt = OptionsList.SelectedItem as WizardOptionEntry;


            // We want one full unified list of options as a source for our listbox.
            var options = new List<WizardOptionEntry>();
            foreach (var g in Data.Groups)
            {
                options.AddRange(g.Options);
            }
            OptionsList.ItemsSource = options;

            // We then use WPF's grouping functionality to group them by their GroupName property.
            var view = (CollectionView)CollectionViewSource.GetDefaultView(OptionsList.ItemsSource);
            var groupDescription = new PropertyGroupDescription("GroupName");
            view.GroupDescriptions.Clear();
            view.GroupDescriptions.Add(groupDescription);

            var idx = 0;
            if(opt != null)
            {
                idx = options.IndexOf(opt);
            }
            OptionsList.SelectedIndex = idx;
        }

        /// <summary>
        /// Event handler for options list selection changed
        /// </summary>
        private void OptionsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(OptionSelected)));

            var opt = OptionsList.SelectedItem as WizardOptionEntry;
            if (opt == null) return;

            OptionDescriptionTextBox.Text = opt.Description;

            if (!String.IsNullOrWhiteSpace(opt.ImagePath))
            {
                var uri = new Uri(opt.ImagePath);
                OptionPreviewImage.Source = new BitmapImage(uri);
            }
            else
            {
                OptionPreviewImage.Source = null;
            }
        }

        private void SelectAllButton_Click(object sender, RoutedEventArgs e)
        {
            SelectAllOptions(true);
        }

        private void DeselectAllButton_Click(object sender, RoutedEventArgs e)
        {
            SelectAllOptions(false);
        }

        private void SelectAllOptions(bool isSelected)
        {
            foreach (var item in OptionsList.ItemsSource)
            {
                var modOption = (WizardOptionEntry)item;
                if (modOption.OptionType == EOptionType.Multi)
                {
                    modOption.Selected = isSelected;
                }
            }
        }

        private void AddGroup_Click(object sender, RoutedEventArgs e)
        {
            Data.Groups.Add(new WizardGroupEntry());
            SetupUi();
        }

        private void EditGroup_Click(object sender, RoutedEventArgs e)
        {
            // Open thing.
            var opt = OptionsList.SelectedItem as WizardOptionEntry;
            if (opt == null) return;

            var owningGroup = Data.Groups.FirstOrDefault(x => x.Options.Any(o => o == opt));
            if (owningGroup == null) return;

            var wind = new EditWizardGroupWindow(owningGroup);
            wind.Owner = Window.GetWindow(this);
            wind.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            wind.ShowDialog();
        }

        private void DeleteGroup_Click(object sender, RoutedEventArgs e)
        {
            var opt = OptionsList.SelectedItem as WizardOptionEntry;
            if (opt == null) return;

            var owningGroup = Data.Groups.FirstOrDefault(x => x.Options.Any(o => o == opt));
            if (owningGroup == null) return;

            Data.Groups.Remove(owningGroup);

            SetupUi();
        }

        private void MoveGroupUp_Click(object sender, RoutedEventArgs e)
        {
            var opt = OptionsList.SelectedItem as WizardOptionEntry;
            if (opt == null) return;

            var owningGroup = Data.Groups.FirstOrDefault(x => x.Options.Any(o => o == opt));
            if (owningGroup == null) return;

            var idx = Data.Groups.IndexOf(owningGroup);
            if (idx == 0) return;

            var lowerGroup = Data.Groups[idx - 1];
            Data.Groups[idx] = lowerGroup;
            Data.Groups[idx - 1] = owningGroup;
            SetupUi();
        }

        private void MoveGroupDown_Click(object sender, RoutedEventArgs e)
        {
            var opt = OptionsList.SelectedItem as WizardOptionEntry;
            if (opt == null) return;

            var owningGroup = Data.Groups.FirstOrDefault(x => x.Options.Any(o => o == opt));
            if (owningGroup == null) return;

            var idx = Data.Groups.IndexOf(owningGroup);
            if (idx >= Data.Groups.Count -2) return;

            var higherGroup = Data.Groups[idx + 1];
            Data.Groups[idx] = higherGroup;
            Data.Groups[idx + 1] = owningGroup;
            SetupUi();
        }
    }

    /// <summary>
    /// Template selector class for items in the options list
    /// </summary>
    public class ImportSelectionTemplateSelector : DataTemplateSelector
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
                var selectionType = ((WizardOptionEntry)item).OptionType;

                if (selectionType == EOptionType.Single)
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
