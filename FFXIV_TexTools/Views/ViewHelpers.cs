using FFXIV_TexTools.Helpers;
using FFXIV_TexTools.Properties;
using FFXIV_TexTools.Resources;
using FolderSelect;
using MahApps.Metro.Controls.Dialogs;
using SixLabors.ImageSharp.Formats.Png;
using System;
using System.CodeDom;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using xivModdingFramework.Cache;
using xivModdingFramework.General.Enums;
using xivModdingFramework.Helpers;
using xivModdingFramework.Mods;
using xivModdingFramework.Mods.FileTypes;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.Window;
using Application = System.Windows.Application;
using UserControl = System.Windows.Controls.UserControl;

namespace FFXIV_TexTools.Views
{
    public static class ViewHelpers
    {

        public const string LoadModpackFilter = "Modpack Files|*.pmp;*.ttmp2;*.ttmp;*.pmp;*.json";
        public const string ModpackFileFilter = "Modpack Files|*.pmp;*.ttmp2|Penumbra Modpack|*.pmp|TexTools Modpack|*.ttmp2";
        public static Progress<(int current, int total, string message)> BindReportProgress(ProgressDialogController controller)
        {
            return new Progress<(int current, int total, string message)>(BindReportProgressAction(controller));
        }
        public static Action<(int current, int total, string message)> BindReportProgressAction(ProgressDialogController controller)
        {
            Action<(int current, int total, string message)> f = ((int current, int total, string message) report) =>
            {
                var message = "";
                if (!report.message.Equals(string.Empty))
                {
                    message =report.message.L();
                }
                else
                {
                    message = $"{UIMessages.PleaseStandByMessage}";
                }

                if (report.total > 0)
                {
                    var value = (double)report.current / (double)report.total;
                    controller.SetProgress(value);

                    message += "\n\n " + report.current + "/" + report.total;
                }
                else
                {
                    controller.SetIndeterminate();
                }

                controller.SetMessage(message);
            };

            return f;
        }

        public static WindowWrapper GetWin32Window(this Window window)
        {
            var Win32Window = new WindowWrapper(new WindowInteropHelper(window).Handle);
            return Win32Window;
        }
        public static WindowWrapper GetWin32Window(this UserControl control)
        {
            var wind = Window.GetWindow(control);
            var Win32Window = new WindowWrapper(new WindowInteropHelper(wind).Handle);
            return Win32Window;
        }

        public static bool ConfirmDiscardChanges(this UserControl control, string filePath)
        {

            WindowWrapper win32Window = null;
            var wind = Window.GetWindow(control);
            if (!IsWindowOpen(wind))
            {
                wind = null;
            }
            else
            {
                win32Window = new WindowWrapper(new WindowInteropHelper(wind).Handle);
            }
            var fileName = Path.GetFileName(filePath);
            var res = FlexibleMessageBox.Show(win32Window, "You have unsaved changes to the file: " + fileName +  "\nThey will be lost if you continue.\n\nAre you sure you wish to continue and discard these changes?", "Discard Changes Confirmation", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button1);
            if(res == DialogResult.Yes || res == DialogResult.OK)
            {
                return true;
            }
            return false;
        }


        public static void ShowError(this Window wind, string title, string message)
        {
            try
            {
                WindowWrapper win32Window = null;
                if (!IsWindowOpen(wind))
                {
                    wind = null;
                }
                else
                {
                    win32Window = new WindowWrapper(new WindowInteropHelper(wind).Handle);
                }
                FlexibleMessageBox.Show(win32Window, message, title, MessageBoxButtons.OK, MessageBoxIcon.Error,
                    MessageBoxDefaultButton.Button1);
            }
            catch (Exception ex)
            {
                // Can't let the error function crash us.
                Trace.WriteLine(ex);
            }
        }
        public static void ShowError(this UserControl control, string title, string message)
        {
            try
            {
                WindowWrapper win32Window = null;
                var wind = Window.GetWindow(control);
                if (!IsWindowOpen(wind))
                {
                    wind = null;
                }
                else
                {
                    win32Window = new WindowWrapper(new WindowInteropHelper(wind).Handle);
                }
                FlexibleMessageBox.Show(win32Window, message, title, MessageBoxButtons.OK, MessageBoxIcon.Error,
                    MessageBoxDefaultButton.Button1);
            }
            catch(Exception ex)
            {
                // Can't let the error function crash us.
                Trace.WriteLine(ex);
            }
        }
        public static void ShowWarning(this Window wind, string title, string message)
        {
            try
            {
                WindowWrapper win32Window = null;
                if (!IsWindowOpen(wind))
                {
                    wind = null;
                }
                else
                {
                    win32Window = new WindowWrapper(new WindowInteropHelper(wind).Handle);
                }
                FlexibleMessageBox.Show(win32Window, message, title, MessageBoxButtons.OK, MessageBoxIcon.Warning,
                    MessageBoxDefaultButton.Button1);
            }
            catch (Exception ex)
            {
                // Can't let the error function crash us.
                Trace.WriteLine(ex);
            }
        }
        public static void ShowWarning(this UserControl control, string title, string message)
        {
            try
            {
                WindowWrapper win32Window = null;
                var wind = Window.GetWindow(control);
                if (!IsWindowOpen(wind))
                {
                    wind = null;
                }
                else
                {
                    win32Window = new WindowWrapper(new WindowInteropHelper(wind).Handle);
                }
                FlexibleMessageBox.Show(win32Window, message, title, MessageBoxButtons.OK, MessageBoxIcon.Warning,
                    MessageBoxDefaultButton.Button1);
            }
            catch (Exception ex)
            {
                // Can't let the error function crash us.
                Trace.WriteLine(ex);
            }
        }

