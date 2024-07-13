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

namespace FFXIV_TexTools.Models
{
    public class PenumbraUpgradeStatus
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

        public async Task<EUpgradeResult> ProcessMod(string baseDir, string targetDir, string mod)
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
                var s = await ModpackUpgrader.UpgradeModpack(source, target);
                res = s ? EUpgradeResult.Success : EUpgradeResult.Unchanged;
            } catch (Exception ex)
            {
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

            await IOUtil.CompressWindowsDirectory(target);

            if (Upgrades.ContainsKey(mod))
            {
                Upgrades[mod] = res;
            }
            return res;
        }
    }
}
