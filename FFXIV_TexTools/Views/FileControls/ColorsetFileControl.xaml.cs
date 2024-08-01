using FFXIV_TexTools.Textures;
using SharpDX.Toolkit.Graphics;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Bmp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using TeximpNet.DDS;
using xivModdingFramework.Cache;
using xivModdingFramework.Items.Enums;
using xivModdingFramework.Items.Interfaces;
using xivModdingFramework.SqPack.FileTypes;
using xivModdingFramework.Textures.DataContainers;
using xivModdingFramework.Textures.Enums;
using xivModdingFramework.Textures.FileTypes;
using xivModdingFramework.Variants.DataContainers;
using xivModdingFramework.Variants.FileTypes;
using xivModdingFramework.Items;
using Image = SixLabors.ImageSharp.Image;
using xivModdingFramework.Materials.FileTypes;
using xivModdingFramework.Materials.DataContainers;
using xivModdingFramework.Mods;
using FFXIV_TexTools.Controls;
using FFXIV_TexTools.ViewModels;
using SharpDX;
using System.Collections.ObjectModel;
using FFXIV_TexTools.Helpers;
using MahApps.Metro;
using FFXIV_TexTools.Properties;
using Xceed.Wpf.Toolkit;
using xivModdingFramework.Helpers;
using System.Diagnostics;
using FFXIV_TexTools.Resources;
using xivModdingFramework.Textures;
using FFXIV_TexTools.Views.Textures;

namespace FFXIV_TexTools.Views.Controls
{
    /// <summary>
    /// Interaction logic for TextureFileControl.xaml
    /// </summary>
    public partial class ColorsetFileControl : FileViewControl, INotifyPropertyChanged
    {

        List<ColorsetRowControl> ColorSetRowControls = new List<ColorsetRowControl>();
        StainingTemplateFile DyeTemplateFile;
        int RowId = 0;

        ColorsetEditorViewModel _vm;

        private int _rowCount = 32;
        private int _columnCount = 8;

        static List<Half[]> _CopiedRow;
        static List<Half[]> CopiedRow
        {
            get => _CopiedRow;
            set
            {
                _CopiedRow = value;
                CopyUpdated?.Invoke();
            }
        }
        static byte[] CopiedRowDye;



        private static event Action CopyUpdated;

        private bool DawnTrail
        {
            get
            {
                return _columnCount == 8;
            }
        }

        private bool LegacyShader
        {
            get
            {
                if (Material == null) return false;

                if (DawnTrail)
                {
                    return Material.ShaderPack == ShaderHelpers.EShaderPack.CharacterLegacy;
                }
                else
                {
                    return true;
                }
            }
        }

        private XivMtrl Material;

        List<Half[]> RowData;

        private bool _Mtrl_Loading = true;

        ObservableCollection<KeyValuePair<ushort, string>> DyeTemplateCollection = new ObservableCollection<KeyValuePair<ushort, string>>();
        ObservableCollection<KeyValuePair<int, string>> PreviewDyeCollection = new ObservableCollection<KeyValuePair<int, string>>();
        ObservableCollection<KeyValuePair<int, string>> TileMaterialIds = new ObservableCollection<KeyValuePair<int, string>>();
        ObservableCollection<KeyValuePair<uint, string>> DyeChannelCollection = new ObservableCollection<KeyValuePair<uint, string>>();

        private List<CheckBox> DyeBoxes = new List<CheckBox>();

        private Helpers.ViewportCanvasRenderer canvasRenderer = null;


        public ColorsetFileControl()
        {
            DataContext = this;
            ViewType = EFileViewType.Editor;
            InitializeComponent();

            // This is really the Viewport's VM, not the general editor's VM.
            _vm = new ColorsetEditorViewModel(ColorsetRowViewport);
            ColorsetRowViewport.DataContext = _vm;

            if (Configuration.EnvironmentConfiguration.TT_Unshared_Rendering)
                canvasRenderer = new Helpers.ViewportCanvasRenderer(ColorsetRowViewport, AlternateViewportCanvas);

            var s = new Separator();
            s.Height = 2;
            ColorSetRowsPanel.Children.Add(s);

            for (int i = 0; i < _rowCount; i++)
            {
                ColorsetRowControl elem;
                if (_rowCount == 32)
                {
                    elem = new ColorsetRowControl(i)
                    {
                        Height = 16,
                        Width = 160
                    };
                }
                else
                {
                    elem = new ColorsetRowControl(i)
                    {
                        Height = 24,
                        Width = 160
                    };
                }

                ColorSetRowControls.Add(elem);

                var border = new Border
                {
                    Child = elem,
                    BorderThickness = new Thickness(0)
                };

                ColorSetRowsPanel.Children.Add(border);

                elem.MouseLeftButtonDown += ColorsetRow_Clicked;

                if(i % 2 == 1)
                {
                    var sep = new Separator();
                    sep.Height = 2;
                    ColorSetRowsPanel.Children.Add(sep);
                }
            }

            DyeTemplateIdBox.ItemsSource = DyeTemplateCollection;
            DyeTemplateIdBox.DisplayMemberPath = "Value";
            DyeTemplateIdBox.SelectedValuePath = "Key";

            // Binding handlers for any time the data is changed in the UI.
            const int _DYE_BITS = 12;
            for (int i = 0; i < _DYE_BITS; i++)
            {
                var st = "DyeBit" + i;
                var box = (CheckBox)FindName(st);
                box.Checked += ValueChanged;
                box.Unchecked += ValueChanged;
                DyeBoxes.Add(box);
            }

            DiffuseColorPicker.SelectedColorChanged += DiffuseColorPicker_SelectedColorChanged; ;
            SpecularColorPicker.SelectedColorChanged += SpecularColorPicker_SelectedColorChanged; ;
            EmissiveColorPicker.SelectedColorChanged += EmissiveColorPicker_SelectedColorChanged; ;


            TileIdBox.SelectionChanged += ValueChanged;
            TileSkewXBox.TextChanged += ValueChanged;
            TileSkewYBox.TextChanged += ValueChanged;
            TileCountXBox.TextChanged += ValueChanged;
            TileCountYBox.TextChanged += ValueChanged;
            TileOpacityBox.TextChanged += ValueChanged;

            ShaderEffectBox.TextChanged += ValueChanged;
            EffectOpacityBox.TextChanged += ValueChanged;
            EffectUnknownA.TextChanged += ValueChanged;
            EffectUnknownB.TextChanged += ValueChanged;
            EffectUnknownR.TextChanged += ValueChanged;

            DiffuseAlphaBox.TextChanged += ValueChanged;
            SpecAlphaBox.TextChanged += ValueChanged;
            EmissAlphaBox.TextChanged += ValueChanged;


            RoughnessBox.TextChanged += ValueChanged;
            MetallicBox.TextChanged += ValueChanged;
            PbrUnknownBox.TextChanged += ValueChanged;
            AnisotropyBlendingBox.TextChanged += ValueChanged;
            ShaderTemplateBox.TextChanged += ValueChanged;

            SheenTintRateBox.TextChanged += ValueChanged;
            SheenUnknownBox.TextChanged += ValueChanged;
            SheenRateBox.TextChanged += ValueChanged;
            SheenApertureBox.TextChanged += ValueChanged;


            DyePreviewIdBox.ItemsSource = PreviewDyeCollection;
            DyePreviewIdBox.DisplayMemberPath = "Value";
            DyePreviewIdBox.SelectedValuePath = "Key";


            TileIdBox.ItemsSource = TileMaterialIds;
            TileIdBox.DisplayMemberPath = "Value";
            TileIdBox.SelectedValuePath = "Key";
            for (int i = 0; i < 64; i++)
            {
                TileMaterialIds.Add(new KeyValuePair<int, string>(i, i.ToString()));
            }

            DyeChannelBox.ItemsSource = DyeChannelCollection;
            DyeChannelBox.DisplayMemberPath = "Value";
            DyeChannelBox.SelectedValuePath = "Key";
            for (uint i = 0; i < 4; i++)
            {
                DyeChannelCollection.Add(new KeyValuePair<uint, string>(i, (i + 1).ToString()));
            }

            SetDyeBitLabels();
            CopyUpdated += OnCopyUpdated;
        }

