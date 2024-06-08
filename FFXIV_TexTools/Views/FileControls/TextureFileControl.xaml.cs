using FFXIV_TexTools.Textures;
using FFXIV_TexTools.Views.Textures;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Bmp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Formats.Tga;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Memory;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using xivModdingFramework.Cache;
using xivModdingFramework.Items;
using xivModdingFramework.Items.Interfaces;
using xivModdingFramework.Mods;
using xivModdingFramework.Textures.DataContainers;
using xivModdingFramework.Textures.Enums;
using xivModdingFramework.Textures.FileTypes;
using xivModdingFramework.Variants.DataContainers;
using xivModdingFramework.Variants.FileTypes;
using Image = SixLabors.ImageSharp.Image;
using xivModdingFramework.Helpers;
using xivModdingFramework.Textures;
using System.Collections.ObjectModel;

namespace FFXIV_TexTools.Views.Controls
{
    /// <summary>
    /// Interaction logic for TextureFileControl.xaml
    /// </summary>
    public partial class TextureFileControl : FileViewControl, INotifyPropertyChanged
    {

        private XivTex _Texture = null;
        public XivTex Texture
        {
            get => _Texture;
            set
            {
                _Texture = value;
                if(Texture != null)
                {
                    Format = Texture.TextureFormat;
                } else
                {
                    Format = XivTexFormat.INVALID;
                }
                OnPropertyChanged(nameof(Texture));
            }
        }

