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
using static FFXIV_TexTools.ViewModels.TextureViewModel;
using xivModdingFramework.Items;
using Image = SixLabors.ImageSharp.Image;

namespace FFXIV_TexTools.Views.Controls
{
    /// <summary>
    /// Interaction logic for TextureFileControl.xaml
    /// </summary>
    public partial class TextureFileControl : FileViewControl, INotifyPropertyChanged
    {
        public TextureFileControl()
        {
            DataContext = this;
            InitializeComponent();
            ImageZoombox.Loaded += ImageZoombox_Loaded;
            SizeChanged += TextureFileControl_SizeChanged;

            PropertyChanged += TextureFileControl_PropertyChanged;
            ViewType = EFileViewType.Editor;
        }


        private XivTex _Texture = null;
        public XivTex Texture
        {
            get => _Texture;
            set
            {
                _Texture = value;
                OnPropertyChanged(nameof(Texture));
            }
        }

        private BitmapSource _ImageSource = null;
        public BitmapSource ImageSource
        {
            get => _ImageSource;
            set
            {
                _ImageSource = value;
                OnPropertyChanged(nameof(ImageSource));
            }
        }

        private ColorChannels _ImageEffect = null;
        public ColorChannels ImageEffect
        {
            get => _ImageEffect;
            set
            {
                _ImageEffect = value;
                OnPropertyChanged(nameof(ImageEffect));
            }
        }


        protected override async Task<byte[]> INTERNAL_GetUncompressedData()
        {
            return Texture.ToUncompressedTex();
        }

        protected override async Task<bool> INTERNAL_LoadFile(byte[] uncompressedData)
        {
            Texture = XivTex.FromUncompressedTex(uncompressedData);

            await UpdateDisplayImage();

            _ = LoadParentFileInformation(InternalFilePath, ReferenceItem);
            CenterImage();
            return true;
        }

        protected override async Task<bool> INTERNAL_SaveAs(string externalFilePath)
        {
            var ext = Path.GetExtension(externalFilePath).ToLower();

            if(ext == ".tex")
            {
                return await SaveAsRaw(externalFilePath);
            }
            else if(ext == ".dds")
            {
                Tex.SaveTexAsDDS(externalFilePath, Texture);
                return true;
            } 

            IImageEncoder encoder;
            if (ext == ".bmp")
            {
                encoder = new BmpEncoder()
                {
                    SupportTransparency = true,
                    BitsPerPixel = BmpBitsPerPixel.Pixel32
                };
            }
            else
            {
                encoder = new PngEncoder()
                {
                    BitDepth = PngBitDepth.Bit16
                };
            };

            var pixData = await Texture.GetRawPixels(-1);
            using (var img = Image.LoadPixelData<Rgba32>(pixData, Texture.Width, Texture.Height))
            {
                img.Save(externalFilePath, encoder);
            }
            return true;
        }



        #region UI Display Properties / UI Fluff
        private static void SwapRedBlue(byte[] imageData)
        {
            for (int i = 0; i < imageData.Length; i += 4)
            {
                byte x = imageData[i];
                byte y = imageData[i + 2];
                imageData[i] = y;
                imageData[i + 2] = x;
            }
        }

        private static void MultiplyAlpha(byte[] imageData)
        {
            for (int i = 0; i < imageData.Length; i += 4)
            {
                byte a = imageData[i + 3];
                imageData[i] = (byte)(imageData[i] * a / 256);
                imageData[i + 1] = (byte)(imageData[i + 1] * a / 256);
                imageData[i + 2] = (byte)(imageData[i + 2] * a / 256);
            }
        }

        private string _TextureFormat;
        public string TextureFormat
        {
            get => _TextureFormat;
            set
            {
                _TextureFormat = value;
                OnPropertyChanged(nameof(TextureFormat));
            }
        }

        private string _TextureDimensions;
        public string TextureDimensions
        {
            get => _TextureDimensions;
            set
            {
                _TextureDimensions = value;
                OnPropertyChanged(nameof(TextureDimensions));
            }
        }

        private string _MipMapInfo;
        public string MipMapInfo
        {
            get => _MipMapInfo;
            set
            {
                _MipMapInfo = "MipMaps: " + value;
                OnPropertyChanged(nameof(MipMapInfo));
            }
        }

        private bool _channelsEnabled = true;
        public bool ChannelsEnabled
        {
            get => _channelsEnabled;
            set
            {
                _channelsEnabled = value;
                OnPropertyChanged(nameof(ChannelsEnabled));
            }
        }

