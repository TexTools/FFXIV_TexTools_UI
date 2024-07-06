using FFXIV_TexTools.Helpers;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using xivModdingFramework.Cache;
using xivModdingFramework.General.Enums;

namespace FFXIV_TexTools.Console
{

    internal static class ConsoleManager
    {

        public static async Task<bool> HandleConsoleArgs(string[] args)
        {
            if(args == null || args.Length < 1 || args[0] != "/c")
            {
                return false;
            }

            var gameDir = new DirectoryInfo(Properties.Settings.Default.FFXIV_Directory);
            var lang = XivLanguages.GetXivLanguage(Properties.Settings.Default.Application_Language);
            await XivCache.SetGameInfo(gameDir, lang, false);

            var cmd = args[1];

            var code = -1;
            if(cmd == "/u")
            {
                code = await HandleUpgrade(args);
            }

            Application.Current.Shutdown(code);
            return true;
        }

        public static async Task<int> HandleUpgrade(string[] args)
        {
            if(args.Length < 4)
            {
                return -1;
            }

            var src = args[2];
            var dest = args[3];

            try
            {
                await ModpackUpgrader.UpgradeModpack(src, dest);
            } catch (Exception ex)
            {
                Trace.WriteLine(ex);
                return -1;
            }
            return 0;
        }

    }
}
