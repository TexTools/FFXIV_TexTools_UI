using FFXIV_TexTools.Views.Controls;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Tga;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
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
using xivModdingFramework.Textures;
using xivModdingFramework.Textures.FileTypes;
using xivModdingFramework.Variants.FileTypes;
using Image = SixLabors.ImageSharp.Image;
using SixLabors.ImageSharp;
using Point = SixLabors.ImageSharp.Point;
using xivModdingFramework.Helpers;

namespace FFXIV_TexTools.Views.Textures
{
    /// <summary>
    /// Interaction logic for IndexTextureCreator.xaml
    /// </summary>
    public partial class EyeDiffuseCreator : Window, INotifyPropertyChanged
    {
        private static OpenFileDialog OpenDialog = new OpenFileDialog()
        {
            Filter = "Image Files|*.dds;*.png;*.tga;*.bmp;*.tex",
            Title = "Select Image File",
        };

        private static SaveFileDialog SaveDialog = new SaveFileDialog()
        {
            Filter = "Image Files|*.dds;*.png;*.tga;*.bmp;*.tex",
            Title = "Save Image File",
        };

        private static TgaEncoder Encoder = new TgaEncoder() { 
            BitsPerPixel = TgaBitsPerPixel.Pixel32, 
            Compression = TgaCompression.None 
        };

        public event PropertyChangedEventHandler PropertyChanged;
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



        public EyeDiffuseCreator()
        {
            DataContext = this;
            InitializeComponent();
            Closing += OnClose;
        }

        private void OnClose(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if(Owner != null)
            {
                Owner.Activate();
            }
        }

        public static void ShowWindow(Window owner = null)
        {
            if(owner == null)
            {
                owner = MainWindow.GetMainWindow();
            }

            var wind = new EyeDiffuseCreator()
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
            if (OpenDialog.ShowDialog() != System.Windows.Forms.DialogResult.OK)
            {
                return;
            }

            MaskPath = OpenDialog.FileName;
        }

        private async void Convert_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(MaskPath) || !File.Exists(MaskPath))
            {
                return;
            }

            var dir = Path.GetDirectoryName(MaskPath);
            var fName = Path.GetFileNameWithoutExtension(MaskPath);

            fName += "_diffuse.tga";

            SaveDialog.Title = "Save Eye Texture...";
            SaveDialog.InitialDirectory = dir;
            SaveDialog.FileName = fName;

            if (SaveDialog.ShowDialog() != System.Windows.Forms.DialogResult.OK)
            {
                return;
            }
            var outPath = SaveDialog.FileName;

            ConvertEnabled = false;
            try
            {
                // The Ratio of Iris to Sclera is 92/100 in old textures, but is
                // 1/2.55 roughly in the new.
                // Multiplying these terms together results in a ratio of roughly .44
                double ratio = 0.442;


                var maskData = await Tex.GetPixelDataFromFile(MaskPath);

                // In order to guarantee we're resizing up, not down, and are still a power-of-two, we have to 4x the
                // dimensions of the original mask file, as a 2x would result in some amount of compression.
                var w = maskData.Width * 4;
                var h = maskData.Width * 4;

                var irisW = (int)(w * ratio);
                var irisH = (int)(h * ratio);

                // Pull the base game eye files as our baseline.
                var rTx = MainWindow.DefaultTransaction;
                var baseDiffuseTex = await Tex.GetXivTex("chara/common/texture/eye/eye01_base.tex", true, rTx);
                var frameTex = await Tex.GetXivTex("chara/common/texture/eye/eye01_mask.tex", true, rTx);

                var diffuseData = await baseDiffuseTex.GetRawPixels();
                var frameData = await frameTex.GetRawPixels();

                // Convert mask to greyscale copy of just the red channel data.
                await TextureHelpers.ExpandChannel(maskData.PixelData, 0, maskData.Width, maskData.Height);
                var resizedMask = await TextureHelpers.ResizeImage(maskData.PixelData, maskData.Width, maskData.Height, irisW, irisH);

                // Convert eye frame to just the actual framing information
                await TextureHelpers.ExpandChannel(frameData, 2, frameTex.Width, frameTex.Height, true);


                // Resize and blur the frame slightly.
                using (var frameImage = Image.LoadPixelData<Rgba32>(frameData, frameTex.Width, frameTex.Height))
                {
                    var resizeOptions = new ResizeOptions
                    {
                        Size = new SixLabors.ImageSharp.Size(w, h),
                        PremultiplyAlpha = false,
                        Mode = SixLabors.ImageSharp.Processing.ResizeMode.Stretch,
                        Sampler = SixLabors.ImageSharp.Processing.KnownResamplers.NearestNeighbor,
                    };
                    frameImage.Mutate(x => x.Resize(resizeOptions));

                    // Box-blur the mask just a hair to reduce the harshness at the edges.
                    // This looks a little nicer than just bicubic upscaling the mask.
                    frameImage.Mutate(x => x.BoxBlur(w/128));
                    frameData = IOUtil.GetImageSharpPixels(frameImage);
                    frameImage.SaveAsTga("E:\\img.tga", Encoder);
                }

                var maskPixels = new byte[w * h * 4];

                // Draw the mask onto a new blank canvas and get the byte data back.
                using (var blankImage = Image.LoadPixelData<Rgba32>(maskPixels, w, h))
                {
                    using (var maskImage = Image.LoadPixelData<Rgba32>(resizedMask, irisW, irisH))
                    {
                        var pt = new Point((w / 2) - (irisW / 2), (h / 2) - (irisH / 2));
                        blankImage.Mutate(x => x.DrawImage(maskImage, pt, 1.0f));

                        maskPixels = IOUtil.GetImageSharpPixels(blankImage);

                    }
                }


                // Use the frame to mask the mask.
                await TextureHelpers.MaskImage(maskPixels, frameData, w, h);

                // And finally, resize the diffuse and draw the masked image back in.
                using (var mainImage = Image.LoadPixelData<Rgba32>(diffuseData, baseDiffuseTex.Width, baseDiffuseTex.Height))
                {
                    using (var maskImage = Image.LoadPixelData<Rgba32>(maskPixels, w, h))
                    {
                        maskImage.SaveAsTga("E:\\img2.tga", Encoder);
                        var resizeOptions = new ResizeOptions
                        {
                            Size = new SixLabors.ImageSharp.Size(w, h),
                            PremultiplyAlpha = false,
                            Mode = SixLabors.ImageSharp.Processing.ResizeMode.Stretch,
                            Sampler = SixLabors.ImageSharp.Processing.KnownResamplers.Bicubic,
                        };
                        mainImage.Mutate(x => x.Resize(resizeOptions));

                        var ops = new GraphicsOptions()
                        {
                            AlphaCompositionMode = PixelAlphaCompositionMode.SrcAtop,
                        };
                        mainImage.Mutate(x => x.DrawImage(maskImage, ops));

                        mainImage.SaveAsTga(outPath, Encoder);
                    }
                }
                //var baseDiffuseData = 

                /*
                //await TextureHelpers.CreateIndexTexture(data.PixelData, indexData, data.Width, data.Height);

                */

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
