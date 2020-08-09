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

using SixLabors.ImageSharp.Formats.Png;
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
        private readonly Dictionary<string, Image> _imageDictionary;

        public ImportWizardModPackControl(ModPackPageJson modPackPage, Dictionary<string, Image> imageDictionary)
        {
            InitializeComponent();

            MainWindow.MakeHighlander();

            _imageDictionary = imageDictionary;

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
                    BitmapImage bmp;

                    using (var ms = new MemoryStream())
                    {
                        _imageDictionary[option.ImagePath].Save(ms, new PngEncoder());

                        bmp = new BitmapImage();
                        bmp.BeginInit();
                        bmp.StreamSource = ms;
                        bmp.CacheOption = BitmapCacheOption.OnLoad;
                        bmp.EndInit();
                        bmp.Freeze();
                    }

                    OptionPreviewImage.Source = bmp;
                }
                else
                {
                    OptionPreviewImage.Source = null;
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
