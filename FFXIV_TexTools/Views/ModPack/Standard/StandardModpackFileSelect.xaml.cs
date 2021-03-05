using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using xivModdingFramework.Cache;
using xivModdingFramework.General.Enums;
using xivModdingFramework.Items.Enums;
using xivModdingFramework.Items.Interfaces;
using xivModdingFramework.SqPack.FileTypes;
using xivModdingFramework.Variants.FileTypes;

using Index = xivModdingFramework.SqPack.FileTypes.Index;

namespace FFXIV_TexTools.Views
{
    /// <summary>
    /// Interaction logic for StandardModpackFileSelect.xaml
    /// </summary>
    public partial class StandardModpackFileSelect : Page
    {

        public class FileEntry
        {
            private static readonly Regex _raceRegx = new Regex("c([0-9]{4})");
            private static readonly Regex _variantMatch = new Regex("v([0-9]{4})");
            public FileEntry(string path)
            {
                File = path;

                var fName = System.IO.Path.GetFileName(File);
                DisplayName = fName; // File name first.

                var match = _variantMatch.Match(path);
                if (match.Success)
                {
                    var variant = match.Groups[1].Value;
                    DisplayName += " - v" + variant; 
                }
                match = _raceRegx.Match(fName);
                if (match.Success)
                {
                    var race = XivRaces.GetXivRace(match.Groups[1].Value);
                    DisplayName += " - " + race.GetDisplayName();
                }

            }
            public string DisplayName { get; set; }
            public string File { get; set; }
        }

        public event EventHandler<ObservableCollection<string>> FilesSelected;
        private ObservableCollection<FileEntry> Files = new ObservableCollection<FileEntry>();

        private IItem _item;
        private XivDependencyLevel _level;

        public StandardModpackFileSelect(IItem item, XivDependencyLevel level)
        {
            DataContext = this;
            InitializeComponent();
            _item = item;
            _level = level;


            ItemName.Content = _item.Name + " - " + StandardModpackCreator.GetNiceLevelName(_level) + " Level";
            ItemLevel.Content = "Select " + StandardModpackCreator.GetNiceLevelName(_level) + " Files";

            FilesListBox.ItemsSource = Files;
            FilesListBox.DisplayMemberPath = "DisplayName";
            FilesListBox.SelectedValuePath = "File";
            FilesListBox.SelectionMode = SelectionMode.Multiple;
            NextButton.IsEnabled = false;

            FilesListBox.SelectionChanged += FilesListBox_SelectionChanged;

            NextButton.Click += NextButton_Click;
            BackButton.Click += BackButton_Click;

            LoadItems();
        }

        private void FilesListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (FilesListBox.SelectedItems == null || FilesListBox.SelectedItems.Count == 0)
            {
                NextButton.IsEnabled = false;

            }
            else
            {
                NextButton.IsEnabled = true;
            }
        }

        private async Task LoadItems()
        {

            List<string> children = new List<string>();
            var root = _item.GetRoot();
            if (root != null)
            {
                if (_level == XivDependencyLevel.Model)
                {
                    children = await root.GetModelFiles();
                }
                else if (_level == XivDependencyLevel.Material)
                {
                    var imc = new Imc(XivCache.GameInfo.GameDirectory);
                    try
                    {
                        var entry = await imc.GetImcInfo((IItemModel)_item);
                        children = await root.GetMaterialFiles(entry.MaterialSet);
                    } catch
                    {
                        if(root.Info.SecondaryType == XivItemType.hair
                            || root.Info.SecondaryType == XivItemType.tail
                            || (root.Info.PrimaryType == XivItemType.human && root.Info.SecondaryType == XivItemType.body))
                        {
                            // These types don't have IMC entries, but have a material variant number.
                            // Kind of weird, but whatever.
                            children = await root.GetMaterialFiles(1);
                        } else
                        {
                            children = await root.GetMaterialFiles(0);
                        }
                    }
                }
                else if (_level == XivDependencyLevel.Texture)
                {
                    try
                    {
                        var imc = new Imc(XivCache.GameInfo.GameDirectory);
                        var entry = await imc.GetImcInfo((IItemModel)_item);
                        children = await root.GetTextureFiles(entry.MaterialSet);
                    }
                    catch
                    {
                        if (root.Info.SecondaryType == XivItemType.hair
                            || root.Info.SecondaryType == XivItemType.tail
                            || (root.Info.PrimaryType == XivItemType.human && root.Info.SecondaryType == XivItemType.body))
                        {
                            // These types don't have IMC entries, but have a material variant number.
                            // Kind of weird, but whatever.
                            children = await root.GetTextureFiles(1);
                        }
                        else
                        {
                            children = await root.GetTextureFiles(0);
                        }
                    }
                }
                else
                {
                    // Invalid or root, nothing listed.
                }
            }

            var index = new Index(XivCache.GameInfo.GameDirectory);
            foreach(var file in children)
            {
                var exists = await index.FileExists(file);
                if (!exists) continue;

                Files.Add(new FileEntry(file));
            }
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            if(FilesSelected != null)
            {
                FilesSelected.Invoke(this, null);
            }
        }

        private void NextButton_Click(object sender, RoutedEventArgs e)
        {
            if (FilesListBox.SelectedItems == null || FilesListBox.SelectedItems.Count == 0) return;

            if(FilesSelected != null)
            {
                var results = new ObservableCollection<string>();
                foreach( var item in FilesListBox.SelectedItems)
                {
                    results.Add(((FileEntry)item).File);
                }

                FilesSelected.Invoke(this, results);
            }
        }
    }
}