        private void OnCopyUpdated()
        {
            if (CopiedRow == null)
            {
                PasteRowButton.IsEnabled = false;
            }
            else
            {
                PasteRowButton.IsEnabled = true;
            }
        }

        public override void OnControlKey(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (Keyboard.Modifiers == ModifierKeys.Control)
            {
                if (e.Key == Key.D2)
                {
                    if (this.RowId < _rowCount - 1)
                    {
                        _ = SetRow(this.RowId + 1);
                        e.Handled = true;
                    }
                }
                else if (e.Key == Key.D1)
                {
                    if (this.RowId > 0)
                    {
                        _ = SetRow(this.RowId - 1);
                        e.Handled = true;
                    }
                }
            }
        }

        public override string GetNiceName()
        {
            return "Colorset";
        }

        protected override KeyValuePair<string, string> GetDefaultExtension()
        {
            return new KeyValuePair<string, string>(".mtrl", "FFXIV Material");
        }

        public override Dictionary<string, string> GetValidFileExtensions()
        {
            return new Dictionary<string, string>()
            {
                { ".mtrl", "FFXIV Material" },
                { ".dds", "DDS Image" },
                //{ ".tga", "TGA Image" }
            };
        }

        public override void INTERNAL_ClearFile()
        {
            Material = null;
        }

        protected override async Task<byte[]> INTERNAL_GetUncompressedData()
        {
            return Mtrl.XivMtrlToUncompressedMtrl(Material);
        }

        protected virtual async Task<byte[]> INTERNAL_CreateUncompressedFile(string externalFile, string internalFile, IItem referenceItem)
        {
            var ext = Path.GetExtension(externalFile).ToLower();


            // This one's a little jank since we're really loading a partial file, and supplementing it with internal game files.
            var material = await Mtrl.GetXivMtrl(internalFile, false, MainWindow.DefaultTransaction);
            Tex.ImportColorsetTexture(material, externalFile, true, true);

            return Mtrl.XivMtrlToUncompressedMtrl(material);
        }

        protected override async Task<byte[]> INTERNAL_ExternalToUncompressedFile(string externalFile, string internalFile, IItem referenceItem, ModTransaction tx)
        {
            var ext = Path.GetExtension(externalFile).ToLower();
            if (ext == ".mtrl")
            {
                return await base.INTERNAL_ExternalToUncompressedFile(externalFile, internalFile, referenceItem, tx);
            }
            else if (ext == ".dds")
            {
                // Merge DDS colorset data into our current material.
                var csetData = Tex.GetColorsetDataFromDDS(externalFile);
                var mtrl = (XivMtrl)Material.Clone();
                mtrl.ColorSetData = csetData.ColorsetData;
                mtrl.ColorSetDyeData = csetData.DyeData;
                return Mtrl.XivMtrlToUncompressedMtrl(mtrl);
            }
            else if (ext == ".tga")
            {
                throw new Exception("TGA is supported for Colorset Export only.");
            }
            else
            {
                throw new NotImplementedException();
            }
        }

        protected override async Task<bool> INTERNAL_LoadFile(byte[] data, string path, IItem referenceItem, ModTransaction tx)
        {
            var mat = Mtrl.GetXivMtrl(data, path);
            await SetMaterial(mat);
            return true;
        }

        protected override async Task<bool> INTERNAL_WriteModFile(ModTransaction tx)
        {
            // We override this in order to use MTRL's import function, which checks for missing texture files, etc.
            await Mtrl.ImportMtrl(Material, ReferenceItem, XivStrings.TexTools, true, tx);

            await EndwalkerUpgrade.UpdateEndwalkerMaterial(Material, XivStrings.TexTools, true, tx);

            return true;
        }

        protected override async Task<bool> INTERNAL_SaveAs(string externalFilePath)
        {
            var ext = Path.GetExtension(externalFilePath).ToLower();
            if(ext == ".mtrl")
            {
                File.WriteAllBytes(externalFilePath, await INTERNAL_GetUncompressedData());
                return true;
            }
            else if(ext == ".tga")
            {
                var tex = await Mtrl.GetColorsetXivTex(Material);
                var pix = await tex.GetRawPixels();

                await TextureHelpers.ModifyPixels((offset) =>
                {
                    pix[offset + 3] = 255;
                }, tex.Width, tex.Height);

                using var img = SixLabors.ImageSharp.Image.LoadPixelData<Rgba32>(pix, tex.Width, tex.Height);

                img.SaveAsTga(externalFilePath,
                    new SixLabors.ImageSharp.Formats.Tga.TgaEncoder()
                    {
                        BitsPerPixel = SixLabors.ImageSharp.Formats.Tga.TgaBitsPerPixel.Pixel32,
                        Compression = SixLabors.ImageSharp.Formats.Tga.TgaCompression.None
                    });

                return true;
            }
            else
            {
                var tex = await Mtrl.GetColorsetXivTex(Material);
                var dyePath = Path.Combine(Path.GetDirectoryName(externalFilePath), Path.GetFileNameWithoutExtension(externalFilePath) + ".dat");

                Mtrl.SaveColorsetDyeData(Material, dyePath);
                Tex.SaveTexAsDDS(externalFilePath, tex);
                return true;
            }
        }


