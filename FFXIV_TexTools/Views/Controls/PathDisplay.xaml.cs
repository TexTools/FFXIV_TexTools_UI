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

namespace FFXIV_TexTools.Views.Controls
{
    /// <summary>
    /// Interaction logic for PathDisplay.xaml
    /// </summary>
    public partial class PathDisplay : Window
    {
        public PathDisplay(string title, string path) : this(new List<(string title, string path)>() { (path, title) })
        {

        }
        public PathDisplay(List<(string title, string path)> paths)
        {
            if(this.Owner == null)
            {
                Owner = MainWindow.GetMainWindow();
            }
            WindowStartupLocation = WindowStartupLocation.CenterOwner;

            InitializeComponent();


            var row = 0;
            foreach (var pair in paths)
            {
                var rd = new RowDefinition();
                rd.Height = new GridLength(40);
                PrimaryGrid.RowDefinitions.Add(rd);

                var lb = new Label();
                lb.Margin = new Thickness(5);
                lb.VerticalAlignment = VerticalAlignment.Center;
                lb.HorizontalAlignment = HorizontalAlignment.Right;
                lb.SetValue(Grid.ColumnProperty, 0);
                lb.SetValue(Grid.RowProperty, row);
                lb.Content = pair.title;

                PrimaryGrid.Children.Add(lb);

                var tb = new TextBox();
                tb.IsReadOnly = true;
                tb.Margin = new Thickness(5);
                tb.VerticalAlignment = VerticalAlignment.Center;
                tb.SetValue(Grid.ColumnProperty, 1);
                tb.SetValue(Grid.RowProperty, row);
                tb.Text = pair.path;

                PrimaryGrid.Children.Add(tb);

                row++;
            }

            Height = 80 + (row * 40);


        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
