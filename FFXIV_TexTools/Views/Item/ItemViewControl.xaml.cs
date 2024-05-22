using FFXIV_TexTools.Resources;
using FFXIV_TexTools.Views.Controls;
using MahApps.Metro.Controls;
using MahApps.Metro.IconPacks;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
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
using xivModdingFramework.Items.Enums;
using xivModdingFramework.Items.Interfaces;
using xivModdingFramework.Materials.FileTypes;
using xivModdingFramework.Mods;
using xivModdingFramework.Variants.FileTypes;

namespace FFXIV_TexTools.Views.Item
{
    public partial class ItemViewControl : UserControl, INotifyPropertyChanged
    {
        /*  This class is the primary container for the core TexTools "Item" based view system.
         *  It primarily serves a wrapper for the various File Wrapper tabs.
         *  With the majority of its logic serving to parse an IItem into its constituent files,
         *  and pass those file paths to the correct places,
         *  as well as wrapping those file paths into dropdown selections in some of the tabs.
        */

        private List<FileWrapperControl> FileControls = new List<FileWrapperControl>();
        public ItemViewControl()
        {
            DataContext = this;
            InitializeComponent();
            ActiveView = this;

            TextureWrapper.SetControlType(typeof(TextureFileControl));
            MaterialWrapper.SetControlType(typeof(MaterialFileControl));
            ModelWrapper.SetControlType(typeof(ModelFileControl));
            MetadataWrapper.SetControlType(typeof(MetadataFileControl));

            FileControls.Add(TextureWrapper);
            FileControls.Add(MaterialWrapper);
            FileControls.Add(ModelWrapper);
            FileControls.Add(MetadataWrapper);

            MetadataWrapper.FileControl.FileSaved += OnMetadataSaved;

            ExtraButtonsRow.Height = new GridLength(0);

            // Go ahead and show the model panel.
            // The viewport there can take a second to initialize,
            // So it helps a little to get it visible to kick that stuff off earlier.
            ShowPanel(ModelWrapper);
        }

        private async void OnMetadataSaved(FileViewControl sender, bool success)
        {
            try
            {
                // Always reload the item on Metadata reload, to be safe.
                _TargetFile = Item.GetRoot().Info.GetRootFile();
                await SetItem(Item);
            }
            catch
            {
                // No-Op, just safety catch.
            }
        }

        #region Basic IPropertyNotify Properties Pattern

        public event PropertyChangedEventHandler PropertyChanged;

        public static ItemViewControl ActiveView { get; private set; }

        private IItem _Item;
        public IItem Item
        {
            get => _Item;
            private set
            {
                _Item = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Item)));
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


        private bool _LoadSourceSet = false;
        private string _TargetFile = null;

        /// <summary>
        /// Internal dictionary structure representing all of the files available for this item.
        /// [Model] => [Referenced Materials] => [Referenced Textures]
        /// 
        /// If a layer is missing, empty string is used as the only key.
        /// </summary>
        private Dictionary<string, Dictionary<string, HashSet<string>>> Files;

        private XivDependencyRoot Root;

        /// <summary>
        /// Simple shortcut accessor and null checking for ActiveView.SetItem(item)
        /// </summary>
        /// <param name="item"></param>
        /// <returns></returns>
        public static async Task StaticSetItem(IItem item)
        {
            if(ActiveView == null)
            {
                return;
            }
            await ActiveView.SetItem(item);
        }

