using FFXIV_TexTools.Helpers;
using FFXIV_TexTools.Properties;
using FFXIV_TexTools.Resources;
using FFXIV_TexTools.Views.Models;
using SharpDX;
using SharpDX.Direct3D9;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Input;
using xivModdingFramework.Cache;
using xivModdingFramework.General.Enums;
using xivModdingFramework.Helpers;
using xivModdingFramework.Items;
using xivModdingFramework.Items.Enums;
using xivModdingFramework.Items.Interfaces;
using xivModdingFramework.Models.DataContainers;
using xivModdingFramework.Models.FileTypes;
using xivModdingFramework.Models.Helpers;

namespace FFXIV_TexTools.ViewModels
{
    public class ImportModelEditViewModel
    {


        private ImportModelEditView _view;
        private TTModel _newModel;
        private TTModel _oldModel;

        private readonly Regex ImcAttributeRegex = new Regex("^atr_([a-z]{2})_([a-j])$");

        private readonly Regex DefaultSkinRegex = new Regex("\\/mt_c[0-9]{4}b0001_a\\.mtrl");
        private readonly Regex ItemMaterialRegex = new Regex("\\/mt_c([0-9]{4})[e|a][0-9]{4}_[a-z0-9]{3}_([a-z])+\\.mtrl");
        private const string SkinMaterial = "/mt_c0101b0001_a.mtrl";
        private readonly KeyValuePair<string, string> DefaultTag = new KeyValuePair<string, string>("_!ADDNEW!_", "Add Attributes...".L());
        private readonly KeyValuePair<string, string> CustomTag = new KeyValuePair<string, string>("_!CUSTOM!_", "Custom".L());
        private readonly KeyValuePair<string, string> SkinTag = new KeyValuePair<string, string>(SkinMaterial, "Skin".L());
        private readonly string UnknownText = "Unknown".L();

        private readonly float OldModelSize;
        private readonly float NewModelSize;
        private const float MinAcceptableSize = 0.5f;
        private const float MaxAcceptableSize = 2f;

        private HashSet<string> RootMaterials = new HashSet<string>();

        private XivDependencyRoot _root;

        private TTMeshGroup GetGroup()
        {
            if (_view.MeshNumberBox.SelectedValue == null)
                return null;

            var mIdx = (int)_view.MeshNumberBox.SelectedValue;
            return _newModel.MeshGroups[mIdx];

        }
        private TTMeshPart GetPart()
        {
            var m = GetGroup();
            if(m == null || _view.PartNumberBox.SelectedValue == null)
            {
                return null;
            }
            var pIdx = (int) _view.PartNumberBox.SelectedValue;
            return m.Parts[pIdx];

        }

        public ImportModelEditViewModel(ImportModelEditView view, TTModel newModel, TTModel oldModel)
        {
            _view = view;
            _newModel = newModel;
            _oldModel = oldModel;




            // Get all the materials available.

            // Merge all the default skin materials together, since FFXIV auto-handles them anyways.
            foreach (var m in _newModel.MeshGroups)
            {
                if (m.Material == null)
                {
                    // Sanity assurance.
                    m.Material = _newModel.MeshGroups[0].Material;
                }


                var result = DefaultSkinRegex.Match(m.Material);
                if (result.Success)
                {
                    m.Material = SkinMaterial;
                }
            }

            // Calculate the model bounding box sizes.
            float minX = 9999.0f, minY = 9999.0f, minZ = 9999.0f;
            float maxX = -9999.0f, maxY = -9999.0f, maxZ = -9999.0f;
            foreach (var m in _newModel.MeshGroups)
            {
                foreach (var p in m.Parts)
                {
                    foreach (var v in p.Vertices)
                    {
                        minX = minX < v.Position.X ? minX : v.Position.X;
                        minY = minY < v.Position.Y ? minY : v.Position.Y;
                        minZ = minZ < v.Position.Z ? minZ : v.Position.Z;

                        maxX = maxX > v.Position.X ? maxX : v.Position.X;
                        maxY = maxY > v.Position.Y ? maxY : v.Position.Y;
                        maxZ = maxZ > v.Position.Z ? maxZ : v.Position.Z;
                    }
                }
            }

            Vector3 min = new Vector3(minX, minY, minZ);
            Vector3 max = new Vector3(maxX, maxY, maxZ);

            NewModelSize = Vector3.Distance(min, max);

            minX = 9999.0f; minY = 9999.0f; minZ = 9999.0f;
            maxX = -9999.0f; maxY = -9999.0f; maxZ = -9999.0f;
            foreach (var m in _oldModel.MeshGroups)
            {
                foreach (var p in m.Parts)
                {
                    foreach (var v in p.Vertices)
                    {
                        minX = minX < v.Position.X ? minX : v.Position.X;
                        minY = minY < v.Position.Y ? minY : v.Position.Y;
                        minZ = minZ < v.Position.Z ? minZ : v.Position.Z;

                        maxX = maxX > v.Position.X ? maxX : v.Position.X;
                        maxY = maxY > v.Position.Y ? maxY : v.Position.Y;
                        maxZ = maxZ > v.Position.Z ? maxZ : v.Position.Z;
                    }
                }
            }

            min = new Vector3(minX, minY, minZ);
            max = new Vector3(maxX, maxY, maxZ);

            OldModelSize = Vector3.Distance(min, max);

            if(newModel.MeshGroups.Count > 0)
            {
                _view.ModelTypeComboBox.SelectedValue = newModel.MeshGroups[0].MeshType;
            }

            AsyncInit();
        }

