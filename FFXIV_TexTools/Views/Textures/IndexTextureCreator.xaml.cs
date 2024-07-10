using FFXIV_TexTools.Views.Controls;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Formats.Tga;
using SixLabors.ImageSharp.PixelFormats;
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

namespace FFXIV_TexTools.Views.Textures
{
    /// <summary>
    /// Interaction logic for IndexTextureCreator.xaml
    /// </summary>
    public partial class IndexTextureCreator : Window, INotifyPropertyChanged
    {
        private static OpenFileDialog OpenDialog = new OpenFileDialog()
        {
            Filter = "Image Files|*.dds;*.png;*.tga;*.bmp;*.tex",
            Title = "Select Image File",
        };

        private static SaveFileDialog SaveDialog = new SaveFileDialog()
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



        public IndexTextureCreator()
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

            var wind = new IndexTextureCreator()
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

            NormalPath = OpenDialog.FileName;
        }

        private async void Convert_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(NormalPath) || !File.Exists(NormalPath))
            {
                return;
            }

            var dir = Path.GetDirectoryName(NormalPath);
            var fName = Path.GetFileNameWithoutExtension(NormalPath);

            fName += "_index.tga";

            SaveDialog.Title = "Save Index Texture...";
            SaveDialog.InitialDirectory = dir;
            SaveDialog.FileName = fName;

            if (SaveDialog.ShowDialog() != System.Windows.Forms.DialogResult.OK)
            {
                return;
            }
            var indexPath = SaveDialog.FileName;

            ConvertEnabled = false;
            try
            {
                var data = await Tex.GetPixelDataFromFile(NormalPath);

                var indexData = new byte[data.PixelData.Length];

                await TextureHelpers.CreateIndexTexture(data.PixelData, indexData, data.Width, data.Height);

                using (var image = SixLabors.ImageSharp.Image.LoadPixelData<Rgba32>(indexData, data.Width, data.Height))
                {
                    if (indexPath.ToLower().EndsWith(".png"))
                    {
                        image.Save(indexPath, PngEncoder);
                    }
                    else
                    {
                        image.Save(indexPath, TgaEncoder);
                    }
                }

            } catch(Exception ex)
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
