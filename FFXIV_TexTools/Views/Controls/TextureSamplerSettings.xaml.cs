
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using static xivModdingFramework.Materials.DataContainers.ShaderHelpers;
using xivModdingFramework.Materials.DataContainers;
using System.Windows.Forms;
using System.ComponentModel;
using static xivModdingFramework.Materials.DataContainers.TextureSampler;
using SharpDX;
using xivModdingFramework.Textures.FileTypes;
using System.Text.RegularExpressions;

namespace FFXIV_TexTools.Views.Controls
{
    /// <summary>
    /// Interaction logic for RawFloatValueDisplay.xaml
    /// </summary>
    public partial class TextureSamplerSettings : Window, INotifyPropertyChanged
    {
        private MtrlTexture Texture;

        public event PropertyChangedEventHandler PropertyChanged;

        public string TexturePath
        {
            get
            {
                var tokenizedPath = _Material.TokenizePath(Texture.TexturePath, Texture.Usage);
                return tokenizedPath;
            }
            set
            {
                var detokenized = _Material.DetokenizePath(value, Texture);
                Texture.TexturePath = detokenized;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(TexturePath)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DetokenizedPath)));
            }
        }
        public string DetokenizedPath
        {
            get
            {
                return Texture.TexturePath;
            }
            set
            {
                return;
            }
        }
        public float LoDBias
        {
            get
            {
                return Texture.Sampler.LoDBias;
            }
            set
            {
                Texture.Sampler.LoDBias = value;
                if (!_Updating)
                {
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(LoDBias)));
                }
            }
        }
        public ETilingMode UTilingMode
        {
            get
            {
                return Texture.Sampler.UTilingMode;
            }
            set
            {
                Texture.Sampler.UTilingMode = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(UTilingMode)));
            }
        }
        public ETilingMode VTilingMode
        {
            get
            {
                return Texture.Sampler.VTilingMode;
            }
            set
            {
                Texture.Sampler.VTilingMode = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(VTilingMode)));
            }
        }
        public ESamplerId SamplerType
        {
            get
            {
                return Texture.Sampler.SamplerId;
            }
            set
            {
                Texture.Sampler.SamplerId = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SamplerType)));
            }
        }
        public long SamplerSettingsRaw
        {
            get
            {
                return Texture.Sampler.SamplerSettingsRaw;
            }
            set
            {
                Texture.Sampler.SamplerSettingsRaw = (uint)value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SamplerSettingsRaw)));
            }
        }
        public byte UnknownLow
        {
            get
            {
                return Texture.Sampler.SamplerSettingsLowUnknown;
            }
            set
            {
                Texture.Sampler.SamplerSettingsLowUnknown = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(UnknownLow)));
            }
        }

        public long TextureFlagsRaw
        {
            get
            {
                return Texture.Flags;
            }
            set
            {
                Texture.Flags = (ushort)value;
                UpdateCheckBoxes();
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(TextureFlagsRaw)));
            }
        }

        public uint MinimumLoD
        {
            get
            {
                return Texture.Sampler.MinimumLoDLevel;
            }
            set
            {
                Texture.Sampler.MinimumLoDLevel = (byte) value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(MinimumLoD)));
            }
        }

        ObservableCollection<KeyValuePair<string, ETilingMode>> TilingSource { get; set; }
        ObservableCollection<KeyValuePair<string, ESamplerId>> SamplerSource { get; set; }
        ObservableCollection<KeyValuePair<string, uint>> MinimumLoDSource { get; set; }

        private XivMtrl _Material;
        private MtrlTexture _OriginalTexture;
        public TextureSamplerSettings(XivMtrl mtrl, MtrlTexture tex)
        {
            if(mtrl == null || !mtrl.Textures.Contains(tex))
            {
                throw new Exception("Material does not contain referenced texture.");
            }

            // Configure the underlying data first, then initialize.
            Texture = (MtrlTexture)tex.Clone();
            _Material = mtrl;
            _OriginalTexture = tex;
            DataContext = this;
            InitializeComponent();


            TilingSource = new ObservableCollection<KeyValuePair<string, ETilingMode>>();
            var values = Enum.GetValues(typeof(ETilingMode));
            foreach (ETilingMode v in values)
            {
                var kv = new KeyValuePair<string, ETilingMode>(v.ToString(), v);
                TilingSource.Add(kv);
            }
            UTilingBox.ItemsSource = TilingSource;
            VTilingBox.ItemsSource = TilingSource;

            SamplerSource = new ObservableCollection<KeyValuePair<string, ESamplerId>>();
            values = Enum.GetValues(typeof(ESamplerId));
            foreach (ESamplerId v in values)
            {
                var kv = new KeyValuePair<string, ESamplerId>(v.ToString(), v);
                SamplerSource.Add(kv);
            }
            SamplerBox.ItemsSource = SamplerSource;

            MinimumLoDSource = new ObservableCollection<KeyValuePair<string, uint>>();
            for(uint i =0; i <16; i++)
            {
                var kv = new KeyValuePair<string, uint>(i.ToString(), i);
                MinimumLoDSource.Add(kv);
            }
            MinLoDBox.ItemsSource = MinimumLoDSource;


            // UI config has to go after Initialize.
            LoDBiasSlider.Minimum = -8.0f;
            LoDBiasSlider.Maximum = 7.984375f;
            LoDBiasSlider.Value = LoDBias;


            BitControl0.SetNames(new List<string>()
            {
                "Bit 0",
                "Bit 1",
                "Bit 2",
                "Bit 3",
                "Bit 4",
                "Bit 5",
                "Bit 6",
                "Bit 7",
            });
            BitControl1.SetNames(new List<string>()
            {
                "Bit 8",
                "Bit 9",
                "Bit 10",
                "Bit 11",
                "Bit 12",
                "Bit 13",
                "Bit 14",
                "DX9 Textures",
            });
            BitControl1.SetTooltips(new List<string>()
            {
                "",
                "",
                "",
                "",
                "",
                "",
                "",
                "Determines if this material has both DX9 and DX11 textures.",
            });
            BitControl0.ByteChanged += ByteChanged;
            BitControl1.ByteChanged += ByteChanged;
            UpdateCheckBoxes();

            // Hooks get attached last to avoid any unwanted triggers.
            LoDBiasSlider.ValueChanged += LoDBiasSlider_ValueChanged;
            PropertyChanged += TextureSamplerSettings_PropertyChanged;

        }

        private void ByteChanged(object sender, byte e)
        {
            if (_Updating)
            {
                return;
            }
            UpdateTexByteValues();
        }
        private void UpdateCheckBoxes()
        {
            _Updating = true;
            BitControl0.DisplayByte = (byte)Texture.Flags;
            BitControl1.DisplayByte = (byte)(Texture.Flags >> 8);
            _Updating = false;
        }


        private void UpdateTexByteValues()
        {
            Texture.Flags = (ushort)((BitControl1.DisplayByte << 8) | BitControl0.DisplayByte);
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(TextureFlagsRaw)));
        }

        private void LoDBiasSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            LoDBias = (float) LoDBiasSlider.Value;
        }

        private bool _Updating;
        private void TextureSamplerSettings_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (_Updating) return;

            if (e.PropertyName == nameof(LoDBias))
            {
                // Clunky hooking here b/c we have multiple elements displaying the same data.
                _Updating = true;
                LoDBiasSlider.Value = (float)LoDBias;
                _Updating = false;
            }


            // Clunky hooking here b/c we have multiple elements displaying the same data.
            if (e.PropertyName == nameof(LoDBias)
                || e.PropertyName == nameof(UTilingMode)
                || e.PropertyName == nameof(VTilingMode)
                || e.PropertyName == nameof(MinimumLoD)
                || e.PropertyName == nameof(UnknownLow))
            {
                _Updating = true;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SamplerSettingsRaw)));
                _Updating = false;
            } else if(e.PropertyName == nameof(SamplerSettingsRaw))
            {
                _Updating = true;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(LoDBias)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(UTilingMode)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(VTilingMode)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(MinimumLoD)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(UnknownLow)));
                _Updating = false;
            }
        }

        private void Confirm_Click(object sender, RoutedEventArgs e)
        {
            // Replace the original texture with our edited one.
            var idx = _Material.Textures.IndexOf(_OriginalTexture);
            _Material.Textures.RemoveAt(idx);
            _Material.Textures.Insert(idx, Texture);

            DialogResult = true;
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }

        public static bool ShowSamplerSettings(XivMtrl mtrl, MtrlTexture tex, Window owner = null)
        {
            var wind = new TextureSamplerSettings(mtrl, tex);
            wind.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            wind.Owner = owner != null ? owner : System.Windows.Application.Current.MainWindow;
            var result = wind.ShowDialog();
            if(result != true)
            {
                return false;
            }

            return true;
        }

        private void HexInput(object sender, System.Windows.Input.TextCompositionEventArgs e)
        {
            Regex regex = new Regex("[^0-9a-f]+", RegexOptions.IgnoreCase);
            e.Handled = regex.IsMatch(e.Text);
        }

        private void SharedPath_Click(object sender, RoutedEventArgs e)
        {

            var path = _Material.GetTextureRootDirectoy() + "/" + _Material.GetDefaultTexureName(_Material.ResolveFullUsage(Texture), false);
            TexturePath = path;
        }

        private void UniquePath_Click(object sender, RoutedEventArgs e)
        {
            var path = _Material.GetTextureRootDirectoy() + "/" + _Material.GetDefaultTexureName(_Material.ResolveFullUsage(Texture), true);
            TexturePath = path;
        }
    }
}
