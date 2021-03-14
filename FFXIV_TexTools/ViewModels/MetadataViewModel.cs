using FFXIV_TexTools.Resources;
using FFXIV_TexTools.Views.Metadata;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FFXIV_TexTools.Properties;
using xivModdingFramework.Cache;
using xivModdingFramework.General.Enums;
using xivModdingFramework.Helpers;
using xivModdingFramework.Items.DataContainers;
using xivModdingFramework.Items.Interfaces;
using xivModdingFramework.Models.FileTypes;
using xivModdingFramework.Mods.FileTypes;
using xivModdingFramework.SqPack.FileTypes;

namespace FFXIV_TexTools.ViewModels
{

    public class MetadataViewModel
    {
        private MetadataView _view;
        private ItemMetadata _metadata;
        private ItemMetadata _original;
        public MetadataViewModel(MetadataView view)
        {
            _view = view;
        }


        /// <summary>
        /// Sets the given dependency root for display.
        /// Automatically resolves to the first item of the root if none were given.
        /// 
        /// Returns false if the root or metadata is invalid.
        /// </summary>
        /// <param name="root"></param>
        /// <returns></returns>
        public async Task<bool> SetRoot(XivDependencyRoot root, int startingVariant = 0)
        {
            _metadata = await ItemMetadata.GetMetadata(root);
            _original = await ItemMetadata.GetMetadata(root);
            if (_metadata == null || !_metadata.AnyMetadata) {
                _view.SaveButton.IsEnabled = false;
                _view.NexSlotButton.IsEnabled = false;
                _view.PreviousSlotButton.IsEnabled = false;
                return false;
            }

            _view.SaveButton.IsEnabled = true;

            if (root.Info.Slot == null)
            {
                _view.NexSlotButton.IsEnabled = false;
                _view.PreviousSlotButton.IsEnabled = false;
            }
            else
            {
                _view.NexSlotButton.IsEnabled = true;
                _view.PreviousSlotButton.IsEnabled = true;
            }

            if (_metadata.ImcEntries.Count > 0)
            {
                _view.ImcView.Visibility = System.Windows.Visibility.Visible;
                await _view.ImcView.SetMetadata(_metadata, startingVariant);
            } else
            {
                _view.ImcView.Visibility = System.Windows.Visibility.Collapsed;
            }

            if(_metadata.EqpEntry != null)
            {
                _view.EqpView.Visibility = System.Windows.Visibility.Visible;
                await _view.EqpView.SetMetadata(_metadata);
            }
            else
            {
                _view.EqpView.Visibility = System.Windows.Visibility.Collapsed;
            }

            if (_metadata.EqdpEntries.Count > 0)
            {
                _view.EqdpView.Visibility = System.Windows.Visibility.Visible;
                await _view.EqdpView.SetMetadata(_metadata);
            }
            else
            {
                _view.EqdpView.Visibility = System.Windows.Visibility.Collapsed;
            }

            if(_metadata.EstEntries.Count > 0)
            {
                _view.SkeletonView.Visibility = System.Windows.Visibility.Visible;
                await _view.SkeletonView.SetMetadata(_metadata);
            } else
            {
                _view.SkeletonView.Visibility = System.Windows.Visibility.Collapsed;
            }

            if(_metadata.GmpEntry != null)
            {
                _view.VisorView.Visibility = System.Windows.Visibility.Visible;
                await _view.VisorView.SetMetadata(_metadata);
            } else
            {
                _view.VisorView.Visibility = System.Windows.Visibility.Collapsed;

            }

            return (_metadata != null);
        }

        public async Task<bool> Save()
        {
            if (_metadata == null) return false;
            var success = false;
            try
            {
                await MainWindow.GetMainWindow().LockUi("Updating Metadata");

                await ItemMetadata.SaveMetadata(_metadata, XivStrings.TexTools);

                var _mdl = new Mdl(XivCache.GameInfo.GameDirectory, IOUtil.GetDataFileFromPath(_metadata.Root.Info.GetRootFile()));

                foreach (var kv in _metadata.EqdpEntries)
                {
                    if (kv.Value.bit1 == false) continue;
                    if (_original.EqdpEntries[kv.Key].bit1 == true) continue;

                    // Here we have a new race, we need to create a model for it.
                    await _mdl.AddRacialModel(_metadata.Root.Info.PrimaryId, _metadata.Root.Info.Slot, kv.Key, XivStrings.TexTools);
                }

                if(_metadata.ImcEntries.Count > 0)
                {
                    var _dat = new Dat(XivCache.GameInfo.GameDirectory);
                    var originalMaterialSetMax = _original.ImcEntries.Select(x => x.MaterialSet).Max();
                    var newMaterialSetMax = _metadata.ImcEntries.Select(x => x.MaterialSet).Max();

                    if(newMaterialSetMax > originalMaterialSetMax)
                    {
                        // We have new materials to add.

                        // First find the base files to copy. (Just always copy from set 1 for simplicity)
                        var copySource = await _metadata.Root.GetMaterialFiles(1);
                        var item = _metadata.Root.GetFirstItem();

                        for(int i = originalMaterialSetMax +1; i <= newMaterialSetMax; i++)
                        {
                            foreach(var material in copySource)
                            {
                                var dest = material.Replace("v0001", "v" + i.ToString().PadLeft(4, '0'));

                                await _dat.CopyFile(material, dest, XivStrings.TexTools, false, item);
                            }
                        }
                    }
                }

                success = true;
            } catch(Exception Ex)
            {
                Helpers.FlexibleMessageBox.Show("An Error occured while saving the Metadata: \n" + Ex.Message, "Metadata Error", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Error, System.Windows.Forms.MessageBoxDefaultButton.Button1);
            }
            finally
            {
                await MainWindow.GetMainWindow().UnlockUi();
            }

            if (success)
            {
                var mw = MainWindow.GetMainWindow();
                mw.ReloadItem();
            }

            return success;
        }
    }
}
