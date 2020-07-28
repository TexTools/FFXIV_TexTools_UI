using System;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Timers;
using xivModdingFramework.Cache;
using xivModdingFramework.Items.Interfaces;
using System.Windows.Input;
using System.Collections.Generic;
using System.ComponentModel;
using xivModdingFramework.General.Enums;
using xivModdingFramework.Items.Categories;
using FFXIV_TexTools.Resources;
using System.Collections.ObjectModel;
using System.Windows.Data;
using System.Linq;
using xivModdingFramework.Items.Enums;
using xivModdingFramework.Items.DataContainers;
using xivModdingFramework.Models.FileTypes;
using System.Windows;
using FFXIV_TexTools.Helpers;

namespace FFXIV_TexTools.Views.Controls
{

    /// <summary>
    /// Interaction logic for ItemSelectControl.xaml
    /// </summary>
    public partial class ItemSelectControl : UserControl
    {

        private bool _mainMenuMode;
        public bool MainMenuMode {
            get
            {
                return _mainMenuMode;
            }
            set
            {
                _mainMenuMode = value;
                if (value)
                {
                    SelectItemLabel.Visibility = Visibility.Collapsed;
                    SelectButton.Visibility = Visibility.Collapsed;
                    SearchBar.SetValue(Grid.ColumnSpanProperty, 2);
                    SearchBar.SetValue(Grid.RowProperty, 0);
                    Tabs.SetValue(Grid.RowSpanProperty, 2);
                } else
                {
                    SelectItemLabel.Visibility = Visibility.Visible;
                    SelectButton.Visibility = Visibility.Visible;
                    SearchBar.SetValue(Grid.ColumnSpanProperty, 1);
                    SearchBar.SetValue(Grid.RowProperty, 2);
                    Tabs.SetValue(Grid.RowSpanProperty, 1);
                }
            }
        }

        public bool DeferLoading
        {
            get; set;
        }

        private Dictionary<string, ItemTreeElement> DependencyRootNodes = new Dictionary<string, ItemTreeElement>();


        private bool _READY = false;
        private bool _SILENT = false;

        // Fired when tree finishes loading.
        public event EventHandler ItemsLoaded;

        // Fired whenever the selected item (actually) changes.
        public event EventHandler ItemSelected;

        // Fired whenever the user clicks the confirmation, or double clicks an item.
        public event EventHandler ItemConfirmed;

        // Fired whenever the search filter activates.
        public event EventHandler FilterChanged;

        Timer SearchTimer;
        private ObservableCollection<ItemTreeElement> CategoryElements = new ObservableCollection<ItemTreeElement>();
        private ObservableCollection<ItemTreeElement> SetElements = new ObservableCollection<ItemTreeElement>();

        private IItem _selectedItem;
        public IItem SelectedItem
        {
            get
            {
                return _selectedItem;
            }
            set
            {
                SelectItem(value);
            }
        }

        public ItemSelectControl() : this(null)
        {

        }
        public ItemSelectControl(bool? deferLoading = null)
        {

            DataContext = this;
            InitializeComponent();


            SelectButton.Click += SelectButton_Click;
            SearchBar.KeyDown += SearchBar_KeyDown;
            CategoryTree.SelectedItemChanged += CategoryTree_Selected;
            SetTree.SelectedItemChanged += CategoryTree_Selected;
            SearchBar.TextChanged += SearchBar_TextChanged;
            Loaded += ItemSelectControl_Loaded;

        }

        private void ItemSelectControl_Loaded(object sender, System.Windows.RoutedEventArgs e)
        {
            // This is done here because the DeferLoading property will not actually be
            // Populated from our parent XAML file in the constructor yet.
            if (!DeferLoading)
            {
                // Async'ing this is fine, it'll load when it's done.
                // All the other functions are safety checked on the _READY var.
                LoadItems();
            }
        }

        ~ItemSelectControl()
        {
            if (SearchTimer != null)
            {
                SearchTimer.Stop();
                SearchTimer.Dispose();
            }
        }


