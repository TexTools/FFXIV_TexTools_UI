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
using xivModdingFramework.Cache;
using xivModdingFramework.Items.Interfaces;

namespace FFXIV_TexTools.Views
{
    /// <summary>
    /// Interaction logic for CompleteModpackLevelSelect.xaml
    /// </summary>
    public partial class StandardModpackLevelSelect : Page
    {

        private IItem _item;
        public event EventHandler<XivDependencyLevel> LevelSelected;
        public StandardModpackLevelSelect(IItem item)
        {
            _item = item;
            InitializeComponent();
            EverythingButton.Click += EverythingButton_Click;
            ModelButton.Click += ModelButton_Click;
            MaterialButton.Click += MaterialButton_Click;
            TextureButton.Click += TextureButton_Click;
            BackButton.Click += BackButton_Click;
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            if (LevelSelected != null)
            {
                LevelSelected.Invoke(this, XivDependencyLevel.Invalid);
            }
        }

        private void TextureButton_Click(object sender, EventArgs e)
        {
            if (LevelSelected != null)
            {
                LevelSelected.Invoke(this, XivDependencyLevel.Texture);
            }
        }

        private void MaterialButton_Click(object sender, EventArgs e)
        {
            if (LevelSelected != null)
            {
                LevelSelected.Invoke(this, XivDependencyLevel.Material);
            }
        }

        private void ModelButton_Click(object sender, EventArgs e)
        {
            if (LevelSelected != null)
            {
                LevelSelected.Invoke(this, XivDependencyLevel.Model);
            }
        }

        private void EverythingButton_Click(object sender, EventArgs e)
        {
            if (LevelSelected != null)
            {
                LevelSelected.Invoke(this, XivDependencyLevel.Root);
            }
        }
    }
}
