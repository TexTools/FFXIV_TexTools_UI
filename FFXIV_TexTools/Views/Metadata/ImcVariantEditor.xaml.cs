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
using xivModdingFramework.Variants.DataContainers;

namespace FFXIV_TexTools.Views.Metadata
{
    /// <summary>
    /// Interaction logic for ImcVariantEditor.xaml
    /// </summary>
    public partial class ImcVariantEditor : UserControl, INotifyPropertyChanged
    {
        private XivImc _Imc = new XivImc();
        public XivImc ImcEntry
        {
            get => _Imc;
            set
            {
                _Imc = value;

                _LOADING = true;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(MaterialSet)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SoundId)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(AnimationId)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(VfxId)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DecalId)));
                MaskGrid.SetMask(ImcEntry.AttributeMask);
                _LOADING = false;

                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ImcEntry)));
            }
        }
        public byte SoundId
        {
            get => ImcEntry.SoundId;
            set
            {
                ImcEntry.SoundId = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SoundId)));
                OnValueChanged();
            }
        }
        public byte MaterialSet
        {
            get => ImcEntry.MaterialSet;
            set
            {
                ImcEntry.MaterialSet = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(MaterialSet)));
                OnValueChanged();
            }
        }
        public byte AnimationId
        {
            get => ImcEntry.Animation;
            set
            {
                ImcEntry.Animation = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(AnimationId)));
                OnValueChanged();
            }
        }
        public byte VfxId
        {
            get => ImcEntry.Vfx;
            set
            {
                ImcEntry.Vfx = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(VfxId)));
                OnValueChanged();
            }
        }
        public byte DecalId
        {
            get => ImcEntry.Decal;
            set
            {
                ImcEntry.Decal = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DecalId)));
                OnValueChanged();
            }
        }

        public event EventHandler<XivImc> ValueChanged;

        private bool _LOADING;

        public ImcVariantEditor()
        {
            DataContext = this;
            InitializeComponent();
            MaskGrid.SetMask(ImcEntry.AttributeMask);
            MaskGrid.MaskChanged += MaskGrid_MaskChanged;
        }

        public event PropertyChangedEventHandler PropertyChanged;
        private void MaskGrid_MaskChanged(ushort mask)
        {
            ImcEntry.AttributeMask = mask;
            OnValueChanged();
        }

        private void OnValueChanged()
        {
            if (_LOADING) return;
            ValueChanged?.Invoke(this, ImcEntry);
        }
    }
}