        /// <summary>
        /// Builds the set tree from XivCache.GetAllRoots().
        /// </summary>
        private void BuildSetTree()
        {

            // First we must generate all the dependency root nodes.
            var roots = XivCache.GetAllRoots();
            var primaryTypeGroups = new Dictionary<XivItemType, ItemTreeElement>();
            var primaryIdGroups = new Dictionary<XivItemType, Dictionary<int, ItemTreeElement>>();
            var secondaryTypeGroups = new Dictionary<XivItemType, Dictionary<int, Dictionary<XivItemType, ItemTreeElement>>>();
            var secondaryIdGroups = new Dictionary<XivItemType, Dictionary<int, Dictionary<XivItemType, Dictionary<int, ItemTreeElement>>>>();

            // This giant for loop monstrosity builds the actual root nodes based on the dictionaries returned by XivCache.GetAllRoots()
            foreach (var kvPrimaryType in roots)
            {
                var primaryType = kvPrimaryType.Key;

                // Create the new node.
                primaryTypeGroups.Add(primaryType, new ItemTreeElement(null, null, XivItemTypes.NiceNames[primaryType]));

                // Add us to parent.
                SetElements.Add(primaryTypeGroups[primaryType]);

                // Ensure the other lists have our primary type reference.
                primaryIdGroups.Add(primaryType, new Dictionary<int, ItemTreeElement>());
                secondaryTypeGroups.Add(primaryType, new Dictionary<int, Dictionary<XivItemType, ItemTreeElement>>());
                secondaryIdGroups.Add(primaryType, new Dictionary<int, Dictionary<XivItemType, Dictionary<int, ItemTreeElement>>>());

                foreach (var kvPrimaryId in kvPrimaryType.Value)
                {
                    var primaryId = kvPrimaryId.Key;

                    // Create the new node.
                    primaryIdGroups[primaryType].Add(primaryId, new ItemTreeElement(null, primaryTypeGroups[primaryType], XivItemTypes.GetSystemPrefix(primaryType) + primaryId.ToString().PadLeft(4, '0')));

                    // Add us to parent.
                    primaryTypeGroups[primaryType].Children.Add(primaryIdGroups[primaryType][primaryId]);

                    // Ensure the other lists have our primary id reference.
                    secondaryTypeGroups[primaryType].Add(primaryId, new Dictionary<XivItemType, ItemTreeElement>());
                    secondaryIdGroups[primaryType].Add(primaryId, new Dictionary<XivItemType, Dictionary<int, ItemTreeElement>>());

                    foreach (var kvSecondaryType in kvPrimaryId.Value)
                    {
                        var secondaryType = kvSecondaryType.Key;

                        if (secondaryType != XivItemType.none)
                        {
                            // Create the new node.
                            secondaryTypeGroups[primaryType][primaryId].Add(secondaryType, new ItemTreeElement(null, primaryIdGroups[primaryType][primaryId], XivItemTypes.NiceNames[secondaryType]));

                            // Add us to parent.
                            primaryIdGroups[primaryType][primaryId].Children.Add(secondaryTypeGroups[primaryType][primaryId][secondaryType]);

                            // Ensure the other lists have our secondary type reference.
                            secondaryIdGroups[primaryType][primaryId].Add(secondaryType, new Dictionary<int, ItemTreeElement>());
                        }

                        foreach (var kvSecondaryId in kvSecondaryType.Value)
                        {
                            var secondaryId = kvSecondaryId.Key;

                            if (secondaryType != XivItemType.none)
                            {
                                // Create the new node.
                                secondaryIdGroups[primaryType][primaryId][secondaryType].Add(secondaryId, new ItemTreeElement(null, secondaryTypeGroups[primaryType][primaryId][secondaryType], XivItemTypes.GetSystemPrefix(secondaryType) + secondaryId.ToString().PadLeft(4, '0')));

                                // Add us to parent.
                                secondaryTypeGroups[primaryType][primaryId][secondaryType].Children.Add(secondaryIdGroups[primaryType][primaryId][secondaryType][secondaryId]);
                            }

                            foreach (var kvSlot in kvSecondaryId.Value)
                            {
                                var root = kvSlot.Value;


                                var slotName = Mdl.SlotAbbreviationDictionary.FirstOrDefault(x => x.Value == root.Slot).Key;
                                slotName = String.IsNullOrWhiteSpace(slotName) ? "--" : slotName;

                                if (secondaryType != XivItemType.none)
                                {
                                    // Create the new node.
                                    var elem = new ItemTreeElement(null, secondaryIdGroups[primaryType][primaryId][secondaryType][secondaryId], slotName);

                                    // Add us to parent.
                                    secondaryIdGroups[primaryType][primaryId][secondaryType][secondaryId].Children.Add(elem);

                                    // Save us to the primary listing so the items can list themselves under us.
                                    DependencyRootNodes.Add(root.ToString(), elem);
                                }
                                else
                                {
                                    // Create the new node.
                                    var elem = new ItemTreeElement(null, primaryIdGroups[primaryType][primaryId], slotName);

                                    // Add us to parent.
                                    primaryIdGroups[primaryType][primaryId].Children.Add(elem);

                                    // Save us to the primary listing so the items can list themselves under us.
                                    DependencyRootNodes.Add(root.ToString(), elem);
                                }
                            }
                        }
                    }
                }
            }
        }

