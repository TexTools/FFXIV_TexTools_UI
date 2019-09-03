using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace FFXIV_TexTools.Views
{
    /// <summary>
    /// 自定义自动匹配文本框
    /// </summary>    
    public partial class AutoCompleteTextBox : Canvas
    {
        private VisualCollection controls;
        private TextBox textBox;
        private ComboBox comboBox;
        private delegate void TextChangedCallback();
        private int searchThreshold;

        public AutoCompleteTextBox()
        {
            controls = new VisualCollection(this);
            InitializeComponent();

            searchThreshold = 1;        // default threshold to 2 char

            // set up the text box and the combo box
            comboBox = new ComboBox();
            comboBox.IsSynchronizedWithCurrentItem = true;
            comboBox.IsTabStop = false;
            Panel.SetZIndex(comboBox, -1);
            comboBox.SelectionChanged += new SelectionChangedEventHandler(comboBox_SelectionChanged);

            textBox = new TextBox();
            textBox.TextChanged += new TextChangedEventHandler(textBox_TextChanged);
            textBox.GotFocus += new RoutedEventHandler(textBox_GotFocus);
            textBox.KeyUp += new KeyEventHandler(textBox_KeyUp);
            textBox.KeyDown += new KeyEventHandler(textBox_KeyDown);
            textBox.VerticalContentAlignment = VerticalAlignment.Center;
            var textBoxbinding = new Binding();
            textBoxbinding.Source = this;
            textBoxbinding.Mode = BindingMode.TwoWay;
            textBoxbinding.Path = new PropertyPath("Text");
            textBox.SetBinding(TextBox.TextProperty, textBoxbinding);
            
            controls.Add(comboBox);
            controls.Add(textBox);
        }
        public static readonly DependencyProperty TextProperty = DependencyProperty.Register("Text", typeof(string), typeof(AutoCompleteTextBox),new PropertyMetadata(""));
        public string Text
        {
            get { return (string)GetValue(TextProperty); }
            set
            {
                SetValue(TextProperty, value);
            }
        }

        public int Threshold
        {
            get { return searchThreshold; }
            set { searchThreshold = value; }
        }

        public static readonly DependencyProperty ItemsSourceProperty = DependencyProperty.Register("ItemsSource", typeof(ObservableCollection<AutoCompleteEntry>), typeof(AutoCompleteTextBox));
        public ObservableCollection<AutoCompleteEntry> ItemsSource
        {
            get => (ObservableCollection<AutoCompleteEntry>)GetValue(ItemsSourceProperty);
            set {
                SetValue(ItemsSourceProperty,value);
            }
        }
        private void comboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (null != comboBox.SelectedItem)
            {
                var entry = comboBox.SelectedItem as AutoCompleteEntry;
                this.Text = entry.DisplayName;
                comboBox.IsDropDownOpen = false;
            }
        }
        private void TextChanged()
        {
            this.comboBox.Items.Clear();
            var name = this.textBox.Text.Trim();
            if (name.Length >= searchThreshold)
            {
                var result = this.ItemsSource.Where(it => it.DisplayName.Contains(name)).OrderBy(it => it.DisplayName).Take(20);
                foreach (var item in result)
                {
                    this.comboBox.Items.Add(item);
                }
                comboBox.IsDropDownOpen = comboBox.HasItems;
            }
            else
            {
                comboBox.IsDropDownOpen = false;
            }
        }

        private void textBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            TextChanged();
        }

        public void textBox_GotFocus(object sender, RoutedEventArgs e)
        {
        }

        public void textBox_KeyDown(object sender, KeyEventArgs e)
        {
            //if (textBox.IsInputMethodEnabled == true)
            //{
            //    comboBox.IsDropDownOpen = false;
            //}
        }

        public void textBox_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Down && comboBox.IsDropDownOpen == true)
            {
                comboBox.Focus();
            }
        }

        protected override Size ArrangeOverride(Size arrangeSize)
        {
            textBox.Arrange(new Rect(arrangeSize));
            comboBox.Arrange(new Rect(arrangeSize));
            return base.ArrangeOverride(arrangeSize);
        }

        protected override Visual GetVisualChild(int index)
        {
            return controls[index];
        }

        protected override int VisualChildrenCount
        {
            get { return controls.Count; }
        }

    }
}