        public static void ShowError(string title, string message)
        {
            var wind = MainWindow.GetMainWindow();
            if (!IsWindowOpen(wind))
            {
                wind = null;
            }
            FlexibleMessageBox.Show(wind.Win32Window, message, title, MessageBoxButtons.OK, MessageBoxIcon.Error,
                MessageBoxDefaultButton.Button1);
        }
        public static bool InfoPrompt(this Window wind, string title, string message)
        {
            try
            {
                WindowWrapper win32Window = null;
                if (!IsWindowOpen(wind))
                {
                    wind = null;
                }
                else
                {
                    win32Window = new WindowWrapper(new WindowInteropHelper(wind).Handle);
                }
                var res = FlexibleMessageBox.Show(win32Window, message, title, MessageBoxButtons.OKCancel, MessageBoxIcon.Information,
                    MessageBoxDefaultButton.Button1);

                return res == DialogResult.OK;
            }
            catch (Exception ex)
            {
                // Can't let the error function crash us.
                Trace.WriteLine(ex);
                return false;
            }
        }
        public static bool WarningPrompt(this Window wind, string title, string message)
        {
            try
            {
                WindowWrapper win32Window = null;
                if (!IsWindowOpen(wind))
                {
                    wind = null;
                }
                else
                {
                    win32Window = new WindowWrapper(new WindowInteropHelper(wind).Handle);
                }
                var res = FlexibleMessageBox.Show(win32Window, message, title, MessageBoxButtons.OKCancel, MessageBoxIcon.Warning,
                    MessageBoxDefaultButton.Button1);

                return res == DialogResult.OK;
            }
            catch (Exception ex)
            {
                // Can't let the error function crash us.
                Trace.WriteLine(ex);
                return false;
            }
        }

        public static Action Debounce(Action func, int milliseconds = 300)
        {
            CancellationTokenSource cancelTokenSource = null;

            return () =>
            {
                cancelTokenSource?.Cancel();
                cancelTokenSource = new CancellationTokenSource();

                Task.Delay(milliseconds, cancelTokenSource.Token)
                    .ContinueWith(t =>
                    {
                        if (t.IsCompleted && !t.IsCanceled)
                        {
                            func();
                        }
                    }, TaskScheduler.Default);
            };
        }
        public static Action<T> Debounce<T>(Action<T> func, int milliseconds = 300)
        {
            CancellationTokenSource cancelTokenSource = null;

            return arg =>
            {
                cancelTokenSource?.Cancel();
                cancelTokenSource = new CancellationTokenSource();

                Task.Delay(milliseconds, cancelTokenSource.Token)
                    .ContinueWith(t =>
                    {
                        if (t.IsCompleted && !t.IsCanceled)
                        {
                            func(arg);
                        }
                    }, TaskScheduler.Default);
            };
        }


        /// <summary>
        /// Gets an import settings object with most of the standard user-controlled or system standard stuff set already.
        /// </summary>
        /// <returns></returns>
        public static ModPackImportSettings GetDefaultImportSettings(ProgressDialogController controller = null, Window owner = null)
        {
            var settings = new ModPackImportSettings();
            settings.AutoAssignSkinMaterials = Properties.Settings.Default.AutoMaterialFix;
            settings.UpdateEndwalkerFiles = Properties.Settings.Default.FixPreDawntrailOnImport;
            settings.UpdatePartialEndwalkerFiles = Properties.Settings.Default.FixPreDawntrailPartialOnImport;
            settings.RootConversionFunction = ModpackRootConvertWindow.GetRootConversionFunction(owner);
            settings.SourceApplication = XivStrings.TexTools;

            if (controller != null)
            {
                settings.ProgressReporter = BindReportProgress(controller);
            }
            return settings;
        }


