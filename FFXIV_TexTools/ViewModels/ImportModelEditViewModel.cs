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
        private ImportModelEditView _view;
        private TTModel _model;

        private readonly Regex DefaultSkinRegex = new Regex("\\/mt_c[0-9]{4}b0001_a\\.mtrl");
        private readonly Regex ItemMaterialRegex = new Regex("\\/mt_c([0-9]{4})e[0-9]{4}_[a-z0-9]{3}_([a-z])\\.mtrl");
        private const string SkinMaterial = "/mt_c0101b0001_a.mtrl";
        private readonly KeyValuePair<string, string> DefaultTag = new KeyValuePair<string, string>("_!ADDNEW!_", "Add Parts...");
        private readonly KeyValuePair<string, string> CustomTag = new KeyValuePair<string, string>("_!CUSTOM!_", "Custom");
        private readonly KeyValuePair<string, string> SkinTag = new KeyValuePair<string, string>(SkinMaterial, "Skin");

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

        private void ResetAttributesList()
        {
            _view.AllAttributesSource.Clear();
            _view.AddAttributeTextBox.Text = "";

            _view.AllAttributesSource.Add(DefaultTag);
            var m = GetGroup();
            if (m == null) return;

            foreach (var p in m.Parts)
            {
                foreach (var a in p.Attributes) {
                    var r = _view.AllAttributesSource.FirstOrDefault(x => x.Key == a);
                    if (r.Key == null)
                    {
                        _view.AllAttributesSource.Add(new KeyValuePair<string, string>(a, a));
                    }
                }
            }
            _view.AllAttributesSource.Add(CustomTag);

            _view.AddAttributeBox.SelectedValue = DefaultTag.Key;
        }
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
                _view.AddAttributeTextBox.IsEnabled = true;

                _view.AddAttributeTextBox.Focus();
                _view.AddAttributeTextBox.Text = "";
            } else
            {
                _view.AddAttributeTextBox.IsEnabled = false;
                _view.AddAttributeTextBox.Text = "";
                var p = GetPart();
                if(!p.Attributes.Contains(attr))
                {
                    p.Attributes.Add(attr);
                    _view.AttributesSource.Add(new KeyValuePair<string, string>(attr, attr));
                }
                ResetAttributesList();
            }

        }
        private void AddAttributeTextBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            
            if (e.Key == Key.Return)
            {
                var attr = _view.AddAttributeTextBox.Text;
                attr.ToLower();
                attr.Trim();
                var validator = new Regex("[^a-z_]");
                attr = validator.Replace(attr, "");
                if (attr == "") return;

                _view.AddAttributeBox.Focus();

                var p = GetPart();
                if (!p.Attributes.Contains(attr))
                {
                    p.Attributes.Add(attr);
                    _view.AttributesSource.Add(new KeyValuePair<string, string>(attr, attr));
                }
                ResetAttributesList();
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

            ResetAttributesList();
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
                _view.AttributesSource.Add(new KeyValuePair<string, string>(attribute, attribute));
            }
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
