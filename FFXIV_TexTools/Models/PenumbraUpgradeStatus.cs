using Newtonsoft.Json.Converters;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using xivModdingFramework.Helpers;
using FFXIV_TexTools.Helpers;
using System.Diagnostics;
using xivModdingFramework.Mods;
using xivModdingFramework.Exd.FileTypes;
using FFXIV_TexTools.Views.Upgrades;

namespace FFXIV_TexTools.Models
{
    public class PenumbraUpgradeStatus : ICloneable
    {
        [JsonConverter(typeof(StringEnumConverter))]
        public enum EUpgradeResult
        {
            NotStarted,
            InProgress,
            Failure,
            Success,
            Unchanged,
        }

        public Dictionary<string, EUpgradeResult> Upgrades = new Dictionary<string, EUpgradeResult>();

        public async Task<EUpgradeResult> ProcessMod(string baseDir, string targetDir, string mod, bool compress = true)
        {
            var source = Path.GetFullPath(Path.Combine(baseDir, mod));
            var target = Path.GetFullPath(Path.Combine(targetDir, mod));

            if (source != target) {
                IOUtil.RecursiveDeleteDirectory(target);
            }

            Directory.CreateDirectory(target);

            var res = EUpgradeResult.Failure;
            try
            {
                var result = await ModpackUpgrader.UpgradeModpack(source, target, false);

                if (!result)
                {
                    // If we did nothing, just direct copy the mod, rather than doing the more expensive
                    // PMP write.
                    IOUtil.CopyFolder(source, target);
                }

                res = result ? EUpgradeResult.Success : EUpgradeResult.Unchanged;



            } catch (Exception ex)
            {
                LogUpgradeError(targetDir, mod, ex);
                if (Directory.Exists(target))
                {
                    if (source != target)
                    {
                        try
                        {
                            IOUtil.RecursiveDeleteDirectory(target);
                        }
                        catch(Exception ex2)
                        {
                            throw new Exception("Unable to delete directory for failed conversion, possibly due to security issue: " + target + "\n"+ ex2.Message + "\n\nOriginal Conversion Failure: " + ex.Message);
                        }

                        try
                        {
                            IOUtil.CopyFolder(source, target);
                        }
                        catch (Exception ex2)
                        {
                            throw new Exception("Unable to copy failed mod directory to destination.\nFrom: " + source + "\nTo: " + target + "\n" + ex2.Message + "\n\nOriginal Conversion Failure: " + ex.Message);
                        }
                    }
                }
                res = EUpgradeResult.Failure;
                Trace.WriteLine("Modpack Upgrade Failure for Penumbra Mod: " + mod);
                Trace.WriteLine(ex);
            }

            if (compress)
            {
                await IOUtil.CompressWindowsDirectory(target);
            }

            lock (PenumbraLibraryUpgradeWindow._ResultsLock)
            {
                if (Upgrades.ContainsKey(mod))
                {
                    Upgrades[mod] = res;
                }
            }
            return res;
        }

        private static object _logLock = new object();

        private static void LogUpgradeError(string targetDir, string mod, Exception ex)
        {
            try
            {
                lock (_logLock)
                {
                    Directory.CreateDirectory(targetDir);
                    var path = Path.GetFullPath(Path.Combine(targetDir, "upgrade_errors.txt"));

                    var text = "";
                    if (File.Exists(path))
                    {
                        text = File.ReadAllText(path);
                    }

                    text += "\n\n ================ MOD FAILURE : " + mod + " ================== \n";
                    text += ex.Message + "\n";
                    text += ex.StackTrace;

                    while(ex.InnerException != null)
                    {
                        ex = ex.InnerException;
                        text += "";
                        text += ex.Message + "\n";
                        text += ex.StackTrace;
                    }

                    File.WriteAllText(path, text);
                }
            }
            catch(Exception e)
            {
                Trace.WriteLine(e);
            }
        }

        public object Clone()
        {
            var cl = (PenumbraUpgradeStatus) MemberwiseClone();
            cl.Upgrades = new Dictionary<string, EUpgradeResult>(Upgrades);
            return cl;
        }
    }
}