        protected override async Task<bool> ShouldUpdateOnFileChange(string changedFile)
        {
            if(!string.Equals(changedFile, InternalFilePath))
            {
                // We only care about changing if our exact file was altered.
                return false;
            }

            // Time for some cursed tech.
            return await Task.Run(async () =>
            {
                var tx = MainWindow.DefaultTransaction;
                if (!await tx.FileExists(changedFile) || Material == null)
                {
                    // File was deleted or restored.
                    return true;
                }

                var newMtrl = await Mtrl.GetXivMtrl(changedFile, false, tx);

                var result = Mtrl.CompareMaterials(Material, newMtrl);

                if (result.ColorsetDifferences)
                {
                    // If our colorset got changed, we should be prompted for reload.
                    return true;
                }

                if (!result.OtherDifferences)
                {
                    // No reason to reload if it's the same file.
                    return false;
                }

                // Okay, we now need to the data and soft-reload the UI.
                newMtrl.ColorSetData = Material.ColorSetData;
                newMtrl.ColorSetDyeData = Material.ColorSetDyeData;

                await SetMaterial(newMtrl, RowId);
                await UpdateModState(tx);

                return false;
            });
        }


        private void SetDyeBitLabels()
        {
            if (Material == null || Material.ShaderPack != ShaderHelpers.EShaderPack.CharacterLegacy)
            {
                DyeBit0.Content = "Dye Diffuse";
                DyeBit1.Content = "Dye Specular(?)";
                DyeBit2.Content = "Dye Emissive";
                DyeBit3.Content = "Dye Emissive Alpha(?)";
                DyeBit4.Content = "Dye Metallic";
                DyeBit5.Content = "Dye Roughness";
                DyeBit6.Content = "Dye Fresnel Y";
                DyeBit7.Content = "Dye Fresnel Z";
                DyeBit8.Content = "Dye Fresnel Albedo";
                DyeBit9.Content = "Dye Anisotropy";
                DyeBit10.Content = "Dye Shader Effect";
                DyeBit11.Content = "Dye Effect Opacity";
                DyeBit5.Visibility = Visibility.Visible;
                DyeBit6.Visibility = Visibility.Visible;
                DyeBit7.Visibility = Visibility.Visible;
                DyeBit8.Visibility = Visibility.Visible;
                DyeBit9.Visibility = Visibility.Visible;
                DyeBit10.Visibility = Visibility.Visible;
                DyeBit11.Visibility = Visibility.Visible;

                ShaderTemplateBox.Visibility = Visibility.Visible;

                PbrGroup.Visibility = Visibility.Visible;
                FresnelGroup.Visibility = Visibility.Visible;
                ShaderEffectsGroup.Visibility = Visibility.Visible;

                DiffuseAlphaLabel.Content = "Diffuse Unknown";
                SpecAlphaLabel.Content = "Specular Unknown";

            }
            else
            {
                DyeBit0.Content = "Dye Diffuse";
                DyeBit1.Content = "Dye Specular";
                DyeBit2.Content = "Dye Emissive";
                DyeBit3.Content = "Dye Specular Power";
                DyeBit4.Content = "Dye Gloss";

                DiffuseAlphaLabel.Content = "Gloss";
                SpecAlphaLabel.Content = "Specular Power";

                DyeBit5.Visibility = Visibility.Collapsed;
                DyeBit6.Visibility = Visibility.Collapsed;
                DyeBit7.Visibility = Visibility.Collapsed;
                DyeBit8.Visibility = Visibility.Collapsed;
                DyeBit9.Visibility = Visibility.Collapsed;
                DyeBit10.Visibility = Visibility.Collapsed;
                DyeBit11.Visibility = Visibility.Collapsed;

                ShaderTemplateBox.Visibility = Visibility.Collapsed;
                ShaderTemplateLabel.Visibility = Visibility.Collapsed;

                PbrGroup.Visibility = Visibility.Collapsed;
                FresnelGroup.Visibility = Visibility.Collapsed;
                ShaderEffectsGroup.Visibility = Visibility.Collapsed;

            }
        }


        public void ValueChanged(object sender, RoutedEventArgs e)
        {
            UpdateRow();
        }

        // Color pickers get their own special handling, because technically the raw values can go over
        // normal 255 byte color ranges, so we need to not stomp them when calling the normal UpdateRow() function.
        private void DiffuseColorPicker_SelectedColorChanged(object sender, RoutedPropertyChangedEventArgs<System.Windows.Media.Color?> e)
        {
            if (_Mtrl_Loading) return;

            if (DiffuseColorPicker.SelectedColor.Value.A != 255)
            {
                DiffuseColorPicker.SelectedColor = new System.Windows.Media.Color()
                {
                    R = DiffuseColorPicker.SelectedColor.Value.R,
                    B = DiffuseColorPicker.SelectedColor.Value.B,
                    G = DiffuseColorPicker.SelectedColor.Value.G,
                    A = 255
                };
            }

            RowData[0][0] = ColorByteToHalf(DiffuseColorPicker.SelectedColor.Value.R);
            RowData[0][1] = ColorByteToHalf(DiffuseColorPicker.SelectedColor.Value.G);
            RowData[0][2] = ColorByteToHalf(DiffuseColorPicker.SelectedColor.Value.B);
            UpdateRow();
        }
        private void SpecularColorPicker_SelectedColorChanged(object sender, RoutedPropertyChangedEventArgs<System.Windows.Media.Color?> e)
        {
            if (_Mtrl_Loading) return;

            if (SpecularColorPicker.SelectedColor.Value.A != 255)
            {
                SpecularColorPicker.SelectedColor = new System.Windows.Media.Color()
                {
                    R = SpecularColorPicker.SelectedColor.Value.R,
                    B = SpecularColorPicker.SelectedColor.Value.B,
                    G = SpecularColorPicker.SelectedColor.Value.G,
                    A = 255
                };
            }

            var r = SpecularColorPicker.SelectedColor.Value.R;
            var g = SpecularColorPicker.SelectedColor.Value.G;
            var b = SpecularColorPicker.SelectedColor.Value.B;


            RowData[1][0] = ColorByteToHalf(SpecularColorPicker.SelectedColor.Value.R);
            RowData[1][1] = ColorByteToHalf(SpecularColorPicker.SelectedColor.Value.G);
            RowData[1][2] = ColorByteToHalf(SpecularColorPicker.SelectedColor.Value.B);
            UpdateRow();
        }
        private void EmissiveColorPicker_SelectedColorChanged(object sender, RoutedPropertyChangedEventArgs<System.Windows.Media.Color?> e)
        {
            if (_Mtrl_Loading) return;

            if (EmissiveColorPicker.SelectedColor.Value.A != 255)
            {
                EmissiveColorPicker.SelectedColor = new System.Windows.Media.Color()
                {
                    R = EmissiveColorPicker.SelectedColor.Value.R,
                    B = EmissiveColorPicker.SelectedColor.Value.B,
                    G = EmissiveColorPicker.SelectedColor.Value.G,
                    A = 255
                };
            }


            RowData[2][0] = ColorByteToHalf(EmissiveColorPicker.SelectedColor.Value.R);
            RowData[2][1] = ColorByteToHalf(EmissiveColorPicker.SelectedColor.Value.G);
            RowData[2][2] = ColorByteToHalf(EmissiveColorPicker.SelectedColor.Value.B);
            UpdateRow();
        }

