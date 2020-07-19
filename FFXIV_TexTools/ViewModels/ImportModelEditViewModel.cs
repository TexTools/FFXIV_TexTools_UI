using FFXIV_TexTools.Helpers;
using FFXIV_TexTools.Properties;
using FFXIV_TexTools.Resources;
using FFXIV_TexTools.Views.Models;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Input;
using xivModdingFramework.General.Enums;
using xivModdingFramework.Helpers;
using xivModdingFramework.Items;
using xivModdingFramework.Items.Interfaces;
using xivModdingFramework.Models.DataContainers;
using xivModdingFramework.Models.FileTypes;
using xivModdingFramework.Models.Helpers;

namespace FFXIV_TexTools.ViewModels
{
    public class ImportModelEditViewModel
    {
        /// <summary>
        /// The list of nice, human readable attribute names, keyed by their underlying FFXIV name.
        /// </summary>
        /// <returns></returns>
        private Dictionary<string, string> NiceAttributeNames = new Dictionary<string, string>()
        {

            { "atr_hair","Hair" },
            { "atr_kam", "Scalp" },
            { "atr_hig", "Facial Hair" },
            { "atr_mim", "Ear" },
            { "atr_hrn", "Horn" },
            { "atr_kao", "Face" },

            // Used in weapons
            { "atr_arrow", "Arrow" },
            { "atr_ar0", "Quiver Arrow" },
            { "atr_attach", "Gauss Barrel" },

            // Used in Body gear
            { "atr_hij", "Wrist" },
            { "atr_ude", "Elbow" },
            { "atr_nek", "Neck" },

            // Used in Leg gear
            { "atr_kod", "Waist" },
            { "atr_sne", "Shin" },
            { "atr_hiz", "Knee" },

            // Used in headgear
            { "atr_inr", "Gorget" },    // Neck only for head items.
            
            { "atr_lod", "Excess Detail" },

            // Used in Glove items
            { "atr_arm", "Glove" },

            // Used in Foot items
            { "atr_lpd", "Knee Pad" },
            { "atr_leg", "Boot" },

            // Misc
            { "atr_tlh", "Non-Tail Races Only" },
            { "atr_tls", "Tail Races Only" },

            // Unknown/unverified
            { "atr_top", "Body" },
            { "atr_sta", null },

            // IMC Attributes are handled below in GetNiceAttributeName()
        };

        private Dictionary<string, string> NiceImcAttributeNames = new Dictionary<string, string>()
        {
            { "bv", "Weapon" },
            { "dv", XivStrings.Legs },
            { "mv", XivStrings.Head },
            { "gv", XivStrings.Hands},
            { "sv", XivStrings.Feet },
            { "tv", XivStrings.Body },
            { "fv", XivStrings.Face },
            { "hv", XivStrings.Hair },
            { "nv", null },
        };

        /// <summary>
        /// The list of nice, human readable shape names, keyed by their underlying FFXIV name.
        /// </summary>
        /// <returns></returns>
        private Dictionary<string, string> NiceShapeNames = new Dictionary<string, string>()
        {

        };


        private ImportModelEditView _view;
        private TTModel _model;

        private readonly Regex ImcAttributeRegex = new Regex("^atr_([a-z]{2})_([a-j])$");

        private readonly Regex DefaultSkinRegex = new Regex("\\/mt_c[0-9]{4}b0001_a\\.mtrl");
        private readonly Regex ItemMaterialRegex = new Regex("\\/mt_c([0-9]{4})e[0-9]{4}_[a-z0-9]{3}_([a-z])\\.mtrl");
        private const string SkinMaterial = "/mt_c0101b0001_a.mtrl";
        private readonly KeyValuePair<string, string> DefaultTag = new KeyValuePair<string, string>("_!ADDNEW!_", "Add Parts...");
        private readonly KeyValuePair<string, string> CustomTag = new KeyValuePair<string, string>("_!CUSTOM!_", "Custom");
        private readonly KeyValuePair<string, string> SkinTag = new KeyValuePair<string, string>(SkinMaterial, "Skin");
        private readonly string UnknownText = "Unknown";

