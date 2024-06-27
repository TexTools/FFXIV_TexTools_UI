using FFXIV_TexTools.ViewModels;
using System.Collections.Generic;
using System.Threading.Tasks;
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

            this._viewModel = new SharedItemsViewModel(PrimaryTree);
            this.DataContext = this._viewModel;
        }

        public async Task<bool> SetItem(IItem item)
        {
            return await _viewModel.SetItem(item);
        }

    }
}