        private async void ColorsetRow_Clicked(object sender, MouseButtonEventArgs e)
        {
            try
            {
                var selectedRowControl = e.Source as ColorsetRowControl;
                if (selectedRowControl == null) return;
                SelectedColorsetRowImage.Source = selectedRowControl.RowImageSource;
                var rowNumber = (int)selectedRowControl.DataContext;
                if(rowNumber >= _rowCount)
                {
                    return;
                }
                await SetRow(rowNumber);
            }
            catch(Exception ex)
            {
                Trace.WriteLine(ex);
            }
        }

        private List<Half[]> GetRowData(int row)
        {
            if(row >= _rowCount)
            {
                return new List<Half[]>();
            }

            var offset = row * _columnCount * 4;

            var data = new List<Half[]>(4);
            for (int i = 0; i < _columnCount; i++)
            {
                var arr = new Half[4];
                data.Add(arr);
                for (int z = 0; z < 4; z++)
                {
                    arr[z] = Material.ColorSetData[offset];
                    offset++;
                }
            }
            return data;
        }

        private Half[] RowDataToRaw(List<Half[]> data)
        {
            var raw = new Half[_columnCount * 4];
            for (int i = 0; i < _columnCount; i++)
            {
                Array.Copy(data[i], 0, raw, i * 4, 4);
            }

            return raw;
        }

        private async Task SetRow(int rowNumber)
        {

            if (Material == null) return;

            _Mtrl_Loading = true;

            var dyeLen = Material.ColorSetData.Count == 256 ? 32 : 128;
            if (Material.ColorSetDyeData == null || Material.ColorSetDyeData.Length != dyeLen)
            {
                Material.ColorSetDyeData = new byte[dyeLen];
            }

            RowId = rowNumber;

            // Triggered when the user clicks on a Colorset row.
            DetailsGroupBox.Header = $"Material - Colorset Row Editor - Row {ViewHelpers.ColorsetRowToNiceName(rowNumber)}".L();
            RowData = GetRowData(RowId);

            SetDyeBitLabels();

            var r = ColorHalfToByte(RowData[0][0]);
            var g = ColorHalfToByte(RowData[0][1]);
            var b = ColorHalfToByte(RowData[0][2]);
            DiffuseColorPicker.SelectedColor = new System.Windows.Media.Color() { R = r, G = g, B = b, A = 255 };

            r = ColorHalfToByte(RowData[1][0]);
            g = ColorHalfToByte(RowData[1][1]);
            b = ColorHalfToByte(RowData[1][2]);
            SpecularColorPicker.SelectedColor = new System.Windows.Media.Color() { R = r, G = g, B = b, A = 255 };

            r = ColorHalfToByte(RowData[2][0]);
            g = ColorHalfToByte(RowData[2][1]);
            b = ColorHalfToByte(RowData[2][2]);
            EmissiveColorPicker.SelectedColor = new System.Windows.Media.Color() { R = r, G = g, B = b, A = 255 };



            DiffuseAlphaBox.Text = RowData[0][3].ToString();
            SpecAlphaBox.Text = RowData[1][3].ToString();
            EmissAlphaBox.Text = RowData[2][3].ToString();


            SheenRateBox.Text = RowData[3][0].ToString();
            SheenTintRateBox.Text = RowData[3][1].ToString();
            SheenApertureBox.Text = RowData[3][2].ToString();
            SheenUnknownBox.Text = RowData[3][3].ToString();


            RoughnessBox.Text = RowData[4][0].ToString();
            PbrUnknownBox.Text = RowData[4][1].ToString();
            MetallicBox.Text = RowData[4][2].ToString();
            AnisotropyBlendingBox.Text = RowData[4][3].ToString();


            EffectUnknownR.Text = RowData[5][0].ToString();
            EffectOpacityBox.Text = RowData[5][1].ToString();
            EffectUnknownB.Text = RowData[5][2].ToString();
            EffectUnknownA.Text = RowData[5][3].ToString();


            ShaderTemplateBox.Text = RowData[6][0].ToString();
            TileIdBox.SelectedValue = (int)(Math.Floor(RowData[6][1] * 64));
            TileOpacityBox.Text = RowData[6][2].ToString();
            ShaderEffectBox.Text = RowData[6][3].ToString();


            TileCountXBox.Text = RowData[7][0].ToString();
            TileCountYBox.Text = RowData[7][3].ToString();
            TileSkewXBox.Text = RowData[7][1].ToString();
            TileSkewYBox.Text = RowData[7][2].ToString();




            uint dyeData = 0;
            if (Material.ColorSetDyeData.Length != 0)
            {
                if (DawnTrail)
                {
                    dyeData = BitConverter.ToUInt32(Material.ColorSetDyeData, rowNumber * 4);
                }
                else
                {
                    dyeData = BitConverter.ToUInt16(Material.ColorSetDyeData, rowNumber * 2);
                }
            }

            if (dyeData == uint.MaxValue)
            {
                dyeData = 0;
            }

            ushort dyeTemplateId = STM.GetTemplateKeyFromMaterialData(Material, RowId);
            DyeTemplateIdBox.SelectedValue = dyeTemplateId;

            var dyeBoxes = DawnTrail ? DyeBoxes.Count : 5;
            for (int i = 0; i < dyeBoxes; i++)
            {
                var shifted = 0x01 << i;
                var active = (dyeData & shifted) > 0;
                DyeBoxes[i].IsChecked = active;
            }

            if (dyeData > 0 && DawnTrail)
            {
                uint dyeChannel = dyeData << 3 >> 30;
                DyeChannelBox.SelectedValue = dyeChannel;
            }
            else
            {
                DyeChannelBox.SelectedValue = (uint)0;
            }

            foreach (var control in ColorSetRowControls)
            {
                control.Margin = new Thickness(0);
                var border = (Border)control.Parent;
                border.BorderThickness = new Thickness(0);
                border.BorderBrush = Brushes.Transparent;
            }

            var rowControl = ColorSetRowControls[RowId];
            rowControl.Margin = new Thickness(-2, 0, -2, 0);
            var rowBorder = (Border)rowControl.Parent;
            rowBorder.BorderThickness = new Thickness(2);
            rowBorder.BorderBrush = Brushes.Black;

            UpdateDyeStatus();

            await UpdateViewport();

            _Mtrl_Loading = false;
        }

