
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using static xivModdingFramework.Materials.DataContainers.ShaderHelpers;
using xivModdingFramework.Materials;
using xivModdingFramework.Materials.DataContainers;
using System.ComponentModel;
using System.Xml.Linq;
using System.Text.RegularExpressions;
using System.Windows.Input;
using System.Linq;
using System.Windows.Controls;

namespace FFXIV_TexTools.Views.Controls
{
    /// <summary>
    /// Interaction logic for RawFloatValueDisplay.xaml
    /// </summary>
    public partial class MaterialFlagsEditor : Window, INotifyPropertyChanged
    {
        public ushort Flags;
        public ushort Flags2;
        public ulong AdditionalData;

        public event PropertyChangedEventHandler PropertyChanged;

        private XivMtrl _Material;

        public ulong FullInt { get
            {
                var ret = (((ulong) Flags2 << 16) | (ulong)Flags);
                return ret;
            }
            set
            {
                Flags = (ushort)value;
                Flags2 = (ushort)(value >> 16);
                UpdateCheckBoxes();
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(FullInt)));
            }
        }
        public ulong AdditionalDataRaw
        {
            get
            {
                return AdditionalData;
            }
            set
            {
                AdditionalData = value;
                UpdateCheckBoxes();
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(AdditionalDataRaw)));
            }
        }
        public MaterialFlagsEditor(XivMtrl material)
        {
            _Material = material;
            DataContext = this;
            Flags = _Material.MaterialFlags;
            Flags2 = _Material.MaterialFlags2;

            var data = _Material.AdditionalData;
            if(_Material.AdditionalData.Length < 8)
            {
                data = new byte[8];
                _Material.AdditionalData.CopyTo(data, 0);
            }

            AdditionalData = BitConverter.ToUInt64(data,0);

            InitializeComponent();
            AdditionalDataLengthBox.Text = _Material.AdditionalData.Length.ToString();
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(FullInt)));
            BitControl0.SetNames(new List<string>()
            {
                "Show Backfaces",
                "Bit 1",
                "Bit 2",
                "Bit 3",
                "Bit 4",
                "Enable Translucency",
                "Bit 6",
                "Bit 7",
            });
            BitControl1.SetNames(new List<string>()
            {
                "Bit 8",
                "Bit 9",
                "Bit 10",
                "Bit 11",
                "Bit 12",
                "Bit 13",
                "Bit 14",
                "Bit 15",
            });
            BitControl2.SetNames(new List<string>()
            {
                "Bit 16",
                "Bit 17",
                "Bit 18",
                "Bit 19",
                "Bit 20",
                "Bit 21",
                "Bit 22",
                "Bit 23",
            });
            BitControl3.SetNames(new List<string>()
            {
                "Bit 24",
                "Bit 25",
                "Bit 26",
                "Bit 27",
                "Bit 28",
                "Bit 29",
                "Bit 30",
                "Bit 31",
            });

            BitControl0.ByteChanged += ByteChanged;
            BitControl1.ByteChanged += ByteChanged;
            BitControl2.ByteChanged += ByteChanged;
            BitControl3.ByteChanged += ByteChanged;

            UpdateCheckBoxes();
        }

        private bool _Updating;

        private void ByteChanged(object sender, byte e)
        {
            if(_Updating)
            {
                return;
            }
            UpdateValues();
        }
        private void UpdateValues()
        {
            Flags = (ushort)((BitControl1.DisplayByte << 8) | BitControl0.DisplayByte);
            Flags2 = (ushort)((BitControl3.DisplayByte << 8) | BitControl2.DisplayByte);

            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(FullInt)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(AdditionalDataRaw)));
        }

        private void UpdateCheckBoxes()
        {
            _Updating = true;
            BitControl0.DisplayByte = (byte)Flags;
            BitControl1.DisplayByte = (byte)(Flags >> 8);
            BitControl2.DisplayByte = (byte)Flags2;
            BitControl3.DisplayByte = (byte)(Flags2 >> 8);
            _Updating = false;
        }
        private void NumberValidationTextBox(object sender, TextCompositionEventArgs e)
        {
            Regex regex = new Regex("[^0-9]+");
            e.Handled = regex.IsMatch(e.Text);
        }
        private void HexInput(object sender, TextCompositionEventArgs e)
        {
            Regex regex = new Regex("[^0-9a-f]+",RegexOptions.IgnoreCase);
            e.Handled = regex.IsMatch(e.Text);
        }
        private void Confirm_Click(object sender, RoutedEventArgs e)
        {
            UpdateValues();

            _Material.MaterialFlags = Flags;
            _Material.MaterialFlags2 = Flags2;

            int val = 0;
            var success = Int32.TryParse(AdditionalDataLengthBox.Text, out val);
            if(!success)
            {
                val = _Material.AdditionalData.Length;
            }
            var data = BitConverter.GetBytes(AdditionalData);
            _Material.AdditionalData = data.Take(val).ToArray();

            DialogResult = true;
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            Flags = 0;
            Flags2 = 0;
            AdditionalData = 0;
            DialogResult = false;
        }

        public static bool ShowFlagsEditor(XivMtrl mtrl, Window owner = null)
        {
            var wind = new MaterialFlagsEditor(mtrl);
            wind.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            wind.Owner = owner != null ? owner : System.Windows.Application.Current.MainWindow;
            var result = wind.ShowDialog();
            if(result != true)
            {
                return false;
            }
            return true;
        }

    }
}
