using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
using System.Windows.Navigation;
using System.Windows.Shapes;
using xivModdingFramework.Materials.DataContainers;

namespace FFXIV_TexTools.Views.Textures
{
    /// <summary>
    /// Interaction logic for SamplerSettingsEntryControl.xaml
    /// </summary>
    public partial class SamplerSettingsEntryControl : UserControl
    {
        private TextureSamplerSettings _sampler;
        public TextureSamplerSettings Sampler
        {
            get
            {
                return _sampler;
            }
        }

        public ObservableCollection<KeyValuePair<MtrlSamplerId, string>> SamplerTypesSource;

        public event EventHandler<EventArgs> RequestDelete;
        public SamplerSettingsEntryControl()
        {
            InitializeComponent();
        }
        public SamplerSettingsEntryControl(TextureSamplerSettings sampler)
        {
            InitializeComponent();

            _sampler = sampler;
            this.DataContext = _sampler;
            textureBox.Text = sampler.TexturePath;

            // Build the sampler type reference.
            SamplerTypesSource = new ObservableCollection<KeyValuePair<MtrlSamplerId, string>>();
            foreach (var val in Enum.GetValues(typeof(MtrlSamplerId)))
            {
                var key = (MtrlSamplerId)val;
                SamplerTypesSource.Add(new KeyValuePair<MtrlSamplerId, string>(key, key.ToString()));
            }

            samplerBox.ItemsSource = SamplerTypesSource;
            samplerBox.DisplayMemberPath = "Value";
            samplerBox.SelectedValuePath = "Key";

            samplerBox.SelectedValue = _sampler.SamplerId;

            settingsBox.Text = _sampler.SamplerSettings.ToString("X");
            flagsBox.Text = _sampler.Flags.ToString("X");

            samplerBox.SelectionChanged += SamplerBox_SelectionChanged;
            textureBox.TextChanged += TextureBox_TextChanged;
            settingsBox.TextChanged += SettingsBox_TextChanged;
            flagsBox.TextChanged += FlagsBox_TextChanged;
        }

        private void FlagsBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                ushort decValue = Convert.ToUInt16(flagsBox.Text, 16);
                _sampler.Flags = decValue;
                flagsBox.Background = Brushes.White;
            } catch
            {
                flagsBox.Background = Brushes.Red;
            }
        }

        private void SettingsBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                ushort decValue = Convert.ToUInt16(settingsBox.Text, 16);
                _sampler.SamplerSettings = decValue;
                settingsBox.Background = Brushes.White;
            }
            catch
            {
                settingsBox.Background = Brushes.Red;
            }
        }

        private void TextureBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            _sampler.TexturePath = textureBox.Text;
        }

        private void SamplerBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var id = (MtrlSamplerId)samplerBox.SelectedValue;
            _sampler.SamplerId = id;

            if(id == MtrlSamplerId.None)
            {
                // Event handlers will update the underlying sampler here.
                settingsBox.Text = "0";
                flagsBox.Text = "0";
                settingsBox.IsEnabled = false;
                flagsBox.IsEnabled = false;
            } else
            {
                settingsBox.IsEnabled = true;
                flagsBox.IsEnabled = true;
            }
        }

        private void deleteButton_Click(object sender, RoutedEventArgs e)
        {
            if (RequestDelete != null)
            {
                RequestDelete.Invoke(this, null);
            }
        }
    }
}
