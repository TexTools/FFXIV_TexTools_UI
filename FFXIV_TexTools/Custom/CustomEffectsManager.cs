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
using SharpDX.Direct3D;

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
                    new ShaderPassDescription(DefaultPassNames.PBR)
                    {
                        ShaderList = new[]
                        {
                            DefaultVSShaderDescriptions.VSMeshDefault,
                            DefaultPSShaderDescriptions.PSMeshPBR
                        },
                        BlendStateDescription = DefaultBlendStateDescriptions.BSAlphaBlend,
                        DepthStencilStateDescription = DefaultDepthStencilDescriptions.DSSDepthLessEqual
                    },
                    new ShaderPassDescription(DefaultPassNames.Colors)
                    {
                        ShaderList = new[]
                        {
                            DefaultVSShaderDescriptions.VSMeshDefault,
                            DefaultPSShaderDescriptions.PSMeshVertColor
                        },
                        BlendStateDescription = DefaultBlendStateDescriptions.BSAlphaBlend,
                        DepthStencilStateDescription = DefaultDepthStencilDescriptions.DSSDepthLessEqual
                    },
                    new ShaderPassDescription(DefaultPassNames.Normals)
                    {
                        ShaderList = new[]
                        {
                            DefaultVSShaderDescriptions.VSMeshDefault,
                            DefaultPSShaderDescriptions.PSMeshVertNormal
                        },
                        BlendStateDescription = DefaultBlendStateDescriptions.BSAlphaBlend,
                        DepthStencilStateDescription = DefaultDepthStencilDescriptions.DSSDepthLessEqual
                    },
                    new ShaderPassDescription(DefaultPassNames.Positions)
                    {
                        ShaderList = new[]
                        {
                            DefaultVSShaderDescriptions.VSMeshDefault,
                            DefaultPSShaderDescriptions.PSMeshVertPosition
                        },
                        BlendStateDescription = DefaultBlendStateDescriptions.BSSourceAlways,
                        DepthStencilStateDescription = DefaultDepthStencilDescriptions.DSSDepthLess
                    },
                    new ShaderPassDescription(DefaultPassNames.Diffuse)
                    {
                        ShaderList = new[]
                        {
                            DefaultVSShaderDescriptions.VSMeshDefault,
                            DefaultPSShaderDescriptions.PSMeshDiffuseMap
                        },
                        BlendStateDescription = DefaultBlendStateDescriptions.BSAlphaBlend,
                        DepthStencilStateDescription = DefaultDepthStencilDescriptions.DSSDepthLessEqual
                    },
                    new ShaderPassDescription(DefaultPassNames.ColorStripe1D)
                    {
                        ShaderList = new[]
                        {
                            DefaultVSShaderDescriptions.VSMeshDefault,
                            DefaultPSShaderDescriptions.PSMeshColorStripe
                        },
                        BlendStateDescription = DefaultBlendStateDescriptions.BSAlphaBlend,
                        DepthStencilStateDescription = DefaultDepthStencilDescriptions.DSSDepthLessEqual
                    },
                    new ShaderPassDescription(DefaultPassNames.ViewCube)
                    {
                        ShaderList = new[]
                        {
                            DefaultVSShaderDescriptions.VSMeshDefault,
                            DefaultPSShaderDescriptions.PSMeshViewCube
                        },
                        BlendStateDescription = DefaultBlendStateDescriptions.BSSourceAlways,
                        DepthStencilStateDescription = DefaultDepthStencilDescriptions.DSSDepthLess
                    },
                    new ShaderPassDescription(DefaultPassNames.NormalVector)
                    {
                        ShaderList = new[]
                        {
                            DefaultVSShaderDescriptions.VSMeshDefault,
                            DefaultGSShaderDescriptions.GSMeshNormalVector,
                            DefaultPSShaderDescriptions.PSLineColor
                        },
                        BlendStateDescription = DefaultBlendStateDescriptions.BSSourceAlways,
                        DepthStencilStateDescription = DefaultDepthStencilDescriptions.DSSDepthLess,
                        Topology = PrimitiveTopology.PointList
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
                    new ShaderPassDescription(DefaultPassNames.PBROITPass)
                    {
                        ShaderList = new[]
                        {
                            DefaultVSShaderDescriptions.VSMeshDefault,
                            DefaultPSShaderDescriptions.PSMeshPBROIT
                        },
                        BlendStateDescription = DefaultBlendStateDescriptions.BSOITBlend,
                        DepthStencilStateDescription = DefaultDepthStencilDescriptions.DSSLessNoWrite
                    },
                    new ShaderPassDescription(DefaultPassNames.DiffuseOIT)
                    {
                        ShaderList = new[]
                        {
                            DefaultVSShaderDescriptions.VSMeshDefault,
                            DefaultPSShaderDescriptions.PSMeshDiffuseMapOIT
                        },
                        BlendStateDescription = DefaultBlendStateDescriptions.BSOITBlend,
                        DepthStencilStateDescription = DefaultDepthStencilDescriptions.DSSLessNoWrite
                    },
                    new ShaderPassDescription(DefaultPassNames.PreComputeMeshBoneSkinned)
                    {
                        ShaderList = new[]
                        {
                            DefaultVSShaderDescriptions.VSMeshBoneSkinnedBasic,
                            DefaultGSShaderDescriptions.GSMeshBoneSkinnedOut
                        },
                        BlendStateDescription = DefaultBlendStateDescriptions.NoBlend,
                        DepthStencilStateDescription = DefaultDepthStencilDescriptions.DSSNoDepthNoStencil,
                        Topology = PrimitiveTopology.PointList,
                        InputLayoutDescription = new InputLayoutDescription(DefaultVSShaderByteCodes.VSMeshBoneSkinningBasic, DefaultInputLayout.VSInputBoneSkinnedBasic),
                    },
                    new ShaderPassDescription(DefaultPassNames.DepthPrepass)
                    {
                        ShaderList = new[]
                        {
                            DefaultVSShaderDescriptions.VSMeshDepth,
                            DefaultPSShaderDescriptions.PSDepthStencilOnly
                        },
                        BlendStateDescription = DefaultBlendStateDescriptions.NoBlend,
                        DepthStencilStateDescription = DefaultDepthStencilDescriptions.DSSDepthLess
                    },
                    new ShaderPassDescription(DefaultPassNames.MeshSSAOPass)
                    {
                        ShaderList = new[]
                        {
                            DefaultVSShaderDescriptions.VSMeshSSAO,
                            DefaultPSShaderDescriptions.PSSSAOP1
                        },
                        BlendStateDescription = DefaultBlendStateDescriptions.BSSourceAlways,
                        DepthStencilStateDescription = DefaultDepthStencilDescriptions.DSSDepthLessEqual
                    },
                    new ShaderPassDescription(DefaultPassNames.MeshTriTessellation)
                    {
                        ShaderList = new[]
                        {
                            DefaultVSShaderDescriptions.VSMeshTessellation,
                            DefaultHullShaderDescriptions.HSMeshTessellation,
                            DefaultDomainShaderDescriptions.DSMeshTessellation,
                            DefaultPSShaderDescriptions.PSMeshBlinnPhong
                        },
                        BlendStateDescription = DefaultBlendStateDescriptions.BSAlphaBlend,
                        DepthStencilStateDescription = DefaultDepthStencilDescriptions.DSSDepthLessEqual,
                        Topology = PrimitiveTopology.PatchListWith3ControlPoints
                    },
                    new ShaderPassDescription(DefaultPassNames.MeshTriTessellationOIT)
                    {
                        ShaderList = new[]
                        {
                            DefaultVSShaderDescriptions.VSMeshTessellation,
                            DefaultHullShaderDescriptions.HSMeshTessellation,
                            DefaultDomainShaderDescriptions.DSMeshTessellation,
                            DefaultPSShaderDescriptions.PSMeshBlinnPhongOIT
                        },
                        BlendStateDescription = DefaultBlendStateDescriptions.BSOITBlend,
                        DepthStencilStateDescription = DefaultDepthStencilDescriptions.DSSLessNoWrite,
                        Topology = PrimitiveTopology.PatchListWith3ControlPoints
                    },
                    new ShaderPassDescription(DefaultPassNames.MeshPBRTriTessellation)
                    {
                        ShaderList = new[]
                        {
                            DefaultVSShaderDescriptions.VSMeshTessellation,
                            DefaultHullShaderDescriptions.HSMeshTessellation,
                            DefaultDomainShaderDescriptions.DSMeshTessellation,
                            DefaultPSShaderDescriptions.PSMeshPBR
                        },
                        BlendStateDescription = DefaultBlendStateDescriptions.BSAlphaBlend,
                        DepthStencilStateDescription = DefaultDepthStencilDescriptions.DSSDepthLessEqual,
                        Topology = PrimitiveTopology.PatchListWith3ControlPoints
                    },
                    new ShaderPassDescription(DefaultPassNames.MeshPBRTriTessellationOIT)
                    {
                        ShaderList = new[]
                        {
                            DefaultVSShaderDescriptions.VSMeshTessellation,
                            DefaultHullShaderDescriptions.HSMeshTessellation,
                            DefaultDomainShaderDescriptions.DSMeshTessellation,
                            DefaultPSShaderDescriptions.PSMeshPBROIT
                        },
                        BlendStateDescription = DefaultBlendStateDescriptions.BSOITBlend,
                        DepthStencilStateDescription = DefaultDepthStencilDescriptions.DSSLessNoWrite,
                        Topology = PrimitiveTopology.PatchListWith3ControlPoints
                    },
                    new ShaderPassDescription(DefaultPassNames.MeshOutline)
                    {
                        ShaderList = new[]
                        {
                            DefaultVSShaderDescriptions.VSMeshDefault,
                            DefaultPSShaderDescriptions.PSMeshXRay
                        },
                        BlendStateDescription = DefaultBlendStateDescriptions.BSOverlayBlending,
                        DepthStencilStateDescription = DefaultDepthStencilDescriptions.DSSLessEqualNoWrite
                    },
                    new ShaderPassDescription(DefaultPassNames.ShadowPass)
                    {
                        ShaderList = new[]
                        {
                            DefaultVSShaderDescriptions.VSMeshShadow,
                            DefaultPSShaderDescriptions.PSShadow
                        },
                        BlendStateDescription = DefaultBlendStateDescriptions.NoBlend,
                        DepthStencilStateDescription = DefaultDepthStencilDescriptions.DSSDepthLess
                    },
                    new ShaderPassDescription(DefaultPassNames.Wireframe)
                    {
                        ShaderList = new[]
                        {
                            DefaultVSShaderDescriptions.VSMeshWireframe,
                            DefaultPSShaderDescriptions.PSMeshWireframe
                        },
                        BlendStateDescription = DefaultBlendStateDescriptions.BSSourceAlways,
                        DepthStencilStateDescription = DefaultDepthStencilDescriptions.DSSLessEqualNoWrite,
                        Topology = PrimitiveTopology.TriangleList
                    },
                    new ShaderPassDescription(DefaultPassNames.WireframeOITPass)
                    {
                        ShaderList = new[]
                        {
                            DefaultVSShaderDescriptions.VSMeshWireframe,
                            DefaultPSShaderDescriptions.PSMeshWireframeOIT
                        },
                        BlendStateDescription = DefaultBlendStateDescriptions.BSOITBlend,
                        DepthStencilStateDescription = DefaultDepthStencilDescriptions.DSSLessEqualNoWrite,
                        Topology = PrimitiveTopology.TriangleList
                    },
                    new ShaderPassDescription(DefaultPassNames.EffectOutlineP1)
                    {
                        ShaderList = new[]
                        {
                            DefaultVSShaderDescriptions.VSMeshWireframe,
                            DefaultPSShaderDescriptions.PSMeshOutlineQuadStencil
                        },
                        BlendStateDescription = DefaultBlendStateDescriptions.BSSourceAlways,
                        DepthStencilStateDescription = DefaultDepthStencilDescriptions.DSSMeshOutlineP1,
                        StencilRef = 1
                    },
                    new ShaderPassDescription(DefaultPassNames.EffectMeshXRayP1)
                    {
                        ShaderList = new[]
                        {
                            DefaultVSShaderDescriptions.VSMeshWireframe,
                            DefaultPSShaderDescriptions.PSDepthStencilOnly
                        },
                        BlendStateDescription = DefaultBlendStateDescriptions.NoBlend,
                        DepthStencilStateDescription = DefaultDepthStencilDescriptions.DSSEffectMeshXRayP1,
                    },
                    new ShaderPassDescription(DefaultPassNames.EffectMeshXRayP2)
                    {
                        ShaderList = new[]
                        {
                            DefaultVSShaderDescriptions.VSMeshDefault,
                            DefaultPSShaderDescriptions.PSEffectMeshXRay
                        },
                        BlendStateDescription = DefaultBlendStateDescriptions.BSOverlayBlending,
                        DepthStencilStateDescription = DefaultDepthStencilDescriptions.DSSEffectMeshXRayP2,
                        StencilRef = 1
                    },
                    new ShaderPassDescription(DefaultPassNames.EffectMeshXRayGridP1)
                    {
                        ShaderList = new[]
                        {
                            DefaultVSShaderDescriptions.VSMeshWireframe,
                            DefaultPSShaderDescriptions.PSDepthStencilOnly
                        },
                        BlendStateDescription = DefaultBlendStateDescriptions.NoBlend,
                        DepthStencilStateDescription = DefaultDepthStencilDescriptions.DSSEffectMeshXRayGridP1,
                        StencilRef = 1
                    },
                    new ShaderPassDescription(DefaultPassNames.EffectMeshXRayGridP2)
                    {
                        ShaderList = new[]
                        {
                            DefaultVSShaderDescriptions.VSMeshWireframe,
                            DefaultPSShaderDescriptions.PSDepthStencilOnly
                        },
                        BlendStateDescription = DefaultBlendStateDescriptions.NoBlend,
                        DepthStencilStateDescription = DefaultDepthStencilDescriptions.DSSEffectMeshXRayGridP2,
                        StencilRef = 1
                    },
                    new ShaderPassDescription(DefaultPassNames.EffectMeshXRayGridP3)
                    {
                        ShaderList = new[]
                        {
                            DefaultVSShaderDescriptions.VSMeshDefault,
                            DefaultPSShaderDescriptions.PSEffectXRayGrid
                        },
                        BlendStateDescription = DefaultBlendStateDescriptions.BSAlphaBlend,
                        DepthStencilStateDescription = DefaultDepthStencilDescriptions.DSSEffectMeshXRayGridP3,
                        StencilRef = 1
                    },
                    new ShaderPassDescription(DefaultPassNames.EffectMeshDiffuseXRayGridP3)
                    {
                        ShaderList = new[]
                        {
                            DefaultVSShaderDescriptions.VSMeshDefault,
                            DefaultPSShaderDescriptions.PSEffectDiffuseXRayGrid
                        },
                        BlendStateDescription = DefaultBlendStateDescriptions.BSAlphaBlend,
                        DepthStencilStateDescription = DefaultDepthStencilDescriptions.DSSEffectMeshXRayGridP3,
                        StencilRef = 1
                    },
                }
            };
            AddTechnique(customMesh);
        }

        public static class CustomPSShaderDescription
        {
            public static ShaderDescription PSCustomMesh = new ShaderDescription(nameof(PSCustomMesh), ShaderStage.Pixel,
                new ShaderReflector(), ShaderHelper.LoadShaderCode($"{AppDomain.CurrentDomain.BaseDirectory}\\Resources\\Shaders\\psCustomMeshBlinnPhong.cso"));
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