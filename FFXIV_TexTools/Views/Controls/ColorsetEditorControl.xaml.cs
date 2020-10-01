using FFXIV_TexTools.Helpers;
using FFXIV_TexTools.Resources;
using FFXIV_TexTools.ViewModels;
using FFXIV_TexTools.Views.Controls;
using HelixToolkit.Wpf.SharpDX;
using MahApps.Metro;
using Newtonsoft.Json;
using SharpDX;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using xivModdingFramework.Cache;
using xivModdingFramework.Helpers;
using xivModdingFramework.Materials.DataContainers;
using xivModdingFramework.Materials.FileTypes;

namespace FFXIV_TexTools.Controls
{
    /// <summary>
    /// Interaction logic for ColorsetEditorControl.xaml
    /// </summary>
    public partial class ColorsetEditorControl : UserControl
    {
        List<Image> ColorSetRowImages = new List<Image>();
        StainingTemplateFile DyeTemplateFile;
        int RowId = 0;

        ColorsetEditorViewModel _vm;

        XivMtrl _mtrl;

        List<Half[]> RowData;

        private bool _LOADING = true;

        public event EventHandler MaterialSaved;

        ObservableCollection<KeyValuePair<ushort, string>> DyeTemplateCollection = new ObservableCollection<KeyValuePair<ushort, string>>();
        ObservableCollection<KeyValuePair<int, string>> PreviewDyeCollection = new ObservableCollection<KeyValuePair<int, string>>();
        ObservableCollection<KeyValuePair<int, string>> TileMaterialIds = new ObservableCollection<KeyValuePair<int, string>>();

        public ColorsetEditorControl()
        {
            this.DataContext = _vm = new ColorsetEditorViewModel(this);
            InitializeComponent();

            for (int i = 0; i < 16; i++)
            {
                var elem = new System.Windows.Controls.Image();
                elem.Height = 24;
                elem.Width = 192;
                elem.DataContext = i;
                ColorSetRowImages.Add(elem);

                var border = new Border();
                border.Child = elem;

                border.BorderThickness = new Thickness(0);

                ColorSetRowsPanel.Children.Add(border);


                elem.MouseLeftButtonDown += ColorsetRow_Clicked;
            }

            DyeTemplateIdBox.ItemsSource = DyeTemplateCollection;
            DyeTemplateIdBox.DisplayMemberPath = "Value";
            DyeTemplateIdBox.SelectedValuePath = "Key";

            // Binding handlers for any time the data is changed in the UI.
            DyeDiffuseBox.Checked += ValueChanged;
            DyeDiffuseBox.Unchecked += ValueChanged;
            DyeSpecularBox.Checked += ValueChanged;
            DyeSpecularBox.Unchecked += ValueChanged;
            DyeEmissiveBox.Checked += ValueChanged;
            DyeEmissiveBox.Unchecked += ValueChanged;
            DyeGlossBox.Checked += ValueChanged;
            DyeGlossBox.Unchecked += ValueChanged;
            DyeSpecularPower.Checked += ValueChanged;
            DyeSpecularPower.Unchecked += ValueChanged;
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

        }



        private void ValueChanged(object sender, RoutedEventArgs e)
        {
            UpdateRow();
        }

