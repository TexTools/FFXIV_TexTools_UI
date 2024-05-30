using FFXIV_TexTools.Helpers;
using FFXIV_TexTools.Resources;
using FFXIV_TexTools.Views.Controls;
using FFXIV_TexTools.Views.SharedItems;
using HelixToolkit.Wpf;
using MahApps.Metro.Controls;
using MahApps.Metro.Controls.Dialogs;
using MahApps.Metro.IconPacks;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.Eventing.Reader;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using xivModdingFramework.Cache;
using xivModdingFramework.General.Enums;
using xivModdingFramework.Helpers;
using xivModdingFramework.Items.Categories;
using xivModdingFramework.Items.DataContainers;
using xivModdingFramework.Items.Enums;
using xivModdingFramework.Items.Interfaces;
using xivModdingFramework.Materials.DataContainers;
using xivModdingFramework.Materials.FileTypes;
using xivModdingFramework.Mods;
using xivModdingFramework.Textures.Enums;
using xivModdingFramework.Textures.FileTypes;
using xivModdingFramework.Variants.FileTypes;

namespace FFXIV_TexTools.Views.Item
{
    public partial class ItemViewControl : UserControl, INotifyPropertyChanged, IDisposable
    {
        /*  This class is the primary container for the core TexTools "Item" based view system.
         *  It primarily serves a wrapper for the various File Wrapper tabs.
         *  With the majority of its logic serving to parse an IItem into its constituent files,
         *  and pass those file paths to the correct places,
         *  as well as wrapping those file paths into dropdown selections in some of the tabs.
        */

        // TODO: Make this prettier.
        private static SolidColorBrush SelectedBrush = new SolidColorBrush(new Color() { A = 255, R = 0, G = 0, B = 0 });

        private static SolidColorBrush UnselectedBrush = new SolidColorBrush(new Color() { A = 0, R = 0, G = 0, B = 0 });