        /// <summary>
        /// Gets the race from the settings
        /// </summary>
        /// <param name="gender">The gender of the currently selected race</param>
        /// <returns>A tuple containing the race and body</returns>
        public static (XivRace Race, string BodyID) GetUserRace(int gender)
        {
            var settingsRace = Settings.Default.Default_Race;
            var defaultBody = "0001";

            if (settingsRace.Equals(XivStringRaces.Hyur_M))
            {
                if (gender == 0)
                {
                    return (XivRaces.GetXivRace("0101"), defaultBody);
                }
            }

            if (settingsRace.Equals(XivStringRaces.Hyur_H))
            {
                if (gender == 0)
                {
                    return (XivRaces.GetXivRace("0301"), defaultBody);
                }

                return (XivRaces.GetXivRace("0401"), defaultBody);
            }

            if (settingsRace.Equals(XivStringRaces.Aura_R))
            {
                if (gender == 0)
                {
                    return (XivRaces.GetXivRace("1301"), defaultBody);
                }

                return (XivRaces.GetXivRace("1401"), defaultBody);
            }

            if (settingsRace.Equals(XivStringRaces.Aura_X))
            {
                if (gender == 0)
                {
                    return (XivRaces.GetXivRace("1301"), "0101");
                }

                return (XivRaces.GetXivRace("1401"), "0101");
            }

            return (XivRaces.GetXivRace("0201"), defaultBody);
        }
        public static T FindParentOfType<T>(this DependencyObject child) where T : DependencyObject
        {
            DependencyObject parentDepObj = child;
            do
            {
                parentDepObj = VisualTreeHelper.GetParent(parentDepObj);
                T parent = parentDepObj as T;
                if (parent != null) return parent;
            }
            while (parentDepObj != null);
            return null;
        }

        /// <summary>
        /// Loads a file into a BitmapImage object safely in such a way that the file handle is properly released immediately.
        /// </summary>
        /// <param name="file"></param>
        /// <returns></returns>
        public static BitmapImage SafeBitmapFromFile(string file)
        {
            if (string.IsNullOrWhiteSpace(file))
            {
                return null;
            }

            if (!File.Exists(file))
            {
                return null;
            }

            try
            {
                var bmp = new BitmapImage();
                using var stream = File.OpenRead(file);
                bmp.BeginInit();
                bmp.StreamSource = stream;
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.EndInit();
                return bmp;
            } catch(Exception ex)
            {
                return null;
            }
        }


        public static bool ShowConfirmation(this DependencyObject self, string title, string message)
        {
            var wind = Window.GetWindow(self);

            WindowWrapper w32 = null;

            if (IsWindowOpen(wind)) {
                w32 = GetWin32Window(wind);
            }

            var res = FlexibleMessageBox.Show(w32, message, title, MessageBoxButtons.OKCancel, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button1);
            return res == DialogResult.OK;

        }

        public static (string File, BitmapImage Image) LoadUserImage(this DependencyObject d)
        {
            var wind = d as Window;
            if (wind == null)
            {
                wind = Window.GetWindow(d);
            }
            return LoadUserImage(wind);
        }
        public static (string File, BitmapImage Image) LoadUserImage(this Window wind)
        {
            var openFileDialog = new OpenFileDialog
            {
                Filter = "Image Files|*.BMP;*.JPG;*.GIF;*.PNG;*.TGA".L()
            };


            if (openFileDialog.ShowDialog() != System.Windows.Forms.DialogResult.OK) return (null, null);

            try
            {
                var img = SixLabors.ImageSharp.Image.Load(openFileDialog.FileName);

                var tempFile = IOUtil.GetFrameworkTempFile();
                using (var fs = new FileStream(tempFile, FileMode.OpenOrCreate))
                {
                    var enc = new PngEncoder();
                    img.Save(fs, enc);
                }

                return (tempFile, ViewHelpers.SafeBitmapFromFile(tempFile));

            }
            catch (Exception ex)
            {
                wind.ShowError("Image Error".L(), "An error occurred while loading the image:\n\n" + ex.Message);
                return (null, null);
            }
        }

