using FFXIV_TexTools.Resources;
using MahApps.Metro.Controls;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
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
using xivModdingFramework.Items.Interfaces;
using xivModdingFramework.Mods;
using xivModdingFramework.Mods.Enums;

namespace FFXIV_TexTools.Views
{
    /// <summary>
    /// Simple UI element that handles property notification and various sub-data about the files.
    /// </summary>
    public class UiWrappedFile : INotifyPropertyChanged
    {
        private static readonly Regex _extractRaceRegex = new Regex(".*c([0-9]{4})");
        public event PropertyChangedEventHandler PropertyChanged;

        public UiWrappedFile(string file, EModState? state = null, bool selected = false, ModTransaction tx = null)
        {
            _FilePath = file;
            _ModState = state;
            _Selected = selected;

            var root = XivCache.GetFilePathRoot(_FilePath);
            if (root != null)
            {
                var item = root.GetFirstItem();
                _Item = item;
            }

            GetModState(tx);
        }

        private async void GetModState(ModTransaction tx = null)
        {
            if (tx == null)
            {
                tx = MainWindow.DefaultTransaction;
            }
            UpdateModState(await Modding.GetModState(_FilePath, tx));
        }

        public void UpdateModState(EModState state)
        {
            _ModState = state;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ModState)));
        }

        private bool _Selected;
        public bool Selected
        {
            get
            {
                return _Selected;
            }
            set
            {
                _Selected = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Selected)));
            }
        }

        private string _FilePath;
        public string FilePath
        {
            get
            {
                return _FilePath;
            }
            set
            {
                _FilePath = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(FilePath)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(FileName)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(FileType)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(RaceGender)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ItemName)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ItemCategory)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ModState)));
            }
        }
        public string FileName
        {
            get
            {
                return System.IO.Path.GetFileName(_FilePath);
            }
        }
        public string FileType
        {
            get
            {
                var ext = System.IO.Path.GetExtension(_FilePath);
                switch (ext)
                {
                    case ".tex":
                    case ".dds":
                    case ".png":
                    case ".bmp":
                    case ".jpg":
                        ext = "Texture";
                        break;
                    case ".mtrl":
                        ext = "Material";
                        break;
                    case ".mdl":
                    case ".fbx":
                    case ".db":
                    case ".obj":
                        ext = "Model";
                        break;
                    case ".meta":
                        ext = "Metadata";
                        break;
                    case ".rgsp":
                        ext = "Racial Scaling";
                        break;
                    case ".avfx":
                        ext = "VFX";
                        break;
                    case ".atex":
                        ext = "VFX Texture";
                        break;
                }
                return ext;
            }
        }
        public string RaceGender
        {
            get
            {
                var res = _extractRaceRegex.Match(_FilePath);
                if (!res.Success)
                {
                    return "";
                }
                var race = XivRaces.GetXivRace(res.Groups[1].Value);
                return race.GetDisplayName();
            }
        }

        private IItem _Item;
        public string ItemName
        {
            get
            {
                if (_Item == null) return "";

                return _Item.Name;
            }
        }
        public string ItemCategory
        {
            get
            {
                if (_Item == null) return "";

                return _Item.PrimaryCategory;
            }
        }

        private EModState? _ModState;
        public string ModState
        {
            get
            {
                if (_ModState == null)
                {
                    return "";
                }
                else
                {
                    return _ModState.ToString();
                }
            }
        }
    }

}

namespace FFXIV_TexTools.Views.Controls
{
    /// <summary>
    /// Interaction logic for FileListControl.xaml
    /// </summary>
    public partial class FileListControl : UserControl, INotifyPropertyChanged
    {

        public event PropertyChangedEventHandler PropertyChanged;
        private ObservableCollection<UiWrappedFile> _Files = new ObservableCollection<UiWrappedFile>();


        public event EventHandler SelectionChanged;


        public string SelectedCountLabel
        {
            get
            {
                return _SelectedCount.ToString() + " File(s) Selected";
            }
        }

        private int _SelectedCount;

        public bool AnySelected
        {
            get
            {
                return _Files.Any(x => x.Selected);
            }
        }

