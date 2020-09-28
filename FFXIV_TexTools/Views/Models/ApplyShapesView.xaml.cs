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
using System.Windows.Shapes;
using xivModdingFramework.Models.DataContainers;

namespace FFXIV_TexTools.Views.Models
{
    /// <summary>
    /// Interaction logic for ApplyShapesView.xaml
    /// </summary>
    public partial class ApplyShapesView : Window
    {
        private List<string> AllShapes = new List<string>();
        public List<string> SelectedShapes;
        private Dictionary<string, CheckBox> ShapeBoxes = new Dictionary<string, CheckBox>();
        public ApplyShapesView(TTModel model, List<string> currentShapes)
        {
            InitializeComponent();

            AllShapes = model.ShapeNames;

            var count = 0;
            foreach(var shape in AllShapes)
            {
                if (shape == "original") continue;

                if(count % 2 == 0)
                {
                    ShapesGrid.Rows++;
                }

                var cb = new CheckBox();

                // Annoying replacement for the fact that WPF eats the first underscore as a special character.
                cb.Content = shape.Replace("_","__");
                cb.Margin = new Thickness(10, 5, 10, 5);

                cb.IsChecked = currentShapes.Contains(shape);
                ShapeBoxes.Add(shape, cb);
                
                ShapesGrid.Children.Add(cb);
                count++;
            }
        }

        private void ApplyButton_Click(object sender, RoutedEventArgs e)
        {
            SelectedShapes = new List<string>();
            foreach(var kv in ShapeBoxes)
            {
                if (kv.Value.IsChecked == true)
                {
                    SelectedShapes.Add(kv.Key);
                }
            }

            DialogResult = true;
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}
