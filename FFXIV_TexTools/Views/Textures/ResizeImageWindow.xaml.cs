using FFXIV_TexTools.Views.Controls;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using xivModdingFramework.Textures.DataContainers;
using xivModdingFramework.Textures.FileTypes;

namespace FFXIV_TexTools.Views.Textures
{
    /// <summary>
    /// Interaction logic for EditChannelsWindow.xaml
    /// </summary>
    public partial class ResizeImageWindow : Window, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        public ObservableCollection<KeyValuePair<string, int>> ImageSizes { get; set; } = new ObservableCollection<KeyValuePair<string, int>>()
        {
            new KeyValuePair<string, int>("64", 64),
            new KeyValuePair<string, int>("128", 128),
            new KeyValuePair<string, int>("256", 256),
            new KeyValuePair<string, int>("512", 512),
            new KeyValuePair<string, int>("1024", 1024),
            new KeyValuePair<string, int>("2048", 2048),
            new KeyValuePair<string, int>("4096", 4096),
        };
        public ObservableCollection<KeyValuePair<string, bool>> Samplers { get; set; } = new ObservableCollection<KeyValuePair<string, bool>>()
        {
            new KeyValuePair<string, bool>("Bicubic", false),
            new KeyValuePair<string, bool>("Nearest Neighbor", true),
        };


        private int _Width;
        public int TexWidth
        {
            get => _Width;
            set
            {
                _Width = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(TexWidth)));
            }
        }
        private int _Height;
        public int TexHeight
        {
            get => _Height;
            set
            {
                _Height = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(TexHeight)));
            }
        }


        private bool _Sampler;
        public bool Sampler
        {
            get => _Sampler;
            set
            {
                _Sampler = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Sampler)));
            }
        }

        public XivTex Texture;

        public ResizeImageWindow(XivTex tex)
        {
            Texture = (XivTex) tex.Clone();
            DataContext = this;
            InitializeComponent();

            TexWidth = Texture.Width;
            TexHeight = Texture.Height;

            Closing += EditChannelsWindow_Closing;
        }

        private void EditChannelsWindow_Closing(object sender, CancelEventArgs e)
        {
            if (null != Owner)
            {
                Owner.Activate();
            }
        }
        private async void Confirm_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                await Tex.ResizeXivTx(Texture, TexWidth, TexHeight, Sampler);
                DialogResult = true;
            }
            catch(Exception ex) 
            {
                this.ShowError("Resize Error","An error occurred while resizing the image:\n\n" + ex.Message);
            }

        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }

        public static XivTex ShowResizeWindow(TextureFileControl texControl, XivTex tex)
        {
            var owner = Window.GetWindow(texControl);
            var wind = new ResizeImageWindow(tex);
            wind.Owner = owner;
            wind.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            var res = wind.ShowDialog();

            XivTex ret = null;
            if(res == true)
            {
                ret = wind.Texture;
            }
            return ret;
        }

    }
}
