// Copyright © 2019 Rafael Gonzalez - All Rights Reserved
// 
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
// 
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
// 
// You should have received a copy of the GNU General Public License
// along with this program.  If not, see <http://www.gnu.org/licenses/>.

using MahApps.Metro;
using SharpDX.Direct2D1.Effects;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using xivModdingFramework.Models.DataContainers;

namespace FFXIV_TexTools.Views.Models
{
    /// <summary>
    /// Interaction logic for ModelInspector.xaml
    /// </summary>
    public partial class ModelInspector
    {
        private Dictionary<string, List<string>> _pathData;
        private XivMdl _xivMdl;
        private string _textColor = "Black";

        public ModelInspector(XivMdl xivMdl)
        {
            InitializeComponent();

            var appStyle = ThemeManager.DetectAppStyle(Application.Current);
            if (appStyle.Item1.Name.Equals("BaseDark"))
            {
                _textColor = "White";
            }

            _xivMdl = xivMdl;

            FillModelMetaData();

            FillPathComboBox();

            FillOtherDataComboBox();

            FillLoDComboBox();
            Closing += ModelInspector_Closing;
        }

        private void ModelInspector_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (null != Owner)
            {
                Owner.Activate();
            }
        }

        private void PrintVar<T>(RichTextBox textBox, T source, string name, bool doubleSpace = true)
        {
            object value;
            var property = typeof(T).GetProperty(name);

            if (property != null)
            {
                value = property.GetValue(source);
            } else
            {
                var field = typeof(T).GetField(name);
                if(field == null)
                {
                    return;
                } else
                {
                    value = field.GetValue(source);
                }

            }

            var spaces = doubleSpace ? "\n\n" : "\n";
            AddText(textBox, name + " :\t\t".L(), _textColor, false);
            AddText(textBox, $"{value.ToString()}" + spaces, _textColor, true);
        }
        private void PrintAllProps<T>(RichTextBox textBox, T source, bool doubleSpace = true)
        {
            var dataType = typeof(T);
            var props = dataType.GetProperties();
            
            foreach(var p in props)
            {
                if(p.PropertyType.IsValueType)
                {
                    PrintVar(textBox, source, p.Name, doubleSpace);
                }
            }
        }

        /// <summary>
        /// Fills the combobox with the available model metadata
        /// </summary>
        private void FillModelMetaData()
        {
            var textBox = ModelMetaDataRichTextBox;

            var modelData = _xivMdl.ModelData;

            // Unknown 0
            AddText(textBox, "Mdl Version:\t\t".L(), _textColor, false);
            AddText(textBox, $"{_xivMdl.MdlVersion.ToString()}\n\n", _textColor, true);

            PrintAllProps(textBox, modelData);

            // Extra LoD Count
            if (_xivMdl.ExtraLoDList != null)
            {
                AddText(textBox, "Extra LoD Count:\t".L(), _textColor, false);
                AddText(textBox, $"{_xivMdl.ExtraLoDList.Count}\n\n", "Blue", true);
            }

            // Extra Mesh Count
            if (_xivMdl.ExtraMeshData != null)
            {
                AddText(textBox, "Extra Mesh Count:\t".L(), _textColor, false);
                AddText(textBox, $"{_xivMdl.ExtraMeshData.Count}\n\n", "Blue", true);
            }
        }

        /// <summary>
        /// Fills the combobox with available paths 
        /// </summary>
        private void FillPathComboBox()
        {
            var paths = _xivMdl.PathData;

            _pathData = new Dictionary<string, List<string>>();

            if (paths.AttributeList.Count > 0)
            {
                _pathData.Add("Attributes".L(), paths.AttributeList);
            }

            if (paths.BoneList.Count > 0)
            {
                _pathData.Add("Bones".L(), paths.BoneList);
            }

            if (paths.MaterialList.Count > 0)
            {
                _pathData.Add("Materials".L(), paths.MaterialList);
            }

            if (paths.ShapeList.Count > 0)
            {
                _pathData.Add("Shapes".L(), paths.ShapeList);
            }

            if (paths.ExtraPathList.Count > 0)
            {
                _pathData.Add("Extras".L(), paths.ExtraPathList);
            }

            PathComboBox.ItemsSource = _pathData;

            PathComboBox.SelectedIndex = 0;
        }

