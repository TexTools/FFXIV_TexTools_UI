using FFXIV_TexTools.Controls;
using FFXIV_TexTools.Helpers;
using FFXIV_TexTools.Views.Controls;
using HelixToolkit.Wpf.SharpDX;
using MahApps.Metro.Controls;
using SharpDX;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using xivModdingFramework.Cache;
using xivModdingFramework.General;
using xivModdingFramework.Materials.DataContainers;
using xivModdingFramework.Materials.FileTypes;
using xivModdingFramework.Textures;
using xivModdingFramework.Textures.DataContainers;
using xivModdingFramework.Textures.FileTypes;
using Vector3D = System.Windows.Media.Media3D.Vector3D;
using WinColor = System.Windows.Media.Color;
using Point3D = System.Windows.Media.Media3D.Point3D;
using SharpDX.Direct3D11;

namespace FFXIV_TexTools.ViewModels
{
    public class ColorsetEditorViewModel : BaseViewPortViewModel, INotifyPropertyChanged, IDisposable
    {
        private static XivTex TileTextureNormal;
        private static XivTex TileTextureOrb;

        // SharpDX 3D Model Viewing Stuff, which is really all this VM is used for.
        private Viewport3DX _viewport;

        public ObservableElement3DCollection Models { get; } = new ObservableElement3DCollection();

        private XivMtrl _mtrl;
        private int RowId;

        private StainingTemplateFile DyeTemplateFile;

        private List<Half[]> RowData;

        private bool DawnTrail
        {
            get
            {
                if (_mtrl == null) return true;

                if(_mtrl.ColorSetData.Count >= 1024)
                {
                    return true;
                }
                return false;
            }
        }

        private bool LegacyShader
        {
            get
            {
                if(_mtrl == null) return false;  

                if(DawnTrail)
                {
                    return _mtrl.ShaderPack == ShaderHelpers.EShaderPack.CharacterLegacy;
                }
                else
                {
                    return true;
                }
            }
        }

        public ColorsetEditorViewModel(Viewport3DX viewport) : base()
        {
            _viewport = viewport;
            Camera.UpDirection = new System.Windows.Media.Media3D.Vector3D(0, 1, 0);
            Camera.LookAt(new Point3D(0, 0, 0), new Vector3D(-4, 0, 0), 0);
        }


        public async Task SetMaterial(XivMtrl mtrl, StainingTemplateFile dyeFile) {
            _mtrl = mtrl;
            DyeTemplateFile = dyeFile;
            _viewport.BackgroundColor = System.Windows.Media.Colors.Gray;
            _viewport.Background = Brushes.Gray;



            if (TileTextureNormal == null)
            {
                try
                {
                    TileTextureNormal = await Tex.GetXivTex("chara/common/texture/tile_norm_array.tex");
                    // This is not the correct usage, but works for the moment.
                    TileTextureOrb = await Tex.GetXivTex("chara/common/texture/tile_orb_array.tex");

                    // Sphere Array
                    // chara/common/texture/sphere_d_array.tex 
                }
                catch
                {
                }
            }


        }

