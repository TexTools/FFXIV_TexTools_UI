using FFXIV_TexTools.ViewModels;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using xivModdingFramework.General.Enums;
using xivModdingFramework.Items.Interfaces;
using xivModdingFramework.Materials.DataContainers;
using xivModdingFramework.Mods;

namespace FFXIV_TexTools.Views.Textures
{
    public enum MaterialEditorMode
    {
        EditSingle,
        EditMulti,
        NewSingle,
        NewRace,
        NewMulti
    }
    /// <summary>
    /// Interaction logic for MaterialEditor.xaml
    /// </summary>
    public partial class MaterialEditorView
    {
        private MaterialEditorViewModel viewModel;
        private XivMtrl _material;
        private IItemModel _item;
        private MaterialEditorMode _mode;

        public ObservableCollection<KeyValuePair<MtrlShader, string>> ShaderSource;
        public ObservableCollection<KeyValuePair<MtrlShaderPreset, string>> PresetSource;
        private static XivMtrl _copiedMaterial;
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
            ShaderSource = new ObservableCollection<KeyValuePair<MtrlShader, string>>();
            ShaderSource.Add(new KeyValuePair<MtrlShader, string>(MtrlShader.Standard, "Standard"));
            ShaderSource.Add(new KeyValuePair<MtrlShader, string>(MtrlShader.Glass, "Glass"));
            ShaderSource.Add(new KeyValuePair<MtrlShader, string>(MtrlShader.Skin, "Skin"));
            ShaderSource.Add(new KeyValuePair<MtrlShader, string>(MtrlShader.Hair, "Hair"));
            ShaderSource.Add(new KeyValuePair<MtrlShader, string>(MtrlShader.Iris, "Iris"));
            ShaderSource.Add(new KeyValuePair<MtrlShader, string>(MtrlShader.Furniture, "Furniture"));
            ShaderSource.Add(new KeyValuePair<MtrlShader, string>(MtrlShader.DyeableFurniture, "Dyeable Furniture"));
            ShaderComboBox.ItemsSource = ShaderSource;
            ShaderComboBox.DisplayMemberPath = "Value";
            ShaderComboBox.SelectedValuePath = "Key";


            PresetSource = new ObservableCollection<KeyValuePair<MtrlShaderPreset, string>>();
            PresetSource.Add(new KeyValuePair<MtrlShaderPreset, string>(MtrlShaderPreset.Default, "Default"));
            PresetComboBox.ItemsSource = PresetSource;
            PresetComboBox.DisplayMemberPath = "Value";
            PresetComboBox.SelectedValuePath = "Key";

            PasteMaterialButton.IsEnabled = _copiedMaterial != null;


            DisableButton.IsEnabled = false;
            DisableButton.Visibility = Visibility.Hidden;

            SaveButton.Click += SaveButton_Click;
        }

        public async Task<bool> SetMaterial(XivMtrl material, IItemModel item, MaterialEditorMode mode = MaterialEditorMode.EditSingle)
        {
            _material = material;
            _item = item;
            _mode = mode;

            return await viewModel.SetMaterial(material, item, mode);
        }

        public XivMtrl GetMaterial()
        {
            return viewModel.GetMaterial();
        }

