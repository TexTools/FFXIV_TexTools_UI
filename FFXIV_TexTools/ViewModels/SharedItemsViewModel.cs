using FFXIV_TexTools.Views;
using SharpDX.Toolkit.Graphics;
using SharpDX.Win32;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
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
        private SharedItemsView _view;
        private MainWindow _mainWindow;
        private IItem _item;
        private TreeView _tree;
        private Imc _imc;
        private Gear _gear;

        protected virtual void NotifyPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public SharedItemsViewModel(SharedItemsView view)
        {
            _view = view;
            _tree = _view.PrimaryTree;
            var gameDirectory = new DirectoryInfo(Properties.Settings.Default.FFXIV_Directory);
            _imc = new Imc(gameDirectory, XivDataFile._04_Chara);
            _gear = new Gear(gameDirectory, XivLanguages.GetXivLanguage(Properties.Settings.Default.Application_Language));
        }


        /// <summary>
        /// Updates the View/ViewModel with a new selected base item.
        /// </summary>
        /// <param name="item"></param>
        /// <returns></returns>
        public async Task SetItem(IItem item, MainWindow mainWindow = null)
        {
            if (mainWindow != null)
            {
                _mainWindow = mainWindow;
            }

            var gameDirectory = new DirectoryInfo(Properties.Settings.Default.FFXIV_Directory);
            _imc = new Imc(gameDirectory, item.DataFile);
            _item = item;
            _tree.Items.Clear();
            IItemModel im = null;
            try
            {
                im = (IItemModel)item;
            } catch(Exception ex)
            {
                return;
            }

            if(im == null || im.ModelInfo == null)
            {
                return;
            }

            var topLevelItem = new TreeViewItem();
            topLevelItem.Header = "";
            if(im.ModelInfo.PrimaryID > 0)
            {
                topLevelItem.Header += CapFirst(item.GetPrimaryItemType().ToString()) + " #" + im.ModelInfo.PrimaryID.ToString().PadLeft(4, '0');
            } else
            {
                topLevelItem.Header += CapFirst(item.GetPrimaryItemType().ToString());
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
                 fullInfo = await _imc.GetFullImcInfo(im);
            } catch(Exception ex)
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
                return;

            }
            var sharedList = await _gear.GetSameModelList(im);

            var myVariantNumber = fullInfo.GetImcInfo(im.ModelInfo.ImcSubsetID, im.SecondaryCategory).Variant;

            var sharedItems= new Dictionary<int, List<IItemModel>>();
            var variantHeaders = new Dictionary<int, TreeViewItem>();

            // TODO -
            // Add the Variant header nodes at the start, and only scan the IMC files when a 

            TreeViewItem myHeader = null;
            TreeViewItem myNode = null;
            foreach(var i in sharedList)
            {
                // Get the Variant # information
                var info = fullInfo.GetImcInfo(i.ModelInfo.ImcSubsetID, i.SecondaryCategory);
                if(info == null)
                {
                    // Invalid IMC Set ID for the item.
                    continue;
                }

                if (!sharedItems.ContainsKey(info.Variant))
                {
                    sharedItems.Add(info.Variant, new List<IItemModel>());
                    variantHeaders.Add(info.Variant, new TreeViewItem());
                    variantHeaders[info.Variant].Header = "Variant #" + info.Variant;
                    variantHeaders[info.Variant].DataContext = info.Variant;
                }

                sharedItems[info.Variant].Add(i);
                var nextNode = new TreeViewItem();
                nextNode.Header = i.Name;
                nextNode.DataContext = i;
                variantHeaders[info.Variant].Items.Add(nextNode);

                
                if(myHeader == null && info.Variant == myVariantNumber)
                {
                    myHeader = variantHeaders[info.Variant];
                }

                if (i.Name == im.Name)
                {
                    myNode = nextNode;
                } else
                {
                    nextNode.MouseDoubleClick += ItemNode_Activated;
                }
            }

            var ordered = variantHeaders.OrderBy(x => x.Key);

            foreach(var kv in ordered)
            {
                nextParent.Items.Add(kv.Value);
            }
            nextParent.IsExpanded = true;

            if (myHeader != null)
            {
                myHeader.IsExpanded = true;
            }
            if(myNode != null)
            {
                myNode.IsSelected = true;
            }




            //var topLevelItem = treeItem;



            
            //PrimaryTree.Clear
        }

        private void ItemNode_Activated(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            var treeItem = (TreeViewItem)sender;
            var item = (IItem) treeItem.DataContext;
            _mainWindow.SelectItem(item);
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
    }
}
