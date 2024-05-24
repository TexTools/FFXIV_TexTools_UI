using FFXIV_TexTools.Views;
using SharpDX.Toolkit.Graphics;
using SharpDX.Win32;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using xivModdingFramework.Cache;
using xivModdingFramework.General.Enums;
using xivModdingFramework.Items;
using xivModdingFramework.Items.Categories;
using xivModdingFramework.Items.Enums;
using xivModdingFramework.Items.Interfaces;
using xivModdingFramework.Models.FileTypes;
using xivModdingFramework.Variants.FileTypes;
using static xivModdingFramework.Variants.FileTypes.Imc;

namespace FFXIV_TexTools.ViewModels
{
    class SharedItemsViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private IItem _item;
        private TreeView _tree;

        protected virtual void NotifyPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public SharedItemsViewModel(TreeView tree)
        {
            _tree = tree;
        }


        /// <summary>
        /// Updates the View/ViewModel with a new selected base item.
        /// </summary>
        /// <param name="item"></param>
        /// <returns></returns>
        public async Task<bool> SetItem(IItem item)
        {
            var tx = MainWindow.DefaultTransaction;

            _item = item;
            _tree.Items.Clear();
            IItemModel im = item as IItemModel;

            if (im == null || im.ModelInfo == null || !Imc.UsesImc(im))
            {
                return false;
            }

            var topLevelItem = new TreeViewItem();
            topLevelItem.Header = "";
            if(im.ModelInfo.PrimaryID > 0)
            {
                topLevelItem.Header += CapFirst(item.GetPrimaryItemType().ToString().L()) + " #" + im.ModelInfo.PrimaryID.ToString().PadLeft(4, '0');
            } else
            {
                topLevelItem.Header += CapFirst(item.GetPrimaryItemType().ToString().L());
            }
            _tree.Items.Add(topLevelItem);


            var nextParent = topLevelItem;
            if(im.ModelInfo.SecondaryID > 0)
            {
                var nextNode = new TreeViewItem();
                nextNode.Header += CapFirst(item.GetSecondaryItemType().ToString()) + " #" + im.ModelInfo.SecondaryID.ToString().PadLeft(4, '0');
                nextParent.Items.Add(nextNode);
                nextParent.IsExpanded = true;
                nextParent = nextNode;
            }

            var abbreviation = _item.GetItemSlotAbbreviation();
            if (abbreviation != "")
            {
                var nextNode = new TreeViewItem();
                nextNode.Header = Mdl.SlotAbbreviationDictionary.First(x => x.Value == abbreviation).Key;
                nextParent.Items.Add(nextNode);
                nextParent.IsExpanded = true;
                nextParent = nextNode;
            }

            FullImcInfo fullInfo = null;
            try
            {
                fullInfo = await Imc.GetFullImcInfo(im, false, tx);
                if(fullInfo == null)
                {
                    return false;
                }
            } 
            catch(Exception ex)
            {
                // This item has no IMC file.
                var nextNode = new TreeViewItem();
                nextNode.Header = im.Name;
                nextNode.DataContext = im;
                //nextNode.MouseDoubleClick += ItemNode_Activated;
                nextParent.Items.Add(nextNode);
                nextParent.IsExpanded = true;
                nextNode.IsSelected = true;
                nextParent = nextNode;

                // No shared items for things without IMC files, so just hide the view entirely?
                return false;

            }

            var sharedList = new List<IItemModel>();
            try
            {
                sharedList = await im.GetSharedModelItems(tx);
            }
            catch
            {
                // No-Op.  If this broke the user is already getting bombarded with errors.
            }

            var myMaterialSetNumber = fullInfo.GetEntry(im.ModelInfo.ImcSubsetID, abbreviation).MaterialSet;
            var myImcNumber = im.ModelInfo.ImcSubsetID;

            var imcVariantHeaders = new Dictionary<int, TreeViewItem>();


            TreeViewItem myImcHeader = null;
            TreeViewItem myNode = null;
            foreach(var i in sharedList)
            {
                // Get the Variant # information
                var info = fullInfo.GetEntry(i.ModelInfo.ImcSubsetID, abbreviation);
                if(info == null)
                {
                    // Invalid IMC Set ID for the item.
                    continue;
                }
                var imcVariant = i.ModelInfo.ImcSubsetID;
                var mtrlVariant = info.MaterialSet;

                if (!imcVariantHeaders.ContainsKey(imcVariant))
                {
                    imcVariantHeaders.Add(imcVariant, new TreeViewItem());
                    imcVariantHeaders[imcVariant].Header = "Variant ".L() + i.ModelInfo.ImcSubsetID;
                    imcVariantHeaders[imcVariant].DataContext = i.ModelInfo.ImcSubsetID;

                    imcVariantHeaders[imcVariant].Header += " - Material Set: ".L() + mtrlVariant;

                    var hiddenParts = MaskToHidenParts(info.Mask);
                    imcVariantHeaders[i.ModelInfo.ImcSubsetID].Header += " | Hidden Parts: ".L();

                    if (hiddenParts.Count > 0)
                    {
                        imcVariantHeaders[imcVariant].Header += String.Join(",", hiddenParts);
                    }
                    else
                    {
                        imcVariantHeaders[imcVariant].Header += "None".L();
                    }

                    imcVariantHeaders[imcVariant].Header += " - [%] " + i.Name;

                    if (i.ModelInfo.ImcSubsetID == myImcNumber)
                    {
                        myImcHeader = imcVariantHeaders[imcVariant];
                    }
                }

                var nextNode = new TreeViewItem();
                nextNode.Header = i.Name;


                nextNode.DataContext = i;
                imcVariantHeaders[i.ModelInfo.ImcSubsetID].Items.Add(nextNode);

                
                if (i.Name == im.Name)
                {
                    myNode = nextNode;
                }
                nextNode.MouseDoubleClick += ItemNode_Activated;
            }

            foreach (var h in imcVariantHeaders)
            {

                var st = ((string)h.Value.Header);
                h.Value.Header = st.Replace("%", h.Value.Items.Count.ToString());
            }


            var ordered = imcVariantHeaders.OrderBy(x => x.Key);

            foreach(var kv in ordered)
            {
                nextParent.Items.Add(kv.Value);
            }
            nextParent.IsExpanded = true;

            if (myImcHeader != null)
            {
                myImcHeader.IsExpanded = true;
            }
            if (myNode != null)
            {
                myNode.IsSelected = true;
            }

            return true;
        }

        private void ItemNode_Activated(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            var treeItem = (TreeViewItem)sender;
            var item = (IItem) treeItem.DataContext;
            MainWindow.GetMainWindow().SetSelectedItem(item);
        }

        /// <summary>
        /// Capitalize first letter of the given string.
        /// </summary>
        /// <param name="s"></param>
        /// <returns></returns>
        private string CapFirst(string s)
        {
            return s[0].ToString().ToUpper() + s.Substring(1);
        }

        private List<char> MaskToHidenParts(ushort mask)
        {
            var ret = new List<char>();
            BitArray bits = new BitArray(System.BitConverter.GetBytes(mask));


            var idx = 0;
            foreach(var b in bits)
            {
                // The Mask only uses the first 10 bits.
                if(idx > 9)
                {
                    break;
                }

                var visible = (bool)b;
                if(!visible)
                {
                    var letter = xivModdingFramework.Helpers.Constants.Alphabet[idx];
                    ret.Add(letter);
                }
                idx++;
            }

            return ret;
        }
    }
}
