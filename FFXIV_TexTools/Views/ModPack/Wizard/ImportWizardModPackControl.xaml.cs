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
using System.Windows.Media.Imaging;
using xivModdingFramework.Mods.DataContainers;
using Image = SixLabors.ImageSharp.Image;

namespace FFXIV_TexTools.Views
{
    /// <summary>
    /// Interaction logic for ImportWizardModPackControl.xaml
    /// </summary>
    public partial class ImportWizardModPackControl : UserControl
    {
        private readonly Dictionary<string, string> _imgageTempFileDictionary = new Dictionary<string, string>();

        public ImportWizardModPackControl(ModPackPageJson modPackPage, Dictionary<string, Image> imageDictionary)
        {
            InitializeComponent();

            MainWindow.MakeHighlander();


            // Just immediately save all the images to the temp folder, because the loading times from ImageSharp are absolutely
            // awful, and/or leak RAM all over the place otherwise.
            var tempFolder = Path.GetTempPath();
            foreach (var kv in imageDictionary)
            {
                try
                {
                    var newfName = Path.Combine(tempFolder, Guid.NewGuid().ToString());
                    using (var fstream = new FileStream(newfName, FileMode.OpenOrCreate))
                    {
                        kv.Value.SaveAsBmp(fstream);
                    }
                    _imgageTempFileDictionary.Add(kv.Key, newfName);
                } catch(Exception ex)
                {
                    // No-Op
                }
            }


            OptionsList.ItemsSource = new List<ModOptionJson>();

            foreach (var modGroupJson in modPackPage.ModGroups)
            {
                if (modGroupJson.SelectionType.Equals("Single"))
                {
                    var checkedOption = modGroupJson.OptionList.SingleOrDefault(it => it.IsChecked);
                    if (checkedOption==null)
                    {
                        modGroupJson.OptionList[0].IsChecked = true;
                    }
                }

                ((List<ModOptionJson>)OptionsList.ItemsSource).AddRange(modGroupJson.OptionList);
            }

            var view = (CollectionView)CollectionViewSource.GetDefaultView(OptionsList.ItemsSource);
            var groupDescription = new PropertyGroupDescription("GroupName");
            view.GroupDescriptions.Clear();
            view.GroupDescriptions.Add(groupDescription);

            OptionsList.SelectedIndex = 0;
        }

        /// <summary>
        /// Event handler for options list selection changed
        /// </summary>
        private void OptionsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (OptionsList.SelectedItem is ModOptionJson option)
            {
                OptionDescriptionTextBox.Text = option.Description ?? string.Empty;

                if (!option.ImagePath.Equals(string.Empty))
                {
                    if(_imgageTempFileDictionary.ContainsKey(option.ImagePath))
                    {
                        try
                        {
                            var path = _imgageTempFileDictionary[option.ImagePath];
                            BitmapImage bi3 = new BitmapImage();
                            bi3.BeginInit();
                            bi3.UriSource = new Uri(path, UriKind.Absolute);
                            bi3.EndInit();

                            OptionPreviewImage.Source = bi3;
                        } catch(Exception ex)
                        {
                            // No-Op
                        }
                    }
                }
                else
                {
                    OptionPreviewImage.Source = null;
                }
            }
        }
        private void Option_Toggled(object sender, RoutedEventArgs e)
        {
            var s = (FrameworkElement)sender;
            var modOption = (ModOptionJson)s.DataContext;
            OptionsList.SelectedItem = modOption;
        }

        ~ImportWizardModPackControl()
        {
            try
            {
                // Clean up temp files.
                foreach (var kv in _imgageTempFileDictionary)
                {
                    File.Delete(kv.Value);
                }
            }
            catch
            {
                // No-Op
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
                var modOption = (ModOptionJson)item;
                if (!modOption.SelectionType.Equals("Single"))
                {
                    modOption.IsChecked = isSelected;
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
                var selectionType = ((ModOptionJson)item).SelectionType;

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
