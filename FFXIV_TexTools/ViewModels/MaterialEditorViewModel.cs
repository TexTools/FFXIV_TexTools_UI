using AutoUpdaterDotNET;
using FFXIV_TexTools.Helpers;
using FFXIV_TexTools.Resources;
using FFXIV_TexTools.Views;
using FFXIV_TexTools.Views.Textures;
using HelixToolkit.Wpf.SharpDX;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;
using FFXIV_TexTools.Properties;
using xivModdingFramework.Cache;
using xivModdingFramework.General.Enums;
using xivModdingFramework.Items;
using xivModdingFramework.Items.Categories;
using xivModdingFramework.Items.DataContainers;
using xivModdingFramework.Items.Interfaces;
using xivModdingFramework.Materials.DataContainers;
using xivModdingFramework.Materials.FileTypes;
using xivModdingFramework.Mods;
using xivModdingFramework.SqPack.FileTypes;
using xivModdingFramework.Textures.Enums;
using xivModdingFramework.Variants.DataContainers;
using xivModdingFramework.Variants.FileTypes;
using Constants = xivModdingFramework.Helpers.Constants;

using Index = xivModdingFramework.SqPack.FileTypes.Index;
using static xivModdingFramework.Materials.DataContainers.ShaderHelpers;
using System.Globalization;
using System.Windows.Data;

namespace FFXIV_TexTools.ViewModels
{
    public enum MaterialSpecularMode
    {
        FullColor,      // DXT1 Spec Map
        MultiMap,       // DXT1 Multi Map
        None            // None (Colorset Only)
    };

    public enum MaterialDiffuseMode
    {
        FullColor,      // DXT1 Diffuse Map
        None            // None (Colorset Only)
    }


    class MaterialEditorViewModel
    {
        public MaterialEditorMode _mode;

        
        private MaterialEditorView _view;
        private Mtrl _mtrl;
        private Index _index;
        private Gear _gear;
        private Modding _modding;
        private XivMtrl _material;
        private IItemModel _item;
        private string _newMaterialIdentifier;

        public MaterialEditorViewModel(MaterialEditorView view)
        {
            _view = view;
        }

        public async Task<bool> SetMaterial(XivMtrl material, IItemModel item, MaterialEditorMode mode)
        {
            if (material == null)
            {
                return false;
            }

            _mode = mode;
            _material = material;
            _item = item;

            var gameDirectory = new DirectoryInfo(Properties.Settings.Default.FFXIV_Directory);
            _mtrl = new Mtrl(XivCache.GameInfo.GameDirectory);
            _index = new Index(gameDirectory);
            _modding = new Modding(gameDirectory);
            _gear = new Gear(gameDirectory, GetLanguage());


            // Drop the multi functions down to singles if they only have one Material to edit anyways.
            if(_mode == MaterialEditorMode.EditMulti || _mode == MaterialEditorMode.NewMulti)
            {
                // This isn't an actual perfect check for if there's only one Variant, but doing so
                // would be a bit expensive here, and passing it through EditMulti isn't harmful anyways.
                var sameModelItems = await _item.GetSharedModelItems();
                if(sameModelItems.Count == 1)
                {
                    if (_mode == MaterialEditorMode.EditMulti)
                    {
                        _mode = MaterialEditorMode.EditSingle;
                    }
                }
            }

            // Update to new material name
            switch(_mode)
            {
                case MaterialEditorMode.EditSingle:
                    _view.MaterialPathTextBox.Text = _material.MTRLPath;
                    break;
                case MaterialEditorMode.EditMulti:
                    _view.MaterialPathTextBox.Text = "Editing Multiple Materials: Material ".L() + _material.GetMaterialIdentifier();
                    break;
                case MaterialEditorMode.NewMulti:
                    _view.MaterialPathTextBox.Text = "New Materials".L();
                    break;
                case MaterialEditorMode.NewRace:
                    _view.MaterialPathTextBox.Text = "New Racial Material".L();
                    break;
            }

            var normal = _material.Textures.FirstOrDefault( x => x.Usage == XivTexType.Normal);
            var diffuse = _material.Textures.FirstOrDefault(x => x.Usage == XivTexType.Diffuse);
            var specular = _material.Textures.FirstOrDefault(x => x.Usage == XivTexType.Specular);
            var multi = _material.Textures.FirstOrDefault(x => x.Usage == XivTexType.Mask);
            var reflection = _material.Textures.FirstOrDefault(x => x.Usage == XivTexType.Reflection);

            // Show Settings
            //_view.TransparencyComboBox.SelectedValue = shader.TransparencyEnabled;
            //_view.BackfacesComboBox.SelectedValue = shader.RenderBackfaces;
            //_view.ColorsetComboBox.SelectedValue = _material.ColorSetData.Count > 0;
            _view.ShaderComboBox.SelectedValue = _material.Shader;


            if(_mode == MaterialEditorMode.NewMulti )
            {
                _newMaterialIdentifier = _material.GetMaterialIdentifier();
                _view.MaterialPathTextBox.Text = "New Materials: Material ".L() + _newMaterialIdentifier;
            }

            // Get the mod entry.
            if (_mode == MaterialEditorMode.EditSingle || _mode == MaterialEditorMode.EditMulti)
            {
                var mod = await _modding.TryGetModEntry(_material.MTRLPath);
                if (mod != null && mod.enabled)
                {
                    _view.DisableButton.IsEnabled = true;
                    _view.DisableButton.Visibility = System.Windows.Visibility.Visible;
                }
            }

            return true;
        }

