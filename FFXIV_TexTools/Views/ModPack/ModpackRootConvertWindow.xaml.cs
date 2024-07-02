using FFXIV_TexTools.Resources;
using FFXIV_TexTools.Views.Controls;
using MahApps.Metro;
using MahApps.Metro.Controls;
using SharpDX;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Runtime.CompilerServices;
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
using xivModdingFramework.General.Enums;
using xivModdingFramework.Helpers;
using xivModdingFramework.Items.Enums;
using xivModdingFramework.Items.Interfaces;
using xivModdingFramework.Mods;
using xivModdingFramework.Mods.DataContainers;
using xivModdingFramework.SqPack.DataContainers;
using static xivModdingFramework.Mods.FileTypes.ModPackImportSettings;

namespace FFXIV_TexTools.Views
{
    /// <summary>
    /// Interaction logic for ModpackRootConvertWindow.xaml
    /// </summary>
    public partial class ModpackRootConvertWindow : Window
    {
        Dictionary<XivDependencyRoot, List<IItemModel>> Items;

        ModTransaction _Transaction;

        public Dictionary<XivDependencyRoot, (XivDependencyRoot Root, int Variant)> Results;
        public Dictionary<XivDependencyRoot, (IItemModel SourceItem, IItemModel DestinationItem)> ItemSelections;
        public Dictionary<XivDependencyRoot, (ComboBox SourceItemSelection, TextBox DestinationItemBox, CheckBox EnabledCheckBox, CheckBox VariantCheckBox, Button SearchButton)> UiElements;


        public ModpackRootConvertWindow(ModTransaction tx)
        {
            var appStyle = ThemeManager.DetectAppStyle(Application.Current);
            var theme = ((AppTheme)appStyle.Item1);
            if (theme.Name.Equals("BaseDark"))
            {
            }
            _Transaction = tx;

            InitializeComponent();


            Results = new Dictionary<XivDependencyRoot, (XivDependencyRoot Root, int Variant)>();
            Items = new Dictionary<XivDependencyRoot, List<IItemModel>>();
            ItemSelections = new Dictionary<XivDependencyRoot, (IItemModel SourceItem, IItemModel DestinationItem)>();
            UiElements = new Dictionary<XivDependencyRoot, (ComboBox SourceItemSelection, TextBox DestinationItemBox, CheckBox EnabledCheckBox, CheckBox VariantCheckBox, Button SearchButton)>();

            // Async init function
            Closing += ModpackRootConvertWindow_Closing;
        }

        private void ModpackRootConvertWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (null != Owner)
            {
                Owner.Activate();
            }
        }

        private bool Init(HashSet<string> filePaths)
        {
            List<XivDependencyRoot> roots = new List<XivDependencyRoot>();
            Task.Run(async () =>
            {
                var metaFiles = filePaths.Where(x => x.EndsWith(".meta")).OrderBy(x => x);

                var tx = MainWindow.DefaultTransaction;
                foreach (var file in metaFiles)
                {
                    var root = await XivCache.GetFirstRoot(file);
                    if (root != null && RootCloner.IsSupported(root))
                    {
                        var items = await root.GetAllItems(-1, tx);
                        Items.Add(root, items);
                        ItemSelections.Add(root, (items[0], items[0]));

                        var df = IOUtil.GetDataFileFromPath(root.Info.GetRootFile());

                        var models = await root.GetModelFiles(_Transaction);

                        // If any models are missing, don't allow conversions.
                        if(models.Any(x => !filePaths.Contains(x)))
                        {
                            continue;
                        }


                        if (items[0].ModelInfo != null)
                        {
                            Results.Add(root, (root, items[0].ModelInfo.ImcSubsetID));
                        } else
                        {
                            Results.Add(root, (root, -1));
                        }
                        roots.Add(root);
                    }
                }
            }).Wait();

            foreach(var root in roots)
            {
                PrimaryStackPanel.Children.Add(MakeRootGrid(root));
            }


            return roots.Count > 0;
        }

