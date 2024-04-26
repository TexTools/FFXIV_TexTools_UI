
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using static xivModdingFramework.Materials.DataContainers.ShaderHelpers;
using System.IO;
using System.Linq;
using HelixToolkit.SharpDX.Core.Shaders;
using xivModdingFramework.Materials.DataContainers;
using System.ComponentModel;
using System.Xml.Linq;

namespace FFXIV_TexTools.Views.Controls
{
    /// <summary>
    /// Interaction logic for RawFloatValueDisplay.xaml
    /// </summary>
    public partial class MaterialFlagsEditor : Window, INotifyPropertyChanged
    {
        public ushort Flags;
        public ushort Flags2;

        public event PropertyChangedEventHandler PropertyChanged;

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
        public MaterialFlagsEditor(ushort flags, ushort flags2)
        {
            InitializeComponent();
            DataContext = this;
            Flags = flags;
            Flags2 = flags2;
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

        private void Confirm_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            UpdateValues();
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Flags = 0;
            Flags2 = 0;
            Close();
        }

        public static (bool Success, ushort Flags, ushort Unknown) ShowFlagsEditor(ushort flags, ushort flags2, Window owner = null)
        {
            var wind = new MaterialFlagsEditor(flags, flags2);
            wind.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            wind.Owner = owner != null ? owner : System.Windows.Application.Current.MainWindow;
            var result = wind.ShowDialog();
            if(result != true)
            {
                return (false, 0, 0);
            }
            return (true, wind.Flags, wind.Flags2);

        }
    }
}
