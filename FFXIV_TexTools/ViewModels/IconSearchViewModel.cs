// xivModdingFramework
// Copyright © 2018 Rafael Gonzalez - All Rights Reserved
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
using System.ComponentModel;
using System.IO;
using System.Windows.Data;
using System.Windows.Input;
using xivModdingFramework.General.Enums;
using xivModdingFramework.Helpers;
using xivModdingFramework.Items.DataContainers;
using xivModdingFramework.Mods;
using xivModdingFramework.SqPack.FileTypes;

namespace FFXIV_TexTools.ViewModels
{
    public class IconSearchViewModel : INotifyPropertyChanged
    {
        private string _iconText, _iconStatusLabel;
        private readonly MainWindow _mainView;

        public IconSearchViewModel(MainWindow mainView)
        {
            _mainView = mainView;
        }

        /// <summary>
        /// The Icon Search Text
        /// </summary>
        public string IconText
        {
            get => _iconText;
            set
            {
                _iconText = value;
                NotifyPropertyChanged(nameof(IconText));
            }
        }

        /// <summary>
        /// The Icon Status Label
        /// </summary>
        public string IconStatusLabel
        {
            get => _iconStatusLabel;
            set
            {
                _iconStatusLabel = value;
                NotifyPropertyChanged(nameof(IconStatusLabel));
            }
        }

        // Commands
        public ICommand OpenIconCommand => new RelayCommand(OpenIconClick);
        public ICommand TextBoxEnterCommand => new RelayCommand(TextBoxEnter);

        /// <summary>
        /// Command triggered when Enter key is hit in text box
        /// </summary>
        private void TextBoxEnter(object obj)
        {
            OpenIcon();
        }

        /// <summary>
        /// Command triggered when Open button is clicked
        /// </summary>
        /// <param name="obj"></param>
        private void OpenIconClick(object obj)
        {
            OpenIcon();
        }

        /// <summary>
        /// Attempts to open the icon
        /// </summary>
        private async void OpenIcon()
        {
            var tx = MainWindow.UserTransaction != null ? MainWindow.UserTransaction : ModTransaction.BeginTransaction();
            if (_mainView.TabsControl.SelectedIndex == 1)
            {
                _mainView.TabsControl.SelectedIndex = 0;
            }

            _mainView.ModelTabItem.IsEnabled = false;


            var iconInt = -1;
            try
            {
                if (IconText?.Length < 7)
                {
                    iconInt = int.Parse(IconText);
                    IconStatusLabel = string.Empty;
                }
                else
                {
                    IconText = string.Empty;
                    IconStatusLabel = UIStrings.UI_SearchStatus_Max;
                }
            }
            catch
            {
                IconText = string.Empty;
                IconStatusLabel = UIStrings.UI_SearchStatus_Numeric;
            }

            if (iconInt > -1)
            {
                var iconFileString = $"{IconText.PadLeft(6, '0')}.tex";
                var iconFolderInt = (iconInt / 1000) * 1000;
                var iconFolderString = $"ui/icon/{iconFolderInt.ToString().PadLeft(6, '0')}";
                var path = iconFolderString + "/" + iconFileString;

                if (await tx.FileExists(path))
                {
                    var textureView = _mainView.TextureTabItem.Content as TextureView;
                    var textureViewModel = textureView.DataContext as TextureViewModel;

                    var xivUI = new XivUi
                    {
                        Name = Path.GetFileNameWithoutExtension(iconFileString),
                        PrimaryCategory = XivStrings.UI,
                        SecondaryCategory = XivStrings.Icon,
                        TertiaryCategory = XivStrings.Icon,
                        IconNumber = iconInt,
                        DataFile = XivDataFile._06_Ui,
                        UiPath = $"{iconFolderString}/{iconFileString}"
                    };

                    await textureViewModel.UpdateTexture(xivUI);
                }
                else
                {
                    var iconLangFolderString = $"ui/icon/{iconFolderInt.ToString().PadLeft(6, '0')}/en";
                    if (await tx.FileExists(path))
                    {
                        var textureView = _mainView.TextureTabItem.Content as TextureView;
                        var textureViewModel = textureView.DataContext as TextureViewModel;

                        var xivUI = new XivUi
                        {
                            Name = Path.GetFileNameWithoutExtension(iconFileString),
                            PrimaryCategory = XivStrings.UI,
                            SecondaryCategory = XivStrings.Icon,
                            TertiaryCategory = XivStrings.Icon,
                            IconNumber = iconInt,
                            DataFile = XivDataFile._06_Ui,
                            UiPath = $"{iconFolderString}/{iconFileString}"
                        };

                        await textureViewModel.UpdateTexture(xivUI);
                    }
                    else
                    {
                        IconText = string.Empty;
                        IconStatusLabel = string.Format(UIStrings.UI_Search_NothingFound, iconInt);
                    }
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void NotifyPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}