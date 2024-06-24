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
                // Load and conver the image.
                var maskData = await Tex.GetPixelDataFromFile(MaskPath);

                var result = await EndwalkerUpgrade.ConvertEyeMaskToDiffuse(maskData.PixelData, maskData.Width, maskData.Height);


                using (var mainImage = Image.LoadPixelData<Rgba32>(result.PixelData, result.Width, result.Height))
                {
                    mainImage.SaveAsTga(outPath, Encoder);
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
