﻿using FFXIV_TexTools.Resources;
using FFXIV_TexTools.Views.Item;
using Ionic.Zip;
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
using xivModdingFramework.Items.Interfaces;
using xivModdingFramework.Mods;
using xivModdingFramework.Mods.Enums;

namespace FFXIV_TexTools.Views.Controls
{
    /// <summary>
    /// Interaction logic for FileWrapperControl.xaml
    /// </summary>
    public partial class FileWrapperControl : UserControl, INotifyPropertyChanged, IDisposable
    {
        private FileViewControl _FileControl;
        public FileViewControl FileControl { get => _FileControl;
            protected set
            {
                if(_FileControl != null)
                {
                    _FileControl.KeyDown -= OnKeyDown;
                }
                _FileControl = value;
                if (_FileControl != null)
                {
                    _FileControl.KeyDown += OnKeyDown;
                }

                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(FileControl)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SaveDropdownVisibility)));
            }
        }


        private string _FilePath;
        public string FilePath { get
                => _FilePath;
            private set
            {
                _FilePath = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(FilePath)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SaveDropdownVisibility)));
            }
        }

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

        private bool _SaveEnabled;
        private bool disposedValue;

        public bool SaveEnabled
        {
            get
            {
                return _SaveEnabled && TxWatcher.SaveAllowed;
            }
            set
            {
                _SaveEnabled = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SaveEnabled)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(EnableDisableEnabled)));
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
                return true && SaveEnabled;
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
        public Visibility SaveDropdownVisibility
        {
            get
            {
                if(FileControl == null)
                {
                    return Visibility.Collapsed;
                }
                if(!FileControl.HasAdditionalSaveFunctions())
                {
                    return Visibility.Collapsed;
                }
                return Visibility.Visible;
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

        public bool CopyPasteEnabled
        {
            get {

                if (FileControl == null || !FileControl.HasFile || string.IsNullOrWhiteSpace(FileControl.InternalFilePath))
                {
                    return false;
                }
                return true;
            }
        }

        public FileWrapperControl()
        {
            DataContext = this;
            InitializeComponent();

            LoadButton.IsEnabled = false;
            SaveAsGrid.IsEnabled = false;
            SaveEnabled = false;

            SaveText.Text = TxWatcher.SaveLabel;

            TxWatcher.SaveStatusChanged += TxWatcher_SaveStatusChanged;
            KeyDown += OnKeyDown;

            DragOver += OnDragOver;
            Drop += OnDragDrop;
            PreviewDragOver += OnPreviewDragOver;
            PreviewDragEnter += OnPreviewDragEnter;
            AllowDrop = true;
        }

        private bool DragOk(DragEventArgs e)
        {
            if (!LoadButton.IsEnabled)
            {
                return false;
            }
            if (FileControl == null || FileControl.InternalFilePath == null)
            {
                return false;
            }
            string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);

            if (files.Length == 0)
            {
                return false;
            }

            if (files.Length > 1)
            {
                return false;
            }

            var file = files[0];
            var ext = Path.GetExtension(file);
            if (!FileControl.GetValidFileExtensions().Keys.Contains(ext))
            {
                return false;
            }
            return true;
        }

        private void OnPreviewDragEnter(object sender, DragEventArgs e)
        {
            if (DragOk(e))
            {
                e.Effects = DragDropEffects.Copy;
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }
        }

        private void OnPreviewDragOver(object sender, DragEventArgs e)
        {
            if (DragOk(e))
            {
                e.Effects = DragDropEffects.Copy;
                e.Handled = true;
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }
        }

        private void OnDragOver(object sender, DragEventArgs e)
        {
            if (DragOk(e))
            {
                e.Effects = DragDropEffects.Copy;
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }

        }

        private async void OnDragDrop(object sender, DragEventArgs e)
        {
            try
            {
                if (!DragOk(e))
                {
                    return;
                }

                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                if(files.Length != 1)
                {
                    return;
                }

                var file = files[0];
                var ext = Path.GetExtension(file);
                if(!FileControl.GetValidFileExtensions().Keys.Contains(ext))
                {
                    return;
                }

                var success = await FileControl.LoadExternalFile(file);
                if (!success)
                {
                    await FileControl.ReloadFile();
                }
            }
            catch(Exception ex)
            {
                this.ShowError("File Load Error", "An error occurred while loading the file:\n\n" + ex.Message);
            }
        }

        public void OnKeyDown(object sender, KeyEventArgs e)
        {
            if (Keyboard.Modifiers == ModifierKeys.Control) {
                if (e.Key == Key.S)
                {
                    if (SaveButton.IsEnabled)
                    {
                        _ = SaveFile();
                    }
                    e.Handled = true;
                }
                else if (e.Key == Key.O || e.Key == Key.E)
                {
                    if (LoadButton.IsEnabled)
                    {
                        if (FileControl == null)
                        {
                            return;
                        }
                        _ = FileControl.LoadFileByDialog();
                    }
                    e.Handled = true;
                }
                else if (e.Key == Key.D)
                {
                    if (EnableDisableButton.IsEnabled)
                    {
                        _ = EnableDisable();
                    }
                    e.Handled = true;
                }
                else if (e.Key == Key.A)
                {
                    if (EnableDisableButton.IsEnabled)
                    {
                        if (FileControl == null)
                        {
                            return;
                        }
                        _ = FileControl.SaveAsByDialog();
                    }
                    e.Handled = true;
                }
                else if (e.Key == Key.R)
                {
                    if (RefreshButton.IsEnabled)
                    {
                        if (FileControl == null)
                        {
                            return;
                        }
                        _ = FileControl.ReloadFile();
                    }
                    e.Handled = true;
                }
                else if (e.Key == Key.C)
                {
                    _ = Copy();
                    e.Handled = true;
                }
                else if (e.Key == Key.V)
                {
                    _ = Paste();
                    e.Handled = true;
                }
                else if (e.Key == Key.Q)
                {
                    _ = PopOutFile();
                    e.Handled = true;
                }
                else
                {
                    if(FileControl != null)
                    {
                        FileControl.OnControlKey(sender, e);
                    }
                }
            }
        }

        private void TxWatcher_SaveStatusChanged(bool allowed, string text)
        {
            try
            {
                // Refresh this value to indicate a change occurred.
                Dispatcher.Invoke(() =>
                {
                    // We don't know what event thread we're propogating from, so this needs to be dispatcher invoked.
                    SaveText.Text = TxWatcher.SaveLabel;
                    SaveEnabled = _SaveEnabled;
                });
            }
            catch(Exception ex) 
            {
                // No-Op
                Trace.WriteLine(ex);
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public static async Task<FileViewControl> GetControlForFile(string file, byte[] data = null, Type forcedType = null)
        {
            var tx = MainWindow.DefaultTransaction;

            if (forcedType != null)
            {
                var obj = Activator.CreateInstance(forcedType) as FileViewControl;
                if (obj == null)
                {
                    return null;
                }
                else if (!await obj.CanLoadFile(file, data, tx))
                {
                    obj.Dispose();
                    return null;
                }
                else if (obj != null)
                {
                    return obj;
                }
            }

            FileViewControl handler = new TextureFileControl();
            if (await handler.CanLoadFile(file, data, tx))
            {
                return handler;
            }
            handler.Dispose();

            handler = new ModelFileControl();
            if (await handler.CanLoadFile(file, data, tx))
            {
                return handler;
            }
            handler.Dispose();

            handler = new MaterialFileControl();
            if (await handler.CanLoadFile(file, data, tx))
            {
                return handler;
            }
            handler.Dispose();

            handler = new MetadataFileControl();
            if (await handler.CanLoadFile(file, data, tx))
            {
                return handler;
            }
            handler.Dispose();


            return null;
        }

        public bool SetControlType(Type t)
        {
            try
            {
                if (FileControl != null)
                {
                    FileControl.ClearFile();
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

        private void DisposeFileControl()
        {
            if(FileControl == null)
            {
                return;
            }

            if (FileControlEntry != null && FileControlEntry.Children != null && FileControlEntry.Children.Contains(FileControl))
            {
                FileControlEntry.Children.Remove(FileControl);
            }

            FileControl.PropertyChanged -= FileControl_PropertyChanged;
            FileControl.Dispose();
            FileControl = null;
        }
        
        public async Task<bool> LoadExternalFile(string externalFilePath, string internalFilePath, IItem referenceItem = null, bool reloadControl = true, Type forcedControlType = null)
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
                        DisposeFileControl();
                    }

                    var control = await GetControlForFile(internalFilePath, null, forcedControlType);
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
                    var tx = MainWindow.DefaultTransaction;
                    if (!await FileControl.CanLoadFile(internalFilePath, null, tx))
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

        public async Task<bool> LoadInternalFile(string internalFilePath, IItem referenceItem = null, byte[] data = null, bool reloadControl = true, Type forcedControlType = null)
        {

            this.IsEnabled = false;
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
                        DisposeFileControl();
                    }

                    var control = await GetControlForFile(internalFilePath, data, forcedControlType);
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
                    if (!await FileControl.CanLoadFile(internalFilePath, data, MainWindow.DefaultTransaction))
                    {
                        await ClearFile();
                        return false;
                    }
                }


                await FileControl.LoadInternalFile(internalFilePath, false, referenceItem, data);
                await SetupUi();

                this.IsEnabled = true;
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

            FileControl.ClearFile();
            FilePath = "";
            await SetupUi();
        }

        private async Task SetupUi()
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SaveDropdownVisibility)));
            SaveContextMenu.Items.Clear();
            SaveAsContextMenu.Items.Clear();
            if (FileControl == null || string.IsNullOrWhiteSpace(FilePath))
            {
                FilePathBox.Text = "No File Loaded";
                LoadButton.IsEnabled = false;
                SaveAsGrid.IsEnabled = false;
                SaveEnabled = false;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CopyPasteEnabled)));
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
                try
                {
                    SaveAsModpack();
                } catch(Exception ex)
                {
                    Trace.WriteLine(ex);
                }
            };
            SaveAsContextMenu.Items.Add(mpItem);

            LoadButton.IsEnabled = true;
            SaveAsGrid.IsEnabled = true;
            SaveEnabled = true;

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
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CopyPasteEnabled)));
        }

        private async Task SetupSaveContextMenu()
        {
            var saveOptions = await FileControl.GetAdditionalSaveFunctions();

            SaveContextMenu.Items.Clear();
            foreach (var option in saveOptions)
            {
                var mi = new MenuItem();
                mi.Header = option.Name;
                mi.IsEnabled = option.Enabled;
                var func = option.Function;
                mi.Click += async (object sender, RoutedEventArgs e) =>
                {
                    try
                    {
                        await func.Invoke();
                    }
                    catch (Exception ex)
                    {
                        Trace.WriteLine(ex);
                    }
                };
                SaveContextMenu.Items.Add(mi);
            }
        }

        private void EnableDisable_Click(object sender, RoutedEventArgs e)
        {
            _ = EnableDisable();
        }

        private async Task EnableDisable()
        {
            if (!this.CheckFileWrite())
            {
                return;
            }

            try
            {
                TxWatcher.DisableSave();

                if (FileControl == null)
                {
                    return;
                }
                if (!FileControl.HasFile)
                {
                    return;
                }

                var targetState = string.Equals(EnableDisableButton.Content, UIStrings.Enable);
                await Task.Run((Func<Task>)(async () =>
                {
                    var rtx = MainWindow.DefaultTransaction;
                    var desiredState = targetState ? EModState.Enabled : EModState.Disabled;
                    var mod = await rtx.GetMod(FilePath);
                    if (mod == null)
                    {
                        return;
                    }

                    await Modding.SetModState(desiredState, mod.Value, MainWindow.UserTransaction);
                }));
            }
            catch (Exception ex)
            {
                this.ShowError("File Save Error", "An error occurred while saving the file:\n\n" + ex.Message);
            }
            finally
            {
                TxWatcher.EnableSave();
            }
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
            await SaveFile();
        }

        private async Task SaveFile(ModTransaction tx = null, bool ignoreWarning = false)
        {
            if ((!ignoreWarning) && !this.CheckFileWrite(tx))
            {
                return;
            }

            try
            {
                TxWatcher.DisableSave();

                if (FileControl == null)
                {
                    return;
                }

                if (!FileControl.HasFile)
                {
                    return;
                }


                if (tx == null)
                {
                    tx = MainWindow.UserTransaction;
                }

                await FileControl.SaveCurrentFile(tx, ignoreWarning);
            }
            catch(Exception ex)
            {
                // These should already be handled, but safety is good whenever dealing with an async void call.
                Trace.WriteLine(ex);
            }
            finally
            {
                TxWatcher.EnableSave();
            }
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
            var unsaved = UnsavedChanges;
            try
            {
                var tx = MainWindow.UserTransaction;
                var boiler = await TxBoiler.BeginWrite(tx, true, null, true);
                tx = boiler.Transaction;
                try
                {
                    // Due to the nature of the single file modpack exporter, we need to temp save the file here.
                    await SaveFile(tx, true);

                    SingleFileModpackCreator.ExportFile(FilePath, Window.GetWindow(this), tx);
                }
                finally
                {
                    await boiler.Cancel(true);
                }
            }
            catch(Exception ex)
            {
                this.ShowError("Modpack Export Error", "An error occured while trying to export the file:\n\n" + ex.Message);
            }
            finally
            {

                UnsavedChanges = unsaved;
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
            _ = PopOutFile();
        }

        public async Task PopOutFile()
        {

            try
            {
                if (FileControl != null)
                {
                    await SimpleFileViewWindow.OpenFile(FilePath, FileControl.ReferenceItem, null, null, Window.GetWindow(this));
                }
            }
            catch
            {
                // No-Op
            }
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                TxWatcher.SaveStatusChanged -= TxWatcher_SaveStatusChanged;

                if (disposing)
                {
                    DisposeFileControl();
                }

                disposedValue = true;
            }
        }

        public bool HandleUnsaveConfirmation()
        {
            if (FileControl == null) return true;
            if (!FileControl.HasFile) return true;

            return FileControl.ConfirmDiscardChanges(FileControl.InternalFilePath);
        }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        private async void SaveDropdown_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                await SetupSaveContextMenu();
                SaveContextMenu.PlacementTarget = SaveButton;
                SaveContextMenu.IsOpen = true;
            }
            catch(Exception ex)
            {
                Trace.WriteLine(ex);
            }
        }

        private void Copy_Click(object sender, RoutedEventArgs e)
        {
            _ = Copy();
        }

        private void Paste_Click(object sender, RoutedEventArgs e)
        {
            _ = Paste();
        }

        public async Task Copy()
        {
            if(FileControl == null || !FileControl.HasFile || string.IsNullOrWhiteSpace(FileControl.InternalFilePath))
            {
                return;
            }

            var data = await FileControl.GetUncompressedData();

            var ext = Path.GetExtension(FileControl.InternalFilePath).ToLower();
            Clipboard.SetData(ext, data);
        }
        public async Task Paste()
        {
            if (FileControl == null || !FileControl.HasFile)
            {
                return;
            }
            var ext = Path.GetExtension(FileControl.InternalFilePath).ToLower();
            var raw = Clipboard.GetData(ext);
            var asBytes = raw as byte[];

            if (asBytes == null) return;

            try
            {
                await FileControl.LoadRawData(asBytes);
            }
            catch(Exception ex)
            {
                this.ShowError("Paste Error", "An error occurred while pasting the file:\n\n" + ex.Message);
            }
        }
    }
}