        private ICollectionView _VisibleFiles;
        public ICollectionView VisibleFiles {
            get
            {
                return _VisibleFiles;
            }
            set
            {
                _VisibleFiles = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(VisibleFiles)));
            }
        }

        public FileListControl()
        {
            DataContext = this;
            DebouncedUpdate = ViewHelpers.Debounce<string>((s) =>
            {
                UpdateVisible();
            }, 300);

            InitializeComponent();

        }
        public void SetFiles(IEnumerable<string> files)
        {
            _Files = new ObservableCollection<UiWrappedFile>();
            var dist = files.Distinct();
            var tx = MainWindow.DefaultTransaction;
            foreach (var file in dist)
            {
                var uif = new UiWrappedFile(file, null, true, tx);
                uif.PropertyChanged += Uif_PropertyChanged;
                _Files.Add(uif);
            }

            _SelectedCount = _Files.Count;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectedCountLabel)));

            VisibleFiles = CollectionViewSource.GetDefaultView(_Files);
            VisibleFiles.Filter += FilterFiles;
            SortBy(nameof(UiWrappedFile.FilePath));
        }

        private void Uif_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            SelectionChanged?.Invoke(this, null);
            if (_SELECTED_INTERNAL) return;

            var uif = sender as UiWrappedFile;
            if (e.PropertyName == nameof(UiWrappedFile.Selected))
            {
                OnCheckboxChecked(uif);
            }
        }

        private bool _SELECTED_INTERNAL = false;
        private void OnCheckboxChecked(UiWrappedFile file)
        {
            var selectedInList = FileBox.SelectedItems.Contains(file);
            if (selectedInList)
            {
                _SELECTED_INTERNAL = true;
                // Match states if we're selecting stuff that are UI highlighted.
                foreach (UiWrappedFile item in FileBox.SelectedItems)
                {
                    item.Selected = file.Selected;
                }
                _SELECTED_INTERNAL = false;
            }
            _SelectedCount = _Files.Count(x => x.Selected);
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectedCountLabel)));
        }

        private bool FilterFiles(object obj)
        {
            var file = (UiWrappedFile)obj;
            return MeetsCriteria(file, _SearchText);
        }

        Action<string> DebouncedUpdate;
        private string _SearchText = "";
        private void SearchTextChanged(object sender, TextChangedEventArgs e)
        {
            _SearchText = SearchBox.Text.ToLower();
            DebouncedUpdate(SearchBox.Text);
        }

        private void UpdateVisible()
        {
            this.Invoke(() =>
            {
                _VisibleFiles.Refresh();
            });
        }

        private bool MeetsCriteria(UiWrappedFile file, string searchText)
        {
            if (file.FilePath.Contains(searchText))
            {
                return true;
            }

            if (file.FileType.Contains(searchText))
            {
                return true;
            }

            if (file.ItemName.ToLower().Contains(searchText))
            {
                return true;
            }

            if (file.ItemCategory.ToLower().Contains(searchText))
            {
                return true;
            }

            if (file.RaceGender.ToLower().Contains(searchText))
            {
                return true;
            }
            
            if (file.ModState.ToLower().Contains(searchText))
            {
                return true;
            }

            return false;
        }

        private ListSortDirection _SortDirection = ListSortDirection.Ascending;
        private string _SortProperty = "";

        private void SortBy(string prop = "")
        {
            if (string.IsNullOrWhiteSpace(prop))
            {
                prop = "FilePath";
            }

            if(prop == _SortProperty)
            {
                // Invert
                _SortDirection = _SortDirection == ListSortDirection.Ascending ? ListSortDirection.Descending : ListSortDirection.Ascending;
            }
            else
            {
                _SortDirection = ListSortDirection.Ascending;
            }
            _SortProperty = prop;

            _VisibleFiles.SortDescriptions.Clear();
            _VisibleFiles.SortDescriptions.Add(new SortDescription(prop, _SortDirection));

            if (prop != "FilePath")
            {
                // Tiebreaker.
                _VisibleFiles.SortDescriptions.Add(new SortDescription("FilePath", _SortDirection));
            }
        }
        private void Header_Click(object sender, RoutedEventArgs e)
        {
            var h = e.OriginalSource as GridViewColumnHeader;
            if (h != null && h.Content != null && !string.IsNullOrWhiteSpace((string)h.Content))
            {
                // This switch is shit but I couldn't find a better way to bind the selected header to the prop name
                // that wasn't wildly complicated/over-engineered.
                var prop = "";
                switch (h.Content)
                {
                    case "Item":
                        prop = nameof(UiWrappedFile.ItemName);
                        break;
                    case "Category":
                        prop = nameof(UiWrappedFile.ItemCategory);
                        break;
                    case "Race/Gender":
                        prop = nameof(UiWrappedFile.RaceGender);
                        break;
                    case "File Type":
                        prop = nameof(UiWrappedFile.FileType);
                        break;
                    case "File Name":
                        prop = nameof(UiWrappedFile.FileName);
                        break;
                    case "Mod Status":
                        prop = nameof(UiWrappedFile.ModState);
                        break;
                    case "File Path":
                        prop = nameof(UiWrappedFile.FilePath);
                        break;
                }

                SortBy(prop);
            }
        }

        private void SelectAll_Click(object sender, RoutedEventArgs e)
        {
            foreach (UiWrappedFile file in _Files)
            {
                file.Selected = true;
            }
        }

        private void ClearAll_Click(object sender, RoutedEventArgs e)
        {
            foreach (UiWrappedFile file in _Files)
            {
                file.Selected = false;
            }
        }

        private void SelectVisible_Click(object sender, RoutedEventArgs e)
        {
            foreach (UiWrappedFile file in VisibleFiles)
            {
                file.Selected = true;
            }
        }

        private void ClearVisible_Click(object sender, RoutedEventArgs e)
        {
            foreach (UiWrappedFile file in VisibleFiles)
            {
                file.Selected = false;
            }
        }

        public HashSet<string> GetSelectedFiles()
        {
            var selected = new HashSet<string>();
            foreach(var file in _Files)
            {
                if (file.Selected)
                {
                    selected.Add(file.FilePath);
                }
            }

            return selected;
        }
        public HashSet<string> GetUnselectedFiles()
        {
            var unselected = new HashSet<string>();
            foreach (var file in _Files)
            {
                if (!file.Selected)
                {
                    unselected.Add(file.FilePath);
                }
            }

            return unselected;
        }
    }
}