        private byte[] _PixelData = null;
        public byte[] PixelData
        {
            get => _PixelData;
            set
            {
                _PixelData = value;
                if (!_LoadingImage && _PixelData != null)
                {
                    PixelChanges = true;
                }
                else
                {
                    PixelChanges = false;
                }
                OnPropertyChanged(nameof(_PixelData));
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

        private XivTexFormat _Format;
        public XivTexFormat Format
        {
            get => _Format;
            set
            {
                if (!_LoadingImage && value != XivTexFormat.INVALID)
                {
                    UnsavedChanges = true;
                }
                ShowFormatWarnings(value);
                _Format = value;
                OnPropertyChanged(nameof(Format));
            }
        }

        private bool _PixelChanges;
        public bool PixelChanges
        {
            get => _PixelChanges;
            set
            {
                _PixelChanges = value;

                if(Format == XivTexFormat.BC7 && PixelChanges)
                {
                    Format = XivTexFormat.A8R8G8B8;
                }

                OnPropertyChanged(nameof(PixelChanges));
            }
        }

        public ObservableCollection<KeyValuePair<string, XivTexFormat>> Formats { get; set; } = ViewHelpers.GetEnumSource<XivTexFormat>();

        private bool _LoadingImage;
        public TextureFileControl()
        {
            DataContext = this;
            InitializeComponent();
            ImageZoombox.Loaded += ImageZoombox_Loaded;

            SizeChanged += TextureFileControl_SizeChanged;
            PropertyChanged += TextureFileControl_PropertyChanged;

            ViewType = EFileViewType.Editor;
        }

        private bool NeedsPixelMerge()
        {
            if (UnsavedChanges && (Texture.TextureFormat != Format || PixelChanges))
            {
                return true;
            }
            return false;
        }

        protected override async Task<byte[]> INTERNAL_GetUncompressedData()
        {
            if (NeedsPixelMerge())
            {
                Texture.TextureFormat = Format;
                await Tex.MergePixelData(Texture, PixelData);
            }

            // Assign this any time we save.
            var options = new SmartImportOptions()
            {
                TextureFormat = Format
            };
            LastImportOptions = options;

            return Texture.ToUncompressedTex();
        }

        protected override async Task<bool> INTERNAL_LoadFile(byte[] uncompressedData, string path, IItem referenceItem, ModTransaction tx)
        {
            Texture = XivTex.FromUncompressedTex(uncompressedData);
            Texture.FilePath = path;

            if(Texture != null)
            {
                _LoadingImage = true;
                try
                {
                    PixelData = await Texture.GetRawPixels(-1);
                }
                finally
                {
                    _LoadingImage = false;
                }
            }
            UpdateDisplayImage();

            _ = LoadParentFileInformation(path, referenceItem);
            CenterImage();
            return true;
        }

        protected override async Task<bool> INTERNAL_SaveAs(string externalFilePath)
        {
            var ext = Path.GetExtension(externalFilePath).ToLower();

            if(ext == ".tex" || ext == ".atex")
            {
                return await SaveAsRaw(externalFilePath);
            }
            else if(ext == ".dds")
            {
                var tex = Texture;
                if (NeedsPixelMerge())
                {
                    tex = (XivTex)Texture.Clone();
                    tex.TextureFormat = Format;
                    await Tex.MergePixelData(tex, PixelData);
                }
                Tex.SaveTexAsDDS(externalFilePath, tex);
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
            else if (ext == ".png")
            {
                encoder = new PngEncoder()
                {
                    BitDepth = PngBitDepth.Bit16
                };
            } else {
                encoder = new TgaEncoder()
                {
                    BitsPerPixel = TgaBitsPerPixel.Pixel32,
                };
            };

            using (var img = Image.LoadPixelData<Rgba32>(PixelData, Texture.Width, Texture.Height))
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

        private string _TextureInfo;
        public string TextureInfo
        {
            get => _TextureInfo;
            set
            {
                _TextureInfo = value;
                OnPropertyChanged(nameof(TextureInfo));
            }
        }

        private Visibility _SharedVariantVisibility = Visibility.Collapsed;
        public Visibility SharedVariantVisibility
        {
            get => _SharedVariantVisibility;
            set
            {
                _SharedVariantVisibility = value;
                OnPropertyChanged(nameof(SharedVariantVisibility));
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
                { ".tga", "TGA Image" },
                { ".bmp", "Bitmap Image" },
                { ".tex", "FFXIV Texture" },
                { ".atex", "FFXIV VFX Texture" },
            };
        }
        public override void INTERNAL_ClearFile()
        {
            Texture = null;
            ImageSource = null;
            ChannelsEnabled = false;
            PixelData = null;
            
            if (Texture == null)
            {
                TextureInfo = "--";
            }

            SharedVariantVisibility = Visibility.Collapsed;
        }
        private async void TextureFileControl_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(GreenChecked)
                || e.PropertyName == nameof(RedChecked)
                || e.PropertyName == nameof(BlueChecked)
                || e.PropertyName == nameof(AlphaChecked))
            {
                UpdateDisplayImage();
            }
        }

        public void UpdateDisplayImage()
        {
            if (string.IsNullOrWhiteSpace(InternalFilePath) || Texture == null)
            {
                return;
            }

            try
            {
                ChannelsEnabled = true;

                if(Texture == null)
                {
                    TextureInfo = "--";
                    return;
                }

                TextureInfo = $"{Texture.Width}x{Texture.Height} ({Texture.MipMapCount} MipMaps)";

                var r = RedChecked ? 1.0f : 0.0f;
                var g = GreenChecked ? 1.0f : 0.0f;
                var b = BlueChecked ? 1.0f : 0.0f;
                var a = AlphaChecked ? 1.0f : 0.0f;

                if(ImageEffect == null)
                {
                    ImageEffect = new ColorChannels();
                }
                ImageEffect.Channel = new System.Windows.Media.Media3D.Point4D(r, g, b, a);
                OnPropertyChanged(nameof(ImageEffect));


                var pixData = (byte[]) PixelData.Clone();
                SwapRedBlue(pixData);
                if (AlphaChecked)
                {
                    MultiplyAlpha(pixData);
                }

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

            SharedVariantVisibility = Visibility.Collapsed;

            var root = await XivCache.GetFirstRoot(path);

            if(root == null)
            {
                return;
            }

            if(item == null)
            {
                item = root.GetFirstItem();
            }

            var asIm = item as IItemModel;
            if (asIm == null || !Imc.UsesImc(asIm))
            {
                return;
            }
            try
            {
                List<string> parents = new List<string>();
                List<XivImc> entries = new List<XivImc>();
                await Task.Run(async () =>
                {
                    var tx = MainWindow.DefaultTransaction;
                    var info = (await Imc.GetFullImcInfo(asIm, false, tx));
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
                        parents = await XivCache.GetParentFiles(InternalFilePath, tx);
                    }
                });

                // Invalid IMC set, cancel.
                if (asIm.ModelInfo.ImcSubsetID > entries.Count || entries.Count == 0)
                {
                    return;
                }

                if(parents == null || parents.Count == 0)
                {
                    SharedVariantLabel.Content = $"Unused (Orphaned) Texture".L();
                    SharedVariantVisibility = Visibility.Visible;
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

                    var differentFiles = parents.Select(x => Path.GetFileName(x)).ToHashSet();
                    var count = differentFiles.Count;

                    SharedVariantLabel.Content = $"Used in {variantSum._()}/{vCount._()} Variant(s)".L();
                    SharedTextureLabel.Content = $"Used in {count._()} Material(s)".L();
                    SharedVariantVisibility = Visibility.Visible;
                }
            }
            catch (Exception ex)
            {
                // No-op.  Lacking this data is not a critical failure.
            }
        }

        protected override void FreeManaged()
        {
            SizeChanged -= TextureFileControl_SizeChanged;
            PropertyChanged -= TextureFileControl_PropertyChanged;

            if(ImageZoombox != null)
            {
                ImageZoombox.Loaded -= ImageZoombox_Loaded;
            }

            if (ImageEffect != null)
            {
                ImageEffect.Dispose();
            }

            ImageEffect = null;
            ImageSource = null;
            Texture = null;

            base.FreeManaged();
        }

        private async void EditChannels_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if(Texture == null || PixelData == null)
                {
                    return;
                }
                EditChannelsWindow.ShowChannelEditor(this);
            }
            catch (Exception ex)
            {
                Trace.WriteLine(ex);
            }
        }

