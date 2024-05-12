using FFXIV_TexTools.Helpers;
using FFXIV_TexTools.Resources;
using FFXIV_TexTools.ViewModels;
using FFXIV_TexTools.Views.Controls;
using MahApps.Metro;
using SharpDX;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using xivModdingFramework.Cache;
using xivModdingFramework.Materials.DataContainers;
using xivModdingFramework.Materials.FileTypes;
using Xceed.Wpf.Toolkit;

namespace FFXIV_TexTools.Controls
{
    /// <summary>
    /// Interaction logic for ColorsetEditorControl.xaml
    /// </summary>
    public partial class ColorsetEditorControl : UserControl
    {
        List<ColorsetRowControl> ColorSetRowControls = new List<ColorsetRowControl>();
        StainingTemplateFile DyeTemplateFile;
        int RowId = 0;

        ColorsetEditorViewModel _vm;

        private int _rowCount = 32;
        private int _columnCount = 8;

        XivMtrl _mtrl;

        List<Half[]> RowData;

        private bool _LOADING = true;

        public event EventHandler MaterialSaved;

        ObservableCollection<KeyValuePair<ushort, string>> DyeTemplateCollection = new ObservableCollection<KeyValuePair<ushort, string>>();
        ObservableCollection<KeyValuePair<int, string>> PreviewDyeCollection = new ObservableCollection<KeyValuePair<int, string>>();
        ObservableCollection<KeyValuePair<int, string>> TileMaterialIds = new ObservableCollection<KeyValuePair<int, string>>();
        ObservableCollection<KeyValuePair<uint, string>> DyeChannelCollection = new ObservableCollection<KeyValuePair<uint, string>>();

        private List<CheckBox> DyeBoxes = new List<CheckBox>();

        public ColorsetEditorControl()
        {
            this.DataContext = _vm = new ColorsetEditorViewModel(this);
            InitializeComponent();

            for (int i = 0; i < _rowCount; i++)
            {
                ColorsetRowControl elem;
                if (_rowCount == 32)
                {
                    elem = new ColorsetRowControl(i)
                    {
                        Height = 12,
                        Width = 160
                    };
                } else
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
            }

            DyeTemplateIdBox.ItemsSource = DyeTemplateCollection;
            DyeTemplateIdBox.DisplayMemberPath = "Value";
            DyeTemplateIdBox.SelectedValuePath = "Key";

            // Binding handlers for any time the data is changed in the UI.
            const int _DYE_BITS = 12;
            for(int i = 0; i < _DYE_BITS; i++)
            {
                var st = "DyeBit" + i;
                var box = (CheckBox) FindName(st);
                box.Checked += ValueChanged;
                box.Unchecked += ValueChanged;
                DyeBoxes.Add(box);
            }

            DiffuseColorPicker.SelectedColorChanged += DiffuseColorPicker_SelectedColorChanged; ;
            SpecularColorPicker.SelectedColorChanged += SpecularColorPicker_SelectedColorChanged; ;
            EmissiveColorPicker.SelectedColorChanged += EmissiveColorPicker_SelectedColorChanged; ;

            SpecularPowerBox.TextChanged += ValueChanged;
            GlossBox.TextChanged += ValueChanged;

            TileIdBox.SelectionChanged += ValueChanged;
            TileSkewXBox.TextChanged += ValueChanged;
            TileSkewYBox.TextChanged += ValueChanged;
            TileCountXBox.TextChanged += ValueChanged;
            TileCountYBox.TextChanged += ValueChanged;
            TileOpacityBox.TextChanged += ValueChanged;
            AnisotropyBlendingBox.TextChanged += ValueChanged;
            ShaderTemplateBox.TextChanged += ValueChanged;

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

        }



        private void ValueChanged(object sender, RoutedEventArgs e)
        {
            UpdateRow();
        }


