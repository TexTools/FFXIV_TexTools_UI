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

            // Unknown 0
            AddText(textBox, "Unknown 0:\t\t".L(), _textColor, false);
            AddText(textBox, $"{modelData.Unknown0.ToString()}\n\n", _textColor, true);

            // Mesh Count
            AddText(textBox, "Mesh Count:\t\t".L(), _textColor, false);
            AddText(textBox, $"{modelData.MeshCount.ToString()}\n\n", _textColor, true);

            // Attribute Count
            AddText(textBox, "Attribute Count:\t".L(), _textColor, false);
            AddText(textBox, $"{modelData.AttributeCount.ToString()}\n\n", _textColor, true);

            // Mesh Part Count
            AddText(textBox, "Mesh Part Count:\t".L(), _textColor, false);
            AddText(textBox, $"{modelData.MeshPartCount.ToString()}\n\n", _textColor, true);

            // Material Count
            AddText(textBox, "Material Count:\t".L(), _textColor, false);
            AddText(textBox, $"{modelData.MaterialCount.ToString()}\n\n", _textColor, true);

            // Bone Count
            AddText(textBox, "Bone Count:\t\t".L(), _textColor, false);
            AddText(textBox, $"{modelData.BoneCount.ToString()}\n\n", _textColor, true);

            // Bone List Count
            AddText(textBox, "Bone List Count:\t".L(), _textColor, false);
            AddText(textBox, $"{modelData.BoneListCount.ToString()}\n\n", _textColor, true);

            // Mesh Shape Info Count
            AddText(textBox, "Shape Info Count:\t".L(), _textColor, false);
            AddText(textBox, $"{modelData.ShapeCount.ToString()}\n\n", _textColor, true);

            // Mesh Shape Data Count
            //AddText(textBox, "Shape Data Count:\t", _textColor, false);
            //AddText(textBox, $"{modelData.ShapePartCount.ToString()}\n\n", _textColor, true);

            // Unknown 1
            AddText(textBox, "Unknown 1:\t\t".L(), _textColor, false);
            AddText(textBox, $"{modelData.Unknown1.ToString()}\n\n", _textColor, true);

            // Unknown 2
            AddText(textBox, "Unknown 2:\t\t".L(), _textColor, false);
            AddText(textBox, $"{modelData.Unknown2.ToString()}\n\n", _textColor, true);

            // Unknown 3
            AddText(textBox, "Unknown 3:\t\t".L(), _textColor, false);
            AddText(textBox, $"{modelData.Unknown3.ToString()}\n\n", _textColor, true);

            // Unknown 4
            AddText(textBox, "Unknown 4:\t\t".L(), _textColor, false);
            AddText(textBox, $"{modelData.Unknown4.ToString()}\n\n", _textColor, true);

            // Unknown 5
            AddText(textBox, "Unknown 5:\t\t".L(), _textColor, false);
            AddText(textBox, $"{modelData.Unknown5.ToString()}\n\n", _textColor, true);

            // Unknown 6
            AddText(textBox, "Unknown 6:\t\t".L(), _textColor, false);
            AddText(textBox, $"{modelData.Unknown6.ToString()}\n\n", _textColor, true);

            // Unknown 7
            AddText(textBox, "Unknown 7:\t\t".L(), _textColor, false);
            AddText(textBox, $"{modelData.Unknown7.ToString()}\n\n", _textColor, true);

            // Unknown 8
            AddText(textBox, "Unknown 8:\t\t".L(), _textColor, false);
            AddText(textBox, $"{modelData.Unknown8.ToString()}\n\n", _textColor, true);

            // Unknown 9
            AddText(textBox, "Unknown 9:\t\t".L(), _textColor, false);
            AddText(textBox, $"{modelData.Unknown9.ToString()}\n\n", _textColor, true);

            // Unknown 10a
            AddText(textBox, "Unknown 10a:\t\t".L(), _textColor, false);
            AddText(textBox, $"{modelData.Unknown10a.ToString()}\n\n", _textColor, true);

            // Unknown 10b
            AddText(textBox, "Unknown 10b:\t\t".L(), _textColor, false);
            AddText(textBox, $"{modelData.Unknown10b.ToString()}\n\n", _textColor, true);

            // Unknown 11
            AddText(textBox, "Unknown 11:\t\t".L(), _textColor, false);
            AddText(textBox, $"{modelData.Unknown11.ToString()}\n\n", _textColor, true);

            // Unknown 12
            AddText(textBox, "Unknown 12:\t\t".L(), _textColor, false);
            AddText(textBox, $"{modelData.Unknown12.ToString()}\n\n", _textColor, true);

            // Unknown 13
            AddText(textBox, "Unknown 13:\t\t".L(), _textColor, false);
            AddText(textBox, $"{modelData.Unknown13.ToString()}\n\n", _textColor, true);

            // Unknown 14
            AddText(textBox, "Unknown 14:\t\t".L(), _textColor, false);
            AddText(textBox, $"{modelData.Unknown14.ToString()}\n\n", _textColor, true);

            // Unknown 15
            AddText(textBox, "Unknown 15:\t\t".L(), _textColor, false);
            AddText(textBox, $"{modelData.Unknown15.ToString()}\n\n", _textColor, true);

            // Unknown 16
            AddText(textBox, "Unknown 16:\t\t".L(), _textColor, false);
            AddText(textBox, $"{modelData.Unknown16.ToString()}\n\n", _textColor, true);

            // Unknown 17
            AddText(textBox, "Unknown 17:\t\t".L(), _textColor, false);
            AddText(textBox, $"{modelData.Unknown17.ToString()}\n\n", _textColor, true);

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

            if (_xivMdl.BoundBox != null)
            {
                otherList.Add("Bounding Box".L());
            }

            if (_xivMdl.BoneTransformDataList.Count > 0)
            {
                otherList.Add("Transforms".L());
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

            var selectedItem = (string) OtherDataComboBox.SelectedItem;

            var textBox = OtherDataRichTextBox;

            if (selectedItem.Equals("Unknown".L()))
            {
                if (_xivMdl.UnkData0?.Unknown != null)
                {
                    AddText(textBox, "Unknown 0 Size:\t".L(), _textColor, false);
                    AddText(textBox, $"{_xivMdl.UnkData0.Unknown.Length}\n\n", _textColor, true);
                }

                if (_xivMdl.UnkData1?.Unknown != null)
                {
                    AddText(textBox, "Unknown 1 Size:\t".L(), _textColor, false);
                    AddText(textBox, $"{_xivMdl.UnkData1.Unknown.Length}\n\n", _textColor, true);
                }

                if (_xivMdl.UnkData2?.Unknown != null)
                {
                    AddText(textBox, "Unknown 2 Size:\t".L(), _textColor, false);
                    AddText(textBox, $"{_xivMdl.UnkData2.Unknown.Length}\n\n", _textColor, true);
                }
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
                var boundBox = _xivMdl.BoundBox;

                AddText(textBox, "Vector Count:\t".L(), _textColor, false);
                AddText(textBox, $"{boundBox.PointList.Count}\n\n", _textColor, true);

                for (var i = 0; i < boundBox.PointList.Count; i++)
                {
                    AddText(textBox, $"{i}:\t", _textColor, false);
                    AddText(textBox, $"{boundBox.PointList[i]}\n\n", _textColor, true);
                }
            }

            if (selectedItem.Equals("Transforms".L()))
            {
                var transforms = _xivMdl.BoneTransformDataList;

                AddText(textBox, "Transform Count:\t".L(), _textColor, false);
                AddText(textBox, $"{transforms.Count}\n\n", _textColor, true);

                for (var i = 0; i < transforms.Count; i++)
                {
                    AddText(textBox, $"{i} T0:\t", _textColor, false);
                    AddText(textBox, $"{transforms[i].Transform0}\n", _textColor, true);
                    AddText(textBox, $"{i} T1:\t", _textColor, false);
                    AddText(textBox, $"{transforms[i].Transform1}\n\n", _textColor, true);
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

            // Mesh Offset
            AddText(textBox, "Mesh Offset:\t\t".L(), _textColor, false);
            AddText(textBox, $"{lod.MeshOffset}\n\n", _textColor, true);

            // Mesh Count
            AddText(textBox, "Mesh Count:\t\t".L(), _textColor, false);
            AddText(textBox, $"{lod.MeshCount}\n\n", _textColor, true);

            // Unknown 0
            AddText(textBox, "Unknown 0:\t\t".L(), _textColor, false);
            AddText(textBox, $"{lod.Unknown0}\n\n", _textColor, true);

            // Unknown 1
            AddText(textBox, "Unknown 1:\t\t".L(), _textColor, false);
            AddText(textBox, $"{lod.Unknown1}\n\n", _textColor, true);

            // Mesh End
            AddText(textBox, "Mesh End:\t\t".L(), _textColor, false);
            AddText(textBox, $"{lod.MeshEnd}\n\n", _textColor, true);

            // Extra Mesh Count
            AddText(textBox, "Extra Mesh Count:\t".L(), _textColor, false);
            AddText(textBox, $"{lod.ExtraMeshCount}\n\n", _textColor, true);

            // Mesh Sum
            AddText(textBox, "Mesh Sum:\t\t".L(), _textColor, false);
            AddText(textBox, $"{lod.MeshSum}\n\n", _textColor, true);

            // Unknown 2
            AddText(textBox, "Unknown 2:\t\t".L(), _textColor, false);
            AddText(textBox, $"{lod.Unknown2}\n\n", _textColor, true);

            // Unknown 3
            AddText(textBox, "Unknown 3:\t\t".L(), _textColor, false);
            AddText(textBox, $"{lod.Unknown3}\n\n", _textColor, true);

            // Unknown 4
            AddText(textBox, "Unknown 4:\t\t".L(), _textColor, false);
            AddText(textBox, $"{lod.MeshGroupTotal}\n\n", _textColor, true);

            // Unknown 5
            AddText(textBox, "Unknown 5:\t\t".L(), _textColor, false);
            AddText(textBox, $"{lod.Unknown5}\n\n", _textColor, true);

            // Index Start
            AddText(textBox, "Index Start:\t\t".L(), _textColor, false);
            AddText(textBox, $"{lod.IndexDataStart}\n\n", _textColor, true);

            // Unknown 6
            AddText(textBox, "Unknown 6:\t\t".L(), _textColor, false);
            AddText(textBox, $"{lod.Unknown6}\n\n", _textColor, true);

            // Unknown 7
            AddText(textBox, "Unknown 7:\t\t".L(), _textColor, false);
            AddText(textBox, $"{lod.Unknown7}\n\n", _textColor, true);

            // Vertex Size
            AddText(textBox, "Vertex Size:\t\t".L(), _textColor, false);
            AddText(textBox, $"{lod.VertexDataSize}\n\n".L(), _textColor, true);

            // Index Size
            AddText(textBox, "Index Size:\t\t".L(), _textColor, false);
            AddText(textBox, $"{lod.IndexDataSize}\n\n", _textColor, true);

            // Vertex Offset
            AddText(textBox, "Vertex Offset:\t\t".L(), _textColor, false);
            AddText(textBox, $"{lod.VertexDataOffset}\n\n", _textColor, true);

            // Index Offset
            AddText(textBox, "Index Offset:\t\t".L(), _textColor, false);
            AddText(textBox, $"{lod.IndexDataOffset}\n\n", _textColor, true);
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
