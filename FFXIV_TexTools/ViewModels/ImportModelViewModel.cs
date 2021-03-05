using FFXIV_TexTools.Helpers;
using FFXIV_TexTools.Properties;
using FFXIV_TexTools.Resources;
using FFXIV_TexTools.Views.Models;
using SharpDX;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows.Media;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Forms;
using xivModdingFramework.General.Enums;
using xivModdingFramework.Helpers;
using xivModdingFramework.Items;
using xivModdingFramework.Items.Interfaces;
using xivModdingFramework.Models.DataContainers;
using xivModdingFramework.Models.FileTypes;
using xivModdingFramework.Models.Helpers;
using System.Text.RegularExpressions;
using xivModdingFramework.Items.DataContainers;

namespace FFXIV_TexTools.ViewModels
{
    public class ImportModelViewModel
    {
        private const int ExpandedHeight = 680;
        private const double CloseDelay = 3000f;

        private ImportModelView _view;
        private IItemModel _item;
        private XivRace _race;
        private Mdl _mdl;
        private List<string> _importers;
        private bool _dataOnly;
        private string _internalPath;
        private string _submeshId;
        private System.Timers.Timer _closeTimer;
        private bool _anyWarnings = false;


        private bool _success = false;
        private Action _onComplete;
        public bool Success
        {
            get
            {
                return _success;
            }
        }

        private bool _showEditor;

        private async Task AssignPath() {
            var result = await _mdl.GetMdlPath(_item, _race, _submeshId);
            _internalPath = result;
        }



        private void SetupRaces()
        {
            var races = Eqp.PlayableRaces;

            _view.RaceComboBox.SelectedValuePath = "Key";
            _view.RaceComboBox.DisplayMemberPath = "Value";

            _view.RaceComboBox.IsEnabled = false;

            if (_race == XivRace.All_Races)
            {
                _view.OverrideRaceButton.IsEnabled = false;
                var kv = new KeyValuePair<XivRace, string>(XivRace.All_Races, "--");
                _view.RaceComboBox.Items.Add(kv);
            } else
            {
                foreach (var race in races)
                {
                    var kv = new KeyValuePair<XivRace, string>(race, XivRaces.GetDisplayName(race));
                    _view.RaceComboBox.Items.Add(kv);
                }
            }

            _view.RaceComboBox.SelectedValue = _race;

        }

        /// <summary>
        /// Automatically attempts to set the racial override status based upon a selected file name.
        /// </summary>
        private void SetRaceOverrideByFileName()
        {
            if (String.IsNullOrWhiteSpace(_view.FileNameTextBox.Text)) return;

            var fileName = Path.GetFileNameWithoutExtension(_view.FileNameTextBox.Text);
            var raceRegex = new Regex("c([0-9]{4})");
            var match = raceRegex.Match(fileName);
            if (match.Success)
            {
                // Swap the race to a non-NPC version if it's an NPC race.
                var raceCode = match.Groups[1].Value.Substring(0, 2) + "01";
                var race = XivRaces.GetXivRace(raceCode);

                if(race != _race && race != XivRace.All_Races && _race != XivRace.All_Races)
                {
                    // We have a valid race.
                    _view.OverrideRaceButton.IsChecked = true;
                    _view.RaceComboBox.SelectedValue = race;
                }
            }
        }


        public ImportModelViewModel(ImportModelView view, IItemModel item, XivRace race, string submeshId, bool dataOnly, Action onComplete = null)
        {
            _view = view;
            _item = item;
            _race = race;
            _submeshId = submeshId;
            _dataOnly = dataOnly;
            _onComplete = onComplete;

            if(typeof(XivCharacter) == _item.GetType())
            {
                // Fix up naming scheme for character items to match user expectation.
                var clone = (XivCharacter)((XivCharacter)_item).Clone();
                clone.Name = clone.SecondaryCategory;
                _item = clone;
            }

            var gameDirectory = new DirectoryInfo(Settings.Default.FFXIV_Directory);
            var saveDirectory = new DirectoryInfo(Settings.Default.Save_Directory);
            var dataFile = IOUtil.GetDataFileFromPath(_item.GetItemRootFolder());
            _mdl = new Mdl(gameDirectory, dataFile);
            _importers = _mdl.GetAvailableImporters();

            SetupRaces();


            // We need to explicitly fork this onto a new thread to avoid deadlock.
            Task.Run(AssignPath).Wait();

            var defaultPath = $"{IOUtil.MakeItemSavePath(_item, saveDirectory, _race)}\\3D";
            defaultPath = defaultPath.Replace("/", "\\");
            var modelName = Path.GetFileNameWithoutExtension(_internalPath);


            // Scan to see which file type(s) actually exist.
            bool foundValidFile = false;

            // FBX is default, so check that first.
            var startingPath = Path.Combine(defaultPath, modelName) + ".fbx";
            if (File.Exists(startingPath))
            {
                foundValidFile = true;
            }
            if (!foundValidFile)
            {
                foreach (var suffix in _importers)
                {
                    startingPath = Path.Combine(defaultPath, modelName) + "." + suffix;
                    if (File.Exists(startingPath))
                    {
                        foundValidFile = true;
                        break;
                    }
                }
            }

            if (!foundValidFile)
            {
                startingPath = "";
            }


            _view.FileNameTextBox.Text = startingPath;

            // Event Handlers
            _view.SelectFileButton.Click += SelectFileButton_Click;
            _view.ImportButton.Click += ImportButton_Click;
            _view.EditButton.Click += EditButton_Click;
            _view.Closing += _view_Closing;
            _view.OverrideRaceButton.Checked += OverrideRaceButton_Checked;
            _view.OverrideRaceButton.Unchecked += OverrideRaceButton_Unchecked;

            // Default Settings for specific categories.
            if (item.SecondaryCategory == XivStrings.Face)
            {
                _view.UseOriginalShapeDataButton.IsChecked = true;
            }
            if (item.SecondaryCategory == XivStrings.Hair)
            {
                _view.CloneUV1Button.IsChecked = true;
            }

            var iType = item.GetPrimaryItemType();
            if (iType == xivModdingFramework.Items.Enums.XivItemType.equipment || iType == xivModdingFramework.Items.Enums.XivItemType.accessory || iType == xivModdingFramework.Items.Enums.XivItemType.weapon) {
                _view.ForceUVsButton.IsChecked = true;
            }
        }

