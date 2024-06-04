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
    /// Interaction logic for MeshInspectorView.xaml
    /// </summary>
    public partial class MeshInspectorView
    {
        private List<MeshData> _meshDataList;
        private string _textColor = "Black";

        public MeshInspectorView(List<MeshData> meshDataList, int LodNum)
        {
            InitializeComponent();

            var appStyle = ThemeManager.DetectAppStyle(Application.Current);
            if (appStyle.Item1.Name.Equals("BaseDark"))
            {
                _textColor = "White";
            }

            Title = $"{FFXIV_TexTools.Resources.UIStrings.Mesh_Inspector} (LoD {LodNum})";

            _meshDataList = meshDataList;

            FillMeshNumComboBox();
        }

        /// <summary>
        /// Fills the combobox with the numbers of available meshes
        /// </summary>
        private void FillMeshNumComboBox()
        {
            var meshNumList = new List<int>();

            for (var i = 0; i < _meshDataList.Count; i++)
            {
                meshNumList.Add(i);
            }

            MeshNumComboBox.ItemsSource = meshNumList;

            MeshNumComboBox.SelectedIndex = 0;
        }

        private void PrintVar<T>(RichTextBox textBox, T source, string name, bool doubleSpace = true)
        {
            object value;
            var property = typeof(T).GetProperty(name);

            if (property != null)
            {
                value = property.GetValue(source);
            }
            else
            {
                var field = typeof(T).GetField(name);
                if (field == null)
                {
                    return;
                }
                else
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

            foreach (var p in props)
            {
                if (p.PropertyType.IsValueType)
                {
                    PrintVar(textBox, source, p.Name, doubleSpace);
                }
            }
        }

        /// <summary>
        /// Event handler for mesh number changing
        /// </summary>
        private void MeshNumComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            MeshMetaDataRichTextBox.Document.Blocks.Clear();

            var selectedMeshNum = (int)MeshNumComboBox.SelectedItem;

            var mesh = _meshDataList[selectedMeshNum].MeshInfo;

            var textBox = MeshMetaDataRichTextBox;
            PrintAllProps(textBox, mesh);

            FillMeshPartComboBox();
            FillVertexStructComboBox();
        }

        /// <summary>
        /// Fills the combobox with available mesh parts
        /// </summary>
        private void FillMeshPartComboBox()
        {
            var selectedMeshNum = (int)MeshNumComboBox.SelectedItem;

            var mesh = _meshDataList[selectedMeshNum].MeshInfo;

            var meshPartNums = new List<int>();

            for (var i = 0; i < mesh.MeshPartCount; i++)
            {
                meshPartNums.Add(i);
            }

            PartNumComboBox.ItemsSource = meshPartNums;

            PartNumComboBox.SelectedIndex = 0;
        }

        /// <summary>
        /// Event handler for mesh part number changing
        /// </summary>
        private void PartNumComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            MeshPartsRichTextBox.Document.Blocks.Clear();

            var selectedMeshNum = (int)MeshNumComboBox.SelectedItem;
            var selectedPartNum = (int?) PartNumComboBox.SelectedItem ?? 0;

            var meshPart = _meshDataList[selectedMeshNum].MeshPartList[selectedPartNum];

            var textBox = MeshPartsRichTextBox;

            PrintAllProps(textBox, meshPart);
        }

        /// <summary>
        /// Fills the combo box with available vertex structures
        /// </summary>
        private void FillVertexStructComboBox()
        {
            var selectedMeshNum = (int)MeshNumComboBox.SelectedItem;

            var mesh = _meshDataList[selectedMeshNum];

            var vertexStructList = mesh.VertexDataStructList;

            var vertexStructNumList = new List<int>();

            for (var i = 0; i < vertexStructList.Count; i++)
            {
                vertexStructNumList.Add(i);
            }

            StructNumComboBox.ItemsSource = vertexStructNumList;

            StructNumComboBox.SelectedIndex = 0;
        }

        /// <summary>
        /// Event handler for vertex structure changing
        /// </summary>
        private void StructNumComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            DataStructRichTextBox.Document.Blocks.Clear();

            var selectedStructNum = (int) StructNumComboBox.SelectedItem;

            var selectedMeshNum = (int)MeshNumComboBox.SelectedItem;

            var mesh = _meshDataList[selectedMeshNum];

            var vertexStruct = mesh.VertexDataStructList[selectedStructNum];

            var textBox = DataStructRichTextBox;

            PrintAllProps(textBox, vertexStruct);
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
        /// Event handler for the window closing
        /// </summary>
        private void MetroWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            Owner.Activate();
        }
    }
}