        public async Task SetColorsetRow(int row, int columnCount, int dyeId = -1)
        {
            if (_mtrl == null) return;

            try
            {
                RowId = row;

                var offset = RowId * columnCount * 4;
                RowData = new List<Half[]>(columnCount);
                for (int i = 0; i < columnCount; i++)
                {
                    var arr = new Half[4];
                    RowData.Add(arr);
                    for (int z = 0; z < 4; z++)
                    {
                        arr[z] = _mtrl.ColorSetData[offset];
                        offset++;
                    }
                }

                var mg3 = MakeCube();

                var lmMaterial = new PhongMaterial()
                {
                    AmbientColor = SharpDX.Color.Gray,
                };
                float[] diffuseColor = new float[3];
                float[] specularColor = new float[3];


                var dMax = Math.Max(1.0f, Math.Max(RowData[0][0], Math.Max(RowData[0][1], RowData[0][2])));
                var sMax = Math.Max(1.0f, Math.Max(RowData[0][0], Math.Max(RowData[0][1], RowData[0][2])));
                var eMax = Math.Max(1.0f, Math.Max(RowData[0][0], Math.Max(RowData[0][1], RowData[0][2])));

                diffuseColor[0] = RowData[0][0] / dMax;
                diffuseColor[1] = RowData[0][1] / dMax;
                diffuseColor[2] = RowData[0][2] / dMax;

                specularColor[0] = RowData[1][0] / sMax;
                specularColor[1] = RowData[1][1] / sMax;
                specularColor[2] = RowData[1][2] / sMax;


                if (RowData[2][0] != 0 || RowData[2][1] != 0 || RowData[2][2] != 0)
                {
                    lmMaterial.EmissiveColor = new SharpDX.Color(
                    ColorsetFileControl.ColorHalfToByte(RowData[2][0] / eMax),
                    ColorsetFileControl.ColorHalfToByte(RowData[2][1] / eMax),
                    ColorsetFileControl.ColorHalfToByte(RowData[2][2] / eMax));
                }

                float glossVal = RowData[0][3];
                float specularPower = RowData[1][3];
                float metallicVal = 0;
                if (!LegacyShader)
                {
                    glossVal = 32 * Math.Max(Math.Min(1, (1 - RowData[4][0])), 0);
                    metallicVal = Math.Max(Math.Min(1, (1 - RowData[4][2])), 0) ;
                    specularPower = 1.0f;
                }



                if (dyeId >= 0 && dyeId < 128 && _mtrl.ColorSetDyeData.Length > 0)
                {
                    var templateId = STM.GetTemplateKeyFromMaterialData(_mtrl, RowId);
                    var template = DyeTemplateFile.GetTemplate(templateId);


                    uint data;
                    if (_mtrl.ColorSetDyeData.Length > 32)
                    {
                        var dyeRowSize = 4;
                        data = BitConverter.ToUInt32(_mtrl.ColorSetDyeData, dyeRowSize * RowId);
                    }
                    else
                    {
                        var dyeRowSize = 2;
                        data = BitConverter.ToUInt16(_mtrl.ColorSetDyeData, dyeRowSize * RowId);
                    }

                    if(template != null && templateId != 0)
                    {

                        bool useDiffuse = (data & 0x01) > 0;
                        bool useSpecular = (data & 0x02) > 0;
                        bool useEmissive = (data & 0x04) > 0;

                        bool useSpecPower = (data & 0x08) > 0;
                        bool useGloss = (data & 0x10) > 0;

                        bool useMetallic = (data & 0x10) != 0;
                        bool useRoughness = (data & 0x20) != 0;

                        var diffuse = template.GetDiffuseData(dyeId);
                        var spec = template.GetSpecularData(dyeId);
                        var emissive = template.GetEmissiveData(dyeId);

                        if (useDiffuse && diffuse != null)
                        {
                            var max = Math.Max(1.0f, Math.Max(diffuse[0], Math.Max(diffuse[1], diffuse[2])));
                            diffuseColor[0] = diffuse[0] / max;
                            diffuseColor[1] = diffuse[1] / max;
                            diffuseColor[2] = diffuse[2] / max;

                        }

                        if (useSpecular && spec != null)
                        {
                            var max = Math.Max(1.0f, Math.Max(spec[0], Math.Max(spec[1], spec[2])));
                            specularColor[0] = diffuse[0] / max;
                            specularColor[1] = diffuse[1] / max;
                            specularColor[2] = diffuse[2] / max;
                        }

                        if (useEmissive && emissive != null)
                        {
                            var max = Math.Max(1.0f, Math.Max(emissive[0], Math.Max(emissive[1], emissive[2])));

                            lmMaterial.EmissiveColor = new SharpDX.Color(
                            ColorsetFileControl.ColorHalfToByte(emissive[0] / max),
                            ColorsetFileControl.ColorHalfToByte(emissive[1] / max),
                            ColorsetFileControl.ColorHalfToByte(emissive[2] / max));
                        }


                        if (LegacyShader)
                        {
                            var gloss = template.GetGlossData(dyeId);
                            var specPower = template.GetSpecularPowerData(dyeId);
                            if (useGloss && gloss != null)
                            {
                                glossVal = gloss[0];
                            }

                            if (useSpecPower && specPower != null)
                            {
                                specularPower = specPower[0];
                            }
                        }
                        else
                        {
                            var metallicDye = template.GetData(5, dyeId);
                            var roughDye = template.GetData(6, dyeId);
                            if (useMetallic && metallicDye != null)
                            {
                                metallicVal = metallicDye[0];
                            } 
                            if (useRoughness && roughDye != null)
                            {
                                glossVal = 32 * Math.Max(Math.Min(1, (1 - roughDye[0])), 0);
                            }
                        }
                                                
                    }
                }


                if (specularPower == 0)
                {
                    glossVal = 1;
                }

                // This is some arbitrary math to make the gloss more or less reflected in the visual.
                lmMaterial.SpecularShininess = (glossVal * glossVal) + 15;
                lmMaterial.SpecularColor = lmMaterial.SpecularColor * specularPower;



                if (TileTextureNormal != null)
                {
                    var layer = (int)Math.Floor(RowData[6][1] * 64);
                    if (layer > 63 || layer < 0) layer = 0;
                    var normData = await TileTextureNormal.GetRawPixels(layer);
                    lmMaterial.NormalMap = MakeTextureModel(normData);

                    var orbData = await TileTextureOrb.GetRawPixels();
                    var size = TileTextureOrb.Width * TileTextureOrb.Height * 4;
                    var specularMask = new byte[size];
                    var diffuseMask = new byte[size];
                    var roughMask = new byte[size];

                    await TextureHelpers.ModifyPixels((offset) =>
                    {
                        var specPix = ColorsetFileControl.ColorByteToHalf(orbData[offset + 0]);
                        var roughPix = ColorsetFileControl.ColorByteToHalf(orbData[offset + 1]);
                        var diffPix = ColorsetFileControl.ColorByteToHalf(orbData[offset + 2]);

                        specPix *= (1 - roughPix);

                        Color4 diff = new Color4(
                            diffPix * diffuseColor[0],
                            diffPix * diffuseColor[1],
                            diffPix * diffuseColor[2],
                            1.0f);

                        Color4 spec = new Color4(
                            specPix * specularColor[0],
                            specPix * specularColor[1],
                            specPix * specularColor[2],
                            1.0f); 

                        // As metalness rises, the diffuse/specular colors merge.
                        diff = Color4.Lerp(diff, diff * spec, metallicVal);
                        spec = Color4.Lerp(spec, diff * spec, metallicVal);


                        diffuseMask[offset + 0] = ColorsetFileControl.ColorHalfToByte(diff[0]);
                        diffuseMask[offset + 1] = ColorsetFileControl.ColorHalfToByte(diff[1]);
                        diffuseMask[offset + 2] = ColorsetFileControl.ColorHalfToByte(diff[2]);
                        diffuseMask[offset + 3] = 255;

                        specularMask[offset + 0] = ColorsetFileControl.ColorHalfToByte(spec[0]);
                        specularMask[offset + 1] = ColorsetFileControl.ColorHalfToByte(spec[1]);
                        specularMask[offset + 2] = ColorsetFileControl.ColorHalfToByte(spec[2]);
                        specularMask[offset + 3] = 255;

                    }, TileTextureOrb.Width, TileTextureOrb.Height);

                    lmMaterial.DiffuseAlphaMap = MakeTextureModel(diffuseMask);
                    lmMaterial.SpecularColorMap = MakeTextureModel(specularMask);

                    var sampler = HelixToolkit.SharpDX.Core.Shaders.DefaultSamplers.LinearSamplerWrapAni1;
                    sampler.AddressU = TextureAddressMode.Wrap;
                    sampler.AddressV = TextureAddressMode.Wrap;
                    lmMaterial.DiffuseMapSampler = sampler;
                }

                MeshGeometryModel3D mgm3 = new MeshGeometryModel3D()
                {
                    Geometry = mg3,
                    Material = lmMaterial
                };


                foreach (var m in Models)
                {
                    m.Dispose();
                }
                Models.Clear();
                Models.Add(mgm3);
            } catch(Exception ex)
            {
                FlexibleMessageBox.Show("Unable to update 3D Viewport.\n\nError: ".L() + ex.Message, "Viewport Error".L(), System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Warning);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float Lerp(float a, float b, float t)
        {
            return a + (b - a) * t;
        }

        /// <summary>
        /// Handles converting the byte array to color
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        private TextureModel MakeTextureModel(byte[] data)
        {
            var tileX = (float)RowData[7][0];
            var tileY = (float)RowData[7][3];
            var tileSkewX = (float)RowData[7][1];
            var tileSkewY = (float)RowData[7][2];

            var ogW = 32;
            var ogH = 32;
            var w = 256;
            var h = 256;
            Color4[] colors = new Color4[w * h];

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    var nPixel = (y * w) + x;
                    //U = Dot Product ([u,v], [Red, Green])
                    float u = (float)x / (float)w;
                    float v = ((float)y) / (float)h;

                    // This is now the new U and V coordinates we want to map to.
                    var newU = SharpDX.Vector2.Dot(new SharpDX.Vector2(u, v), new SharpDX.Vector2(tileX, tileSkewX)) % 1.0f;
                    var newV = SharpDX.Vector2.Dot(new SharpDX.Vector2(u, v), new SharpDX.Vector2(tileSkewY, tileY)) % 1.0f;

                    var xPx = (int)(newU * ogW);
                    var yPx = (int)(newV * ogH);

                    xPx = xPx < 0 ? ogW - xPx : xPx;
                    yPx = yPx < 0 ? ogH - yPx : yPx;

                    xPx = xPx >= ogW ? 0 : xPx;
                    yPx = yPx >= ogH ? 0 : yPx;


                    var ogPixel = (yPx * ogW) + xPx;
                    var ogDataOffset = ogPixel * 4;

                    
                    colors[nPixel] = new Color4()
                    {
                        Red = data[ogDataOffset] / 255.0f,
                        Green = data[ogDataOffset + 1] / 255.0f,
                        Blue = data[ogDataOffset + 2] / 255.0f,
                        Alpha = 1.0f,
                    };
                }
            }

            var tm = new TextureModel(colors, w, h);
            return tm;
        }