        public static ObservableCollection<KeyValuePair<string, T>> GetEnumSource<T>() where T : struct, IConvertible
        {
            var src = new ObservableCollection<KeyValuePair<string, T>>();

            foreach(T t in Enum.GetValues(typeof(T))){
                src.Add(new KeyValuePair<string, T>(t.ToString(), t));
            }

            return src;
        }

        public static byte[] HexToBytes(string hex)
        {
            if (hex.Length % 2 == 1)
            {
                throw new ArgumentException("Hex string must be an even number of characters.");
            }

            byte[] arr = new byte[hex.Length >> 1];

            for (int i = 0; i < hex.Length >> 1; ++i)
            {
                arr[i] = (byte)((GetHexVal(hex[i << 1]) << 4) + (GetHexVal(hex[(i << 1) + 1])));
            }

            return arr;
        }

        private static int GetHexVal(char hex)
        {
            int val = (int)hex;
            //For uppercase A-F letters:
            //return val - (val < 58 ? 48 : 55);
            //For lowercase a-f letters:
            //return val - (val < 58 ? 48 : 87);
            //Or the two combined, but a bit slower:
            return val - (val < 58 ? 48 : (val < 97 ? 55 : 87));
        }

        public static string ColorsetRowToNiceName(int id, bool dawntrail = true)
        {
            if (dawntrail)
            {
                var ab = id % 2 == 0 ? "A" : "B";
                var row = (id / 2) + 1;
                return row.ToString() + " " + ab;
            } else
            {
                return (id + 1).ToString();
            }
        }

        /// <summary>
        /// Checks if an unsafe operation can proceed, showing the user errors if not.
        /// Returns true if everything is OK, or false if the operation should be cancelled.
        /// </summary>
        /// <param name="obj"></param>
        /// <param name="requiresUnsafe"></param>
        /// <param name="requiresNoOpenTx"></param>
        /// <returns></returns>
        public static bool CheckUnsafeOperation(this DependencyObject obj, bool requiresUnsafe, bool requiresNoOpenTx)
        {
            Window wind = obj as Window;
            if(wind == null)
            {
                wind = Window.GetWindow(obj);
            }

            if(requiresUnsafe && !XivCache.GameWriteEnabled)
            {
                wind.ShowWarning("Safe Mode Error", "This operation can only be performed in UNSAFE mode.");
                return false;
            }

            if(requiresNoOpenTx && ModTransaction.ActiveTransaction != null && ModTransaction.ActiveTransaction.State != ETransactionState.Closed)
            {
                wind.ShowWarning("Transaction State Error", "This cannot be run while there is an open transaction.");
                return false;
            }

            return true;
        }

        /// <summary>
        /// Checks if a file write is allowed in the current state.
        /// </summary>
        /// <param name="obj"></param>
        /// <param name="requiresUnsafe"></param>
        /// <param name="requiresNoOpenTx"></param>
        /// <returns></returns>
        public static bool CheckFileWrite(this DependencyObject obj, ModTransaction tx = null)
        {
            Window wind = obj as Window;
            if (wind == null)
            {
                wind = Window.GetWindow(obj);
            }

            if(tx == null)
            {
                tx = MainWindow.UserTransaction;
            }

            if ((tx == null || tx.ReadOnly) && !XivCache.GameWriteEnabled)
            {
                wind.ShowWarning("Safe Mode Error", "This operation can only be performed within a Transaction or in UNSAFE mode ");
                return false;
            }

            return true;
        }
        public static bool IsWindowOpen<T>(T wind) where T : Window
        {
            if(wind == null)
            {
                return false;
            }

            if(typeof(T) == typeof(MainWindow))
            {
                var mw = wind as MainWindow;
                if (!mw.MainWindowLoaded)
                {
                    return false;
                }
            }

            var w = Application.Current.Windows.OfType<T>().FirstOrDefault(x => x == wind);
            if(w == null)
            {
                return false;
            }

            if (w.IsVisible)
            {
                return true;
            }
            return false;
        }

        public static BitmapImage GetDefaultModImage()
        {
            const string _defaultImage = "./Resources/default_mod_header.jpg";
            if (!File.Exists(_defaultImage))
            {
                return null;
            }

            return SafeBitmapFromFile(_defaultImage);
        }


        /// <summary>
        /// Retrieves the slot suffix for a given model.
        /// </summary>
        /// <param name="modelFileNameOrPath"></param>
        /// <returns></returns>
        public static string GetModelSlot(string modelFileNameOrPath)
        {
            var regex = new Regex("_([a-z0-9]{3})\\.mdl");
            var match = regex.Match(modelFileNameOrPath);
            if (!match.Success)
            {
                return "";
            }
            return match.Groups[1].Value;
        }
    }
}
