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
        private char _newMaterialIdentifier;
        private XivRace _overrideDestinationRace;

        public MaterialEditorViewModel(MaterialEditorView view)
        {
            _view = view;
            _view.RawValuesButton.Click += RawValuesButton_Click;
        }

        private void RawValuesButton_Click(object sender, RoutedEventArgs e)
        {
            if(_material == null)
            {
                return; 
            }

            var values = _material.ShaderConstants.Select( x=> { 
                var ret = x.ConstantId.ToString().L() + " { "; 

                foreach(var val in x.Values)
                {
                    ret += val.ToString("0.000") + ", ";
                }

                ret = ret.Substring(0, ret.Length - 2);
                ret += " }";

                return ret;
            }).ToList();

            var wind = new AffectedFilesView(values, "Raw Shader Parameters") { Owner = _view };
            wind.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            wind.Show();
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
                    } else
                    {
                        _mode = MaterialEditorMode.NewSingle;
                    }
                }
            }

            /*
            // Debug code for finding unknown Shader Parameters.
            var unknowns = new List<ShaderParameterStruct>();
            foreach(var sp in material.ShaderParameterList)
            {
                if (!Enum.IsDefined(typeof(MtrlShaderParameterId), sp.ParameterID))
                {
                    unknowns.Add(sp);
                }
            }
            if(unknowns.Count > 0)
            {
                // Debug line
                var json = JsonConvert.SerializeObject(unknowns.ToArray());
            }
            */


            // Update to new material name
            switch(_mode)
            {
                case MaterialEditorMode.EditSingle:
                    _view.MaterialPathLabel.Text = _material.MTRLPath;
                    break;
                case MaterialEditorMode.EditMulti:
                    _view.MaterialPathLabel.Text = "Editing Multiple Materials: Material ".L() + _material.GetMaterialIdentifier();
                    break;
                case MaterialEditorMode.NewSingle:
                    _view.MaterialPathLabel.Text = "New Material".L();
                    break;
                case MaterialEditorMode.NewMulti:
                    _view.MaterialPathLabel.Text = "New Materials".L();
                    break;
                case MaterialEditorMode.NewRace:
                    _view.MaterialPathLabel.Text = "New Racial Material".L();
                    break;
            }

            var shader = _material.GetShaderInfo();
            var normal = _material.GetMapInfo(XivTexType.Normal);
            var diffuse = _material.GetMapInfo(XivTexType.Diffuse);
            var specular = _material.GetMapInfo(XivTexType.Specular);
            var multi = _material.GetMapInfo(XivTexType.Multi);
            var reflection = _material.GetMapInfo(XivTexType.Reflection);

            // Show Paths
            _view.NormalTextBox.Text = normal == null ? "" : normal.Path;
            _view.SpecularTextBox.Text = specular == null ? "" : specular.Path;
            _view.SpecularTextBox.Text = multi == null ? _view.SpecularTextBox.Text : multi.Path;
            _view.DiffuseTextBox.Text = diffuse == null ? "" : diffuse.Path;
            _view.DiffuseTextBox.Text = reflection == null ? _view.DiffuseTextBox.Text : reflection.Path;

            // Add Other option if needed.
            if (shader.Shader == MtrlShader.Other)
            {
                _view.ShaderSource.Add(new KeyValuePair<MtrlShader, string>(MtrlShader.Other, "Other".L()));
            }

            // Show Settings
            _view.TransparencyComboBox.SelectedValue = shader.TransparencyEnabled;
            _view.BackfacesComboBox.SelectedValue = shader.RenderBackfaces;
            _view.ColorsetComboBox.SelectedValue = shader.HasColorset;
            _view.ShaderComboBox.SelectedValue = shader.Shader;
            _view.PresetComboBox.SelectedValue = shader.Preset;


            if(_mode == MaterialEditorMode.NewMulti )
            {
                // Bump up the material identifier letter.
                _newMaterialIdentifier = await GetNewMaterialIdentifier();
                _view.MaterialPathLabel.Text = "New Materials: Material ".L() + _newMaterialIdentifier;
            } else if(_mode == MaterialEditorMode.NewSingle)
            {
                _newMaterialIdentifier = await GetNewMaterialIdentifier();
                _view.MaterialPathLabel.Text = "New Material: Material ".L() + _newMaterialIdentifier;
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
            //return _materialSettings.Material;
            return _material;
        }

        /// <summary>
        /// Sanitizes a path string
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        private string SanitizePath(string path)
        {
            path = path.ToLower();
            path = Regex.Replace(path, "[^a-z0-9-_/\\.{}]", "");
            return path;
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
            _view.NormalTextBox.Text = SanitizePath(_view.NormalTextBox.Text);
            _view.DiffuseTextBox.Text = SanitizePath(_view.DiffuseTextBox.Text);
            _view.SpecularTextBox.Text = SanitizePath(_view.SpecularTextBox.Text);

            // New Data
            var newShader = new ShaderInfo() { 
                Shader = (MtrlShader) _view.ShaderComboBox.SelectedValue,
                Preset = (MtrlShaderPreset) _view.PresetComboBox.SelectedValue,
                TransparencyEnabled = (bool) _view.TransparencyComboBox.SelectedValue,
                RenderBackfaces = (bool) _view.BackfacesComboBox.SelectedValue
            };

            MapInfo newNormal = null;
            MapInfo newDiffuse = null;
            MapInfo newSpecular = null;
            MapInfo newMulti = null;
            MapInfo newReflection = null;

            // Don't know what defines the normal's format between the two types.
            // But setting it to the UseColorset value all the time causes extremely rare crashes
            // in the Try On menu sometimes.  Possibly GFX card/Driver related?
            var oldNormal = _material.GetMapInfo(XivTexType.Normal);

            // Normal
            newNormal = new MapInfo() { Usage = XivTexType.Normal, Format = oldNormal.Format, Path = _view.NormalTextBox.Text };

            // Specular / Multi
            if (newShader.HasMulti)
            {
                newMulti = new MapInfo() { Usage = XivTexType.Multi, Format = MtrlTextureSamplerFormatPresets.NoColorset, Path = _view.SpecularTextBox.Text };
            }
            else
            {
                newSpecular = new MapInfo() { Usage = XivTexType.Specular, Format = MtrlTextureSamplerFormatPresets.NoColorset, Path = _view.SpecularTextBox.Text };
            }

            // Diffuse / Reflection
            if (newShader.HasDiffuse)
            {
                newDiffuse = new MapInfo() { Usage = XivTexType.Diffuse, Format = MtrlTextureSamplerFormatPresets.NoColorset, Path = _view.DiffuseTextBox.Text };
            }
            else if (newShader.HasReflection)
            { 
                newReflection = new MapInfo() { Usage = XivTexType.Reflection, Format = MtrlTextureSamplerFormatPresets.NoColorset, Path = _view.DiffuseTextBox.Text };
            }

            if(_mode == MaterialEditorMode.NewSingle)
            {
                // This needs to be updated BEFORE setting texture paths.
                _material.MTRLPath = Regex.Replace(_material.MTRLPath, "_([a-z0-9])\\.mtrl", "_" + _newMaterialIdentifier + ".mtrl");
            }

            _material.SetShaderInfo(newShader); // This should be set BEFORE changing the maps over.
            _material.SetMapInfo(XivTexType.Normal, newNormal);
            _material.SetMapInfo(XivTexType.Specular, newSpecular);
            _material.SetMapInfo(XivTexType.Multi, newMulti);
            _material.SetMapInfo(XivTexType.Diffuse, newDiffuse);
            _material.SetMapInfo(XivTexType.Reflection, newReflection);


            try
            {
                if (_mode == MaterialEditorMode.NewSingle || _mode == MaterialEditorMode.EditSingle)
                {
                    // Save the existing MTRL.
                    await _mtrl.ImportMtrl(_material, _item, XivStrings.TexTools);
                }
                else if (_mode == MaterialEditorMode.NewMulti || _mode == MaterialEditorMode.EditMulti || _mode == MaterialEditorMode.NewRace)
                {
                    // Ship it to the more complex save function.
                    await SaveMulti();

                    // Change this after calling SaveMulti.  Updated so that the external classes looking for the material after
                    // can find the right type identifier and such.
                    if (_mode != MaterialEditorMode.NewRace)
                    {
                        _material.MTRLPath = Regex.Replace(_material.MTRLPath, "_([a-z0-9])\\.mtrl", "_" + _newMaterialIdentifier + ".mtrl");
                    }
                }
            } catch (Exception Ex)
            {
                FlexibleMessageBox.Show("An error occurred when saving the Material.".L());
            }
            return _material;
        }

        public async Task<char> GetNewMaterialIdentifier()
        {

            // Get new Material Identifier
            var alphabet = Constants.Alphabet;
            List<string> partList = new List<string>();

            var materialIdentifier = _material.GetMaterialIdentifier();

            var newIdentifier = '\0';
            var rex = new Regex("_([a-z0-9])\\.mtrl");
            if (!rex.IsMatch(_material.MTRLPath))
            {
                return 'a';
            }
            for (var i = 1; i < alphabet.Length; i++)
            {
                var identifier = alphabet[i];
                var newPath = Regex.Replace(_material.MTRLPath, "_([a-z0-9])\\.mtrl", "_" + identifier + ".mtrl");
                var exists = await _index.FileExists(newPath);
                if(!exists)
                {
                    return identifier;
                }
            }

            // No empty material names left.
            // Note - This can be fixed.  Materials don't need to be named a-z, but realisitcally is anyone going to have more than 26 materials?
            if (newIdentifier == '\0')
            {
                if (materialIdentifier == '\0')
                {
                    newIdentifier = 'a';
                }
                else
                {
                    throw new NotSupportedException("Maximum Material Limit Reached.".L());
                }
            }
            return newIdentifier;

        }

        public async Task SaveMulti()
        {

            var _imc = new Imc(XivCache.GameInfo.GameDirectory);
            var _index = new Index(XivCache.GameInfo.GameDirectory);

            // Get tokenized map info structs.
            // This will let us set them in the new Materials and
            // Detokenize them using the new paths.
            var mapInfos = _material.GetAllMapInfos(true);


            // Shader info likewise will be pumped into each new material.
            var shaderInfo = _material.GetShaderInfo();

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

                var dxVersion = 11;
                XivMtrl itemXivMtrl;

                // Get mtrl path
                if(await _index.FileExists(oldMaterialPath))
                {
                    // Use the Material from this variant as a base?
                    itemXivMtrl = await _mtrl.GetMtrlData(_item, oldMaterialPath, dxVersion);
                } else
                {
                    itemXivMtrl = await _mtrl.GetMtrlData(_item, _material.MTRLPath, dxVersion);
                }

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

                // Load the Shader Settings
                itemXivMtrl.SetShaderInfo(shaderInfo, true);

                // Loop our tokenized map infos and pump them back in
                // using the new modified material to detokenize them.
                foreach (var info in mapInfos)
                {
                    itemXivMtrl.SetMapInfo(info.Usage, (MapInfo)info.Clone());
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