        /// <summary>
        /// Primary setter for other areas in TexTools to assign an item to this view.
        /// </summary>
        /// <param name="item"></param>
        /// <returns></returns>
        public async Task SetItem(IItem item)
        {
            await MainWindow.GetMainWindow().LockUi("Loading Item", "Please wait...", this);
            try
            {
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

                await TextureWrapper.ClearFile();
                await MaterialWrapper.ClearFile();
                await ModelWrapper.ClearFile();
                await MetadataWrapper.ClearFile();

                if (Item == null)
                {
                    return;
                }

                ItemInfoEnabled = true;
                Root = Item.GetRoot();

                if (Root == null)
                {
                    // Texture only item.  Not implemented yet.
                    TexturesEnabled = true;
                    throw new NotImplementedException();
                    return;
                }

                // We can add materials to anything with a root.
                AddMaterialEnabled = true;

                // Populate the Files structure.
                var tx = MainWindow.DefaultTransaction;
                await GetModels(tx);
                await GetMaterials(tx);
                await GetTextures(tx);

                // Load metadata view manually since it's not handled by the above functions.
                var success = await MetadataWrapper.LoadInternalFile(Root.Info.GetRootFile(), Item, null, false);
                if (success)
                {
                    MetadataEnabled = true;
                }

                if(!string.IsNullOrWhiteSpace(_TargetFile))
                {
                    _LoadSourceSet = true;
                }
                // Assign the first combo box, which will kick off the children boxes in turn.
                AddModels(Files.Keys.ToList());


                if (!string.IsNullOrWhiteSpace(_TargetFile))
                {
                    await ShowFile(_TargetFile);
                }
            }
            catch(Exception ex) 
            {
                this.ShowError("Item Load Error", "An error occurred while loading the item:\n\n" + ex.Message);
            }
            finally
            {
                await MainWindow.GetMainWindow().UnlockUi(this);
            }
        }

        /// <summary>
        /// Populates the top level strcture of the Files dictionary.
        /// </summary>
        private async Task GetModels(ModTransaction tx)
        {
            TextBoxHelper.SetWatermark(ModelComboBox, UIStrings.Model);
            if (Root == null)
            {
                Files.Add("", new Dictionary<string, HashSet<string>>());
                AddModels(new List<string>());
                return;
            }

            var asIm = Item as IItemModel;
            if (asIm == null)
            {
                // If we got handed a malformed item, just load by root.
                var models = await Root.GetModelFiles(tx);
                foreach(var m in models)
                {
                    Files.Add(m, new Dictionary<string, HashSet<string>>());
                }
                return;
            }

            if(Root.Info.PrimaryType != XivItemType.human)
            {
                // Anything outside of the human tree is fine to ship as-is.
                var models = await Root.GetModelFiles(tx);
                foreach (var m in models)
                {
                    Files.Add(m, new Dictionary<string, HashSet<string>>());
                }
                return;
            }

            var sType = Root.Info.SecondaryType.Value;
            if(Root.Info.SecondaryId != 0)
            {
                // Need to handle this case later.
                throw new NotImplementedException();
            }


            if(sType == XivItemType.face)
            {
                TextBoxHelper.SetWatermark(ModelComboBox, XivStrings.Face + " ID");

            } else if(sType == XivItemType.body)
            {
                TextBoxHelper.SetWatermark(ModelComboBox, XivStrings.Body + " ID");

            } else if(sType == XivItemType.tail)
            {
                TextBoxHelper.SetWatermark(ModelComboBox, XivStrings.Tail + " ID");

            } else if(sType == XivItemType.hair)
            {
                TextBoxHelper.SetWatermark(ModelComboBox, XivStrings.Hair + " ID");
            }
            else if (sType == XivItemType.ear)
            {
                TextBoxHelper.SetWatermark(ModelComboBox, XivStrings.Ear + " ID");
            }
            else
            {
                // Malformed item.
                throw new InvalidDataException("The given item was invalid.");
            }

            var race = XivRaces.GetXivRace(Root.Info.PrimaryId);

            var values = await Character.GetAvailableSecondaryIds(race, sType, MainWindow.DefaultTransaction);

            foreach (var v in values)
            {
                Files.Add(v.ToString(), new Dictionary<string, HashSet<string>>());
            }

            if(Files.Count == 0)
            {
                Files.Add("", new Dictionary<string, HashSet<string>>());
            }
        }


