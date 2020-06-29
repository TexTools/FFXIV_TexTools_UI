using FFXIV_TexTools.ViewModels;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using xivModdingFramework.Items.Interfaces;
using xivModdingFramework.Materials.DataContainers;

namespace FFXIV_TexTools.Views
{
    /// <summary>
    /// Interaction logic for MaterialEditor.xaml
    /// </summary>
    public partial class SharedItemsView : UserControl
    {
        private SharedItemsViewModel _viewModel;
        public SharedItemsView()
        {
            InitializeComponent();

            this._viewModel = new SharedItemsViewModel(this);
            this.DataContext = this._viewModel;
        }

    }
}