using FFXIV_TexTools.Helpers;
using FFXIV_TexTools.Resources;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using FFXIV_TexTools.Properties;
using xivModdingFramework.Cache;
using xivModdingFramework.General.Enums;
using xivModdingFramework.Helpers;
using xivModdingFramework.Models.FileTypes;

namespace FFXIV_TexTools.Views
{
    /// <summary>
    /// Interaction logic for CopyModelDialog.xaml
    /// </summary>
    public partial class CopyModelDialog : Window
    {
        public CopyModelDialog()
        {
            InitializeComponent();
        }

        private async void AnyTextChanged(object sender, TextChangedEventArgs e)
        {
            var to = ToBox.Text;
            var from = FromBox.Text;

            if (string.IsNullOrWhiteSpace(to) || string.IsNullOrWhiteSpace(from))
            {
                CopyButton.IsEnabled = false;
                return;
            }

            to = to.Trim().ToLower();
            from = from.Trim().ToLower();

            if(!to.EndsWith(".mdl") || !from.EndsWith(".mdl")) {
                MaterialCopyNotice.Text = "--";
                MaterialCopyNotice.Foreground = Brushes.Black;

                RaceChangeNotice.Text = "--";
                RaceChangeNotice.Foreground = Brushes.Black;

                CopyButton.IsEnabled = false;
                return;
            }

            try
            {
                var df = IOUtil.GetDataFileFromPath(to);
                var df2 = IOUtil.GetDataFileFromPath(from);

                if(df != df2)
                {
                    MaterialCopyNotice.Text = "Source and target files must exist within the same data file.";
                    MaterialCopyNotice.Foreground = Brushes.Red;

                    RaceChangeNotice.Text = "--";
                    RaceChangeNotice.Foreground = Brushes.Black;

                    CopyButton.IsEnabled = false;
                    return;
                }

            } catch
            {
                MaterialCopyNotice.Text = "At least one file path is not a valid internal FFXIV file path.";
                MaterialCopyNotice.Foreground = Brushes.Red;

                RaceChangeNotice.Text = "--";
                RaceChangeNotice.Foreground = Brushes.Black;

                CopyButton.IsEnabled = false;
                return;
            }

            CopyButton.IsEnabled = true;
            var toRoot = await XivCache.GetFirstRoot(to);
            var fromRoot = await XivCache.GetFirstRoot(from);

            if (toRoot == null || fromRoot == null)
            {
                MaterialCopyNotice.Text = "Unknown File Root - Materials and textures will not be copied.";
                MaterialCopyNotice.Foreground = Brushes.DarkGoldenrod;

                RaceChangeNotice.Text = "Unknown File Root - Model will not be racially adjusted.";
                RaceChangeNotice.Foreground = Brushes.DarkGoldenrod;
                return;
            } else
            {
                MaterialCopyNotice.Text = "Materials and textures will be copied to destination root folder.";
                MaterialCopyNotice.Foreground = Brushes.Green;
            }

            var raceRegex = new Regex("c([0-9]{4})");

            var toMatch = raceRegex.Match(to);
            var fromMatch = raceRegex.Match(from);

            if (!toMatch.Success || !fromMatch.Success)
            {
                RaceChangeNotice.Text = "Model is not racial - Model will not be racially adjusted.";
                RaceChangeNotice.Foreground = Brushes.Black;
                return;
            }

            var toRace = XivRaces.GetXivRace(toMatch.Groups[1].Value);
            var fromRace = XivRaces.GetXivRace(fromMatch.Groups[1].Value);

            if(toRace == fromRace)
            {
                RaceChangeNotice.Text = "Model races are identical - Model will not be racially adjusted.";
                RaceChangeNotice.Foreground = Brushes.Black;
                return;
            }


            RaceChangeNotice.Text = "Model will be adjusted from " + fromRace.GetDisplayName() + " to " + toRace.GetDisplayName() + ".";
            RaceChangeNotice.Foreground = Brushes.Green;


        }

        private async void CopyButton_Click(object sender, RoutedEventArgs e)
        {
            var to = ToBox.Text;
            var from = FromBox.Text;
            try
            {

                if (string.IsNullOrWhiteSpace(to) || string.IsNullOrWhiteSpace(from)) return;
                to = to.Trim().ToLower();
                from = from.Trim().ToLower();

                if (!to.EndsWith(".mdl") || !from.EndsWith(".mdl"))
                {
                    return;
                }

                var toRoot = await XivCache.GetFirstRoot(to);
                var fromRoot = await XivCache.GetFirstRoot(from);
                var df = IOUtil.GetDataFileFromPath(to);

                var _mdl = new Mdl(XivCache.GameInfo.GameDirectory, df);

                await _mdl.CopyModel(from, to, XivStrings.TexTools, true, Settings.Default.Lumina_IsEnabled, new DirectoryInfo(Settings.Default.Lumina_Directory ?? string.Empty));
                FlexibleMessageBox.Show("Model Copied Successfully.", "Model Copy Confirmation", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Information);
                Close();
            }
            catch(Exception ex)
            {
                FlexibleMessageBox.Show("Model Copied Failed.\n\nError: " + ex.Message, "Model Copy Error", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Error);
            }

        }
    }
}