        private string _ItemNameText;
        public string ItemNameText
        {
            get
            {
                return _ItemNameText;
            }
            set
            {
                _ItemNameText = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ItemNameText)));
            }
        }

        public bool RefreshEnabled
        {
            get
            {
                return Item != null;
            }
        }


        private List<FileWrapperControl> FileControls = new List<FileWrapperControl>();
        #region Basic IPropertyNotify Properties Pattern

        public event PropertyChangedEventHandler PropertyChanged;

        public static ItemViewControl MainItemView { get; private set; }

        private IItem _Item;
        public IItem Item
        {
            get => _Item;
            private set
            {
                _Item = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Item)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(RefreshEnabled)));
            }
        }

        private ObservableCollection<KeyValuePair<string, string>> _Models { get; set; }
        public ObservableCollection<KeyValuePair<string, string>> Models
        {
            get => _Models;
            set
            {
                _Models = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Models)));
            }
        }

        private ObservableCollection<KeyValuePair<string, string>> _Materials { get; set; }
        public ObservableCollection<KeyValuePair<string, string>> Materials
        {
            get => _Materials;
            set
            {
                _Materials = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Materials)));
            }
        }

        private ObservableCollection<KeyValuePair<string, string>> _Textures { get; set; }
        public ObservableCollection<KeyValuePair<string, string>> Textures
        {
            get => _Textures;
            set
            {
                _Textures = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Textures)));
            }
        }

        private bool _ModelsEnabled;
        public bool ModelsEnabled
        {
            get => _ModelsEnabled;
            set
            {
                _ModelsEnabled = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ModelsEnabled)));
            }
        }

        private bool _MaterialsEnabled;
        public bool MaterialsEnabled
        {
            get => _MaterialsEnabled;
            set
            {
                _MaterialsEnabled = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(MaterialsEnabled)));
            }
        }

        private bool _TexturesEnabled;
        public bool TexturesEnabled
        {
            get => _TexturesEnabled;
            set
            {
                _TexturesEnabled = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(TexturesEnabled)));
            }
        }

        private bool _MetadataEnabled;
        public bool MetadataEnabled
        {
            get => _MetadataEnabled;
            set
            {
                _MetadataEnabled = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(MetadataEnabled)));
            }
        }

        private bool _ItemInfoEnabled;
        public bool ItemInfoEnabled
        {
            get => _ItemInfoEnabled;
            set
            {
                _ItemInfoEnabled = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ItemInfoEnabled)));
            }
        }

        private bool _AddMaterialEnabled;
        public bool AddMaterialEnabled
        {
            get => _AddMaterialEnabled;
            set
            {
                _AddMaterialEnabled = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(AddMaterialEnabled)));
            }
        }

        #endregion


        private bool _TargetFileSet {  get
            {
                return _TargetFile.ModelKey != null;
            } 
        }

        // Special indicator requesting not to change the visible panel.
        private bool _TargetFileLocked { get
            {
                return _TargetFile.ModelKey == "!";
            } 
        }

        private (string ModelKey, string MaterialKey, string TextureKey) _TargetFile = (null, null, null);

        /// <summary>
        /// Internal dictionary structure representing all of the files available for this item.
        /// [Model] => [Referenced Materials] => [Referenced Textures]
        /// 
        /// If a layer is missing, empty string is used as the only key.
        /// </summary>
        private Dictionary<string, Dictionary<string, HashSet<string>>> Files;

        private XivDependencyRoot Root;

        private bool _UpdateQueued;
        private bool UpdateQueued
        {
            get => _UpdateQueued;
            set
            {
                _UpdateQueued = value;

                // Notify the child controls to stop processing for updates, since we're about to wipe them anyways.
                TextureWrapper.FileControl._UpdateQueued = true;
                MaterialWrapper.FileControl._UpdateQueued = true;
                ModelWrapper.FileControl._UpdateQueued = true;
                MetadataWrapper.FileControl._UpdateQueued = true;
            }
        }

        public ItemViewControl()
        {
            DataContext = this;
            InitializeComponent();

            TextureWrapper.SetControlType(typeof(TextureFileControl));
            MaterialWrapper.SetControlType(typeof(MaterialFileControl));
            ModelWrapper.SetControlType(typeof(ModelFileControl));
            MetadataWrapper.SetControlType(typeof(MetadataFileControl));

            FileControls.Add(TextureWrapper);
            FileControls.Add(MaterialWrapper);
            FileControls.Add(ModelWrapper);
            FileControls.Add(MetadataWrapper);

            MetadataWrapper.FileControl.FileSaved += OnMetadataSaved;

            ButtonGrid.Visibility = Visibility.Collapsed;
            ItemNameText = "No Item Selected";

            // Go ahead and show the model panel.
            // The viewport there can take a second to initialize,
            // So it helps a little to get it visible to kick that stuff off earlier.
            ShowPanel(ModelWrapper);

            TextureWrapper.FileControl.FileDeleted += FileControl_FileDeleted;
            MaterialWrapper.FileControl.FileDeleted += FileControl_FileDeleted;
            ModelWrapper.FileControl.FileDeleted += FileControl_FileDeleted;
            MetadataWrapper.FileControl.FileDeleted += Metadata_FileDeleted;

            ModTransaction.FileChangedOnCommit += Tx_FileChanged;
            if (MainWindow.UserTransaction != null)
            {
                MainWindow.UserTransaction.FileChanged += Tx_FileChanged;
                MainWindow.UserTransaction.TransactionClosed += Tx_Closed;
            }
            TxWatcher.UserTxStarted += OnUserTransactionStarted;

            _DebouncedRebuildComboBoxes = ViewHelpers.Debounce<IItem>(DispatchRebuildComboBoxes);
        }
        private void OnUserTransactionStarted(ModTransaction tx)
        {
            if (tx != null)
            {
                tx.FileChanged += Tx_FileChanged;
                tx.TransactionClosed += Tx_Closed;
            }
        }
        private void Tx_Closed(ModTransaction sender)
        {
            sender.TransactionClosed -= Tx_Closed;
            sender.FileChanged -= Tx_FileChanged;
        }
        private void Tx_FileChanged(string changedFile, long newOffset)
        {
            if (Files == null) return;

            if (string.IsNullOrWhiteSpace(changedFile))
            {
                return;
            }

            try
            {

                // This is where we can listen for files getting modified.
                var fRoot = XivCache.GetFilePathRoot(changedFile);
                if(Root == fRoot)
                {
                    // File is contained in our root...
                    var keys = GetFileKeys(changedFile);
                    if (keys.ModelKey == "!")
                    {
                        _UpdateQueued = true;


                        // But is not already listed in our file structure.
                        // This means we need to reload the item.
                        _DebouncedRebuildComboBoxes(Item);
                    }
                    return;
                }
            }
            catch (Exception ex)
            {
                // No Op
                Trace.WriteLine(ex);
            }
        }
        private async void Metadata_FileDeleted(string internalPath)
        {
            try
            {
                // Deleted metadata basically required reloading the item due to the bredth of possible changes.
                await await Dispatcher.InvokeAsync(async () =>
                {
                    await SetItem(Item);
                });
            }
            catch (Exception ex)
            {
                //No Op
                Trace.WriteLine(ex);
            }
        }
        private async void FileControl_FileDeleted(string internalPath)
        {
            try
            {
                await await Dispatcher.InvokeAsync(async () =>
                {
                    await SafeRemoveFile(internalPath);
                });
            }
            catch(Exception ex)
            {
                //No Op
                Trace.WriteLine(ex);
            }
        }
        protected override void OnVisualParentChanged(DependencyObject oldParent)
        {
            var wind = Window.GetWindow(this);
            if (wind == MainWindow.GetMainWindow())
            {
                MainItemView = this;
            }

            wind.PreviewKeyDown += Wind_PreviewKeyDown;
            wind.PreviewMouseRightButtonDown += Wind_PreviewMouseRightButtonDown;
        }


        private async void OnMetadataSaved(FileViewControl sender, bool success)
        {
            try
            {
                // Always reload the item on Metadata reload, to be safe.
                await SetItem(Item, Item.GetRoot().Info.GetRootFile());
            }
            catch
            {
                // No-Op, just safety catch.
            }
        }

        /// <summary>
        /// Simple shortcut accessor and null checking for ActiveView.SetItem(item)
        /// </summary>
        /// <param name="item"></param>
        /// <returns></returns>
        public static async Task StaticSetItem(IItem item)
        {
            if(MainItemView == null)
            {
                return;
            }
            await MainItemView.SetItem(item);
        }

        private string _PreLoadModel = null;
        private string _PreLoadMaterial = null;
        private string _PreLoadTexture = null;

        /// <summary>
        /// Primary setter for other areas in TexTools to assign an item to this view.
        /// </summary>
        /// <param name="item"></param>
        /// <returns></returns>
        public async Task<bool> SetItem(IItem item, string targetFile = null)
        {
            var res = HandleUnsaveConfirmation(null, null);
            if (!res)
            {
                return false;
            }

            _PreLoadModel = null;
            _PreLoadMaterial = null;
            _PreLoadTexture = null;

            if (Item == item && targetFile == null)
            {
                // This is a refresh/reload. Try to keep us on the same file.
                targetFile = _VisiblePanel.FilePath;
            } else if(targetFile == null)
            {
                // Changing item, and we're looking at something.
                // Hang onto these to try to logically guess where the user wants to look at next.
                _PreLoadModel = ModelWrapper.FilePath;
                _PreLoadMaterial = MaterialWrapper.FilePath;
                _PreLoadTexture = TextureWrapper.FilePath;
            }

            ProgressDialogController lockController = null;
            if (this == MainItemView)
            {
                await MainWindow.GetMainWindow().LockUi("Loading Item", "Please wait...", this);
            }
            else
            {
                var wind = Window.GetWindow(this) as MetroWindow;
                if (wind != null)
                {
                    lockController = await wind.ShowProgressAsync("Loading Item", "Please wait...");
                }
            }

            try
            {
                _TargetFile = GetFileKeys(targetFile);

                Item = item;
                ModelsEnabled = false;
                MaterialsEnabled = false;
                TexturesEnabled = false;
                MetadataEnabled = false;
                ItemInfoEnabled = false;
                AddMaterialEnabled = false;
                Models = new ObservableCollection<KeyValuePair<string, string>>();
                Materials = new ObservableCollection<KeyValuePair<string, string>>();
                Textures = new ObservableCollection<KeyValuePair<string, string>>();
                Files = new Dictionary<string, Dictionary<string, HashSet<string>>>();
                ItemNameText = "Loading Item...";

                ResetWatermarks();

                if (Item == null)
                {
                    await TextureWrapper.ClearFile();
                    await MaterialWrapper.ClearFile();
                    await ModelWrapper.ClearFile();
                    await MetadataWrapper.ClearFile();

                    ItemNameText = "No Item Selected";
                    ShowPanel(null);
                    return true;
                }

                ItemInfoEnabled = true;
                Root = Item.GetRoot();

                // We can add materials to anything with a root.
                AddMaterialEnabled = true;

                var tx = MainWindow.DefaultTransaction;

                await SetItemName(tx);

                // Load metadata view manually since it's not handled by the above functions.
                if (Root != null)
                {
                    var success = await MetadataWrapper.LoadInternalFile(Root.Info.GetRootFile(), Item, null, false);
                    if (success)
                    {
                        MetadataEnabled = true;
                    }
                }

                await RebuildComboBoxes(tx);



                return true;
            }
            catch(Exception ex) 
            {
                this.ShowError("Item Load Error", "An error occurred while loading the item:\n\n" + ex.Message);
                return false;
            }
            finally
            {
                if (this == MainItemView)
                {
                    await MainWindow.GetMainWindow().UnlockUi(this);
                } else if(lockController != null)
                {
                    await lockController.CloseAsync();
                }
            }
        }

        private async Task RebuildComboBoxes(ModTransaction tx) {

            // Populate the Files structure.
            await Task.Run(async () =>
            {
                // These need to be in sequence, so they can't be paralell'd
                await GetModels(tx);
                await GetMaterials(tx);
                await GetTextures(tx);
            });

            // Assign the first combo box, which will kick off the children boxes in turn.
            AddModels(Files.Keys.ToList());
        }

        private async Task SetItemName(ModTransaction tx)
        {
            if(Item == null)
            {
                ItemNameText = "No Item Selected";
                return;
            } else if(Root == null || !Root.Info.IsValid())
            {
                ItemNameText = Item.Name;
                return;
            }

            var variantString = "";
            var asIm = Item as IItemModel;
            if (asIm != null && Imc.UsesImc(Root) && asIm != null && asIm.ModelInfo != null && asIm.ModelInfo.ImcSubsetID >= 0)
            {
                variantString += " - Variant " + asIm.ModelInfo.ImcSubsetID;

                
                var mSetId = await Imc.GetMaterialSetId(asIm, false, tx);
                if (mSetId >= 0)
                {
                    variantString += "/Material Set " + mSetId;
                }
            }

            ItemNameText = Root.Info.GetBaseFileName() + variantString + " : " + Item.Name;
        }

        private void ResetWatermarks()
        {

            TextBoxHelper.SetWatermark(ModelComboBox, UIStrings.Model);
            TextBoxHelper.SetWatermark(MaterialComboBox, UIStrings.Material);
            TextBoxHelper.SetWatermark(TextureComboBox, UIStrings.Texture);
        }
        private void AssignModelWatermark()
        {
            var ct = Models.Count;
            if (Models.Count == 0 || Models[0].Value == "")
            {
                ct = 0;
            }

            var markText = UIStrings.Model;
            if (ct == 0 || ct > 1)
            {
                markText = UIStrings.Models;
            }

            markText += " (" + ct + ")";
            TextBoxHelper.SetWatermark(ModelComboBox, markText);
        }
        private void AssignMaterialWatermark()
        {
            string markText = UIStrings.Material;
            var ct = Materials.Count;
            if (Materials.Count == 0 || Materials[0].Value == "")
            {
                ct = 0;
            }
            if (ct == 0 || ct > 1)
            {
                markText = UIStrings.Materials;
            }

            markText += " (" + ct + ")";
            TextBoxHelper.SetWatermark(MaterialComboBox, markText);
        }
        private void AssignTexturewatermark()
        {

            var ct = Textures.Count;
            if (Textures.Count == 0 || Textures[0].Value == "")
            {
                ct = 0;
            }

            string markText = UIStrings.Texture;
            if (ct == 0 || ct > 1)
            {
                markText = UIStrings.Textures;
            }

            markText += " (" + ct + ")";
            TextBoxHelper.SetWatermark(TextureComboBox, markText);
        }

        /// <summary>
        /// Populates the top level strcture of the Files dictionary.
        /// </summary>
        private async Task GetModels(ModTransaction tx)
        {
            Files = new Dictionary<string, Dictionary<string, HashSet<string>>>();

            if (Root == null)
            {
                Files.Add("", new Dictionary<string, HashSet<string>>());
                return;
            }

            var models = await Root.GetModelFiles(tx);
            foreach (var m in models)
            {
                Files.Add(m, new Dictionary<string, HashSet<string>>());
            }

            if(Files.Count == 0)
            {
                Files.Add("", new Dictionary<string, HashSet<string>>());
            }
            return;
        }


        /// <summary>
        /// Populates the second level structure of the Files dictionary
        /// </summary>
        /// <returns></returns>
        private async Task GetMaterials(ModTransaction tx)
        {
            if(Root == null)
            {
                if (Files.Count >= 1)
                {
                    Files.First().Value.Add("", new HashSet<string>());
                }
                return;
            }
            if(Files.Count == 0)
            {
                // Hmm.  How did we get here?
                return;
            }

            var asIm = Item as IItemModel;
            var materialSet = -1;
            if(asIm != null)
            {
                materialSet = await Imc.GetMaterialSetId(asIm, false, tx);
            }

            HashSet<string> foundMaterials = new HashSet<string>();
            if (Root.Info.PrimaryType == XivItemType.human && Root.Info.SecondaryType == XivItemType.body)
            {
                // Exceptions class.
                var materials = await Root.GetMaterialFiles(-1, tx, false);
                foundMaterials.UnionWith(materials);
                var key = Files.First().Key;
                foreach (var mat in materials)
                {
                    if (!Files[key].ContainsKey(mat))
                    {
                        Files[key].Add(mat, new HashSet<string>());
                    }
                }
            }
            else
            {
                if (Files.Count == 1 && Files.First().Key == "")
                {
                    // No valid model files.  But there might be materials.
                    var materials = await Root.GetMaterialFiles(-1, tx, false);
                    foreach (var mat in materials)
                    {
                        if (!Files[""].ContainsKey(mat))
                        {
                            Files[""].Add(mat, new HashSet<string>());
                        }
                    }
                }
                else
                {
                    // Resolve by referenced materials.
                    foreach (var file in Files)
                    {
                        var model = file.Key;

                        if (string.IsNullOrWhiteSpace(model)) break;
                        var materials = await Root.GetVariantShiftedMaterials(model, materialSet, tx);
                        foundMaterials.UnionWith(materials);

                        foreach (var mat in materials)
                        {
                            if (!Files[model].ContainsKey(mat))
                            {
                                Files[model].Add(mat, new HashSet<string>());
                            }
                        }
                    }
                }
            }


            var orphanMaterials = await Root.GetModdedMaterials(materialSet, tx);
            if (Root.Info.SecondaryType != null)
            {
                // If there is a secondary ID, just snap these onto the first entry, because there's only one model (or 0).
                var entry = Files.First().Value;
                foreach (var orph in orphanMaterials)
                {
                    if (foundMaterials.Contains(orph)) continue;

                    if (!entry.ContainsKey(orph)) {
                        entry.Add(orph, new HashSet<string>());
                    }
                }
            }
            else
            {
                foreach (var orph in orphanMaterials)
                {
                    //if (foundMaterials.Contains(orph)) continue;

                    // This goes to the matching fake-secondary entry, if there is one.
                    // Ex. on Equipment, the race is a fake primary value.
                    var primary = IOUtil.GetPrimaryIdFromFileName(orph);
                    var match = Files.FirstOrDefault(x => IOUtil.GetPrimaryIdFromFileName(x.Key) == primary);
                    if(match.Key != null && match.Value != null && !string.IsNullOrWhiteSpace(primary))
                    {
                        if (!match.Value.ContainsKey(orph))
                        {
                            match.Value.Add(orph, new HashSet<string>());
                        }
                    }
                    else
                    {
                        var entry = Files.First().Value;
                        entry.Add(orph, new HashSet<string>());
                    }
                }
            }


            if(asIm != null && asIm.IconId > 0)
            {
                foreach(var file in Files)
                {
                    file.Value.Add("icon", new HashSet<string>());
                }
            }

            // Ensure we have at least a blank entry.
            foreach (var file in Files)
            {
                if (file.Value.Count == 0)
                {
                    file.Value.Add("", new HashSet<string>());
                }
            }
        }

        private async Task<(string model, List<string> materials)> GetMaterialsTask(string model, XivDependencyRoot root, ModTransaction tx)
        {
            var materials = await root.GetMaterialFiles(-1, tx, false);
            return (model, materials);
        }

        private async Task GetTextures(ModTransaction tx)
        {
            if (Files.Count == 0 || Files.First().Value.Count == 0)
            {
                // How did we get here...
                return;
            }

            if (Root == null)
            {
                var uiItem = Item as XivUi;
                var asChar = Item as XivCharacter;
                var set = Files.First().Value.First().Value;

                if (uiItem != null)
                {
                    var paths = await uiItem.GetTexPaths(true, true, MainWindow.DefaultTransaction);
                    set.UnionWith(paths);

                } else if (asChar != null)
                {
                    var paths = await asChar.GetDecalTextures(MainWindow.DefaultTransaction);
                    set.UnionWith(paths);

                }
                return;
            }

            var asIm = Item as IItemModel;

            // Anything with materials is easy. 
            foreach(var mdlKv in Files)
            {
                foreach(var mtrlKv in Files[mdlKv.Key])
                {
                    var mtrl = mtrlKv.Key;
                    List<string> textures = new List<string>();
                    if(mtrlKv.Key == "")
                    {
                        mtrlKv.Value.Add("");
                        break;
                    } else if(mtrlKv.Key == "icon")
                    {
                        if (asIm != null) {
                            textures = await Tex.GetItemIcons(asIm.IconId, tx);
                        }
                    }
                    else
                    {
                        textures = await Mtrl.GetTexturePathsFromMtrlPath(mtrl, false, false, tx);
                    }

                    foreach(var tex in textures)
                    {
                        mtrlKv.Value.Add(tex);
                    }
                }
            }
        }

        /// <summary>
        /// Adds the given paths to the Models combo box.
        /// </summary>
        /// <param name="models"></param>
        private void AddModels(IEnumerable<string> models)
        {
            models = models.OrderBy(x => x);
            Models = new ObservableCollection<KeyValuePair<string, string>>();
            foreach (var model in models)
            {
                if (model == "")
                {
                    Models.Add(new KeyValuePair<string, string>("--", ""));
                    break;
                }

                var niceName = Path.GetFileNameWithoutExtension(model);
                var race = IOUtil.GetRaceFromPath(model);
                var baseName = Root.Info.GetBaseFileName();

                if (race != XivRace.All_Races)
                {
                    niceName = race.GetDisplayName();
                }
                else if (model.StartsWith(baseName) && model != baseName)
                {
                    niceName = model.Substring(Root.Info.GetBaseFileName().Length);
                }

                Models.Add(new KeyValuePair<string, string>(niceName, model));
            }

            if (Models.Count == 0)
            {
                Models.Add(new KeyValuePair<string, string>("--", ""));
            }

            AssignModelWatermark();
            ModelsEnabled = true;

            if (_TargetFileSet && Models.Any(x => x.Value == _TargetFile.ModelKey))
            {
                ModelComboBox.SelectedValue = _TargetFile.ModelKey;
            } else if(!string.IsNullOrWhiteSpace(_PreLoadModel))
            {
                // User was looking at the model panel.
                var info = XivCache.GetFileNameRootInfo(_PreLoadModel, false);
                if (!info.IsValid())
                {
                    // Can't do anything here.  Too weird.
                    ModelComboBox.SelectedIndex = 0;
                } else
                {
                    // See if we have file that matches our primary root info (Ex. Race)
                    var target = Files.Keys.FirstOrDefault(x => {
                        var xinfo = XivCache.GetFileNameRootInfo(x, false);
                        if(xinfo.PrimaryType == info.PrimaryType && xinfo.PrimaryId == info.PrimaryId)
                        {
                            // Matching primaries, Ex. Race
                            return true;
                        }
                        return false;
                    });


                    if (!string.IsNullOrWhiteSpace(target))
                    {
                        // Found a same race/etc. type model.  Keep it.
                        ModelComboBox.SelectedValue = target;
                    } else
                    {
                        // Fallback
                        ModelComboBox.SelectedIndex = 0;
                    }
                }
            } else
            {
                // Default behavior
                ModelComboBox.SelectedIndex = 0;
            }
        }

        /// <summary>
        /// Adds the given paths to the Materials combo box.
        /// </summary>
        /// <param name="models"></param>
        private void AddMaterials(IEnumerable<string> materials)
        {
            materials = materials.OrderBy(x => x);
            Materials = new ObservableCollection<KeyValuePair<string, string>>();
            foreach (var material in materials)
            {
                if (material == "")
                {
                    Materials.Add(new KeyValuePair<string, string>("--", ""));
                    break;
                }
                else if (material == "icon")
                {
                    Materials.Add(new KeyValuePair<string, string>(XivStrings.Icon, "icon"));
                    continue;
                }

                var niceName = Path.GetFileNameWithoutExtension(material);

                // Get the root for just the file name.
                var simpleRoot = XivCache.GetFileNameRootInfo(niceName, false);

                XivRace race = XivRace.All_Races;
                string slot = null;
                string suffix = IOUtil.GetMaterialSuffix(material);
                if (simpleRoot.IsValid())
                {
                    if(simpleRoot.PrimaryType == XivItemType.human)
                    {
                        race = XivRaces.GetXivRace(simpleRoot.PrimaryId);
                    }
                    slot = simpleRoot.Slot;
                }

                if(race != XivRace.All_Races && !string.IsNullOrWhiteSpace(suffix))
                {
                    niceName = race.GetDisplayName() + ": ";
                    if (!string.IsNullOrWhiteSpace(slot)) {
                        niceName += slot.ToUpper() + " ";
                    }
                    niceName += suffix.ToUpper();
                }


                Materials.Add(new KeyValuePair<string, string>(niceName, material));
            }

            if (Materials.Count == 0)
            {
                Materials.Add(new KeyValuePair<string, string>("--", ""));
            }

            AssignMaterialWatermark();
            MaterialsEnabled = true;

            if (_TargetFile.MaterialKey != null && Materials.Any(x => x.Value == _TargetFile.MaterialKey))
            {
                MaterialComboBox.SelectedValue = _TargetFile.MaterialKey;
            }
            else if (!string.IsNullOrWhiteSpace(_PreLoadMaterial))
            {
                // Default behavior
                // In theory, should we snap to some kind of approximately similar material?
                // That seems fairly unclear.
                MaterialComboBox.SelectedIndex = 0;
            }
            else
            {
                // Default behavior
                MaterialComboBox.SelectedIndex = 0;
            }
        }

        /// <summary>
        /// Adds the given paths to the Textures combo box.
        /// </summary>
        /// <param name="textures"></param>
        private async Task AddTextures(IEnumerable<string> textures)
        {
            Textures = new ObservableCollection<KeyValuePair<string, string>>();
            var mtrlPath = (string)MaterialComboBox.SelectedValue;
            XivMtrl mtrl = null;
            if (!string.IsNullOrEmpty(mtrlPath) && IOUtil.IsFFXIVInternalPath(mtrlPath))
            {
                var tx = MainWindow.DefaultTransaction;
                if(await tx.FileExists(mtrlPath))
                {
                    mtrl = await Mtrl.GetXivMtrl(mtrlPath, false, tx);
                }
            }
            

            foreach (var texture in textures)
            {
                if (texture == "")
                {
                    Textures.Add(new KeyValuePair<string, string>("--", ""));
                    break;
                }

                var niceName = Path.GetFileNameWithoutExtension(texture);

                if (mtrl != null && mtrl.Textures.Any(x => x.Dx11Path == texture))
                {
                    niceName = mtrl.ResolveFullUsage(mtrl.Textures.First(x => x.Dx11Path == texture)).ToString() + " - " +  niceName;
                }
                else if (mtrl != null && mtrl.Textures.Any(x => x.Dx9Path == texture))
                {
                    niceName = "DX9 - " + niceName;
                }
                else
                {
                    
                }



                Textures.Add(new KeyValuePair<string, string>(niceName, texture));
            }

            if (Textures.Count == 0)
            {
                Textures.Add(new KeyValuePair<string, string>("--", ""));
            }

            AssignTexturewatermark();
            TexturesEnabled = true;
            if (_TargetFile.TextureKey != null && Textures.Any(x => x.Value == _TargetFile.TextureKey))
            {
                TextureComboBox.SelectedValue = _TargetFile.TextureKey;
            }
            else if(Root == null)
            {
                if(!string.IsNullOrWhiteSpace(_PreLoadTexture))
                {
                    // Do something to select similar texture?
                }

                // Texture only item always snaps to texture.
                var hr = textures.FirstOrDefault(x => x.EndsWith("_hr1.tex"));
                if (!string.IsNullOrWhiteSpace(hr))
                {
                    // Select High res textures by default.
                    TextureComboBox.SelectedValue = hr;
                }
                else
                {
                    TextureComboBox.SelectedIndex = 0;
                }
                ShowPanel(TextureWrapper);
            }
            else
            {
                TextureComboBox.SelectedIndex = 0;
            }
        }

        // Internal state control flag.
        private bool _CANCELLING_COMBO_BOXES;


        public bool HandleUnsaveConfirmation(ComboBox c, SelectionChangedEventArgs e)
        {
            if (_CANCELLING_COMBO_BOXES)
            {
                // Mid-reset.
                return false;
            }

            if (e != null)
            {
                if (e.RemovedItems.Count == 0)
                {
                    // Something broke, unfortunate.
                    return true;
                }
            }

            var res = true;
            bool metadataPrompt = false;
            bool modelPrompt = false;
            bool materialPrompt = false;
            bool texPrompt = false;

            if (MetadataWrapper.UnsavedChanges && (c == null))
            {
                res = MetadataWrapper.FileControl.ConfirmDiscardChanges(MetadataWrapper.FilePath);
                metadataPrompt = true;
            }
            if (ModelWrapper.UnsavedChanges && ((c == ModelComboBox) || c == null))
            {
                res = ModelWrapper.FileControl.ConfirmDiscardChanges(ModelWrapper.FilePath);
                modelPrompt = true;
            }
            if (res && MaterialWrapper.UnsavedChanges && (c == MaterialComboBox || c == ModelComboBox || c == null))
            {
                res = MaterialWrapper.FileControl.ConfirmDiscardChanges(MaterialWrapper.FilePath);
                materialPrompt = true;
            }
            if (res && TextureWrapper.UnsavedChanges && c == null)
            {
                res = TextureWrapper.FileControl.ConfirmDiscardChanges(TextureWrapper.FilePath);
                texPrompt = true;
            }

            // If user rejected any unsave confirmations
            if (!res)
            {

                // Restore selected combo box back to its previous state.
                if (c != null && e != null)
                {
                    _CANCELLING_COMBO_BOXES = true;
                    c.SelectedItem = e.RemovedItems[0];
                    _CANCELLING_COMBO_BOXES = false;
                }

                return false;
            }

            // Clear flags.
            if (res && metadataPrompt)
            {
                MetadataWrapper.UnsavedChanges = false;
            }

            if (res && modelPrompt)
            {
                ModelWrapper.UnsavedChanges = false;
            }

            if (res && materialPrompt)
            {
                MaterialWrapper.UnsavedChanges = false;
            }

            if (res && texPrompt)
            {
                TextureWrapper.UnsavedChanges = false;
            }

            return true;
        }


        private (string ModelKey, string MaterialKey, string TexKey) GetFileKeys(string file)
        {
            if (string.IsNullOrEmpty(file))
            {
                return (null, null, null);
            }

            if(Files == null)
            {
                return (null, null, null);
            }

            foreach (var mkv in Files)
            {
                if (mkv.Key == file)
                {
                    return (file, null, null);
                }
                foreach (var mtkv in mkv.Value)
                {
                    if (mtkv.Key == file)
                    {
                        return (mkv.Key, file, null);
                    }

                    foreach (var tex in mtkv.Value)
                    {
                        if (tex == file)
                        {
                            return (mkv.Key, mtkv.Key, file);
                        }
                    }
                }
            }
            
            // File not in our tree, don't force visibility.
            return ("!", null, null);
        }

        private bool RemoveFile(string file)
        {
            foreach (var mkv in Files)
            {
                if (mkv.Key == file)
                {
                    Files.Remove(file);
                    return true;
                }
                foreach(var mtkv in mkv.Value)
                {
                    if(mtkv.Key == file)
                    {
                        Files[mkv.Key].Remove(file);
                        return true;
                    }

                    foreach(var tex in mtkv.Value)
                    {
                        if(tex == file)
                        {
                            Files[mkv.Key][mtkv.Key].Remove(tex);
                            return true;
                        }
                    }
                }
            }
            return false;
        }

        /// <summary>
        /// Tests if a file exists, safely removing it from the combo boxes as non-disruptively as possible if it doesn't.
        /// </summary>
        /// <param name="shiftUp"></param>
        /// <returns></returns>
        private async Task<bool> SafeRemoveFile(string fileRemoved)
        {
            if (string.IsNullOrWhiteSpace(fileRemoved))
            {
                return false;
            }

            if (!IOUtil.IsFFXIVInternalPath(fileRemoved))
            {
                // Non-FFXIV file.  Either something weird or an internal flag like 'icon'
                return false;
            }

            var tx = MainWindow.DefaultTransaction;
            if(await tx.FileExists(fileRemoved))
            {
                // File still exists, leave it in.
                return false;
            }


            if (!RemoveFile(fileRemoved))
            {
                return false;
            }

            string file = null;
            if (!string.IsNullOrWhiteSpace(fileRemoved))
            {
                var ext = Path.GetExtension(fileRemoved);
                if (TextureWrapper.FileControl.GetValidFileExtensions().Keys.Contains(ext))
                {
                    file = Textures.First().Value;
                    file = file == fileRemoved ? Materials.First().Value : file;
                } else if (MaterialWrapper.FileControl.GetValidFileExtensions().Keys.Contains(ext))
                {
                    file = Materials.First().Value;
                    file = file == fileRemoved ? Models.First().Value : file;
                } else if(ModelWrapper.FileControl.GetValidFileExtensions().Keys.Contains(ext))
                {
                    file = Models.First().Value;
                    file = file == fileRemoved ? null : file;
                } else
                {
                    file = null;
                }
            }

            _TargetFile = GetFileKeys(file);
            AddModels(Files.Keys.ToList());
            return true;
        }

        private async Task SafeAddFile((string ModelKey, string MaterialKey, string TextureKey) keys)
        {
            if(string.IsNullOrWhiteSpace(keys.ModelKey))
            {
                return;
            }

            bool anyChanges = false;

            // Add to the base files dictionary.
            if (!Files.ContainsKey(keys.ModelKey))
            {
                Files.Add(keys.ModelKey, new Dictionary<string, HashSet<string>>());
                anyChanges = true;
            }

            if (keys.MaterialKey != null && !Files[keys.ModelKey].ContainsKey(keys.MaterialKey))
            {
                Files[keys.ModelKey].Add(keys.MaterialKey, new HashSet<string>());
                anyChanges = true;
            }

            if (keys.TextureKey!= null && !Files[keys.ModelKey][keys.MaterialKey].Contains(keys.TextureKey))
            {
                Files[keys.ModelKey][keys.MaterialKey].Add(keys.TextureKey);
                anyChanges = true;
            }

            if(!anyChanges)
            {
                return;
            }

            _TargetFile = keys;

            // Rebuild the list.
            AddModels(Files.Keys.ToList());
        }

        private async void Model_Changed(object sender, SelectionChangedEventArgs e)
        {
            try
            {

                if (ModelComboBox.SelectedValue == null)
                {
                    return;
                }

                if (!HandleUnsaveConfirmation(ModelComboBox, e))
                {
                    return;
                }

                var currentModel = (string)ModelComboBox.SelectedValue;

                if (!string.IsNullOrEmpty(currentModel))
                {
                    if(await SafeRemoveFile(currentModel))
                    {
                        return;
                    }
                }

                if (currentModel == null || !Files.ContainsKey(currentModel))
                {
                    AddMaterials(new List<string>());
                    return;
                }

                var mats = Files[currentModel].Keys.ToList();
                var success = await ModelWrapper.LoadInternalFile(currentModel, Item, null, false);

                if(!_TargetFileLocked && 
                    _TargetFile.ModelKey != null
                    && _TargetFile.MaterialKey == null
                    && _TargetFile.TextureKey == null)
                {
                    ShowPanel(ModelWrapper);
                }

                AddMaterials(mats);
            }
            catch
            {
                // No-Op.
            }
        }

        private async void Material_Changed(object sender, SelectionChangedEventArgs e)
        {

            try
            {
                if (MaterialComboBox.SelectedValue == null)
                {
                    return;
                }


                if (!HandleUnsaveConfirmation(MaterialComboBox, e))
                {
                    return;
                }

                var currentModel = (string)ModelComboBox.SelectedValue;
                if (currentModel == null || !Files.ContainsKey(currentModel))
                {
                    await AddTextures(new List<string>());
                    return;
                }
                var currentMaterial = (string)MaterialComboBox.SelectedValue;

                if (!string.IsNullOrEmpty(currentMaterial))
                {
                    if (await SafeRemoveFile(currentMaterial))
                    {
                        return;
                    }
                }

                if (currentMaterial == null || !Files[currentModel].ContainsKey(currentMaterial))
                {
                    await AddTextures(new List<string>());
                    return;
                }

                var texs = Files[currentModel][currentMaterial];

                var success = await MaterialWrapper.LoadInternalFile(currentMaterial, Item, null, false);

                if (!_TargetFileLocked 
                    && _TargetFile.MaterialKey != null
                    && _TargetFile.TextureKey == null)
                {
                    ShowPanel(MaterialWrapper);
                }

                await AddTextures(texs);
            }
            catch(Exception ex)
            {
                // No-Op.
                Trace.WriteLine(ex);
            }

        }

        private async void Texture_Changed(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                if (TextureComboBox.SelectedValue == null)
                {
                    return;
                }

                if (!HandleUnsaveConfirmation(TextureComboBox, e))
                {
                    return;
                }


                var tex = (string)TextureComboBox.SelectedValue;

                if (!string.IsNullOrEmpty(tex))
                {
                    if (await SafeRemoveFile(tex))
                    {
                        return;
                    }
                }

                var success = await TextureWrapper.LoadInternalFile(tex, Item, null, false);

                if (!_TargetFileLocked && _TargetFile.TextureKey != null)
                {
                    ShowPanel(TextureWrapper);
                }
            }
            catch
            {
                // No-Op.  Should be handled elsewhere, this is just super safety.
            }

            // All done here.
            _PreLoadModel = null;
            _PreLoadMaterial = null;
            _PreLoadTexture = null;
            _TargetFile = (null, null, null);
        }

        private async void ItemInfo_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                await SharedItemsWindow.ShowSharedItems(Item);
            } catch(Exception ex)
            {
                Trace.WriteLine(ex);
            }
        }

        private FileWrapperControl _VisiblePanel;
        private void ShowPanel(FileWrapperControl control)
        {
            _VisiblePanel = control;

            ModelBorder.BorderBrush = UnselectedBrush;
            MaterialBorder.BorderBrush = UnselectedBrush;
            TextureBorder.BorderBrush = UnselectedBrush;
            //ModelBorder.Margin = new Thickness(5);
            //MaterialBorder.Margin = new Thickness(5);
            //TextureBorder.Margin = new Thickness(5);

            foreach (var panel in FileControls)
            {
                panel.Visibility = panel == control ? Visibility.Visible : Visibility.Collapsed;
            }

            if(control == ModelWrapper && ModelWrapper.FileControl.HasFile)
            {
                //ModelBorder.Margin = new Thickness(2);
                ModelBorder.BorderBrush = SelectedBrush;
            } else if(control == MaterialWrapper && MaterialWrapper.FileControl.HasFile)
            {
                //MaterialBorder.Margin = new Thickness(2);
                MaterialBorder.BorderBrush = SelectedBrush;
            } else if (control == TextureWrapper && TextureWrapper.FileControl.HasFile)
            {
                //TextureBorder.Margin = new Thickness(2);
                TextureBorder.BorderBrush = SelectedBrush;
            }
        }

        private void ShowTexture_Click(object sender, RoutedEventArgs e)
        {
            ShowPanel(TextureWrapper);
        }

        private void ShowMaterial_Click(object sender, RoutedEventArgs e)
        {
            ShowPanel(MaterialWrapper);
        }

        private void ShowModel_Click(object sender, RoutedEventArgs e)
        {
            ShowPanel(ModelWrapper);
        }

        private void ShowExtraButtons_Click(object sender, RoutedEventArgs e)
        {
            if (ButtonGrid.Visibility == Visibility.Visible)
            {
                ButtonGrid.Visibility = Visibility.Collapsed;
                //var icon = new FontAwesomeExtension(PackIconFontAwesomeKind.SortUpSolid);
                //ShowExtraButtonsButton.Content = icon;
                //ExtraButtonsIcon.Kind = PackIconFontAwesomeKind.SortUpSolid;
            } else
            {
                ButtonGrid.Visibility = Visibility.Visible;
                //var icon = new FontAwesomeExtension(PackIconFontAwesomeKind.SortDownSolid);
                //ShowExtraButtonsButton.Content = icon;
                //ExtraButtonsIcon.Kind = PackIconFontAwesomeKind.SortDownSolid;
            }
        }

        private void ShowMetadata_Click(object sender, RoutedEventArgs e)
        {
            ShowPanel(MetadataWrapper);
        }

        private async void AddMaterial_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var matControl = (MaterialFileControl)MaterialWrapper.FileControl;
                var currentModel = (string) ModelComboBox.SelectedValue;
                var result = await CreateMaterialDialog.ShowCreateMaterialDialogSimple(matControl.Material, currentModel, Window.GetWindow(this));
                if(result == null)
                {
                    return;
                }

                // Load the new data at the new path.
                var data = Mtrl.XivMtrlToUncompressedMtrl(result);
                await SimpleFileViewWindow.OpenFile(result.MTRLPath, Item, data, typeof(MaterialFileControl), Window.GetWindow(this));
            }
            catch(Exception ex)
            {
                Trace.WriteLine(ex);
            }
        }

        private bool _DROPDOWN_OPEN;
        private bool _DROPDOWN_CANCEL;
        private bool disposedValue;

        private void Combobox_DropdownOpened(object sender, EventArgs e)
        {
            _DROPDOWN_OPEN = true;
            _DROPDOWN_CANCEL = false;
        }

        private void ModelComboBox_DropDownClosed(object sender, EventArgs e)
        {
            if (_DROPDOWN_CANCEL)
            {
                _DROPDOWN_CANCEL = false;
                return;
            }

            _DROPDOWN_CANCEL = false;
            ShowPanel(ModelWrapper);
        }

        private void MaterialComboBox_DropDownClosed(object sender, EventArgs e)
        {
            if (_DROPDOWN_CANCEL)
            {
                _DROPDOWN_CANCEL = false;
                return;
            }

            _DROPDOWN_CANCEL = false;
            ShowPanel(MaterialWrapper);
        }

        private void TextureComboBox_DropDownClosed(object sender, EventArgs e)
        {
            if (_DROPDOWN_CANCEL)
            {
                _DROPDOWN_CANCEL = false;
                return;
            }

            _DROPDOWN_CANCEL = false;
            ShowPanel(TextureWrapper);
        }
        private void Wind_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (_DROPDOWN_OPEN)
            {
                var controlSource = e.OriginalSource as Control;
                if (controlSource != null && controlSource.Parent == null)
                {
                    // Clicked on the content of the dropdown, which is just null-parent floating text boxes.
                    // This doesn't close the popup window.
                    return;
                }
                _DROPDOWN_CANCEL = true;
            }
        }
        private void Wind_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if(e.Key == Key.Escape)
            {
                if (_DROPDOWN_OPEN)
                {
                    _DROPDOWN_CANCEL = true;
                }
            }
        }

        private Action<IItem> _DebouncedRebuildComboBoxes;
        private async void DispatchRebuildComboBoxes(IItem item)
        {
            try
            {
                await await Dispatcher.InvokeAsync(async () =>
                {
                    var tx = MainWindow.DefaultTransaction;
                    _TargetFile = GetFileKeys(_VisiblePanel.FilePath);
                    await RebuildComboBoxes(tx);
                });
            }
            catch (Exception ex)
            {
                // No-Op
                Trace.WriteLine(ex);
            }
        }


        private async void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var success = await SetItem(Item, _VisiblePanel.FilePath);
            }
            catch
            {
                // No op
            }
        }

        private async void PopOut_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var success = await SimpleItemViewWindow.ShowItem(Item, Window.GetWindow(this));
            }
            catch
            {
                // No op
            }
        }

        private void RemoveDeletedHandler(FileWrapperControl fw)
        {
            if(fw == null)
            {
                return;
            }
            if(fw.FileControl == null)
            {
                return;
            }

            fw.FileControl.FileDeleted -= FileControl_FileDeleted;
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                // This part isn't strictly needed if we're nulling the refs already,
                // But it doesn't hurt to be safe.
                RemoveDeletedHandler(MetadataWrapper);
                RemoveDeletedHandler(ModelWrapper);
                RemoveDeletedHandler(MaterialWrapper);
                RemoveDeletedHandler(TextureWrapper);

                // Global event handlers definitely have to go.
                ModTransaction.FileChangedOnCommit -= Tx_FileChanged;
                TxWatcher.UserTxStarted -= OnUserTransactionStarted;

                if (MainWindow.UserTransaction != null)
                {
                    MainWindow.UserTransaction.FileChanged -= Tx_FileChanged;
                    MainWindow.UserTransaction.TransactionClosed -= Tx_Closed;
                }

                if (disposing)
                {
                    MetadataWrapper.Dispose();
                    ModelWrapper.Dispose();
                    MaterialWrapper.Dispose();
                    TextureWrapper.Dispose();

                    MetadataWrapper = null;
                    ModelWrapper = null;
                    MaterialWrapper = null;
                    TextureWrapper = null;
                }

                disposedValue = true;
            }
        }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        private async void RemoveOrphans_Click(object sender, RoutedEventArgs e)
        {
            if(Root == null)
            {
                return;
            }
            try
            {
                var orphans = await Root.GetOrphanedFiles(MainWindow.DefaultTransaction);
                if(orphans.Count == 0)
                {
                    FlexibleMessageBox.Show(this.GetWin32Window(), "This item tree currently has no orphaned files.", "No Orphans Notification", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Information, System.Windows.Forms.MessageBoxDefaultButton.Button1);
                    return;
                }

                var orphanText = string.Join("\n", orphans);

                FlexibleMessageBox.Show(this.GetWin32Window(), "This will delete the following orphaned/unused mod files:\n\n" + orphanText + "\n\n Continue?", "Delete Orphans Confirmation", System.Windows.Forms.MessageBoxButtons.OKCancel, System.Windows.Forms.MessageBoxIcon.Warning, System.Windows.Forms.MessageBoxDefaultButton.Button1);

                var tx = MainWindow.UserTransaction;
                var boiler = TxBoiler.BeginWrite(ref tx);
                var states = new List<TxFileState>();
                try
                {
                    foreach (var file in orphans)
                    {
                        states.Add(await tx.SaveFileState(file));
                        await Modding.DeleteMod(file, tx);
                    }
                    await boiler.Commit();
                    await SetItem(Item, _VisiblePanel.FilePath);
                }
                catch
                {
                    await boiler.Catch(states);
                    throw;
                }


            }
            catch(Exception ex)
            {
                this.ShowError("Delete File Error", "An error occurred while deleting the files: \n\n" + ex.Message);
            }
        }
    }
}
