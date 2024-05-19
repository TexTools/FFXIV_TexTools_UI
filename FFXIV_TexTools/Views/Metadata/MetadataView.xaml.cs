using FFXIV_TexTools.Helpers;
using FFXIV_TexTools.Resources;
using FFXIV_TexTools.ViewModels;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Forms;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Xceed.Wpf.Toolkit.PropertyGrid.Attributes;
using xivModdingFramework.Cache;
using xivModdingFramework.Exd.FileTypes;
using xivModdingFramework.Items.Enums;
using xivModdingFramework.Items.Interfaces;
using xivModdingFramework.Materials.FileTypes;
using xivModdingFramework.Models.FileTypes;
using xivModdingFramework.Mods;
using xivModdingFramework.Mods.Enums;
using xivModdingFramework.Mods.FileTypes;
using xivModdingFramework.SqPack.FileTypes;
using xivModdingFramework.Variants.FileTypes;
using UserControl = System.Windows.Controls.UserControl;

namespace FFXIV_TexTools.Views.Metadata
{
    /// <summary>
    /// Interaction logic for MetdataView.xaml
    /// </summary>
    public partial class MetadataView : UserControl
    {
        private MetadataViewModel _vm;
        private XivDependencyRoot _root;

        private static MetadataView _currentView;
        public static MetadataView CurrentView {
            get { return _currentView; }
        }

        private int _lastNumber = -1;


        public MetadataView()
        {
            _currentView = this;
            _vm = new MetadataViewModel(this);
            DataContext = _vm;
            InitializeComponent();

            var mw = MainWindow.GetMainWindow();
            mw.SelectedPrimaryItemValueChanged += Mw_SelectedPrimaryItemValueChanged;
        }

        private void Mw_SelectedPrimaryItemValueChanged(object sender, int e)
        {
            if (_root == null) return;

            // Only human types have the weird multi-root "Item" representation thing.
            if (_root.Info.PrimaryType != XivItemType.human) return;
            
            // Body stuff is totally desynced.
            if (_root.Info.SecondaryType == XivItemType.body) return;

            if (e <= 0) return;
            if (e == (int)_root.Info.SecondaryId) return;


            // Change root to the new "number" root.
            var nRoot = new XivDependencyRootInfo()
            {
                PrimaryId = _root.Info.PrimaryId,
                PrimaryType = _root.Info.PrimaryType,
                SecondaryId = e,
                SecondaryType = _root.Info.SecondaryType,
                Slot = _root.Info.Slot
            };

            _lastNumber = e;

            // Switch display to that new root.
            SetRoot(nRoot.ToFullRoot());
        }

