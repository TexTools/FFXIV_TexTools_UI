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

        public ModelInspector(XivMdl xivMdl)
        {
            InitializeComponent();

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
            AddText(textBox, "Unknown 0:\t\t", "Black", false);
            AddText(textBox, $"{modelData.Unknown0.ToString()}\n\n", "Black", true);

            // Mesh Count
            AddText(textBox, "Mesh Count:\t\t", "Black", false);
            AddText(textBox, $"{modelData.MeshCount.ToString()}\n\n", "Black", true);

            // Attribute Count
            AddText(textBox, "Attribute Count:\t", "Black", false);
            AddText(textBox, $"{modelData.AttributeCount.ToString()}\n\n", "Black", true);

            // Mesh Part Count
            AddText(textBox, "Mesh Part Count:\t", "Black", false);
            AddText(textBox, $"{modelData.MeshPartCount.ToString()}\n\n", "Black", true);

            // Material Count
            AddText(textBox, "Material Count:\t", "Black", false);
            AddText(textBox, $"{modelData.MaterialCount.ToString()}\n\n", "Black", true);

            // Bone Count
            AddText(textBox, "Bone Count:\t\t", "Black", false);
            AddText(textBox, $"{modelData.BoneCount.ToString()}\n\n", "Black", true);

            // Bone List Count
            AddText(textBox, "Bone List Count:\t", "Black", false);
            AddText(textBox, $"{modelData.BoneListCount.ToString()}\n\n", "Black", true);

            // Mesh Shape Info Count
            AddText(textBox, "Shape Info Count:\t", "Black", false);
            AddText(textBox, $"{modelData.ShapeCount.ToString()}\n\n", "Black", true);

            // Mesh Shape Data Count
            AddText(textBox, "Shape Data Count:\t", "Black", false);
            AddText(textBox, $"{modelData.ShapeDataCount.ToString()}\n\n", "Black", true);

            // Unknown 1
            AddText(textBox, "Unknown 1:\t\t", "Black", false);
            AddText(textBox, $"{modelData.Unknown1.ToString()}\n\n", "Black", true);

            // Unknown 2
            AddText(textBox, "Unknown 2:\t\t", "Black", false);
            AddText(textBox, $"{modelData.Unknown2.ToString()}\n\n", "Black", true);

            // Unknown 3
            AddText(textBox, "Unknown 3:\t\t", "Black", false);
            AddText(textBox, $"{modelData.Unknown3.ToString()}\n\n", "Black", true);

            // Unknown 4
            AddText(textBox, "Unknown 4:\t\t", "Black", false);
            AddText(textBox, $"{modelData.Unknown4.ToString()}\n\n", "Black", true);

            // Unknown 5
            AddText(textBox, "Unknown 5:\t\t", "Black", false);
            AddText(textBox, $"{modelData.Unknown5.ToString()}\n\n", "Black", true);

            // Unknown 6
            AddText(textBox, "Unknown 6:\t\t", "Black", false);
            AddText(textBox, $"{modelData.Unknown6.ToString()}\n\n", "Black", true);

            // Unknown 7
            AddText(textBox, "Unknown 7:\t\t", "Black", false);
            AddText(textBox, $"{modelData.Unknown7.ToString()}\n\n", "Black", true);

            // Unknown 8
            AddText(textBox, "Unknown 8:\t\t", "Black", false);
            AddText(textBox, $"{modelData.Unknown8.ToString()}\n\n", "Black", true);

            // Unknown 9
            AddText(textBox, "Unknown 9:\t\t", "Black", false);
            AddText(textBox, $"{modelData.Unknown9.ToString()}\n\n", "Black", true);

            // Unknown 10a
            AddText(textBox, "Unknown 10a:\t\t", "Black", false);
            AddText(textBox, $"{modelData.Unknown10a.ToString()}\n\n", "Black", true);

            // Unknown 10b
            AddText(textBox, "Unknown 10b:\t\t", "Black", false);
            AddText(textBox, $"{modelData.Unknown10b.ToString()}\n\n", "Black", true);

            // Unknown 11
            AddText(textBox, "Unknown 11:\t\t", "Black", false);
            AddText(textBox, $"{modelData.Unknown11.ToString()}\n\n", "Black", true);

            // Unknown 12
            AddText(textBox, "Unknown 12:\t\t", "Black", false);
            AddText(textBox, $"{modelData.Unknown12.ToString()}\n\n", "Black", true);

            // Unknown 13
            AddText(textBox, "Unknown 13:\t\t", "Black", false);
            AddText(textBox, $"{modelData.Unknown13.ToString()}\n\n", "Black", true);

            // Unknown 14
            AddText(textBox, "Unknown 14:\t\t", "Black", false);
            AddText(textBox, $"{modelData.Unknown14.ToString()}\n\n", "Black", true);

            // Unknown 15
            AddText(textBox, "Unknown 15:\t\t", "Black", false);
            AddText(textBox, $"{modelData.Unknown15.ToString()}\n\n", "Black", true);

            // Unknown 16
            AddText(textBox, "Unknown 16:\t\t", "Black", false);
            AddText(textBox, $"{modelData.Unknown16.ToString()}\n\n", "Black", true);

            // Unknown 17
            AddText(textBox, "Unknown 17:\t\t", "Black", false);
            AddText(textBox, $"{modelData.Unknown17.ToString()}\n\n", "Black", true);

            // Extra LoD Count
            if (_xivMdl.ExtraLoDList != null)
            {
                AddText(textBox, "Extra LoD Count:\t", "Black", false);
                AddText(textBox, $"{_xivMdl.ExtraLoDList.Count}\n\n", "Blue", true);
            }

            // Extra Mesh Count
            if (_xivMdl.ExtraMeshData != null)
            {
                AddText(textBox, "Extra Mesh Count:\t", "Black", false);
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
                _pathData.Add("Attributes", paths.AttributeList);
            }

            if (paths.BoneList.Count > 0)
            {
                _pathData.Add("Bones", paths.BoneList);
            }

            if (paths.MaterialList.Count > 0)
            {
                _pathData.Add("Materials", paths.MaterialList);
            }

            if (paths.ShapeList.Count > 0)
            {
                _pathData.Add("Shapes", paths.ShapeList);
            }

            if (paths.ExtraPathList.Count > 0)
            {
                _pathData.Add("Extras", paths.ExtraPathList);
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

            otherList.Add("Unknown");
            otherList.Add("Data Blocks");

            for (var i = 0; i < _xivMdl.BoneIndexMeshList.Count; i++)
            {
                otherList.Add($"Bone Index (Mesh) {i}");
            }

            if (_xivMdl.BoneIndexPart != null && _xivMdl.BoneIndexPart.BoneIndexCount > 0)
            {
                otherList.Add($"Bone Index (Part)");
            }

            if (_xivMdl.BoundBox != null)
            {
                otherList.Add("Bounding Box");
            }

            if (_xivMdl.BoneTransformDataList.Count > 0)
            {
                otherList.Add("Transforms");
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
                AddText(textBox, $"{path}", "Black", false);
                AddText(textBox, "\n", "Black", false);
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

            if (selectedItem.Equals("Unknown"))
            {
                if (_xivMdl.UnkData0?.Unknown != null)
                {
                    AddText(textBox, "Unknown 0 Size:\t", "Black", false);
                    AddText(textBox, $"{_xivMdl.UnkData0.Unknown.Length}\n\n", "Black", true);
                }

                if (_xivMdl.UnkData1?.Unknown != null)
                {
                    AddText(textBox, "Unknown 1 Size:\t", "Black", false);
                    AddText(textBox, $"{_xivMdl.UnkData1.Unknown.Length}\n\n", "Black", true);
                }

                if (_xivMdl.UnkData2?.Unknown != null)
                {
                    AddText(textBox, "Unknown 2 Size:\t", "Black", false);
                    AddText(textBox, $"{_xivMdl.UnkData2.Unknown.Length}\n\n", "Black", true);
                }
            }

            if (selectedItem.Equals("Data Blocks"))
            {
                if (_xivMdl.AttrDataBlock?.AttributePathOffsetList != null)
                {
                    AddText(textBox, "Attribute Offset Count:\t", "Black", false);
                    AddText(textBox, $"{_xivMdl.AttrDataBlock.AttributePathOffsetList.Count}\n\n", "Black", true);
                }

                if (_xivMdl.MatDataBlock?.MaterialPathOffsetList != null)
                {
                    AddText(textBox, "Material Offset Count:\t", "Black", false);
                    AddText(textBox, $"{_xivMdl.MatDataBlock.MaterialPathOffsetList.Count}\n\n", "Black", true);
                }

                if (_xivMdl.BonDataBlock?.BonePathOffsetList != null)
                {
                    AddText(textBox, "Bone Offset Count:\t", "Black", false);
                    AddText(textBox, $"{_xivMdl.BonDataBlock.BonePathOffsetList.Count}\n\n", "Black", true);
                }
            }

            if (selectedItem.Contains("Bone Index (Mesh)"))
            {
                var num = int.Parse(selectedItem.Substring(selectedItem.Length - 1));

                var boneIndex = _xivMdl.BoneIndexMeshList[num];

                AddText(textBox, "Index Count:\t", "Black", false);
                AddText(textBox, $"{boneIndex.BoneIndexCount}\n\n", "Black", true);


                for (var i = 0; i < boneIndex.BoneIndexCount; i++)
                {
                    AddText(textBox, $"{i}:\t", "Black", false);
                    AddText(textBox, $"{boneIndex.BoneIndices[i]}\n\n", "Black", true);
                }
            }

            if (selectedItem.Equals("Bone Index (Part)"))
            {
                var boneIndex = _xivMdl.BoneIndexPart;

                AddText(textBox, "Index Count:\t", "Black", false);
                AddText(textBox, $"{boneIndex.BoneIndexCount / 2}\n\n", "Black", true);

                for (var i = 0; i < boneIndex.BoneIndexList.Count; i++)
                {
                    AddText(textBox, $"{i}:\t", "Black", false);
                    AddText(textBox, $"{boneIndex.BoneIndexList[i]}\n\n", "Black", true);
                }
            }

            if (selectedItem.Equals("Bounding Box"))
            {
                var boundBox = _xivMdl.BoundBox;

                AddText(textBox, "Vector Count:\t", "Black", false);
                AddText(textBox, $"{boundBox.PointList.Count}\n\n", "Black", true);

                for (var i = 0; i < boundBox.PointList.Count; i++)
                {
                    AddText(textBox, $"{i}:\t", "Black", false);
                    AddText(textBox, $"{boundBox.PointList[i]}\n\n", "Black", true);
                }
            }

            if (selectedItem.Equals("Transforms"))
            {
                var transforms = _xivMdl.BoneTransformDataList;

                AddText(textBox, "Transform Count:\t", "Black", false);
                AddText(textBox, $"{transforms.Count}\n\n", "Black", true);

                for (var i = 0; i < transforms.Count; i++)
                {
                    AddText(textBox, $"{i} T0:\t", "Black", false);
                    AddText(textBox, $"{transforms[i].Transform0}\n", "Black", true);
                    AddText(textBox, $"{i} T1:\t", "Black", false);
                    AddText(textBox, $"{transforms[i].Transform1}\n\n", "Black", true);
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
            AddText(textBox, "Mesh Offset:\t\t", "Black", false);
            AddText(textBox, $"{lod.MeshOffset}\n\n", "Black", true);

            // Mesh Count
            AddText(textBox, "Mesh Count:\t\t", "Black", false);
            AddText(textBox, $"{lod.MeshCount}\n\n", "Black", true);

            // Unknown 0
            AddText(textBox, "Unknown 0:\t\t", "Black", false);
            AddText(textBox, $"{lod.Unknown0}\n\n", "Black", true);

            // Unknown 1
            AddText(textBox, "Unknown 1:\t\t", "Black", false);
            AddText(textBox, $"{lod.Unknown1}\n\n", "Black", true);

            // Unknown 2
            AddText(textBox, "Unknown 2:\t\t", "Black", false);
            AddText(textBox, $"{lod.Unknown2}\n\n", "Black", true);

            // Unknown 3
            AddText(textBox, "Unknown 3:\t\t", "Black", false);
            AddText(textBox, $"{lod.Unknown3}\n\n", "Black", true);

            // Unknown 4
            AddText(textBox, "Unknown 4:\t\t", "Black", false);
            AddText(textBox, $"{lod.Unknown4}\n\n", "Black", true);

            // Unknown 5
            AddText(textBox, "Unknown 5:\t\t", "Black", false);
            AddText(textBox, $"{lod.Unknown5}\n\n", "Black", true);

            // Unknown 6
            AddText(textBox, "Unknown 6:\t\t", "Black", false);
            AddText(textBox, $"{lod.Unknown6}\n\n", "Black", true);

            // Index Start
            AddText(textBox, "Index Start:\t\t", "Black", false);
            AddText(textBox, $"{lod.IndexDataStart}\n\n", "Black", true);

            // Unknown 7
            AddText(textBox, "Unknown 7:\t\t", "Black", false);
            AddText(textBox, $"{lod.Unknown7}\n\n", "Black", true);

            // Unknown 8
            AddText(textBox, "Unknown 8:\t\t", "Black", false);
            AddText(textBox, $"{lod.Unknown8}\n\n", "Black", true);

            // Vertex Size
            AddText(textBox, "Vertex Size:\t\t", "Black", false);
            AddText(textBox, $"{lod.VertexDataSize}\n\n", "Black", true);

            // Index Size
            AddText(textBox, "Index Size:\t\t", "Black", false);
            AddText(textBox, $"{lod.IndexDataSize}\n\n", "Black", true);

            // Vertex Offset
            AddText(textBox, "Vertex Offset:\t\t", "Black", false);
            AddText(textBox, $"{lod.VertexDataOffset}\n\n", "Black", true);

            // Index Offset
            AddText(textBox, "Index Offset:\t\t", "Black", false);
            AddText(textBox, $"{lod.IndexDataOffset}\n\n", "Black", true);
        }

        /// <summary>
        /// Event handler for mesh inspector button clicked
        /// </summary>
        private void MeshInspectorButton_Click(object sender, RoutedEventArgs e)
        {
            var selectedLoD = (int)LoDComboBox.SelectedItem;

            var meshInspector = new MeshInspectorView(_xivMdl.LoDList[selectedLoD].MeshDataList, selectedLoD);
            meshInspector.Owner = this;
            meshInspector.Show();
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
