using FFXIV_TexTools.Helpers;
using FFXIV_TexTools.Properties;
using FFXIV_TexTools.Resources;
using FFXIV_TexTools.Views.Projects;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using WK.Libraries.BetterFolderBrowserNS;
using xivModdingFramework.Cache;
using xivModdingFramework.Helpers;
using xivModdingFramework.Mods;
using xivModdingFramework.Mods.FileTypes;

namespace FFXIV_TexTools.Views.Transactions
{
    /// <summary>
    /// Interaction logic for TransactionStatusWindow.xaml
    /// </summary>
    public partial class TransactionStatusWindow : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        public static TransactionStatusWindow Instance;

        private string _TxStatusText;
        public string TxStatusText
        {
            get => _TxStatusText;
            set
            {
                _TxStatusText = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(TxStatusText)));
            }
        }

        private Brush _TxStatusBrush;
        public Brush TxStatusBrush
        {
            get => _TxStatusBrush;
            set
            {
                _TxStatusBrush = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(TxStatusBrush)));
            }
        }

        private bool _TxActionEnabled;
        public bool TxActionEnabled
        {
            get => _TxActionEnabled;
            set
            {
                _TxActionEnabled = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(TxActionEnabled)));
            }
        }

        private bool _CommitEnabled;
        public bool CommitEnabled
        {
            get => _CommitEnabled;
            set
            {
                _CommitEnabled = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CommitEnabled)));
            }
        }

        private bool _TxTargetEnabled;
        public bool TxTargetEnabled
        {
            get => _TxTargetEnabled;
            set
            {
                _TxTargetEnabled = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(TxTargetEnabled)));
            }
        }
        private bool _TxPathEnabled;
        public bool TxPathEnabled
        {
            get => _TxPathEnabled;
            set
            {
                _TxPathEnabled = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(TxPathEnabled)));
            }
        }

        public static bool _KeepOpen { get; private set; }
        public bool KeepOpen
        {
            get => _KeepOpen;
            set
            {
                _KeepOpen = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(KeepOpen)));

                if(_KeepOpen == false)
                {
                    AutoCommitBox.IsEnabled = false;
                    AutoCommit = false;
                } else
                {
                    AutoCommitBox.IsEnabled = true;
                }
            }
        }

        public static bool _AutoCommit { get; private set; }
        public bool AutoCommit
        {
            get => _AutoCommit;
            set
            {
                _AutoCommit = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(AutoCommit)));
            }
        }


        private string _TxTargetPath;
        public string TxTargetPath
        {
            get => _TxTargetPath;
            set
            {
                _TxTargetPath = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(TxTargetPath)));
            }
        }

        private ETransactionTarget _TxTarget;
        public ETransactionTarget TxTarget
        {
            get => _TxTarget;
            set
            {
                var original = _TxTarget;
                _TxTarget = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(TxTarget)));

                if(original != _TxTarget)
                {
                    UpdateTarget();
                }
            }
        }

        public ObservableCollection<KeyValuePair<string, ETransactionTarget>> TargetSource { get; set; } = new ObservableCollection<KeyValuePair<string, ETransactionTarget>>();

        public ObservableCollection<string> FileListSource { get; set; } = new ObservableCollection<string>();

        private bool _LOADING;
        public TransactionStatusWindow()
        {
            if(ProjectWindow.Instance != null)
            {
                ProjectWindow.Instance.Close();
            }

            Instance = this;
            DataContext = this;

            foreach(ETransactionTarget target in Enum.GetValues(typeof(ETransactionTarget)))
            {
                TargetSource.Add(new KeyValuePair<string, ETransactionTarget>(target.ToString(), target));
            }

            InitializeComponent();
            Closing += OnClose;

            _LOADING = true;
            TxWatcher.UserTxStateChanged += OnTxStateChanged;
            TxWatcher.UserTxSettingsChanged += OnTxSettingsChanged;
            TxWatcher.UserTxFileChanged += OnFileChanged;

            UpdateTxStateUi(MainWindow.UserTransaction == null ? ETransactionState.Closed : MainWindow.UserTransaction.State);
            if(MainWindow.UserTransaction != null)
            {
                UpdateTxSettingsUi(MainWindow.UserTransaction.Settings);
                UpdateFileList();
            }
            DebouncedFileListUpdate = ViewHelpers.Debounce(DispatchFileListUpdate, 300);
            _LOADING = false;
        }

        private Action DebouncedFileListUpdate;

        private void UpdateFileList()
        {
            if(MainWindow.UserTransaction == null)
            {
                return;
            }

            var tx = MainWindow.UserTransaction;

            FileListSource.Clear();

            IOrderedEnumerable<string> files;
            if(MainWindow.UserTransaction.State == ETransactionState.Preparing)
            {
                files = tx.PrepFiles.OrderBy(x => x);
            } else
            {
                files = tx.ModifiedFiles.OrderBy(x => x);
            }

            foreach(var file in files)
            {
                if (IOUtil.IsMetaInternalFile(file))
                {
                    continue;
                }

                FileListSource.Add(file);
            }

            if (string.IsNullOrWhiteSpace(TxTargetPath) || tx == null || tx.ModifiedFiles.Count == 0)
            {
                CommitEnabled = false;
            }
            else
            {
                CommitEnabled = true;
            }
        }

        private async void OnTxSettingsChanged(ModTransactionSettings settings)
        {
            try
            {
                await await Dispatcher.InvokeAsync(async () =>
                {
                    UpdateTxSettingsUi(settings);
                });
            }
            catch (Exception ex)
            {
                Trace.WriteLine(ex);
            }
        }

        private async void OnTxStateChanged(ETransactionState oldState, ETransactionState newState)
        {
            try
            {
                await await Dispatcher.InvokeAsync(async () =>
                {
                    UpdateTxStateUi(newState);
                    var tx = MainWindow.UserTransaction;
                    if(tx != null)
                    {
                        UpdateTxSettingsUi(tx.Settings);
                        UpdateFileList();
                    }
                });
            } catch(Exception ex)
            {
                Trace.WriteLine(ex);
            }
        }

        private void OnFileChanged(string file)
        {
            try
            {
                DebouncedFileListUpdate();
            }
            catch (Exception ex)
            {
                Trace.WriteLine(ex);
            }
        }
        private async void DispatchFileListUpdate()
        {
            try
            {
                await Dispatcher.InvokeAsync(() =>
                {
                    UpdateFileList();
                });
            }
            catch(Exception ex)
            {
                Trace.WriteLine(ex);
            }
        }

        private void ShowRow(Grid row)
        {
            PreTxRow.Visibility = Visibility.Collapsed;
            PreparingTxRow.Visibility = Visibility.Collapsed;
            DuringTxRow.Visibility = Visibility.Collapsed;
            row.Visibility = Visibility.Visible;
        }
        private void UpdateTxStateUi(ETransactionState newState)
        {
            TxStatusText = newState.ToString();
            if (PenumbraAttachHandler.IsAttached && newState == ETransactionState.Open)
            {
                TxStatusText = "Penumbra Sync".L();
            }


            if (newState == ETransactionState.Invalid || newState == ETransactionState.Closed)
            {
                // TX is closed.
                TxStatusBrush = Brushes.DarkGray;
                TxActionEnabled = true;

                TxStatusGrid.Visibility = Visibility.Collapsed;
                TargetGrid.Visibility = Visibility.Collapsed;
                ShowRow(PreTxRow);
            }
            else if (newState == ETransactionState.Preparing)
            {
                // TX is ready for prep-writing.
                TxStatusBrush = Brushes.DarkOrange;
                TxActionEnabled = true;

                TxStatusGrid.Visibility = Visibility.Visible;
                TargetGrid.Visibility = Visibility.Collapsed;
                ShowRow(PreparingTxRow);
            }
            else if (newState == ETransactionState.Open)
            {
                // TX is ready for writing.
                TxStatusBrush = Brushes.DarkGreen;
                TxActionEnabled = true;

                TxStatusGrid.Visibility = Visibility.Visible;
                TargetGrid.Visibility = Visibility.Visible;


                ShowRow(DuringTxRow);
            }
            else
            {
                // TX is working.
                TxStatusBrush = Brushes.DarkRed;
                TxActionEnabled = false;

                TxStatusGrid.Visibility = Visibility.Visible;
                TargetGrid.Visibility = Visibility.Visible;
                ShowRow(DuringTxRow);
            }

            if (PenumbraAttachHandler.IsAttached)
            {
                PenumbraRestore.Visibility = Visibility.Visible;
            }
            else
            {
                PenumbraRestore.Visibility = Visibility.Collapsed;
            }

        }

        private void UpdateTxSettingsUi(ModTransactionSettings settings)
        {
            if (PenumbraAttachHandler.IsAttached)
            {
                TxPathEnabled = false;
                TxTargetEnabled = false;
                CloseOnCommitBox.IsEnabled = false;
                KeepOpen = true;
                AutoCommitBox.IsEnabled = false;
                TxTarget = settings.Target;
                TxTargetPath = settings.TargetPath;
                return;
            }

            TxTargetEnabled = true;
            TxTarget = settings.Target;
            TxTargetPath = settings.TargetPath;
            if( TxTarget == ETransactionTarget.GameFiles)
            {
                TxPathEnabled = false;
            }
            else
            {
                TxPathEnabled = true;
            }

            if ( TxTarget == ETransactionTarget.GameFiles)
            {
                CloseOnCommitBox.IsEnabled = false;
                KeepOpen = false;
            }
            else
            {
                CloseOnCommitBox.IsEnabled = true;
            }

            var tx = MainWindow.UserTransaction;
            if (string.IsNullOrWhiteSpace(TxTargetPath) || tx == null || tx.ModifiedFiles.Count == 0)
            {
                CommitEnabled = false;
            } else
            {
                CommitEnabled = true;
            }
        }

        private void OnClose(object sender, CancelEventArgs e)
        {
            TxWatcher.UserTxStateChanged -= OnTxStateChanged;
            TxWatcher.UserTxSettingsChanged -= OnTxSettingsChanged;

            // TODO: Should we hang onto this event for auto-import?
            TxWatcher.UserTxFileChanged -= OnFileChanged;
            Instance = null;

            if (null != Owner)
            {
                Owner.Activate();
            }
        }

        private async void Cancel_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (MainWindow.UserTransaction == null)
                {
                    return;
                }

                TxActionEnabled = false;

                await ModTransaction.CancelTransaction(MainWindow.UserTransaction, true);
            }
            catch(Exception ex)
            {
                this.ShowError("Transaction Error", "An error occured while cancelling the transaction:\n\n" + ex.Message);
            }
        }


        private async void Begin_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (MainWindow.UserTransaction != null)
                {
                    if(MainWindow.UserTransaction.State == ETransactionState.Preparing)
                    {
                        MainWindow.UserTransaction.Start();
                    }
                    return;
                }

                TxActionEnabled = false;
                MainWindow.UserTransaction = await ModTransaction.BeginTransaction(true, null, null, false, false);
            }
            catch (Exception ex)
            {
                this.ShowError("Transaction Error", "An error occured while creating the transaction:\n\n" + ex.Message);
            }
        }

        private async void Commit_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (MainWindow.UserTransaction == null)
                {
                    return;
                }

                if(!XivCache.GameWriteEnabled && MainWindow.UserTransaction.Settings.Target == ETransactionTarget.GameFiles)
                {
                    var res = FlexibleMessageBox.Show(ViewHelpers.GetWin32Window(Window.GetWindow(this)),
                        "You are committing to the live FFXIV game files while SAFE mode is enabled.\n\nAre you SURE this is what you meant to do?", "Safe Mode Write Confirmation", MessageBoxButtons.OKCancel, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button1);
                    if(res != System.Windows.Forms.DialogResult.OK)
                    {
                        return;
                    }
                }

                TxActionEnabled = false;
                var close = !KeepOpen;
                await ModTransaction.CommitTransaction(MainWindow.UserTransaction, close);

            }
            catch (Exception ex)
            {
                this.ShowError("Transaction Error", "An error occured while committing the transaction:\n\n" + ex.Message);
            }
        }

        public static void ShowTxStatus()
        {
            if (Instance == null)
            {
                var wind = new TransactionStatusWindow();
                wind.Owner = MainWindow.GetMainWindow();
                wind.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                wind.Show();
                return;
            } else
            {
                Instance.Show();
                return;
            }
        }

        private void UpdateTarget()
        {
            if(MainWindow.UserTransaction == null)
            {
                return;
            }
            if (PenumbraAttachHandler.IsAttached)
            {
                return;
            }

            if (_LOADING)
            {
                return;
            }

            var tx = MainWindow.UserTransaction;

            var settings = tx.Settings;
            settings.Target = TxTarget;

            if(settings.Target == ETransactionTarget.GameFiles)
            {
                settings.TargetPath = XivCache.GameInfo.GameDirectory.FullName;
            } else
            {
                settings.TargetPath = "";
            }

            tx.Settings = settings;
        }


        private void SelectTargetPath_Click(object sender, RoutedEventArgs e)
        {
            if(MainWindow.UserTransaction == null)
            {
                return;
            }

            if (PenumbraAttachHandler.IsAttached)
            {
                return;
            }

            var tx = MainWindow.UserTransaction;
            var settings = tx.Settings;

            if( settings.Target == ETransactionTarget.GameFiles)
            {
                return;
            }

            var path = settings.TargetPath;

            if (settings.Target == ETransactionTarget.FolderTree || settings.Target == ETransactionTarget.PenumbraModFolder)
            {
                var fbd = new FolderBrowserDialog();

                if (!string.IsNullOrWhiteSpace(TxTargetPath))
                {
                    fbd.SelectedPath = TxTargetPath;
                }

                var res = fbd.ShowDialog(this.GetWin32Window());
                if (res != System.Windows.Forms.DialogResult.OK)
                {
                    return;
                }

                var folder = fbd.SelectedPath;
                settings.TargetPath = folder;
                MainWindow.UserTransaction.Settings = settings;
            } else if(settings.Target == ETransactionTarget.TTMP || settings.Target == ETransactionTarget.PMP)
            {
                var sfd = new SaveFileDialog();

                if (settings.Target == ETransactionTarget.TTMP)
                {
                    sfd.Filter = "TexTools Modpack Files|*.ttmp2";
                } else
                {
                    sfd.Filter = "Penumbra Modpack Files|*.pmp";
                }

                if (!string.IsNullOrWhiteSpace(TxTargetPath))
                {
                    sfd.InitialDirectory = Path.GetDirectoryName(TxTargetPath);
                } else
                {
                    sfd.InitialDirectory = Settings.Default.ModPack_Directory;
                }

                var res = sfd.ShowDialog();
                if (res != System.Windows.Forms.DialogResult.OK)
                {
                    return;
                }

                var file = sfd.FileName;
                settings.TargetPath = file;
                MainWindow.UserTransaction.Settings = settings;
            }

        }

        private async void ResetFiles_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (MainWindow.UserTransaction == null)
                {
                    return;
                }
                var tx = MainWindow.UserTransaction;
                if (tx.State != ETransactionState.Open && tx.State != ETransactionState.Preparing)
                {
                    return;
                }

                var files = FileListBox.SelectedItems;
                if (files == null || files.Count == 0)
                {
                    return;
                }
                
                var prePrep = tx.State == ETransactionState.Open ? false : true;

                foreach(var file in files)
                {
                    var st = file as string;
                    if (st == null) continue;
                    await tx.ResetFile(st, prePrep);
                }
                UpdateFileList();
            }
            catch(Exception ex)
            {
                this.ShowError("Reset File Error","An error occurred when resetting the file:\n\n" + ex.Message);
            }
        }

        static BetterFolderBrowser PenumbraAttachDialog = new BetterFolderBrowser();
        private async void AttachPenumbra_Click(object sender, RoutedEventArgs e)
        {

            try
            {
                await BeginPenumbraAttach();
            } catch(Exception ex) {
                this.ShowError("Penumbra Attach Error", "An error occurred while beginning the transaction:\n\n" + ex.Message);
            }
        }

        private async Task BeginPenumbraAttach()
        {

            if (ModTransaction.ActiveTransaction != null && ModTransaction.ActiveTransaction.State != ETransactionState.Preparing)
            {
                return;
            }

            PenumbraAttachDialog.Title = "Select Penumbra Mod Folder...";

            var res = PenumbraAttachDialog.ShowDialog();
            if (res != System.Windows.Forms.DialogResult.OK)
            {
                return;
            }
            
            _AutoCommit = false;
            _KeepOpen = false;


            var backupFolder = Path.Combine(Path.GetTempPath(), "TexTools_Transaction_Backup");
            IOUtil.DeleteTempDirectory(backupFolder);
            IOUtil.CopyFolder(PenumbraAttachDialog.SelectedPath, backupFolder);

            var tx = await PenumbraAttachHandler.Attach(PenumbraAttachDialog.SelectedPath);
            MainWindow.UserTransaction = tx;
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private async void Prepare_Click(object sender, RoutedEventArgs e)
        {
            TxActionEnabled = false;
            try
            {
                MainWindow.UserTransaction = await ModTransaction.BeginTransaction(true, null, null, true, false);
            }
            catch(Exception ex)
            {
                ViewHelpers.ShowError("Transaction Error", "An error occurred while creating the transaction:\n\n" + ex.Message);
            }
        }

        private async void RestorePenumbraBackup_Click(object sender, RoutedEventArgs e)
        {
            if(!PenumbraAttachHandler.IsAttached || MainWindow.UserTransaction == null || MainWindow.UserTransaction.State != ETransactionState.Open)
            {
                return;
            }
            if(MainWindow.UserTransaction.Settings.Target != ETransactionTarget.PenumbraModFolder)
            {
                return;
            }

            var penumbraPath = MainWindow.UserTransaction.Settings.TargetPath;
            var backupFolder = Path.Combine(Path.GetTempPath(), "TexTools_Transaction_Backup");

            if(!Directory.Exists(backupFolder))
            {
                return;
            }

            if (!ViewHelpers.WarningPrompt(this, 
                "Backup Restore Confirmation", 
                "This will cancel your current transaction and reload the pre-transaction backup back to Penumbra.\n\nAre you sure you wish to proceed? All current work will be lost."))
            {
                return;
            }

            try
            {
                TxActionEnabled = false;
                await ModTransaction.CancelTransaction(MainWindow.UserTransaction, true);

                IOUtil.RecursiveDeleteDirectory(penumbraPath);

                IOUtil.CopyFolder(backupFolder, penumbraPath);

                var di = new DirectoryInfo(penumbraPath);
                var folder = di.Name;
                _ = PenumbraAPI.ReloadMod(folder);
            } catch (Exception ex)
            {
                this.ShowError("Backup Restore Error", "An error occurred while restoring the backup:\n\n" + ex.Message + "\n\nBackup Location: " + backupFolder);
            }

        }
    }

}
