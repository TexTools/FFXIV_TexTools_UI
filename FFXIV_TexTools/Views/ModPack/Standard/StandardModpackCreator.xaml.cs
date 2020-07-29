using FFXIV_TexTools.ViewModels;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using xivModdingFramework.Cache;
using xivModdingFramework.Items.Interfaces;

namespace FFXIV_TexTools.Views
{
    /// <summary>
    /// Interaction logic for CompleteModpackCreator.xaml
    /// </summary>
    public partial class StandardModpackCreator : Window
    {
        public readonly StandardModpackViewModel ViewModel;

        private IItem _inProgressItem;
        private XivDependencyLevel _inProgressLevel;
        public StandardModpackCreator()
        {
            ViewModel = new StandardModpackViewModel(this);
            this.DataContext = ViewModel;
            InitializeComponent();

            ShowItemSelect();
        }

        // Show initial page.
        private void ShowItemSelect()
        {
            var homePage = new StandardModpackCreatorItemSelect(ViewModel);
            homePage.ItemSelected += HomePage_ItemSelected;
            this.Content = homePage;
        }

        // They selected an item (or pressed cancel)
        private void HomePage_ItemSelected(object sender, xivModdingFramework.Items.Interfaces.IItem e)
        {
            if(e == null)
            {
                // Cancel was pressed, exit.
                Close();
                return;
            }

            _inProgressItem = e;
            var levelSelect = new StandardModpackLevelSelect(_inProgressItem);
            levelSelect.LevelSelected += LevelSelect_LevelSelected;
            this.Content = levelSelect;
        }

        // They selected a level (or pressed back)
        private void LevelSelect_LevelSelected(object sender, xivModdingFramework.Cache.XivDependencyLevel e)
        {
            if (e == XivDependencyLevel.Invalid) {
                // Back button was pressed, return to item screen.
                ShowItemSelect();
                return;
            }
            _inProgressLevel = e;
            var fileSelect = new StandardModpackFileSelect(_inProgressItem, _inProgressLevel);
            fileSelect.FilesSelected += FileSelect_FilesSelected;
            this.Content = fileSelect;
        }

        // They selected some files (or pressed back)
        private void FileSelect_FilesSelected(object sender, ObservableCollection<string> e)
        {
            if(e == null || e.Count == 0)
            {
                // Back button pressed, return them to the level select screen.
                HomePage_ItemSelected(this, _inProgressItem);
            }

            var entry = new StandardModpackItemEntry(_inProgressItem, _inProgressLevel, e);
            ViewModel.Entries.Add(entry);

            // Entry added, back to the item select now.
            ShowItemSelect();
        }
    }
}
