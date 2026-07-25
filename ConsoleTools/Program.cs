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
using xivModdingFramework.Helpers;
using xivModdingFramework.Models.DataContainers;
using xivModdingFramework.Models.FileTypes;
using xivModdingFramework.Models.Helpers;
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

            int res = -1;
            try
            {
                res = Run(args).GetAwaiter().GetResult();
            }
            catch(Exception ex) 
            {
                Console.WriteLine(ex);
            }

            try
            {
                // Always clear the temp folder, or try to.
                IOUtil.ClearTempFolder();
            }
            catch
            {

            }

            return res;
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
            else if (cmd == "/upgrade")
            {
                code = await HandleUpgrade();
            }
            else if (cmd == "/resave")
            {
                code = await HandleResaveModpack();
            }
            else if (cmd == "/extract")
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
            else if (cmd == "/list")
            {
                code = await ListRoot();
            }
            else
            {
                Console.WriteLine("Unknown Command: " + cmd);
                code = -1;
            }

            return code;
        }

        private static async Task<int> ListRoot()
        {
            if (_Args.Length < 2)
            {
                return -1;
            }

            var rootSt = _Args[1];
            var rootInfo = XivCache.GetFileNameRootInfo(rootSt, true);

            if (!rootInfo.IsValid())
            {
                Console.WriteLine("Given Root ID is not valid: " + rootSt);
                return -1;
            }
            var root = new XivDependencyRoot(rootInfo);

            var files = await root.GetAllFiles();

            foreach(var file in files)
            {
                Console.WriteLine(file);
            }
            return 0;
        }

        public static async Task<int> HandleUpgrade()
        {
            if (_Args.Length < 3)
            {
                return -1;
            }

            var src = _Args[1];
            var dest = _Args[2];
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
            if (_Args.Length < 3)
            {
                Console.WriteLine("Insufficient argument count for function.");
                return -1;
            }

            var src = _Args[1];
            var dest = _Args[2];
            System.Console.WriteLine("Loading Modpack: " + src);
            try
            {
                var data = await WizardData.FromModpack(src);
                if(data == null)
                {
                    Console.WriteLine("Failed to load Modpack at: " + src);
                    return -1;
                }

                await data.WriteModpack(dest, true);

                System.Console.Write("Modpack Saved to: " + dest);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                return -1;
            }
            return 0;
        }

        public static async Task<int> ExtractFile()
        {
            if (_Args.Length < 3)
            {
                Console.WriteLine("Insufficient argument count for function.");
                return -1;
            }

            var sqpack = GetFlag("/sqpack");
            var src = _Args[1];
            var dest = _Args[2];

            Console.WriteLine("Extracting File: " + src);

            var rtx = ModTransaction.BeginReadonlyTransaction();
            if (Path.GetExtension(src).ToLower() == Path.GetExtension(dest).ToLower())
            {
                var data = await rtx.ReadFile(src, false, sqpack);
                File.WriteAllBytes(dest, data);
            } else if (src.EndsWith(".tex") || src.EndsWith(".atex"))
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

            Console.WriteLine("File saved to:" + dest);
            return 0;
        }
        public static async Task<int> WrapFile()
        {
            int paramStart = 0;
            int id = 0;
            do
            {
                if (id == _Args.Length)
                {
                    Console.WriteLine("Could not find file paths");
                    return -1;
                }
                if (!_Args[id].StartsWith("/"))
                {
                    paramStart = id;
                }
                else
                {
                    id++;
                }

            } while (paramStart == 0);

            if (!File.Exists(_Args[paramStart]) || !Path.IsPathRooted(_Args[paramStart+1]))
            {
                Console.WriteLine("Insufficient argument count for function.");
                return -1;
            }


            var src = _Args[paramStart];
            var dest = _Args[paramStart + 1];

            // Just dub something in with same extention if we weren't given one.
            // This will work for anything other than MDL.
            var ffPath = "chara/file" + Path.GetExtension(dest);
            if (paramStart + 2 < _Args.Length &&!_Args[paramStart+2].StartsWith("/"))
            {

                ffPath = _Args[paramStart + 2];
            }
            Console.WriteLine("Wrapping File: " + src);

            //option handling :3 this is going to be a massive conditional sowwy
            var options = new SmartImportOptions()
            {
                ModelOptions = new ModelImportOptions()
            };



            string flagStr = String.Empty;
            if (GetFlag("/tangents"))
            {
                options.ModelOptions.UseImportedTangents = true;
                flagStr += " Using imported tangents,";
            }
            if (GetFlag("/mats"))
            {
                options.ModelOptions.CopyMaterials = false;
                flagStr += " Not copying materials,";
            }
            if (GetFlag("/attributes"))
            {
                options.ModelOptions.CopyAttributes = false;
                flagStr += " Not copying attributes,";
            }
            if (GetFlag("/shiftuvs"))
            {
                options.ModelOptions.ShiftImportUV = false;
                flagStr += " Not shifting imported UV's,";
            }
            if (GetFlag("/cloneuv2"))
            {
                options.ModelOptions.CloneUV2 = true;
                flagStr += " Cloning UV1 to UV2,";
            }
            if (GetFlag("/autoscale"))
            {
                options.ModelOptions.AutoScale = false;
                flagStr += " Ignoring automatic model scaling,";
            }
            if (GetFlag("/heels"))
            {
                options.ModelOptions.AutoAssignHeels = false;
                flagStr += " Ignoring automatic heels attribute,";
            }
            if (flagStr != String.Empty)
            {
                Console.WriteLine("Creating model with the following options:" + flagStr.Remove(flagStr.Length - 1) + ".");
            }
            var sqpack = GetFlag("/sqpack");
            var parsed = new byte[0];
            if (sqpack)
            {
                parsed = await SmartImport.CreateCompressedFile(src, ffPath);
            }
            else
            {

                parsed = await SmartImport.CreateUncompressedFile(src, ffPath, options: options);
            }

            File.WriteAllBytes(dest, parsed);
            Console.WriteLine("Wrapped file saved to: " + dest);
            return 0;
        }

        public static async Task<int> UnwrapFile()
        {
            if (_Args.Length < 3)
            {
                Console.WriteLine("Insufficient argument count for function.");
                return -1;
            }

            var ffPath = "";
            if(_Args.Length > 3)
            {
                ffPath = _Args[3];
            }

            var src = _Args[1];
            var dest = _Args[2];

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
            }
            else if (src.EndsWith(".tex") || src.EndsWith(".atex"))
            {
                var tex = XivTex.FromUncompressedTex(data);
                await tex.SaveAs(dest);
            }
            else if (src.EndsWith(".mdl"))
            {
                var mdl = Mdl.GetXivMdl(data);
                var ttm = await TTModel.FromRaw(mdl);
                ttm.Source = ffPath;
                await Mdl.ExportTTModelToFile(ttm, dest, 1, null, rtx);
            }
            else
            {
                File.WriteAllBytes(dest, data);
            }

            Console.WriteLine("Unwrapped File saved to: " + dest);
            return 0;
        }


        public static async Task<int> ShowHelp()
        {
            System.Console.WriteLine("==== ConsoleTools Help ====");
            System.Console.WriteLine("");
            System.Console.WriteLine("== Commands ==");
            System.Console.WriteLine("\t/? - Help => You're looking at it.");
            System.Console.WriteLine("");
            System.Console.WriteLine("\t/upgrade [ModpackFilePath] [DestFilePath] - Updates a given Modpack for Dawntrail.");
            System.Console.WriteLine("");
            System.Console.WriteLine("\t/resave [ModpackFilePath] [DestFilePath] - Re-Saves a given modpack file to a new path or type, after performing basic file processing.");
            System.Console.WriteLine("");
            System.Console.WriteLine("\t/extract [FfxivInternalPath] [DestFilePath] - Extracts a given file from FFXIV.  May be SQPacked with /sqpack");
            System.Console.WriteLine("");
            System.Console.WriteLine("\t/wrap [SourceFilePath] [DestFilePath] [IntendedFfxivFilePath] [Options] - Creates an FFXIV format file from the given source file.  May be SQPacked with /sqpack.  FF Path only needed for MDLs. Supports the flags /tangents to use imported tangents, /mats to not copy materials, /attributes to not copy attributes, /shiftuvs to not shift imported uvs, /cloneuv2 to clone uv1 to uv2, /autoscale to ignore automatic model scaling, and /heels to ignore automatic heels attribute \");");
            System.Console.WriteLine("");
            System.Console.WriteLine("\t/unwrap [SourceFilePath] [DestFilePath] [IntendedFfxivFilePath] - Unwraps a given on-disk SqPacked or Flat FFXIV file into the given format. FF Path only needed for MDLs Skeleton/Texture info.");
            System.Console.WriteLine("");
            System.Console.WriteLine("\t/list [RootId] - List the entire collection of files associated with a given root ID. ( Ex. c0101h0010 )");
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

