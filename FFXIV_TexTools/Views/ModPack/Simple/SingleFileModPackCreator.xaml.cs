// FFXIV TexTools
// Copyright © 2019 Rafael Gonzalez - All Rights Reserved
// 
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
// 
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
// 
// You should have received a copy of the GNU General Public License

using FFXIV_TexTools.Helpers;
using FFXIV_TexTools.Properties;
using FFXIV_TexTools.Resources;
using FFXIV_TexTools.ViewModels;
using FolderSelect;
using MahApps.Metro.Controls.Dialogs;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Forms;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shapes;
using xivModdingFramework.Cache;
using xivModdingFramework.General.Enums;
using xivModdingFramework.Mods;
using xivModdingFramework.Mods.DataContainers;
using xivModdingFramework.Mods.Enums;
using xivModdingFramework.Mods.FileTypes;
using xivModdingFramework.SqPack.DataContainers;
using xivModdingFramework.SqPack.FileTypes;
using xivModdingFramework.Textures.Enums;

using Path = System.IO.Path;

namespace FFXIV_TexTools.Views
{
    /// <summary>
    /// Interaction logic for SimpleModPackImporter.xaml
    /// </summary>
    public partial class SingleFileModpackCreator : INotifyPropertyChanged
    {
        private string _File;
        private bool _IncludeChildren = true;
        private string _DestinationPath;

        public event PropertyChangedEventHandler PropertyChanged;

        public string DestinationPath
        {
            get
            {
                return _DestinationPath;
            }
            set
            {
                _DestinationPath = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DestinationPath)));
            }
        }
        public bool IncludeChildren
        {
            get
            {
                return _IncludeChildren;
            }
            set
            {
                _IncludeChildren = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IncludeChildren)));
            }
        }


        public SingleFileModpackCreator(string file)
        {
            _File = file;
            var defaultPath = Path.GetFullPath(Path.Combine(Settings.Default.ModPack_Directory, Path.GetFileNameWithoutExtension(file) + ".ttmp2"));
            DestinationPath = defaultPath;

            DataContext = this;
            InitializeComponent();
        }


        private async Task DoExport()
        {
            try
            {
                var mp = new ModPack();
                mp.Name = Path.GetFileNameWithoutExtension(DestinationPath);
                mp.Author = Settings.Default.Default_Author;
                mp.Version = "1.0";
                mp.Url = "";

                await TTMP.CreateModpackFromFile(_File, DestinationPath, IncludeChildren, mp, MainWindow.DefaultTransaction);
            }
            catch(Exception ex)
            {


                ViewHelpers.ShowError("Export Error", ex.Message);
            }
        }

        /// <summary>
        /// Show the user the export dialog and return after completion.
        /// </summary>
        /// <param name="file"></param>
        /// <returns></returns>
        public static void ExportFile(string file, Window owner = null)
        {
            var wind = new SingleFileModpackCreator(file);
            wind.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            wind.Owner = owner;
            wind.ShowDialog();
        }

        private void SaveAs_Click(object sender, RoutedEventArgs e)
        {
            var sd = new SaveFileDialog();
            sd.Filter = $"TTMP2 Files (*.ttmp2)|*.ttmp2".L();

            sd.FileName = DestinationPath;
            var result = sd.ShowDialog();
            if(result == System.Windows.Forms.DialogResult.OK)
            {
                DestinationPath = sd.FileName;
            }
        }
        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
        private async void Save_Click(object sender, RoutedEventArgs e)
        {
            await DoExport();
            DialogResult = true;
        }
    }
}
