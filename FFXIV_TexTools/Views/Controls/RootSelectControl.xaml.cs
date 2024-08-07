﻿using FFXIV_TexTools.Views.Controls;
using FFXIV_TexTools.Views.ItemConverter;
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
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using xivModdingFramework.Cache;
using xivModdingFramework.Items;
using xivModdingFramework.Items.Interfaces;
using xivModdingFramework.Mods;
using UserControl = System.Windows.Controls.UserControl;

namespace FFXIV_TexTools.Views.Controls
{
    /// <summary>
    /// Interaction logic for RootSelectControl.xaml
    /// </summary>
    public partial class RootSelectControl : UserControl, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        public static readonly DependencyProperty RootProperty = DependencyProperty.Register(nameof(Root), 
            typeof(XivDependencyRoot), 
            typeof(RootSelectControl),
            new PropertyMetadata(RootChangedCallback));
        public XivDependencyRoot Root
        {
            get { return (XivDependencyRoot)GetValue(RootProperty); }
            set { SetValue(RootProperty, value); }
        }

        public Func<IItem, XivDependencyRoot, bool> ItemSelect = ItemSelectFunc;
        public Func<IItem, XivDependencyRoot, bool> ItemFilter = ItemFilterFunc;

        public string LabelText
        {
            get {
                if(Root == null)
                {
                    return "No Item Selected";
                }
                return Root.Info.GetBaseFileName();
            }
        }

        public RootSelectControl()
        {
            InitializeComponent();
            ItemLabel.DataContext = this;
        }

        private static void RootChangedCallback(DependencyObject sender, DependencyPropertyChangedEventArgs e)
        {
            var c = sender as RootSelectControl;
            if (c != null && e != null)
            {
                c.PropertyChanged?.Invoke(c, new PropertyChangedEventArgs(nameof(LabelText)));
            }
        }

        private void SelectButton_Click(object sender, RoutedEventArgs e)
        {
            var item = PopupItemSelection.ShowItemSelection(ItemSelect, ItemFilter, this);
            if (item == null) return;

            var root = item.GetRoot();
            if (root == null) return;

            Root = root;
        }

        public static bool ItemSelectFunc(IItem item, XivDependencyRoot root)
        {
            if (item == null) return false;
            if(root == null) return false;
            return true;
        }

        public static bool ItemFilterFunc(IItem item, XivDependencyRoot root)
        {
            if (item == null) return false;
            if (root == null) return false;
            return true;
        }
    }
}
