using FFXIV_TexTools.Helpers;
using FFXIV_TexTools.Resources;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using xivModdingFramework.General;
using xivModdingFramework.General.DataContainers;
using xivModdingFramework.General.Enums;
using xivModdingFramework.Mods.DataContainers;

namespace FFXIV_TexTools.Views.Metadata
{
    /// <summary>
    /// Interaction logic for RaceGenderScalingEditor.xaml
    /// </summary>
    public partial class RaceGenderScalingEditor : Window
    {
        private XivSubRace Race;
        private XivGender Gender;
        RacialGenderScalingParameter _data;
        public RaceGenderScalingEditor(XivSubRace race, XivGender gender)
        {
            Race = race;
            Gender = gender;
            InitializeComponent();

            Race = race;
            Gender = gender;
        }

        public async Task Init()
        {
            if (Race == XivSubRace.Invalid)
            {
                this.Close();
                return;
            }

            _data = await CMP.GetScalingParameter(Race, Gender);

            Title = $"Racial Settings - {Race.GetDisplayName()._()} - {Gender.ToString()._()}".L();
            TitleBox.Content = $"Racial Settings: {Race.GetDisplayName()._()} - {Gender.ToString()._()}".L();

            MinHeightBox.Text = _data.MinSize.ToString();
            MaxHeightBox.Text = _data.MaxSize.ToString();

            MinTailBox.Text = _data.MinTail.ToString();
            MaxTailBox.Text = _data.MaxTail.ToString();

            MinBustX.Text = _data.BustMinX.ToString();
            MinBustY.Text = _data.BustMinY.ToString();
            MinBustZ.Text = _data.BustMinZ.ToString();

            MaxBustX.Text = _data.BustMaxX.ToString();
            MaxBustY.Text = _data.BustMaxY.ToString();
            MaxBustZ.Text = _data.BustMaxZ.ToString();

            if (Gender != XivGender.Female)
            {
                MinBustXLabel.Visibility = Visibility.Collapsed;
                MinBustYLabel.Visibility = Visibility.Collapsed;
                MinBustZLabel.Visibility = Visibility.Collapsed;

                MaxBustXLabel.Visibility = Visibility.Collapsed;
                MaxBustYLabel.Visibility = Visibility.Collapsed;
                MaxBustZLabel.Visibility = Visibility.Collapsed;

                MinBustX.Visibility = Visibility.Collapsed;
                MinBustY.Visibility = Visibility.Collapsed;
                MinBustZ.Visibility = Visibility.Collapsed;

                MaxBustX.Visibility = Visibility.Collapsed;
                MaxBustY.Visibility = Visibility.Collapsed;
                MaxBustZ.Visibility = Visibility.Collapsed;
            }

            ShowDialog();
        }

        private async void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _data.MinSize = float.Parse(MinHeightBox.Text);
                _data.MaxSize = float.Parse(MaxHeightBox.Text);

                _data.MinTail = float.Parse(MinTailBox.Text);
                _data.MaxTail = float.Parse(MaxTailBox.Text);

                if (Gender == XivGender.Female)
                {
                    _data.BustMinX = float.Parse(MinBustX.Text);
                    _data.BustMinY = float.Parse(MinBustY.Text);
                    _data.BustMinZ = float.Parse(MinBustZ.Text);

                    _data.BustMaxX = float.Parse(MaxBustX.Text);
                    _data.BustMaxY = float.Parse(MaxBustY.Text);
                    _data.BustMaxZ = float.Parse(MaxBustZ.Text);
                }

            }
            catch(Exception Ex)
            {
                FlexibleMessageBox.Show("Cannot save changes: One or more values are not valid.".L(), "Invalid Data Error".L(), System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Error);
                return;
            }

            try
            {
                ResetButton.IsEnabled = false;
                CancelButton.IsEnabled = false;
                SaveButton.IsEnabled = false;
                SaveButton.Content = "Working...".L();

                await CMP.SaveScalingParameter(_data, XivStrings.TexTools);

                this.Close();
            } catch(Exception ex)
            {
                FlexibleMessageBox.Show("Cannot save changes:\n\nError: ".L() + ex.Message, "Save Scaling Error".L(), System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Error);

                ResetButton.IsEnabled = true;
                CancelButton.IsEnabled = true;
                SaveButton.IsEnabled = true;
                SaveButton.Content = "Save";
                return;
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private async void ResetButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                ResetButton.IsEnabled = false;
                CancelButton.IsEnabled = false;
                SaveButton.IsEnabled = false;
                ResetButton.Content = "Working...".L();

                await CMP.DisableRgspMod(Race, Gender, MainWindow.DefaultTransaction);

                this.Close();
            }
            catch (Exception ex)
            {
                FlexibleMessageBox.Show("Cannot save changes:\n\nError: ".L() + ex.Message, "Save Scaling Error".L(), System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Error);

                ResetButton.IsEnabled = true;
                CancelButton.IsEnabled = true;
                SaveButton.IsEnabled = true;
                ResetButton.Content = "Restore Defaults".L();
                return;
            }
        }
    }
}
