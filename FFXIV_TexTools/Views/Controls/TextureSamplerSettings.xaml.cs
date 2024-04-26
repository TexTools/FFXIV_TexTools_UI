
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using static xivModdingFramework.Materials.DataContainers.ShaderHelpers;
using xivModdingFramework.Materials.DataContainers;
using System.Windows.Forms;
using System.ComponentModel;

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
                return Texture.TexturePath;
            }
            set
            {
                Texture.TexturePath = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(TexturePath)));
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
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(TexturePath)));
            }
        }

        public TextureSamplerSettings(MtrlTexture tex)
        {
            InitializeComponent();
            Texture = (MtrlTexture)tex.Clone();
            DataContext = this;
        }

        private void Confirm_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        public static MtrlTexture ShowSamplerSettings(MtrlTexture tex)
        {
            var wind = new TextureSamplerSettings(tex);
            wind.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            var result = wind.ShowDialog();
            if(result != true)
            {
                return null;
            }
            
            return wind.Texture;
        }
    }
}
