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

using FFXIV_TexTools.ViewModels;
using xivModdingFramework.General.Enums;
using xivModdingFramework.Items.Interfaces;
using xivModdingFramework.Models.DataContainers;

namespace FFXIV_TexTools.Views.Models
{
    /// <summary>
    /// Interaction logic for AdvancedModelImportView.xaml
    /// </summary>
    public partial class AdvancedModelImportView
    {
        private AdvancedImportViewModel _viewModel;
        private bool _fromWizard;

        public AdvancedModelImportView(XivMdl xivMdl, IItemModel itemModel, XivRace selectedRace, bool fromWizard)
        {
            InitializeComponent();

            _fromWizard = fromWizard;
            _viewModel = new AdvancedImportViewModel(xivMdl, itemModel, selectedRace, this, fromWizard);
            this.DataContext = _viewModel;

            if (fromWizard)
            {
                //esrinzou for chinese UI
                //Title = "Advanced Model Options";
                //ImportButton.Content = "Add";
                //esrinzou begin
                Title = FFXIV_TexTools.Resources.UIStrings.Advanced_Model_Options;
                ImportButton.Content = FFXIV_TexTools.Resources.UIStrings.Add;
                //esrinzou end
            }
        }

        public byte[] RawModelData { get; set; }

        /// <summary>
        /// Event Handler for Cancel Button Click
        /// </summary>
        private void Button_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        /// <summary>
        /// Event Handler for Import Button Click
        /// </summary>
        private void ImportButton_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            DialogResult = true;

            if (_fromWizard)
            {
                RawModelData = _viewModel.RawModelData;
            }

            Close();
        }
    }
}
