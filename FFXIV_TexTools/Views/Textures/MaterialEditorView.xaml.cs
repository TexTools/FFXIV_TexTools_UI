using FFXIV_TexTools.ViewModels;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using xivModdingFramework.Items.Interfaces;
using xivModdingFramework.Materials.DataContainers;

namespace FFXIV_TexTools.Views.Textures
{
    /// <summary>
    /// Interaction logic for MaterialEditor.xaml
    /// </summary>
    public partial class MaterialEditorView
    {
        private MaterialEditorViewModel viewModel;
        private XivMtrl _material;
        public XivMtrl Material
        {
            get
            {
                return _material;
            }
        }

        public MaterialEditorView()
        {
            InitializeComponent();
            viewModel = new MaterialEditorViewModel(this);

            // Setup for the combo boxes.
            Dictionary<MtrlShader, string> ShaderSource = new Dictionary<MtrlShader, string>();
            ShaderSource.Add(MtrlShader.Standard, "Standard");
            ShaderSource.Add(MtrlShader.Glass, "Glass");
            ShaderSource.Add(MtrlShader.Skin, "Skin");
            ShaderSource.Add(MtrlShader.Hair, "Hair");
            ShaderSource.Add(MtrlShader.Iris, "Iris");
            ShaderSource.Add(MtrlShader.Furniture, "Furniture");
            ShaderSource.Add(MtrlShader.Other, "Other");
            ShaderComboBox.ItemsSource = ShaderSource;
            ShaderComboBox.DisplayMemberPath = "Value";
            ShaderComboBox.SelectedValuePath = "Key";

            Dictionary<MtrlTextureDescriptorFormat, string> NormalSource = new Dictionary<MtrlTextureDescriptorFormat, string>();
            NormalSource.Add(MtrlTextureDescriptorFormat.WithAlpha, "With Alpha");
            NormalSource.Add(MtrlTextureDescriptorFormat.WithoutAlpha, "Without Alpha");
            NormalComboBox.ItemsSource = NormalSource;
            NormalComboBox.DisplayMemberPath = "Value";
            NormalComboBox.SelectedValuePath = "Key";

            Dictionary<MaterialSpecularMode, string> SpecularSource = new Dictionary<MaterialSpecularMode, string>();
            SpecularSource.Add(MaterialSpecularMode.FullColor, "Full Color Specular");
            SpecularSource.Add(MaterialSpecularMode.MultiMap, "Multi Map");
            SpecularSource.Add(MaterialSpecularMode.None, "None");
            SpecularComboBox.ItemsSource = SpecularSource;
            SpecularComboBox.DisplayMemberPath = "Value";
            SpecularComboBox.SelectedValuePath = "Key";

            Dictionary<MaterialDiffuseMode, string> DiffuseSource = new Dictionary<MaterialDiffuseMode, string>();
            DiffuseSource.Add(MaterialDiffuseMode.FullColor, "Full Color Diffuse");
            DiffuseSource.Add(MaterialDiffuseMode.None, "None");
            DiffuseComboBox.ItemsSource = DiffuseSource;
            DiffuseComboBox.DisplayMemberPath = "Value";
            DiffuseComboBox.SelectedValuePath = "Key";

            Dictionary<bool, string> TransparencySource = new Dictionary<bool, string>();
            TransparencySource.Add(true, "Enabled");
            TransparencySource.Add(false, "Disabled");
            TransparencyComboBox.ItemsSource = TransparencySource;
            TransparencyComboBox.DisplayMemberPath = "Value";
            TransparencyComboBox.SelectedValuePath = "Key";

            SaveButton.Click += SaveButton_Click;
        }

        public void SetMaterial(XivMtrl material, IItemModel item, bool writeFile = true)
        {
            _material = Material;
            viewModel.SetMaterial(material, item, writeFile);

        }

        public XivMtrl GetMaterial()
        {
            return viewModel.GetMaterial();
        }

        private async void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            _material = await viewModel.SaveChanges();
            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void DiffuseComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if((MaterialDiffuseMode) DiffuseComboBox.SelectedValue == MaterialDiffuseMode.None)
            {
                DiffuseTextBox.IsEnabled = false;
            } else
            {
                DiffuseTextBox.IsEnabled = true;
            }
        }

        private void SpecularComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if ((MaterialSpecularMode)DiffuseComboBox.SelectedValue == MaterialSpecularMode.None)
            {
                SpecularTextBox.IsEnabled = false;
            }
            else
            {
                SpecularTextBox.IsEnabled = true;
            }

        }

        private void ShaderComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if((MtrlShader) ShaderComboBox.SelectedValue == MtrlShader.Glass)
            {
                TransparencyComboBox.SelectedValue = true;
                TransparencyComboBox.IsEnabled = false;
            } else if ((MtrlShader)ShaderComboBox.SelectedValue == MtrlShader.Furniture)
            {
                // Disable everything for furniture shader items.
                // Haven't done enough research here yet to be able to 
                // sanely allow them to be edited this way.

                // Furniture items also completely lack usage structs, so 
                // how their actual maps are connected to the shader is currently
                // totally unknown.
                ShaderComboBox.IsEnabled = false;
                TransparencyComboBox.IsEnabled = true;
                NormalComboBox.IsEnabled = false;
                NormalTextBox.IsEnabled = false;
                SpecularComboBox.IsEnabled = false;
                SpecularTextBox.IsEnabled = false;
                DiffuseComboBox.IsEnabled = false;
                DiffuseTextBox.IsEnabled = false;

            } else
            {
                ShaderComboBox.IsEnabled = true;
                TransparencyComboBox.IsEnabled = true;
                NormalComboBox.IsEnabled = true;
                NormalTextBox.IsEnabled = true;
                SpecularComboBox.IsEnabled = true;
                SpecularTextBox.IsEnabled = true;
                DiffuseComboBox.IsEnabled = true;
                DiffuseTextBox.IsEnabled = true;
            }

        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            var help = new Views.Textures.MaterialEditorHelpView();
            help.ShowDialog();
        }
    }
}