        public XivMtrl GetMaterial()
        {
            return _material;
        }

        /// <summary>
        /// Updates the XivMtrl with the selected changes.
        /// </summary>
        public async Task<XivMtrl> SaveChanges()
        {
            _view.SaveButton.IsEnabled = false;
            _view.CancelButton.IsEnabled = false;
            _view.DisableButton.IsEnabled = false;
            _view.SaveButton.Content = UIStrings.Working_Ellipsis;

            try
            {
                if (_mode == MaterialEditorMode.EditSingle)
                {
                    // Save the existing MTRL.
                    await _mtrl.ImportMtrl(_material, _item, XivStrings.TexTools);
                }
                else if (_mode == MaterialEditorMode.NewMulti || _mode == MaterialEditorMode.EditMulti || _mode == MaterialEditorMode.NewRace)
                {
                    // Ship it to the more complex save function.
                    await SaveMulti();
                }
            } catch (Exception Ex)
            {
                FlexibleMessageBox.Show("An error occurred when saving the Material.".L());
            }
            return _material;
        }


        public async Task SaveMulti()
        {

            var _imc = new Imc(XivCache.GameInfo.GameDirectory);
            var _index = new Index(XivCache.GameInfo.GameDirectory);

            // Get tokenized map info structs.
            // This will let us set them in the new Materials and
            // Detokenize them using the new paths.
            //var mapInfos = _material.GetAllMapInfos(true);

            // Add new Materials for shared model items.    
            var oldMaterialIdentifier = _material.GetMaterialIdentifier();
            var oldMtrlName = Path.GetFileName(_material.MTRLPath);

            // Ordering these by name ensures that we create textures for the new variants in the first
            // item alphabetically, just for consistency's sake.
            var sameModelItems = (await _item.GetSharedModelItems()).OrderBy(x => x.Name, new ItemNameComparer());

            var oldVariantString = "/v" + _material.GetVariant().ToString().PadLeft(4, '0') + '/';
            var modifiedVariants = new List<int>();

            var mtrlReplacementRegex = "_" + oldMaterialIdentifier + ".mtrl";
            var mtrlReplacementRegexResult = "_" + _newMaterialIdentifier + ".mtrl";

            if(_mode == MaterialEditorMode.NewRace)
            {
                mtrlReplacementRegexResult = mtrlReplacementRegex;
            }

            var newMtrlName = oldMtrlName.Replace(mtrlReplacementRegex, mtrlReplacementRegexResult);

            var root = _item.GetRootInfo();

            var imcEntries = new List<XivImc>();
            var materialSets = new HashSet<byte>();
            try
            {
                var imcInfo = await _imc.GetFullImcInfo(_item);
                imcEntries = imcInfo.GetAllEntries(root.Slot, true);
                materialSets = imcEntries.Select(x => x.MaterialSet).ToHashSet();
            } catch
            {
                // Item doesn't use IMC entries, and thus only has a single variant. 
                materialSets.Clear();
                materialSets.Add(0);
            }

            // We need to save our non-existent base material once before we can continue.
            if (_mode == MaterialEditorMode.NewRace)
            {
                await _mtrl.ImportMtrl(_material, _item, XivStrings.TexTools);
            }

            var count = 0;

            var allItems = (await root.ToFullRoot().GetAllItems());

            var matNumToItems = new Dictionary<int, List<IItemModel>>();
            foreach (var i in allItems) {
                if (imcEntries.Count <= i.ModelInfo.ImcSubsetID) continue;

                var matSet = imcEntries[i.ModelInfo.ImcSubsetID].MaterialSet;
                if(!matNumToItems.ContainsKey(matSet))
                {
                    matNumToItems.Add(matSet, new List<IItemModel>());
                }

                var saveItem = i;

                if (typeof(XivCharacter) == i.GetType())
                {
                    var temp = (XivCharacter)((XivCharacter)_item).Clone();
                    temp.Name = saveItem.SecondaryCategory;
                    saveItem = temp;
                }

                matNumToItems[matSet].Add(saveItem);
            }

            var keys = matNumToItems.Keys.ToList();
            foreach(var key in keys)
            {
                var list = matNumToItems[key];
                matNumToItems[key] = list.OrderBy(x => x.Name, new ItemNameComparer()).ToList();
            }

            // Load and modify all the MTRLs.
            foreach (var materialSetId in materialSets)
            {
                var variantPath = _mtrl.GetMtrlFolder(root, materialSetId);
                var oldMaterialPath = variantPath + "/" + oldMtrlName;
                var newMaterialPath = variantPath + "/" + newMtrlName;

                // Don't create materials for set 0.  (SE sets the material ID to 0 when that particular set-slot doesn't actually exist as an item)
                if (materialSetId == 0 && imcEntries.Count > 0) continue;

                // We have clone, just use clone.
                XivMtrl itemXivMtrl = (XivMtrl) _material.Clone();

                // If we're an item that doesn't use IMC variants, make sure we don't accidentally move the material around.
                if (materialSetId != 0)
                {
                    // Shift the MTRL to the new variant folder.
                    itemXivMtrl.MTRLPath = Regex.Replace(itemXivMtrl.MTRLPath, oldVariantString, "/v" + materialSetId.ToString().PadLeft(4, '0') + "/");
                }

                if (_mode == MaterialEditorMode.NewMulti)
                {
                    // Change the MTRL part identifier.
                    itemXivMtrl.MTRLPath = Regex.Replace(itemXivMtrl.MTRLPath, mtrlReplacementRegex, mtrlReplacementRegexResult);
                }

                IItem item;
                try
                {
                    item = matNumToItems[materialSetId].First();
                } catch
                {
                    item = (await XivCache.GetFirstRoot(itemXivMtrl.MTRLPath)).GetFirstItem();
                }

                count++;
                // Write the new Material
                await _mtrl.ImportMtrl(itemXivMtrl, item, XivStrings.TexTools);
                _view.SaveStatusLabel.Content = $"Updated {count._()}/{materialSets.Count._()} Material Sets...".L();
            }

        }

