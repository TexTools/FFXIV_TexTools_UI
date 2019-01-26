using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using FFXIV_TexTools.Properties;
using MahApps.Metro;

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
            base.OnStartup(e);
        }
    }
}