        // Color pickers get their own special handling, because technically the raw values can go over
        // normal 255 byte color ranges, so we need to not stomp them when calling the normal UpdateRow() function.
        private void DiffuseColorPicker_SelectedColorChanged(object sender, RoutedPropertyChangedEventArgs<System.Windows.Media.Color?> e)
        {
            if(DiffuseColorPicker.SelectedColor.Value.A != 255)
            {
                DiffuseColorPicker.SelectedColor = new System.Windows.Media.Color() {
                    R = DiffuseColorPicker.SelectedColor.Value.R,
                    B = DiffuseColorPicker.SelectedColor.Value.B,
                    G = DiffuseColorPicker.SelectedColor.Value.G,
                    A = 255
                };
                return;
            }

            RowData[0][0] = new Half(DiffuseColorPicker.SelectedColor.Value.R / 255.0f);
            RowData[0][1] = new Half(DiffuseColorPicker.SelectedColor.Value.G / 255.0f);
            RowData[0][2] = new Half(DiffuseColorPicker.SelectedColor.Value.B / 255.0f);
            UpdateRow();
        }
        private void SpecularColorPicker_SelectedColorChanged(object sender, RoutedPropertyChangedEventArgs<System.Windows.Media.Color?> e)
        {
            if (SpecularColorPicker.SelectedColor.Value.A != 255)
            {
                SpecularColorPicker.SelectedColor = new System.Windows.Media.Color()
                {
                    R = SpecularColorPicker.SelectedColor.Value.R,
                    B = SpecularColorPicker.SelectedColor.Value.B,
                    G = SpecularColorPicker.SelectedColor.Value.G,
                    A = 255
                };
                return;
            }

            RowData[1][0] = new Half(SpecularColorPicker.SelectedColor.Value.R / 255.0f);
            RowData[1][1] = new Half(SpecularColorPicker.SelectedColor.Value.G / 255.0f);
            RowData[1][2] = new Half(SpecularColorPicker.SelectedColor.Value.B / 255.0f);
            UpdateRow();
        }
        private void EmissiveColorPicker_SelectedColorChanged(object sender, RoutedPropertyChangedEventArgs<System.Windows.Media.Color?> e)
        {
            if (EmissiveColorPicker.SelectedColor.Value.A != 255)
            {
                EmissiveColorPicker.SelectedColor = new System.Windows.Media.Color()
                {
                    R = EmissiveColorPicker.SelectedColor.Value.R,
                    B = EmissiveColorPicker.SelectedColor.Value.B,
                    G = EmissiveColorPicker.SelectedColor.Value.G,
                    A = 255
                };
                return;
            }

            RowData[2][0] = new Half(EmissiveColorPicker.SelectedColor.Value.R / 255.0f);
            RowData[2][1] = new Half(EmissiveColorPicker.SelectedColor.Value.G / 255.0f);
            RowData[2][2] = new Half(EmissiveColorPicker.SelectedColor.Value.B / 255.0f);
            UpdateRow();
        }

        private void ColorsetRow_Clicked(object sender, MouseButtonEventArgs e)
        {
            var selectedImage = (System.Windows.Controls.Image)e.Source;
            SelectedColorsetRowImage.Source = selectedImage.Source;
            var rowNumber = (int)selectedImage.DataContext;
            SetRow(rowNumber);
        }

        private List<Half[]> GetRowData(int row)
        {
            var offset = row * 16;
            var data = new List<Half[]>(4);
            for (int i = 0; i < 4; i++)
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
            var raw = new Half[16];
            for(int i = 0; i < 4; i++)
            {
                Array.Copy(data[i], 0, raw, i * 4, 4);
            }

            return raw;
        }

