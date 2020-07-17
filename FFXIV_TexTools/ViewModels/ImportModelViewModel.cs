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

namespace FFXIV_TexTools.ViewModels
{
    public class ImportModelViewModel
    {
        private const int ExpandedHeight = 640;
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


        public ImportModelViewModel(ImportModelView view, IItemModel item, XivRace race, string submeshId, bool dataOnly, Action onComplete = null)
        {
            _view = view;
            _item = item;
            _race = race;
            _submeshId = submeshId;
            _dataOnly = dataOnly;
            _onComplete = onComplete;

            var gameDirectory = new DirectoryInfo(Settings.Default.FFXIV_Directory);
            var saveDirectory = new DirectoryInfo(Settings.Default.Save_Directory);
            var dataFile = IOUtil.GetDataFileFromPath(_item.GetItemRootFolder());
            _mdl = new Mdl(gameDirectory, dataFile);
            _importers = _mdl.GetAvailableImporters();


            var path = _mdl.GetMdlPath(item, race);
            _internalPath = path;

            var defaultPath = $"{IOUtil.MakeItemSavePath(_item, saveDirectory, _race)}\\3D";
            defaultPath = defaultPath.Replace("/", "\\");
            var modelName = Path.GetFileNameWithoutExtension(_internalPath);


            // Scan to see which file type(s) actually exist.
            bool foundValidFile = false;
            string startingPath = "";
            foreach (var suffix in _importers)
            {
                startingPath = Path.Combine(defaultPath, modelName) + "." + suffix;
                if (File.Exists(defaultPath))
                {
                    foundValidFile = true;
                    break;
                }
            }

            if (!foundValidFile)
            {
                startingPath = Path.Combine(defaultPath, modelName) + ".dae";
            }


            _view.FileNameTextBox.Text = startingPath;

            // Event Handlers
            _view.SelectFileButton.Click += SelectFileButton_Click;
            _view.ImportButton.Click += ImportButton_Click;
            _view.EditButton.Click += EditButton_Click;
            _view.Closing += _view_Closing;

            // Default Settings for specific categories.
            if (item.SecondaryCategory == XivStrings.Face)
            {
                _view.EnableShapeDataButton.IsChecked = true;
            }
            if (item.SecondaryCategory == XivStrings.Hair)
            {
                _view.CloneUV1Button.IsChecked = true;
            }
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
            _showEditor = showEditor;
            _view.Height = ExpandedHeight;
            _view.EnableAll(false);
            var d = new DirectoryInfo(_view.FileNameTextBox.Text);

            // Clear log.
            _view.LogTextBox.Document.Blocks.Clear();
            _view.LogTextBox.AppendText("");

            var options = new ModelModifierOptions();
            options.EnableShapeData = _view.EnableShapeDataButton.IsChecked == true ? true : false;
            options.ForceUVQuadrant = _view.ForceUVsButton.IsChecked == true ? true : false;
            options.ClearUV2 = _view.ClearUV2Button.IsChecked == true ? true : false;
            options.CloneUV2 = _view.CloneUV1Button.IsChecked == true ? true : false;
            options.ClearVAlpha = _view.ClearVAlphaButton.IsChecked == true ? true : false;
            options.ClearVColor = _view.ClearVColorButton.IsChecked == true ? true : false;

            // Asynchronously call ImportModel.
            Task.Run(async () =>
           {
               try
               {
                   if (showEditor)
                   {
                       await _mdl.ImportModel(_item, _race, d.FullName, options, LogMessageReceived, IntermediateStep, XivStrings.TexTools, _submeshId, _dataOnly);
                   }
                   else
                   {
                       await _mdl.ImportModel(_item, _race, d.FullName, options, LogMessageReceived, null, XivStrings.TexTools, _submeshId, _dataOnly);
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

                           WriteToLog("> [ERROR] " + ex.Message, Brushes.DarkRed);
                           FlexibleMessageBox.Show("An error occurred during import:\n" + ex.Message + "\n\nThe import has been cancelled.", "Import Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
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
        private async Task<bool> IntermediateStep(TTModel model)
        {
            var result = false;
            await _view.Dispatcher.BeginInvoke((ThreadStart)delegate ()
            {
                try
                {
                    var editorWindow = new ImportModelEditView(model) { Owner = _view };
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

                _closeTimer = new System.Timers.Timer(CloseDelay);
                _closeTimer.Elapsed += _closeTimer_Elapsed;
                _closeTimer.Start();
                _view.KeyDown += _view_KeyDown;
                WriteToLog("> [INFO] This window will automatically close in 3 seconds... (ESC to cancel)", Brushes.Black);

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
            openFileDialog.InitialDirectory = Path.GetDirectoryName(_view.FileNameTextBox.Text);

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
            }
        }
    }
}
