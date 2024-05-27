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

        public TransactionStatusWindow()
        {
            Instance = this;
            DataContext = this;
            InitializeComponent();
            Closing += OnClose;

            TxWatcher.UserTxStateChanged += TxStateChanged;

            UpdateTxState(MainWindow.UserTransaction == null ? ETransactionState.Closed : MainWindow.UserTransaction.State);
        }

        private void TxStateChanged(ETransactionState oldState, ETransactionState newState)
        {
            UpdateTxState(newState);
        }

        private void UpdateTxState(ETransactionState newState)
        {
            TxStatusText = newState.ToString();

            if (newState == ETransactionState.Open)
            {
                // TX is ready for writing.
                TxStatusBrush = Brushes.DarkGreen;
                TxActionEnabled = true;
                DuringTxRow.Visibility = Visibility.Visible;
                PreTxRow.Visibility = Visibility.Collapsed;
            }
            else if (newState == ETransactionState.Invalid || newState == ETransactionState.Closed)
            {
                // TX is closed.
                TxStatusBrush = Brushes.DarkGray;
                TxActionEnabled = true;
                DuringTxRow.Visibility = Visibility.Collapsed;
                PreTxRow.Visibility = Visibility.Visible;
            }
            else if (newState == ETransactionState.Preparing)
            {
                // TX is ready for prep-writing.
                TxStatusBrush = Brushes.DarkOrange;
                TxActionEnabled = true;
                DuringTxRow.Visibility = Visibility.Visible;
                PreTxRow.Visibility = Visibility.Collapsed;
            }
            else
            {
                // TX is working.
                TxStatusBrush = Brushes.DarkRed;
                TxActionEnabled = false;
                DuringTxRow.Visibility = Visibility.Visible;
                PreTxRow.Visibility = Visibility.Collapsed;
            }

        }

        private void OnClose(object sender, CancelEventArgs e)
        {
            TxWatcher.UserTxStateChanged -= TxStateChanged;
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
                await ModTransaction.CommitTransaction(MainWindow.UserTransaction);

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
    }

}
