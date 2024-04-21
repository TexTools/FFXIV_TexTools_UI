using FFXIV_TexTools.Helpers;
using FFXIV_TexTools.Properties;
using HelixToolkit.Wpf.SharpDX.Utilities;
using MahApps.Metro;
using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Threading;
using Application = System.Windows.Application;
using Clipboard = System.Windows.Clipboard;

namespace FFXIV_TexTools
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        static NVOptimusEnabler nvEnabler = new NVOptimusEnabler();

        protected override void OnStartup(StartupEventArgs e)
        {
            // Disable hardware acceleration of all windows if requested
            if (Configuration.EnvironmentConfiguration.TT_Software_Rendering)
                System.Windows.Media.RenderOptions.ProcessRenderMode = System.Windows.Interop.RenderMode.SoftwareOnly;

            var appStyle = ThemeManager.DetectAppStyle(Application.Current);

            ThemeManager.ChangeAppStyle(Application.Current, ThemeManager.GetAccent(appStyle.Item2.Name), ThemeManager.GetAppTheme(Settings.Default.Application_Theme));

            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;

            Dispatcher.UnhandledException += DispatcherOnUnhandledException;

            Application.Current.DispatcherUnhandledException += CurrentOnDispatcherUnhandledException;

            TaskScheduler.UnobservedTaskException += TaskSchedulerOnUnobservedTaskException;

            base.OnStartup(e);

            var mainWindow = new MainWindow(e.Args);
        }

        private void TaskSchedulerOnUnobservedTaskException(object sender, UnobservedTaskExceptionEventArgs e)
        {
            var ver = FileVersionInfo.GetVersionInfo(System.Reflection.Assembly.GetExecutingAssembly().Location).FileVersion;

            const string lineBreak = "\n======================================================\n";

            var errorText = "TexTools ran into an error.\n\n" +
                            "Please submit a bug report with the following information.\n " +
                            lineBreak +
                            e.Exception +
                            lineBreak + "\n" +
                            "Copy to clipboard?";

            if (FlexibleMessageBox.Show(errorText, "Crash Report " + ver, MessageBoxButtons.YesNo, MessageBoxIcon.Error) == DialogResult.Yes)
            {
                Clipboard.SetText(e.Exception.ToString());
            }
        }

        private void CurrentOnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            var ver = FileVersionInfo.GetVersionInfo(System.Reflection.Assembly.GetExecutingAssembly().Location).FileVersion;

            const string lineBreak = "\n======================================================\n";

            var errorText = "TexTools ran into an error.\n\n" +
                            "Please submit a bug report with the following information.\n " +
                            lineBreak +
                            e.Exception +
                            lineBreak + "\n" +
                            "Copy to clipboard?";

            if (FlexibleMessageBox.Show(errorText, "Crash Report " + ver, MessageBoxButtons.YesNo, MessageBoxIcon.Error) == DialogResult.Yes)
            {
                Clipboard.SetText(e.Exception.ToString());
            }
        }

        private void DispatcherOnUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            var ver = FileVersionInfo.GetVersionInfo(System.Reflection.Assembly.GetExecutingAssembly().Location).FileVersion;

            const string lineBreak = "\n======================================================\n";

            var errorText = "TexTools ran into an error.\n\n" +
                            "Please submit a bug report with the following information.\n " +
                            lineBreak +
                            e.Exception +
                            lineBreak + "\n" +
                            "Copy to clipboard?";

            if (FlexibleMessageBox.Show(errorText, "Crash Report " + ver, MessageBoxButtons.YesNo, MessageBoxIcon.Error) == DialogResult.Yes)
            {
                Clipboard.SetText(e.Exception.ToString());
            }
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