        private async Task BuildCategoryTree()
        {

            foreach(var kv in _categoryStructure)
            {
                // Make the top level node.
                var e = new ItemTreeElement(null, null, kv.Key);

                foreach(var secondary in kv.Value)
                {
                    var e2 = new ItemTreeElement(e, null, secondary);
                    e.Children.Add(e2);
                }
                CategoryElements.Add(e);
            }

            var gameDir = XivCache.GameInfo.GameDirectory;
            var language = XivCache.GameInfo.GameLanguage;

            var items = new List<IItem>();


            var gear = new Gear(gameDir, language);
            var companions = new Companions(gameDir, language);
            var housing = new Housing(gameDir, language);
            var ui = new UI(gameDir, language);
            var character = new Character(gameDir, language);


            items.AddRange(await gear.GetGearList());
            items.AddRange(await character.GetCharacterList());
            items.AddRange(await companions.GetMinionList());
            items.AddRange(await companions.GetPetList());
            items.AddRange(await ui.GetUIList());
            items.AddRange(await housing.GetFurnitureList());

            if (language != XivLanguage.Chinese && language != XivLanguage.Korean)
            {
                // I don't remember why we needed to set this this without the filter for CN, but carrying it through for now.
                items.AddRange(await companions.GetMountList());
            }
            else
            {
                items.AddRange(await companions.GetMountList(null, XivStrings.Mounts));
                items.AddRange(await companions.GetMountList(null, XivStrings.Ornaments));
            }

            foreach(var item in items)
            {
                ItemTreeElement catParent = null;
                var topLevel = CategoryElements.FirstOrDefault(x => x.DisplayName == item.PrimaryCategory);
                if(topLevel == null)
                {
                    topLevel = new ItemTreeElement(null, null, item.PrimaryCategory);
                    CategoryElements.Add(topLevel);
                }

                var secondLevel = topLevel.Children.FirstOrDefault(x => x.DisplayName == item.SecondaryCategory);
                if (secondLevel == null)
                {
                    secondLevel = new ItemTreeElement(topLevel, null, item.SecondaryCategory);
                    topLevel.Children.Add(secondLevel);
                }

                catParent = secondLevel;

                ItemTreeElement setParent = null;
                try
                {
                    var type = item.GetType();
                    // Perf.  Much faster to just not test those types at all, as we know they won't resolve.
                    if (type != typeof(XivUi) && type != typeof(XivFurniture))
                    {
                        var itemRoot = item.GetItemRootInfo();
                        if (itemRoot.PrimaryType != XivItemType.unknown)
                        {

                            var st = itemRoot.ToString();
                            if (DependencyRootNodes.ContainsKey(st))
                            {
                                setParent = DependencyRootNodes[st];
                            }
                        }
                    }
                } catch(Exception ex)
                {
                    throw;
                }

                var e2 = new ItemTreeElement(catParent, setParent, item);
                if(catParent != null)
                {
                    catParent.Children.Add(e2);
                }
                if(setParent != null)
                {
                    setParent.Children.Add(e2);
                }
            }

        }