        /// <summary>
        /// Fills the combobox with other available data
        /// </summary>
        private void FillOtherDataComboBox()
        {
            var otherList = new List<string>();

            otherList.Add("Vertex Structure".L());
            otherList.Add("Unknown".L());
            otherList.Add("Data Blocks".L());

            for (var i = 0; i < _xivMdl.MeshBoneSets.Count; i++)
            {
                otherList.Add($"Bone Index (Mesh) {i._()}".L());
            }

            if (_xivMdl.PartBoneSets != null && _xivMdl.PartBoneSets.BoneIndexCount > 0)
            {
                otherList.Add($"Bone Index (Part)".L());
            }

            if (_xivMdl.BoundingBoxes != null && _xivMdl.BoneBoundingBoxes.Count > 0)
            {
                otherList.Add("Bounding Box".L());
            }

            if (_xivMdl.NeckMorphTable != null && _xivMdl.NeckMorphTable.Count > 0)
            {
                otherList.Add("Neck Morph".L());
            }

            OtherDataComboBox.ItemsSource = otherList;

            OtherDataComboBox.SelectedIndex = 0;
        }

        /// <summary>
        /// Fills the combo box with LoD numbers 0-2
        /// </summary>
        private void FillLoDComboBox()
        {
            var LoDList = new List<int> {0, 1, 2};

            LoDComboBox.ItemsSource = LoDList;

            LoDComboBox.SelectedIndex = 0;
        }

        /// <summary>
        /// Event handler for path type changing
        /// </summary>
        private void PathComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            PathsRichTextBox.Document.Blocks.Clear();

            var selectedItem = (KeyValuePair<string, List<string>>)PathComboBox.SelectedItem;

            var textBox = PathsRichTextBox;

