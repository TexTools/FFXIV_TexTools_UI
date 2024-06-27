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

namespace FFXIV_TexTools.Views.Textures
{
    /// <summary>
    /// Interaction logic for EditChannelsWindow.xaml
    /// </summary>
    public partial class EditChannelsWindow : Window, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        private ObservableCollection<KeyValuePair<string, int>> _Channels = new ObservableCollection<KeyValuePair<string, int>>();
        public ObservableCollection<KeyValuePair<string, int>> Channels
        {
            get => _Channels;
            set
            {
                _Channels = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Channels)));
            }
        }

        private ObservableCollection<KeyValuePair<string, int>> _OtherChannels = new ObservableCollection<KeyValuePair<string, int>>();
        public ObservableCollection<KeyValuePair<string, int>> OtherChannels
        {
            get => _OtherChannels;
            set
            {
                _OtherChannels = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(OtherChannels)));
            }
        }

        private int _SelectedChannel;
        public int SelectedChannel
        {
            get => _SelectedChannel;
            set
            {
                _SelectedChannel = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectedChannel)));
                var ch = Channels.Where(x => x.Value != value);
                OtherChannels = new ObservableCollection<KeyValuePair<string, int>>(ch);
                CopyChannel = OtherChannels.FirstOrDefault().Value;
                SwapChannel = OtherChannels.FirstOrDefault().Value;
            }
        }

        private int _SwapChannel;
        public int SwapChannel
        {
            get => _SwapChannel;
            set
            {
                _SwapChannel = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SwapChannel)));
            }
        }

        private int _CopyChannel;
        public int CopyChannel
        {
            get => _CopyChannel;
            set
            {
                _CopyChannel = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CopyChannel)));
            }
        }


        private int _GreyValue;
        public int GreyValue
        {
            get => _GreyValue;
            set
            {
                _GreyValue = Math.Max(Math.Min(value, 255), 0);
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(GreyValue)));
            }
        }


        private byte[] OriginalPixelData;
        private byte[] PixelData;
        private XivTex Texture;
        private TextureFileControl Control;

        public EditChannelsWindow(TextureFileControl texControl)
        {
            DataContext = this;
            InitializeComponent();

            var tex = texControl.Texture;
            var pixelData = texControl.PixelData;

            OriginalPixelData = (byte[]) pixelData.Clone();
            PixelData = pixelData;
            Texture = tex;
            Control = texControl;

            Channels.Add(new KeyValuePair<string, int>("Red", 0));
            Channels.Add(new KeyValuePair<string, int>("Green", 1));
            Channels.Add(new KeyValuePair<string, int>("Blue", 2));
            Channels.Add(new KeyValuePair<string, int>("Alpha", 3));
            SelectedChannel = 0;
            Closing += EditChannelsWindow_Closing;
        }

        private void EditChannelsWindow_Closing(object sender, CancelEventArgs e)
        {
            if (null != Owner)
            {
                Owner.Activate();
            }
        }

        private async Task ModifyPixels(Action<int> action)
        {
            List<Task> tasks = new List<Task>();
            for (int i = 0; i < Texture.Height; i++)
            {
                var h = i;
                tasks.Add(Task.Run(() =>
                {
                    for (int x = 0; x < Texture.Width; x++)
                    {
                        var offset = ((Texture.Width * h) + x) * 4;
                        action(offset);
                    }
                }));
            }
            await Task.WhenAll(tasks);
            Control.UpdateDisplayImage();
        }

        private async Task FillChannel(int channel, byte value)
        {
            Action<int> act = (i) =>
            {
                PixelData[i + channel] = value;
            };
            await ModifyPixels(act);
        }

        private async Task InvertChannel(int channel)
        {
            Action<int> act = (i) =>
            {
                PixelData[i + channel] = (byte)(255 - PixelData[i + channel]);
            };
            await ModifyPixels(act);
        }

        private async Task SwapChannels(int channelA, int channelB)
        {
            Action<int> act = (i) =>
            {
                var a = PixelData[i + channelA];
                PixelData[i + channelA] = PixelData[i + channelB];
                PixelData[i + channelB] = a;
            };
            await ModifyPixels(act);
        }

        private async Task AlterChannel(int channel, int addOrSubtract)
        {
            Action<int> act = (i) =>
            {
                byte a = PixelData[i + channel];
                var z = a + addOrSubtract;
                PixelData[i + channel] = (byte)Math.Max(Math.Min(z, 255), 0);
            };
            await ModifyPixels(act);
        }

        private async Task CopyChannelAction(int channelFrom, int channelTo)
        {
            Action<int> act = (i) =>
            {
                PixelData[i + channelTo] = PixelData[i + channelFrom];
            };
            await ModifyPixels(act);
        }

        private async Task ClampChannel(int channel, byte min, byte max)
        {
            Action<int> act = (i) =>
            {
                PixelData[i + channel] = Math.Max(Math.Min(PixelData[i + channel], max), min);
            };
            await ModifyPixels(act);
        }

        private async Task RemapChannel(int channel, byte newMin, byte newMax, byte oldMin = 0, byte oldMax = 255)
        {
            Action<int> act = (i) =>
            {
                byte a = PixelData[i + channel];
                var z = (float)(a - oldMin) / (float)(oldMax - oldMin) * (float)(newMax - newMin) + newMin;
                PixelData[i + channel] = (byte)Math.Max(Math.Min(Math.Round(z), 255), 0);
            };
            await ModifyPixels(act);
        }


        private void Confirm_Click(object sender, RoutedEventArgs e)
        {
            Control.UnsavedChanges = true;
            DialogResult = true;
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }

        public static void ShowChannelEditor(TextureFileControl texControl)
        {
            var owner = Window.GetWindow(texControl);
            var wind = new EditChannelsWindow(texControl);
            wind.Owner = owner;
            wind.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            var res = wind.ShowDialog();
            
            if(res == true)
            {
                texControl.PixelData = wind.PixelData;
            } else
            {
                texControl.PixelData = wind.OriginalPixelData;
            }
            texControl.UpdateDisplayImage();
        }

        private async void Fill_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                await FillChannel(SelectedChannel, (byte)GreyValue);
            } catch (Exception ex)
            {
                Trace.WriteLine(ex.Message);
            }
        }

        private async void Invert_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                await InvertChannel(SelectedChannel);
            }
            catch (Exception ex)
            {
                Trace.WriteLine(ex.Message);
            }
        }

        private async void Copy_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                await CopyChannelAction(SelectedChannel, CopyChannel);
            }
            catch (Exception ex)
            {
                Trace.WriteLine(ex.Message);
            }
        }

        private async void Swap_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                await SwapChannels(SelectedChannel, SwapChannel);
            }
            catch (Exception ex)
            {
                Trace.WriteLine(ex.Message);
            }
        }

        private async void Brighten_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                await AlterChannel(SelectedChannel, 5);
            }
            catch (Exception ex)
            {
                Trace.WriteLine(ex.Message);
            }

        }

        private async void Darken_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                await AlterChannel(SelectedChannel, -5);
            }
            catch (Exception ex)
            {
                Trace.WriteLine(ex.Message);
            }


        }
    }
}
