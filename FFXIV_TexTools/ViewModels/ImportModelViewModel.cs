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
using System.Diagnostics;
using System.Linq.Expressions;

namespace FFXIV_TexTools.ViewModels
{
    public class ImportModelViewModel : INotifyPropertyChanged
    {
        private const int ExpandedHeight = 680;
        private const double CloseDelay = 3000f;

        private ImportModelView _view;
        private List<string> _importers;
        private string _internalPath;
        private System.Timers.Timer _closeTimer;
        private bool _anyWarnings = false;

        private bool _simpleMode = false;


        private bool _success = false;
        private Action<ModelImportResult> _onComplete;

        ModelImportResult _result;

        public event PropertyChangedEventHandler PropertyChanged;

        private bool _ComplexOptionsEnabled;
        public bool ComplexOptionsEnabled
        {
            get => _ComplexOptionsEnabled;
            set
            {
                _ComplexOptionsEnabled = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ComplexOptionsEnabled)));
            }
        }
        private string _FinishText;
        public string FinishText
        {
            get => _FinishText;
            set
            {
                _FinishText = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(FinishText)));
            }
        }


        private void SetupRaces()
        {
            var races = Eqp.PlayableRaces;

            _view.RaceComboBox.SelectedValuePath = "Key";
            _view.RaceComboBox.DisplayMemberPath = "Value";

            _view.RaceComboBox.IsEnabled = false;
            var race = IOUtil.GetRaceFromPath(_internalPath);

            if (race == XivRace.All_Races)
            {
                _view.OverrideRaceButton.IsEnabled = false;
                var kv = new KeyValuePair<XivRace, string>(XivRace.All_Races, "--");
                _view.RaceComboBox.Items.Add(kv);
            } else
            {
                foreach (var r in races)
                {
                    var kv = new KeyValuePair<XivRace, string>(r, XivRaces.GetDisplayName(r));
                    _view.RaceComboBox.Items.Add(kv);
                }
            }

            _view.RaceComboBox.SelectedValue = race;

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
            var mdlRace = IOUtil.GetRaceFromPath(_internalPath);

            if (match.Success)
            {
                // Swap the race to a non-NPC version if it's an NPC race.
                var raceCode = match.Groups[1].Value.Substring(0, 2) + "01";
                var race = XivRaces.GetXivRace(raceCode);

                if(race != mdlRace && race != XivRace.All_Races && mdlRace != XivRace.All_Races)
                {
                    // We have a valid race.
                    _view.OverrideRaceButton.IsChecked = true;
                    _view.RaceComboBox.SelectedValue = race;
                }
            }
        }


        public ImportModelViewModel(ImportModelView view, string internalPath, IItem referenceItem, Action<ModelImportResult> onComplete = null, string startingFilePath = null, bool simpleMode = false)
        {
            _view = view;
            _onComplete = onComplete;
            _internalPath = internalPath;
            _simpleMode = simpleMode;

            ComplexOptionsEnabled = !simpleMode;

            FinishText = (simpleMode ? "Add Model" : "Load Model");

            if (referenceItem != null && typeof(XivCharacter) == referenceItem.GetType())
            {
                // Fix up naming scheme for character items to match user expectation.
                var clone = (XivCharacter)((XivCharacter)referenceItem).Clone();
                clone.Name = clone.SecondaryCategory;
                referenceItem = clone;
            }

            var gameDirectory = new DirectoryInfo(Settings.Default.FFXIV_Directory);
            var saveDirectory = new DirectoryInfo(Settings.Default.Save_Directory);
            var dataFile = IOUtil.GetDataFileFromPath(_internalPath);
            _importers = Mdl.GetAvailableImporters();

            SetupRaces();

            var race = IOUtil.GetRaceFromPath(_internalPath);

            var defaultPath = "";
            if (referenceItem != null)
            {
                defaultPath = $"{IOUtil.MakeItemSavePath((IItem)referenceItem, saveDirectory, race)}\\3D".Replace("/", "\\");
            }
            var modelName = Path.GetFileNameWithoutExtension(_internalPath);


            // Scan to see which file type(s) actually exist.
            bool foundValidFile = false;

            // FBX is default, so check that first.
            if (startingFilePath == null)
            {
                startingFilePath = Path.Combine(defaultPath, modelName) + ".fbx";
                if (File.Exists(startingFilePath))
                {
                    foundValidFile = true;
                }

                if (!foundValidFile)
                {
                    foreach (var suffix in _importers)
                    {
                        startingFilePath = Path.Combine(defaultPath, modelName) + "." + suffix;
                        if (File.Exists(startingFilePath))
                        {
                            foundValidFile = true;
                            break;
                        }
                    }
                }
                if (!foundValidFile)
                {
                    startingFilePath = "";
                }
            }
            _view.FileNameTextBox.Text = startingFilePath;



            // Event Handlers
            _view.SelectFileButton.Click += SelectFileButton_Click;
            _view.ImportButton.Click += ImportButton_Click;
            _view.EditButton.Click += EditButton_Click;
            _view.Closing += _view_Closing;
            _view.OverrideRaceButton.Checked += OverrideRaceButton_Checked;
            _view.OverrideRaceButton.Unchecked += OverrideRaceButton_Unchecked;

            // Default Settings for specific categories, event handlers are added to allow users to opt out of these defaults
            if (referenceItem != null)
            {
                if (referenceItem.SecondaryCategory == XivStrings.Face && ComplexOptionsEnabled)
                {
                    _view.UseOriginalShapeDataButton.IsChecked = Settings.Default.UseOriginalShapeDataForFace;
                    _view.UseOriginalShapeDataButton.Click += UseOriginalShapeDataButton_Clicked;
                }
                if (referenceItem.SecondaryCategory == XivStrings.Hair)
                {
                    _view.CloneUV1Button.IsChecked = Settings.Default.CloneUV1toUV2ForHair;
                    _view.CloneUV1Button.Click += CloneUV1Button_Clicked;
                }

                var iType = referenceItem.GetPrimaryItemType();
                if (iType == xivModdingFramework.Items.Enums.XivItemType.equipment 
                    || iType == xivModdingFramework.Items.Enums.XivItemType.accessory 
                    || iType == xivModdingFramework.Items.Enums.XivItemType.weapon)
                {
                    _view.ForceUVsButton.IsChecked = Settings.Default.ForceUV1QuadrantForGear;
                    _view.ForceUVsButton.Click += ForceUVsButton_Clicked;
                }
            }
        }

        private void UseOriginalShapeDataButton_Clicked(object sender, RoutedEventArgs e)
        {
            Settings.Default.UseOriginalShapeDataForFace = _view.UseOriginalShapeDataButton.IsChecked == true;
            Settings.Default.Save();
        }

        private void CloneUV1Button_Clicked(object sender, RoutedEventArgs e)
        {
            Settings.Default.CloneUV1toUV2ForHair = _view.CloneUV1Button.IsChecked == true;
            Settings.Default.Save();
        }

        private void ForceUVsButton_Clicked(object sender, RoutedEventArgs e)
        {
            Settings.Default.ForceUV1QuadrantForGear = _view.ForceUVsButton.IsChecked == true;
            Settings.Default.Save();
        }


        private void OverrideRaceButton_Checked(object sender, RoutedEventArgs e)
        {
            _view.RaceComboBox.IsEnabled = true;
        }
        private void OverrideRaceButton_Unchecked(object sender, RoutedEventArgs e)
        {
            var race = IOUtil.GetRaceFromPath(_internalPath);

            _view.RaceComboBox.IsEnabled = false;
            _view.RaceComboBox.SelectedValue = race;
        }


        private void _view_Closing(object sender, CancelEventArgs e)
        {
            // Make sure to notify if we never did for some reason.
            if(_result == null)
            {
                if (_onComplete != null)
                {
                    var result = new ModelImportResult()
                    {
                        Path = _internalPath,
                        Success = _success,
                        Data = null,
                        Model = null,
                        
                    };
                    _onComplete(result);
                }
            }
        }

        private async void ImportButton_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            try 
            { 
                await DoImport(false);
            }
            catch (Exception ex)
            {
                Trace.WriteLine(ex);
            }
        }

        private async void EditButton_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            try
            {
                await DoImport(true);
            }
            catch(Exception ex)
            {
                Trace.WriteLine(ex);
            }
        }

        private async Task DoImport(bool showEditor)
        {

            var race = IOUtil.GetRaceFromPath(_internalPath);
            string externalPath = null;
            _anyWarnings = false;
            if (_view.FileNameTextBox.Text != null && _view.FileNameTextBox.Text.Trim() != "") 
            {
                try
                {
                    var d = new DirectoryInfo(_view.FileNameTextBox.Text);
                    externalPath = d.FullName;
                }
                catch(Exception ex)
                {
                    // Invalid directory.
                    FlexibleMessageBox.Show("The given file path is invalid.".L(), "Import Error".L(), MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
            }

            _view.Height = ExpandedHeight;
            _view.EnableAll(false);

            // Clear log.
            _view.LogTextBox.Document.Blocks.Clear();
            _view.LogTextBox.AppendText("");

            var options = new ModelImportOptions();
            options.UseOriginalShapeData = _view.UseOriginalShapeDataButton.IsChecked == true ? true : false;
            options.ForceUVQuadrant = _view.ForceUVsButton.IsChecked == true ? true : false;
            options.ClearUV2 = _view.ClearUV2Button.IsChecked == true ? true : false;
            options.CloneUV2 = _view.CloneUV1Button.IsChecked == true ? true : false;
            options.ClearVAlpha = _view.ClearVAlphaButton.IsChecked == true ? true : false;
            options.ClearVColor = _view.ClearVColorButton.IsChecked == true ? true : false;
            options.AutoScale = _view.AutoScaleButton.IsChecked == true ? true : false;

            options.SourceApplication = XivStrings.TexTools;

            var selectedRace = XivRace.All_Races;
            if (_view.RaceComboBox.SelectedValue != null) {
                selectedRace = (XivRace)_view.RaceComboBox.SelectedValue;
            }

            if(selectedRace != XivRace.All_Races && selectedRace != race)
            {
                options.SourceRace = selectedRace;
            }

            options.LoggingFunction = LogMessageReceived;
            

           // Asynchronously call ImportModel.
           await Task.Run(async () =>
           {
               try
               {
                   if (_simpleMode)
                   {
                       var model = await Mdl.LoadExternalModel(externalPath, options, true);
                       await OnImportComplete(null, model);
                       return;
                   }


                   if (showEditor)
                   {
                       options.IntermediaryFunction = IntermediateStep;
                   }

                   byte[] data = await Mdl.FileToUncompressedMdl(externalPath, _internalPath, options, MainWindow.UserTransaction);

                    await OnImportComplete(data);
               }
               catch (Exception ex)
               {
                    // This is kind of a weird construct, but ensures this is called
                    // on the main UI thread.
                    // main thread that has ownership to edit the Enabled values.
                    await await _view.Dispatcher.InvokeAsync(async () =>
                    {
                       if (ex.Message != "cancel")
                       {
                           _anyWarnings = true;
                           WriteToLog("> [ERROR] " + ex.Message, Brushes.DarkRed);
                           WriteToLog("> Previous log messages may include more information.", Brushes.DarkRed);
                           FlexibleMessageBox.Show($"An error occurred during import:\n{ex.Message._()}\n\nThe import has been cancelled.\nPlease see the text log for more information.".L(), "Import Error".L(), MessageBoxButtons.OK, MessageBoxIcon.Error);
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
            await _view.Dispatcher.InvokeAsync(() =>
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
            try
            {
                if (message == null || message.Trim() == "") return;

                _view.Dispatcher.InvokeAsync(() =>
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
            } catch(Exception ex)
            {
                Trace.WriteLine(ex);
            }
        }

        /// <summary>
        /// This is called when the import is successfully completed.
        /// </summary>
        private async Task OnImportComplete(byte[] data, TTModel model = null)
        {
            await await _view.Dispatcher.InvokeAsync(async () =>
            {
                WriteToLog("> [SUCCESS] Model Imported Successfully.", Brushes.DarkGreen);
                _view.SetData(data);
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
                    var result = new ModelImportResult()
                    {
                        Path = _internalPath,
                        Success = _success,
                        Data = data,
                        Model = model,
                    };
                    _result = result;
                    _onComplete(result);
                }
            });
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

        private async void _closeTimer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            try
            {
                await _view.Dispatcher.InvokeAsync(() =>
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
                        if (_view.IsActive)
                        {
                            _view.Close();
                        }
                    }
                    catch (Exception ex)
                    {
                        //No-Op.  If this fails /bc window is already closed it doesn't matter.
                    }
                });
            }
            catch(Exception ex)
            {
                Trace.WriteLine(ex);
            }
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

            openFileDialog.Filter = $"3D Models|{filter._()}";
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