        /// <summary>
        /// This should only really be called directly if the control was created with DeferLoading set to true.
        /// </summary>
        /// <returns></returns>
        public async Task LoadItems()
        {
            if (_READY)
            {
                SearchTimer.Stop();
                SearchTimer.Dispose();
                ClearSelection();
            }
            CategoryElements = new ObservableCollection<ItemTreeElement>();
            SetElements = new ObservableCollection<ItemTreeElement>();
            DependencyRootNodes = new Dictionary<string, ItemTreeElement>();

            CategoryTree.ItemsSource = CategoryElements;
            SetTree.ItemsSource = SetElements;

            try
            {
                BuildSetTree();
                await BuildCategoryTree();
            } catch(Exception ex)
            {
                FlexibleMessageBox.Show("Item List Error", "An error occurred while loading the item list.\n" + ex.Message, System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Warning);
                return;
            }



            // This is a little slow/computation heavy, so better to not lock the main thread during it.
            // It's also not actually necessary for general minute 0 functionality, so we can go ahead and
            // Let the user at least browse the list first.
            await Task.Run(() => {
               foreach (var kv in DependencyRootNodes)
               {
                    // This dependency root had no EXD-Items associated with it.
                    // Gotta make a generic item for it.
                    if (kv.Value.Children.Count == 0)
                   {
                        // See if we can actually turn this root into a fully fledged item.
                        try
                       {
                           var root = XivCache.CreateDependencyRoot(kv.Key);
                           if (root != null)
                           {
                                // If we can, add it into the list.
                                var item = root.ToItem();
                               var e = new ItemTreeElement(null, kv.Value, item);
                               kv.Value.Children.Add(e);
                           }
                       }
                       catch (Exception ex)
                       {
                           throw;
                       }

                   }
               }
           });



            var view = (CollectionView)CollectionViewSource.GetDefaultView(CategoryElements);
            view.Filter = SearchFilter;


            view = (CollectionView)CollectionViewSource.GetDefaultView(SetElements);
            view.Filter = SearchFilter;

            SearchTimer = new Timer(300);
            SearchTimer.Elapsed += Search;

            _READY = true;
            if (ItemsLoaded != null)
            {
                ItemsLoaded.Invoke(this, null);
            }
        }

        private void CategoryTree_Selected(object sender, System.Windows.RoutedPropertyChangedEventArgs<object> e)
        {
            if (!_READY) return;
            if (_SILENT) return;

            var z = (e.OriginalSource);
            // Nothing actually changed.
            if (_selectedItem == e.NewValue)
                return;

            var oldE = (ItemTreeElement)e.OldValue;
            var newE = (ItemTreeElement)e.NewValue;
            if (newE != null
             && _selectedItem == null && newE.Item == null)
            {
                // If both actual underlying items were null
                // we didn't really change anything.
                return;
            }

            // Don't allow null selections to escape this controller.
            if (newE == null || newE.Item == null) return;

            _selectedItem = newE == null ? null : newE.Item;
            ItemSelected.Invoke(this, null);
        }

        private ItemTreeElement FindElement(IItem item, ObservableCollection<ItemTreeElement> elements = null) {

            if (!_READY) return null;

            if (elements == null)
            {
                elements = CategoryElements;
            }

            foreach (var e in elements) {
                if (e.Children.Count > 0)
                {
                    var found = FindElement(item, e.Children);
                    if (found != null)
                    {
                        return found;
                    }
                }
                else
                {
                    if (item.Name == e.Item.Name)
                    {
                        return e;
                    }
                }
            }

            return null;
        }

        private void SelectItem(IItem item)
        {
            if (!_READY) return;
            if (item == null) return;

            // Supress the changes generated by ClearSelection.
            _SILENT = true;
            ClearSelection();
            _SILENT = false;

            var e = FindElement(item);
            if (e != null)
            {
                e.IsSelected = true;
                _selectedItem = item;
            }
            else
            {

                if (_selectedItem != item)
                {
                    _selectedItem = item;
                    ItemSelected.Invoke(this, null);
                }

            }
        }


        public void ClearSelection(ObservableCollection<ItemTreeElement> elements = null)
        {
            if (!_READY) return;

            if (elements == null)
            {
                elements = CategoryElements;
            }

            foreach (var e in elements)
            {
                e.IsSelected = false;
                if (e.Children.Count > 0)
                {
                    ClearSelection(e.Children);
                }
            }
        }


        private void Search(object sender, ElapsedEventArgs e)
        {
            if (!_READY) return;
            SearchTimer.Stop();

            // Do stuff.
            Dispatcher.Invoke(() =>
            {
                CollectionViewSource.GetDefaultView(CategoryElements).Refresh();
                CollectionViewSource.GetDefaultView(SetElements).Refresh();

                if (FilterChanged != null)
                {
                    FilterChanged.Invoke(this, null);
                }
            });
        }

        private void AcceptSelection()
        {
            if (!_READY) return;
            if (ItemSelected != null)
            {
                ItemConfirmed.Invoke(this, null);
            }
        }

        private void SearchBar_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (!_READY) return;

