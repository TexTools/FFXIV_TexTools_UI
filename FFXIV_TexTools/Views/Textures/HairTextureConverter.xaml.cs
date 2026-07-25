using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using xivModdingFramework.Textures.FileTypes;
using xivModdingFramework.Textures;
using SixLabors.ImageSharp.Formats.Tga;
using System.IO;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Formats;

namespace FFXIV_TexTools.Views.Textures
{
    /// <summary>
    /// Interaction logic for HairTextureConverter.xaml
    /// </summary>
    public partial class HairTextureConverter : Window, INotifyPropertyChanged
    {
        public static OpenFileDialog OpenDialog = new OpenFileDialog()
        {
            Filter = "Image Files|*.dds;*.png;*.tga;*.bmp;*.tex",
            Title = "Select Image File",
        };

        public static SaveFileDialog SaveDialog = new SaveFileDialog()
        {
            Filter = ViewHelpers.ConverterImageSaveFilter,
            Title = "Save Image File",
        };

        public static TgaEncoder TgaEncoder = new TgaEncoder()
        {
            BitsPerPixel = TgaBitsPerPixel.Pixel32,
            Compression = TgaCompression.None
        };

        public static PngEncoder PngEncoder = new PngEncoder()
        {
            BitDepth = PngBitDepth.Bit16
        };


        public event PropertyChangedEventHandler PropertyChanged;
        private string _NormalPath;
        public string NormalPath
        {
            get => _NormalPath;
            set
            {
                _NormalPath = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(NormalPath)));
            }
        }


        private string _MaskPath;
        public string MaskPath
        {
            get => _MaskPath;
            set
            {
                _MaskPath = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(MaskPath)));
            }
        }

        private bool _ConvertEnabled = true;
        public bool ConvertEnabled
        {
            get => _ConvertEnabled;
            set
            {
                _ConvertEnabled = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ConvertEnabled)));
            }
        }

        public HairTextureConverter()
        {
            DataContext = this;
            InitializeComponent();
            Closing += OnClose;
        }
        private void OnClose(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (Owner != null)
            {
                Owner.Activate();
            }
        }

        public static void ShowWindow(Window owner = null)
        {
            if (owner == null)
            {
                owner = MainWindow.GetMainWindow();
            }

            var wind = new HairTextureConverter()
            {
                Owner = owner,
            };
            wind.WindowStartupLocation = WindowStartupLocation.CenterOwner;

            wind.Show();
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void SelectNormal_Click(object sender, RoutedEventArgs e)
        {
            OpenDialog.FileName = null;
            if(OpenDialog.ShowDialog() != System.Windows.Forms.DialogResult.OK)
            {
                return;
            }

            NormalPath = OpenDialog.FileName;
        }

        private void SelectMask_Click(object sender, RoutedEventArgs e)
        {
            OpenDialog.FileName = null;
            if (OpenDialog.ShowDialog() != System.Windows.Forms.DialogResult.OK)
            {
                return;
            }

            MaskPath = OpenDialog.FileName;
        }

        private async void Convert_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(NormalPath) || !File.Exists(NormalPath))
            {
                return;
            }
            if (string.IsNullOrWhiteSpace(MaskPath) || !File.Exists(MaskPath))
            {
                return;
            }

            var dir = Path.GetDirectoryName(NormalPath);
            var fName = Path.GetFileNameWithoutExtension(NormalPath);
            fName += "_dtnormal.tga";
            SaveDialog.Title = "Save New Normal Texture...";
            SaveDialog.InitialDirectory = dir;
            SaveDialog.FileName = fName;
            if (SaveDialog.ShowDialog() != System.Windows.Forms.DialogResult.OK)
            {
                return;
            }
            var normalPath = SaveDialog.FileName;

            dir = Path.GetDirectoryName(MaskPath);
            fName = Path.GetFileNameWithoutExtension(MaskPath);
            fName += "_dtmask.tga";
            SaveDialog.Title = "Save New Mask Texture...";
            SaveDialog.InitialDirectory = dir;
            SaveDialog.FileName = fName;
            if (SaveDialog.ShowDialog() != System.Windows.Forms.DialogResult.OK)
            {
                return;
            }
            var maskPath = SaveDialog.FileName;


            ConvertEnabled = false;
            try
            {
                var normData = await Tex.GetPixelDataFromFile(NormalPath);
                var maskData = await Tex.GetPixelDataFromFile(MaskPath);

                var data = await TextureHelpers.ResizeImages(normData.PixelData, normData.Width, normData.Height, maskData.PixelData, maskData.Width, maskData.Height);




                await TextureHelpers.CreateHairMaps(data.TexA, data.TexB, data.Width, data.Height);

                var normRaw = data.TexA;
                var maskRaw = data.TexB;


                using (var image = SixLabors.ImageSharp.Image.LoadPixelData<Rgba32>(normRaw, data.Width, data.Height))
                {
                    if (normalPath.ToLower().EndsWith(".png"))
                    {
                        image.Save(normalPath, PngEncoder);
                    }
                    else
                    {
                        image.Save(normalPath, TgaEncoder);
                    }
                }
                using (var image = SixLabors.ImageSharp.Image.LoadPixelData<Rgba32>(maskRaw, data.Width, data.Height))
                {
                    if (maskPath.ToLower().EndsWith(".png"))
                    {
                        image.Save(maskPath, PngEncoder);
                    }
                    else
                    {
                        image.Save(maskPath, TgaEncoder);
                    }
                }
            }
            catch (Exception ex)
            {
                this.ShowError("Conversion Error", "An error occurred while converting the texture(s):\n\n" + ex.Message);
            }
            finally
            {
                ConvertEnabled = true;
            }



        }
    }
}
