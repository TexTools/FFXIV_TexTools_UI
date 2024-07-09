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

            if (ofd.ShowDialog() != System.Windows.Forms.DialogResult.OK)
            {
                return;
            }

            var path = ofd.FileName;


            await mw.LockUi("Upgrading Modpack");
            try
            {

                var data = await xivModdingFramework.Mods.ModpackUpgrader.UpgradeModpack(path, includePartials);

                var ext = Path.GetExtension(path);

                var name = Path.GetFileNameWithoutExtension(path);
                if (ext == ".json")
                {
                    name = IOUtil.MakePathSafe(data.MetaPage.Name, false);
                }

                if (ext != ".ttmp2" && ext != ".pmp")
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
                await data.WriteModpack(newPath, true);
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
