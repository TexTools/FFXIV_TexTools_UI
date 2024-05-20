using FFXIV_TexTools.Resources;
using MahApps.Metro.Controls;
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
using xivModdingFramework.Mods;
using xivModdingFramework.Mods.Enums;

namespace FFXIV_TexTools.Views.Controls
{
    /// <summary>
    /// Interaction logic for FileWrapperControl.xaml
    /// </summary>
    public partial class FileWrapperControl : UserControl
    {
        public FileViewControl FileControl { get; private set; }
        public string FilePath { get; private set; } = "";

        public FileWrapperControl()
        {
            InitializeComponent();

            LoadButton.IsEnabled = false;
            SaveAsGrid.IsEnabled = false;
            SaveButton.IsEnabled = false;
            EnableDisableButton.IsEnabled = false;
        }

        public static FileViewControl GetControlForFile(string file)
        {
            var texHandler = new TextureFileControl();
            if (texHandler.CanLoadFile(file))
            {
                return texHandler;
            }

            return null;
        }


        public async Task<bool> LoadExternalFile(string externalFilePath, string internalFilePath)
        {
            FilePathBox.Text = "Loading File...";
            FilePath = internalFilePath;
            var control = GetControlForFile(externalFilePath);
            if (control == null)
            {
                return false;
            }
            FileControl = control;
            FileControlEntry.Children.Add(FileControl);

            await FileControl.LoadExternalFile(externalFilePath, internalFilePath);
            SetupUi();
            return true;
        }
        public async Task<bool> LoadInternalFile(string internalFilePath)
        {
            FilePathBox.Text = "Loading File...";
            FilePath = internalFilePath;

            if(FileControl != null)
            {
                FileControlEntry.Children.Remove(FileControl);
                FileControl = null;
            }

            var control = GetControlForFile(internalFilePath);
            if (control == null)
            {
                await SetupUi();
                return false;
            }


            FileControl = control;
            FileControlEntry.Children.Add(FileControl);

            await FileControl.LoadInternalFile(internalFilePath);
            await SetupUi();
            return true;
        }

        public async Task ClearFile()
        {
            if (FileControl == null)
            {
                return;
            }

            await FileControl.ClearFile();
            FilePath = "";
            await SetupUi();
        }

        private async Task SetupUi()
        {
            SaveAsContextMenu.Items.Clear();
            if (FileControl == null || string.IsNullOrWhiteSpace(FilePath))
            {
                FilePathBox.Text = "No File Loaded";
                LoadButton.IsEnabled = false;
                SaveAsGrid.IsEnabled = false;
                SaveButton.IsEnabled = false;
                EnableDisableButton.IsEnabled = false;
                return;
            }

            var tx = MainWindow.DefaultTransaction;
            var mod = await tx.GetMod(FilePath);
            if (mod != null)
            {
                var state = await mod.Value.GetState(tx);

                EnableDisableButton.Content = state == xivModdingFramework.Mods.Enums.EModState.Enabled ? UIStrings.Disable : UIStrings.Enable;
                EnableDisableButton.IsEnabled = true;
            }
            else
            {
                EnableDisableButton.Content = UIStrings.Enable;
                EnableDisableButton.IsEnabled = false;
            }

            FilePathBox.Text = FilePath;

            var extensions = FileControl.GetValidFileExtensions();
            foreach(var ext in extensions)
            {
                var mi = new MenuItem();
                mi.Header = ext.Key.ToUpper().Substring(1);
                var extension = ext;
                mi.Click += (object sender, RoutedEventArgs e) =>
                {
                    SaveAsExtension(extension.Key);
                };
                SaveAsContextMenu.Items.Add(mi);
            }

            LoadButton.IsEnabled = true;
            SaveAsGrid.IsEnabled = true;
            SaveButton.IsEnabled = true;
            RefreshButton.IsEnabled = true;
        }

        private async void EnableDisable_Click(object sender, RoutedEventArgs e)
        {
            if (FileControl == null)
            {
                return;
            }
            if (!FileControl.HasFile)
            {
                return;
            }

            var rtx = MainWindow.DefaultTransaction;
            var desiredState = string.Equals(EnableDisableButton.Content, UIStrings.Enable) ? EModState.Enabled : EModState.Disabled;
            var mod = await rtx.GetMod(FilePath);
            if (mod == null)
            {
                return;
            }

            await Modding.SetModState(desiredState, mod.Value, MainWindow.UserTransaction);
        }

        private async void Load_Click(object sender, RoutedEventArgs e)
        {
            if (FileControl == null)
            {
                return;
            }
            await FileControl.LoadFileByDialog();
        }

        private async void SaveAs_Click(object sender, RoutedEventArgs e)
        {
            if (FileControl == null)
            {
                return;
            }

            if (!FileControl.HasFile)
            {
                return;
            }
            await FileControl.SaveAsByDialog();
        }

        private async void SaveAsExtension(string extension)
        {
            if (FileControl == null)
            {
                return;
            }

            if (!FileControl.HasFile)
            {
                return;
            }

            await FileControl.SaveAsByDialog(extension);
        }

        private async void Save_Click(object sender, RoutedEventArgs e)
        {
            if (FileControl == null)
            {
                return;
            }

            if (!FileControl.HasFile)
            {
                return;
            }
            await FileControl.SaveCurrentFile(MainWindow.UserTransaction);
        }

        private void SaveAsDropdown_Click(object sender, RoutedEventArgs e)
        {
            SaveAsContextMenu.PlacementTarget = SaveAsButton;
            SaveAsContextMenu.IsOpen = true;
        }

        private async void FileInfo_Click(object sender, RoutedEventArgs e)
        {
            if (FileControl == null)
            {
                return;
            }
            await FileControl.ReloadFile();
        }

        private async void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            if (FileControl == null)
            {
                return;
            }
            await FileControl.ReloadFile();
        }
    }
}
