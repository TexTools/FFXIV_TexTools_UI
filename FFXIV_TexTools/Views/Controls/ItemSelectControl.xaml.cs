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
using xivModdingFramework.Items;
using System.Runtime.CompilerServices;

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
                    SelectButton.Visibility = Visibility.Collapsed;
                    Tabs.SetValue(Grid.RowSpanProperty, 2);
                } else
                {
                    SelectButton.Visibility = Visibility.Visible;
                    Tabs.SetValue(Grid.RowSpanProperty, 1);
                }
            }
        }

        public bool DeferLoading
        {
            get; set;
        }

        public bool ExpandCharacterMenu
        {
            get; set;
        }

        private Dictionary<string, ItemTreeElement> DependencyRootNodes = new Dictionary<string, ItemTreeElement>();


        private bool _READY = false;
        private bool _SILENT = false;

        // Fired when tree finishes loading.
        public event EventHandler ItemsLoaded;

        // Fired whenever the selected item actually changes.  (NULLs and Duplicates are filtered out)
        public event EventHandler<IItem> ItemSelected;

        // Fired any time the UI selection event fires, regardless of the selection.
        public event EventHandler<IItem> RawItemSelected;

        // Fired whenever the user clicks the confirmation, or double clicks an item.
        public event EventHandler<IItem> ItemConfirmed;

        // Fired whenever the search filter activates.
        public event EventHandler<string> FilterChanged;


        // Expose our lock and unlock functions so sub-menus using us can replace them.
        public Func<string, string, object, Task> LockUiFunction;
        public Func<object, Task> UnlockUiFunction;

        public Func<IItem, bool> ExtraSearchFunction;

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

        public ItemSelectControl()
        {
            DataContext = this;
            InitializeComponent();

            if (MainWindow.GetMainWindow() != null)
            {
                LockUiFunction = MainWindow.GetMainWindow().LockUi;
                UnlockUiFunction = MainWindow.GetMainWindow().UnlockUi;
            }

            SelectButton.Click += SelectButton_Click;
            SearchBar.KeyDown += SearchBar_KeyDown;
            CategoryTree.SelectedItemChanged += CategoryTree_Selected;
            SetTree.SelectedItemChanged += CategoryTree_Selected;
            SearchBar.TextChanged += SearchBar_TextChanged;
            Loaded += ItemSelectControl_Loaded;

        }

        private void ItemSelectControl_Loaded(object sender, System.Windows.RoutedEventArgs e)
        {
            if (DesignerProperties.GetIsInDesignMode(this)) { return; }
            // This is done here because the DeferLoading property will not actually be
            // Populated from our parent XAML file in the constructor yet.
            if (!DeferLoading)
            {
                // Async'ing this is fine, it'll load when it's done.
                // All the other functions are safety checked on the _READY var.
                if (CategoryElements.Count == 0)
                {
                    LoadItems();
                }
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


        private Dictionary<XivItemType, Dictionary<XivRace, ItemTreeElement>> HumanParentNodes = new Dictionary<XivItemType, Dictionary<XivRace, ItemTreeElement>>();
        /// <summary>
        /// Builds the set tree from XivCache.GetAllRoots().
        /// </summary>
        private void BuildSetTree()
        {

            // First we must generate all the dependency root nodes.
            var roots = XivCache.GetAllRootsDictionary().OrderBy(x => x.Key);
            var primaryTypeGroups = new Dictionary<XivItemType, ItemTreeElement>();
            var primaryIdGroups = new Dictionary<XivItemType, Dictionary<int, ItemTreeElement>>();
            var secondaryTypeGroups = new Dictionary<XivItemType, Dictionary<int, Dictionary<XivItemType, ItemTreeElement>>>();
            var secondaryIdGroups = new Dictionary<XivItemType, Dictionary<int, Dictionary<XivItemType, Dictionary<int, ItemTreeElement>>>>();

            // This giant for loop monstrosity builds the actual root nodes based on the dictionaries returned by XivCache.GetAllRoots()
            foreach (var kvPrimaryType in roots)
            {
                var primaryType = kvPrimaryType.Key;

                // Create the new node.
                primaryTypeGroups.Add(primaryType, new ItemTreeElement(null, null, XivItemTypes.NiceNames[primaryType], true));

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
                    primaryIdGroups[primaryType].Add(primaryId, new ItemTreeElement(null, primaryTypeGroups[primaryType], XivItemTypes.GetSystemPrefix(primaryType) + primaryId.ToString().PadLeft(4, '0'), true));

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
                            secondaryTypeGroups[primaryType][primaryId].Add(secondaryType, new ItemTreeElement(null, primaryIdGroups[primaryType][primaryId], XivItemTypes.NiceNames[secondaryType], true));

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
                                secondaryIdGroups[primaryType][primaryId][secondaryType].Add(secondaryId, new ItemTreeElement(null, secondaryTypeGroups[primaryType][primaryId][secondaryType], XivItemTypes.GetSystemPrefix(secondaryType) + secondaryId.ToString().PadLeft(4, '0'), true));

                                // Add us to parent.
                                secondaryTypeGroups[primaryType][primaryId][secondaryType].Children.Add(secondaryIdGroups[primaryType][primaryId][secondaryType][secondaryId]);
                            }

                            foreach (var kvSlot in kvSecondaryId.Value)
                            {
                                var root = kvSlot.Value;


                                var slotName = Mdl.SlotAbbreviationDictionary.FirstOrDefault(x => x.Value == root.Slot).Key;

                                if (secondaryType != XivItemType.none)
                                {
                                    ItemTreeElement elem = null;
                                    // This root has no slots, just list the parent as the root element.
                                    if (String.IsNullOrWhiteSpace(slotName))
                                    {
                                        elem = secondaryIdGroups[primaryType][primaryId][secondaryType][secondaryId];
                                        DependencyRootNodes.Add(root.ToString(), elem);
                                    }
                                    else
                                    {
                                        // Create the new node.
                                        elem = new ItemTreeElement(null, secondaryIdGroups[primaryType][primaryId][secondaryType][secondaryId], slotName);

                                        // Add us to parent.
                                        secondaryIdGroups[primaryType][primaryId][secondaryType][secondaryId].Children.Add(elem);

                                        // Save us to the primary listing so the items can list themselves under us.
                                        DependencyRootNodes.Add(root.ToString(), elem);
                                    }

                                }
                                else
                                {
                                    // This root has no slots, just list the parent as the root element.
                                    if (String.IsNullOrWhiteSpace(slotName))
                                    {
                                        DependencyRootNodes.Add(root.ToString(), primaryIdGroups[primaryType][primaryId]);
                                        break;
                                    }

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

        private async Task<List<IItem>> BuildCategoryTree()
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

            var items = await XivCache.GetFullItemList();

            foreach(var item in items)
            {
                // Find what node we should be attached to.
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
                    if (item.SecondaryCategory == item.Name)
                    {
                        // These are a special snowflake case.
                        secondLevel = topLevel;
                    }
                    else
                    {
                        secondLevel = new ItemTreeElement(topLevel, null, item.SecondaryCategory);
                        topLevel.Children.Add(secondLevel);
                    }
                }

                catParent = secondLevel;

                ItemTreeElement setParent = null;

                // Try and see if we have a valid root parent to attach to in the sets tree.
                try
                {
                    var type = item.GetType();
                    // Perf.  Much faster to just not test those types at all, as we know they won't resolve.
                    if (type != typeof(XivUi))
                    {
                        var itemRoot = item.GetRootInfo();
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

                ItemTreeElement e2;
                if (ExpandCharacterMenu && typeof(XivCharacter) == item.GetType())
                {
                    var charItem = (XivCharacter)item;
                    if (charItem.ModelInfo != null && charItem.ModelInfo.PrimaryID > 0)
                    {
                        e2 = new ItemTreeElement(catParent, setParent, item.Name);
                    } else
                    {
                        e2 = new ItemTreeElement(catParent, setParent, item);
                    }
                } else
                {
                    e2 = new ItemTreeElement(catParent, setParent, item);
                }
                if(catParent != null)
                {
                    catParent.Children.Add(e2);
                }
                if(setParent != null)
                {
                    setParent.Children.Add(e2);
                }

                if (ExpandCharacterMenu)
                {
                    if (typeof(XivCharacter) == item.GetType())
                    {
                        // Cache the references to our human root nodes.
                        var charItem = (XivCharacter)item;
                        var type = charItem.GetSecondaryItemType();
                        if (type != XivItemType.none)
                        {
                            var raceCode = charItem.ModelInfo.PrimaryID;
                            var race = XivRaces.GetXivRace(raceCode);

                            if (!HumanParentNodes.ContainsKey(type))
                            {
                                HumanParentNodes.Add(type, new Dictionary<XivRace, ItemTreeElement>());
                            }

                            HumanParentNodes[type].Add(race, e2);
                        }
                    }
                }
            }
            return items;

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

            if (LockUiFunction != null)
            {
                await LockUiFunction(UIStrings.Loading_Items, null, this);
            }


            // Pump us into another thread so the UI stays nice and fresh.
            await Task.Run(async () =>
            {
                CategoryElements = new ObservableCollection<ItemTreeElement>();
                SetElements = new ObservableCollection<ItemTreeElement>();
                DependencyRootNodes = new Dictionary<string, ItemTreeElement>();

                try
                {
                    // Gotta build set tree first, so the items from the item list can latch onto the nodes there.
                    BuildSetTree();
                    await BuildCategoryTree();

                }
                catch (Exception ex)
                {
                    FlexibleMessageBox.Show("An error occurred while loading the item list.\n".L() + ex.Message, "Item List Error".L(), System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Warning);
                    return;
                }


                var toAdd = new List<(ItemTreeElement parent, ItemTreeElement child)>();
                foreach (var kv in DependencyRootNodes)
                {
                    // This dependency root had no EXD-Items associated with it.
                    // Gotta make a generic item for it.
                    if (kv.Value.Children.Count == 0)
                    {
                        // See if we can actually turn this root into a fully fledged item.
                        try
                        {
                            var root = await XivCache.GetFirstRoot(kv.Key);
                            ItemTreeElement e;
                            if (root != null)
                            {
                                // If we can, add it into the list.
                                var item = root.ToRawItem();
                                e = new ItemTreeElement(null, kv.Value, item);
                                toAdd.Add((kv.Value, e));

                                if (ExpandCharacterMenu && root.Info.PrimaryType == XivItemType.human)
                                {
                                    // Cache our human type elements if we need them later.
                                    var race = XivRaces.GetXivRace(root.Info.PrimaryId);
                                    var sType = (XivItemType)root.Info.SecondaryType;

                                    if (!HumanParentNodes.ContainsKey(sType)) continue;
                                    if (!HumanParentNodes[sType].ContainsKey(race)) continue;

                                    var parent = HumanParentNodes[sType][race];
                                    e.CategoryParent = parent;
                                    parent.Children.Add(e);
                                }
                            }
                            else
                            {
                                e = new ItemTreeElement(null, kv.Value, "[Unsupported]");
                                toAdd.Add((kv.Value, e));
                            }



                        }
                        catch (Exception ex)
                        {
                            throw;
                        }

                    }
                }

                // Loop back through to add the new items, so we're not affecting the previous iteration.
                foreach (var tup in toAdd)
                {
                    tup.parent.Children.Add(tup.child);
                }





            });

            var view = (CollectionView)CollectionViewSource.GetDefaultView(CategoryElements);
            view.Filter = SearchFilter;


            view = (CollectionView)CollectionViewSource.GetDefaultView(SetElements);
            view.Filter = SearchFilter;

            SearchTimer = new Timer(300);
            SearchTimer.Elapsed += Search;

            CategoryTree.ItemsSource = CategoryElements;
            SetTree.ItemsSource = SetElements;

            _READY = true;

            Search(this, null);

            if (UnlockUiFunction != null)
            {
                await UnlockUiFunction(this);
            }

            if (ItemsLoaded != null)
            {
                ItemsLoaded.Invoke(this, null);
            }
        }

        private void CategoryTree_Selected(object sender, System.Windows.RoutedPropertyChangedEventArgs<object> e)
        {
            if (!_READY) return;
            if (_SILENT) return;

            var oldE = (ItemTreeElement)e.OldValue;
            var newE = (ItemTreeElement)e.NewValue;

            if (RawItemSelected != null) {
                RawItemSelected.Invoke(null, newE == null ? null : newE.Item);
            }

            if (newE != null
             && _selectedItem == null && newE.Item == null)
            {
                // If both actual underlying items were null
                // we didn't really change anything.
                return;
            }

            // Don't allow null selections to escape this controller.
            if (newE == null || newE.Item == null) return;

            // If we re-selected the same item, selection doesn't escape this controller (didn't actually change).
            if (_selectedItem == newE.Item) return;

            _selectedItem = newE.Item;
            if (ItemSelected != null)
            {
                ItemSelected.Invoke(this, _selectedItem);
            }
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
                    if (item.Name == e.Item?.Name)
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

                // Manually invoke this in case the item isn't in the filter currently.
                ItemSelected.Invoke(this, _selectedItem);
            }
            else
            {
                // Item was not in the tree.
                if (_selectedItem != item)
                {
                    _selectedItem = item;
                    ItemSelected.Invoke(this, _selectedItem);
                }

            }
        }

        public void ClearSelection()
        {
            ClearSelection(SetElements);
            ClearSelection(CategoryElements);
        }
        public void DoFilter()
        {
            Search(this, null);
        }
        private void ClearSelection(ObservableCollection<ItemTreeElement> elements = null)

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
                ClearSelection(CategoryElements);
                ClearSelection(SetElements);
                CollectionViewSource.GetDefaultView(CategoryElements).Refresh();
                CollectionViewSource.GetDefaultView(SetElements).Refresh();

                if (FilterChanged != null)
                {
                    FilterChanged.Invoke(this, SearchBar.Text);
                }
            });
        }

        private void AcceptSelection()
        {
            if (!_READY) return;
            if (SelectedItem != null && ItemConfirmed != null)
            {
                ItemConfirmed.Invoke(this, _selectedItem);
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

        private bool IncludeAllSearchFilter(object o)
        {
            var e = (ItemTreeElement)o;
            if (e.Children.Count > 0)
            {
                var subItems = (CollectionView)CollectionViewSource.GetDefaultView((e).Children);
                subItems.Filter = IncludeAllSearchFilter;
                e.IsExpanded = !string.IsNullOrEmpty(SearchBar.Text);
            }
            return true;
        }

        private bool SearchFilter(object o)
        {
            if (!_READY) return true;

            var e = (ItemTreeElement)o;
            var groups = SearchBar.Text.Split('|');
            var iMatch = false;

            foreach (var group in groups)
            {
                var searchTerms = group.Split(' ');
                bool match = searchTerms.All(term => e.DisplayName.ToLower().Contains(term.Trim().ToLower()));
                iMatch = iMatch || match;
            }

            if (e.Children.Count > 0)
            {
                var subItems = (CollectionView)CollectionViewSource.GetDefaultView((e).Children);

                e.IsExpanded = !string.IsNullOrEmpty(SearchBar.Text);
                if (e.Searchable && iMatch)
                {
                    // If the actual group name matches the typed in name, include everything in that group.
                    // This is only allowed in the set menu, as there's some pretty generic words used in the
                    // category menu structure that muck this up. (Ex. Searching for "Hands" becomes impossible)
                    subItems.Filter = IncludeAllSearchFilter;
                    return true;
                }
                else
                {
                    subItems.Filter = SearchFilter;
                    return !subItems.IsEmpty;
                }
            }
            else if(e.Item != null)
            {
                if(ExtraSearchFunction != null)
                {
                    // If we have an extra search criteria supplied by an outside function, it has to pass that, too.
                    iMatch = ExtraSearchFunction(e.Item) && iMatch;
                }
            }


            return iMatch;
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
                /*{ XivStrings.Body },
                { XivStrings.Face },
                { XivStrings.Hair },
                { XivStrings.Tail },
                { XivStrings.Ear },
                { XivStrings.Face_Paint },
                { XivStrings.Equipment_Decals }*/ 
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
        public ItemTreeElement(ItemTreeElement itemParent, ItemTreeElement depencencyParent, string name, bool searchable = false)
        {
            CategoryParent = itemParent;
            SetParent = depencencyParent;
            _backupName = name;
            Searchable = searchable;
            Children = new ObservableCollection<ItemTreeElement>();
        }


        /// <summary>
        /// Constructor for an actual valid item.
        /// </summary>
        /// <param name="i"></param>
        public ItemTreeElement(ItemTreeElement itemParent, ItemTreeElement depencencyParent, IItem i, bool searchable = true)
        {
            CategoryParent = itemParent;
            SetParent = depencencyParent;
            Item = i;
            Searchable = searchable;
            Children = new ObservableCollection<ItemTreeElement>();
        }

        private string _backupName;


        public readonly bool Searchable;

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