        /// <summary>
        /// Sets the view to the given item. 
        /// Returns False if the view should be hidden.
        /// </summary>
        /// <param name="item"></param>
        /// <returns></returns>
        public async Task<bool> SetItem(IItem item)
        {
            var defaultVariant = 0;
            var im = item as IItemModel;
            if(im == null || !Imc.UsesImc(im))
            {
                //No-Op
            }
            else if(im.ModelInfo != null)
            {
                defaultVariant = im.ModelInfo.ImcSubsetID >= 0 ? im.ModelInfo.ImcSubsetID : 0;
            }

            return await SetRoot(item.GetRoot(), defaultVariant);
        }
        public async Task<bool> SetRoot(XivDependencyRoot root, int defaultVariant = 0)
        {
            _root = root;
            if (_root == null) return false;
            try
            {
                if (root.Info.PrimaryType == XivItemType.human)
                {
                    if (_lastNumber > 0)
                    {
                        if (_lastNumber != (int)root.Info.SecondaryId)
                        {
                            // Change root to the new "number" root.
                            var nRoot = new XivDependencyRootInfo()
                            {
                                PrimaryId = _root.Info.PrimaryId,
                                PrimaryType = _root.Info.PrimaryType,
                                SecondaryId = _lastNumber,
                                SecondaryType = _root.Info.SecondaryType,
                                Slot = _root.Info.Slot
                            };
                            _root = nRoot.ToFullRoot();
                        }
                    }
                    if (_root.Info.SecondaryId > 0)
                    {
                        _lastNumber = (int)_root.Info.SecondaryId;
                    }
                }

                SetLabel.Content = XivItemTypes.GetSystemPrefix(root.Info.PrimaryType) + root.Info.PrimaryId.ToString().PadLeft(4, '0');
                SlotLabel.Content = Mdl.SlotAbbreviationDictionary.FirstOrDefault(x => x.Value == _root.Info.Slot).Key + "(" + _root.Info.Slot + ")";

                var items = await _root.GetAllItems();
                var count = items.Count;
                if(count > 1)
                {
                    // If we have real items, ignore the base set.
                    count--;
                }

                ItemNameBox.Text = items[0].Name;
                if (count > 1)
                {
                    var i = count - 1;
                    var s = i > 1 ? "s" : "";
                    ItemNameBox.Text += $" ( + {i} Other Item{s} )";
                }

                var path = _root.Info.GetRootFile();
                var mod = await MainWindow.DefaultTransaction.GetMod(path);

                if (mod == null)
                {
                    ToggleButton.IsEnabled = false;
                    ToggleButton.Content = "Enable".L();
                }
                else
                {
                    ToggleButton.IsEnabled = false;
                    var tx = MainWindow.DefaultTransaction;
                    var enabled = await mod.Value.GetState(tx) == EModState.Enabled;
                    ToggleButton.IsEnabled = true;
                    if (enabled)
                    {
                        ToggleButton.Content = "Disable".L();
                    }
                    else
                    {
                        ToggleButton.Content = "Enable".L();
                    }
                }
                return await _vm.SetRoot(_root, defaultVariant);
            }
            catch(Exception ex) 
            {
                FlexibleMessageBox.Show("Unabled to load item Metadata: \n\n".L() + ex.Message, "Metatdata Load Error".L() ,MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
        }

        private void PreviousSlotButton_Click(object sender, RoutedEventArgs e)
        {
            var type = _root.Info.PrimaryType;
            if (type == XivItemType.demihuman)
            {
                type = (XivItemType)_root.Info.SecondaryType;
            }

            var slots = XivItemTypes.GetAvailableSlots(type);

            var currentSlotIdx = Array.IndexOf(slots.ToArray(), _root.Info.Slot);
            var nextSlotIdx = currentSlotIdx - 1;
            if (nextSlotIdx < 0)
            {
                nextSlotIdx = slots.Count - 1;
            }

            if(nextSlotIdx >= slots.Count || nextSlotIdx < 0)
            {
                // Can't change slots, this item only has one.
                return;
            }

            var nextSlot = slots[nextSlotIdx];

            var newRootInfo = (XivDependencyRootInfo)_root.Info.Clone();
            newRootInfo.Slot = nextSlot;

            var newRoot = newRootInfo.ToFullRoot();

            if (newRoot == null)
            {
                // Shouldn't ever actually hit this, but if we do, cancel the process.
                return;
            }

            SetRoot(newRoot);
        }

        private void NexSlotButton_Click(object sender, RoutedEventArgs e)
        {
            var type = _root.Info.PrimaryType;
            if (type == XivItemType.demihuman)
            {
                type = (XivItemType)_root.Info.SecondaryType;
            }

            var slots = XivItemTypes.GetAvailableSlots(type);

            var currentSlotIdx = Array.IndexOf(slots.ToArray(), _root.Info.Slot);
            var nextSlotIdx = currentSlotIdx + 1;
            if (nextSlotIdx == slots.Count)
            {
                nextSlotIdx = 0;
            }

            if (nextSlotIdx >= slots.Count || nextSlotIdx < 0)
            {
                // Can't change slots, this item only has one.
                return;
            }
            var nextSlot = slots[nextSlotIdx];

            var newRootInfo = (XivDependencyRootInfo) _root.Info.Clone();
            newRootInfo.Slot = nextSlot;

            var newRoot = newRootInfo.ToFullRoot();

            if(newRoot == null)
            {
                // Shouldn't ever actually hit this, but if we do, cancel the process.
                return;
            }

            SetRoot(newRoot);
        }

        private void AffectedItemsButton_Click(object sender, RoutedEventArgs e)
        {
            ShowAffectedItems();
        }

        private async Task ShowAffectedItems()
        {
            var items = await _root.GetAllItems();
            var itemNames = items.Select(x => x.Name);

            var win = new AffectedFilesView(itemNames, "Affected Items");
            win.Show();
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            _vm.Save();
        }

        private async void ToggleButton_Click(object sender, RoutedEventArgs e)
        {
            var tx = MainWindow.UserTransaction;

            var ownTx = false;
            if(tx == null)
            {
                tx = ModTransaction.BeginTransaction(true);
                ownTx = true;
            }
            try
            {
                var path = _root.Info.GetRootFile();
                var mod = await tx.GetMod(path);

                if (mod == null) return;

                var enabled = await mod.Value.GetState(tx) == EModState.Enabled;

                await MainWindow.GetMainWindow().LockUi("Updating Metadata".L());

                if (enabled)
                {
                    await Modding.SetModState(EModState.Disabled, path, tx);
                    ToggleButton.Content = "Enable".L();
                }
                else
                {
                    await Modding.SetModState(EModState.Enabled, path, tx);
                    ToggleButton.Content = "Disable".L();
                }

                if (ownTx)
                {
                    await ModTransaction.CommitTransaction(tx);
                }
            }
            catch(Exception ex)
            {
                if (ownTx)
                {
                    ModTransaction.CancelTransaction(tx);
                }
                FlexibleMessageBox.Show("Unable to toggle mod status.\n\nError: ".L() + ex.Message, "Mod Toggle Error".L(), System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Error);
            }
            finally
            {
                await MainWindow.GetMainWindow().UnlockUi();

                var mw = MainWindow.GetMainWindow();
                mw.ReloadItem();
            }

        }

        private async void SaveModpack_Click(object sender, RoutedEventArgs e)
        {
            await MainWindow.GetMainWindow().LockUi("Exporting Metadata".L());
            try
            {
                // To meet user expectations, we need to temporarily save the file as the on-screen state before exporting,
                // Then roll back the file after.

                var tx = MainWindow.UserTransaction;
                var file = _root.Info.GetRootFile();
                bool ownTx = false;
                TxFileState state = null;
                if (tx == null)
                {
                    ownTx = true;
                    tx = ModTransaction.BeginTransaction(true);
                }
                try
                {
                    state = await tx.SaveFileState(file);

                    await _vm.Save(true, tx);
                    SingleFileModpackCreator.ExportFile(_root.Info.GetRootFile(), MainWindow.GetMainWindow(), tx);

                }
                finally
                {
                    if (ownTx)
                    {
                        ModTransaction.CancelTransaction(tx, true);
                    }
                    else
                    {
                        await tx.RestoreFileState(state);
                    }
                }
            }
            finally
            {
                await MainWindow.GetMainWindow().UnlockUi();
            }
        }

        private async void ExportRaw_Click(object sender, RoutedEventArgs e)
        {
            var file = _root.Info.GetRootFile();
            var sd = new SaveFileDialog();
            sd.Filter = "FFXIV Item Metadata (*.meta)|*.meta";
            sd.FileName = System.IO.Path.GetFileName(file);
            var res = sd.ShowDialog();
            if (res != DialogResult.OK)
            {
                return;
            }

            await MainWindow.GetMainWindow().LockUi("Exporting Metadata".L());
            try
            {
                // To meet user expectations, we need to temporarily save the file as the on-screen state before exporting,
                // Then roll back the file after.

                var tx = MainWindow.UserTransaction;
                bool ownTx = false;
                TxFileState state = null;
                if (tx == null)
                {
                    ownTx = true;
                    tx = ModTransaction.BeginTransaction(true);
                }
                try
                {
                    state = await tx.SaveFileState(file);

                    await _vm.Save(true, tx);
                    var metadata = await ItemMetadata.GetMetadata(file, false, tx);
                    var data = await ItemMetadata.Serialize(metadata);



                    File.WriteAllBytes(sd.FileName, data);

                }
                finally
                {
                    if (ownTx)
                    {
                        ModTransaction.CancelTransaction(tx, true);
                    }
                    else
                    {
                        await tx.RestoreFileState(state);
                    }
                }
            }
            finally
            {
                await MainWindow.GetMainWindow().UnlockUi();
            }
        }

        private async void ImportRaw_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                await MainWindow.GetMainWindow().LockUi("Importing Metadata".L());
                var ofd = new OpenFileDialog();
                ofd.Filter = "FFXIV Item Metadata (*.meta)|*.meta";
                var res = ofd.ShowDialog();
                if (res != DialogResult.OK)
                {
                    return;
                }
                var data = File.ReadAllBytes(ofd.FileName);

                var metadata = await ItemMetadata.Deserialize(data);

                var file = _root.Info.GetRootFile();
                
                metadata.AlterRoot(_root);
                metadata.Validate(file);

                data = await ItemMetadata.Serialize(metadata);

                var tx = MainWindow.UserTransaction;

                // Data will automatically be expanded.
                await Dat.ImportType2Data(data, file, XivStrings.TexTools, _root.GetFirstItem(), tx);

                // Fill in missing racial models or material sets.
                await metadata.FillMissingFiles(XivStrings.TexTools, tx);

                await SetRoot(_root);
            }
            catch(Exception ex)
            {
                ViewHelpers.ShowError("Metadata Import Error", "Could not import Metadata due to an error:\n\n" + ex.Message);
            }
            finally
            {
                await MainWindow.GetMainWindow().UnlockUi();
            }


        }
    }
}
