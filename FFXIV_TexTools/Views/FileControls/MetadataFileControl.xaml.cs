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
using xivModdingFramework.Mods;
using xivModdingFramework.Mods.FileTypes;
using xivModdingFramework.Models.FileTypes;

namespace FFXIV_TexTools.Views.Controls
{
    /// <summary>
    /// Interaction logic for TextureFileControl.xaml
    /// </summary>
    public partial class MetadataFileControl : FileViewControl, INotifyPropertyChanged
    {
        public MetadataFileControl()
        {
            DataContext = this;
            InitializeComponent();
            EqpView.FileChanged += FileChanged;
            SkeletonView.FileChanged += FileChanged;
            EqdpView.FileChanged += FileChanged;
            ImcView.FileChanged += FileChanged;
            VisorView.FileChanged += FileChanged;
            ViewType = EFileViewType.Editor;
        }

        private bool _LOADING_METADATA;
        private void FileChanged()
        {
            if (!_LOADING_METADATA)
            {
                UnsavedChanges = true;
            }
        }

        private ItemMetadata _Metadata;
        public ItemMetadata Metadata
        {
            get
            {
                return _Metadata;
            }
            set
            {
                _Metadata = value;
                OnPropertyChanged(nameof(Metadata));
            }
        }


        private XivDependencyRoot _Root;
        public XivDependencyRoot Root
        {
            get
            {
                return _Root;
            }
            set
            {
                _Root = value;
                OnPropertyChanged(nameof(Root));
            }
        }

        public override string GetNiceName()
        {
            return "Metadata";
        }

        public override Dictionary<string, string> GetValidFileExtensions()
        {
            return new Dictionary<string, string>()
            {
                { ".meta", "FFXIV Item Metadata" }
            };
        }
        protected override async Task<byte[]> INTERNAL_GetDataFromPath(string internalPath, bool forceOriginal, ModTransaction tx)
        {
            // Metadata has to call into some special functions as the path may not normally exist.
            var mData = await ItemMetadata.GetMetadata(internalPath, forceOriginal, tx);
            return await ItemMetadata.Serialize(mData);
        }

        public override async Task INTERNAL_ClearFile()
        {
            Metadata = null;
        }

        protected override async Task<bool> INTERNAL_CanLoadFile(string filePath, byte[] data, ModTransaction tx)
        {
            // Metadata can load files that don't exist, because they're really a fake compiled file.
            var targetRoot = await XivCache.GetFirstRoot(filePath);
            if(targetRoot == null)
            {
                return false;
            }

            // But they do have to be at a very specific location.
            return targetRoot.Info.GetRootFile() == filePath;
        }

        protected override async Task<byte[]> INTERNAL_GetUncompressedData()
        {
            return await ItemMetadata.Serialize(Metadata);
        }

        protected override async Task<bool> INTERNAL_LoadFile(byte[] data)
        {
            Metadata = await ItemMetadata.Deserialize(data);

            Metadata.Validate(InternalFilePath);

            Root = Metadata.Root;
            await OnRootChanged();

            return true;
        }

        protected override async Task<bool> INTERNAL_SaveAs(string externalFilePath)
        {
            File.WriteAllBytes(externalFilePath, await INTERNAL_GetUncompressedData());
            return true;
        }

        private async Task OnRootChanged()
        {
            if(Metadata == null || Root == null)
            {
                // TODO: Should this disable some UI bits?
                return;
            }

            var defaultVariant = 0;
            var im = ReferenceItem as IItemModel;
            if (im == null || !Imc.UsesImc(im))
            {
                //No-Op
            }
            else if (im.ModelInfo != null)
            {
                defaultVariant = im.ModelInfo.ImcSubsetID >= 0 ? im.ModelInfo.ImcSubsetID : 0;
            }


            SetLabel.Content = XivItemTypes.GetSystemPrefix(Root.Info.PrimaryType) + Root.Info.PrimaryId.ToString().PadLeft(4, '0');
            SlotLabel.Content = Mdl.SlotAbbreviationDictionary.FirstOrDefault(x => x.Value == Root.Info.Slot).Key + "(" + Root.Info.Slot + ")";


            var tx = MainWindow.DefaultTransaction;

            _LOADING_METADATA = true;

            if (Metadata.ImcEntries.Count > 0)
            {
                ImcView.Visibility = System.Windows.Visibility.Visible;
                await ImcView.SetMetadata(Metadata, defaultVariant);
            }
            else
            {
                ImcView.Visibility = System.Windows.Visibility.Collapsed;
            }

            if (Metadata.EqpEntry != null)
            {
                EqpView.Visibility = System.Windows.Visibility.Visible;
                await EqpView.SetMetadata(Metadata);
            }
            else
            {
                EqpView.Visibility = System.Windows.Visibility.Collapsed;
            }

            if (Metadata.EqdpEntries.Count > 0)
            {
                EqdpView.Visibility = System.Windows.Visibility.Visible;
                await EqdpView.SetMetadata(Metadata);
            }
            else
            {
                EqdpView.Visibility = System.Windows.Visibility.Collapsed;
            }

            if (Metadata.EstEntries.Count > 0)
            {
                SkeletonView.Visibility = System.Windows.Visibility.Visible;
                await   SkeletonView.SetMetadata(Metadata);
            }
            else
            {
                SkeletonView.Visibility = System.Windows.Visibility.Collapsed;
            }

            if (Metadata.GmpEntry != null)
            {
                VisorView.Visibility = System.Windows.Visibility.Visible;
                await VisorView.SetMetadata(Metadata);
            }
            else
            {
                VisorView.Visibility = System.Windows.Visibility.Collapsed;

            }

            _LOADING_METADATA = false;

        }

    }
}
