﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
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
using System.Windows.Shapes;
using xivModdingFramework.Cache;
using xivModdingFramework.Helpers;
using xivModdingFramework.Mods;

namespace FFXIV_TexTools.Views.Transactions
{
    /// <summary>
    /// Interaction logic for TransactionStatusWindow.xaml
    /// </summary>
    public partial class TransactionStatusWindow : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        private static TransactionStatusWindow Instance;

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

        private bool _CloseOnCommit;
        public bool CloseOnCommit
        {
            get => _CloseOnCommit;
            set
            {
                _CloseOnCommit = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CloseOnCommit)));
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

        public TransactionStatusWindow()
        {
            Instance = this;
            DataContext = this;

            foreach(ETransactionTarget target in Enum.GetValues(typeof(ETransactionTarget)))
            {
                TargetSource.Add(new KeyValuePair<string, ETransactionTarget>(target.ToString(), target));
            }

            InitializeComponent();
            Closing += OnClose;

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

            var files = tx.ModifiedFiles.OrderBy(x => x);
            foreach(var file in files)
            {
                if (IOUtil.IsMetaInternalFile(file))
                {
                    continue;
                }

                FileListSource.Add(file);
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
        private void UpdateTxStateUi(ETransactionState newState)
        {
            TxStatusText = newState.ToString();

            if (newState == ETransactionState.Open)
            {
                // TX is ready for writing.
                TxStatusBrush = Brushes.DarkGreen;
                TxActionEnabled = true;
                DuringTxRow.Visibility = Visibility.Visible;
                TxStatusGrid.Visibility = Visibility.Visible;
                PreTxRow.Visibility = Visibility.Collapsed;
            }
            else if (newState == ETransactionState.Invalid || newState == ETransactionState.Closed)
            {
                // TX is closed.
                TxStatusBrush = Brushes.DarkGray;
                TxActionEnabled = true;
                DuringTxRow.Visibility = Visibility.Collapsed;
                TxStatusGrid.Visibility = Visibility.Collapsed;
                PreTxRow.Visibility = Visibility.Visible;
            }
            else if (newState == ETransactionState.Preparing)
            {
                // TX is ready for prep-writing.
                TxStatusBrush = Brushes.DarkOrange;
                TxActionEnabled = true;
                DuringTxRow.Visibility = Visibility.Visible;
                TxStatusGrid.Visibility = Visibility.Visible;
                PreTxRow.Visibility = Visibility.Collapsed;
            }
            else
            {
                // TX is working.
                TxStatusBrush = Brushes.DarkRed;
                TxActionEnabled = false;
                DuringTxRow.Visibility = Visibility.Visible;
                TxStatusGrid.Visibility = Visibility.Visible;
                PreTxRow.Visibility = Visibility.Collapsed;
            }

        }

        private void UpdateTxSettingsUi(ModTransactionSettings settings)
        {
            TxTarget = settings.Target;
            TxTargetPath = settings.TargetPath;
            if(TxTarget == ETransactionTarget.Invalid || TxTarget == ETransactionTarget.GameFiles)
            {
                TxPathEnabled = false;
            }
            else
            {
                TxPathEnabled = true;
            }
            if (TxTarget == ETransactionTarget.GameFiles)
            {
                CloseOnCommitBox.IsEnabled = false;
                CloseOnCommit = true;
            }
            else
            {
                CloseOnCommitBox.IsEnabled = true;
            }
        }

        private void OnClose(object sender, CancelEventArgs e)
        {
            TxWatcher.UserTxStateChanged -= OnTxStateChanged;
            TxWatcher.UserTxSettingsChanged -= OnTxSettingsChanged;
            TxWatcher.UserTxFileChanged -= OnFileChanged;
            Instance = null;
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (MainWindow.UserTransaction == null)
                {
                    return;
                }

                TxActionEnabled = false;

                ModTransaction.CancelTransaction(MainWindow.UserTransaction, true);
            }
            catch(Exception ex)
            {
                this.ShowError("Transaction Error", "An error occured while cancelling the transaction:\n\n" + ex.Message);
            }
        }


        private void Begin_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (MainWindow.UserTransaction != null)
                {
                    return;
                }

                TxActionEnabled = false;
                MainWindow.UserTransaction = ModTransaction.BeginTransaction(true);

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

                TxActionEnabled = false;
                await ModTransaction.CommitTransaction(MainWindow.UserTransaction, CloseOnCommit != false);

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

            var tx = MainWindow.UserTransaction;
            var settings = tx.Settings;

            if(settings.Target == ETransactionTarget.Invalid || settings.Target == ETransactionTarget.GameFiles)
            {
                return;
            }

            var path = settings.TargetPath;

            if (settings.Target == ETransactionTarget.LuminaFolders)
            {
                var fbd = new FolderBrowserDialog();
                var res = fbd.ShowDialog(this.GetWin32Window());
                if(res != System.Windows.Forms.DialogResult.OK)
                {
                    return;
                }

                var folder = fbd.SelectedPath;
                settings.TargetPath = folder;
                MainWindow.UserTransaction.Settings = settings;
            } else
            {
                var sfd = new SaveFileDialog();
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
    }

}