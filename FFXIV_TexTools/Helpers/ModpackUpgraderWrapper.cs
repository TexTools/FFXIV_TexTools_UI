using FFXIV_TexTools.Properties;
using FFXIV_TexTools.Views;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using xivModdingFramework.Helpers;

namespace FFXIV_TexTools.Helpers
{
    internal class ModpackUpgraderWrapper
    {
        public static async Task UpgradeModpackPrompted(bool includePartials = true)
        {
            var mw = MainWindow.GetMainWindow();
            var ofd = new OpenFileDialog()
            {
                Filter = ViewHelpers.LoadModpackFilter,
                InitialDirectory = Path.GetFullPath(Settings.Default.ModPack_Directory),
            };

            ofd.Multiselect = true;
            if (ofd.ShowDialog() != System.Windows.Forms.DialogResult.OK)
            {
                return;
            }


            var paths = ofd.FileNames;


            await mw.LockUi("Upgrading Modpack", "Please Wait...\n\nIf this takes more than 3-5 minutes, please close TexTools and retry with \nOptions => Settings => 'Compress Upgrade Textures' turned off.");
            try
            {
                var i = 1;
                foreach (var path in paths)
                {
                    if(paths.Length > 1)
                    {
                        mw._lockProgressController.SetMessage("Updating Mod #" + i + "/" + paths.Length);
                    }
                    i++;

                    var data = await xivModdingFramework.Mods.ModpackUpgrader.UpgradeModpack(path, includePartials);

                    if (!data.AnyChanges && paths.Length == 1)
                    {
                        FlexibleMessageBox.Show(ViewHelpers.GetWin32Window(MainWindow.GetMainWindow()),
                             "The upgrader found nothing to upgrade in the modpack.\n\nThis typically means the mod either does not need to be upgraded, must be manually upgraded, or was already upgraded, possibly by another tool (Ex. Penumbra).",
                             "No Upgrade Needed",
                             MessageBoxButtons.OK, MessageBoxIcon.Information);
                        return;
                    }

                    var ext = Path.GetExtension(path);

                    var name = Path.GetFileNameWithoutExtension(path);
                    if (ext == ".json")
                    {
                        name = IOUtil.MakePathSafe(data.Data.MetaPage.Name, false);
                    }

                    if (ext != ".ttmp2" && ext != ".pmp")
                    {
                        ext = ".pmp";
                    }


                    // Final Save location
                    var dir = Path.GetDirectoryName(path);
                    var fName = name + "_dt" + ext;
                    var newPath = Path.GetFullPath(Path.Combine(dir, fName));
                    if (paths.Length == 1)
                    {
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
                        newPath = sfd.FileName;
                    }

                    await data.Data.WriteModpack(newPath, true);
                }
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
    }
}