        private async Task AsyncInit()
        {
            // Get this model's root.
            _root = await XivCache.GetFirstRoot(_oldModel.Source);

            if (_root != null && _root.Info.PrimaryType != XivItemType.indoor && _root.Info.PrimaryType != XivItemType.outdoor)
            {
                // Get all the materials in this root, and add them to the selectable list.
                var tx = MainWindow.DefaultTransaction;
                var materials = await _root.GetMaterialFiles(-1, tx);
                foreach (var m in materials)
                {
                    var mName = Path.GetFileName(m);
                    mName = "/" + mName;
                    RootMaterials.Add(mName);
                }

                RootMaterials = RootMaterials.OrderBy(x => x).ToHashSet();
            }

            UpdateModelSizeWarning();
            UpdateMaterialsList();

            _view.MeshNumberBox.SelectionChanged += MeshNumberBox_SelectionChanged;
            _view.PartNumberBox.SelectionChanged += PartNumberBox_SelectionChanged;
            _view.MaterialSelectorBox.SelectionChanged += MaterialSelectorBox_SelectionChanged;
            _view.MaterialPathTextBox.KeyDown += MaterialPathTextBox_KeyDown;
            _view.MaterialPathTextBox.LostFocus += MaterialPathTextBox_LostFocus;

            _view.ShapesListBox.SelectionChanged += ShapesListBox_SelectionChanged;
            _view.AttributesListBox.SelectionChanged += AttributesListBox_SelectionChanged;

            _view.RemoveShapeButton.Click += RemoveShapeButton_Click;
            _view.RemoveAttributeButton.Click += RemoveAttributeButton_Click;

            _view.AddAttributeBox.SelectionChanged += AddAttributeBox_SelectionChanged;
            _view.AddAttributeTextBox.KeyDown += AddAttributeTextBox_KeyDown;

            _view.ScaleComboBox.SelectionChanged += ScaleComboBox_SelectionChanged;

            _view.ModelTypeComboBox.SelectionChanged += ModelTypeComboBox_SelectionChanged;

            _view.MeshNumberBox.SelectedIndex = 0;
        }


        private void ScaleComboBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            UpdateModelSizeWarning();
        }

