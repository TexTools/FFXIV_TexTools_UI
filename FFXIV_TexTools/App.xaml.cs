using FFXIV_TexTools.Helpers;
using FFXIV_TexTools.Properties;
using MahApps.Metro;
using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Forms;
using Application = System.Windows.Application;
using Clipboard = System.Windows.Clipboard;

namespace FFXIV_TexTools
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            var appStyle = ThemeManager.DetectAppStyle(Application.Current);

            ThemeManager.ChangeAppStyle(Application.Current, ThemeManager.GetAccent(appStyle.Item2.Name), ThemeManager.GetAppTheme(Settings.Default.Application_Theme));

            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
            base.OnStartup(e);

            var mainWindow = new MainWindow(e.Args);
            mainWindow.Show();
        }

        private static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            var ver = FileVersionInfo.GetVersionInfo(System.Reflection.Assembly.GetExecutingAssembly().Location).FileVersion;

            const string lineBreak = "\n======================================================\n";

            var errorText = "TexTools ran into an error.\n\n" +
                            "Please submit a bug report with the following information.\n " +
                            lineBreak +
                            e.ExceptionObject +
                            lineBreak + "\n" +
                            "Copy to clipboard?";

            if (FlexibleMessageBox.Show(errorText, "Crash Report " + ver, MessageBoxButtons.YesNo, MessageBoxIcon.Error) == DialogResult.Yes)
            {
                Clipboard.SetText(e.ExceptionObject.ToString());
            }
        }
    }
}
