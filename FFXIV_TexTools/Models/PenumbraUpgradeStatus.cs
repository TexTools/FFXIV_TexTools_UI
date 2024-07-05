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
            IOUtil.CompressWindowsDirectory(target);

            var res = EUpgradeResult.Failure;
            try
            {
                await ModpackUpgrader.UpgradeModpack(source, target);
                res = EUpgradeResult.Success;
            } catch (Exception ex)
            {
                if (source != target)
                {
                    IOUtil.RecursiveDeleteDirectory(target);
                    IOUtil.CompressWindowsDirectory(target);
                    IOUtil.CopyFolder(source, target);
                }

                res = EUpgradeResult.Failure;
                Trace.WriteLine("Modpack Upgrade Failure for Penumbra Mod: " + mod);
                Trace.WriteLine(ex);
            }


            if (Upgrades.ContainsKey(mod))
            {
                Upgrades[mod] = res;
            }
            return res;
        }
    }
}