        public async Task DisableMod()
        {
            _view.SaveButton.IsEnabled = false;
            _view.CancelButton.IsEnabled = false;
            _view.DisableButton.IsEnabled = false;
            _view.DisableButton.Content = UIStrings.Working_Ellipsis;
            var files = new HashSet<string>();

            var root = _item.GetRoot();
            if(root == null || !Imc.UsesImc(root))
            {
                // This is the only copy of the material we know how to find.
                files.Add(_material.MTRLPath);
            } else
            {
                var imc = new Imc(XivCache.GameInfo.GameDirectory);
                var info = await imc.GetFullImcInfo(_item);
                var entries = info.GetAllEntries(root.Info.Slot);
                var materialSets = entries.Select(x => x.MaterialSet).ToHashSet();

                var extract = new Regex("(v[0-9]{4})");
                var rep = extract.Match(_material.MTRLPath).Groups[1].Value;

                // Remove the material in all of the referenced material sets.
                foreach(var setId in materialSets)
                {
                    var newPath = _material.MTRLPath.Replace(rep, "v" + setId.ToString().PadLeft(4, '0'));
                    files.Add(newPath);
                }
            }

            try
            {
                foreach (var file in files)
                {
                    var modEntry = await _modding.TryGetModEntry(file);

                    if (modEntry == null)
                    {
                        continue;
                    }

                    // If the file is a custom addition, and not a modification.
                    if (modEntry.IsCustomFile())
                    {
                        await _modding.DeleteMod(file);
                    }
                    else
                    {
                        await _modding.ToggleModStatus(file, false);
                    }
                }
            } catch(Exception ex)
            {
                FlexibleMessageBox.Show("Unable to delete Mod.\n\nError: ".L() + ex.Message, "Mod Delete Error".L(), MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            _view.Close(false);
        }
        private static XivLanguage GetLanguage()
        {
            return XivLanguages.GetXivLanguage(Properties.Settings.Default.Application_Language);
        }
    }
}