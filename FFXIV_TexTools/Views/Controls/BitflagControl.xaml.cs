using SixLabors.ImageSharp;
using System;
using System.Collections.Generic;
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

namespace FFXIV_TexTools.Views.Controls
{
    /// <summary>
    /// Interaction logic for BitflagControl.xaml
    /// </summary>
    public partial class BitflagControl : UserControl
    {
        List<CheckBox> Boxes = new List<CheckBox>();

        private byte _byte;
        public byte DisplayByte { get
            {
                return _byte;
            }
            set
            {
                if(_byte != value)
                {
                    _byte = value;
                    OnByteChanged();
                }
            }
        }

        public event EventHandler<byte> ByteChanged;
        private bool _Updating = false;
        public BitflagControl()
        {
            InitializeComponent();

            Boxes.Add(Checkbox0);
            Boxes.Add(Checkbox1);
            Boxes.Add(Checkbox2);
            Boxes.Add(Checkbox3);
            Boxes.Add(Checkbox4);
            Boxes.Add(Checkbox5);
            Boxes.Add(Checkbox6);
            Boxes.Add(Checkbox7);

            foreach(var bx in Boxes)
            {
                bx.Checked += Checked;
                bx.Unchecked += Checked;
            }
        }
        public void SetNames(List<string> names)
        {
            for(int i = 0; i < names.Count; i++)
            {
                
                if (!String.IsNullOrWhiteSpace(names[i]))
                {
                    Boxes[i].Content = names[i];
                }
            }
        }
        public void SetTooltips(List<string> names)
        {
            for (int i = 0; i < names.Count; i++)
            {

                if (!String.IsNullOrWhiteSpace(names[i]))
                {
                    Boxes[i].ToolTip = names[i];
                }
            }
        }

        private void Checked(object sender, RoutedEventArgs e)
        {
            if (_Updating) return;

            var box = (CheckBox)sender;
            var index = Boxes.IndexOf(box);
            var val = box.IsChecked;

            if(val == true)
            {
                DisplayByte |= (byte) (1 << index);
            }
            else
            {
                DisplayByte &= (byte)~(1 << index);
            }
        }

        private void OnByteChanged()
        {
            _Updating = true;
            for(int i = 0; i < Boxes.Count; i++) {
                var flag = (byte) (1 << i);
                var res = DisplayByte & flag;
                if (res > 0)
                {
                    Boxes[i].IsChecked = true;
                } else
                {
                    Boxes[i].IsChecked = false;
                }
            }
            _Updating = false;

            ByteChanged?.Invoke(this, DisplayByte);
        }
    }
}
