using FFXIV_TexTools.Resources;
using MahApps.Metro.Controls.Dialogs;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Xceed.Wpf.Toolkit.PropertyGrid.Attributes;
using xivModdingFramework.Cache;
using xivModdingFramework.Items.Enums;
using xivModdingFramework.Items.Interfaces;
using xivModdingFramework.Variants.FileTypes;

namespace FFXIV_TexTools.Views
{
    /// <summary>
    /// Interaction logic for DependencyInfoView.xaml
    /// </summary>
    public partial class DependencyInfoView
    {
        private string _path;
        private ProgressDialogController _lockProgressController;
        private IProgress<string> _lockProgress;
        public DependencyInfoView(string filePath)
        {
            InitializeComponent();

            AffectedItemsBox.DisplayMemberPath = "Name";

            SetFile(filePath);

            ChildFilesBox.MouseDoubleClick += ChildFilesBox_MouseDoubleClick;
            ParentFilesBox.MouseDoubleClick += ChildFilesBox_MouseDoubleClick;
            SiblingFilesBox.MouseDoubleClick += ChildFilesBox_MouseDoubleClick;

            AffectedItemsBox.MouseDoubleClick += AffectedItemsBox_MouseDoubleClick;
        }

        public async Task LockUi()
        {
            _lockProgressController = await this.ShowProgressAsync("Loading".L(), "Please Wait...".L());

            _lockProgressController.SetIndeterminate();

            _lockProgress = new Progress<string>((update) =>
            {
                _lockProgressController.SetMessage(update);
            });
        }
        public async Task UnlockUi()
        {
            await _lockProgressController.CloseAsync();
            _lockProgressController = null;
            _lockProgress = null;
        }


        private void AffectedItemsBox_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            var box = (ListBox)sender;
            if (box.SelectedItem != null)
            {
                var item = (IItem)box.SelectedItem;

                MainWindow.GetMainWindow().SetSelectedItem(item);
            }
        }

