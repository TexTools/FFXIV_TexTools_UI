using FFXIV_TexTools.Resources;
using FFXIV_TexTools.Views.Textures;
using HelixToolkit.Wpf;
using HelixToolkit.Wpf.SharpDX;
using Newtonsoft.Json;
using SharpDX.D3DCompiler;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Xceed.Wpf.Toolkit.PropertyGrid.Attributes;
using xivModdingFramework.General.Enums;
using xivModdingFramework.Items.Interfaces;
using xivModdingFramework.Materials.DataContainers;
using xivModdingFramework.Materials.FileTypes;
using xivModdingFramework.Textures.DataContainers;
using xivModdingFramework.Textures.Enums;

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
        public bool WriteFile = false;

        private MaterialEditorView _view;
        private Mtrl _mtrl;
        private XivMtrl _material;
        private IItemModel _item;
        public MaterialEditorViewModel(MaterialEditorView view)
        {
            _view = view;
        }

        public void SetMaterial(XivMtrl material, IItemModel item, bool writeFile = true)
        {
            if (material == null)
            {
                return;
            }

            WriteFile = writeFile;

            var gameDirectory = new DirectoryInfo(Properties.Settings.Default.FFXIV_Directory);
            _mtrl = new Mtrl(gameDirectory, item.DataFile, GetLanguage());


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

            _material = material;
            _item = item;

            // Update to new material name
            if (WriteFile)
            {
                _view.MaterialPathLabel.Text = _material.MTRLPath;
            } else
            {
                _view.MaterialPathLabel.Text = "New Material(s)";
            }

            var shader = _material.GetShaderInfo();
            var normal = _material.GetMapInfo(XivTexType.Normal);
            var diffuse = _material.GetMapInfo(XivTexType.Diffuse);
            var specular = _material.GetMapInfo(XivTexType.Specular);
            var multi = _material.GetMapInfo(XivTexType.Multi);
            var reflection = _material.GetMapInfo(XivTexType.Reflection);

            // Show Paths
            _view.NormalTextBox.Text = normal == null ? "" : normal.path;
            _view.SpecularTextBox.Text = specular == null ? "" : specular.path;
            _view.SpecularTextBox.Text = multi == null ? _view.SpecularTextBox.Text : multi.path;
            _view.DiffuseTextBox.Text = diffuse == null ? "" : diffuse.path;
            _view.DiffuseTextBox.Text = reflection == null ? _view.DiffuseTextBox.Text : reflection.path;

            // Add Other option if needed.
            if (shader.Shader == MtrlShader.Other)
            {
                _view.ShaderSource.Add(new KeyValuePair<MtrlShader, string>(MtrlShader.Other, "Other"));
            }

            // Show Settings
            _view.TransparencyComboBox.SelectedValue = shader.TransparencyEnabled;
            _view.ColorsetComboBox.SelectedValue = shader.HasColorset;
            _view.ShaderComboBox.SelectedValue = shader.Shader;
            _view.PresetComboBox.SelectedValue = shader.Preset;



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
            _view.NormalTextBox.Text = SanitizePath(_view.NormalTextBox.Text);
            _view.DiffuseTextBox.Text = SanitizePath(_view.DiffuseTextBox.Text);
            _view.SpecularTextBox.Text = SanitizePath(_view.SpecularTextBox.Text);

            // New Data
            var newShader = new ShaderInfo() { 
                Shader = (MtrlShader) _view.ShaderComboBox.SelectedValue,
                Preset = (MtrlShaderPreset) _view.PresetComboBox.SelectedValue,
                TransparencyEnabled = (bool) _view.TransparencyComboBox.SelectedValue 
            };

            MapInfo newNormal = null;
            MapInfo newDiffuse = null;
            MapInfo newSpecular = null;
            MapInfo newMulti = null;
            MapInfo newReflection = null;

            // Normal
            newNormal = new MapInfo() { Usage = XivTexType.Normal, Format = MtrlTextureDescriptorFormat.UsesColorset, path = _view.NormalTextBox.Text };

            // Specular / Multi
            if (newShader.HasMulti)
            {
                newMulti = new MapInfo() { Usage = XivTexType.Multi, Format = MtrlTextureDescriptorFormat.NoColorset, path = _view.SpecularTextBox.Text };
            }
            else
            {
                newSpecular = new MapInfo() { Usage = XivTexType.Specular, Format = MtrlTextureDescriptorFormat.NoColorset, path = _view.SpecularTextBox.Text };
            }

            // Diffuse / Reflection
            if (newShader.HasDiffuse)
            {
                newDiffuse = new MapInfo() { Usage = XivTexType.Diffuse, Format = MtrlTextureDescriptorFormat.NoColorset, path = _view.DiffuseTextBox.Text };
            }
            else if (newShader.HasReflection)
            { 
                newReflection = new MapInfo() { Usage = XivTexType.Reflection, Format = MtrlTextureDescriptorFormat.NoColorset, path = _view.DiffuseTextBox.Text };
            }

            _material.SetShaderInfo(newShader); // This should be set BEFORE changing the maps over.
            _material.SetMapInfo(XivTexType.Normal, newNormal);
            _material.SetMapInfo(XivTexType.Specular, newSpecular);
            _material.SetMapInfo(XivTexType.Multi, newMulti);
            _material.SetMapInfo(XivTexType.Diffuse, newDiffuse);
            _material.SetMapInfo(XivTexType.Reflection, newReflection);


            if (WriteFile)
            {
                // Write the new MTRLs - ImportMtrl automatically generates any missing textures.
                var newMtrlOffset = await _mtrl.ImportMtrl(_material, _item, XivStrings.TexTools);
            }
            return _material;
        }
        private static XivLanguage GetLanguage()
        {
            return XivLanguages.GetXivLanguage(Properties.Settings.Default.Application_Language);
        }
    }
}
