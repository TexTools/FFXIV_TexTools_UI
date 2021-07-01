using FFXIV_TexTools.ViewModels;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
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
using xivModdingFramework.Mods;
using xivModdingFramework.Mods.DataContainers;

namespace FFXIV_TexTools.Views
{
    /// <summary>
    /// Interaction logic for BackupModpackCreator.xaml
    /// </summary>
    public partial class BackupModpackCreator
    {
        private readonly DirectoryInfo _gameDirectory;
        private readonly ModList _modList;

        public BackupModpackCreator()
        {
            InitializeComponent();

            _gameDirectory = new DirectoryInfo(Properties.Settings.Default.FFXIV_Directory);
            var modding = new Modding(_gameDirectory);
            _modList = modding.GetModList();

            DataContext = new BackupModpackViewModel(_modList);
            ModpackList.ItemsSource = new List<ModPack>();


            ((List<ModPack>)ModpackList.ItemsSource).AddRange(_modList.ModPacks);
            ModpackList.SelectedIndex = 0;
        }

        private void SelectAllButton_Click(object sender, RoutedEventArgs e)
        {

        }

        private void ClearSelectedButton_Click(object sender, RoutedEventArgs e)
        {

        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void CreateModPackButton_Click(object sender, RoutedEventArgs e)
        {

        }

        private void ModpackList_SelectionChanged(object sender, RoutedEventArgs e)
        {
            (DataContext as BackupModpackViewModel).UpdateDescription((ModPack)ModpackList.SelectedItem);
        }
    }
}