        private Grid MakeRootGrid(XivDependencyRoot root)
        {

            var items = Items[root];
            var selected = ItemSelections[root];

            var top = new Grid();
            top.DataContext = root;


            var gBox = new GroupBox();
            top.Children.Add(gBox);

            var txt = root.Info.GetBaseFileName(true);
            txt = txt.Replace('_', ' ');
            if (items.Count > 0)
            {
                txt += " - " + items[0];
            }
            gBox.Header = txt;
            gBox.Margin = new Thickness(10);

            var g = new Grid();
            gBox.Content = g;

            g.Width = 700;
            g.Height = 80;
            g.RowDefinitions.Add(new RowDefinition() { Height = new GridLength(40) });
            g.RowDefinitions.Add(new RowDefinition() { Height = new GridLength(40) });

            g.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(250) });
            g.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(50) });
            g.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(250) });
            g.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(150) });



            var destinationCheckbox = new CheckBox();
            destinationCheckbox.Content = "Change Destination Item".L();
            destinationCheckbox.IsChecked = false;

            g.Children.Add(destinationCheckbox);
            destinationCheckbox.SetValue(Grid.RowProperty, 0);
            destinationCheckbox.SetValue(Grid.ColumnProperty, 2);
            destinationCheckbox.SetValue(Grid.ColumnSpanProperty, 2);
            destinationCheckbox.Margin = new Thickness(5, 0, 5, 0);
            destinationCheckbox.VerticalAlignment = VerticalAlignment.Center;

            var variantCheckbox = new CheckBox();
            variantCheckbox.Content = "Make All Variants Identical".L();
            variantCheckbox.IsChecked = true;

            g.Children.Add(variantCheckbox);
            variantCheckbox.SetValue(Grid.RowProperty, 0);
            variantCheckbox.SetValue(Grid.ColumnProperty, 0);
            variantCheckbox.SetValue(Grid.ColumnSpanProperty, 2);
            variantCheckbox.Margin = new Thickness(5, 0, 5, 0);
            variantCheckbox.VerticalAlignment = VerticalAlignment.Center;
            variantCheckbox.IsEnabled = false;

            var srcCb = new ComboBox();
            g.Children.Add(srcCb);
            srcCb.SetValue(Grid.RowProperty, 1);
            srcCb.SetValue(Grid.ColumnProperty, 0);
            srcCb.VerticalAlignment = VerticalAlignment.Center;

            srcCb.ItemsSource = Items[root];
            srcCb.DisplayMemberPath = "Name";
            srcCb.SelectedValuePath = "ModelInfo.ImcSubsetID";

            srcCb.SelectedItem = selected.SourceItem;
            srcCb.Margin = new Thickness(5, 0, 5, 0);
            srcCb.IsEnabled = false;
            srcCb.ToolTip = "The original item(s) this mod applied to.".L();


            var dst = new TextBox();
            g.Children.Add(dst);
            dst.SetValue(Grid.RowProperty, 1);
            dst.SetValue(Grid.ColumnProperty, 2);
            dst.VerticalAlignment = VerticalAlignment.Center;
            dst.Text = selected.DestinationItem.Name;
            dst.IsReadOnly = true;
            dst.Margin = new Thickness(5, 0, 5, 0);
            dst.IsEnabled = false;
            dst.ToolTip = "The new item(s) this mod will apply to instead.".L();

            var lb = new Label();
            g.Children.Add(lb);
            lb.Content = "=>";
            lb.VerticalAlignment = VerticalAlignment.Center;
            lb.SetValue(Grid.RowProperty, 1);
            lb.SetValue(Grid.ColumnProperty, 1);
            lb.Margin = new Thickness(5, 0, 5, 0);

            var btn = new Button();
            g.Children.Add(btn);
            btn.VerticalAlignment = VerticalAlignment.Center;
            btn.SetValue(Grid.RowProperty, 1);
            btn.SetValue(Grid.ColumnProperty, 3);
            btn.Margin = new Thickness(5,0,5,0);
            btn.Content = "Choose New Item".L();
            btn.IsEnabled = false;

            var btn2 = new Button();
            g.Children.Add(btn2);
            btn2.VerticalAlignment = VerticalAlignment.Center;
            btn2.SetValue(Grid.RowProperty, 0);
            btn2.SetValue(Grid.ColumnProperty, 3);
            btn2.Margin = new Thickness(5, 0, 5, 0);
            btn2.Content = "Show Affected Items".L();
            btn2.IsEnabled = true;

            variantCheckbox.Checked += VariantBox_Checked;
            variantCheckbox.Unchecked += VariantBox_Checked;
            btn.Click += ItemSearch_Click;
            btn2.Click += AffectedItems_Click;
            srcCb.SelectionChanged += SrcCb_SelectionChanged;
            destinationCheckbox.Checked += DestinationCheckbox_Checked;
            destinationCheckbox.Unchecked += DestinationCheckbox_Checked;

            UiElements.Add(root, (srcCb, dst, destinationCheckbox, variantCheckbox, btn));


            return top;
        }

        private async void AffectedItems_Click(object sender, RoutedEventArgs e)
        {
            var root = ((XivDependencyRoot)((Button)e.Source).DataContext);
            if (root == null) return;

            var destRoot = Results[root].Root;

            var items = new List<IItemModel>();
            var ui = UiElements[root];

            if (ui.EnabledCheckBox.IsChecked == true)
            {
                items = await destRoot.GetAllItems(-1, MainWindow.DefaultTransaction);
            } else
            {
                items = await root.GetAllItems(-1, MainWindow.DefaultTransaction);
            }


            var strings = items.Select(x => x.Name).ToList();

            var wind = new AffectedFilesView(strings, "Affected Items".L()) { Owner = this };
            wind.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            wind.Show();

        }

        private void DestinationCheckbox_Checked(object sender, RoutedEventArgs e)
        {
            var root = ((XivDependencyRoot)((CheckBox)e.Source).DataContext);
            if (root == null) return;
            var ui = UiElements[root];
            var cb = ui.EnabledCheckBox;

            var on = cb.IsChecked == true ? true : false;
            
            ui.VariantCheckBox.IsEnabled = on;
            ui.DestinationItemBox.IsEnabled = on;
            ui.SearchButton.IsEnabled = on;

            if (on && ui.VariantCheckBox.IsChecked == true)
            {
                ui.SourceItemSelection.IsEnabled = true;
            } else
            {
                ui.SourceItemSelection.IsEnabled = false;
            }
        }

        private void SrcCb_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var root = ((XivDependencyRoot)((ComboBox)e.Source).DataContext);
            if (root == null) return;
            var ui = UiElements[root];
            var cb = ui.SourceItemSelection;
            var item = ((IItemModel)cb.SelectedItem);
            if (item == null) return;

            ItemSelections[root] = (item, ItemSelections[root].DestinationItem);


            Results[root] = (Results[root].Root, item.ModelInfo.ImcSubsetID);
        }

        private void VariantBox_Checked(object sender, RoutedEventArgs e)
        {
            var root = ((XivDependencyRoot)((CheckBox)e.Source).DataContext);
            if (root == null) return;
            var ui = UiElements[root];
            var cb = ui.VariantCheckBox;

            var on = cb.IsChecked == true ? true : false;

            if (on)
            {
                ui.SourceItemSelection.IsEnabled = true;

                var item = ((IItemModel)ui.SourceItemSelection.SelectedItem);
                if (item == null) return;

                var srcItem = ItemSelections[root].SourceItem;
                Results[root] = (Results[root].Root, item.ModelInfo.ImcSubsetID);
            } else
            {
                ui.SourceItemSelection.IsEnabled = false;
                ui.SourceItemSelection.SelectedIndex = 0;

                Results[root] = (Results[root].Root, -1);
            }
        }

        private void ItemSearch_Click(object sender, RoutedEventArgs e)
        {
            var root = ((XivDependencyRoot) ((Button)e.Source).DataContext);
            if (root == null) return;

            var srcItem = ItemSelections[root].SourceItem;

            var selectedItem = PopupItemSelection.ShowItemSelection((IItem item, XivDependencyRoot root) =>
            {
                return ValidationFunction(item, root);
            }, (IItem item, XivDependencyRoot root) =>
            {
                return ValidationFunction(item, root);
            }, this);

            if (selectedItem == null) return;

            var im = (IItemModel)selectedItem;
            if (im == null) return;

            var selectedRoot = im.GetRoot();
            if (selectedRoot == null) return;

            var ui = UiElements[root];

            ui.DestinationItemBox.DataContext = selectedItem;
            ui.DestinationItemBox.Text = selectedItem.Name;

            ItemSelections[root] = (ItemSelections[root].SourceItem, im);
            Results[root] = (selectedRoot, Results[root].Variant);
        }

        private static bool ValidationFunction(IItem item, XivDependencyRoot root) {
            // Item Select Acceptance
            if (item == null) return false;
            if (root == null) return false;

            var itemRoot = item.GetRoot();
            if (itemRoot == null) return false;

            if(root.Info.PrimaryType == XivItemType.equipment || root.Info.PrimaryType == XivItemType.accessory)
            {
                if(itemRoot.Info.PrimaryType == XivItemType.accessory)
                {
                    // Allow converting most things to accessories.
                    return true;
                }
            }

            if (itemRoot.Info.PrimaryType == root.Info.PrimaryType &&
                itemRoot.Info.SecondaryType == root.Info.SecondaryType &&
                itemRoot.Info.Slot == root.Info.Slot)
            {
                return true;
            }

            return false;
        }



        /// <summary>
        /// Wrapper that binds the window owner to the delegate function.
        /// </summary>
        /// <param name="owner"></param>
        /// <returns></returns>
        public static RootConversionDelegate GetRootConversionFunction(Window owner)
        {
            RootConversionDelegate del = async (HashSet<string> files, ModTransaction tx) =>
            {
                return await GetRootConversions(files, tx, owner);
            };

            return del;
        }

        /// <summary>
        /// Displays the dialog window to the user, returning the final result after completion, or throwing an error if the user cancelled.
        /// </summary>
        /// <param name="files"></param>
        /// <param name="indexFiles"></param>
        /// <param name="modlist"></param>
        /// <returns></returns>
        public static async Task<Dictionary<XivDependencyRoot, (XivDependencyRoot Root, int Variant)>> GetRootConversions(HashSet<string> files, ModTransaction tx, Window owner  = null)
        {
            Dictionary<XivDependencyRoot, (XivDependencyRoot Root, int Variant)> result = null;
            var mw = MainWindow.GetMainWindow();
            mw.Invoke(() =>
            {
                ModpackRootConvertWindow window;
                try
                {

                    window = new ModpackRootConvertWindow(tx);
                    if (owner == null)
                    {
                        if (ViewHelpers.IsWindowOpen(mw))
                        {
                            window.Owner = mw;
                            window.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                        }
                        else
                        {
                            window.WindowStartupLocation = WindowStartupLocation.CenterScreen;
                        }
                    } else
                    {
                        window.Owner = owner;
                        window.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                    }
                } catch(Exception ex)
                {
                    window = new ModpackRootConvertWindow(tx);
                    window.WindowStartupLocation = WindowStartupLocation.CenterScreen;
                }

                var anyRoots = window.Init(files);

                if(!anyRoots)
                {
                    return;
                }

                var dialogResult = window.ShowDialog();
                if (dialogResult != true)
                {
                    throw new OperationCanceledException("User Cancelled Import Process.".L());
                }

                result = window.Results;
            });
            return result;

        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }

        private void ContinueButton_Click(object sender, RoutedEventArgs e)
        {
            foreach(var kv in UiElements)
            {
                if (kv.Value.EnabledCheckBox.IsChecked != true)
                {
                    // Don't included disabled items.
                    Results.Remove(kv.Key);
                }
            }

            DialogResult = true;
        }
    }

}
