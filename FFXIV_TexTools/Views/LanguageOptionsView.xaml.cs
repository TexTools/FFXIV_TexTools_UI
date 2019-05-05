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

using System.Windows.Controls;

namespace FFXIV_TexTools.Views
{
    /// <summary>
    /// Interaction logic for LanguageOptionsView.xaml
    /// </summary>
    public partial class LanguageOptionsView : UserControl
    {
        public LanguageOptionsView()
        {
            InitializeComponent();

            //esrinzou for chinese UI
            //CurrentLanguageLabel.Content = $"{XivLanguages.GetXivLanguage(Properties.Settings.Default.Application_Language)}";
            //esrinzou begin
            CurrentLanguageLabel.Content = System.Globalization.CultureInfo.CurrentUICulture.NativeName;
            //esrinzou end
        }

        /// <summary>
        /// Updates the application's language to the given one.
        /// </summary>
        /// <param name="language">Language name string</param>
        /// <param name="message">Message for the language change.</param>
        private void UpdateLanguage(string language, string message)
        {
            Properties.Settings.Default.Application_Language = language;
            Properties.Settings.Default.Save();

            Helpers.FlexibleMessageBox.Show(message);

            System.Windows.Forms.Application.Restart();
            System.Windows.Application.Current.Shutdown();
        }

        private void EnglishBtn_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            UpdateLanguage("en", "TexTools will now restart to apply the changes.");
        }

        private void JapaneseBtn_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            UpdateLanguage("ja", "TexToolsは変更を適用するためリスタートしました。");
        }

        private void GermanBtn_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            UpdateLanguage("de", "TexTools wurde neu gestartet, um die Änderungen anzuwenden.");
        }

        private void FrenchBtn_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            UpdateLanguage("fr", "TexTools a redémarré pour appliquer les modifications.");
        }

        private void KoreanBtn_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            UpdateLanguage("ko", "TexTools가 변경 사항을 적용하기 위해 재시작되었습니다.");
        }

        private void ChineseBtn_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            UpdateLanguage("zh", "TexTools重新启动以应用更改。");
        }
    }
}