        // Color pickers get their own special handling, because technically the raw values can go over
        // normal 255 byte color ranges, so we need to not stomp them when calling the normal UpdateRow() function.
        private void DiffuseColorPicker_SelectedColorChanged(object sender, RoutedPropertyChangedEventArgs<System.Windows.Media.Color?> e)
        {
            if (_LOADING) return;

            if (DiffuseColorPicker.SelectedColor.Value.A != 255)
            {
                DiffuseColorPicker.SelectedColor = new System.Windows.Media.Color() {
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
            if (_LOADING) return;

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
            if (_LOADING) return;

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

        private void ColorsetRow_Clicked(object sender, MouseButtonEventArgs e)
        {
            var selectedRowControl = (ColorsetRowControl)e.Source;
            SelectedColorsetRowImage.Source = selectedRowControl.RowImageSource;
            var rowNumber = (int)selectedRowControl.DataContext;
            SetRow(rowNumber);
        }

        private List<Half[]> GetRowData(int row)
        {
            var offset = row * _columnCount * 4;
            var data = new List<Half[]>(4);
            for (int i = 0; i < _columnCount; i++)
            {
                var arr = new Half[4];
                data.Add(arr);
                for (int z = 0; z < 4; z++)
                {
                    arr[z] = _mtrl.ColorSetData[offset];
                    offset++;
                }
            }
            return data;
        }

        private Half[] RowDataToRaw(List<Half[]> data)
        {
            var raw = new Half[_columnCount * 4];
            for(int i = 0; i < _columnCount; i++)
            {
                Array.Copy(data[i], 0, raw, i * 4, 4);
            }

            return raw;
        }

        private async Task SetRow(int rowNumber) {

            if (_mtrl == null) return;

            _LOADING = true;

            var dyeLen = _mtrl.ColorSetData.Count == 256 ? 32 : 128;
            if (_mtrl.ColorSetDyeData == null || _mtrl.ColorSetDyeData.Length != dyeLen)
            {
                _mtrl.ColorSetDyeData = new byte[dyeLen];
            }

            RowId = rowNumber;

            // Triggered when the user clicks on a Colorset row.
            DetailsGroupBox.Header = $"Material - Colorset Row Editor - Row #{(rowNumber + 1)._()}".L();
            RowData = GetRowData(RowId);

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



            if (_mtrl.ColorSetData.Count > 256)
            {
                // Dawntrail flipped these two values.
                SpecularPowerBox.Text = RowData[1][3].ToString();
                GlossBox.Text = RowData[0][3].ToString();
            } else
            {
                SpecularPowerBox.Text = RowData[0][3].ToString();
                GlossBox.Text = RowData[1][3].ToString();
            }

            if (_columnCount== 4)
            {
                TileIdBox.SelectedValue = (int)(Math.Floor(RowData[2][3] * 64));
                TileCountXBox.Text = RowData[3][0].ToString();
                TileCountYBox.Text = RowData[3][3].ToString();
                TileSkewXBox.Text = RowData[3][1].ToString();
                TileSkewYBox.Text = RowData[3][2].ToString();
            } else
            {
                TileIdBox.SelectedValue = (int)(Math.Floor(RowData[6][1] * 64));
                TileCountXBox.Text = RowData[7][0].ToString();
                TileCountYBox.Text = RowData[7][3].ToString();
                TileSkewXBox.Text = RowData[7][1].ToString();
                TileSkewYBox.Text = RowData[7][2].ToString();


                ShaderTemplateBox.Text = RowData[6][0].ToString();
                TileOpacityBox.Text = RowData[6][2].ToString();
                AnisotropyBlendingBox.Text = RowData[4][3].ToString();
            }

            


            uint dyeData = 0;
            if (_mtrl.ColorSetDyeData.Length != 0) { 
                if (_mtrl.ColorSetDyeData.Length > (rowNumber * 4) + 4) {
                    dyeData = BitConverter.ToUInt32(_mtrl.ColorSetDyeData, rowNumber * 4);
                }
            }

            if(dyeData == uint.MaxValue)
            {
                dyeData = 0;
            }

            ushort dyeTemplateId = STM.GetTemplateKeyFromMaterialData(_mtrl, RowId);
            DyeTemplateIdBox.SelectedValue = dyeTemplateId;

            for(int i = 0; i < DyeBoxes.Count; i++)
            {
                var shifted = 0x01 << i;
                var active = (dyeData & shifted) > 0;
                DyeBoxes[i].IsChecked = active;
            }

            if (dyeData > 0)
            {
                uint dyeChannel = dyeData << 3 >> 30;
                DyeChannelBox.SelectedValue = dyeChannel;
            } else
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

            _LOADING = false;
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

        private byte ColorHalfToByte(Half half)
        {
            var b = (byte) Math.Round((Math.Sqrt(half) * 255));

            return b;
        }

        private Half ColorByteToHalf(byte b)
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
                var half = _mtrl.ColorSetData[valueOffset];

                // Convert RGBA to BGRA
                byte[] pixel = new byte[4];
                var destinationOffset = x * 4;
                pixels[destinationOffset + 0] = ColorHalfToByte(_mtrl.ColorSetData[valueOffset + 2]);
                pixels[destinationOffset + 1] = ColorHalfToByte(_mtrl.ColorSetData[valueOffset + 1]);
                pixels[destinationOffset + 2] = ColorHalfToByte(_mtrl.ColorSetData[valueOffset + 0]);
                //pixels[destinationOffset + 3] = HalfToByte(_mtrl.ColorSetData[valueOffset + 3]);

                // Turn off Alpha, it looks horrible and confusing.
                pixels[destinationOffset + 3] = 255;
            }

            const int expansionSize = 8;
            var expandedPixels = new byte[pixels.Length * expansionSize * expansionSize];
            for(int z = 0; z < expansionSize; z++)
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
        /// Sets the material and selects a given row (or row 0)
        /// </summary>
        /// <param name="mtrl"></param>
        /// <param name="row"></param>
        /// <returns></returns>
        public async Task SetMaterial(XivMtrl mtrl, int row = 0)
        {
            if (mtrl == null) return;

            _LOADING = true;
            _mtrl = mtrl;

            STM.EStainingTemplate stainingTemplate;
            if (mtrl.ColorSetData.Count == 256)
            {
                stainingTemplate = STM.EStainingTemplate.Endwalker;
                _columnCount = 4;
                _rowCount = 16;
            } else
            {
                stainingTemplate = STM.EStainingTemplate.Dawntrail;
                _columnCount = 8;
                _rowCount = 32;
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
            DiffuseColorPicker.TabForeground= fgBrush;
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

                var dyes = await STM.GetDyeNames();

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


                if(_mtrl.ShaderPack != ShaderHelpers.EShaderPack.CharacterLegacy)
                {
                    // Gloss/Spec Power only work on legacy shaders.
                    GlossBox.Visibility = Visibility.Collapsed;
                    GlossLabel.Visibility = Visibility.Collapsed;
                    SpecularPowerBox.Visibility = Visibility.Collapsed;
                    SpecularPowerLabel.Visibility = Visibility.Collapsed;
                } else
                {
                    GlossBox.Visibility = Visibility.Visible;
                    GlossLabel.Visibility = Visibility.Visible;
                    SpecularPowerBox.Visibility = Visibility.Visible;
                    SpecularPowerLabel.Visibility = Visibility.Visible;
                }

                await _vm.SetMaterial(_mtrl, DyeTemplateFile);
                await SetRow(row);

                for (int i = 0; i < _rowCount; i++)
                {
                    await UpdateRowVisual(i);
                }

            } catch(Exception ex)
            {
                FlexibleMessageBox.Show("Unable to load material into colorset editor.\n\nError: ".L() + ex.Message, "Colorset Editor Error".L(), System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Warning);
            }
            _LOADING = false;
        }

        /// <summary>
        /// Saves the material to file.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            var mw = MainWindow.GetMainWindow();
            await mw.LockUi();
            try
            {

                var mtrlLib = new Mtrl(XivCache.GameInfo.GameDirectory);

                var item = mw.GetSelectedItem();
                await mtrlLib.ImportMtrl(_mtrl, item, XivStrings.TexTools);
                MaterialSaved.Invoke(this, null);
            }
            catch(Exception ex)
            {
                FlexibleMessageBox.Show("Unable to save Material.\n\nError: ".L() + ex.Message, "Material Save Error".L(), System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Error);
                return;
            }
            finally
            {
                await mw.UnlockUi();
            }
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
                for(int i = 0; i < DyeBoxes.Count; i++)
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

            for(int i = 0; i < DyeBoxes.Count; i++)
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
            if (_mtrl == null) return;
            if (_mtrl.ColorSetData.Count == 0) return;
            if (_LOADING) return;

            try
            {
                float fl;
                if (_mtrl.ColorSetData.Count > 256 && _mtrl.ShaderPack == ShaderHelpers.EShaderPack.CharacterLegacy)
                {
                    // Values flipped on Dawntrail Materials.
                    fl = 1.0f;
                    float.TryParse(SpecularPowerBox.Text, out fl);
                    RowData[1][3] = new Half(fl);

                    fl = 1.0f;
                    float.TryParse(GlossBox.Text, out fl);
                    RowData[0][3] = new Half(fl);
                } else if (_mtrl.ColorSetData.Count <= 256)
                {
                    fl = 1.0f;
                    float.TryParse(SpecularPowerBox.Text, out fl);
                    RowData[0][3] = new Half(fl);

                    fl = 1.0f;
                    float.TryParse(GlossBox.Text, out fl);
                    RowData[1][3] = new Half(fl);
                }

                fl = 0.0f;
                float.TryParse(ShaderTemplateBox.Text, out fl);
                RowData[6][0] = new Half(fl);

                RowData[6][1] = new Half((((int)TileIdBox.SelectedValue) + 0.5f) / 64.0f);

                fl = 0.0f;
                float.TryParse(TileOpacityBox.Text, out fl);
                RowData[6][2] = new Half(fl);

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

                fl = 0.0f;
                float.TryParse(AnisotropyBlendingBox.Text, out fl);
                RowData[4][3] = new Half(fl);

                uint modifier = (uint)0;
                if (DyeTemplateIdBox.SelectedValue != null)
                {
                    var v = (ushort) DyeTemplateIdBox.SelectedValue;
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



                var _dyeSize = 2;
                if (_mtrl.ColorSetData.Count > 256)
                {
                    _dyeSize = 4;
                }


                var offset = RowId * _dyeSize;
                var bytes = BitConverter.GetBytes(modifier);

                Array.Copy(bytes, 0, _mtrl.ColorSetDyeData, offset, _dyeSize);

                offset = RowId * _columnCount * 4;
                for(int x = 0; x < _columnCount; x++)
                {
                    for(int y = 0; y < 4; y++)
                    {
                        _mtrl.ColorSetData[offset] = RowData[x][y];
                        offset++;
                    }
                }

                UpdateDyeStatus();
                await UpdateRowVisual(RowId);
                await UpdateViewport();
            }
            catch(Exception ex)
            {
                // No-Op...?
                var z = "z";
            }
        }

        private async void DataChanged(object sender, SelectionChangedEventArgs e)
        {
            await UpdateRow();
        }



        private void RawAssignColorPixel(int col, string name, ColorPicker picker)
        {
            if (!RawEditPixel(col, name, true))
            {
                return;
            }
            _LOADING = true;

            var c = GetDisplayColor(col);
            picker.SelectedColor = new System.Windows.Media.Color() { 
                R = c.r,
                G = c.g, 
                B = c.b, 
                A = 255,
            };
            _LOADING = false;
            UpdateRow();
        }

        private void AssignPixel(int col, float r, float g, float b, float a = float.NaN)
        {
            _LOADING = true;
            RowData[col][0] = r;
            RowData[col][1] = g;
            RowData[col][2] = b;

            if(a != float.NaN)
            {
                RowData[col][3] = a;
            }
            _LOADING = false;
        }
        private (byte r, byte g, byte b) GetDisplayColor(int col)
        {
            var hr = RowData[col][0];
            var hg = RowData[col][1];
            var hb = RowData[col][2];
            

            return (ColorHalfToByte(hr), ColorHalfToByte(hb), ColorHalfToByte(hg));

        }

        private bool RawEditPixel(int col, string title, bool includeAlpha = false)
        {
            if(col >= _columnCount)
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

        List<Half[]> CopiedRow;
        byte[] CopiedRowDye;
        private void CopyRowButton_Click(object sender, RoutedEventArgs e)
        {
            CopiedRow = GetRowData(RowId);

            var dyeSize = 2;
            if(_mtrl.ColorSetData.Count > 256)
            {
                dyeSize = 4;
            }

            CopiedRowDye = new byte[dyeSize];
            Array.Copy(_mtrl.ColorSetDyeData, RowId * dyeSize, CopiedRowDye, 0, dyeSize);

            PasteRowButton.IsEnabled = true;
        }

        private void PasteRowButton_Click(object sender, RoutedEventArgs e)
        {
            if (CopiedRow == null) return;

            // Disable Dye copying for now since that's not set up yet.

            var dyeSize = 2;
            if (_mtrl.ColorSetData.Count > 256)
            {
                dyeSize = 4;
            }
            var offset = RowId * dyeSize;
            Array.Copy(CopiedRowDye, 0, _mtrl.ColorSetDyeData, offset, dyeSize);

            offset = RowId * _columnCount * 4;
            for (int x = 0; x < _columnCount; x++)
            {
                for (int y = 0; y < 4; y++)
                {
                    _mtrl.ColorSetData[offset] = CopiedRow[x][y];
                    offset++;
                }
            }

            UpdateRowVisual(RowId);
            SetRow(RowId);
        }

        private async void MoveRowUpButton_Click(object sender, RoutedEventArgs e)
        {
            if (RowId == 0) return;
            var prevRowId = RowId - 1;
            await SwapRows(RowId, prevRowId);
        }

        private async void MoveRowDownButton_Click(object sender, RoutedEventArgs e)
        {
            if (RowId == 15) return;
            var prevRowId = RowId + 1;
            await SwapRows(RowId, prevRowId);
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

            var arr = _mtrl.ColorSetData.ToArray();
            Array.Copy(myData, 0, arr, otherOffset, 4 * _columnCount);
            Array.Copy(otherData, 0, arr, myOffset, 4 * _columnCount);

            var offset1 = row1 * 2;
            var offset2 = row2 * 2;

            var b1 = _mtrl.ColorSetDyeData[offset1];
            var b2 = _mtrl.ColorSetDyeData[offset1 + 1];

            _mtrl.ColorSetDyeData[offset1] = _mtrl.ColorSetDyeData[offset2];
            _mtrl.ColorSetDyeData[offset1 + 1] = _mtrl.ColorSetDyeData[offset2 + 1];
            _mtrl.ColorSetDyeData[offset2] = b1;
            _mtrl.ColorSetDyeData[offset2 + 1] = b2;

            _mtrl.ColorSetData = arr.ToList();

            await SetMaterial(_mtrl, row2);
        }

        private async void DyePreviewIdBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_mtrl == null) return;

            CopyDyeValuesButton.IsEnabled = false;
            if (DyePreviewIdBox.SelectedValue != null && DyeTemplateIdBox.SelectedValue != null)
            {
                var template = (ushort)DyeTemplateIdBox.SelectedValue;
                var dyeId = (int)DyePreviewIdBox.SelectedValue;
                if (dyeId >= 0 && template > 0)
                {
                    CopyDyeValuesButton.IsEnabled = true;
                }
            }

            if (_LOADING) return;

            await UpdateViewport();
        }

        private async void CopyDyeValuesButton_Click(object sender, RoutedEventArgs e)
        {

            ushort dyeTemplateId = STM.GetTemplateKeyFromMaterialData(_mtrl.ColorSetDyeData, RowId);
            var template = DyeTemplateFile.GetTemplate(dyeTemplateId);
            var dyeId = (int) DyePreviewIdBox.SelectedValue;

            uint dyeSettings = BitConverter.ToUInt32(_mtrl.ColorSetDyeData, RowId * 4);

            var templateType = _mtrl.ShaderPack == ShaderHelpers.EShaderPack.CharacterLegacy ? STM.EStainingTemplate.Endwalker : STM.EStainingTemplate.Dawntrail;

            if (template == null) return;
            if (dyeId < 0 || dyeId >= 128) return;

            for(int i = 0; i < DyeBoxes.Count; i++)
            {
                var shifted = (uint) (0x1 << i);
                if((dyeSettings & shifted) > 0)
                {
                    // Apply this template dye value to the row.
                    var data = template.GetData(i, dyeId);

                    // Have to used our cursed translation table here.
                    var targetOffset = StainingTemplateEntry.TemplateEntryOffsetToColorsetOffset[templateType][i];
                    var destinationPixel = targetOffset / 4;
                    var destinationColorIndex = targetOffset % 4;
                    
                    for(int z = 0; z < data.Length; z++)
                    {
                        RowData[destinationPixel][destinationColorIndex + z] = data[z];
                    }
                }
            }



            // Copy RowData into the main colorset array.
            var rawData = RowDataToRaw(RowData);
            var fullData = _mtrl.ColorSetData.ToArray();
            var offset = RowId * _columnCount * 4;
            Array.Copy(rawData, 0, fullData, offset, rawData.Length);
            _mtrl.ColorSetData = fullData.ToList();

            // Reload the UI.
            await SetMaterial(_mtrl, RowId);
        }
    }
}
