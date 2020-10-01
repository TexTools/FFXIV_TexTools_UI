using FFXIV_TexTools.Helpers;
using MahApps.Metro.Controls;
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
using System.Windows.Interactivity;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using xivModdingFramework.Cache;
using xivModdingFramework.General.Enums;
using xivModdingFramework.Helpers;
using xivModdingFramework.Items;
using xivModdingFramework.Items.Interfaces;
using xivModdingFramework.Materials.FileTypes;
using xivModdingFramework.Models.FileTypes;
using xivModdingFramework.SqPack.FileTypes;
using xivModdingFramework.Variants.FileTypes;

namespace FFXIV_TexTools.Views.Controls
{
    /// <summary>
    /// Interaction logic for ItemRacialDisplay.xaml
    /// </summary>
    public partial class ItemInfoDisplay : Window
    {
        private IItemModel _item;

        private ObservableCollection<KeyValuePair<string, IItem>> SameVariantItems = new ObservableCollection<KeyValuePair<string, IItem>>();
        private ObservableCollection<KeyValuePair<string, IItem>> SameMSetItems = new ObservableCollection<KeyValuePair<string, IItem>>();
        private ObservableCollection<KeyValuePair<string, IItem>> SameModelItems= new ObservableCollection<KeyValuePair<string, IItem>>();
        public ItemInfoDisplay(IItemModel item)
        {
            _item = item;
            InitializeComponent();

            SameModelBox.ItemsSource = SameModelItems;
            SameMaterialBox.ItemsSource = SameMSetItems;
            SameVariantBox.ItemsSource = SameModelItems;

            SameModelBox.DisplayMemberPath = "Key";
            SameModelBox.SelectedValuePath = "Value";

            SameMaterialBox.DisplayMemberPath = "Key";
            SameMaterialBox.SelectedValuePath = "Value";

            SameVariantBox.DisplayMemberPath = "Key";
            SameVariantBox.SelectedValuePath = "Value";

            SameModelBox.MouseDoubleClick += SameModelBox_MouseDoubleClick;
            SameMaterialBox.MouseDoubleClick += SameModelBox_MouseDoubleClick;
            SameVariantBox.MouseDoubleClick += SameModelBox_MouseDoubleClick;

            try
            {
                AsyncInit();
            } catch(Exception Ex)
            {
                FlexibleMessageBox.Show("Unable to load item information:\n\nError:" + Ex.Message, "Item Information Error", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Warning);
            }
        }

        private void SameModelBox_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            var lb = (ListBox)sender;
            var selected = lb.SelectedItem;

            if (selected == null) return;

            var kv = (KeyValuePair<string, IItem>)selected;
            if (kv.Value == null) return;

            var mw = MainWindow.GetMainWindow();
            mw.SetSelectedItem(kv.Value);
        }

