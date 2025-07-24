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

using System.Windows;

namespace FFXIV_TexTools.Views
{
    /// <summary>
    /// Interaction logic for LanguageSelectView.xaml
    /// </summary>
    public partial class LanguageSelectView
    {
        public LanguageSelectView()
        {
            InitializeComponent();
        }

        public string LanguageCode { get; set; }

        private void EnglishBtn_Click(object sender, RoutedEventArgs e)
        {
            LanguageCode = "en";
            Close();
        }

        private void JapaneseBtn_Click(object sender, RoutedEventArgs e)
        {
            LanguageCode = "ja";
            Close();
        }

        private void GermanBtn_Click(object sender, RoutedEventArgs e)
        {
            LanguageCode = "de";
            Close();
        }

        private void FrenchBtn_Click(object sender, RoutedEventArgs e)
        {
            LanguageCode = "fr";
            Close();
        }

        private void KoreanBtn_Click(object sender, RoutedEventArgs e)
        {
            LanguageCode = "ko";
            Close();
        }

        private void ChineseBtn_Click(object sender, RoutedEventArgs e)
        {
            LanguageCode = "zh";
            Close();
        }

        private void TraditionalChineseBtn_Click(object sender, RoutedEventArgs e)
        {
            LanguageCode = "tc";
            Close();
        }
    }
}
