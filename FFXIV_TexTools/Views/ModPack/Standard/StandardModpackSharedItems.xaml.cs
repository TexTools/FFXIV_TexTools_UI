using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using xivModdingFramework.Cache;
using xivModdingFramework.Items.Interfaces;

namespace FFXIV_TexTools.Views
{
    /// <summary>
    /// Interaction logic for StandardModpackSharedItems.xaml
    /// </summary>
    public partial class StandardModpackSharedItems : Page
    {
        public event EventHandler<bool> ConfirmedSharedItems;

        private IItem _item;
        private XivDependencyLevel _level;

        public StandardModpackSharedItems(IItem item, XivDependencyLevel level)
        {
            _item = item;
            _level = level;

            InitializeComponent();

            ItemName.Content = _item.Name + " - " + StandardModpackCreator.GetNiceLevelName(_level) + " Level";
            ItemLevel.Content = "Review Affected Items";

            NextButton.Click += NextButton_Click;
            BackButton.Click += BackButton_Click;

            LoadItems();
        }

        private async Task LoadItems()
        {
            var im = (IItemModel)_item;
            var root = _item.GetRoot();

            List<IItemModel> items;
            if(_level == XivDependencyLevel.Model || _level == XivDependencyLevel.Root)
            {
                items = await im.GetSharedModelItems();
            } else
            {
                items = await im.GetSharedMaterialItems();
            }

            foreach(var i in items)
            {
                SharedItemsListBox.Items.Add(i.Name);
            }
            SharedItemsLabel.Content = "Modifications at the " + StandardModpackCreator.GetNiceLevelName(_level) + " level will affect (at least) the following [" + items.Count + "] item(s):";
            
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            if (ConfirmedSharedItems != null)
            {
                ConfirmedSharedItems.Invoke(this, false);
            }
        }

        private void NextButton_Click(object sender, RoutedEventArgs e)
        {
            if(ConfirmedSharedItems != null)
            {
                ConfirmedSharedItems.Invoke(this, true);
            }
        }
    }
}