            if (e.Key == Key.Return)
            {
                Search(null, null);
            }
        }
        private void SearchBar_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!_READY) return;
            SearchTimer.Stop();
            SearchTimer.Start();
        }

        private void SelectButton_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            if (!_READY) return;

            AcceptSelection();
        }

        private bool SearchFilter(object o)
        {
            if (!_READY) return true;

            var e = (ItemTreeElement)o;
            if (e.Children.Count > 0)
            {
                var subItems = (CollectionView)CollectionViewSource.GetDefaultView((e).Children);

                subItems.Filter = SearchFilter;

                e.IsExpanded = !string.IsNullOrEmpty(SearchBar.Text);

                return !subItems.IsEmpty;
            }

            var searchTerms = SearchBar.Text.Split(' ');

            return searchTerms.All(term => e.DisplayName.ToLower().Contains(term.Trim().ToLower()));
        }

        private readonly Dictionary<string, List<string>> _categoryStructure = new Dictionary<string, List<string>>()
        {
            { XivStrings.Gear, new List<string>() {
                {XivStrings.Head },
                {XivStrings.Body },
                {XivStrings.Hands },
                {XivStrings.Legs },
                {XivStrings.Feet },
                {XivStrings.Main_Hand },
                {XivStrings.Off_Hand },
                {XivStrings.Dual_Wield },
                {XivStrings.Two_Handed },
                {XivStrings.Earring },
                {XivStrings.Neck },
                {XivStrings.Wrists },
                {XivStrings.Rings },
                {XivStrings.Body_Hands_Legs_Feet },
                {XivStrings.Body_Hands_Legs },
                {XivStrings.Body_Legs_Feet },
                {XivStrings.Head_Body },
                {XivStrings.Legs_Feet },
                {XivStrings.All },
                {XivStrings.Food }
            } },
            { XivStrings.Character, new List<string>() {
                { XivStrings.Body },
                { XivStrings.Face },
                { XivStrings.Hair },
                { XivStrings.Tail },
                { XivStrings.Ear },
                { XivStrings.Face_Paint },
                { XivStrings.Equipment_Decals }
            } },
            { XivStrings.Companions, new List<string>() {
                { XivStrings.Minions },
                { XivStrings.Mounts },
                { XivStrings.Pets },
                { XivStrings.Ornaments },
            } },
            { XivStrings.UI, new List<string>() {
                { XivStrings.Actions },
                { XivStrings.Loading_Screen },
                { XivStrings.Maps },
                { XivStrings.Map_Symbols },
                { XivStrings.Online_Status },
                { XivStrings.Status },
                { XivStrings.Weather },
                { XivStrings.HUD }
            } },
            { XivStrings.Housing, new List<string>() {
                { XivStrings.Indoor_Furniture },
                { XivStrings.Paintings },
                { XivStrings.Outdoor_Furniture },
            } }
        };
    }
    public class ItemTreeElement : INotifyPropertyChanged
    {
        public readonly IItem Item;

        /// <summary>
        /// Constructor for an empty header cateogory
        /// </summary>
        /// <param name="name"></param>
        /// <param name="children"></param>
        public ItemTreeElement(ItemTreeElement itemParent, ItemTreeElement depencencyParent, string name)
        {
            CategoryParent = itemParent;
            SetParent = depencencyParent;
            _backupName = name;
            Children = new ObservableCollection<ItemTreeElement>();
        }


        /// <summary>
        /// Constructor for an actual valid item.
        /// </summary>
        /// <param name="i"></param>
        public ItemTreeElement(ItemTreeElement itemParent, ItemTreeElement depencencyParent, IItem i)
        {
            CategoryParent = itemParent;
            SetParent = depencencyParent;
            Item = i;
            Children = new ObservableCollection<ItemTreeElement>();
        }

        private string _backupName;


        public string DisplayName
        {
            get
            {
                if(Item == null)
                {
                    return _backupName;
                } else
                {
                    return Item.Name;
                }
            }
        }

        public ItemTreeElement SetParent { get; set; }
        public ItemTreeElement CategoryParent { get; set; }
        public ObservableCollection<ItemTreeElement> Children { get; set; }

        private bool _isExpanded;
        private bool _isSelected;

        /// <summary>
        /// The expanded status of the category
        /// </summary>
        public bool IsExpanded
        {
            get => _isExpanded;
            set
            {
                _isExpanded = value;
                NotifyPropertyChanged(nameof(IsExpanded));
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// The selected status of the category
        /// </summary>
        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (value == true && _isSelected != true)
                {
                    //IsExpanded = true;
                }

                _isSelected = value;
                NotifyPropertyChanged(nameof(IsSelected));

            }
        }
        protected virtual void NotifyPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

}