        private TTMeshGroup GetGroup()
        {
            if (_view.MeshNumberBox.SelectedValue == null)
                return null;

            var mIdx = (int)_view.MeshNumberBox.SelectedValue;
            return _model.MeshGroups[mIdx];

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

        public ImportModelEditViewModel(ImportModelEditView view, TTModel model)
        {
            _view = view;
            _model = model;


            // Merge all the default skin materials together, since FFXIV auto-handles them anyways.
            foreach(var m in _model.MeshGroups)
            {
                if(m.Material == null)
                {
                    // Sanity assurance.
                    m.Material = model.MeshGroups[0].Material;
                }


                var result = DefaultSkinRegex.Match(m.Material);
                if (result.Success)
                {
                    m.Material = SkinMaterial;
                }
            }

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

            _view.MeshNumberBox.SelectedIndex = 0;
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
            m.ShapeParts.RemoveAll(x => x.Name == shape);
        }

        private void UpdateMaterialsList()
        {
            // Remove the Custom Tag at the end if it's there.
            _view.MaterialsSource.Remove(CustomTag);

            // If we don't have this already, we're in our first itteration of this function.
            if (!_view.MaterialsSource.Contains(SkinTag))
            {
                _view.MaterialsSource.Add(SkinTag);
            }

            // Add in any new item materials.
            foreach (var m in _model.MeshGroups)
            {
                var result = ItemMaterialRegex.Match(m.Material);
                if (result.Success)
                {
                    if (_view.MaterialsSource.Any(x => x.Key == m.Material)) continue;

                    // We have a new item material here that we haven't added to the list yet.
                    var raceId = result.Groups[1].Value;
                    var partId = result.Groups[2].Value;
                    var race = XivRaces.GetXivRace(raceId);
                    _view.MaterialsSource.Add(new KeyValuePair<string, string>(m.Material, "Material " + partId.ToUpper() + " - " + XivRaces.GetDisplayName(race)));

                }
            }

            // Re-Add the custom tag.
            _view.MaterialsSource.Add(CustomTag);
        }

        // Repopulates the list box showing what attributes are available to add.
        private void ResetAvailableAttributesList()
        {
            _view.AllAttributesSource.Clear();
            _view.AddAttributeTextBox.Text = "";

            _view.AllAttributesSource.Add(DefaultTag);

            var attributes = new List<KeyValuePair<string, string>>(_model.Attributes.Count);
            foreach (var a in _model.Attributes)
            {
                var r = _view.AllAttributesSource.FirstOrDefault(x => x.Key == a);
                if (r.Key == null)
                {
                    attributes.Add(new KeyValuePair<string, string>(a, GetNiceAttributeName(a)));
                }
            }
            
            // Sort the attributes by their nice name before adding them.
            attributes = attributes.OrderBy(x => x.Value).ToList();
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
                    var ImcNiceNameRegex = new Regex("imc (.+) ([a-j])$");
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


                var validator = new Regex("[^a-z_]");
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
                // Todo - Clear all the fields.
                _view.PartNumberBox.SelectedValue = null;
                _view.ShapesSource.Clear();
                _view.PartSource.Clear();
                return;
            }

            _view.PartSource.Clear();
            var mIdx = (int)_view.MeshNumberBox.SelectedValue;
            var m = _model.MeshGroups[mIdx];
            for (var pIdx = 0; pIdx < m.Parts.Count; pIdx++)
            {
                var p = m.Parts[pIdx];
                _view.PartSource.Add(new KeyValuePair<int, string>(pIdx, "#" + pIdx + ": " + (p.Name == null ? "Unnamed" : p.Name)));
            }


            _view.ShapesSource.Clear();
            foreach (var shape in m.ShapeParts)
            {
                _view.ShapesSource.Add(new KeyValuePair<string, string>(shape.Name, shape.Name));
            }

            SetMaterial(m.Material == null ? _model.Materials[0] : m.Material);

            _view.ShapesListBox.SelectedItem = null;
            _view.AttributesListBox.SelectedItem = null;

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
            var p = _model.MeshGroups[mIdx].Parts[pIdx];

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

                    niceName = "IMC " + niceImcName + " " + letter;
                }
            }
            var fullNiceName = niceName + " (" + attribute + ")";
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
                var m = _model.MeshGroups[mIdx];
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
            var m = _model.MeshGroups[mIdx];
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


    }
}
