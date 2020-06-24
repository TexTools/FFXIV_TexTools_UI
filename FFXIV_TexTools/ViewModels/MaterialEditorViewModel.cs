using FFXIV_TexTools.Views.Textures;
using HelixToolkit.Wpf;
using HelixToolkit.Wpf.SharpDX;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
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
        private MaterialEditorView _view;
        private Mtrl _mtrl;
        private XivMtrl _material;
        private IItemModel _item;
        public MaterialEditorViewModel(MaterialEditorView view)
        {
            _view = view;
        }

        public void SetMaterial(XivMtrl material, IItemModel item)
        {
            var gameDirectory = new DirectoryInfo(Properties.Settings.Default.FFXIV_Directory);
            _mtrl = new Mtrl(gameDirectory, item.DataFile, GetLanguage());

            _material = material;
            _item = item;

            _view.MaterialPathLabel.Content = _material.MTRLPath;

            var shader = _material.GetShaderInfo();
            var normal = _material.GetMapInfo(XivTexType.Normal);
            var diffuse = _material.GetMapInfo(XivTexType.Diffuse);
            var specular = _material.GetMapInfo(XivTexType.Specular);
            var multi = _material.GetMapInfo(XivTexType.Multi);

            _view.NormalTextBox.Text = normal.path;

            _view.DiffuseComboBox.SelectedValue = MaterialDiffuseMode.None;
            _view.DiffuseTextBox.Text = "";
            if (diffuse != null)
            {
                _view.DiffuseComboBox.SelectedValue = MaterialDiffuseMode.FullColor;
                _view.DiffuseTextBox.Text = diffuse.path;
            }


            _view.SpecularComboBox.SelectedValue = MaterialSpecularMode.None;
            _view.SpecularTextBox.Text = "";

            if (multi != null)
            {
                _view.SpecularComboBox.SelectedValue = MaterialSpecularMode.MultiMap;
                _view.SpecularTextBox.Text = multi.path;
            }
            else if (specular != null)
            {
                _view.SpecularComboBox.SelectedValue = MaterialSpecularMode.FullColor;
                _view.SpecularTextBox.Text = specular.path;
            }

            _view.TransparencyComboBox.SelectedValue = shader.TransparencyEnabled;
            _view.ShaderComboBox.SelectedValue = shader.Shader;
            _view.NormalComboBox.SelectedValue = normal.Format;


            // Handle tokenizing the paths with placeholders for human readability.
            var path = _material.GetTextureRootDirectoy();
            _view.DiffuseTextBox.Text = _view.DiffuseTextBox.Text.Replace(path, XivMtrl.ItemPathToken);
            _view.SpecularTextBox.Text = _view.SpecularTextBox.Text.Replace(path, XivMtrl.ItemPathToken);
            _view.NormalTextBox.Text = _view.NormalTextBox.Text.Replace(path, XivMtrl.ItemPathToken);

            var commonPath = XivMtrl.GetCommonTextureDirectory();
            _view.DiffuseTextBox.Text = _view.DiffuseTextBox.Text.Replace(commonPath, XivMtrl.CommonPathToken);
            _view.SpecularTextBox.Text = _view.SpecularTextBox.Text.Replace(commonPath, XivMtrl.CommonPathToken);
            _view.NormalTextBox.Text = _view.NormalTextBox.Text.Replace(commonPath, XivMtrl.CommonPathToken);


            var version = _material.GetVersionString();
            if(version != "")
            {
                _view.DiffuseTextBox.Text = _view.DiffuseTextBox.Text.Replace(version, XivMtrl.VersionToken);
                _view.SpecularTextBox.Text = _view.SpecularTextBox.Text.Replace(version, XivMtrl.VersionToken);
                _view.NormalTextBox.Text = _view.NormalTextBox.Text.Replace(version, XivMtrl.VersionToken);
            }

            var normalName = _material.GetDefaultTexureName(XivTexType.Normal, false);
            var multiName = _material.GetDefaultTexureName(XivTexType.Multi, false);
            var specName= _material.GetDefaultTexureName(XivTexType.Specular, false);
            var diffuseName = _material.GetDefaultTexureName(XivTexType.Diffuse, false);


            _view.NormalTextBox.Text = _view.NormalTextBox.Text.Replace(normalName, XivMtrl.TextureNameToken);
            _view.SpecularTextBox.Text = _view.SpecularTextBox.Text.Replace(multiName, XivMtrl.TextureNameToken);
            _view.SpecularTextBox.Text = _view.SpecularTextBox.Text.Replace(specName, XivMtrl.TextureNameToken);
            _view.DiffuseTextBox.Text = _view.DiffuseTextBox.Text.Replace(diffuseName, XivMtrl.TextureNameToken);


        }

        public XivMtrl GetMaterial()
        {
            //return _materialSettings.Material;
            return _material;
        }

        /// <summary>
        /// Updates the XivMtrl with the selected changes.
        /// </summary>
        public async Task<int> SaveChanges()
        {
            // Old Data
            var oldShader = _material.GetShaderInfo();
            var oldNormal = _material.GetMapInfo(XivTexType.Normal);
            var oldDiffuse = _material.GetMapInfo(XivTexType.Diffuse);
            var oldSpecular = _material.GetMapInfo(XivTexType.Specular);
            var oldMulti = _material.GetMapInfo(XivTexType.Multi);


            if(_view.NormalTextBox.Text == "")
            {
                
            }

            // New Data
            var newShader = new ShaderInfo() { Shader = (MtrlShader) _view.ShaderComboBox.SelectedValue, TransparencyEnabled = (bool) _view.TransparencyComboBox.SelectedValue };
            MapInfo newNormal = new MapInfo() { Usage = XivTexType.Normal, Format = (MtrlTextureDescriptorFormat) _view.NormalComboBox.SelectedValue, path = _view.NormalTextBox.Text };
            MapInfo newDiffuse = null;
            MapInfo newSpecular = null;
            MapInfo newMulti = null;


            // Specular
            if((MaterialSpecularMode) _view.SpecularComboBox.SelectedValue == MaterialSpecularMode.FullColor)
            {
                newSpecular = new MapInfo() { Usage = XivTexType.Specular, Format = MtrlTextureDescriptorFormat.WithoutAlpha, path = _view.SpecularTextBox.Text };
            } 
            else if((MaterialSpecularMode)_view.SpecularComboBox.SelectedValue == MaterialSpecularMode.MultiMap)
            {
                newMulti = new MapInfo() { Usage = XivTexType.Multi, Format = MtrlTextureDescriptorFormat.WithoutAlpha, path = _view.SpecularTextBox.Text };
            }

            // Diffuse
            if ((MaterialDiffuseMode)_view.DiffuseComboBox.SelectedValue == MaterialDiffuseMode.FullColor)
            {
                newDiffuse = new MapInfo() { Usage = XivTexType.Diffuse, Format = MtrlTextureDescriptorFormat.WithoutAlpha, path = _view.DiffuseTextBox.Text };
            }

            _material.SetShaderInfo(newShader);
            _material.SetMapInfo(XivTexType.Normal, newNormal);
            _material.SetMapInfo(XivTexType.Specular, newSpecular);
            _material.SetMapInfo(XivTexType.Multi, newMulti);
            _material.SetMapInfo(XivTexType.Diffuse, newDiffuse);


            // Write the new MTRLs - ImportMtrl automatically generates any missing textures.
            var newMtrlOffset = await _mtrl.ImportMtrl(_material, _item, "FilesAddedByTexTools");
            return newMtrlOffset;
        }
        private static XivLanguage GetLanguage()
        {
            return XivLanguages.GetXivLanguage(Properties.Settings.Default.Application_Language);
        }
    }
}
