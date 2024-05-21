using FFXIV_TexTools.Properties;
using FFXIV_TexTools.Resources;
using MahApps.Metro.Controls;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using xivModdingFramework.Cache;
using xivModdingFramework.Helpers;
using xivModdingFramework.Items.Interfaces;
using xivModdingFramework.Mods;
using xivModdingFramework.Mods.Enums;
using xivModdingFramework.SqPack.FileTypes;

namespace FFXIV_TexTools.Views.Controls
{

    public enum EFileViewType
    {
        Viewer,
        Editor
    };

    public abstract class FileViewControl : System.Windows.Controls.UserControl, INotifyPropertyChanged, IDisposable
    {
        private EFileViewType _ViewType;
        public EFileViewType ViewType { get
            {
                return _ViewType;
            }
            protected set
            {
                _ViewType = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ViewType)));
            }
        }

        private bool _UnsavedChanges;
        public bool UnsavedChanges
        {
            get => _UnsavedChanges;
            protected set
            {
                _UnsavedChanges = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(UnsavedChanges)));
            }
        }

        private EModState _ModState;
        public EModState ModState
        {
            get => _ModState;
            protected set
            {
                _ModState = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ModState)));
            }
        }

        private string _InternalFilePath;
        public string InternalFilePath { get
            {
                return _InternalFilePath;
            }
            protected set
            {
                _InternalFilePath = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(InternalFilePath)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HasFile)));
            }
        }

        private string _ExternalFilePath;
        public string ExternalFilePath {
            get
            {
                return _ExternalFilePath;
            }
            protected set
            {
                _ExternalFilePath = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ExternalFilePath)));
            }
        }

        private IItem _ReferenceItem;
        public IItem ReferenceItem
        {
            get
            {
                return _ReferenceItem;
            }
            protected set
            {
                _ReferenceItem = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ReferenceItem)));
            }
        }

        public bool HasFile { get
            {
                return !string.IsNullOrWhiteSpace(InternalFilePath);
            } 
        }

        public FileViewControl()
        {
            ModTransaction.FileChangedOnCommit += ModTransaction_FileChanged;
            if(MainWindow.UserTransaction != null)
            {
                MainWindow.UserTransaction.FileChanged += ModTransaction_FileChanged;
            }

            MainWindow.UserTransactionStarted += MainWindow_UserTransactionStarted;

            DebouncedUpdate = ViewHelpers.Debounce(_UpdateOnMainThread, 100);
        }

        private Action DebouncedUpdate;

        private void MainWindow_UserTransactionStarted()
        {
            if (MainWindow.UserTransaction != null)
            {
                MainWindow.UserTransaction.FileChanged += ModTransaction_FileChanged;
            }
        }

        private bool _IS_SAVING;
        private bool _Disposed;

        private async void ModTransaction_FileChanged(string internalFilePath)
        {
            if (_IS_SAVING || string.IsNullOrWhiteSpace(internalFilePath) || string.IsNullOrWhiteSpace(InternalFilePath))
            {
                return;
            }

            var children = await XivCache.GetChildFiles(InternalFilePath);

            // If our file was changed by someone else, silently attempt to reload it.
            if (children.Contains(internalFilePath) || string.Equals(internalFilePath, InternalFilePath))
            {
                DebouncedUpdate();
            }
        }

        private void _UpdateOnMainThread()
        {
            this.Invoke(() =>
            {
                var task = ReloadFile();
            });
        }

        public delegate void FileActionEventHandler(FileViewControl sender, bool success);

        /// <summary>
        /// Invoked whenever a file load attempt has completed.
        /// </summary>
        public event FileActionEventHandler FileLoaded;

        /// <summary>
        /// Invoked whenever the file is saved to Transaction/Game Files.
        /// </summary>
        public event FileActionEventHandler FileSaved;
        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// Invoke the PropertyChanged event for the given property.
        /// </summary>
        /// <param name="propertyName"></param>
        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        /// <summary>
        /// Validates if this view can load the file in question.
        /// 
        /// This function is expected to catch and handle its own errors if there are any.
        /// </summary>
        /// <param name="filePath"></param>
        /// <returns></returns>
        public virtual bool CanLoadFile(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                return false;
            }

            var exts = GetValidFileExtensions();
            if(exts == null)
            {
                return false;
            }

            var ext = Path.GetExtension(filePath);
            if (exts.ContainsKey(ext))
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// Gets the dictionary of file extensions that this view can load.
        /// Extension (including . ) => Nice Name.
        /// </summary>
        /// <returns></returns>
        public virtual Dictionary<string, string> GetValidFileExtensions()
        {
            return new Dictionary<string, string>();
        }

        /// <summary>
        /// Returns the 'nice' human readable name for the type of files read by this display.
        /// Ex "Image" or "Model"
        /// </summary>
        /// <returns></returns>
        public virtual string GetNiceName()
        {
            return "Unknown";
        }



        /// <summary>
        /// Reloads the current item, returning true if successful.
        /// 
        /// This function is expected to catch and handle its own errors.
        /// </summary>
        /// <returns></returns>
        public virtual async Task<bool> ReloadFile()
        {
            return await LoadInternalFile(InternalFilePath, false, ReferenceItem);
        }

        /// <summary>
        /// Clears the currently visible file, cleaning up any resources as necessary.
        /// 
        /// This function is expected to catch and handle its own errors.
        /// </summary>
        /// <returns></returns>
        public virtual async Task ClearFile()
        {
            if (HasFile)
            {
                var file = InternalFilePath;
                try
                {
                    await INTERNAL_ClearFile();
                    InternalFilePath = null;
                    ExternalFilePath = null;
                    ReferenceItem = null;

                } catch(Exception ex)
                {
                    Trace.WriteLine("An error occurred when unloading file: " + file + "\n" + ex.Message);
                }
            }
        }

        public abstract Task INTERNAL_ClearFile();

        /// <summary>
        /// Load the given external file into view, assuming it will go to the target internal path.
        /// Returns true if the data was successfully loaded.
        /// 
        /// This function is expected to catch and handle its own errors.
        /// </summary>
        /// <param name="externalFile"></param>
        /// <param name="internalFile"></param>
        /// <returns></returns>
        public virtual async Task<bool> LoadInternalFile(string internalFile, bool forceOriginal = false, IItem referenceItem = null)
        {
            bool success = false;
            try
            {
                if (HasFile)
                {
                    await ClearFile();
                }

                if (!CanLoadFile(internalFile))
                {
                    return success;
                }

                ReferenceItem = referenceItem;
                if (ReferenceItem == null)
                {
                    var root = await XivCache.GetFirstRoot(internalFile);
                    if (root != null) {
                        ReferenceItem = root.GetFirstItem();
                    }
                }


                var tx = MainWindow.DefaultTransaction;


                var data = await tx.ReadFile(internalFile, forceOriginal);
                InternalFilePath = internalFile;
                ExternalFilePath = "";
                ModState = await Modding.GetModState(InternalFilePath, tx);
                success = await INTERNAL_LoadFile(data);

                UnsavedChanges = false;
                return success;
            }
            catch (Exception ex)
            {
                this.ShowError("File Load Error", "There was an error loading the file:\n\n" + ex.Message);
                InternalFilePath = "";
                ExternalFilePath = "";
                ModState = EModState.UnModded;
                UnsavedChanges = false;
                return success;
            }
            finally
            {
                FileLoaded?.Invoke(this, success);
            }
        }

        /// <summary>
        /// Load the given external file into view, assuming it will go to the target internal path.
        /// Returns true if the data was successfully loaded.
        /// 
        /// This function is expected to catch and handle its own errors.
        /// </summary>
        /// <param name="externalFile"></param>
        /// <param name="internalFile"></param>
        /// <returns></returns>
        public virtual async Task<bool> LoadExternalFile(string externalFile, string internalFile = null, IItem referenceItem = null)
        {
            var success = false;
            try
            {
                if (HasFile)
                {
                    await ClearFile();
                }

                if (!CanLoadFile(externalFile))
                {
                    return success;
                }

                if (!string.IsNullOrWhiteSpace(internalFile))
                {
                    InternalFilePath = internalFile;
                }
                if (string.IsNullOrEmpty(InternalFilePath))
                {
                    throw new Exception("Must have a valid internal path to prepare the external file for.");
                }

                if (referenceItem == null)
                {
                    var root = await XivCache.GetFirstRoot(internalFile);
                    if (root != null)
                    {
                        referenceItem = root.GetFirstItem();
                    }
                }

                ReferenceItem = referenceItem;
                ExternalFilePath = externalFile;

                var data = await INTERNAL_CreateUncompressedFile(externalFile, internalFile, ReferenceItem);
                if(data == null || data.Length == 0)
                {
                    return false;
                }

                success = await INTERNAL_LoadFile(data);
                UnsavedChanges = true;
                return success;
            } catch (Exception ex)
            {
                this.ShowError("File Load Error", "There was an error loading the file:\n\n" + ex.Message);
                ExternalFilePath = "";
                return success;
            }
            finally
            {
                FileLoaded?.Invoke(this,success);
            }
        }
        protected virtual async Task<byte[]> INTERNAL_CreateUncompressedFile(string externalFile, string internalFile, IItem referenceItem)
        {
            return await SmartImport.CreateUncompressedFile(externalFile, internalFile, MainWindow.DefaultTransaction);
        }

        /// <summary>
        /// Loads the given byte data into the workspace.
        /// Returns true if the data was successfully loaded.
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        protected abstract Task<bool> INTERNAL_LoadFile(byte[] data);

        /// <summary>
        /// Gets the currently viewed file in compressed/SQPacked format.
        /// </summary>
        /// <returns></returns>
        public async Task<byte[]> GetCompressedData()
        {
            var data = await GetUncompressedData();

            var forceType2 = InternalFilePath.EndsWith(".atex");
            return await SmartImport.CreateCompressedFile(data, forceType2);
        }

        /// <summary>
        /// Converts the currently viewed file into uncompresed byte data.
        /// </summary>
        /// <returns></returns>
        public async Task<byte[]> GetUncompressedData()
        {
            return await INTERNAL_GetUncompressedData();
        }

        /// <summary>
        /// Converts the currently viewed file into uncompresed byte data.
        /// </summary>
        /// <returns></returns>
        protected abstract Task<byte[]> INTERNAL_GetUncompressedData();

        /// <summary>
        /// Loads a given file into the view, then saves it to the current user transaction/game files.
        /// 
        /// This function is expected to catch and handle its own errors.
        /// </summary>
        /// <param name="externalFilePath"></param>
        /// <param name="internalFilePath"></param>
        /// <returns></returns>
        public async Task<bool> ImportFile(string externalFilePath, string internalFilePath, IItem referenceItem = null)
        {
            var success = await LoadExternalFile(externalFilePath, internalFilePath, referenceItem);
            if (!success)
            {
                return false;
            }
            return await SaveCurrentFile();
        }

        /// <summary>
        /// Saves the current file to the current transaction/Game DATs, returning true if successful.
        /// 
        /// This function is expected to catch and handle its own errors.
        /// </summary>
        /// <returns></returns>
        public async Task<bool> SaveCurrentFile(ModTransaction tx = null)
        {
            var success = false;
            try
            {
                if (string.IsNullOrWhiteSpace(InternalFilePath))
                {
                    return success;
                }

                if(tx == null)
                {
                    tx = MainWindow.UserTransaction;
                }
                _IS_SAVING = true;

                var data = await GetCompressedData();
                await Dat.WriteModFile(data, InternalFilePath, XivStrings.TexTools, ReferenceItem, tx);
                success = true;
                ModState = EModState.Enabled;
                return success;
            }
            catch(Exception ex)
            {
                this.ShowError("File Save Error", "There was an error saving the file:\n\n" + ex.Message);
                return success;
            } finally
            {
                _IS_SAVING = false;
                FileSaved?.Invoke(this, success);
            }
        }


        protected virtual KeyValuePair<string, string> GetDefaultExtension()
        {
            var extensions = GetValidFileExtensions();
            if(extensions == null || extensions.Count == 0)
            {
                return new KeyValuePair<string, string>(".bin", "Unknown");
            }
            return extensions.First();
        }
        public virtual string GetDefaultSaveDirectory()
        {
            var mw = MainWindow.GetMainWindow();
            var path = Path.GetFullPath(IOUtil.MakeItemSavePath(ReferenceItem, new DirectoryInfo(Settings.Default.Save_Directory), IOUtil.GetRaceFromPath(InternalFilePath)));
            Directory.CreateDirectory(path);
            return path;
        }
        public virtual string GetDefaultSaveName(string extension = null)
        {
            var name = Path.GetFileNameWithoutExtension(InternalFilePath);

            if (string.IsNullOrWhiteSpace(extension))
            {
                extension = GetDefaultExtension().Key;
            }

            return name + extension;
        }


        /// <summary>
        /// Loads a given file via windows dialog, assuming it will go to the given internal file path.
        /// If no path is provided, retains the current internal file path.
        /// </summary>
        /// <returns></returns>
        public async Task<bool> LoadFileByDialog(string internalFilePath = null, IItem referenceItem = null)
        {
            if (!string.IsNullOrWhiteSpace(internalFilePath))
            {
                InternalFilePath = internalFilePath;
            }

            if (string.IsNullOrWhiteSpace(InternalFilePath))
            {
                return false;
            }

            var exts = GetValidFileExtensions();
            if (exts == null || exts.Count == 0)
            {
                return false;
            }

            var ofd = new OpenFileDialog();
            var extStrings = "*" + string.Join(";*", exts.Keys);
            var filter = GetNiceName() + " Files (" + extStrings + ")|" + extStrings;
            ofd.Filter = filter;
            ofd.InitialDirectory = GetDefaultSaveDirectory();
            ofd.FileName = GetDefaultSaveName();

            var res = ofd.ShowDialog();
            if(res != DialogResult.OK)
            {
                return false;
            }

            return await LoadExternalFile(ofd.FileName, InternalFilePath);
        }

        public async Task<bool> ImportFileByDialog(string internalFilePath = null)
        {
            var success = await LoadFileByDialog(internalFilePath);
            if (!success)
            {
                return false;
            }

            return await SaveCurrentFile();
        }

        /// <summary>
        /// Saves the current file to the given location in raw FFXIV binary format.
        /// </summary>
        /// <param name="externalFilePath"></param>
        /// <returns></returns>
        public async Task<bool> SaveAsRaw(string externalFilePath, bool compressed = false)
        {
            if (!HasFile)
            {
                return false;
            }

            var exts = GetValidFileExtensions();
            if (exts == null || exts.Count == 0)
            {
                return false;
            }

            var ext = Path.GetExtension(externalFilePath);
            if (!exts.ContainsKey(ext))
            {
                return false;
            }

            try
            {
                byte[] data;
                if (compressed)
                {
                    data = await GetCompressedData();
                }
                else
                {
                    data = await GetUncompressedData();
                }

                Directory.CreateDirectory(Path.GetDirectoryName(externalFilePath));
                File.WriteAllBytes(externalFilePath, data);
                return true;
            }
            catch(Exception ex)
            {
                this.ShowError("File Save Error", "An error occurred while saving the file:\n\n" + ex.Message);
                return false;
            }
        }


        protected abstract Task<bool> INTERNAL_SaveAs(string externalFilePath);

        /// <summary>
        /// Triggers the Modpack Save dialog.
        /// </summary>
        /// <returns></returns>
        public async Task<bool> SaveAsModpack()
        {
            // Because of the way some of the system internals work, the single-file modpack export needs us to temporarily save the file first.
            var tx = MainWindow.UserTransaction;
            var ownTx = false;
            TxFileState state = null;
            if(tx == null)
            {
                ownTx = true;
                tx = ModTransaction.BeginTransaction(true);
            }
            try
            {
                await SaveCurrentFile(tx);
                SingleFileModpackCreator.ExportFile(InternalFilePath, Window.GetWindow(this), tx);
                return true;
            }
            catch(Exception ex)
            {
                this.ShowError("Modpack Export Error", "An error occured when trying to export the file(s):\n" + ex.Message);
                return false;
            }
            finally
            {
                if (ownTx)
                {
                    ModTransaction.CancelTransaction(tx);
                } else
                {
                    await tx.RestoreFileState(state);
                }
            }
        }

        /// <summary>
        /// Saves the current file to an arbitrary location, prompted by user dialog.
        /// </summary>
        /// <returns></returns>
        public async Task<bool> SaveAsByDialog(string extension = null)
        {
            try
            {
                if (!HasFile)
                {
                    return false;
                }


                var exts = GetValidFileExtensions();
                if (exts == null || exts.Count == 0)
                {
                    return false;
                }

                KeyValuePair<string, string> ext;
                if (string.IsNullOrEmpty(extension))
                {
                    ext = GetDefaultExtension();
                } else if(exts.ContainsKey(extension))
                {
                    ext = exts.First(x => x.Key == extension);
                } else
                {
                    return false;
                }

                var sfd = new SaveFileDialog();

                if (string.IsNullOrWhiteSpace(extension))
                {
                    var extStrings = "*" + string.Join(";*", exts);
                    var filter = GetNiceName() + " Files (" + extStrings + ")|" + extStrings;
                    sfd.Filter = filter;
                } else
                {
                    sfd.Filter = ext.Value + " Files (*" + ext.Key + ")|*" + ext.Key;
                }


                sfd.InitialDirectory = GetDefaultSaveDirectory();
                sfd.FileName = GetDefaultSaveName(extension);

                var res = sfd.ShowDialog();
                if (res != DialogResult.OK)
                {
                    return false;
                }

                var userExt = Path.GetExtension(sfd.FileName);
                if (!exts.ContainsKey(userExt))
                {
                    return false;
                }


                return await INTERNAL_SaveAs(sfd.FileName);
            } catch (Exception ex)
            {
                this.ShowError("Save Error", "An Error occured while saving the file:\n\n" + ex.Message);
                return false;
            }
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_Disposed)
            {
                if (disposing)
                {
                    
                }

                FreeUnmanaged();
                _Disposed = true;
            }
        }

        protected virtual void FreeUnmanaged()
        {

        }

        ~FileViewControl()
        {
             // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
             Dispose(disposing: false);
        }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
