using FFXIV_TexTools.Views;
using System.Windows.Controls;
using System.Windows.Media;

namespace FFXIV_TexTools.Controls
{
    /// <summary>
    /// Interaction logic for ColorsetRowControl.xaml
    /// </summary>
    public partial class ColorsetRowControl : UserControl
    {
        public ColorsetRowControl(int id)
        {
            InitializeComponent();

            RowNumber.Content = ViewHelpers.ColorsetRowToNiceName(id);
            DataContext = id;
        }

        public ImageSource RowImageSource
        {
            get => RowImage.Source;
            set => RowImage.Source = value;
        }
    }
}
