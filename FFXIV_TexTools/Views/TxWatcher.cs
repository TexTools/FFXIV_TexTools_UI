using FFXIV_TexTools.Resources;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using xivModdingFramework.Mods;

namespace FFXIV_TexTools.Views
{
    public static class TxWatcher
    {

        public delegate void UserTxStateChangedEventHandler(ETransactionState oldState, ETransactionState newState);
        public delegate void SaveStatusChangedEventHandler(bool allowed, string text);

        public static event UserTxStateChangedEventHandler UserTxStateChanged;
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
                    return UIStrings.SaveTX;
                }
                else
                {
                    return UIStrings.SaveXIV;
                }
            }
        }

        internal static void INTERNAL_TxStateChanged(ETransactionState oldState, ETransactionState newState)
        {
            UserTxStateChanged?.Invoke(oldState, newState);
            SaveStatusChanged?.Invoke(SaveAllowed, SaveLabel);
        }

        static TxWatcher()
        {
            ModTransaction.ActiveTransactionStateChanged += ActiveTransactionStateChanged;
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
