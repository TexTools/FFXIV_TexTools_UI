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
using xivModdingFramework.Mods;

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

        public async Task<bool> Save(bool simpleSave = false, ModTransaction tx = null)
        {
            if (_metadata == null) return false;

            if(tx == null)
            {
                tx = MainWindow.UserTransaction;
            }

            var success = false;
            try
            {
                if (!simpleSave)
                {
                    await MainWindow.GetMainWindow().LockUi("Updating Metadata".L());
                }

                await ItemMetadata.SaveMetadata(_metadata, XivStrings.TexTools, tx);

                if (!simpleSave)
                {
                    // Fill in missing racial models or material sets.
                    await _metadata.FillMissingFiles(XivStrings.TexTools, tx);
                }


                if (!simpleSave)
                {
                    await _view.SetRoot(_metadata.Root);
                }
                success = true;
            } catch(Exception Ex)
            {
                Helpers.FlexibleMessageBox.Show("An Error occured while saving the Metadata: \n".L() + Ex.Message, "Metadata Error".L(), System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Error, System.Windows.Forms.MessageBoxDefaultButton.Button1);
            }
            finally
            {
                if (!simpleSave)
                {
                    await MainWindow.GetMainWindow().UnlockUi();
                }
            }


            return success;
        }
    }
}