        public async Task AsyncInit()
        {
            var root = _item.GetRoot();
            if (root == null) return;

            var gd = XivCache.GameInfo.GameDirectory;
            var lang = XivCache.GameInfo.GameLanguage;
            var df = IOUtil.GetDataFileFromPath(root.Info.GetRootFile());

            var _index = new Index(gd);
            var _mtrl = new Mtrl(XivCache.GameInfo.GameDirectory);
            var _mdl = new Mdl(gd, df);
            var _imc = new Imc(gd);
            var raceRegex = new Regex("c([0-9]{4})[^b]");

            ItemNameBox.Text = _item.Name;

            var setName = root.Info.GetBaseFileName(false);

            SetLabel.Text = "Set: " + setName;

            if (!String.IsNullOrWhiteSpace(root.Info.Slot)) {
                var niceSlot = Mdl.SlotAbbreviationDictionary.FirstOrDefault(x => x.Value == root.Info.Slot);
                if (niceSlot.Key != null)
                {
                    SlotLabel.Text = "Slot: " + niceSlot.Key + " (" + root.Info.Slot + ")";
                } else
                {
                    SlotLabel.Text = "Slot: Unknown (" + root.Info.Slot + ")";
                }
            } else
            {
                SlotLabel.Text = "Slot: --";
            }

            var usesImc = Imc.UsesImc(_item);
            if (usesImc)
            {
                VariantLabel.Text = "Variant: " + _item.ModelInfo.ImcSubsetID;
            } else
            {
                VariantLabel.Text = "Variant: --";
            }

            var mSet = await _imc.GetMaterialSetId(_item);
            if (mSet > 0)
            {
                MaterialSetLabel.Text = "Material Set: " + mSet;
            } else
            {
                MaterialSetLabel.Text = "Material Set: --";
            }

            var races = XivRaces.PlayableRaces;

            var models = await root.GetModelFiles();
            var materials = await root.GetMaterialFiles(mSet);

            #region Race Chart
            var rowIdx = 1;
            foreach(var race in races)
            {
                var rCode = race.GetRaceCode();

                var row = new RowDefinition();
                row.Height = new GridLength(30);
                RacialGrid.RowDefinitions.Add(row);

                var lBase = new Label();
                lBase.Content = race.GetDisplayName();
                lBase.SetValue(Grid.RowProperty, rowIdx);

                RacialGrid.Children.Add(lBase);

                XivRace? usedMdlRace = race;

                string usedMdl = null; ;
                if (race != XivRace.All_Races)
                {
                    // Check if the race has a model.
                    var mdl = models.FirstOrDefault(x => x.Contains("c" + rCode));
                    if (mdl == null)
                    {
                        // Gotta see which race they're shared from.
                        var node = XivRaceTree.GetNode(race);
                        var parent = node.Parent;

                        while (parent != null)
                        {
                            var code = parent.Race.GetRaceCode();
                            mdl = models.FirstOrDefault(x => x.Contains("c" + code));
                            if (mdl != null)
                            {
                                usedMdlRace = parent.Race;
                                usedMdl = mdl;
                                break;
                            }
                            parent = parent.Parent;
                        }

                        if (mdl == null)
                        {
                            // No model exists for this item.
                            usedMdlRace = null;
                        }
                    }
                    else
                    {
                        usedMdl = mdl;
                    }
                }

                var mdlRaceString = "None";
                if(usedMdlRace == race)
                {
                    mdlRaceString = "Own";
                } else {
                    if (usedMdlRace != null)
                    {
                        mdlRaceString = ((XivRace)usedMdlRace).GetDisplayName();
                    }
                }

                XivRace? usedMtrlRace = usedMdlRace;
                if (race != XivRace.All_Races)
                {
                    if (usedMdlRace == null)
                    {
                        usedMtrlRace = null;
                    }
                    else
                    {
                        // Get the materials used by this racial's model.
                        var mdl = usedMdl;
                        var mdlMaterials = await XivCache.GetChildFiles(mdl);
                        var mtrl = mdlMaterials.FirstOrDefault(x => raceRegex.IsMatch(x));

                        if(mtrl == null)
                        {
                            usedMtrlRace = null;
                        } else
                        {
                            var code = raceRegex.Match(mtrl).Groups[1].Value;
                            usedMtrlRace = XivRaces.GetXivRace(code);
                            if(usedMtrlRace == XivRace.All_Races)
                            {
                                usedMtrlRace = null;
                            }
                        }
                    }
                }

                var mtrlRaceString = "None";
                if (usedMtrlRace == race)
                {
                    mtrlRaceString = "Own";
                }
                else
                {
                    if (usedMtrlRace != null)
                    {
                        mtrlRaceString = ((XivRace)usedMtrlRace).GetDisplayName();
                    }
                }

                var lMdl = new Label();
                lMdl.Content = mdlRaceString;
                lMdl.SetValue(Grid.RowProperty, rowIdx);
                lMdl.SetValue(Grid.ColumnProperty, 1);
                RacialGrid.Children.Add(lMdl);

                var lMtrl = new Label();
                lMtrl.Content = mtrlRaceString;
                lMtrl.SetValue(Grid.RowProperty, rowIdx);
                lMtrl.SetValue(Grid.ColumnProperty, 2);
                RacialGrid.Children.Add(lMtrl);


                rowIdx++;
            }
            #endregion


            if (Imc.UsesImc(_item) && _item.ModelInfo != null)
            {
                var myImcSubsetId = _item.ModelInfo.ImcSubsetID;
                var allItems = await root.GetAllItems();
                var fInfo = await _imc.GetFullImcInfo(_item);
                var entries = fInfo.GetAllEntries(_item.GetItemSlotAbbreviation(), true);

                foreach (var item in allItems)
                {
                    SameModelItems.Add(new KeyValuePair<string, IItem>(item.Name, item));
                    if (entries.Count > item.ModelInfo.ImcSubsetID) {
                        var imSet = entries[item.ModelInfo.ImcSubsetID].MaterialSet;

                        if(mSet == imSet)
                        {
                            SameMSetItems.Add(new KeyValuePair<string, IItem>(item.Name, item));
                        }
                    }
                    if(item.ModelInfo.ImcSubsetID == myImcSubsetId)
                    {
                        SameVariantItems.Add(new KeyValuePair<string, IItem>(item.Name, item));
                    }
                }
            }
        }


    }
}