        private void OverrideRaceButton_Checked(object sender, RoutedEventArgs e)
        {
            _view.RaceComboBox.IsEnabled = true;
        }
        private void OverrideRaceButton_Unchecked(object sender, RoutedEventArgs e)
        {
            _view.RaceComboBox.IsEnabled = false;
            _view.RaceComboBox.SelectedValue = _race;
        }


        private void _view_Closing(object sender, CancelEventArgs e)
        {
            _view.DialogResult = Success;
        }

        private void ImportButton_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            DoImport(false);
        }

        private void EditButton_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            DoImport(true);
        }

        private void DoImport(bool showEditor)
        {

            string path = null;
            _anyWarnings = false;
            if (_view.FileNameTextBox.Text != null && _view.FileNameTextBox.Text.Trim() != "") 
            {
                try
                {
                    var d = new DirectoryInfo(_view.FileNameTextBox.Text);
                    path = d.FullName;
                }
                catch(Exception ex)
                {
                    // Invalid directory.
                    FlexibleMessageBox.Show("The given file path is invalid.", "Import Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
            }

            _showEditor = showEditor;
            _view.Height = ExpandedHeight;
            _view.EnableAll(false);

            // Clear log.
            _view.LogTextBox.Document.Blocks.Clear();
            _view.LogTextBox.AppendText("");

            var options = new ModelModifierOptions();
            options.UseOriginalShapeData = _view.UseOriginalShapeDataButton.IsChecked == true ? true : false;
            options.ForceUVQuadrant = _view.ForceUVsButton.IsChecked == true ? true : false;
            options.ClearUV2 = _view.ClearUV2Button.IsChecked == true ? true : false;
            options.CloneUV2 = _view.CloneUV1Button.IsChecked == true ? true : false;
            options.ClearVAlpha = _view.ClearVAlphaButton.IsChecked == true ? true : false;
            options.ClearVColor = _view.ClearVColorButton.IsChecked == true ? true : false;
            options.AutoScale = _view.AutoScaleButton.IsChecked == true ? true : false;

            var selectedRace = XivRace.All_Races;
            if (_view.RaceComboBox.SelectedValue != null) {
                selectedRace = (XivRace)_view.RaceComboBox.SelectedValue;
            }

            if(selectedRace != XivRace.All_Races && selectedRace != _race)
            {
                options.SourceRace = selectedRace;
            }
            

            // Asynchronously call ImportModel.
            Task.Run(async () =>
           {
               try
               {
                   if (showEditor)
                   {
                       await _mdl.ImportModel(_item, _race, path, options, LogMessageReceived, IntermediateStep, XivStrings.TexTools, _submeshId, _dataOnly, Settings.Default.Lumina_IsEnabled, new DirectoryInfo(Settings.Default.Lumina_Directory));
                   }
                   else
                   {
                       await _mdl.ImportModel(_item, _race, path, options, LogMessageReceived, null, XivStrings.TexTools, _submeshId, _dataOnly, Settings.Default.Lumina_IsEnabled, new DirectoryInfo(Settings.Default.Lumina_Directory));
                   }
                   OnImportComplete();
               }
               catch (Exception ex)
               {
                    // This is kind of a weird construct, but ensures this is called
                    // on the main UI thread.
                    // main thread that has ownership to edit the Enabled values.
                    await _view.Dispatcher.BeginInvoke((ThreadStart)delegate ()
                   {
                       if (ex.Message != "cancel")
                       {
                           _anyWarnings = true;
                           WriteToLog("> [ERROR] " + ex.Message, Brushes.DarkRed);
                           WriteToLog("> Previous log messages may include more information.", Brushes.DarkRed);
                           FlexibleMessageBox.Show("An error occurred during import:\n" + ex.Message + "\n\nThe import has been cancelled.\nPlease see the text log for more information.", "Import Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                       }

                       _view.EnableAll(true);
                   });
               }
           });
        }

        private void WriteToLog(string text, Brush brush = null)
        {
            if (brush == null)
            {
                brush = Brushes.Black;
            }
            var paragraph = new Paragraph();
            Run run = new Run();
            run.Text = text;
            paragraph.Inlines.Add(run);

            paragraph.Foreground = brush;
            _view.LogTextBox.Document.Blocks.Add(paragraph);
        }


        /// <summary>
        /// This is called by the importer when the TTModel has been populated, but
        /// before it is injected into the XIV files.  This lets us step in and do whatever
        /// other manipulations we want on it.
        /// </summary>
        /// <param name="model"></param>
        /// <returns></returns>
        private async Task<bool> IntermediateStep(TTModel newModel, TTModel oldModel)
        {
            var result = false;
            await _view.Dispatcher.BeginInvoke((ThreadStart)delegate ()
            {
                try
                {
                    var editorWindow = new ImportModelEditView(newModel, oldModel) { Owner = _view };
                    result = editorWindow.ShowDialog() == true ? true : false;
                }
                catch (Exception Ex)
                {
                    throw Ex;
                }
            });
            return result;
        }

        /// <summary>
        /// Function called whenever the importer spits out a log line.
        /// </summary>
        /// <param name="isWarning"></param>
        /// <param name="message"></param>
        private void LogMessageReceived(bool isWarning, string message)
        {
            if (message == null || message.Trim() == "") return;

            _view.Dispatcher.BeginInvoke((ThreadStart)delegate ()
           {
               if (isWarning)
               {
                   _anyWarnings = true;
                   WriteToLog("> [WARN] " + message, Brushes.DarkGoldenrod);
               }
               else
               {
                   WriteToLog("> [INFO] " + message, Brushes.Black);
               }
           }).Wait(); // The .Wait() is just to help ensure we don't print log lines out of order.
        }

        /// <summary>
        /// This is called when the import is successfully completed.
        /// </summary>
        private void OnImportComplete()
        {
            _view.Dispatcher.BeginInvoke((ThreadStart)delegate ()
            {
                WriteToLog("> [SUCCESS] Model Imported Successfully.", Brushes.DarkGreen);
                _view.SetData(_mdl.GetRawData());
                _success = true;

                // Remove the old import button handler since it gets reused as a close button.
                _view.ImportButton.Click -= ImportButton_Click;

                _view.EnableClose();

                if (!_anyWarnings)
                {
                    _closeTimer = new System.Timers.Timer(CloseDelay);
                    _closeTimer.Elapsed += _closeTimer_Elapsed;
                    _closeTimer.Start();
                    _view.KeyDown += _view_KeyDown;
                    WriteToLog("> [INFO] This window will automatically close in 3 seconds... (ESC to cancel)", Brushes.Black);
                } else
                {
                    WriteToLog("> [INFO] At Least one warning or error occurred during import.  Window will remain open until manually closed.", Brushes.Black);

                }

                // If we have a callback function, trigger it, that way we can do model refreshes/etc. 
                // while the user is still looking at the log and feeling good about stuff.
                if (_onComplete != null)
                {
                    _onComplete();
                }
            }).Wait();
        }

        private void _view_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            var key = e.Key;
            if (key == System.Windows.Input.Key.Escape)
            {
                if (_closeTimer != null)
                {
                    _closeTimer.Elapsed -= _closeTimer_Elapsed;
                    _closeTimer.Stop();
                    _view.KeyDown += _view_KeyDown;
                    _closeTimer = null;
                    WriteToLog("> [INFO] Automatic close cancelled.", Brushes.Black);
                }
            }
        }

        private void _closeTimer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            _view.Dispatcher.BeginInvoke((ThreadStart)delegate ()
            {
                if (_closeTimer != null)
                {
                    _closeTimer.Elapsed -= _closeTimer_Elapsed;
                    _closeTimer.Stop();
                    _closeTimer = null;
                    if (_view != null)
                    {
                        _view.KeyDown += _view_KeyDown;
                    }
                }
                try
                {
                    _view.DialogResult = Success;
                }
                catch (Exception ex)
                {
                    //No-Op.  If this fails /bc window is already closed it doesn't matter.
                }
            }).Wait();
        }

        private void SelectFileButton_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            var openFileDialog = new System.Windows.Forms.OpenFileDialog();
            try
            {
                openFileDialog.InitialDirectory = Path.GetDirectoryName(_view.FileNameTextBox.Text);
            } catch(Exception ex)
            {
                // Doesn't really matter if the default path they put in is invalid.
            }

            var filter = "";
            foreach (var s in _importers)
            {
                filter += "*." + s + ";";
            }
            filter = filter.Substring(0, filter.Length - 1);

            openFileDialog.Filter = "3D Models (" + filter + ")|" + filter;
            openFileDialog.RestoreDirectory = false;

            if (openFileDialog.ShowDialog() == DialogResult.OK)
            {
                //Get the path of specified file
                _view.FileNameTextBox.Text = openFileDialog.FileName;
                SetRaceOverrideByFileName();
            }

        }
    }
}