        private void UpdateModelSizeWarning()
        {
            float size = (float)(NewModelSize * (Convert.ToDouble(_view.ScaleComboBox.SelectedValue)));
            _view.OldModelSizeBox.Content = OldModelSize.ToString("0.00") + " meters".L();
            _view.NewModelSizeBox.Content = size.ToString("0.00") + " meters".L();

            if (size < MinAcceptableSize * OldModelSize)
            {
                _view.ScaleWarningBox.Foreground = System.Windows.Media.Brushes.DarkGoldenrod;
                _view.ScaleWarningBox.FontWeight = FontWeights.Bold;
                _view.ScaleWarningBox.Text = "You may wish to consider scaling this model up.".L();

                _view.NewModelSizeBox.Foreground = System.Windows.Media.Brushes.DarkGoldenrod;
                _view.NewModelSizeBox.FontWeight = FontWeights.Bold;
            }
            else if (size > MaxAcceptableSize * OldModelSize)
            {
                _view.ScaleWarningBox.Foreground = System.Windows.Media.Brushes.DarkGoldenrod;
                _view.ScaleWarningBox.FontWeight = FontWeights.Bold;
                _view.ScaleWarningBox.Text = "You may wish to consider scaling this model down.".L();

                _view.NewModelSizeBox.Foreground = System.Windows.Media.Brushes.DarkGoldenrod;
                _view.NewModelSizeBox.FontWeight = FontWeights.Bold;

            }
            else
            {
                _view.ScaleWarningBox.Foreground = System.Windows.Media.Brushes.Black;
                _view.ScaleWarningBox.FontWeight = FontWeights.Normal;
                _view.ScaleWarningBox.Text = "";

                _view.NewModelSizeBox.Foreground = System.Windows.Media.Brushes.Black;
                _view.NewModelSizeBox.FontWeight = FontWeights.Normal;
            }
        }

