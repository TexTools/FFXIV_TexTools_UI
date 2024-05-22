using FFXIV_TexTools.Resources;
using MahApps.Metro.Controls;
using MahApps.Metro.Controls.Dialogs;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
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
using xivModdingFramework.Items.Interfaces;
using xivModdingFramework.Mods;
using xivModdingFramework.Mods.Enums;

namespace FFXIV_TexTools.Views.Controls
{
    /// <summary>
    /// Interaction logic for FileWrapperControl.xaml
    /// </summary>
    public partial class FileWrapperControl : UserControl, INotifyPropertyChanged
    {
        public FileViewControl FileControl { get; private set; }
        public string FilePath { get; private set; } = "";

        public string EnableDisableText
        {
            get
            {
                if(FileControl == null)
                {
                    return UIStrings.Enable;
                }
                return FileControl.ModState == EModState.Enabled ? UIStrings.Disable : UIStrings.Enable;
            }
        }
        public bool EnableDisableEnabled
        {
            get
            {
                if (FileControl == null)
                {
                    return false;
                }
                if(FileControl.ModState == EModState.UnModded)
                {
                    return false;
                }
                return true;
            }
        }

        public string UnsavedChangesText
        {
            get
            {
                if(FileControl == null)
                {
                    return "";
                }
                
                if(!FileControl.UnsavedChanges)
                {
                    return "";
                }

                return "(!)";
            }
        }
        public string SaveTooltip
        {
            get
            {
                if (FileControl == null)
                {
                    return UIStrings.SaveTooltip;
                }

                if (!FileControl.UnsavedChanges)
                {
                    return UIStrings.SaveTooltip;
                }

                return UIStrings.SaveTooltip + "\n(You currently have unsaved changes!)";
            }
        }

        public bool UnsavedChanges { get
            {
                if (FileControl == null)
                {
                    return false;
                }

                return FileControl.UnsavedChanges;
            }
            set
            {
                if(FileControl == null)
                {
                    return;
                }
                FileControl.UnsavedChanges = value;
            }
        }

        public FileWrapperControl()
        {
            DataContext = this;
            InitializeComponent();

            LoadButton.IsEnabled = false;
            SaveAsGrid.IsEnabled = false;
            SaveButton.IsEnabled = false;

            SaveText.Text = TxWatcher.SaveLabel;

            TxWatcher.SaveStatusChanged += TxWatcher_SaveStatusChanged;
        }