        private void ChildFilesBox_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            var box = (ListBox)sender;
            if(box.SelectedItem != null)
            {
                SetFile((string)box.SelectedItem);
            }
        }

        private static readonly Regex _extractVariant = new Regex("v([0-9]{4})");

        public async Task SetFile(string filePath)
        {
            await LockUi();
            WarningLabel.Text = "";
            _path = filePath;

            ChildFilesBox.Items.Clear();
            ParentFilesBox.Items.Clear();
            SiblingFilesBox.Items.Clear();
            AffectedItemsBox.Items.Clear();
            ModelLevelBox.Text = "";
            MaterialLevelBox.Text = "";

            var tx = MainWindow.DefaultTransaction;


            FilePathLabel.Text = filePath;
            FileNameBox.Text = Path.GetFileName(filePath);

            var root = await XivCache.GetFirstRoot(filePath);
            if(root == null)
            {
                RootNameBox.Text = "NULL";
                WarningLabel.Text = "File dependency information not supported for this item.".L();
                await UnlockUi();
                return;
            }
            RootNameBox.Text = root.ToRawItem().Name;

            var ext = Path.GetExtension(filePath).Substring(1);

            var allItems = (await root.GetAllItems());

            if(allItems.Count == 0)
            {
                WarningLabel.Text = "File dependency information not supported for this item.";
                await UnlockUi();
                return;
            }

            if(root.Info.PrimaryType == XivItemType.human)
            {
                WarningLabel.Text = "Parent file references & affected items lists \n may not be complete for this file. \n (Unknown cross-references may exist.)";
            }

            var orderedItems = allItems.OrderBy((x => x.Name), new ItemNameComparer()).ToList();
            var modelItem = orderedItems[0];

            ModelLevelBox.Text = modelItem.Name;


            var children = await XivCache.GetChildFiles(filePath, tx);
            var parents = await XivCache.GetParentFiles(filePath, tx);
            var siblings = await XivCache.GetSiblingFiles(filePath, tx);

            if (children == null || parents == null || siblings == null)
            {
                WarningLabel.Text = "File dependency information not supported for this item.";
                await UnlockUi();
                return;
            }

            foreach (var s in children)
            {
                ChildFilesBox.Items.Add(s);
            }
            foreach (var s in parents)
            {
                ParentFilesBox.Items.Add(s);
            }
            foreach (var s in siblings)
            {
                SiblingFilesBox.Items.Add(s);
            }

            Dictionary<XivDependencyRoot, HashSet<int>> mVariants = new Dictionary<XivDependencyRoot, HashSet<int>>();
            var affectedItems = new List<IItem>();
            if(ext == "tex")
            {
                foreach(var parent in parents)
                {
                    // Each of our parents has a root...
                    var pRoot = await XivCache.GetFirstRoot(parent);

                    if (pRoot == null) continue;

                    // And each of these files also has a material set id.
                    var match = _extractVariant.Match(parent);
                    var mVariant = 0;
                    if(match.Success)
                    {
                        mVariant = Int32.Parse(match.Groups[1].Value);
                    }

                    if(!mVariants.ContainsKey(pRoot))
                    {
                        mVariants.Add(pRoot, new HashSet<int>());
                    }
                    mVariants[pRoot].Add(mVariant);


                    var pItems = (await pRoot.GetAllItems());
                }

            } else if(ext == "mtrl")
            {
                var match = _extractVariant.Match(filePath);
                var mVariant = 0;
                if (match.Success)
                {
                    mVariant = Int32.Parse(match.Groups[1].Value);
                }

                if (!mVariants.ContainsKey(root))
                {
                    mVariants.Add(root, new HashSet<int>());
                }
                mVariants[root].Add(mVariant);

            } else
            {
                foreach(var item in allItems)
                {
                    affectedItems.Add(item);
                }
                MaterialLevelBox.Text = "";
            }


            Dictionary<XivDependencyRoot, HashSet<int>> sharedImcSubsets = new Dictionary<XivDependencyRoot, HashSet<int>>();


            try
            {
                // We now have a dictionary of <Root>, <Material Set Id> that comprises all of our referencing material sets.
                // We now need to convert that into a list of <root> => <Imc Subset IDs>, for all IMC subsets in that root which use our material ID.
                foreach (var kv in mVariants)
                {
                    var rt = kv.Key;
                    sharedImcSubsets.Add(rt, new HashSet<int>());
                    var imcPath = rt.GetRawImcFilePath();

                    var fullImcInfo = await Imc.GetFullImcInfo(imcPath, false, MainWindow.DefaultTransaction);

                    var setCount = fullImcInfo.SubsetCount + 1;
                    for (int i = 0; i < setCount; i++)
                    {
                        var info = fullImcInfo.GetEntry(i, rt.Info.Slot);

                        // This IMC subset references the one of our material sets.
                        if (kv.Value.Contains(info.MaterialSet))
                        {
                            sharedImcSubsets[rt].Add(i);
                        }
                    }
                }

                // We now have a dictionary of <root>, <imc subset ids>.  At this point, we can compare this to our original alll items list.
                var sh = allItems.Where(x =>
                {
                    var iRoot = x.GetRoot();

                    // The root must be one of our roots we care about.
                    if (!sharedImcSubsets.ContainsKey(iRoot)) return false;

                    var imcs = sharedImcSubsets[iRoot];

                    // The imc subset must be in the imc subsets that use this material.
                    if (!imcs.Contains(x.ModelInfo.ImcSubsetID)) return false;

                     return true;
                });

                foreach (var i in sh)
                {
                    affectedItems.Add(i);
                }

            } catch
            {
                // The item doesn't have a valid IMC entry.  In that case, it affects all items in the tree.
                affectedItems.AddRange(allItems);
            }



            if (affectedItems.Count == 0)
            {
                MaterialLevelBox.Text = "Unknown";
            }
            else
            {
                if (ext == "tex" || ext == "mtrl")
                {
                    var ordered = affectedItems.OrderBy((x => x.Name), new ItemNameComparer()).ToList();
                    MaterialLevelBox.Text = ordered[0].Name;
                }
            }

            foreach (var item in affectedItems)
            {
                AffectedItemsBox.Items.Add(item);
            }

            await UnlockUi();
        }
    }
}
