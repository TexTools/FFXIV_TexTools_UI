using AutoUpdaterDotNET;
using FFXIV_TexTools.Helpers;
using FFXIV_TexTools.Properties;
using FFXIV_TexTools.Resources;
using FFXIV_TexTools.Views.Models;
using HelixToolkit.Wpf;
using Microsoft.Win32;
using SharpDX.D3DCompiler;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Threading;
using xivModdingFramework.General.Enums;
using xivModdingFramework.Helpers;
using xivModdingFramework.Items;
using xivModdingFramework.Items.Interfaces;
using xivModdingFramework.Models.DataContainers;
using xivModdingFramework.Models.FileTypes;

namespace FFXIV_TexTools.ViewModels
{
    public class ImportModelViewModel
    {
        private const int ExpandedHeight = 640;

        private ImportModelView _view;
        private IItemModel _item;
        private XivRace _race;
        private XivMdl _ogMdl;
        private Mdl _mdl;
        private List<string> _importers;

        private bool _showEditor;

        bool enableShapeData;

        public ImportModelViewModel(ImportModelView view, IItemModel item, XivMdl ogMdl)
        {
            _view = view;
            _item = item;
            _ogMdl = ogMdl;

            var gameDirectory = new DirectoryInfo(Settings.Default.FFXIV_Directory);
            var saveDirectory = new DirectoryInfo(Settings.Default.Save_Directory);
            var dataFile = IOUtil.GetDataFileFromPath(_item.GetItemRootFolder());
            var path = _ogMdl.MdlPath.Folder + "/" + _ogMdl.MdlPath.File;

            _race = IOUtil.GetRaceFromPath(path);

            var defaultPath = $"{IOUtil.MakeItemSavePath(_item, saveDirectory, _race)}\\3D";
            defaultPath = defaultPath.Replace("/", "\\");
            var modelName = Path.GetFileNameWithoutExtension(_ogMdl.MdlPath.File);
            defaultPath = Path.Combine(defaultPath, modelName) + ".fbx";

            _view.FileNameTextBox.Text = defaultPath;


            _mdl = new Mdl(gameDirectory, dataFile);
            _importers = _mdl.GetAvailableImporters();

            if(!ogMdl.HasShapeData)
            {
                _view.EnableShapeDataButton.ToolTip = "This model has no shape data to enable.";
                _view.EnableShapeDataButton.IsEnabled = false;
                _view.EnableShapeDataButton.IsChecked = false;
            }



            // Event Handlers
            _view.SelectFileButton.Click += SelectFileButton_Click;
            _view.ImportButton.Click += ImportButton_Click;
            _view.EditButton.Click += EditButton_Click;
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
            _view.LogTextBox.Text = "";

            // Assigned here so they're read-accessible on other threads.
            enableShapeData = _view.EnableShapeDataButton.IsChecked == true ? true : false;

            // Asynchronously call ImportModel.
            Task.Run( async () =>
            {
                try
                {
                    await _mdl.ImportModel(_item, _ogMdl, d, UIStrings.About_TexTools, IntermediateStep, LogMessageReceived);
                    OnImportComplete();
                } catch(Exception ex)
                {
                    // This is kind of a weird construct, but ensures this is called
                    // on the main UI thread.
                    // main thread that has ownership to edit the Enabled values.
                    await _view.Dispatcher.BeginInvoke((ThreadStart)delegate ()
                    {
                        if (ex.Message != "cancel")
                        {
                            FlexibleMessageBox.Show("An error occurred during import:\n" + ex.Message + "\n\nThe import has been cancelled.", "Import Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        }

                        _view.EnableAll(true);
                    });
                }
            });
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
            model.EnableShapeData = enableShapeData;

            // TODO - Handle Options Processing Here, then show Advanced Import Dialog.
            return true;
        }

        /// <summary>
        /// Function called whenever the importer spits out a log line.
        /// </summary>
        /// <param name="isWarning"></param>
        /// <param name="message"></param>
        private void LogMessageReceived(bool isWarning, string message)
        {
            if(message == null || message.Trim() == "") return;
            _view.Dispatcher.BeginInvoke((ThreadStart) delegate()
            {
                _view.LogTextBox.AppendText(message + "\n");
            });
        }

        /// <summary>
        /// This is called when the import is successfully completed.
        /// </summary>
        private void OnImportComplete()
        {
            _view.Dispatcher.BeginInvoke((ThreadStart)delegate ()
            {
                _view.EnableAll(true);
                _view.DialogResult = true;
            });
        }

        private void SelectFileButton_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            var openFileDialog = new System.Windows.Forms.OpenFileDialog();
            openFileDialog.InitialDirectory = Path.GetDirectoryName(_view.FileNameTextBox.Text);

            var filter = "";
            foreach(var s in _importers)
            {
                filter += "*." + s + ";";
            }
            filter = filter.Substring(0, filter.Length - 1);

            openFileDialog.Filter = "3D Models (" + filter + ")|"+filter;
            openFileDialog.RestoreDirectory = false;

            if (openFileDialog.ShowDialog() == DialogResult.OK)
            {
                //Get the path of specified file
                _view.FileNameTextBox.Text = openFileDialog.FileName;
            }
        }
    }
}
