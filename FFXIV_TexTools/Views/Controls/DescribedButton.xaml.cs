using System;
using System.Windows;
using System.Windows.Controls;

namespace FFXIV_TexTools.Views.Controls
{
    /// <summary>
    /// Interaction logic for DescribedButton.xaml
    /// </summary>
    public partial class DescribedButton : UserControl
    {
        private string _buttonText;
        private string _descriptionText;
        private string _enabledTooltip;
        private string _disabledTooltip;

        /// <summary>
        /// Fired when the button is clicked.
        /// </summary>
        public event EventHandler Click;

        public string EnabledTooltip
        {
            get
            {
                return _enabledTooltip;
            }
            set
            {
                _enabledTooltip = value;
            }
        }

        public string DisabledTooltip
        {
            get
            {
                return _disabledTooltip;
            }
            set
            {
                _disabledTooltip = value;
            }
        }

        public string Tooltip
        {
            get
            {
                if(IsEnabled)
                {
                    return _enabledTooltip;
                }
                else
                {
                    return _disabledTooltip;
                }
            }
        }
        public string ButtonText { get
            {
                return _buttonText;
            }
            set
            {
                _buttonText = value;
                PrimaryButton.Content = value;
            }
        }
        public string DescriptionText
        {
            get
            {
                return _descriptionText;
            }
            set
            {
                _descriptionText = value;
                DescriptionBox.Text = value;
            }
        }
        public DescribedButton()
        {
            DataContext = this;
            InitializeComponent();

            PrimaryButton.Click += PrimaryButton_Click;
        }

        private void PrimaryButton_Click(object sender, RoutedEventArgs e)
        {
            if(Click != null)
            {
                Click.Invoke(sender, e);
            }
        }
    }
}