        private enum PlaneAxis
        {
            X, Y, Z
        }
        private void AddQuad(MeshBuilder plane, PlaneAxis axis, float offset)
        {
            var uvOffset = plane.TextureCoordinates.Count;

            if (axis == PlaneAxis.X)
            {
                if (offset > 0)
                {
                    plane.AddQuad(
                        new SharpDX.Vector3(offset, -offset, offset),
                        new SharpDX.Vector3(offset, offset, offset),
                        new SharpDX.Vector3(offset, offset, -offset),
                        new SharpDX.Vector3(offset, -offset, -offset)
                    );
                }
                else
                {
                    plane.AddQuad(
                        new SharpDX.Vector3(offset, offset, offset),
                        new SharpDX.Vector3(offset, -offset, offset),
                        new SharpDX.Vector3(offset, -offset, -offset),
                        new SharpDX.Vector3(offset, offset, -offset)
                    );
                }
            }
            else if (axis == PlaneAxis.Y)
            {
                if (offset > 0)
                {
                    plane.AddQuad(
                        new SharpDX.Vector3(-offset, offset, offset),
                        new SharpDX.Vector3(offset, offset, offset),
                        new SharpDX.Vector3(offset, offset, -offset),
                        new SharpDX.Vector3(-offset, offset, -offset)
                    );
                }
                else
                {
                    plane.AddQuad(
                        new SharpDX.Vector3(offset, offset, offset),
                        new SharpDX.Vector3(-offset, offset, offset),
                        new SharpDX.Vector3(-offset, offset, -offset),
                        new SharpDX.Vector3(offset, offset, -offset)
                    );
                }
            }
            else
            {
                if (offset > 0)
                {
                    plane.AddQuad(
                        new SharpDX.Vector3(-offset, offset, offset),
                        new SharpDX.Vector3(offset, offset, offset),
                        new SharpDX.Vector3(offset, -offset, offset),
                        new SharpDX.Vector3(-offset, -offset, offset)
                    );
                }
                else
                {
                    plane.AddQuad(
                        new SharpDX.Vector3(-offset, offset, offset),
                        new SharpDX.Vector3(offset, offset, offset),
                        new SharpDX.Vector3(offset, -offset, offset),
                        new SharpDX.Vector3(-offset, -offset, offset)
                    );
                }
            }


            plane.TextureCoordinates[uvOffset + 0] = new SharpDX.Vector2(0, 1);
            plane.TextureCoordinates[uvOffset + 1] = new SharpDX.Vector2(1, 1);
            plane.TextureCoordinates[uvOffset + 2] = new SharpDX.Vector2(1, 0);
            plane.TextureCoordinates[uvOffset + 3] = new SharpDX.Vector2(0, 0);

            plane.Normals[uvOffset + 0] = new SharpDX.Vector3(0, 0, 1);
            plane.Normals[uvOffset + 1] = new SharpDX.Vector3(0, 0, 1);
            plane.Normals[uvOffset + 2] = new SharpDX.Vector3(0, 0, 1);
            plane.Normals[uvOffset + 3] = new SharpDX.Vector3(0, 0, 1);
        }