        /// <summary>
        /// Populates the second level structure of the Files dictionary
        /// </summary>
        /// <returns></returns>
        private async Task GetMaterials(ModTransaction tx)
        {
            if(Root == null)
            {
                Files.Add("", new Dictionary<string, HashSet<string>>());
                return;
            }

            var asIm = Item as IItemModel;
            var materialSet = -1;
            if(asIm != null)
            {
                materialSet = await Imc.GetMaterialSetId(asIm, false, tx);
            }

            if (Root.Info.PrimaryType != XivItemType.human)
            {
                // Standard resolution.  Resolve by referenced materials.
                foreach (var file in Files)
                {
                    var model = file.Key;
                    var materials = await Root.GetVariantShiftedMaterials(model, materialSet, tx);
                    foreach(var mat in materials)
                    {
                        if (!Files[model].ContainsKey(mat))
                        {
                            Files[model].Add(mat, new HashSet<string>());
                        }
                    }
                }

            }
            else
            {
                // Human Folder, the most exceptional nonsense.
                foreach(var file in Files)
                {
                    if(file.Key == "")
                    {
                        break;
                    }

                    // For these, we really are crunching multiple roots into one "item".
                    // So rip the secondary ID back off the "Model" field, and reconstruct the full root.
                    var secondaryId = Int32.Parse(file.Key);
                    var newInfo = (XivDependencyRootInfo)Root.Info.Clone();
                    newInfo.SecondaryId = secondaryId;
                    var newRoot = new XivDependencyRoot(newInfo);

                    // Then let the root logic find all the materials, and attach them.
                    // Orphans skipped here since we stick them in later anyways.
                    var materials = await newRoot.GetMaterialFiles(-1, tx, false);
                    foreach(var material in materials)
                    {
                        file.Value.Add(material, new HashSet<string>());
                    }
                }
            }

            // Ensure we have at least a blank entry.
            foreach(var file in Files)
            {
                if(file.Value.Count == 0)
                {
                    file.Value.Add("", new HashSet<string>());
                }
            }

            var orphanMaterials = await Root.GetOrphanMaterials(materialSet, tx);
            if (Root.Info.SecondaryType != null)
            {
                // If there is a secondary ID, just snap these onto the first entry, because there's only one model (or 0).
                var entry = Files.First().Value;
                foreach (var orph in orphanMaterials)
                {
                    if (!entry.ContainsKey(orph)) {
                        entry.Add(orph, new HashSet<string>());
                    }
                }
            }
            else
            {
                foreach (var orph in orphanMaterials)
                {
                    // This goes to the matching fake-secondary entry, if there is one.
                    // Ex. on Equipment, the race is a fake secondary value.
                    var secondary = IOUtil.GetSecondaryIdFromFileName(orph);
                    var match = Files.FirstOrDefault(x => IOUtil.GetSecondaryIdFromFileName(x.Key) == secondary);
                    if(match.Key != null && match.Value != null)
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
        }
        private async Task GetTextures(ModTransaction tx)
        {

            // Anything with materials is easy. 
            foreach(var mdlKv in Files)
            {
                foreach(var mtrlKv in Files[mdlKv.Key])
                {
                    var mtrl = mtrlKv.Key;
                    if(mtrlKv.Key == "")
                    {
                        break;
                    }

                    var textures = await Mtrl.GetTexturePathsFromMtrlPath(mtrl, false, false, tx);
                    foreach(var tex in textures)
                    {
                        mtrlKv.Value.Add(tex);
                    }
                }
            }

            if(Files.Count == 0)
            {
                // TODO: Add resoultion for texture-only items.
                Files.Add("", new Dictionary<string, HashSet<string>>());
            }
        }

        /// <summary>
        /// Adds the given paths to the Models combo box.
        /// </summary>
        /// <param name="models"></param>
        private void AddModels(IEnumerable<string> models)
        {
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

            ModelsEnabled = true;
            ModelComboBox.SelectedIndex = 0;
        }

        /// <summary>
        /// Adds the given paths to the Materials combo box.
        /// </summary>
        /// <param name="models"></param>
        private void AddMaterials(IEnumerable<string> materials)
        {
            Materials = new ObservableCollection<KeyValuePair<string, string>>();
            foreach (var material in materials)
            {
                if(material == "")
                {
                    Materials.Add(new KeyValuePair<string, string>("--", ""));
                    break;
                }
                var niceName = Path.GetFileNameWithoutExtension(material);
                var race = IOUtil.GetRaceFromPath(material);
                var baseName = Root.Info.GetBaseFileName();

                Materials.Add(new KeyValuePair<string, string>(niceName, material));
            }

            if (Materials.Count == 0)
            {
                Materials.Add(new KeyValuePair<string, string>("--", ""));
            }

            MaterialComboBox.SelectedIndex = 0;
            MaterialsEnabled = true;
        }

        /// <summary>
        /// Adds the given paths to the Textures combo box.
        /// </summary>
        /// <param name="textures"></param>
        private void AddTextures(IEnumerable<string> textures)
        {
            Textures = new ObservableCollection<KeyValuePair<string, string>>();
            foreach (var texture in textures)
            {
                if (texture == "")
                {
                    Textures.Add(new KeyValuePair<string, string>("--", ""));
                    break;
                }

                var niceName = Path.GetFileNameWithoutExtension(texture);
                var race = IOUtil.GetRaceFromPath(texture);
                var baseName = Root.Info.GetBaseFileName();

                Textures.Add(new KeyValuePair<string, string>(niceName, texture));
            }

            if (Textures.Count == 0)
            {
                Textures.Add(new KeyValuePair<string, string>("--", ""));
            }

            TexturesEnabled = true;
            TextureComboBox.SelectedIndex = 0;
        }

        // Internal state control flag.
        private bool _CANCELLING_COMBO_BOXES;


        private async Task<bool> HandleUnsaveConfirmation(ComboBox c, SelectionChangedEventArgs e)
        {
            if (_CANCELLING_COMBO_BOXES)
            {
                // Mid-reset.
                return false;
            }

            if(e.RemovedItems.Count == 0)
            {
                // Something broke, unfortunate.
                return true;
            }

            var res = true;
            bool modelPrompt = false;
            bool materialPrompt = false;
            bool texPrompt = false;

            if (ModelWrapper.UnsavedChanges && (c == ModelComboBox))
            {
                res = ModelWrapper.FileControl.ConfirmDiscardChanges(ModelWrapper.FilePath);
                modelPrompt = true;
            }
            if (res && MaterialWrapper.UnsavedChanges && (c == MaterialComboBox || c == ModelComboBox))
            {
                res = MaterialWrapper.FileControl.ConfirmDiscardChanges(MaterialWrapper.FilePath);
                materialPrompt = true;
            }
            if (res && TextureWrapper.UnsavedChanges)
            {
                res= TextureWrapper.FileControl.ConfirmDiscardChanges(TextureWrapper.FilePath);
                texPrompt = true;
            }

            // If user rejected any unsave confirmations
            if (!res)
            {

                // Restore selected combo box back to its previous state.
                _CANCELLING_COMBO_BOXES = true;
                c.SelectedItem = e.RemovedItems[0];
                _CANCELLING_COMBO_BOXES = false;

                return false;
            }

            // Clear flags.
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

        private async void Model_Changed(object sender, SelectionChangedEventArgs e)
        {

            try
            {
                if (ModelComboBox.SelectedValue == null)
                {
                    return;
                }

                if (!await HandleUnsaveConfirmation(ModelComboBox, e))
                {
                    return;
                }

                var currentModel = (string)ModelComboBox.SelectedValue;
                if (currentModel == null || !Files.ContainsKey(currentModel))
                {
                    AddMaterials(new List<string>());
                    return;
                }
                var mats = Files[currentModel].Keys.ToList();

                if(Root != null && Root.Info.PrimaryType == XivItemType.human)
                {
                    // For these, we really are crunching multiple roots into one "item".
                    // So rip the secondary ID back off the "Model" field, and reconstruct the full root.
                    var secondaryId = Int32.Parse(currentModel);
                    var newInfo = (XivDependencyRootInfo)Root.Info.Clone();
                    newInfo.SecondaryId = secondaryId;
                    var newRoot = new XivDependencyRoot(newInfo);

                    // These then only have a single model, so...
                    var tx = MainWindow.DefaultTransaction;
                    var model = (await newRoot.GetModelFiles(tx)).FirstOrDefault();
                    currentModel = model;
                }

                var success = await ModelWrapper.LoadInternalFile(currentModel, Item, null, false);
                if (success && !_LoadSourceSet)
                {
                    _LoadSourceSet = true;
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


                if (!await HandleUnsaveConfirmation(MaterialComboBox, e))
                {
                    return;
                }

                var currentModel = (string)ModelComboBox.SelectedValue;
                if (currentModel == null || !Files.ContainsKey(currentModel))
                {
                    AddTextures(new List<string>());
                    return;
                }
                var currentMaterial = (string)MaterialComboBox.SelectedValue;
                if (currentMaterial == null || !Files[currentModel].ContainsKey(currentMaterial))
                {
                    AddTextures(new List<string>());
                    return;
                }

                var texs = Files[currentModel][currentMaterial];

                var success = await MaterialWrapper.LoadInternalFile(currentMaterial, Item, null, false);
                if (success && !_LoadSourceSet)
                {
                    _LoadSourceSet = true;
                    ShowPanel(MaterialWrapper);
                }

                AddTextures(texs);

            }
            catch
            {
                // No-Op.
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

                if (!await HandleUnsaveConfirmation(TextureComboBox, e))
                {
                    return;
                }


                var tex = (string)TextureComboBox.SelectedValue;
                var success = await TextureWrapper.LoadInternalFile(tex, Item, null, false);

                if (success && !_LoadSourceSet)
                {
                    _LoadSourceSet = true;
                    ShowPanel(TextureWrapper);
                }
            }
            catch
            {
                // No-Op.  Should be handled elsewhere, this is just super safety.
            }

            _LoadSourceSet = false;
        }

        private void ItemInfo_Click(object sender, RoutedEventArgs e)
        {

        }

        private void ShowPanel(FileWrapperControl control)
        {
            foreach(var panel in FileControls)
            {
                panel.Visibility = panel == control ? Visibility.Visible : Visibility.Collapsed;
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
            if (ExtraButtonsRow.Height.Value == 0)
            {
                ExtraButtonsRow.Height =  new GridLength(40);
                //var icon = new FontAwesomeExtension(PackIconFontAwesomeKind.SortUpSolid);
                //ShowExtraButtonsButton.Content = icon;
                //ExtraButtonsIcon.Kind = PackIconFontAwesomeKind.SortUpSolid;
            } else
            {
                ExtraButtonsRow.Height = new GridLength(0);
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
                var result = await CreateMaterialDialog.ShowCreateMaterialDialogSimple(currentModel, matControl.Material, Item, Window.GetWindow(this));
                if(result == null)
                {
                    return;
                }

                // Load the new data at the new path.
                var data = Mtrl.XivMtrlToUncompressedMtrl(result);
                await MaterialWrapper.LoadInternalFile(result.MTRLPath, Item, data, false);
                ShowPanel(MaterialWrapper);

                // Hook the next material save to reload the item, so that we can get our new material in our combo boxes.
                matControl.FileSaved += MatControl_FileSaved;
                matControl.FileLoaded += MatControl_FileLoaded;
            }
            catch(Exception ex)
            {
                Trace.Write(ex);
            }
        }

        private void MatControl_FileLoaded(FileViewControl sender, bool success)
        {
            // User switched off the new material without saving it.
            MaterialWrapper.FileControl.FileLoaded -= MatControl_FileLoaded;
            MaterialWrapper.FileControl.FileSaved -= MatControl_FileSaved;
        }

        private async void MatControl_FileSaved(FileViewControl sender, bool success)
        {
            try
            {
                var matControl = (MaterialFileControl)MaterialWrapper.FileControl;
                matControl.FileLoaded -= MatControl_FileLoaded;
                matControl.FileSaved -= MatControl_FileSaved;

                _TargetFile = matControl.Material.MTRLPath;
                await SetItem(Item);
            } catch(Exception ex)
            {
                // No op, just safety catch.
                Trace.WriteLine(ex);
            }
        }

        /// <summary>
        /// Shows the given file in the appropriate view, IF the file exists in this item.
        /// Otherwise pops out a view for the file.
        /// </summary>
        /// <param name="file"></param>
        public async Task ShowFile(string file)
        {
            FileWrapperControl control = GetControlForFile(file);
            if (control != null)
            {
                var success = await control.LoadInternalFile(file);

                if (success)
                {
                    ShowPanel(control);
                }
            } else
            {
                await SimpleFileViewWindow.OpenFile(file);
            }
        }

        private FileWrapperControl GetControlForFile(string file)
        {
            if(Root != null)
            {
                if(file == Root.Info.GetRootFile())
                {
                    return MetadataWrapper;
                }
            }

            foreach (var modelKv in Files)
            {
                if (modelKv.Key == file)
                {
                    return ModelWrapper;
                }
                foreach (var materialKv in modelKv.Value)
                {
                    if (materialKv.Key == file)
                    {
                        return MaterialWrapper;
                    }
                    foreach (var texture in materialKv.Value)
                    {
                        if (texture == file)
                        {
                            return TextureWrapper;
                        }
                    }
                }
            }

            return null;
        }
    }
}
