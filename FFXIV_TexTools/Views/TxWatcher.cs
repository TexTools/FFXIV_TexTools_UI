using FFXIV_TexTools.Resources;
using FFXIV_TexTools.Views.Transactions;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using xivModdingFramework.Helpers;
using xivModdingFramework.Mods;
using static FFXIV_TexTools.Views.TxWatcher;

namespace FFXIV_TexTools.Views
{
    public static class TxWatcher
    {

        public delegate void UserTxStartedEventHandler(ModTransaction tx);
        public delegate void UserTxStateChangedEventHandler(ETransactionState oldState, ETransactionState newState);
        public delegate void SaveStatusChangedEventHandler(bool allowed, string text);
        public delegate void UserTxSettingsChangedEventHandler(ModTransactionSettings settings);
        public delegate void UserTxFileChangedEventHandler(string file);

        public static event UserTxStartedEventHandler UserTxStarted;
        public static event UserTxSettingsChangedEventHandler UserTxSettingsChanged;
        public static event UserTxStateChangedEventHandler UserTxStateChanged;
        public static event UserTxFileChangedEventHandler UserTxFileChanged;
        public static event SaveStatusChangedEventHandler SaveStatusChanged;

        public static ModTransaction UserTransaction
        {
            get
            {
                return MainWindow.UserTransaction;
            }
        }

        public static ModTransaction DefaultTransaction
        {
            get
            {
                return MainWindow.DefaultTransaction;
            }
        }


        public static bool SaveAllowed
        {
            get
            {
                if (_HardSaveDisabled)
                {
                    return false;
                }

                if (ModTransaction.ActiveTransaction == null)
                {
                    return true;
                } else if (ModTransaction.ActiveTransaction != MainWindow.UserTransaction) {
                    // This is a temporary write transaction being used internally, no other saves allowed, regardless of state.
                    return false;
                }
                else if (ModTransaction.ActiveTransaction.State == ETransactionState.Open || ModTransaction.ActiveTransaction.State == ETransactionState.Preparing || ModTransaction.ActiveTransaction.State == ETransactionState.Closed)
                {
                    return true;
                }
                else
                {
                    return false;
                }
            }
        }
        public static string SaveLabel
        {
            get
            {
                if (_HardSaveDisabled)
                {
                    return UIStrings.Working_Ellipsis;
                }

                if (UserTransaction == null)
                {
                    return UIStrings.SaveXIV;
                } else if (UserTransaction.State == ETransactionState.Open || UserTransaction.State == ETransactionState.Preparing) {
                    if (PenumbraAttachHandler.IsAttached)
                    {
                        return UIStrings.SavePenumbra;
                    } else
                    {
                        return UIStrings.SaveTX;
                    }
                }
                else
                {
                    return UIStrings.SaveXIV;
                }
            }
        }

        private static Action DebouncedCommit;

        internal static void INTERNAL_TxStateChanged(ETransactionState oldState, ETransactionState newState)
        {
            if(oldState == ETransactionState.Invalid || oldState == ETransactionState.Closed && MainWindow.UserTransaction != null)
            {
                UserTxStarted?.Invoke(MainWindow.UserTransaction);
            }

            UserTxStateChanged?.Invoke(oldState, newState);
            SaveStatusChanged?.Invoke(SaveAllowed, SaveLabel);
        }

        internal static void INTERNAL_TxSettingsChanged(ModTransactionSettings settings)
        {
            UserTxSettingsChanged?.Invoke(settings);
        }
        internal static void INTERNAL_TxFileChanged(string file)
        {
            UserTxFileChanged?.Invoke(file);
            if (TransactionStatusWindow._AutoCommit)
            {
                DebouncedCommit();
            }
        }

        private static async void CommitTx()
        {
            try
            {
                if (!TransactionStatusWindow._AutoCommit)
                {
                    return;
                }

                if (MainWindow.UserTransaction == null)
                {
                    return;
                }

                var tx = MainWindow.UserTransaction;
                if(tx.State != ETransactionState.Open)
                {
                    return;
                }

                if(tx.Settings.Target == ETransactionTarget.GameFiles)
                {
                    return;
                }

                await ModTransaction.CommitTransaction(tx, false);
            }
            catch(Exception ex)
            {
                var mw = MainWindow.GetMainWindow();
                _ = mw.Dispatcher.InvokeAsync(() =>
                {
                    MainWindow.GetMainWindow().ShowError("Commit Transaction Error", "An error occurred while committing the transaction:\n\n" + ex.Message);
                });
            }
        }

        static TxWatcher()
        {
            ModTransaction.ActiveTransactionStateChanged += ActiveTransactionStateChanged;
            DebouncedCommit = ViewHelpers.Debounce(CommitTx, 1000);
        }

        private static void ActiveTransactionStateChanged(ModTransaction sender, ETransactionState oldState, ETransactionState newState)
        {
            SaveStatusChanged?.Invoke(SaveAllowed, SaveLabel);
        }

        private static bool _HardSaveDisabled = false;

        /// <summary>
        /// Strictly disable saving, immediately, system-wide.
        /// Used as a failsafe, since it may take some time for the system to open a Transaction.
        /// The function which uses this MUST have a finally{EnableSave()} block.
        /// </summary>
        /// <returns></returns>
        public static void DisableSave()
        {
            _HardSaveDisabled = true;
            SaveStatusChanged?.Invoke(SaveAllowed, SaveLabel);
        }

        /// <summary>
        /// restore the system level saving block.
        /// </summary>
        /// <returns></returns>
        public static void EnableSave()
        {
            _HardSaveDisabled = false;
            SaveStatusChanged?.Invoke(SaveAllowed, SaveLabel);
        }
    }
}
