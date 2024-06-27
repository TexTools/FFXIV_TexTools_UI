using System;
using System.Collections.Generic;
using System.ComponentModel;
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
using xivModdingFramework.Helpers;

namespace FFXIV_TexTools.Views.Metadata
{
    /// <summary>
    /// Interaction logic for ImcMaskGrid.xaml
    /// </summary>
    public partial class ImcMaskGrid : UserControl, INotifyPropertyChanged
    {
        private ushort _Mask;
        public ushort Mask
        {
            get => _Mask;
            set
            {
                _Mask = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Mask)));
                OnMaskUpdated();
            }
        }

        public event Action<ushort> MaskChanged;

        private List<CheckBox> Boxes = new List<CheckBox>();

        public event PropertyChangedEventHandler PropertyChanged;

        private bool _LOADING = false;
        public ImcMaskGrid()
        {
            InitializeComponent();
            DataContext = this;
            for (int i = 0; i < 10; i++)
            {
                var box = CreateCheckbox(i);
                Boxes.Add(box);
                MaskGrid.Children.Add(box);
            }
        }
        public ImcMaskGrid(ushort mask) : this()
        {
            Mask = mask;
        }

        public void SetMask(ushort mask)
        {
            Mask = mask;
        }

        private CheckBox CreateCheckbox(int id)
        {
            var letter = Constants.Alphabet[id];
            var mask = 1 << id;

            var cb = new CheckBox();
            cb.Content = letter.ToString().ToUpper();

            if ((Mask & mask) != 0)
            {
                cb.IsChecked = true;
            }

            cb.Checked += (object sender, RoutedEventArgs e) =>
            {
                VarChanged(id, true);
            };
            cb.Unchecked += (object sender, RoutedEventArgs e) =>
            {
                VarChanged(id, false);
            };


            return cb;
        }

        private void OnMaskUpdated()
        {
            if (_LOADING) return;
            _LOADING = true;
            for (int i = 0; i < 10; i++)
            {
                var mask = 1 << i;
                if ((Mask & mask) != 0)
                {
                    Boxes[i].IsChecked = true;
                }
                else
                {
                    Boxes[i].IsChecked = false;
                }
            }
            _LOADING = false;
        }

        private void VarChanged(int id, bool on)
        {
            if (_LOADING) return;
            _LOADING = true;

            var mask = 1 << id;
            if (on)
            {
                Mask |= (ushort)mask;
            } else
            {
                Mask &= (ushort)~mask;
            }

            _LOADING = false;
            MaskChanged?.Invoke(Mask);
        }


    }
}
