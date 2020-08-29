using MahApps.Metro.Controls;
using System;
using System.Collections.Generic;
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
        public ItemInfoDisplay(IItemModel item)
        {
            _item = item;
            InitializeComponent();

            AsyncInit();
        }

        public async Task AsyncInit()
        {
            var root = _item.GetRoot();
            var gd = XivCache.GameInfo.GameDirectory;
            var lang = XivCache.GameInfo.GameLanguage;
            var df = IOUtil.GetDataFileFromPath(root.Info.GetRootFile());

            var _index = new Index(gd);
            var _mtrl = new Mtrl(gd, df, lang);
            var _mdl = new Mdl(gd, df);
            var _imc = new Imc(gd);
            var raceRegex = new Regex("c([0-9]{4})[^b]");

            ItemNameBox.Text = _item.Name;

            var races = XivRaces.PlayableRaces;

            var mSet = await _imc.GetMaterialSetId(_item);
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
                    SameModelBox.Items.Add(item.Name);
                    if (entries.Count > item.ModelInfo.ImcSubsetID) {
                        var imSet = entries[item.ModelInfo.ImcSubsetID].Variant;

                        if(mSet == imSet)
                        {
                            SameMaterialBox.Items.Add(item.Name);
                        }
                    }
                    if(item.ModelInfo.ImcSubsetID == myImcSubsetId)
                    {
                        SameVariantBox.Items.Add(item.Name);
                    }
                }
            }
        }


    }
}