        private void AddOverlay_Click(object sender, RoutedEventArgs e)
        {
            var ofd = new OpenFileDialog();
            ofd.Filter = "Image Files with Transparency|*.png;*.tga;*.dds";
            if(ofd.ShowDialog() != DialogResult.OK)
            {
                return;
            }

            try
            {
                var file = ofd.FileName;
                var cw = Texture.Width;
                var ch = Texture.Height;

                using (var overlay = Image.Load<Rgba32>(file))
                {
                    overlay.Mutate(x => x.Resize(cw, ch));

                    using (var img = Image.LoadPixelData<Rgba32>(PixelData, cw, ch))
                    {

                        img.Mutate(x => x.DrawImage(overlay, 1.0f));

                        PixelData = IOUtil.GetImageSharpPixels(img);
                        UpdateDisplayImage();
                        UnsavedChanges = true;
                    }
                }
            } catch(Exception ex)
            {
                this.ShowError("Image Processing Error", "An error occurred while processing the overlay:\n\n" + ex.Message);
            }
        }

        private void ShowFormatWarnings(XivTexFormat format)
        {
            if (Texture == null) return;
            if(format == Texture.TextureFormat && !PixelChanges)
            {
                return;
            }

            if(format == XivTexFormat.BC7)
            {
                this.ShowWarning("Texture Format Warning", "TexTools does not currently have a BC7 Compressor for raw pixel data.\n\nYou will not be able to save the resulting image.");
            }
        }


        protected override async Task<byte[]> INTERNAL_ExternalToUncompressedFile(string externalFile, string internalFile, IItem referenceItem, ModTransaction tx)
        {
            var options = new SmartImportOptions()
            {
                TextureFormat = Format
            };

            LastImportOptions = options;

            // Override this so we can pass in the user's requested texture format.
            return await SmartImport.CreateUncompressedFile(externalFile, internalFile, tx, options);
        }
    }
}