        private async Task UpdateViewport()
        {
            int dyeId = -1;
            if (DyePreviewIdBox.SelectedValue != null)
            {
                dyeId = (int)DyePreviewIdBox.SelectedValue;
            }

            await _vm.SetColorsetRow(RowId, _columnCount, dyeId);
        }

        public static byte ColorHalfToByte(Half half)
        {
            var b = (byte)Math.Round((Math.Sqrt(half) * 255));

            return b;
        }

        public static Half ColorByteToHalf(byte b)
        {
            var f = (b / 255.0f);
            var half = f * f;
            return (Half)(half);
        }

        /// <summary>
        /// Updates the visual image display of the row, both in the
        /// main left-hand listing and in the selected display if the
        /// row is selected.
        /// </summary>
        /// <param name="rowId"></param>
        /// <returns></returns>
        public async Task UpdateRowVisual(int rowId)
        {
            var pixels = new byte[_columnCount * 4];
            for (int x = 0; x < _columnCount; x++)
            {
                var valueOffset = (rowId * _columnCount * 4) + (x * 4);

                // Convert RGBA to BGRA
                byte[] pixel = new byte[4];
                var destinationOffset = x * 4;
                pixels[destinationOffset + 0] = ColorHalfToByte(Material.ColorSetData[valueOffset + 2]);
                pixels[destinationOffset + 1] = ColorHalfToByte(Material.ColorSetData[valueOffset + 1]);
                pixels[destinationOffset + 2] = ColorHalfToByte(Material.ColorSetData[valueOffset + 0]);
                //pixels[destinationOffset + 3] = HalfToByte(Material.ColorSetData[valueOffset + 3]);

                // Turn off Alpha, it looks horrible and confusing.
                pixels[destinationOffset + 3] = 255;
            }

            const int expansionSize = 8;
            var expandedPixels = new byte[pixels.Length * expansionSize * expansionSize];
            for (int z = 0; z < expansionSize; z++)
            {
                for (int w = 0; w < expansionSize * _columnCount; w++)
                {
                    var offset = ((z * expansionSize * _columnCount) + w) * 4;
                    expandedPixels[offset + 0] = pixels[(w / expansionSize) * 4 + 0];
                    expandedPixels[offset + 1] = pixels[(w / expansionSize) * 4 + 1];
                    expandedPixels[offset + 2] = pixels[(w / expansionSize) * 4 + 2];
                    expandedPixels[offset + 3] = pixels[(w / expansionSize) * 4 + 3];
                }
            }

            ColorSetRowControls[rowId].RowImageSource = BitmapSource.Create(_columnCount * expansionSize, 1 * expansionSize, 1, 1, PixelFormats.Bgra32, null, expandedPixels, expansionSize * _columnCount * 4);
            if (RowId == rowId)
            {
                SelectedColorsetRowImage.Source = ColorSetRowControls[rowId].RowImageSource;
            }
        }

        /// <summary>
        /// Overridden here to only load the colorset data.
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        public override async Task<bool> LoadRawData(byte[] data)
        {
            if (!this.ConfirmDiscardChanges(InternalFilePath))
            {
                return false;
            }
            var mat = Mtrl.GetXivMtrl(data, InternalFilePath);

            Material.ColorSetData = mat.ColorSetData;
            Material.ColorSetDyeData = mat.ColorSetDyeData;
            await SetMaterial(Material, RowId);
            UnsavedChanges = true;
            return true;
        }

        /// <summary>
        /// Sets the material and selects a given row (or row 0)
        /// </summary>
        /// <param name="mtrl"></param>
        /// <param name="row"></param>
        /// <returns></returns>
        public async Task SetMaterial(XivMtrl material, int row = -1)
        {
            if(row < 0)
            {
                if(RowId >= 0)
                {
                    row = RowId;
                } else
                {
                    row = 0;
                }
            }
            Material = material;
            if (Material == null) return;

            _Mtrl_Loading = true;

            STM.EStainingTemplate stainingTemplate;
            _columnCount = 8;
            _rowCount = 32;
            if (Material.ShaderPack == ShaderHelpers.EShaderPack.CharacterLegacy)
            {
                stainingTemplate = STM.EStainingTemplate.Endwalker;
            }
            else
            {
                stainingTemplate = STM.EStainingTemplate.Dawntrail;
            }


            var appStyle = ThemeManager.DetectAppStyle(Application.Current);
            Brush bgBrush = MainWindow.GetMainWindow().Background;
            Brush fgBrush = MainWindow.GetMainWindow().Foreground;


            DiffuseColorPicker.Background = bgBrush;
            DiffuseColorPicker.Foreground = fgBrush;
            SpecularColorPicker.Background = bgBrush;
            SpecularColorPicker.Foreground = fgBrush;
            EmissiveColorPicker.Background = bgBrush;
            EmissiveColorPicker.Foreground = fgBrush;

            DiffuseColorPicker.DropDownBackground = bgBrush;
            DiffuseColorPicker.HeaderForeground = fgBrush;
            DiffuseColorPicker.TabForeground = fgBrush;
            DiffuseColorPicker.TabBackground = bgBrush;
            DiffuseColorPicker.HeaderBackground = bgBrush;

            SpecularColorPicker.DropDownBackground = bgBrush;
            SpecularColorPicker.HeaderForeground = fgBrush;
            SpecularColorPicker.TabForeground = fgBrush;
            SpecularColorPicker.TabBackground = bgBrush;
            SpecularColorPicker.HeaderBackground = bgBrush;

            EmissiveColorPicker.DropDownBackground = bgBrush;
            EmissiveColorPicker.HeaderForeground = fgBrush;
            EmissiveColorPicker.TabForeground = fgBrush;
            EmissiveColorPicker.TabBackground = bgBrush;
            EmissiveColorPicker.HeaderBackground = bgBrush;

            try
            {
                DyeTemplateFile = await STM.GetStainingTemplateFile(stainingTemplate);
                DyeTemplateCollection.Clear();

                DyePreviewIdBox.SelectedValue = -1;

                var keys = DyeTemplateFile.GetKeys();
                DyeTemplateCollection.Add(new KeyValuePair<ushort, string>(0, "Undyable".L()));
                foreach (var key in keys)
                {
                    DyeTemplateCollection.Add(new KeyValuePair<ushort, string>(key, key.ToString()));
                }

                if (CopiedRow == null)
                {
                    PasteRowButton.IsEnabled = false;
                }
                else
                {
                    PasteRowButton.IsEnabled = true;
                }

                var dyes = await STM.GetDyeNames(MainWindow.DefaultTransaction);

                PreviewDyeCollection.Clear();
                PreviewDyeCollection.Add(new KeyValuePair<int, string>(-1, "Undyed".L()));
                for (ushort i = 0; i < 128; i++)
                {
                    var name = "Dye " + i.ToString();
                    if (dyes.ContainsKey(i))
                    {
                        name = dyes[i];
                    }
                    PreviewDyeCollection.Add(new KeyValuePair<int, string>(i, name));
                }
                DyePreviewIdBox.SelectedValue = -1;


                await _vm.SetMaterial(Material, DyeTemplateFile);
                await SetRow(row);

                for (int i = 0; i < _rowCount; i++)
                {
                    await UpdateRowVisual(i);
                }

            }
            catch (Exception ex)
            {
                FlexibleMessageBox.Show("Unable to load material into colorset editor.\n\nError: ".L() + ex.Message, "Colorset Editor Error".L(), System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Warning);
            }
            _Mtrl_Loading = false;
        }


