using HelixToolkit.SharpDX.Core.Utilities;
using SharpDX.WIC;
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
using xivModdingFramework.Models.DataContainers;
using xivModdingFramework.Models.FileTypes;
using xivModdingFramework.Mods;
using xivModdingFramework.SqPack.FileTypes;
using xivModdingFramework.Textures.DataContainers;
using xivModdingFramework.Textures.FileTypes;
using xivModdingFramework.VFX.FileTypes;

namespace ConsoleTools
{
    public class ConsoleTools
    {
        private static string[] _Args;
        public static int Main(string[] args)
        {
            // Manual lib loader because the app.config method isn't working for some reason.
            var cwd = Path.Combine(Path.GetDirectoryName(Assembly.GetEntryAssembly().Location), "console_lib");
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
            else if(cmd == "/s")
            {
                code = await HandleResaveModpack();
            }
            else if (cmd == "/e")
            {
                code = await ExtractFile();
            }
            else if (cmd == "/wrap")
            {
                code = await WrapFile();
            }
            else if (cmd == "/unwrap")
            {
                code = await UnwrapFile();
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

        public static async Task<int> HandleResaveModpack()
        {
            if (_Args.Length < 4)
            {
                return -1;
            }

            var src = _Args[2];
            var dest = _Args[3];
            System.Console.Write("Resaving Modpack: " + src);
            try
            {
                var data = await WizardData.FromModpack(src);
                await data.WriteModpack(dest);

                System.Console.Write("Modpack Saved to: " + dest);
            }
            catch (Exception ex)
            {
                Trace.WriteLine(ex);
                return -1;
            }
            return 0;
        }

        public static async Task<int> ExtractFile()
        {
            if (_Args.Length < 4)
            {
                return -1;
            }

            var sqpack = GetFlag("/sqpack");
            var src = _Args[2];
            var dest = _Args[3];

            var rtx = ModTransaction.BeginReadonlyTransaction();
            if (Path.GetExtension(src).ToLower() == Path.GetExtension(dest).ToLower())
            {
                var data = await rtx.ReadFile(src, false, sqpack);
                File.WriteAllBytes(dest, data);
                return 0;
            }
                
            if (src.EndsWith(".tex") || src.EndsWith(".atex"))
            {
                var data = await rtx.ReadFile(src);
                var tex = XivTex.FromUncompressedTex(data);
                await tex.SaveAs(dest);
            }
            else if (src.EndsWith(".mdl"))
            {
                await Mdl.ExportMdlToFile(src, dest, 1, null, false, rtx);
            }
            else
            {
                var data = await rtx.ReadFile(src);
                File.WriteAllBytes(dest, data);
            }

            return 0;
        }
        public static async Task<int> WrapFile()
        {
            if (_Args.Length < 4)
            {
                return -1;
            }

            var src = _Args[2];
            var dest = _Args[3];

            // Just dub something in with same extention if we weren't given one.
            // This will work for anything other than MDL.
            var ffPath = "chara/file" + Path.GetExtension(dest);
            if (_Args.Length > 4)
            {
                ffPath = _Args[4];
            }
            Console.WriteLine("Converting File: " + src);

            var sqpack = GetFlag("/sqpack");
            var parsed = new byte[0];
            if (sqpack)
            {
                parsed = await SmartImport.CreateCompressedFile(src, ffPath);
            } else
            {
                parsed = await SmartImport.CreateUncompressedFile(src, ffPath);
            }

            File.WriteAllBytes(dest, parsed);
            return 0;
        }

        public static async Task<int> UnwrapFile()
        {
            if (_Args.Length < 4)
            {
                return -1;
            }

            if (_Args.Length < 4)
            {
                return -1;
            }

            var ffPath = "";
            if(_Args.Length > 4)
            {
                ffPath = _Args[4];
            }

            var src = _Args[2];
            var dest = _Args[3];

            Console.WriteLine("Unwrapping file: " + src);
            var data = File.ReadAllBytes(src);

            using var br = new BinaryReader(new MemoryStream(data));
            var type = Dat.GetSqPackType(br);

            if(type > 1 && type < 4)
            {
                try
                {
                    Console.WriteLine("Un-Sqpacking file...");
                    data = await Dat.ReadSqPackFile(data);
                }
                catch
                {
                    // If this failed to parse, it may not be SqPacked.
                    Console.WriteLine("Un-Sqpack failed, continuing with file as-is...");
                }
            }

            var rtx = ModTransaction.BeginReadonlyTransaction();
            if (Path.GetExtension(src).ToLower() == Path.GetExtension(dest).ToLower())
            {
                File.WriteAllBytes(dest, data);
                return 0;
            }

            if (src.EndsWith(".tex") || src.EndsWith(".atex"))
            {
                var tex = XivTex.FromUncompressedTex(data);
                await tex.SaveAs(dest);
            }
            else if (src.EndsWith(".mdl"))
            {
                var mdl = Mdl.GetXivMdl(data);
                var ttm = TTModel.FromRaw(mdl);
                ttm.Source = ffPath;
                await Mdl.ExportTTModelToFile(ttm, ffPath, 1, null, rtx);
            }
            else
            {
                File.WriteAllBytes(dest, data);
            }

            return 0;
        }


        public static async Task<int> ShowHelp()
        {
            System.Console.WriteLine("==== ConsoleTools Help ====");
            System.Console.WriteLine("");
            System.Console.WriteLine("== Commands ==");
            System.Console.WriteLine("\t/? - Help => You're looking at it.");
            System.Console.WriteLine("");
            System.Console.WriteLine("\t/u [ModpackFilePath] [DestFilePath] - Updates a given Modpack for Dawntrail.");
            System.Console.WriteLine("");
            System.Console.WriteLine("\t/s [ModpackFilePath] [DestFilePath] - Saves a given modpack file to a new path or type, after performing basic file processing.");
            System.Console.WriteLine("");
            System.Console.WriteLine("\t/e [FfxivInternalPath] [DestFilePath] - Extracts a given file from FFXIV.  May be SQPacked with /sqpack");
            System.Console.WriteLine("");
            System.Console.WriteLine("\t/wrap [SourceFilePath] [DestFilePath] [IntendedFfxivFilePath] - Creates an FFXIV format file from the given source file.  May be SQPacked with /sqpack.  FF Path only needed for MDLs.");
            System.Console.WriteLine("");
            System.Console.WriteLine("\t/unwrap [SourceFilePath] [DestFilePath] [IntendedFfxivFilePath] - Unwraps a given on-disk SqPacked or Flat FFXIV file into the given format. FF Path only needed for MDLs Skeleton/Texture info.");
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