        public void Close(bool result)
        {
            DialogResult = result;
            Close();

        }
        private async void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            _material = await viewModel.SaveChanges();
            Close(true);
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            Close(false);
        }


        private void ShaderComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ShaderComboBox.SelectedValue == null)
            {
                return;
            }

            var shader = (MtrlShader)ShaderComboBox.SelectedValue;
            var presets = ShaderInfo.GetAvailablePresets(shader);

            PresetSource.Clear();
            foreach (var p in presets)
            {
                PresetSource.Add(new KeyValuePair<MtrlShaderPreset, string>(p, p.ToString()));
            }

            // Disable the box if the user has no choice anyways.
            if(PresetSource.Count > 1)
            {
                PresetComboBox.IsEnabled = true;
            } else
            {
                PresetComboBox.IsEnabled = false;
            }


            PresetComboBox.SelectedValue = MtrlShaderPreset.Default;
            // Ensure the UI is updated for the new selection.
            PresetComboBox_SelectionChanged(null, null);


            if(shader == MtrlShader.Other || shader == MtrlShader.Furniture || shader == MtrlShader.DyeableFurniture)
            {
                // Disable everything.
                NormalTextBox.IsEnabled = false;
                DiffuseTextBox.IsEnabled = false;
                SpecularTextBox.IsEnabled = false;
                NewSharedButton.IsEnabled = false;
                NewUniqueButton.IsEnabled = false;
                PresetComboBox.IsEnabled = false;
                ShaderComboBox.IsEnabled = false;
                SaveButton.IsEnabled = false;
                SaveButton.Visibility = Visibility.Hidden;
            }
        }

        private void PresetComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if(ShaderComboBox.SelectedValue == null || PresetComboBox.SelectedValue == null)
            {
                return;
            }

            var shader = (MtrlShader)ShaderComboBox.SelectedValue;
            var preset = (MtrlShaderPreset)PresetComboBox.SelectedValue;

            // Generate a fresh shader info so we can access some of the calculated fields.
            var info = new ShaderInfo() { Shader = shader, Preset = preset };

            if (info.HasMulti)
            {
                SpecularLabel.Content = "Multi:";
                SpecularTextBox.IsEnabled = true;
                SpecularLabel.Visibility = Visibility.Visible;
                SpecularTextBox.Visibility = Visibility.Visible;
            } else if(info.HasSpec)
            {
                SpecularLabel.Content = "Specular:";
                SpecularTextBox.IsEnabled = true;
                SpecularLabel.Visibility = Visibility.Visible;
                SpecularTextBox.Visibility = Visibility.Visible;
            } else
            {
                // This path is never actually reached currently.
                SpecularLabel.Content = "Specular:";
                SpecularTextBox.IsEnabled = false;
                SpecularLabel.Visibility = Visibility.Hidden;
                SpecularTextBox.Visibility = Visibility.Hidden;
            }

            if (info.HasDiffuse)
            {
                DiffuseLabel.Content = "Diffuse:";
                DiffuseTextBox.IsEnabled = true;
                DiffuseLabel.Visibility = Visibility.Visible;
                DiffuseTextBox.Visibility = Visibility.Visible;
            }
            else if(info.HasReflection)
            {
                DiffuseLabel.Content = "Reflection:";
                DiffuseTextBox.IsEnabled = true;
                DiffuseLabel.Visibility = Visibility.Visible;
                DiffuseTextBox.Visibility = Visibility.Visible;

            } else
            {
                DiffuseLabel.Content = "Diffuse:";
                DiffuseTextBox.IsEnabled = false;
                DiffuseLabel.Visibility = Visibility.Hidden;
                DiffuseTextBox.Visibility = Visibility.Hidden;
            }



        }


        private void HelpButton_Click(object sender, RoutedEventArgs e)
        {
            var help = new Views.Textures.MaterialEditorHelpView();
            help.ShowDialog();
        }

        private void CopyMaterialButton_Click(object sender, RoutedEventArgs e)
        {
            _copiedMaterial = (XivMtrl) _material.Clone();
            PasteMaterialButton.IsEnabled = true;
        }

        private async void PasteMaterialButton_Click(object sender, RoutedEventArgs e)
        {
            if (_copiedMaterial != null)
            {
                // Paste the copied Material into the editor using our current path and item.
                var newCopy = (XivMtrl)_copiedMaterial.Clone();
                newCopy.MTRLPath = _material.MTRLPath;
                await SetMaterial(newCopy, _item, _mode);
            }
        }

        private async void DisableButton_Click(object sender, RoutedEventArgs e)
        {
            await viewModel.DisableMod();
        }

        private void NewSharedButton_Click(object sender, RoutedEventArgs e)
        {
            var sharedTex = "{item_folder}/{default_name}";
            DiffuseTextBox.Text = sharedTex;
            SpecularTextBox.Text = sharedTex;
            NormalTextBox.Text = sharedTex;
        }

        private void NewUniqueButton_Click(object sender, RoutedEventArgs e)
        {
            var uniqueTex = "{item_folder}/{variant}_{default_name}";
            DiffuseTextBox.Text = uniqueTex;
            SpecularTextBox.Text = uniqueTex;
            NormalTextBox.Text = uniqueTex;

        }

        private void AdvancedDetailsButton_Click(object sender, RoutedEventArgs e)
        {

            var advancedEditor = new Views.Textures.MaterialEditorAdvancedView() { Owner = Window.GetWindow(this) };

            advancedEditor.SetMaterial(_material);
            //var open = await editor.SetMaterial(material, item, mode);

            var result = advancedEditor.ShowDialog();

            // If we are saving the changes to the active edit material, bring the edited material back in.
            if(result == true)
            {
                _material = advancedEditor.Material;
            }
        }
    }
}