        private bool _RedChecked = true;
        public bool RedChecked
        {
            get => _RedChecked;
            set
            {
                _RedChecked = value;
                OnPropertyChanged(nameof(RedChecked));
            }
        }

        private bool _BlueChecked = true;
        public bool BlueChecked
        {
            get => _BlueChecked;
            set
            {
                _BlueChecked = value;
                OnPropertyChanged(nameof(BlueChecked));
            }
        }

        private bool _GreenChecked = true;
        public bool GreenChecked
        {
            get => _GreenChecked;
            set
            {
                _GreenChecked = value;
                OnPropertyChanged(nameof(GreenChecked));
            }
        }

        private bool _AlphaChecked = false;
        public bool AlphaChecked
        {
            get => _AlphaChecked;
            set
            {
                _AlphaChecked = value;
                OnPropertyChanged(nameof(AlphaChecked));
            }
        }
        private void TextureFileControl_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            CenterImage();
        }
        private void ImageZoombox_Loaded(object sender, RoutedEventArgs e)
        {
            CenterImage();
        }

        private void CenterImage()
        {
            ImageZoombox.CenterContent();
            ImageZoombox.FitToBounds();
        }

        #endregion

        public override string GetNiceName()
        {
            return "Texture";
        }
        public override Dictionary<string, string> GetValidFileExtensions()
        {
            return new Dictionary<string, string>()
            {
                { ".dds", "DDS Image" },
                { ".png", "PNG Image" },
                { ".bmp", "Bitmap Image" },
                { ".tex", "FFXIV Texture" },
            };
        }
        public override async Task INTERNAL_ClearFile()
        {
            ImageSource = null;
            ChannelsEnabled = false;
            TextureFormatLabel.Visibility = Visibility.Collapsed;
            MipMapLabel.Visibility = Visibility.Collapsed;
            SharedVariantLabel.Visibility = Visibility.Collapsed;
            SharedTextureLabel.Visibility = Visibility.Collapsed;
            TexDimensionLabel.Visibility = Visibility.Collapsed;
        }
        private async void TextureFileControl_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(GreenChecked)
                || e.PropertyName == nameof(RedChecked)
                || e.PropertyName == nameof(BlueChecked)
                || e.PropertyName == nameof(AlphaChecked))
            {
                await UpdateDisplayImage();
            }
        }

        private async Task UpdateDisplayImage()
        {
            if (string.IsNullOrWhiteSpace(InternalFilePath) || Texture == null)
            {
                return;
            }

            try
            {
                ChannelsEnabled = true;
                TextureFormatLabel.Visibility = Visibility.Visible;
                MipMapLabel.Visibility = Visibility.Visible;
                TexDimensionLabel.Visibility = Visibility.Visible;

                TextureFormat = Texture.TextureFormat.GetTexDisplayName();
                TextureDimensions = $"{Texture.Height} x {Texture.Width}";
                MipMapInfo = Texture.MipMapCount != 0 ? $"Yes ({Texture.MipMapCount})" : "No";

                var r = RedChecked ? 1.0f : 0.0f;
                var g = GreenChecked ? 1.0f : 0.0f;
                var b = BlueChecked ? 1.0f : 0.0f;
                var a = AlphaChecked ? 1.0f : 0.0f;

                var effect = new ColorChannels();
                effect.Channel = new System.Windows.Media.Media3D.Point4D(r, g, b, a);
                ImageEffect = effect;

                byte[] pixData = new byte[0];
                await Task.Run(async () =>
                {
                    pixData = await Texture.GetRawPixels(-1);
                    SwapRedBlue(pixData);
                    if (AlphaChecked)
                    {
                        MultiplyAlpha(pixData);
                    }
                });


                var format = PixelFormats.Pbgra32;
                ImageSource = BitmapSource.Create(Texture.Width, Texture.Height, 96.0, 96.0, format, null, pixData, Texture.Width * format.BitsPerPixel / 8);
            }
            catch(Exception ex)
            {
                this.ShowError("Image Display Error", "An error occurred while trying to display the image:\n\n" + ex.Message);
            }
        }


        /// <summary>
        /// Asynchronously loads the parent file information for a given texture.
        /// </summary>
        /// <returns></returns>
        private async Task LoadParentFileInformation(string path, IItem item = null)
        {
            if(item == null)
            {
                var root = await XivCache.GetFirstRoot(path);
                if(root!= null)
                {
                    item = root.GetFirstItem();
                }
            }

            if (item == null)
            {
                SharedVariantLabel.Visibility = Visibility.Collapsed;
                SharedTextureLabel.Visibility = Visibility.Collapsed;
                return;
            }
            try
            {

                var asIm = item as IItemModel;
                if (asIm == null)
                {
                    return;
                }

                var root = item.GetRoot();
                if (root == null || !Imc.UsesImc(asIm))
                {
                    SharedVariantLabel.Visibility = Visibility.Collapsed;
                    SharedTextureLabel.Visibility = Visibility.Collapsed;
                    return;
                }



                SharedVariantLabel.Visibility = Visibility.Visible;
                SharedTextureLabel.Visibility = Visibility.Collapsed;
                SharedVariantLabel.Content = "Loading usage data...".L();


                List<string> parents = new List<string>();
                List<XivImc> entries = new List<XivImc>();
                await Task.Run(async () =>
                {
                    var info = (await Imc.GetFullImcInfo(asIm, false, MainWindow.DefaultTransaction));
                    if (info == null)
                    {
                        return;
                    }

                    entries = info.GetAllEntries(asIm.GetItemSlotAbbreviation(), true);

                    if (Path.GetExtension(InternalFilePath).ToLower() == ".mtrl")
                    {
                        parents = new List<string>() { InternalFilePath };
                    }
                    else
                    {
                        parents = await XivCache.GetParentFiles(InternalFilePath);
                    }
                });

                // Invalid IMC set, cancel.
                if (asIm.ModelInfo.ImcSubsetID > entries.Count || entries.Count == 0)
                {
                    if (string.Equals(InternalFilePath,path))
                    {
                        SharedVariantLabel.Visibility = Visibility.Collapsed;
                        SharedTextureLabel.Visibility = Visibility.Collapsed;
                    }
                    return;
                }

                var vCount = entries.Count;
                Dictionary<int, int> variantsPerMset = new Dictionary<int, int>();
                foreach (var e in entries)
                {
                    if (!variantsPerMset.ContainsKey(e.MaterialSet))
                    {
                        variantsPerMset[e.MaterialSet] = 0;
                    }
                    variantsPerMset[e.MaterialSet]++;
                }

                if (variantsPerMset.ContainsKey(0))
                {
                    // Material set 0 is the null set.
                    vCount -= variantsPerMset[0];
                }

                if (parents == null || parents.Count == 0)
                {
                    if (string.Equals(InternalFilePath, path))
                    {
                        SharedVariantLabel.Content = "";
                        SharedVariantLabel.Visibility = Visibility.Collapsed;
                    }
                    return;
                }

                var mymSet = entries[asIm.ModelInfo.ImcSubsetID].MaterialSet;

                // Check if we're just used in some amount of variants of the same material.
                var firstName = Path.GetFileName(parents[0]);
                var sameMaterials = parents.Where(x => Path.GetFileName(x) == firstName);

                var mSetExctraction = new Regex("v([0-9]{4})");
                List<int> representedMaterialSets = new List<int>();
                foreach (var x in sameMaterials)
                {
                    var match = mSetExctraction.Match(x);
                    if (!match.Success) continue;

                    representedMaterialSets.Add(Int32.Parse(match.Groups[1].Value));
                }

                var variantSum = 0;
                foreach (var i in representedMaterialSets)
                {
                    if (!variantsPerMset.ContainsKey(i))
                    {
                        variantSum++;
                    }
                    else
                    {
                        variantSum += variantsPerMset[i];
                    }
                }

                if (string.Equals(InternalFilePath, path))
                {
                    SharedVariantLabel.Content = $"Used by {variantSum._()}/{vCount._()} Variants".L();
                    SharedVariantLabel.Visibility = Visibility.Visible;
                }

                var allSame = sameMaterials.Count() == parents.Count;
                if (!allSame)
                {
                    var differentFiles = parents.Select(x => Path.GetFileName(x)).ToHashSet();
                    var count = differentFiles.Count - 1;
                    if (string.Equals(InternalFilePath, path))
                    {
                        SharedTextureLabel.Content = $"Used by {count._()} Other Materials".L();
                        SharedTextureLabel.Visibility = Visibility.Visible;
                    }
                }
            }
            catch (Exception ex)
            {
                // No-op.  Lacking this data is not a critical failure.
            }
        }
    }
}
