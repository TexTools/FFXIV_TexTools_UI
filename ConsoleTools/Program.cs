using HelixToolkit.SharpDX.Core.Utilities;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using xivModdingFramework.Cache;
using xivModdingFramework.Mods;

namespace ConsoleTools
{
    public class ConsoleTools
    {
        private static string[] _Args;
        public static int Main(string[] args)
        {
            // Manual lib loader because the app.config method isn't working for some reason.
            var cwd = Path.Combine(Path.GetDirectoryName(Assembly.GetEntryAssembly().Location), "lib");
            var referenceFiles = Directory.GetFiles(cwd, "*.dll", SearchOption.AllDirectories);

            AppDomain.CurrentDomain.AssemblyResolve += (obj, arg) =>
            {
                var name = $"{new AssemblyName(arg.Name).Name}.dll";
                var assemblyFile = referenceFiles.Where(x => x.EndsWith(name))
                    .FirstOrDefault();
                if (assemblyFile != null)
                    return Assembly.LoadFrom(assemblyFile);
                return null;
            };

            return Run(args).GetAwaiter().GetResult();
        }


        public static async Task<int> Run(string[] args)
        {
            _Args = args;
            await ConsoleConfig.InitCacheFromConfig();

            return await HandleConsoleArgs();
        }

        private static bool GetFlag(string flag)
        {
            if (_Args.Any(x => x == flag))
            {
                return true;
            }
            return false;
        }
        private static string GetArg(string arg)
        {
            var idx = Array.IndexOf(_Args, arg);
            if (idx >= 0)
            {
                return _Args[idx];
            }
            return null;
        }
        public static async Task<int> HandleConsoleArgs()
        {
            if (_Args == null || _Args.Length < 1)
            {
                return await ShowHelp();
            }
            var cmd = _Args[0];

            var code = -1;
            if (cmd == "/?")
            {
                code = await ShowHelp();
            }
            else if (cmd == "/u")
            {
                code = await HandleUpgrade();
            }
            else
            {
                await ShowHelp();
                code = -1;
            }

            return code;
        }

        public static async Task<int> HandleUpgrade()
        {
            if (_Args.Length < 4)
            {
                return -1;
            }

            var src = _Args[2];
            var dest = _Args[3];
            System.Console.Write("Upgrading Modpack: " + src);

            try
            {
                await xivModdingFramework.Mods.ModpackUpgrader.UpgradeModpack(src, dest);

                System.Console.Write("Upgraded Modpack saved to: " + dest);
            }
            catch (Exception ex)
            {
                Trace.WriteLine(ex);
                return -1;
            }
            return 0;
        }

        public static async Task<int> ShowHelp()
        {
            System.Console.WriteLine("==== ConsoleTools Help ====");
            System.Console.WriteLine("");
            System.Console.WriteLine("== Commands ==");
            System.Console.WriteLine("\t/? - Help => You're looking at it.");
            System.Console.WriteLine("\t/u [PathToSource] [PathToDestination] - Updates a given Modpack for Dawntrail.");
            System.Console.WriteLine("\t/s [ModpackFilePath] [OutputFilePath] - Saves a given modpack file to a new path or type, after performing basic file processing.");
            System.Console.WriteLine("\t/e [FfxivFilePath] [OutputFileName] - Extracts a given file from FFXIV.  May be SQPacked with /sqpack");
            System.Console.WriteLine("\t/i [SourceFilePath] [OutputFilePath] - Creates an FFXIV packed file from the given source file.  May be SQPacked with /sqpack");
            System.Console.WriteLine("");
            System.Console.WriteLine("== FORMATS ==");
            System.Console.WriteLine("\tModpacks may be read or written in .ttmp2, .pmp, or unzipped PMP folder path formats.");
            System.Console.WriteLine("\tImages may be saved as DDS, TEX, TGA, PNG, BMP.");
            System.Console.WriteLine("\tModels may be either DB or FBX (or other formats if you have other external converters set up).");
            System.Console.WriteLine("");
            System.Console.WriteLine("== CURRENT CONFIG ==");

            var config = ConsoleConfig.Get();
            foreach (PropertyInfo prop in typeof(ConsoleConfig).GetProperties())
            {
                var type = Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType;
                System.Console.WriteLine(prop.Name + " : " + prop.GetValue(config, null).ToString());
            }

            return 0;
        }
    }
}

