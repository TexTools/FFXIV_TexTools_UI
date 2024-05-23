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
using FFXIV_TexTools.Views.Models;
using System.Data.Entity.Core.Metadata.Edm;
using System.Windows;
using System.Windows.Controls;

namespace FFXIV_TexTools.Views
{
    /// <summary>
    /// Interaction logic for ModelViewerOptionsView.xaml
    /// </summary>
    public partial class ModelViewerOptionsView : UserControl
    {
        private Viewport3DViewModel _vm
        {
            get
            {
                return DataContext as Viewport3DViewModel;
            }
        }

        public ModelViewerOptionsView()
        {
            InitializeComponent();
        }

        private void OpenShapesMenu_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            if (_vm == null) return;

            try
            {
                if (_vm.Model == null || !_vm.Model.HasShapeData) return;

                var wind = new ApplyShapesView(_vm.Model, _vm.ActiveShapes) { Owner = MainWindow.GetMainWindow() };
                wind.WindowStartupLocation = WindowStartupLocation.CenterOwner;

                var result = wind.ShowDialog();

                if (result != true) return;

                _vm.SetShapes(wind.SelectedShapes);
            }
            catch
            {
                //No op
            }

        }

        private void HighlightColorsetButton_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            if (_vm == null) return;

            try
            {
                var wind = new HighilightedColorsetSelection(_vm.HighlightedColorsetRow) { Owner = MainWindow.GetMainWindow() };
                wind.WindowStartupLocation = WindowStartupLocation.CenterOwner;

                var result = wind.ShowDialog();

                if (result != true) return;

                _vm.HighlightedColorsetRow = wind.SelectedRow;
            }
            catch
            {
                //No Op
            }
        }
    }
}