        private void TxWatcher_SaveStatusChanged(bool allowed, string text)
        {
            SaveText.Text = TxWatcher.SaveLabel;
            if (FileControl == null || string.IsNullOrWhiteSpace(FilePath))
            {
                SaveButton.IsEnabled = false;
            }

            SaveButton.IsEnabled = TxWatcher.SaveAllowed;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public static FileViewControl GetControlForFile(string file, bool forceColorsetEditor = false)
        {

            var texHandler = new TextureFileControl();
            if (texHandler.CanLoadFile(file))
            {
                return texHandler;
            }
            var mdlHandler = new ModelFileControl();
            if (mdlHandler.CanLoadFile(file))
            {
                return mdlHandler;
            }

            if (forceColorsetEditor)
            {
                var colorsetHandler = new ColorsetFileControl();
                if (colorsetHandler.CanLoadFile(file))
                {
                    return colorsetHandler;
                }
            }

            var mtrlHandler = new MaterialFileControl();
            if (mtrlHandler.CanLoadFile(file))
            {
                return mtrlHandler;
            }
            var metaHandler = new MetadataFileControl();
            if (metaHandler.CanLoadFile(file))
            {
                return metaHandler;
            }


            return null;
        }

        public bool SetControlType(Type t)
        {
            try
            {
                if (FileControl != null)
                {
                    _ = FileControl.ClearFile();
                    FileControlEntry.Children.Remove(FileControl);
                    FileControl = null;
                }

                FileControl = (FileViewControl)Activator.CreateInstance(t);
                FileControlEntry.Children.Add(FileControl);
                FileControl.PropertyChanged += FileControl_PropertyChanged;
            }
            catch
            {
                return false;
            }
            return true;
        }

        public async Task<bool> LoadExternalFile(string externalFilePath, string internalFilePath, IItem referenceItem = null, bool reloadControl = true, bool forceColorsetEditor = false)
        {
            if (string.IsNullOrWhiteSpace(externalFilePath) || string.IsNullOrWhiteSpace(internalFilePath))
            {
                return false;
            }

            if (!reloadControl && FileControl == null)
            {
                return false;
            }

            try
            {
                FilePathBox.Text = "Loading File...";
                FilePath = internalFilePath;
                if (reloadControl)
                {
                    if (FileControl != null)
                    {
                        FileControlEntry.Children.Remove(FileControl);
                        FileControl = null;
                    }

                    var control = GetControlForFile(internalFilePath, forceColorsetEditor);
                    if (control == null)
                    {
                        await SetupUi();
                        return false;
                    }
                    FileControl = control;
                    FileControlEntry.Children.Add(FileControl);
                    FileControl.PropertyChanged += FileControl_PropertyChanged;
                }
                else
                {
                    if (!FileControl.CanLoadFile(internalFilePath))
                    {
                        return false;
                    }
                }



                await FileControl.LoadExternalFile(externalFilePath, internalFilePath, referenceItem);
                await SetupUi();
                return true;
            }
            catch
            {
                return false;
            }
        }


        public async Task<bool> LoadInternalFile(string internalFilePath, IItem referenceItem = null, byte[] data = null, bool reloadControl = true, bool forceColorsetEditor = false)
        {

            if (!reloadControl && FileControl == null)
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(internalFilePath))
            {
                await ClearFile();
                return false;
            }

            try
            {
                FilePathBox.Text = "Loading File...";
                FilePath = internalFilePath;

                if (reloadControl)
                {
                    if (FileControl != null)
                    {
                        FileControlEntry.Children.Remove(FileControl);
                        FileControl = null;
                    }

                    var control = GetControlForFile(internalFilePath, forceColorsetEditor);
                    if (control == null)
                    {
                        await SetupUi();
                        return false;
                    }
                    FileControl = control;
                    FileControlEntry.Children.Add(FileControl);
                    FileControl.PropertyChanged += FileControl_PropertyChanged;
                }
                else
                {
                    if (!FileControl.CanLoadFile(internalFilePath))
                    {
                        await ClearFile();
                        return false;
                    }
                }


                await FileControl.LoadInternalFile(internalFilePath, false, referenceItem, data);
                await SetupUi();
                return true;
            }
            catch
            {
                return false;
            }
        }
        private void FileControl_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(FileControl.UnsavedChanges))
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(UnsavedChangesText)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SaveTooltip)));
            }

            if(e.PropertyName == nameof(FileControl.ModState))
            {
                // Do thing
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(EnableDisableEnabled)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(EnableDisableText)));
            }
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
                return;
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

            var mpItem = new MenuItem();
            mpItem.Header = "Modpack";
            mpItem.Click += (object sender, RoutedEventArgs e) =>
            {
                SaveAsModpack();
            };
            SaveAsContextMenu.Items.Add(mpItem);

            LoadButton.IsEnabled = true;
            SaveAsGrid.IsEnabled = true;
            SaveButton.IsEnabled = TxWatcher.SaveAllowed;

            var tx = TxWatcher.DefaultTransaction;
            if (await tx.FileExists(FilePath) || FilePath.EndsWith(".meta"))
            {
                RefreshButton.IsEnabled = true;
                PopOutButton.IsEnabled = true;
            } else
            {
                RefreshButton.IsEnabled = false;
                PopOutButton.IsEnabled = false;
            }
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
            SaveFile();
        }

        private async Task SaveFile(ModTransaction tx = null)
        {
            if (FileControl == null)
            {
                return;
            }

            if (!FileControl.HasFile)
            {
                return;
            }

            if(tx == null)
            {
                tx = MainWindow.UserTransaction;
            }

            await FileControl.SaveCurrentFile(tx);
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

        private async void SaveAsModpack()
        {
            var wind = ((MetroWindow)Window.GetWindow(this));
            var controller = await wind.ShowProgressAsync("Exporting Modpack...", "Please wait...");
            try
            {
                var tx = MainWindow.UserTransaction;
                var ownTx = false;
                TxFileState state = null;
                if (tx == null)
                {
                    ownTx = true;
                    tx = ModTransaction.BeginTransaction(true);
                }
                else
                {
                    state = await tx.SaveFileState(FilePath);
                }
                try
                {
                    // Due to the nature of the single file modpack exporter, we need to temp save the file here.
                    await SaveFile(tx);

                    SingleFileModpackCreator.ExportFile(FilePath, Window.GetWindow(this), tx);
                }
                finally
                {
                    // Cancel or rollback after as needed.
                    if (ownTx)
                    {
                        ModTransaction.CancelTransaction(tx, true);
                    }
                    else
                    {
                        await tx.RestoreFileState(state);
                    }
                }
            }
            catch(Exception ex)
            {
                this.ShowError("Modpack Export Error", "An error occured while trying to export the file:\n\n" + ex.Message);
            }
            finally
            {
                await controller.CloseAsync();
            }

        }

        private void OpenFolder_Click(object sender, RoutedEventArgs e)
        {
            if(FileControl == null)
            {
                return;
            }

            var dir = FileControl.GetDefaultSaveDirectory();
            Directory.CreateDirectory(dir);

            Process.Start(dir);
        }

        private async void PopOut_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                await SimpleFileViewWindow.OpenFile(FilePath);
            }
            catch { 
                // No-Op
            }
        }
    }
}