            foreach (var path in selectedItem.Value)
            {
                AddText(textBox, $"{path}", _textColor, false);
                AddText(textBox, "\n", _textColor, false);
            }
        }

        /// <summary>
        /// Event handler for other data type changing
        /// </summary>
        private void OtherDataComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            OtherDataRichTextBox.Document.Blocks.Clear();

            var selectedItem = (string)OtherDataComboBox.SelectedItem;

            var textBox = OtherDataRichTextBox;

            if (selectedItem.Equals("Unknown".L()))
            {
                if (_xivMdl.UnkData0?.Unknown != null)
                {
                    AddText(textBox, "Unknown 0 Size:\t".L(), _textColor, false);
                    AddText(textBox, $"{_xivMdl.UnkData0.Unknown.Length}\n\n", _textColor, true);
                }

                if (_xivMdl.UnkData1?.TerrainShadowMeshHeader != null)
                {
                    AddText(textBox, "Unknown 1 Size:\t".L(), _textColor, false);
                    AddText(textBox, $"{_xivMdl.UnkData1.TerrainShadowMeshHeader.Length}\n\n", _textColor, true);
                }

                if (_xivMdl.UnkData2?.Unknown != null)
                {
                    AddText(textBox, "Unknown 2 Size:\t".L(), _textColor, false);
                    AddText(textBox, $"{_xivMdl.UnkData2.Unknown.Length}\n\n", _textColor, true);
                }

                /*if (_xivMdl.UnkDataPatch72?.Unknown != null)
                {
                    AddText(textBox, "Unknown Patch72 Size:\t".L(), _textColor, false);
                    AddText(textBox, $"{_xivMdl.UnkDataPatch72.Unknown.Length}\n\n", _textColor, true);
                }*/
            }

            if (selectedItem.Equals("Data Blocks".L()))
            {
                if (_xivMdl.AttrDataBlock?.AttributePathOffsetList != null)
                {
                    AddText(textBox, "Attribute Offset Count:\t".L(), _textColor, false);
                    AddText(textBox, $"{_xivMdl.AttrDataBlock.AttributePathOffsetList.Count}\n\n", _textColor, true);
                }

                if (_xivMdl.MatDataBlock?.MaterialPathOffsetList != null)
                {
                    AddText(textBox, "Material Offset Count:\t".L(), _textColor, false);
                    AddText(textBox, $"{_xivMdl.MatDataBlock.MaterialPathOffsetList.Count}\n\n", _textColor, true);
                }

                if (_xivMdl.BoneDataBlock?.BonePathOffsetList != null)
                {
                    AddText(textBox, "Bone Offset Count:\t".L(), _textColor, false);
                    AddText(textBox, $"{_xivMdl.BoneDataBlock.BonePathOffsetList.Count}\n\n", _textColor, true);
                }
            }

            if (selectedItem.Equals("Vertex Structure".L()))
            {
                for (int li = 0; li < _xivMdl.LoDList.Count; li++)
                {
                    AddText(textBox, "==== LoD Level " + li.ToString() + " ====\n\n", _textColor, true);
                    for (int mi = 0; mi < _xivMdl.LoDList[li].MeshDataList.Count; mi++)
                    {
                        AddText(textBox, "  == Mesh #" + mi.ToString() + " ==\n\n", _textColor, true);
                        foreach (var st in _xivMdl.LoDList[li].MeshDataList[mi].VertexDataStructList)
                        {
                            PrintAllProps(textBox, st, false);
                            AddText(textBox, "\n", _textColor, true);
                        }
                    }
                }
            }

            if (selectedItem.Contains("Bone Index (Mesh)".L()))
            {
                var num = int.Parse(selectedItem.Substring(selectedItem.Length - 1));

                var boneIndex = _xivMdl.MeshBoneSets[num];

                AddText(textBox, "Index Count:\t".L(), _textColor, false);
                AddText(textBox, $"{boneIndex.BoneIndexCount}\n\n", _textColor, true);


                for (var i = 0; i < boneIndex.BoneIndexCount; i++)
                {
                    AddText(textBox, $"{i}:\t", _textColor, false);
                    AddText(textBox, $"{boneIndex.BoneIndices[i]}\n\n", _textColor, true);
                }
            }

            if (selectedItem.Equals("Bone Index (Part)".L()))
            {
                var boneIndex = _xivMdl.PartBoneSets;

                AddText(textBox, "Index Count:\t".L(), _textColor, false);
                AddText(textBox, $"{boneIndex.BoneIndexCount / 2}\n\n", _textColor, true);

                for (var i = 0; i < boneIndex.BoneIndices.Count; i++)
                {
                    AddText(textBox, $"{i}:\t", _textColor, false);
                    AddText(textBox, $"{boneIndex.BoneIndices[i]}\n\n", _textColor, true);
                }
            }


            if (selectedItem.Equals("Bounding Box".L()))
            {
                var bbId = 0;
                foreach(var l in _xivMdl.BoundingBoxes)
                {
                    AddText(textBox, $"\n\nMain BB {bbId}: \n", _textColor, false);
                    bbId++;
                    foreach (var bb in l)
                    {
                        AddText(textBox, $"{bb[0]}, {bb[1]}, {bb[2]},{bb[3]}\n", _textColor, false);
                    }
                }

                bbId = 0;
                foreach (var l in _xivMdl.BoneBoundingBoxes)
                {
                    var bName = _xivMdl.PathData.BoneList[bbId];
                    AddText(textBox, $"\n\nBone BB {bName}: \n", _textColor, false);
                    bbId++;

                    foreach (var bb in l)
                    {
                        AddText(textBox, $"{bb[0]}, {bb[1]}, {bb[2]}, {bb[3]}\n", _textColor, false);
                    }
                }
            }

            if (selectedItem.Equals("Neck Morph".L()))
            {
                var nmId = 0;
                foreach (var l in _xivMdl.NeckMorphTable)
                {
                    AddText(textBox, $"==== Morph Vertex #{nmId} ====\n\n", _textColor, false);
                    nmId++;
                    AddText(textBox, $"Pos: \t{l.PositionAdjust}\n", _textColor, false);
                    AddText(textBox, $"Norm: \t{l.NormalAdjust}\n", _textColor, false);
                    AddText(textBox, $"Bones: \t", _textColor, false);
                    foreach (var bone in l.Bones)
                    {
                        var bName = _xivMdl.PathData.BoneList[bone];
                        AddText(textBox, $"{bName} ", _textColor, false);
                    }

                    AddText(textBox, "\n\n", _textColor, false);
                }
            }
        }

        /// <summary>
        /// Event handler for LoD changing
        /// </summary>
        private void LoDComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            LodRichTextBox.Document.Blocks.Clear();

            var textBox = LodRichTextBox;

            var selectedLoD = (int) LoDComboBox.SelectedItem;

            var lod = _xivMdl.LoDList[selectedLoD];
            PrintAllProps(textBox, lod);
        }

        /// <summary>
        /// Event handler for mesh inspector button clicked
        /// </summary>
        private void MeshInspectorButton_Click(object sender, RoutedEventArgs e)
        {
            var selectedLoD = (int)LoDComboBox.SelectedItem;

            var meshInspector = new MeshInspectorView(_xivMdl.LoDList[selectedLoD].MeshDataList, selectedLoD);
            meshInspector.Owner = this;
            meshInspector.ShowDialog();
        }

        /// <summary>
        /// Adds text to the text box
        /// </summary>
        /// <param name="text">The text to add</param>
        /// <param name="color">The color of the text</param>
        private void AddText(RichTextBox richTextBox, string text, string color, bool bold)
        {
            var bc = new BrushConverter();
            var tr = new TextRange(richTextBox.Document.ContentEnd, richTextBox.Document.ContentEnd) { Text = text };
            try
            {
                tr.ApplyPropertyValue(TextElement.ForegroundProperty, bc.ConvertFromString(color));

                if (bold)
                {
                    tr.ApplyPropertyValue(TextElement.FontWeightProperty, FontWeights.Bold);
                }
            }
            catch (FormatException) { }
        }

        /// <summary>
        /// Event Handler for window closing
        /// </summary>
        private void MetroWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            Owner.Activate();
        }
    }
}