        /// <summary>
        /// Updates the status of the dye checkboxes based on template information.
        /// </summary>
        private void UpdateDyeStatus()
        {
            var value = DyeTemplateIdBox.SelectedValue;
            if (value == null) return;

            var entry = DyeTemplateFile.GetTemplate((ushort)value);

            CopyDyeValuesButton.IsEnabled = false;
            if (entry == null)
            {
                for (int i = 0; i < DyeBoxes.Count; i++)
                {
                    DyeBoxes[i].IsEnabled = false;
                    DyeBoxes[i].IsChecked = false;
                }
                return;
            }

            if (DyePreviewIdBox.SelectedValue != null)
            {
                var dyeId = (int)DyePreviewIdBox.SelectedValue;
                if (dyeId >= 0)
                {
                    CopyDyeValuesButton.IsEnabled = true;
                }
            }

            for (int i = 0; i < DyeBoxes.Count; i++)
            {
                UpdateDyeBox(DyeBoxes[i], entry, i);
            }
        }

        private void UpdateDyeBox(CheckBox dyeBox, StainingTemplateEntry entry, int usedOffset)
        {
            if (entry.GetData(usedOffset) == null)
            {
                dyeBox.IsChecked = false;
                dyeBox.IsEnabled = false;
            }
            else
            {
                dyeBox.IsEnabled = true;
            }
        }

        /// <summary>
        /// Updates all the Row Data based on the current UI State
        /// EXCEPT for the color pickers, which are handled separately.
        /// </summary>
        private async Task UpdateRow()
        {
            if (Material == null) return;
            if (Material.ColorSetData.Count == 0) return;
            if (_Mtrl_Loading) return;

            try
            {
                float fl;

                fl = 1.0f;
                float.TryParse(DiffuseAlphaBox.Text, out fl);
                RowData[0][3] = new Half(fl);

                fl = 1.0f;
                float.TryParse(SpecAlphaBox.Text, out fl);
                RowData[1][3] = new Half(fl);

                fl = 1.0f;
                float.TryParse(EmissAlphaBox.Text, out fl);
                RowData[2][3] = new Half(fl);


                fl = 0.0f;
                float.TryParse(SheenRateBox.Text, out fl);
                RowData[3][0] = new Half(fl);

                fl = 0.0f;
                float.TryParse(SheenTintRateBox.Text, out fl);
                RowData[3][1] = new Half(fl);

                fl = 0.0f;
                float.TryParse(SheenApertureBox.Text, out fl);
                RowData[3][2] = new Half(fl);

                fl = 0.0f;
                float.TryParse(SheenUnknownBox.Text, out fl);
                RowData[3][3] = new Half(fl);

                fl = 0.0f;
                float.TryParse(RoughnessBox.Text, out fl);
                RowData[4][0] = new Half(fl);

                fl = 0.0f;
                float.TryParse(PbrUnknownBox.Text, out fl);
                RowData[4][1] = new Half(fl);

                fl = 0.0f;
                float.TryParse(MetallicBox.Text, out fl);
                RowData[4][2] = new Half(fl);

                fl = 0.0f;
                float.TryParse(AnisotropyBlendingBox.Text, out fl);
                RowData[4][3] = new Half(fl);

                fl = 0.0f;
                float.TryParse(EffectUnknownR.Text, out fl);
                RowData[5][0] = new Half(fl);

                fl = 0.0f;
                float.TryParse(EffectOpacityBox.Text, out fl);
                RowData[5][1] = new Half(fl);

                fl = 0.0f;
                float.TryParse(EffectUnknownB.Text, out fl);
                RowData[5][2] = new Half(fl);

                fl = 0.0f;
                float.TryParse(EffectUnknownA.Text, out fl);
                RowData[5][3] = new Half(fl);

                fl = 0.0f;
                float.TryParse(ShaderTemplateBox.Text, out fl);
                RowData[6][0] = new Half(fl);


                RowData[6][1] = new Half((((int)TileIdBox.SelectedValue) + 0.5f) / 64.0f);

                fl = 0.0f;
                float.TryParse(TileOpacityBox.Text, out fl);
                RowData[6][2] = new Half(fl);

                fl = 0.0f;
                float.TryParse(ShaderEffectBox.Text, out fl);
                RowData[6][3] = new Half(fl);

                fl = 16.0f;
                float.TryParse(TileCountXBox.Text, out fl);
                RowData[7][0] = new Half(fl);

                fl = 16.0f;
                float.TryParse(TileCountYBox.Text, out fl);
                RowData[7][3] = new Half(fl);

                fl = 0f;
                float.TryParse(TileSkewXBox.Text, out fl);
                RowData[7][1] = new Half(fl);

                fl = 0f;
                float.TryParse(TileSkewYBox.Text, out fl);
                RowData[7][2] = new Half(fl);


                uint modifier = (uint)0;
                if (DyeTemplateIdBox.SelectedValue != null)
                {

                    var v = (ushort)DyeTemplateIdBox.SelectedValue;
                    uint templateId = v;
                    var shifted = templateId << 16;
                    modifier |= shifted;

                    var channel = (uint)DyeChannelBox.SelectedValue;
                    shifted = channel << 27;
                    modifier |= shifted;

                    for (int i = 0; i < DyeBoxes.Count; i++)
                    {
                        if (DyeBoxes[i].IsChecked == true)
                        {
                            shifted = (uint)(0x01 << i);
                            modifier |= shifted;
                        }
                    }
                }



                var _dyeSize = 4;


                var offset = RowId * _dyeSize;
                var bytes = BitConverter.GetBytes(modifier);

                Array.Copy(bytes, 0, Material.ColorSetDyeData, offset, _dyeSize);

                offset = RowId * _columnCount * 4;
                for (int x = 0; x < _columnCount; x++)
                {
                    for (int y = 0; y < 4; y++)
                    {
                        Material.ColorSetData[offset] = RowData[x][y];
                        offset++;
                    }
                }

                UnsavedChanges = true;

                UpdateDyeStatus();
                await UpdateRowVisual(RowId);
                await UpdateViewport();
            }
            catch (Exception ex)
            {
                // No-Op...?
                var z = "z";
            }
        }

