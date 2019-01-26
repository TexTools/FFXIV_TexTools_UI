// FFXIV TexTools
// Copyright © 2019 Rafael Gonzalez - All Rights Reserved
// 
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
// 
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
// 
// You should have received a copy of the GNU General Public License
// along with this program.  If not, see <http://www.gnu.org/licenses/>.

using HelixToolkit.Wpf.SharpDX;
using HelixToolkit.Wpf.SharpDX.Shaders;
using System;
using System.IO;

namespace FFXIV_TexTools.Custom
{
    public class CustomEffectsManager : DefaultEffectsManager
    {
        public static class CustomShaderNames
        {
            public static readonly string CustomShader = "CustomShader";
        }

        /// <summary>
        /// Custom Effects Manager for 3D Model
        /// </summary>
        public CustomEffectsManager()
        {
            var customMesh = new TechniqueDescription(CustomShaderNames.CustomShader)
            {
                InputLayoutDescription = new InputLayoutDescription(DefaultVSShaderByteCodes.VSMeshDefault, DefaultInputLayout.VSInput),
                PassDescriptions = new[]
                {
                    new ShaderPassDescription(DefaultPassNames.Default)
                    {
                        ShaderList = new[]
                        {
                            DefaultVSShaderDescriptions.VSMeshDefault,
                            CustomPSShaderDescription.PSCustomMesh
                        },
                        BlendStateDescription = DefaultBlendStateDescriptions.BSAlphaBlend,
                        DepthStencilStateDescription = DefaultDepthStencilDescriptions.DSSDepthLess
                    },
                    new ShaderPassDescription(DefaultPassNames.OITPass)
                    {
                        ShaderList = new[]
                        {
                            DefaultVSShaderDescriptions.VSMeshDefault,
                            DefaultPSShaderDescriptions.PSMeshBlinnPhongOIT
                        },
                        BlendStateDescription = DefaultBlendStateDescriptions.BSOITBlend,
                        DepthStencilStateDescription = DefaultDepthStencilDescriptions.DSSLessNoWrite
                    },
                }
            };
            AddTechnique(customMesh);
        }

        public static class CustomPSShaderDescription
        {
            public static ShaderDescription PSCustomMesh = new ShaderDescription(nameof(PSCustomMesh), ShaderStage.Pixel,
                new ShaderReflector(), ShaderHelper.LoadShaderCode(@"Resources\Shaders\psCustomMeshBlinnPhong.cso"));
        }

        public static class ShaderHelper
        {
            public static byte[] LoadShaderCode(string path)
            {
                if (File.Exists(path))
                {
                    return File.ReadAllBytes(path);
                }
                else
                {
                    throw new ArgumentException($"Shader File not found: {path}");
                }
            }
        }
    }

}