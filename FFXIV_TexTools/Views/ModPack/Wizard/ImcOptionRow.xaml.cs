using SixLabors.ImageSharp.Formats.Png;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using UserControl = System.Windows.Controls.UserControl;

namespace FFXIV_TexTools.Views.Wizard
{
    public class WrappedImcOption : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        public WizardOptionEntry Option;


        public string Name
        {
            get => Option.Name;
            set
            {
                Option.Name = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Name)));
            }
        }
        public string Description
        {
            get => Option.Description;
            set
            {
                Option.Description = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Description)));
            }
        }
        public ushort Mask
        {
            get => Option.ImcData.AttributeMask;
            set
            {
                Option.ImcData.AttributeMask = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Mask)));
                MaskChanged?.Invoke(this);
            }
        }
        public bool IsDisableOption
        {
            get => Option.ImcData.IsDisableOption;
            set
            {
                Option.ImcData.IsDisableOption = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsDisableOption)));
            }
        }

        private ImageSource _Image;
        public ImageSource Image
        {
            get => _Image;
            set
            {
                _Image = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Image)));
            }
        }

        public event Action<WrappedImcOption> RemoveRequested;

        public event Action<WrappedImcOption> MaskChanged;

        public event Action<WrappedImcOption> MoveUpRequested;
        public event Action<WrappedImcOption> MoveDownRequested;

        public void RequestRemove()
        {
            RemoveRequested?.Invoke(this);
        }
        public void RequestMoveUp()
        {
            MoveUpRequested?.Invoke(this);
        }
        public void RequestMoveDown()
        {
            MoveDownRequested?.Invoke(this);
        }
        public WrappedImcOption(WizardOptionEntry option)
        {
            Option = option;
        }
    }

    /// <summary>
    /// Interaction logic for ImcOptionRow.xaml
    /// </summary>
    public partial class ImcOptionRow : UserControl
    {
        public WrappedImcOption Option
        {
            get
            {
                return DataContext as WrappedImcOption;
            }
        }

        public ImcOptionRow()
        {
            InitializeComponent();
            MaskGrid.MaskChanged += MaskGrid_MaskChanged;

            if (Option == null) return;
            Option.Image = ViewHelpers.SafeBitmapFromFile(Option.Option.Image);
            MaskGrid.SetMask(Option.Mask);
        }

        private bool _SETTING_MASK;
        private void MaskGrid_MaskChanged(ushort obj)
        {
            if (_SETTING_MASK) return;
            Option.Mask = obj;
        }
        private void Option_MaskChanged(WrappedImcOption obj)
        {
            _SETTING_MASK = true;
            MaskGrid.SetMask(Option.Mask);
            _SETTING_MASK = false;
        }

        private void Delete_Click(object sender, RoutedEventArgs e)
        {
            Option.RequestRemove();
        }

        private void AddImage_Click(object sender, RoutedEventArgs e)
        {
            var res = ViewHelpers.LoadUserImage(this);
            Option.Option.Image = res.File;
            Option.Image = res.Image;
        }

        private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (Option == null) return;
            Option.Image = ViewHelpers.SafeBitmapFromFile(Option.Option.Image);
            Option.MaskChanged += Option_MaskChanged;
            MaskGrid.SetMask(Option.Mask);
        }

        private void MoveOptionDownButton_Click(object sender, RoutedEventArgs e)
        {
            Option.RequestMoveDown();
        }

        private void MoveOptionUpButton_Click(object sender, RoutedEventArgs e)
        {
            Option.RequestMoveUp();
        }
    }
}