        private void AttributesListBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            var selected = _view.AttributesListBox.SelectedValue;
            if(selected != null)
            {
                _view.RemoveAttributeButton.IsEnabled = true;
            } else
            {
                _view.RemoveAttributeButton.IsEnabled = false;
            }
        }

        private void ShapesListBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            var selected = _view.ShapesListBox.SelectedValue;
            if (selected != null)
            {
                _view.RemoveShapeButton.IsEnabled = true;
            }
            else
            {
                _view.RemoveShapeButton.IsEnabled = false;
            }
        }
        private void ModelTypeComboBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            var val = (EMeshType)_view.ModelTypeComboBox.SelectedValue;
            var m = GetGroup();
            m.MeshType = val;
        }

        private void RemoveAttributeButton_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            var attr = (string)_view.AttributesListBox.SelectedValue;
            var p = GetPart();

            if (attr == null || p == null) return;

            var entry = _view.AttributesSource.First(x => x.Key == attr);
            _view.AttributesSource.Remove(entry);
            p.Attributes.RemoveWhere(x => x == attr);
        }

        private void RemoveShapeButton_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            var shape = (string)_view.ShapesListBox.SelectedValue;
            var m = GetGroup();

            if (shape == null || m == null) return;

            var entry = _view.ShapesSource.First(x => x.Key == shape);
            _view.ShapesSource.Remove(entry);
            foreach (var p in m.Parts)
            {
                p.ShapeParts.Remove(shape);
            }
        }

        private void UpdateMaterialsList()
        {
            // Remove the Custom Tag at the end if it's there.
            _view.MaterialsSource.Remove(CustomTag);

            // If we don't have this already, we're in our first itteration of this function.
            if (!_view.MaterialsSource.Contains(SkinTag))
            {
                _view.MaterialsSource.Add(SkinTag);

                // Get our root materials, if we have any
                foreach(var m in RootMaterials)
                {
                    AddMaterial(m);
                }
            }

            // Add in any new item materials.
            foreach (var m in _newModel.MeshGroups)
            {
                AddMaterial(m.Material);
            }

            // Re-Add the custom tag.
            _view.MaterialsSource.Add(CustomTag);
        }

        private void AddMaterial(string m)
        {
            if (_view.MaterialsSource.Any(x => x.Key == m)) return;

            var result = ItemMaterialRegex.Match(m);
            if (result.Success
                // This check is a little janky, but it's to avoid reading cross-slot references as the same item.
                // ... Not that this should ever really happen, but it's *technically* possible.
                && (_root.Info.Slot == null || m.Contains(_root.Info.Slot)))
            {
                // We have a new item material here that we haven't added to the list yet.
                var raceId = result.Groups[1].Value;
                var partId = result.Groups[2].Value;
                var race = XivRaces.GetXivRace(raceId);
                _view.MaterialsSource.Add(new KeyValuePair<string, string>(m, "Material ".L() + partId.ToUpper() + " - " + XivRaces.GetDisplayName(race)));
            }
            else
            {
                _view.MaterialsSource.Add(new KeyValuePair<string, string>(m, Path.GetFileNameWithoutExtension(m)));
            }

        }

        private static Regex _getRaceRegex = new Regex("c([0-9]{4})");

        // Repopulates the list box showing what attributes are available to add.
        private void ResetAvailableAttributesList()
        {
            _view.AllAttributesSource.Clear();
            _view.AddAttributeTextBox.Text = "";

            _view.AllAttributesSource.Add(DefaultTag);

            // Get all standard attributes for this root.
            HashSet<string> atrList = new HashSet<string>();
            if(_root != null)
            {
                if(_root.Info.Slot != null && AttributesBySlot.ContainsKey(_root.Info.Slot))
                {
                    var slotAttributes = AttributesBySlot[_root.Info.Slot];
                    slotAttributes.ForEach(x => atrList.Add(x));
                }

                if(AttributesByType.ContainsKey(_root.Info.PrimaryType))
                {
                    AttributesByType[_root.Info.PrimaryType].ForEach(x => atrList.Add(x));
                }

                if (_root.Info.PrimaryType == XivItemType.human && (_root.Info.SecondaryType == XivItemType.hair || _root.Info.SecondaryType == XivItemType.face))
                {
                    // Hair & Face has some special attributes that are only available for Miqo'te.
                    var match = _getRaceRegex.Match(_oldModel.Source);
                    if(match.Success == true && 
                        (match.Groups[1].Value.StartsWith("08")
                        || match.Groups[1].Value.StartsWith("07")))
                    {
                        // This is a Miqote (PC/NPC) (M/F) Hair or Face model.
                        atrList.Add("atr_top");

                        if (_root.Info.SecondaryType == XivItemType.hair)
                        {
                            atrList.Add("atr_sta");
                        }
                    }

                    // Au Ra F has some special handling for faces.
                    if (match.Success == true && match.Groups[1].Value.StartsWith("14") && _root.Info.SecondaryType == XivItemType.face)
                    {
                        // This is a weird Au Ra F thing.
                        atrList.Add("atr_hair");
                    }

                }
            }

            var attributes = new List<KeyValuePair<string, string>>(_newModel.Attributes.Count);
            foreach (var a in _newModel.Attributes)
            {
                // Add any attributes in the model that we didn't already pick up.
                if (!atrList.Contains(a)) {
                    atrList.Add(a);
                }
            }

            foreach(var a in atrList)
            {
                var r = _view.AllAttributesSource.FirstOrDefault(x => x.Key == a);
                if (r.Key == null)
                {
                    attributes.Add(new KeyValuePair<string, string>(a, GetNiceAttributeName(a)));
                }
            }
            
            // Sort the attributes by their nice name before adding them.
            //attributes = attributes.OrderBy(x => x.Value).ToList();
            foreach(var kv in attributes)
            {
                _view.AllAttributesSource.Add(kv);
            }

            _view.AllAttributesSource.Add(CustomTag);

            _view.AddAttributeBox.SelectedValue = DefaultTag.Key;
        }


        /// <summary>
        /// When the user selects a new attribute from the dropdown list.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void AddAttributeBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (_view.AddAttributeBox.SelectedValue == null) return;

            var attr = (string)_view.AddAttributeBox.SelectedValue;
            if (attr == DefaultTag.Key)
            {
                _view.AddAttributeTextBox.IsEnabled = false;
                return;
            }

            if (attr == CustomTag.Key)
            {
                // Enable manual editing if they hit the custom button.
                _view.AddAttributeTextBox.IsEnabled = true;
                _view.AddAttributeTextBox.Focus();
                _view.AddAttributeTextBox.Text = "";
            } else
            {
                // Otherwise straight add the thing.
                _view.AddAttributeTextBox.IsEnabled = false;
                _view.AddAttributeTextBox.Text = "";
                var p = GetPart();
                if(!p.Attributes.Contains(attr))
                {
                    p.Attributes.Add(attr);
                    _view.AttributesSource.Add(new KeyValuePair<string, string>(attr, GetNiceAttributeName(attr)));
                }
                ResetAvailableAttributesList();
            }

        }
        private void AddAttributeTextBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            
            if (e.Key == Key.Return)
            {
                var attr = _view.AddAttributeTextBox.Text;
                attr = attr.ToLower();
                attr = attr.Trim();

                // See if we can find it in the nice names list.
                var found = NiceAttributeNames.FirstOrDefault(x => x.Value != null ? (x.Value.ToLower() == attr) : false);
                if(found.Key != null)
                {
                    attr = found.Key;
                } else
                {
                    // See if the thing they typed in is an imc attribute nice name.
                    var ImcNiceNameRegex = new Regex("(.+) (?:variant|imc) ([a-j])$");
                    var match = ImcNiceNameRegex.Match(attr.ToLower());
                    if(match.Success)
                    {
                        var niceSlotName = match.Groups[1].Value;
                        var letter = match.Groups[2].Value;
                        found = NiceImcAttributeNames.FirstOrDefault(x => x.Value != null ? (x.Value.ToLower() == niceSlotName) : false);
                        if (found.Key != null)
                        {
                            var rawSlotName = found.Key;
                            attr = "atr_" + rawSlotName + "_" + letter;
                        }
                    }
                }


                var validator = new Regex("[^a-z0-9._=]");
                attr = validator.Replace(attr, "");
                if (attr == "") return;

                _view.AddAttributeBox.Focus();

                var p = GetPart();
                if (!p.Attributes.Contains(attr))
                {
                    p.Attributes.Add(attr);
                    _view.AttributesSource.Add(new KeyValuePair<string, string>(attr, GetNiceAttributeName(attr)));
                }
                ResetAvailableAttributesList();
            }
        }


        // Mesh number changed.
        private void MeshNumberBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if(_view.MeshNumberBox.SelectedValue == null)
            {
                _view.PartNumberBox.SelectedValue = null;
                _view.ShapesSource.Clear();
                _view.PartSource.Clear();
                return;
            }

            _view.PartSource.Clear();
            var mIdx = (int)_view.MeshNumberBox.SelectedValue;
            var m = _newModel.MeshGroups[mIdx];
            for (var pIdx = 0; pIdx < m.Parts.Count; pIdx++)
            {
                var p = m.Parts[pIdx];

                string name = null;
                if (p.Name != null)
                {
                    var itemName = Path.GetFileNameWithoutExtension(_oldModel.Source);
                    name = p.Name.Replace(itemName, "");
                    name = name.Trim();
                }
                _view.PartSource.Add(new KeyValuePair<int, string>(pIdx, "#" + pIdx + ": " + (name == null ? "Unnamed" : name)));
            }


            _view.ShapesSource.Clear();
            SortedSet<string> shapeNames = new SortedSet<string>();
            foreach (var p in m.Parts)
            {
                foreach (var shpKv in p.ShapeParts)
                {
                    var shape = shpKv.Value;
                    if (!shape.Name.StartsWith("shp_")) continue;
                    shapeNames.Add(shape.Name);
                }
            }

            foreach(var shape in shapeNames)
            {
                _view.ShapesSource.Add(new KeyValuePair<string, string>(shape, GetNiceShapeName(shape)));
            }

            SetMaterial(m.Material == null ? _newModel.Materials[0] : m.Material);

            _view.ShapesListBox.SelectedItem = null;
            _view.AttributesListBox.SelectedItem = null;
            _view.ModelTypeComboBox.SelectedValue = m.MeshType;

            // Set selected part.
            if (m.Parts.Count > 0) {
                _view.PartNumberBox.SelectedValue = 0;
            } else
            {
                _view.PartNumberBox.SelectedValue = null;
            }
        }

        // Part number changed.
        private void PartNumberBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {

            ResetAvailableAttributesList();
            if (_view.MeshNumberBox.SelectedValue == null || _view.PartNumberBox.SelectedValue == null)
            {
                _view.AttributesSource.Clear();
                return;
            }

            var mIdx = (int)_view.MeshNumberBox.SelectedValue;
            var pIdx = (int)_view.PartNumberBox.SelectedValue;
            var p = _newModel.MeshGroups[mIdx].Parts[pIdx];

            _view.AttributesSource.Clear();
            foreach (var attribute in p.Attributes)
            {
                _view.AttributesSource.Add(new KeyValuePair<string, string>(attribute, GetNiceAttributeName(attribute)));
            }
        }

        // Gets the nice, human readable name for an attribute.
        private string GetNiceAttributeName(string attribute)
        {

            var niceName = NiceAttributeNames.ContainsKey(attribute) && NiceAttributeNames[attribute] != null ? NiceAttributeNames[attribute] : UnknownText;
            if (niceName == UnknownText)
            {
                var imcMatch = ImcAttributeRegex.Match(attribute);
                if (imcMatch.Success)
                {
                    var slotPrefix = imcMatch.Groups[1].Value;
                    var letter = imcMatch.Groups[2].Value;
                    var niceImcName = NiceImcAttributeNames.ContainsKey(slotPrefix) && NiceImcAttributeNames[slotPrefix] != null ? NiceImcAttributeNames[slotPrefix] : UnknownText;

                    niceName = niceImcName + " Variant ".L() + letter;
                }
            }
            var fullNiceName = niceName + " (" + attribute + ")";
            return fullNiceName;
        }

        // Gets the nice, human readable name for a shape
        private string GetNiceShapeName(string shape)
        {

            var niceName = NiceShapeNames.ContainsKey(shape) && NiceShapeNames[shape] != null ? NiceShapeNames[shape] : UnknownText;

            var fullNiceName = niceName + " (" + shape + ")";
            return fullNiceName;
        }

        // Material combo box selector changed.
        private void MaterialSelectorBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (_view.MaterialSelectorBox.SelectedValue == null) return;

            var material = (string)_view.MaterialSelectorBox.SelectedValue;
            if (material == CustomTag.Key)
            {
                // Custom material selected
                _view.MaterialPathTextBox.IsEnabled = true;
            }
            else
            {
                // We selected an actual default material selection.
                _view.MaterialPathTextBox.IsEnabled = false;
                _view.MaterialPathTextBox.Text = material;
                var mIdx = (int)_view.MeshNumberBox.SelectedValue;

                // Assign the material to the model.
                var m = _newModel.MeshGroups[mIdx];
                m.Material = material;
            }
        }

        // Sets the material selection at the UI level
        private void SetMaterial(string mat)
        {
            var element = _view.MaterialsSource.FirstOrDefault(x => x.Key == mat);
            if (element.Key != null)
            {
                _view.MaterialSelectorBox.SelectedValue = mat;
            }
            else
            {
                _view.MaterialSelectorBox.SelectedValue = CustomTag.Key;
                _view.MaterialPathTextBox.Text = mat;
            }

        }

        /// <summary>
        /// Locks in the user's typed in value.
        /// </summary>
        private void SaveCustomMaterialText()
        {
            if (((string)_view.MaterialSelectorBox.SelectedValue) != CustomTag.Key) return;


            // Assign the material to the model.
            var mIdx = (int)_view.MeshNumberBox.SelectedValue;
            var m = _newModel.MeshGroups[mIdx];
            var mat = _view.MaterialPathTextBox.Text;
            m.Material = mat;

            // We might have added a new part material, so check that.
            UpdateMaterialsList();

            // And make sure our selection is set correctly.
            SetMaterial(mat);
        }

        private void MaterialPathTextBox_LostFocus(object sender, System.Windows.RoutedEventArgs e)
        {
            SaveCustomMaterialText();
        }

        private void MaterialPathTextBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {

            if (e.Key == Key.Return)
            {
                SaveCustomMaterialText();
            }
        }


        /// <summary>
        /// The list of nice, human readable attribute names, keyed by their underlying FFXIV name.
        /// </summary>
        /// <returns></returns>
        private static readonly Dictionary<string, string> NiceAttributeNames = new Dictionary<string, string>()
        {

            { "atr_kam", "Scalp".L() },
            { "atr_hig", "Facial Hair".L() },
            { "atr_mim", "Ear".L() },
            { "atr_hrn", "Horn".L() },
            { "atr_kao", "Face".L() },

            // Used in weapons
            { "atr_arrow", "Arrow" },
            { "atr_ar1", "Quiver Arrow 1".L() },
            { "atr_ar2", "Quiver Arrow 2".L() },
            { "atr_ar3", "Quiver Arrow 3".L() },
            { "atr_attach", "Gauss Barrel".L() },

            // Used in Body gear
            { "atr_hij", "Wrist".L() },
            { "atr_ude", "Elbow".L() },
            { "atr_nek", "Neck".L() },

            // Used in Leg gear
            { "atr_kod", "Waist".L() },
            { "atr_sne", "Shin".L() },
            { "atr_hiz", "Knee".L() },

            // Used in headgear
            { "atr_inr", "Gorget".L() },    // Neck only for head items.
            
            { "atr_lod", "Excess Detail".L() },

            // Used in Glove items
            { "atr_arm", "Glove".L() },

            // Used in Foot items
            { "atr_lpd", "Knee Pad".L() },
            { "atr_leg", "Boot".L() },

            // Misc
            { "atr_tlh", "Non-Tail Races Only".L() },
            { "atr_tls", "Tail Races Only".L() },
            { "atr_eye_a", "Reaper Transformation".L()},

            // Unknown/unverified
            
            { "atr_blt", "Belt".L() },
            { "atr_top", "Miqo'te Ears".L() },
            { "atr_sta", "Miqo'te Long Hair Unique".L() },
            { "atr_hair","Au Ra F Face Unique".L() },

            // IMC Attributes are handled below in GetNiceAttributeName()
        };

        private static readonly Dictionary<string, string> NiceImcAttributeNames = new Dictionary<string, string>()
        {
            { "bv", "Other".L() },
            { "dv", XivStrings.Legs },
            { "mv", XivStrings.Head },
            { "gv", XivStrings.Hands},
            { "sv", XivStrings.Feet },
            { "tv", XivStrings.Body },
            { "fv", XivStrings.Face },
            { "hv", XivStrings.Hair },
            { "ev", XivStrings.Earring },
            { "nv", XivStrings.Neck },
            { "wv", XivStrings.Wrists },
            { "rv", XivStrings.Rings },
        };



        private static readonly Dictionary<XivItemType, List<string>> AttributesByType = new Dictionary<XivItemType, List<string>>()
        {
            { XivItemType.weapon, new List<string> () {
                "atr_arrow",
                "atr_ar1",
                "atr_ar2",
                "atr_ar3",
                "atr_attach",
                "atr_bv_a",
                "atr_bv_b",
                "atr_bv_c",
            } },
            { XivItemType.monster, new List<string> () {
                "atr_bv_a",
                "atr_bv_b",
                "atr_bv_c",
            } },
            { XivItemType.demihuman, new List<string> () {
                "atr_bv_a",
                "atr_bv_b",
                "atr_bv_c",
            } }
        };

        private static readonly Dictionary<string, List<string>> AttributesBySlot = new Dictionary<string, List<string>>()
        {
            { "met", new List<string> () {
                "atr_inr",
                "atr_mv_a",
                "atr_mv_b",
                "atr_mv_c",
            } },
            { "top", new List<string> () {
                "atr_hij",
                "atr_nek",
                "atr_ude",
                "atr_tlh",
                "atr_tls",
                "atr_tv_a",
                "atr_tv_b",
                "atr_tv_c",
            } },
            { "glv", new List<string> () {
                "atr_arm",
                "atr_gv_a",
                "atr_gv_b",
                "atr_gv_c",
            } },
            { "dwn", new List<string> () {
                "atr_hiz",
                "atr_kod",
                "atr_sne",
                "atr_dv_a",
                "atr_dv_b",
                "atr_dv_c",
            } },
            { "sho", new List<string> () {
                "atr_leg",
                "atr_spd",
                "atr_sv_a",
                "atr_sv_b",
                "atr_sv_c",
            } },
            { "ear", new List<string> () {
                "atr_ev_a",
                "atr_ev_b",
                "atr_ev_c",
            } },
            { "nek", new List<string> () {
                "atr_nv_a",
                "atr_nv_b",
                "atr_nv_c",
            } },
            { "wrs", new List<string> () {
                "atr_wv_a",
                "atr_wv_b",
                "atr_wv_c",
            } },
            { "rir", new List<string> () {
                "atr_rv_a",
                "atr_rv_b",
                "atr_rv_c",
            } },
            { "ril", new List<string> () {
                "atr_rv_a",
                "atr_rv_b",
                "atr_rv_c",
            } },
            { "fac", new List<string> () {
                "atr_hig",
                "atr_hrn",
                "atr_kao",
                "atr_mim",
                "atr_fv_a",
                "atr_fv_b",
                "atr_fv_c",
                "atr_eye_a",
            } },
            { "hir", new List<string> () {
                "atr_kam",
                "atr_hv_a",
                "atr_hv_b",
                "atr_hv_c",
            } },
        };

        /// <summary>
        /// The list of nice, human readable attribute names, keyed by their underlying FFXIV name.
        /// </summary>
        /// <returns></returns>
        private static readonly Dictionary<string, string> NiceShapeNames = new Dictionary<string, string>()
        {
            // This is mostly a copy of the atr list, but sometimes the names change,
            // so it's listed as a separate dictionary.
            { "shp_kam", "Scalp" },
            { "shp_hig", "Facial Hair" },
            { "shp_mim", "Ear" },
            { "shp_hrn", "Horn" },
            { "shp_kao", "Face" },


            // Used in weapons
            { "shp_arrow", "Arrow" },
            { "shp_ar0", "Quiver Arrow" },
            { "shp_attach", "Gauss Barrel" },

            // Used in Body gear
            { "shp_hij", "Wrist" },
            { "shp_ude", "Elbow" },
            { "shp_nek", "Neck" },
            { "shp_blt", "Belt" },

            // Used in Leg gear
            { "shp_kod", "Waist" },
            { "shp_sne", "Shin" },
            { "shp_hiz", "Knee" },

            // Used in headgear
            { "shp_inr", "Gorget" },    // Neck only for head items.
            
            { "shp_lod", "Excess Detail" },

            // Used in Glove items
            { "shp_arm", "Glove" },

            // Used in Foot items
            { "shp_lpd", "Knee Pad" },
            { "shp_leg", "Boot" },

            // Misc
            { "shp_tlh", "Non-Tail Races Only" },
            { "shp_tls", "Tail Races Only" },

            // Unknown/unverified
            { "shp_top", "Miqo'te Ears" },
            { "shp_sta", "Miqo'te Hair Unique" },
            { "shp_hair", "Au Ra F Face Unique" },

            // Face stuff.
            // Was it the smartest idea to do these by hand?  Probably not.
            // But it works, and it's done.
            { "shp_brw_a", "Brow a" },
            { "shp_brw_b", "Brow b" },
            { "shp_brw_c", "Brow c" },
            { "shp_brw_d", "Brow d" },
            { "shp_brw_e", "Brow e" },

            { "shp_chk_a", "Cheek a" },
            { "shp_chk_b", "Cheek b" },
            { "shp_chk_c", "Cheek c" },
            { "shp_chk_d", "Cheek d" },
            { "shp_chk_e", "Cheek e" },

            { "shp_etc_a", "Etc a" },
            { "shp_etc_b", "Etc b" },
            { "shp_etc_c", "Etc c" },
            { "shp_etc_d", "Etc d" },
            { "shp_etc_e", "Etc e" },

            { "shp_irs_a", "Iris a" },

            { "shp_mth_a", "Mouth a" },
            { "shp_mth_b", "Mouth b" },
            { "shp_mth_c", "Mouth c" },
            { "shp_mth_d", "Mouth d" },
            { "shp_mth_e", "Mouth e" },

            { "shp_eye_a", "Eye a" },
            { "shp_eye_b", "Eye b" },
            { "shp_eye_c", "Eye c" },
            { "shp_eye_d", "Eye d" },
            { "shp_eye_e", "Eye e" },

            { "shp_nse_a", "Nose a" },
            { "shp_nse_b", "Nose b" },
            { "shp_nse_c", "Nose c" },
            { "shp_nse_d", "Nose d" },
            { "shp_nse_e", "Nose e" },
        };
    }
}
