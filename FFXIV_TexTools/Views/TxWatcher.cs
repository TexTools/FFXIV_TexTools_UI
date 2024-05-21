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
                if (UserTransaction == null)
                {
                    return true;
                }
                else if (UserTransaction.State == ETransactionState.Open || UserTransaction.State == ETransactionState.Preparing)
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
                if(UserTransaction == null)
                {
                    return UIStrings.SaveXIV;
                } else if(UserTransaction.State == ETransactionState.Open || UserTransaction.State == ETransactionState.Preparing) {
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
    }
}
