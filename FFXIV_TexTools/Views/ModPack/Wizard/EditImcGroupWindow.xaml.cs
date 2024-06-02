using FFXIV_TexTools.Views.Wizard;
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
using System.Windows.Shapes;

namespace FFXIV_TexTools.Views.Wizard
{
    /// <summary>
    /// Interaction logic for ImcGroupEditWindow.xaml
    /// </summary>
    public partial class EditImcGroupWindow : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private WizardGroupEntry Group;
        public EditImcGroupWindow(WizardGroupEntry g)
        {
            Group = g;
            DataContext = this;
            InitializeComponent();
        }

    }
}