        private async Task SetRow(int rowNumber) {

            if (_mtrl == null) return;

            _LOADING = true;

            if (_mtrl.ColorSetDyeData == null || _mtrl.ColorSetDyeData.Length != 32)
            {
                _mtrl.ColorSetDyeData = new byte[32];
            }

            RowId = rowNumber;

            // Triggered when the user clicks on a Colorset row.
            DetailsGroupBox.Header = "Material - Colorset Row Editor - Row #" + (rowNumber + 1).ToString();
            RowData = GetRowData(RowId);

            var r = (byte)Math.Round(RowData[0][0] * 255);
            var g = (byte)Math.Round(RowData[0][1] * 255);
            var b = (byte)Math.Round(RowData[0][2] * 255);
            DiffuseColorPicker.SelectedColor = new System.Windows.Media.Color() { R = r, G = g, B = b, A = 255 };

            r = (byte)Math.Round(RowData[1][0] * 255);
            g = (byte)Math.Round(RowData[1][1] * 255);
            b = (byte)Math.Round(RowData[1][2] * 255);
            SpecularColorPicker.SelectedColor = new System.Windows.Media.Color() { R = r, G = g, B = b, A = 255 };

            r = (byte)Math.Round(RowData[2][0] * 255);
            g = (byte)Math.Round(RowData[2][1] * 255);
            b = (byte)Math.Round(RowData[2][2] * 255);
            EmissiveColorPicker.SelectedColor = new System.Windows.Media.Color() { R = r, G = g, B = b, A = 255 };

            SpecularPowerBox.Text = RowData[0][3].ToString();
            GlossBox.Text = RowData[1][3].ToString();

            TileIdBox.SelectedValue = (int) (Math.Floor(RowData[2][3] * 64));

            TileCountXBox.Text = RowData[3][0].ToString();
            TileCountYBox.Text = RowData[3][3].ToString();
            TileSkewXBox.Text = RowData[3][1].ToString();
            TileSkewYBox.Text = RowData[3][2].ToString();

            ushort dyeData = 0;
            if (_mtrl.ColorSetDyeData.Length != 0) {
                dyeData = BitConverter.ToUInt16(_mtrl.ColorSetDyeData, rowNumber * 2);
            }

            if(dyeData == ushort.MaxValue)
            {
                dyeData = 0;
            }

            ushort dyeTemplateId = (ushort)(dyeData >> 5);
            DyeTemplateIdBox.SelectedValue = dyeTemplateId;

            DyeDiffuseBox.IsChecked = (dyeData & 0x01) > 0;
            DyeSpecularBox.IsChecked = (dyeData & 0x02) > 0;
            DyeEmissiveBox.IsChecked = (dyeData & 0x04) > 0;
            DyeGlossBox.IsChecked = (dyeData & 0x08) > 0;
            DyeSpecularPower.IsChecked = (dyeData & 0x10) > 0;

            foreach (var imgElem in ColorSetRowImages)
            {

                var border = (Border)imgElem.Parent;
                border.BorderThickness = new Thickness(0);
                border.BorderBrush = Brushes.Transparent;
            }

            var elem = (Border)ColorSetRowImages[RowId].Parent;
            elem.BorderThickness = new Thickness(2);
            elem.BorderBrush = Brushes.Black;

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
            
            await _vm.SetColorsetRow(RowId, dyeId);
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
            var pixels = new byte[4 * 4];
            for (int x = 0; x < 4; x++)
            {
                for (int y = 0; y < 4; y++)
                {
                    var offset = (y + (x * 4) + (rowId * 16));
                    var half = _mtrl.ColorSetData[offset];
                    var b = half * 255;
                    b = b > 255 ? 255 : b;
                    b = b < 0 ? 0 : b;

                    var bitoffset = x * 4;
                    if (y == 0)
                    {
                        bitoffset += 2;
                    }
                    else if (y == 1)
                    {
                        bitoffset += 1;
                    }
                    else if (y == 2)
                    {
                        bitoffset += 0;
                    }
                    else
                    {
                        bitoffset += 3;
                    }
                    pixels[bitoffset] = (byte)b;
                }
            }

            var multiplier = 8;
            var perRow = 4 * multiplier;

            var npixels = new byte[pixels.Length * multiplier * multiplier];
            for (int x = 0; x < npixels.Length; x += 4)
            {
                var px = x / 4;
                var col = px % perRow;
                var originalCol = col / multiplier;

                var originalOffset = (originalCol * 4);

                npixels[x] = pixels[originalOffset];
                npixels[x + 1] = pixels[originalOffset + 1];
                npixels[x + 2] = pixels[originalOffset + 2];
                npixels[x + 3] = pixels[originalOffset + 3];
            }

            ColorSetRowImages[rowId].Source = BitmapSource.Create(multiplier * 4, multiplier, 1, 1, PixelFormats.Bgra32, null, npixels, 16 * multiplier);

            if(RowId == rowId)
            {
                SelectedColorsetRowImage.Source = ColorSetRowImages[rowId].Source;
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
                DyeTemplateFile = await STM.GetStainingTemplateFile(false);
                DyeTemplateCollection.Clear();

                DyePreviewIdBox.SelectedValue = -1;

                var keys = DyeTemplateFile.GetKeys();
                DyeTemplateCollection.Add(new KeyValuePair<ushort, string>(0, "Undyable"));
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
                PreviewDyeCollection.Add(new KeyValuePair<int, string>(-1, "Undyed"));
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

                _mtrl = mtrl;
                await _vm.SetMaterial(_mtrl, DyeTemplateFile);
                await SetRow(row);

                for (int i = 0; i < 16; i++)
                {
                    await UpdateRowVisual(i);
                }

            } catch(Exception ex)
            {
                FlexibleMessageBox.Show("Unable to load material into colorset editor.\n\nError: " + ex.Message, "Colorset Editor Error", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Warning);
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
                FlexibleMessageBox.Show("Unable to save Material.\n\nError: " + ex.Message, "Material Save Error", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Error);
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

                DyeDiffuseBox.IsChecked = false;
                DyeDiffuseBox.IsEnabled = false;
                DyeSpecularBox.IsChecked = false;
                DyeSpecularBox.IsEnabled = false;
                DyeEmissiveBox.IsChecked = false;
                DyeEmissiveBox.IsEnabled = false;
                DyeSpecularPower.IsChecked = false;
                DyeSpecularPower.IsEnabled = false;
                DyeGlossBox.IsChecked = false;
                DyeGlossBox.IsEnabled = false;
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

            if (entry.DiffuseEntries.Count == 0)
            {
                DyeDiffuseBox.IsChecked = false;
                DyeDiffuseBox.IsEnabled = false;
            } else
            {
                //DyeDiffuseBox.IsChecked = true;
                DyeDiffuseBox.IsEnabled = true;
            }

            if (entry.SpecularEntries.Count == 0)
            {
                DyeSpecularBox.IsChecked = false;
                DyeSpecularBox.IsEnabled = false;
            }
            else
            {
                //DyeSpecularBox.IsChecked = true;
                DyeSpecularBox.IsEnabled = true;
            }

            if (entry.EmissiveEntries.Count == 0)
            {
                DyeEmissiveBox.IsChecked = false;
                DyeEmissiveBox.IsEnabled = false;
            }
            else
            {
                //DyeEmissiveBox.IsChecked = true;
                DyeEmissiveBox.IsEnabled = true;
            }

            if (entry.SpecularPowerEntries.Count == 0)
            {
                DyeSpecularPower.IsChecked = false;
                DyeSpecularPower.IsEnabled = false;
            }
            else
            {
                //DyeTileBox.IsChecked = true;
                DyeSpecularPower.IsEnabled = true;
            }

            if (entry.GlossEntries.Count == 0)
            {
                DyeGlossBox.IsChecked = false;
                DyeGlossBox.IsEnabled = false;
            }
            else
            {
                //DyeGlossBox.IsChecked = true;
                DyeGlossBox.IsEnabled = true;
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
                var fl = 1.0f;
                float.TryParse(SpecularPowerBox.Text, out fl);
                RowData[0][3] = new Half(fl);

                fl = 1.0f;
                float.TryParse(GlossBox.Text, out fl);
                RowData[1][3] = new Half(fl);

                fl = 0.0f;
                RowData[2][3] = new Half((((int)TileIdBox.SelectedValue) + 0.5f) / 64.0f);

                fl = 16.0f;
                float.TryParse(TileCountXBox.Text, out fl);
                RowData[3][0] = new Half(fl);

                fl = 16.0f;
                float.TryParse(TileCountYBox.Text, out fl);
                RowData[3][3] = new Half(fl);

                fl = 0f;
                float.TryParse(TileSkewXBox.Text, out fl);
                RowData[3][1] = new Half(fl);

                fl = 0f;
                float.TryParse(TileSkewYBox.Text, out fl);
                RowData[3][2] = new Half(fl);

                var templateId = 0;
                if (DyeTemplateIdBox.SelectedValue != null)
                {
                    templateId = (ushort)DyeTemplateIdBox.SelectedValue;
                }
                ushort modifier = (ushort)(templateId << 5);

                if (DyeDiffuseBox.IsChecked == true)
                {
                    modifier = (ushort)(modifier | 0x01);
                }
                if (DyeSpecularBox.IsChecked == true)
                {
                    modifier = (ushort)(modifier | 0x02);
                }
                if (DyeEmissiveBox.IsChecked == true)
                {
                    modifier = (ushort)(modifier | 0x04);
                }
                if (DyeGlossBox.IsChecked == true)
                {
                    modifier = (ushort)(modifier | 0x08);
                }
                if (DyeSpecularPower.IsChecked == true)
                {
                    modifier = (ushort)(modifier | 0x10);
                }

                if (_mtrl.ColorSetDyeData.Length != 32)
                {
                    _mtrl.ColorSetDyeData = new byte[32];
                }

                var offset = RowId * 2;

                var bytes = BitConverter.GetBytes(modifier);

                Array.Copy(bytes, 0, _mtrl.ColorSetDyeData, offset, 2);

                offset = RowId * 16;
                for(int x = 0; x < 4; x++)
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

        private async void DyeTemplateIdBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            await UpdateRow();
        }

        private void EditRawDiffuse_Click(object sender, RoutedEventArgs e)
        {
            var result = RawFloatValueDisplay.ShowEditor(RowData[0][0], RowData[0][1], RowData[0][2], "Diffuse");
            if (float.IsNaN(result.Red)) return;


            byte byteRed = (byte)((result.Red < 0 ? 0 : result.Red > 1 ? 1 : result.Red) * 255);
            byte byteGreen = (byte)((result.Green < 0 ? 0 : result.Green > 1 ? 1 : result.Green) * 255);
            byte byteBlue = (byte)((result.Blue < 0 ? 0 : result.Blue > 1 ? 1 : result.Blue) * 255);

            DiffuseColorPicker.SelectedColor = new System.Windows.Media.Color() { R = byteRed, G = byteGreen, B = byteBlue, A = 255 };

            RowData[0][0] = result.Red;
            RowData[0][1] = result.Green;
            RowData[0][2] = result.Blue;
        }

        private void EditRawSpecular_Click(object sender, RoutedEventArgs e)
        {
            var result = RawFloatValueDisplay.ShowEditor(RowData[1][0], RowData[1][1], RowData[1][2], "Specular");
            if (float.IsNaN(result.Red)) return;


            byte byteRed = (byte)((result.Red < 0 ? 0 : result.Red > 1 ? 1 : result.Red) * 255);
            byte byteGreen = (byte)((result.Green < 0 ? 0 : result.Green > 1 ? 1 : result.Green) * 255);
            byte byteBlue = (byte)((result.Blue < 0 ? 0 : result.Blue > 1 ? 1 : result.Blue) * 255);

            SpecularColorPicker.SelectedColor = new System.Windows.Media.Color() { R = byteRed, G = byteGreen, B = byteBlue, A = 255 };

            RowData[1][0] = result.Red;
            RowData[1][1] = result.Green;
            RowData[1][2] = result.Blue;
        }
        private void EditRawEmmissive_Click(object sender, RoutedEventArgs e)
        {
            var result = RawFloatValueDisplay.ShowEditor(RowData[2][0], RowData[2][1], RowData[2][2], "Emissive");
            if (float.IsNaN(result.Red)) return;


            byte byteRed = (byte)((result.Red < 0 ? 0 : result.Red > 1 ? 1 : result.Red) * 255);
            byte byteGreen = (byte)((result.Green < 0 ? 0 : result.Green > 1 ? 1 : result.Green) * 255);
            byte byteBlue = (byte)((result.Blue < 0 ? 0 : result.Blue > 1 ? 1 : result.Blue) * 255);

            EmissiveColorPicker.SelectedColor = new System.Windows.Media.Color() { R = byteRed, G = byteGreen, B = byteBlue, A = 255 };

            RowData[2][0] = result.Red;
            RowData[2][1] = result.Green;
            RowData[2][2] = result.Blue;
        }

        List<Half[]> CopiedRow;
        byte[] CopiedRowDye;
        private void CopyRowButton_Click(object sender, RoutedEventArgs e)
        {
            CopiedRow = GetRowData(RowId);
            CopiedRowDye = new byte[2];
            Array.Copy(_mtrl.ColorSetDyeData, RowId * 2, CopiedRowDye, 0, 2);

            PasteRowButton.IsEnabled = true;
        }

        private void PasteRowButton_Click(object sender, RoutedEventArgs e)
        {
            if (CopiedRow == null) return;

            var offset = RowId * 2;
            Array.Copy(CopiedRowDye, 0, _mtrl.ColorSetDyeData, offset, 2);

            offset = RowId * 16;
            for (int x = 0; x < 4; x++)
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

            var myData = new Half[16];
            var otherData = new Half[16];
            for (int i = 0; i < 4; i++)
            {
                Array.Copy(myRowData[i], 0, myData, i * 4, 4);
                Array.Copy(otherRowData[i], 0, otherData, i * 4, 4);
            }

            var myOffset = row1 * 16;
            var otherOffset = row2 * 16;

            var arr = _mtrl.ColorSetData.ToArray();
            Array.Copy(myData, 0, arr, otherOffset, 16);
            Array.Copy(otherData, 0, arr, myOffset, 16);

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

            var offset = RowId * 2;
            ushort dyeInfo = BitConverter.ToUInt16(_mtrl.ColorSetDyeData, offset);
            ushort dyeTemplateId = (ushort)(dyeInfo >> 5);

            var flags = dyeInfo & 0x1F;

            bool useDiffuse = (flags & 0x01) > 0;
            bool useSpec = (flags & 0x02) > 0;
            bool useEmissive = (flags & 0x04) > 0;
            bool useGloss = (flags & 0x08) > 0;
            bool useSpecPower = (flags & 0x10) > 0;

            var template = DyeTemplateFile.GetTemplate(dyeTemplateId);
            var dyeId = (int) DyePreviewIdBox.SelectedValue;

            if (template == null) return;
            if (dyeId < 0 || dyeId >= 128) return;

            if (useDiffuse && template.DiffuseEntries.Count > 0)
            {
                var diffuse = template.DiffuseEntries[dyeId];
                RowData[0][0] = diffuse[0];
                RowData[0][1] = diffuse[1];
                RowData[0][2] = diffuse[2];
            }
            
            if (useSpec && template.SpecularEntries.Count > 0)
            {
                var value = template.SpecularEntries[dyeId];
                RowData[1][0] = value[0];
                RowData[1][1] = value[1];
                RowData[1][2] = value[2];
            }
            
            if (useEmissive && template.EmissiveEntries.Count > 0)
            {
                var value = template.EmissiveEntries[dyeId];
                RowData[2][0] = value[0];
                RowData[2][1] = value[1];
                RowData[2][2] = value[2];
            }
            
            if (useGloss && template.GlossEntries.Count > 0)
            {
                var value = template.GlossEntries[dyeId];
                RowData[1][3] = value;
            }

            if (useSpecPower && template.SpecularPowerEntries.Count > 0)
            {
                var value = template.SpecularPowerEntries[dyeId];
                RowData[0][3] = value;
            }

            var rawData = RowDataToRaw(RowData);
            offset = RowId * 16;

            var fullData = _mtrl.ColorSetData.ToArray();

            Array.Copy(rawData, 0, fullData, offset, rawData.Length);

            _mtrl.ColorSetData = fullData.ToList();
            await SetMaterial(_mtrl, RowId);
        }
    }
}