        private bool _UseSphere = true;
        private MeshGeometry3D MakeCube()
        {

            if (_UseSphere)
            {
                var builder = new MeshBuilder();
                builder.AddSphere(SharpDX.Vector3.Zero);
                builder.ComputeTangents(MeshFaces.Default);
                var mg = builder.ToMesh();
                return mg;
            }
            else
            {
                var builder = new MeshBuilder();
                builder.AddCube();
                /*AddQuad(builder, PlaneAxis.X, 0.5f);
                AddQuad(builder, PlaneAxis.X, -0.5f);
                AddQuad(builder, PlaneAxis.Y, 0.5f);
                AddQuad(builder, PlaneAxis.Y, -0.5f);
                AddQuad(builder, PlaneAxis.Z, 0.5f);
                AddQuad(builder, PlaneAxis.Z, -0.5f);*/

                builder.ComputeTangents(MeshFaces.Default);
                return builder.ToMeshGeometry3D();
            }
        }


        #region INotifyPropertyChanged
        public event PropertyChangedEventHandler PropertyChanged;

        protected void NotifyPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        protected void OnPropertyChanged([CallerMemberName]string info = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(info));
        }

        protected bool Set<T>(ref T backingField, T value, [CallerMemberName]string propertyName = "")
        {
            if (object.Equals(backingField, value))
            {
                return false;
            }

            backingField = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        public void Dispose()
        {
            if(Models != null)
            {
                foreach(var m in Models)
                {
                    m?.Dispose();
                }
            }
        }
        #endregion
    }
}
