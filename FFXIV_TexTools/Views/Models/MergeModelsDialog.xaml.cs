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
using xivModdingFramework.Variants.FileTypes;
using xivModdingFramework.Mods;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace FFXIV_TexTools.Views
{
    /// <summary>
    /// Interaction logic for CopyModelDialog.xaml
    /// </summary>
    public partial class MergeModelsDialog : INotifyPropertyChanged
    {
        private ObservableCollection<int> ImcVariantSource = new ObservableCollection<int>();
        private ObservableCollection<KeyValuePair<string, int>> _MeshIdSource = new ObservableCollection<KeyValuePair<string, int>>();

        public ObservableCollection<KeyValuePair<string, int>> MeshIdSource
        {
            get
            {
                return _MeshIdSource;
            } 
            set
            {
                _MeshIdSource = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(MeshIdSource)));
            }
        }

        public MergeModelsDialog(string startingTarget = null)
        {
            InitializeComponent();
            DataContext = this;
            
            ImcVariantSource.Add(0);
            VariantBox.ItemsSource = ImcVariantSource;
            VariantBox.SelectedIndex = 0;

            DebounceUpdate = ViewHelpers.Debounce(LoadVariants);
            MeshIdSource.Add(new KeyValuePair<string, int>("ALL", -1));
            MeshIdBox.SelectedIndex = 0;

            CopyMaterialsBox.Checked += CopyMaterialsBox_Checked;
            CopyMaterialsBox.Unchecked += CopyMaterialsBox_Checked;

            if (!string.IsNullOrWhiteSpace(startingTarget))
            {
                ToBox.Text = startingTarget;
                ToBox.IsEnabled = false;
            }
        }

        private async void CopyMaterialsBox_Checked(object sender, RoutedEventArgs e)
        {
            try
            {
                await UpdateText();
            }
            catch (Exception ex)
            {
                Trace.WriteLine(ex);
            }
        }

        private string _lastFrom;

        private Action DebounceUpdate;

        public event PropertyChangedEventHandler PropertyChanged;

        private async void AnyTextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                await UpdateText();
            } catch(Exception ex)
            {
                Trace.WriteLine(ex);
            }
        }

        private async Task UpdateText()
        {

            var to = ToBox.Text;
            var from = FromBox.Text;

            if (from != _lastFrom)
            {
                _lastFrom = from;
                DebounceUpdate();
            }

            if (string.IsNullOrWhiteSpace(to) || string.IsNullOrWhiteSpace(from))
            {
                CopyButton.IsEnabled = false;
                return;
            }

            to = to.Trim().ToLower();
            from = from.Trim().ToLower();

            if (!to.EndsWith(".mdl") || !from.EndsWith(".mdl"))
            {
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

                if (df != df2)
                {
                    MaterialCopyNotice.Text = "Source and target files must exist within the same data file.".L();
                    MaterialCopyNotice.Foreground = Brushes.Red;

                    RaceChangeNotice.Text = "--";
                    RaceChangeNotice.Foreground = Brushes.Black;

                    CopyButton.IsEnabled = false;
                    return;
                }

            }
            catch
            {
                MaterialCopyNotice.Text = "At least one file path is not a valid internal FFXIV file path.".L();
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
                MaterialCopyNotice.Text = "Unknown File Root - Merge may be unsuccessful.".L();
                MaterialCopyNotice.Foreground = Brushes.DarkGoldenrod;
                return;
            }
            else
            {
                if (CopyMaterialsBox.IsChecked == true)
                {
                    MaterialCopyNotice.Text = "Materials and textures will be copied to destination root folder.".L();
                    MaterialCopyNotice.Foreground = Brushes.Green;
                }
                else
                {
                    MaterialCopyNotice.Text = "Material and texture stubs will be created at the destination root folder.".L();
                    MaterialCopyNotice.Foreground = Brushes.Green;
                }
            }

            var raceRegex = new Regex("c([0-9]{4})");

            var toMatch = raceRegex.Match(to);
            var fromMatch = raceRegex.Match(from);

            if (!toMatch.Success || !fromMatch.Success)
            {
                RaceChangeNotice.Text = "Model is not racial - Model will not be racially adjusted.".L();
                RaceChangeNotice.Foreground = Brushes.Black;
                return;
            }

            var toRace = XivRaces.GetXivRace(toMatch.Groups[1].Value);
            var fromRace = XivRaces.GetXivRace(fromMatch.Groups[1].Value);

            if (toRace == fromRace)
            {
                RaceChangeNotice.Text = "Model races are identical - Model will not be racially adjusted.".L();
                RaceChangeNotice.Foreground = Brushes.Black;
                return;
            }


            RaceChangeNotice.Text = $"Model will be adjusted from {fromRace.GetDisplayName()._()} to {toRace.GetDisplayName()._()}.".L();
            RaceChangeNotice.Foreground = Brushes.Green;
        }

        private async void LoadVariants()
        {
            try
            {
                await await Dispatcher.InvokeAsync(async () =>
                {

                    ImcVariantSource.Clear();
                    ImcVariantSource.Add(0);
                    VariantBox.SelectedIndex = 0;

                    // Validation
                    var from = FromBox.Text;
                    if (!IOUtil.IsFFXIVInternalPath(from)) return;

                    from = from.Trim().ToLower();
                    if (!from.EndsWith(".mdl")) return;

                    var fromRoot = await XivCache.GetFirstRoot(from);
                    if (fromRoot == null) return;
                    if (!Imc.UsesImc(fromRoot)) return;

                    var tx = MainWindow.DefaultTransaction;

                    if (!await tx.FileExists(from)) return;


                    var imcInfo = await Imc.GetFullImcInfo(fromRoot.GetRawImcFilePath(), false, tx);

                    if (imcInfo.SubsetCount == 0) return;

                    ImcVariantSource.Clear();
                    for (int i = 0; i < imcInfo.SubsetCount + 1; i++)
                    {
                        ImcVariantSource.Add(i);
                    }
                    VariantBox.SelectedIndex = 1;

                    var mdl = await Mdl.GetTTModel(from, false, tx);
                    MeshIdSource.Clear();
                    MeshIdSource.Add(new KeyValuePair<string, int>("ALL", -1));

                    for(int i = 0; i < mdl.MeshGroups.Count; i++)
                    {
                        MeshIdSource.Add(new KeyValuePair<string, int>(i.ToString(), i));
                    }

                    MeshIdBox.SelectedIndex = 0;
                });
            }
            catch(Exception ex)
            {
                Trace.WriteLine(ex);
            }
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



                await Mdl.MergeModels(to, from, VariantBox.SelectedIndex, (int)MeshIdBox.SelectedValue, CopyMaterialsBox.IsChecked == true ? true : null, XivStrings.TexTools, MainWindow.UserTransaction);
                FlexibleMessageBox.Show("Model Copied Successfully.".L(), "Model Copy Confirmation".L(), System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Information);
                Close();
            }
            catch(Exception ex)
            {
                FlexibleMessageBox.Show("Model Copied Failed.\n\nError: ".L() + ex.Message, "Model Copy Error".L(), System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Error);
            }

        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
