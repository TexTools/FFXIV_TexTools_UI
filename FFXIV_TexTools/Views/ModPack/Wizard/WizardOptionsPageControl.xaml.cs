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
    public partial class WizardOptionsPageControl : UserControl
    {

        public WizardOptionsPageControl(WizardOptionsPage data)
        {
            InitializeComponent();

            // We want one full unified list of options as a source for our listbox.
            var options = new List<WizardOptionDisplay>();
            foreach(var g in data.Groups)
            {
                options.AddRange(g.Options);
            }
            OptionsList.ItemsSource = options;

            // We then use WPF's grouping functionality to group them by their GroupName property.
            var view = (CollectionView)CollectionViewSource.GetDefaultView(OptionsList.ItemsSource);
            var groupDescription = new PropertyGroupDescription("GroupName");
            view.GroupDescriptions.Clear();
            view.GroupDescriptions.Add(groupDescription);

            // The SelectedIndex value is only used in updating the visual display.
            // It is not used as a final value in any capacity.
            OptionsList.SelectedIndex = 0;
        }

        /// <summary>
        /// Event handler for options list selection changed
        /// </summary>
        private void OptionsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var opt = OptionsList.SelectedItem as WizardOptionDisplay;
            if (opt == null) return;

            OptionDescriptionTextBox.Text = opt.Description;
            //opt.ImagePath;

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
                var modOption = (WizardOptionDisplay)item;
                if (!modOption.OptionType.Equals("Single"))
                {
                    modOption.Selected = isSelected;
                }
            }
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
                var selectionType = ((WizardOptionDisplay)item).OptionType;

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