        private async void DataChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                if (!_Mtrl_Loading)
                {
                    UnsavedChanges = true;
                }
                await UpdateRow();
            }
            catch (Exception ex)
            {
                this.ShowError("Unknown Error", "An error occurred:\n\n" + ex.Message);
            }
        }



        private void RawAssignColorPixel(int col, string name, ColorPicker picker)
        {
            if (!RawEditPixel(col, name, false))
            {
                return;
            }
            _Mtrl_Loading = true;

            var c = GetDisplayColor(col);
            picker.SelectedColor = new System.Windows.Media.Color()
            {
                R = c.r,
                G = c.g,
                B = c.b,
                A = 255,
            };
            UnsavedChanges = true;
            _Mtrl_Loading = false;
            UpdateRow();
        }

        private void AssignPixel(int col, float r, float g, float b, float a = float.NaN)
        {
            _Mtrl_Loading = true;
            RowData[col][0] = r;
            RowData[col][1] = g;
            RowData[col][2] = b;

            if (a != float.NaN)
            {
                RowData[col][3] = a;
            }
            UnsavedChanges = true;
            _Mtrl_Loading = false;
        }
        private (byte r, byte g, byte b) GetDisplayColor(int col)
        {
            var hr = RowData[col][0];
            var hg = RowData[col][1];
            var hb = RowData[col][2];


            return (ColorHalfToByte(hr), ColorHalfToByte(hg), ColorHalfToByte(hb));

        }

        private bool RawEditPixel(int col, string title, bool includeAlpha = false)
        {
            if (col >= _columnCount)
            {
                return false;
            }

            if (includeAlpha)
            {
                var (r, g, b, a) = (RowData[col][0], RowData[col][1], RowData[col][2], RowData[col][3]);
                var result = RawFloatValueDisplay.ShowEditor(r, g, b, a, title);
                if (float.IsNaN(result.Red)) return false;
                AssignPixel(col, result.Red, result.Green, result.Blue, result.Alpha);
            }
            else
            {
                var (r, g, b) = (RowData[col][0], RowData[col][1], RowData[col][2]);
                var result = RawFloatValueDisplay.ShowEditor(r, g, b, title);
                if (float.IsNaN(result.Red)) return false;
                AssignPixel(col, result.Red, result.Green, result.Blue);
            }
            return true;
        }
        private void EditCol4_Click(object sender, RoutedEventArgs e)
        {
            if (RawEditPixel(3, "Pixel #4", true))
            {
                UpdateRow();
            }
        }
        private void EditCol5_Click(object sender, RoutedEventArgs e)
        {
            if (RawEditPixel(4, "Pixel #5", true))
            {
                UpdateRow();
            }
        }
        private void EditCol6_Click(object sender, RoutedEventArgs e)
        {
            if (RawEditPixel(5, "Pixel #6", true))
            {
                UpdateRow();
            }
        }
        private void EditCol7_Click(object sender, RoutedEventArgs e)
        {
            if (RawEditPixel(6, "Pixel #7", true))
            {
                UpdateRow();
            }
        }

        private void EditRawDiffuse_Click(object sender, RoutedEventArgs e)
        {
            RawAssignColorPixel(0, "Diffuse Pixel", DiffuseColorPicker);
        }
        private void EditRawSpecular_Click(object sender, RoutedEventArgs e)
        {
            RawAssignColorPixel(1, "Specular Pixel", SpecularColorPicker);
        }
        private void EditRawEmmissive_Click(object sender, RoutedEventArgs e)
        {
            RawAssignColorPixel(2, "Emissive Pixel", EmissiveColorPicker);
        }
        private void CopyRowButton_Click(object sender, RoutedEventArgs e)
        {
            CopiedRow = GetRowData(RowId);

            var dyeSize = 2;
            if (Material.ColorSetData.Count > 256)
            {
                dyeSize = 4;
            }

            CopiedRowDye = new byte[dyeSize];
            Array.Copy(Material.ColorSetDyeData, RowId * dyeSize, CopiedRowDye, 0, dyeSize);

            PasteRowButton.IsEnabled = true;
        }

        private async void PasteRowButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (CopiedRow == null) return;

                // Disable Dye copying for now since that's not set up yet.

                var dyeSize = 2;
                if (Material.ColorSetData.Count > 256)
                {
                    dyeSize = 4;
                }
                var offset = RowId * dyeSize;
                Array.Copy(CopiedRowDye, 0, Material.ColorSetDyeData, offset, dyeSize);

                offset = RowId * _columnCount * 4;
                for (int x = 0; x < _columnCount; x++)
                {
                    for (int y = 0; y < 4; y++)
                    {
                        Material.ColorSetData[offset] = CopiedRow[x][y];
                        offset++;
                    }
                }

                await UpdateRowVisual(RowId);
                await SetRow(RowId);

                UnsavedChanges = true;
            }
            catch(Exception ex)
            {
                Trace.WriteLine(ex);
            }
        }

        private async void MoveRowUpButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (RowId == 0) return;
                var prevRowId = RowId - 1;
                await SwapRows(RowId, prevRowId);
            }
            catch (Exception ex)
            {
                this.ShowError("Unknown Error", "An error occurred:\n\n" + ex.Message);
            }
        }

        private async void MoveRowDownButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (RowId == _rowCount - 1) return;
                var prevRowId = RowId + 1;
                await SwapRows(RowId, prevRowId);
            }
            catch (Exception ex)
            {
                this.ShowError("Unknown Error", "An error occurred:\n\n" + ex.Message);
            }
        }

        private async Task SwapRows(int row1, int row2)
        {
            var myRowData = GetRowData(row1);
            var otherRowData = GetRowData(row2);

            var myData = new Half[4 * _columnCount];
            var otherData = new Half[4 * _columnCount];
            for (int i = 0; i < _columnCount; i++)
            {
                Array.Copy(myRowData[i], 0, myData, i * 4, 4);
                Array.Copy(otherRowData[i], 0, otherData, i * 4, 4);
            }

            var myOffset = row1 * 4 * _columnCount;
            var otherOffset = row2 * 4 * _columnCount;

            var arr = Material.ColorSetData.ToArray();
            Array.Copy(myData, 0, arr, otherOffset, 4 * _columnCount);
            Array.Copy(otherData, 0, arr, myOffset, 4 * _columnCount);

            var offset1 = row1 * 2;
            var offset2 = row2 * 2;

            var b1 = Material.ColorSetDyeData[offset1];
            var b2 = Material.ColorSetDyeData[offset1 + 1];

            Material.ColorSetDyeData[offset1] = Material.ColorSetDyeData[offset2];
            Material.ColorSetDyeData[offset1 + 1] = Material.ColorSetDyeData[offset2 + 1];
            Material.ColorSetDyeData[offset2] = b1;
            Material.ColorSetDyeData[offset2 + 1] = b2;

            Material.ColorSetData = arr.ToList();

            UnsavedChanges = true;
            await SetMaterial(Material, row2);
        }

        private async void DyePreviewIdBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                if (Material == null) return;

                CopyDyeValuesButton.IsEnabled = false;
                CopyAllDyeValuesButton.IsEnabled = false;
                if (DyePreviewIdBox.SelectedValue != null && DyeTemplateIdBox.SelectedValue != null)
                {
                    CopyAllDyeValuesButton.IsEnabled = true;
                    var template = (ushort)DyeTemplateIdBox.SelectedValue;
                    var dyeId = (int)DyePreviewIdBox.SelectedValue;
                    if (dyeId >= 0 && template > 0)
                    {
                        CopyDyeValuesButton.IsEnabled = true;
                    }
                }

                if (_Mtrl_Loading) return;

                await UpdateViewport();
            }
            catch (Exception ex)
            {
                this.ShowError("Unknown Error", "An error occurred:\n\n" + ex.Message);
            }
        }

        private async void CopyDyeValuesButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var dyeId = (int)DyePreviewIdBox.SelectedValue;

                ApplyDye(RowId, dyeId, -1);

                // Reload the UI.
                await SetMaterial(Material, RowId);
            }
            catch (Exception ex)
            {
                this.ShowError("Unknown Error", "An error occurred:\n\n" + ex.Message);
            }
        }
        private async void CopyAllDyeValuesButton_Click(object sender, RoutedEventArgs e)
        {
            DyeContextMenu.IsOpen = true;
        }

        private void ApplyDye(int rowId, int dyeId, int channel)
        {
            ushort dyeTemplateId = STM.GetTemplateKeyFromMaterialData(Material.ColorSetDyeData, rowId);
            var template = DyeTemplateFile.GetTemplate(dyeTemplateId);

            var templateType = LegacyShader ? STM.EStainingTemplate.Endwalker : STM.EStainingTemplate.Dawntrail;

            if (template == null) return;
            if (dyeId < 0 || dyeId >= 128) return;

            uint dyeData = 0;
            if (Material.ColorSetDyeData.Length == 0)
            {
                return;
            }

            if (DawnTrail)
            {
                dyeData = BitConverter.ToUInt32(Material.ColorSetDyeData, rowId * 4);
            }
            else
            {
                dyeData = BitConverter.ToUInt16(Material.ColorSetDyeData, rowId * 2);
            }

            var dyeCount = LegacyShader ? 5 : 12;
            var rowData = GetRowData(rowId);
            for (int i = 0; i < dyeCount; i++)
            {
                uint dyeChannel = dyeData << 3 >> 30;
                var shifted = (uint)(0x1 << i);
                if ((dyeData & shifted) > 0 && (dyeChannel == channel || channel < 0))
                {
                    // Apply this template dye value to the row.
                    var data = template.GetData(i, dyeId);
                    if (data == null) continue;

                    // Have to used our cursed translation table here.
                    var targetOffset = StainingTemplateEntry.TemplateEntryOffsetToColorsetOffset[templateType][i];

                    if ((i == 3 || i == 4) && !DawnTrail)
                    {
                        // Handling for gloss/spec being flipped.
                        if (i == 3)
                        {
                            targetOffset = 7;
                        }
                        else
                        {
                            targetOffset = 3;
                        }
                    }

                    var destinationPixel = targetOffset / 4;
                    var destinationColorIndex = targetOffset % 4;

                    for (int z = 0; z < data.Length; z++)
                    {
                        rowData[destinationPixel][destinationColorIndex + z] = data[z];
                    }
                }
            }

            // Copy RowData into the main colorset array.
            var rawData = RowDataToRaw(rowData);
            var fullData = Material.ColorSetData.ToArray();
            var offset = rowId * _columnCount * 4;
            Array.Copy(rawData, 0, fullData, offset, rawData.Length);
            Material.ColorSetData = fullData.ToList();
            UnsavedChanges = true;
        }

        protected override void FreeManaged()
        {
            if (ColorsetRowViewport != null)
            {
                ColorsetRowViewport.Dispose();
                _vm.Dispose();
            }
            CopyUpdated -= OnCopyUpdated;
        }

        private async Task DyeAllRows(int channel)
        {
            try
            {
                var dyeId = (int)DyePreviewIdBox.SelectedValue;

                for (int i = 0; i < _rowCount; i++)
                {
                    ApplyDye(i, dyeId, channel);
                }

                // Reload the UI.
                await SetMaterial(Material, RowId);
            }
            catch (Exception ex)
            {
                this.ShowError("Unknown Error", "An error occurred:\n\n" + ex.Message);
            }
        }
        private void DyeChannel1_Click(object sender, RoutedEventArgs e)
        {
            _ = DyeAllRows(0);
        }

        private void DyeChannel2_Click(object sender, RoutedEventArgs e)
        {
            _ = DyeAllRows(1);
        }

        private void DyeChannel3_Click(object sender, RoutedEventArgs e)
        {
            _ = DyeAllRows(2);
        }

        private void DyeChannel4_Click(object sender, RoutedEventArgs e)
        {
            _ = DyeAllRows(3);
        }

        private void DyeAllChannels_Click(object sender, RoutedEventArgs e)
        {
            _ = DyeAllRows(-1);
        }
    }
}
