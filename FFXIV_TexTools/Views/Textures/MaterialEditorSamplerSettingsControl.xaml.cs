using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using xivModdingFramework.Materials.DataContainers;

namespace FFXIV_TexTools.Views.Textures
{
    /// <summary>
    /// Interaction logic for MaterialEditorSamplerSettingsControl.xaml
    /// </summary>
    public partial class MaterialEditorSamplerSettingsControl : UserControl
    {
        private XivMtrl _material;

        private List<SamplerSettingsEntryControl> SamplerControls;
    public void SetMatrial(XivMtrl mtrl)
        {
            if (SamplerControls != null)
            {
                // Remove any existing entries.
                foreach (var g in SamplerControls)
                {
                    PrimaryStack.Children.Remove(g);
                }

                SamplerControls.Clear();
            }

            _material = mtrl;

            foreach(var sampler in _material.TextureSamplerSettingsList)
            {
                MakeSamplerEntry(sampler);
            }

            // Check for any textures that have no sampler.
            foreach(var tex in _material.TexturePathList)
            {
                if(_material.TextureSamplerSettingsList.Any(x => x.TexturePath == tex))
                {
                    continue;
                }

                var placeholder = new TextureSamplerSettings();
                placeholder.SamplerId = MtrlSamplerId.None;
                placeholder.Flags = 0;
                placeholder.SamplerSettings = 0;
                placeholder.TexturePath = tex;
                MakeSamplerEntry(placeholder);
            }

        }
        public MaterialEditorSamplerSettingsControl()
        {
            InitializeComponent();
            SamplerControls = new List<SamplerSettingsEntryControl>();
        }

        private void MakeSamplerEntry(TextureSamplerSettings sampler)
        {
            var samplerControl = new SamplerSettingsEntryControl(sampler);
            SamplerControls.Add(samplerControl);
            PrimaryStack.Children.Add(samplerControl);

            samplerControl.RequestDelete += SamplerControl_RequestDelete;
        }

        private void SamplerControl_RequestDelete(object sender, EventArgs e)
        {
            var control = (SamplerSettingsEntryControl)sender;
            var sampler = control.Sampler;

            PrimaryStack.Children.Remove(control);
            SamplerControls.Remove(control);
            _material.TextureSamplerSettingsList.Remove(sampler);
        }

        public void FinalizeTexturePaths()
        {
            _material.TexturePathList.Clear();
            _material.TextureDxSettingsList.Clear();

            _material.TexturePathList = SamplerControls.Select(x => x.Sampler.TexturePath).ToList();

            foreach(var elemn in _material.TexturePathList)
            {
                // These are the texture directX information bits.
                // We force everything to DX11 mode on saving anyways, so flattening these to 0 is fine.
                _material.TextureDxSettingsList.Add(0);
            }

            // Remove empty and null samplers.
            _material.TextureSamplerSettingsList = _material.TextureSamplerSettingsList.Where(x => !String.IsNullOrWhiteSpace(x.TexturePath) || x.SamplerId == MtrlSamplerId.None).ToList();
        }
    }
}
