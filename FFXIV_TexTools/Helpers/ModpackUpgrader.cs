using FFXIV_TexTools.Properties;
using FFXIV_TexTools.Views.Wizard;
using FFXIV_TexTools.Views;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using xivModdingFramework.Helpers;
using System.Windows.Forms;
using System.IO;
using System.Diagnostics;

namespace FFXIV_TexTools.Helpers
{
    /// <summary>
    /// Full modpack upgrade handler.
    /// This lives in the UI project as it relies on the WizardData handler for simplification of modpack type handling.
    /// </summary>
    public static class ModpackUpgrader
    {
        public static async Task UpgradeModpackPrompted(bool includePartials = true)
        {
#if ENDWALKER
            return;
#endif
            var mw = MainWindow.GetMainWindow();
            var ofd = new OpenFileDialog()
            {
                Filter = ViewHelpers.LoadModpackFilter,
                InitialDirectory = Path.GetFullPath(Settings.Default.ModPack_Directory),
            };

            if (ofd.ShowDialog() != System.Windows.Forms.DialogResult.OK)
            {
                return;
            }

            var path = ofd.FileName;


            await mw.LockUi("Upgrading Modpack");
            try
            {

                var data = await UpgradeModpack(path, includePartials);

                var ext = Path.GetExtension(path);

                var name = Path.GetFileNameWithoutExtension(path);
                if (ext == ".json")
                {
                    name = IOUtil.MakePathSafe(data.MetaPage.Name, false);
                }

                if(ext != ".ttmp2" && ext != ".pmp")
                {
                    ext = ".pmp";
                }


                // Final Save location
                var dir = Path.GetDirectoryName(path);
                var fName = name + "_dt" + ext;
                var sfd = new SaveFileDialog()
                {
                    FileName = fName,
                    Filter = ViewHelpers.ModpackFileFilter,
                    InitialDirectory = dir,
                };
                if (sfd.ShowDialog() != System.Windows.Forms.DialogResult.OK)
                {
                    return;
                }
                var newPath = sfd.FileName;
                await data.WriteModpack(newPath);
            }
            catch (Exception ex)
            {
                ViewHelpers.ShowError("Modpack Upgrade Error", "An error occurred while upgrading the modpack:\n\n" + ex.Message);
            }
            finally
            {
                await mw.UnlockUi();
            }
        }

        public static async Task<WizardData> UpgradeModpack(string path, bool includePartials = true)
        {

            var data = await WizardData.FromModpack(path);
            var textureUpgradeTargets = new Dictionary<string, EndwalkerUpgrade.UpgradeInfo>();

            var allTextures = new HashSet<string>();

            // First Round Upgrade -
            // This does models and base MTRLS only.
            foreach (var p in data.DataPages)
            {
                foreach (var g in p.Groups)
                {
                    foreach (var o in g.Options)
                    {
                        if (o.StandardData != null)
                        {
                            try
                            {
                                var missing = await EndwalkerUpgrade.UpdateEndwalkerFiles(o.StandardData.Files);
                                foreach (var kv in missing)
                                {
                                    if (!textureUpgradeTargets.ContainsKey(kv.Key))
                                    {
                                        textureUpgradeTargets.Add(kv.Key, kv.Value);
                                    }
                                }

                                var textures = o.StandardData.Files.Select(x => x.Key).Where(x => x.EndsWith(".tex"));
                                allTextures.UnionWith(textures);
                            }
                            catch (Exception ex)
                            {
                                var mes = "An error occurred while updating Group: " + g.Name + " - Option: " + o.Name + "\n\n" + ex.Message; ;
                                throw new Exception(mes);
                            }
                        }
                    }
                }
            }


            // Second Round Upgrade - This does textures based on the collated upgrade information from the previous pass
            foreach (var p in data.DataPages)
            {
                foreach (var g in p.Groups)
                {
                    foreach (var o in g.Options)
                    {
                        if (o.StandardData != null)
                        {
                            try
                            {
                                await EndwalkerUpgrade.UpgradeRemainingTextures(o.StandardData.Files, textureUpgradeTargets);
                            }
                            catch (Exception ex)
                            {
                                var mes = "An error occurred while updating Group: " + g.Name + " - Option: " + o.Name + "\n\n" + ex.Message; ;
                                throw new Exception(mes);
                            }
                        }
                    }
                }
            }


            if (includePartials)
            {
                // Find all un-referenced textures.
                var unusedTextures = new HashSet<string>(
                    allTextures.Where(t =>
                        !textureUpgradeTargets.Any(x =>
                            x.Value.Files.ContainsValue(t)
                        )));


                // Third Round Upgrade - This inspects as-of-yet unupgraded textures for possible jank-upgrades,
                // Which is to say, upgrades where we can infer their usage and pairing, but the base mtrl was not included.
                foreach (var p in data.DataPages)
                {
                    foreach (var g in p.Groups)
                    {
                        foreach (var o in g.Options)
                        {
                            if (o.StandardData != null)
                            {
                                var contained = unusedTextures.Where(x => o.StandardData.Files.ContainsKey(x));
                                await EndwalkerUpgrade.UpdateUnclaimedHairTextures(contained.ToList(), "Unused", null, null, o.StandardData.Files);

                                foreach (var possibleMask in contained)
                                {
                                    await EndwalkerUpgrade.UpdateEyeMask(possibleMask, "Unused", null, null, o.StandardData.Files);
                                }
                            }
                        }
                    }
                }
            }

            return data;
        }

        public static async Task UpgradeModpack(string path, string newPath, bool includePartials = true)
        {
#if ENDWALKER
            return;
#endif
            var data = await UpgradeModpack(path, includePartials);

            await data.WriteModpack(newPath);
        }
    }
}
