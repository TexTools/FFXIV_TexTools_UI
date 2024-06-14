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

using FFXIV_TexTools.Custom;
using FFXIV_TexTools.Properties;
using FFXIV_TexTools.Resources;
using FFXIV_TexTools.Views;
using FFXIV_TexTools.Views.Controls;
using HelixToolkit.Wpf.SharpDX;
using HelixToolkit.Wpf.SharpDX.Cameras;
using Newtonsoft.Json.Linq;
using SharpDX;
using SharpDX.Direct3D11;
using SharpDX.Direct3D9;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Media3D;
using System.Windows.Navigation;
using xivModdingFramework.General.Enums;
using xivModdingFramework.Models.DataContainers;
using xivModdingFramework.Models.FileTypes;
using xivModdingFramework.Models.Helpers;
using MeshBuilder = HelixToolkit.Wpf.SharpDX.MeshBuilder;
using MeshGeometry3D = HelixToolkit.Wpf.SharpDX.MeshGeometry3D;
using PerspectiveCamera = HelixToolkit.Wpf.SharpDX.PerspectiveCamera;
using TextureModel = HelixToolkit.Wpf.SharpDX.TextureModel;
using Vector4 = System.Numerics.Vector4;
using WinColor = System.Windows.Media.Color;

namespace FFXIV_TexTools.ViewModels
{
    public class Viewport3DViewModel : BaseViewPortViewModel, INotifyPropertyChanged
    {
        private string _backgroundColor;
        private Vector3D _light1Direction;
        private Vector3D _light2Direction;
        private Vector3D _light3Direction;
        private static readonly Regex bodyMaterial = new Regex("[bf][0-9]{4}", RegexOptions.IgnoreCase);

        public delegate void ViewportEventHandler(Viewport3DViewModel owner);
        public delegate void ViewportZoomEventHandler(Viewport3DViewModel owner, double animationTime, Rect3D? boundingBox);
        public event ViewportEventHandler TextureUpdateRequested;
        public event ViewportZoomEventHandler ZoomExtentsRequested;
        public event EventHandler<int> VisibleMeshChanged;

        public ObservableElement3DCollection Models { get; } = new ObservableElement3DCollection();

        public Viewport3DViewModel()
        {
            Title = "";
            SubTitle = "";

            // Eat exception to not immediately crash in VirtualBox
            try
            {
                EffectsManager = new CustomEffectsManager();
            } catch { }

            Camera = new PerspectiveCamera();
            Camera.CameraInternal.PropertyChanged += CameraInternal_PropertyChanged;

            BackgroundColor = Properties.Settings.Default.BG_Color;
            ReflectionLabel = $"{UIStrings.Reflection}  |  {ReflectionValue}";

            var csetMax = 32;
#if ENDWALKER
            csetMax = 16;
#endif
            ColorsetRowSource.Add(new KeyValuePair<string, int>("All", -1));
            for(int i = 0; i < csetMax; i++)
            {
                ColorsetRowSource.Add(new KeyValuePair<string, int>(ViewHelpers.ColorsetRowToNiceName(i).ToString(), i));
            }

            ResetLights();
        }


        protected void CameraInternal_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if(e.PropertyName == "LookDirection")
            {
                OnCameraDirectionChanged();
            }
        }

        private bool _MoveLightsWithCamera = true;
        public bool MoveLightsWithCamera
        {
            get => _MoveLightsWithCamera;
            set
            {
                _MoveLightsWithCamera = value;
                ResetLights();
                OnPropertyChanged(nameof(MoveLightsWithCamera));
            }
        }

        private void OnCameraDirectionChanged()
        {
            if (_MoveLightsWithCamera)
            {
                ResetLights();
            }
        }

        private MeshGeometry3D GetMeshGeometry(TTModel model, int meshGroupId)
        {
            var group = model.MeshGroups[meshGroupId];
            var mg = new MeshGeometry3D
            {
                Positions = new Vector3Collection((int)group.VertexCount),
                Normals = new Vector3Collection((int)group.VertexCount),
                Colors = new Color4Collection((int)group.VertexCount),
                TextureCoordinates = new Vector2Collection((int)group.VertexCount),
                BiTangents = new Vector3Collection((int)group.VertexCount),
                Tangents = new Vector3Collection((int)group.VertexCount),
                Indices = new IntCollection((int)group.IndexCount)
            };

            var indexCount = 0;
            var vertCount = 0;

            for (int mi = 0; mi < meshGroupId; mi++)
            {
                var g = model.MeshGroups[mi];
                //vertCount += (int) g.VertexCount;
                //indexCount += (int)g.IndexCount;
            }

            foreach (var p in group.Parts)
            {
                foreach (var v in p.Vertices) {

                    // I don't think our current shader actually utilizes this data anyways
                    // but may as well include it correctly.
                    var color = new Color4();
                    color.Red = v.VertexColor[0] / 255f;
                    color.Green = v.VertexColor[1] / 255f;
                    color.Blue = v.VertexColor[2] / 255f;
                    color.Alpha = v.VertexColor[3] / 255f;

                    mg.Positions.Add(v.Position);
                    mg.Normals.Add(v.Normal);
                    mg.TextureCoordinates.Add(v.UV1);
                    mg.Colors.Add(color);
                    mg.BiTangents.Add(v.Binormal);
                    mg.Tangents.Add(v.Tangent);
                }

                foreach (var vertexId in p.TriangleIndices)
                {
                    // Have to bump these to account for merging the lists together.
                    mg.Indices.Add(vertCount + vertexId);
                }


                vertCount += p.Vertices.Count;
                indexCount += p.TriangleIndices.Count;
            }
            return mg;
        }

        private TTModel _Model;
        private List<ModelTextureData> _Textures;
        public TTModel Model {
            get => _Model;
        }
        public List<ModelTextureData> Textures {
            get => _Textures;
        }

        private List<MeshGeometry3D> _Geometry = new List<MeshGeometry3D>();
        private List<PhongMaterial> _Materials = new List<PhongMaterial>();

        private bool _UPDATING = false;
        private void UpdateVisibleMeshSource()
        {
            _UPDATING = true;
            VisibleMeshSource.Clear();
            VisibleMeshSource.Add(new KeyValuePair<string, int>("All", -1));
            if(_Model == null)
            {
                VisibleMesh = -1;
                return;
            }

            for(int i = 0; i < _Model.MeshGroups.Count; i++)
            {
                VisibleMeshSource.Add(new KeyValuePair<string, int>(i.ToString(), i));
            }

            ActiveShapes.Clear();
            VisibleMesh = -1;
            HighlightedColorsetRow = -1;

            _UPDATING = false;
        }

        private Guid LastUpdateId;

        /// <summary>
        /// Updates the model in the 3D viewport
        /// </summary>
        /// <param name="mdlData">The model data</param>
        /// <param name="textureDataDictionary">The texture dictionary for the model</param>
        public async Task UpdateModel(TTModel importModel = null, List<ModelTextureData> importTextures = null)
        {
            if (_UPDATING)
            {
                return;
            }

            var myId = Guid.NewGuid();
            LastUpdateId = myId;

            var newModel = false;
            var newTextures = false;
            var originalModel = _Model;

            if (importModel != null)
            {
                newModel = true;
                _Model = importModel;
            }
            var model = _Model;

            if (importTextures != null)
            {
                newTextures = true;
                _Textures = importTextures;
            }
            var textureDataDictionary = _Textures;


            ShapeButtonEnabled = _Model.HasShapeData;
            ClearModels();

            var totalMeshCount = model.MeshGroups.Count;

            if (VisibleMesh >= totalMeshCount)
            {
                // This retriggers UpdateModel
                VisibleMesh = -1;
                return;
            }

            if (_Model == null)
            {
                return;
            }


            // Push all the potentially CPU intense stuff onto a new thread.
            await Task.Run(() =>
            {
                if (newModel && originalModel != _Model)
                {
                    // Only recalculate if an actually new-new model, since this doesn't change on shape application.
                    ModelModifiers.CalculateTangents(model);
                }

                if (newModel)
                {
                    _Geometry.Clear();
                    ModelModifiers.ApplyShapes(model, ActiveShapes, true);
                }

                if (newTextures)
                {
                    _Materials.Clear();
                }

                for (var i = 0; i < totalMeshCount; i++)
                {
                    if (newModel)
                    {
                        var meshGeometry3D = GetMeshGeometry(model, i);
                        _Geometry.Add(meshGeometry3D);
                    }

                    var isBodyMaterial = bodyMaterial.IsMatch(model.MeshGroups[i].Material);
                    var mtrlName = "/" + Path.GetFileName(model.MeshGroups[i].Material);

                    if (newTextures)
                    {

                        var textureData = textureDataDictionary.FirstOrDefault(x => x.MaterialPath == mtrlName);
                        if (textureData == null)
                        {
                            if (ModelModifiers.IsSkinMaterial(mtrlName))
                            {
                                textureData = textureDataDictionary.FirstOrDefault(x => x.IsSkin);
                                if(textureData == null)
                                {
                                    // Skin material, but we have no textures for skin.
                                    textureData = ModelFileControl.GetPlaceholderTexture(mtrlName);
                                }
                            }
                            else
                            {
                                // Data was invalid somehow, use a placeholder.
                                textureData = ModelFileControl.GetPlaceholderTexture(mtrlName);
                            }
                        }

                        TextureModel diffuse = null, specular = null, normal = null, alpha = null, emissive = null;
                        if (!isBodyMaterial)
                        {
                            if (textureData.Diffuse != null && textureData.Diffuse.Length > 0)
                                diffuse = new TextureModel(NormalizePixelData(textureData.Diffuse), textureData.Width, textureData.Height);

                            if (textureData.Specular != null && textureData.Specular.Length > 0)
                                specular = new TextureModel(NormalizePixelData(textureData.Specular), textureData.Width, textureData.Height);
                        }
                        else
                        {
                            if (textureData.Diffuse != null && textureData.Diffuse.Length > 0)
                                diffuse = new TextureModel(textureData.Diffuse, SharpDX.DXGI.Format.R8G8B8A8_UNorm, textureData.Width, textureData.Height);

                            if (textureData.Specular != null && textureData.Specular.Length > 0)
                                specular = new TextureModel(textureData.Specular, SharpDX.DXGI.Format.R8G8B8A8_UNorm, textureData.Width, textureData.Height);
                        }

                        if (textureData.Normal != null && textureData.Normal.Length > 0)
                            normal = new TextureModel(textureData.Normal, SharpDX.DXGI.Format.R8G8B8A8_UNorm, textureData.Width, textureData.Height);

                        if (textureData.Alpha != null && textureData.Alpha.Length > 0)
                            alpha = new TextureModel(textureData.Alpha, SharpDX.DXGI.Format.R8G8B8A8_UNorm, textureData.Width, textureData.Height);

                        if (textureData.Emissive != null && textureData.Emissive.Length > 0)
                            emissive = new TextureModel(textureData.Emissive, SharpDX.DXGI.Format.R8G8B8A8_UNorm, textureData.Width, textureData.Height);

                        var material = new PhongMaterial
                        {
                            DiffuseColor = PhongMaterials.ToColor(1, 1, 1, 1),
                            SpecularShininess = ReflectionValue,
                            DiffuseMap = diffuse,
                            DiffuseAlphaMap = alpha,
                            SpecularColorMap = specular,
                            NormalMap = normal,
                            EmissiveMap = emissive
                        };

                        _Materials.Add(material);
                    }
                }
            });

            if(myId != LastUpdateId)
            {
                // We got re-called while we were off thread.
                return;
            }

            if (newModel && originalModel != _Model)
            {
                UpdateVisibleMeshSource();
            }

            for (var i = 0; i < totalMeshCount; i++)
            {
                if (VisibleMesh == i || VisibleMesh < 0)
                {
                    var mgm3d = new CustomMeshGeometryModel3D
                    {
                        Geometry = _Geometry[i],
                        Material = _Materials[i],
                        Source = model.Source,
                    };

                    var mtrlName = "/" + Path.GetFileName(model.MeshGroups[i].Material);
                    var textureData = textureDataDictionary.FirstOrDefault(x => x.MaterialPath == mtrlName);
                    if (textureData != null && textureData.RenderBackfaces)
                    {
                        mgm3d.CullMode = CullMode.None;
                    }
                    else
                    {
                        mgm3d.CullMode = CullMode.Back;
                    }

                    Models.Add(mgm3d);
                }
            }

            OnPropertyChanged(nameof(Models));

            var center = new Vector3(0, 1, 0);
            Rect3D r3d = new Rect3D();
            if (Models.Count > 0)
            {

                // SharpDX sucks at computing these in a reasonable way.
                var minPoint = new Vector3(9999.0f);
                var maxPoint = new Vector3(-9999.0f);

                foreach (var m in Models)
                {
                    minPoint.X = minPoint.X < m.Bounds.Minimum.X ? minPoint.X : m.Bounds.Minimum.X;
                    minPoint.Y = minPoint.Y < m.Bounds.Minimum.Y ? minPoint.Y : m.Bounds.Minimum.Y;
                    minPoint.Z = minPoint.Z < m.Bounds.Minimum.Z ? minPoint.Z : m.Bounds.Minimum.Z;
                    maxPoint.X = maxPoint.X > m.Bounds.Maximum.X ? maxPoint.X : m.Bounds.Maximum.X;
                    maxPoint.Y = maxPoint.Y > m.Bounds.Maximum.Y ? maxPoint.Y : m.Bounds.Maximum.Y;
                    maxPoint.Z = maxPoint.Z > m.Bounds.Maximum.Z ? maxPoint.Z : m.Bounds.Maximum.Z;
                }

                center = ((maxPoint - minPoint) / 2.0f) + minPoint;
                var sizeX = maxPoint.X - minPoint.X;
                var sizeY = maxPoint.Y - minPoint.Y;
                var sizeZ = maxPoint.Z - minPoint.Z;

                r3d = new Rect3D(minPoint.X, minPoint.Y, minPoint.Z, sizeX, sizeY, sizeZ);

            }


            
            Camera.UpDirection = new Vector3D(0, 1, 0);

            if(newModel && originalModel != _Model && AllowCameraReset)
            {
                ResetLights();

                if (Models.Count == 0) {
                    ZoomExtentsRequested?.Invoke(this, 0, null);
                } else
                {
                    ZoomExtentsRequested?.Invoke(this, 0, r3d);
                }
            }
        }

        /// <summary>
        /// Take the square root of every pixels' RGB datapoints to match the behavior of the FF14 engine
        /// </summary>
        /// <param name="img"></param>
        protected static Color4[] NormalizePixelData(byte[] img)
        {
            Color4[] result = new Color4[img.Length / 4];
            Parallel.ForEach(Partitioner.Create(0, img.Length / 4), range =>
            {
                for (int i = range.Item1 * 4; i < range.Item2 * 4; i += 4)
                {
                    // This is the only way to do a true single-precision sqrt in .NET Framework
                    var tmp = Vector4.SquareRoot(new Vector4(
                        img[i] / 255.0f,
                        img[i + 1] / 255.0f,
                        img[i + 2] / 255.0f,
                        img[i + 3] / 255.0f
                    ));
                    result[i / 4] = new Color4(tmp.X, tmp.Y, tmp.Z, tmp.W);
                }
            });
            return result;
        }

        /// <summary>
        /// Event handler for the camera property changing
        /// </summary>
        protected virtual void ResetLights()
        {
            var cam = Camera.CameraInternal.LookDirection;
            cam.Y = 0;
            cam.Normalize();

            double angle = 0;
            if (MoveLightsWithCamera)
            {
                angle = Math.Atan2(cam.X * -1, cam.Z * -1);
            }


            var rotMatrix = Matrix.RotationAxis(new Vector3(0, 1, 0), (float) angle);

            var light = new Vector3(-1, -0.5f, -1);
            var res = Vector3.Transform(light, rotMatrix);
            Light1Direction = new Vector3D(res.X, res.Y, res.Z);


            light = new Vector3(1, -0.5f, -1);
            res = Vector3.Transform(light, rotMatrix);
            Light2Direction = new Vector3D(res.X, res.Y, res.Z);

            light = new Vector3(0, 0.5f, 1);
            res = Vector3.Transform(light, rotMatrix);
            Light3Direction = new Vector3D(res.X, res.Y, res.Z);

            UpdateLightSliders();
        }

        #region Properties

        /// <summary>
        /// The background color of the viewport
        /// </summary>
        public string BackgroundColor
        {
            get => _backgroundColor;
            set
            {
                _backgroundColor = value;
                OnPropertyChanged(nameof(BackgroundColor));
            }
        }

        /// <summary>
        /// The direction of light 1
        /// </summary>
        public Vector3D Light1Direction
        {
            get => _light1Direction;
            set
            {
                _light1Direction = value;
                OnPropertyChanged(nameof(Light1Direction));
            }
        }

        /// <summary>
        /// The direction of light 2
        /// </summary>
        public Vector3D Light2Direction
        {
            get => _light2Direction;
            set
            {
                _light2Direction = value;
                OnPropertyChanged(nameof(Light2Direction));
            }
        }

        /// <summary>
        /// The direction of light 3
        /// </summary>
        public Vector3D Light3Direction
        {
            get => _light3Direction;
            set
            {
                _light3Direction = value;
                OnPropertyChanged(nameof(Light3Direction));
            }
        }

        #endregion


        /// <summary>
        /// Clear all the models in the viewport
        /// </summary>
        public void ClearModels()
        {
            foreach(var mdl in Models)
            {
                mdl.Dispose();
            }
            Models.Clear();
        }

        /// <summary>
        /// Updates the direction of the scene lights
        /// </summary>
        /// <param name="value">The new direction value</param>
        /// <param name="num">The light number</param>
        /// <param name="pos">The light position (X, Y, or Z)</param>
        public void UpdateLighting(double value, int num, string pos)
        {
            if (pos.Equals("X"))
            {
                if (num == 0)
                {
                    Light1Direction = new Vector3D(value, Light1Direction.Y, Light1Direction.Z);
                }
                else if (num == 1)
                {
                    Light2Direction = new Vector3D(value, Light2Direction.Y, Light2Direction.Z);
                }
                else if (num == 2)
                {
                    Light3Direction = new Vector3D(value, Light3Direction.Y, Light3Direction.Z);
                }
            }

            if (pos.Equals("Y"))
            {
                if (num == 0)
                {
                    Light1Direction = new Vector3D(Light1Direction.X, value, Light1Direction.Z);
                }
                else if (num == 1)
                {
                    Light2Direction = new Vector3D(Light2Direction.X, value, Light2Direction.Z);
                }
                else if (num == 2)
                {
                    Light3Direction = new Vector3D(Light3Direction.X, value, Light3Direction.Z);
                }
            }

            if (pos.Equals("Z"))
            {
                if (num == 0)
                {
                    Light1Direction = new Vector3D(Light1Direction.X, Light1Direction.Y, value);
                }
                else if (num == 1)
                {
                    Light2Direction = new Vector3D(Light2Direction.X, Light2Direction.Y, value);
                }
                else if (num == 2)
                {
                    Light3Direction = new Vector3D(Light3Direction.X, Light3Direction.Y, value);
                }
            }
        }

        /// <summary>
        /// Gets the current direction for a given light
        /// </summary>
        /// <param name="lightNumber">The light number</param>
        /// <returns></returns>
        public (double x, double y, double z) GetLightOffset(int lightNumber)
        {
            switch (lightNumber)
            {
                case 0:
                    {
                        var x = Light1Direction.X;
                        var y = Light1Direction.Y;
                        var z = Light1Direction.Z;

                        return (x, y, z);
                    }
                case 1:
                    {
                        var x = Light2Direction.X;
                        var y = Light2Direction.Y;
                        var z = Light2Direction.Z;

                        return (x, y, z);
                    }
                case 2:
                    {
                        var x = Light3Direction.X;
                        var y = Light3Direction.Y;
                        var z = Light3Direction.Z;

                        return (x, y, z);
                    }
                default:
                    return (0, 0, 0);
            }
        }

        /// <summary>
        /// Updates the reflection value of the model's material
        /// </summary>
        /// <param name="value">The new reflection value</param>
        public void UpdateReflection(int value)
        {
            foreach (var model in Models)
            {
                var material = ((MeshGeometryModel3D)model).Material as PhongMaterial;

                material.SpecularShininess = value;
            }

            _ReflectionValue = value;
        }

        #region INotify Properties

        private WinColor _Light1Color = new WinColor() { R = 128, G = 128, B = 128, A = 128 };
        public WinColor Light1Color
        {
            get => _Light1Color;
            set
            {
                _Light1Color = value;
                OnPropertyChanged(nameof(Light1Color));
            }
        }
        private WinColor _Light2Color = new WinColor() { R = 128, G = 128, B = 128, A = 128 };
        public WinColor Light2Color
        {
            get => _Light2Color;
            set
            {
                _Light2Color = value;
                OnPropertyChanged(nameof(Light2Color));
            }
        }
        private WinColor _Light3Color = new WinColor() { R = 128, G = 128, B = 128, A = 128 };
        public WinColor Light3Color
        {
            get => _Light3Color;
            set
            {
                _Light3Color = value;
                OnPropertyChanged(nameof(Light3Color));
            }
        }

        private float _LightingXValue;
        public float LightingXValue
        {
            get => _LightingXValue;
            set
            {
                _LightingXValue = value;
                UpdateLighting(value, _CheckedLight, "X");
                OnPropertyChanged(nameof(LightingXValue));
            }
        }

        private float _LightingYValue;
        public float LightingYValue
        {
            get => _LightingYValue;
            set
            {
                _LightingYValue = value;
                UpdateLighting(value, _CheckedLight, "Y");
                OnPropertyChanged(nameof(LightingYValue));
            }
        }

        private float _lightingZValue;
        public float LightingZValue
        {
            get => _lightingZValue;
            set
            {
                _lightingZValue = value;
                UpdateLighting(value, _CheckedLight, "Z");
                OnPropertyChanged(nameof(LightingZValue));
            }
        }

        private int _ReflectionValue  = 5;
        public int ReflectionValue
        {
            get => _ReflectionValue;
            set
            {
                UpdateReflection(value);
                ReflectionLabel = $"{UIStrings.Reflection}  |  {value}";
                OnPropertyChanged(nameof(ReflectionValue));
            }
        }

        private int _CheckedLight = 0;

        private bool _Light1Check = true;
        public bool Light1Check
        {
            get => _Light1Check;
            set
            {
                _Light1Check = value;
                if (value)
                {
                    _CheckedLight = 0;
                    UpdateLightSliders();
                }
            }
        }

        private bool _Light2Check;
        public bool Light2Check
        {
            get => _Light2Check;
            set
            {
                _Light2Check = value;
                if (value)
                {
                    _CheckedLight = 1;
                    UpdateLightSliders();
                }
            }
        }

        private bool _Light3Check;
        public bool Light3Check
        {
            get => _Light3Check;
            set
            {
                _Light3Check = value;
                if (value)
                {
                    _CheckedLight = 2;
                    UpdateLightSliders();
                }
            }
        }
        public string _reflectionLabel;
        public string ReflectionLabel
        {
            get => _reflectionLabel;
            set
            {
                _reflectionLabel = value;
                OnPropertyChanged(nameof(ReflectionLabel));
            }
        }

        private bool _ColorsetButtonEnabled;
        public bool ColorsetButtonEnabled
        {
            get => _ColorsetButtonEnabled;
            set
            {
                _ColorsetButtonEnabled = value;
                OnPropertyChanged(nameof(ColorsetButtonEnabled));
            }
        }

        private bool _ShapeButtonEnabled;
        public bool ShapeButtonEnabled
        {
            get => _ShapeButtonEnabled;
            set
            {
                _ShapeButtonEnabled = value;
                OnPropertyChanged(nameof(ShapeButtonEnabled));
            }
        }

        public bool _AllowCameraReset = true;
        public bool AllowCameraReset
        {
            get => _AllowCameraReset;
            set
            {
                _AllowCameraReset = value;
                OnPropertyChanged(nameof(AllowCameraReset));
            }
        }

        private int _VisibleMesh = -1;
        public int VisibleMesh
        {
            get => _VisibleMesh;
            set
            {
                _VisibleMesh = value;
                _ = UpdateModel();
                OnPropertyChanged(nameof(VisibleMesh));
                VisibleMeshChanged?.Invoke(this, VisibleMesh);
            }
        }

        private ObservableCollection<KeyValuePair<string, int>> _VisibleMeshSource = new ObservableCollection<KeyValuePair<string, int>>();
        public ObservableCollection<KeyValuePair<string, int>> VisibleMeshSource
        {
            get => _VisibleMeshSource;
            set
            {
                _VisibleMeshSource = value;
                OnPropertyChanged(nameof(VisibleMeshSource));
            }
        }


        private int _HighlightedColorsetRow = -1;
        public int HighlightedColorsetRow
        {
            get => _HighlightedColorsetRow;
            set
            {
                var original = _HighlightedColorsetRow;
                _HighlightedColorsetRow = value;
                OnPropertyChanged(nameof(HighlightedColorsetRow));

                if (original != _HighlightedColorsetRow)
                {
                    // This has to be handled by our external view.
                    TextureUpdateRequested?.Invoke(this);
                }
            }
        }
        private ObservableCollection<KeyValuePair<string, int>> _ColorsetRowSource = new ObservableCollection<KeyValuePair<string, int>>();
        public ObservableCollection<KeyValuePair<string, int>> ColorsetRowSource
        {
            get => _ColorsetRowSource;
            set
            {
                _ColorsetRowSource = value;
                OnPropertyChanged(nameof(ColorsetRowSource));
            }
        }

        private List<string> _ActiveShapes = new List<string>();

        public List<string> ActiveShapes
        {
            get => _ActiveShapes.ToList();
        }

        public void AddShape(string shape)
        {
            _ActiveShapes.Add(shape);
            _ = UpdateModel(_Model);
        }

        public void RemoveShape(string shape)
        {
            _ActiveShapes.Remove(shape);
            _ = UpdateModel(_Model);
        }
        
        public void SetShapes(IEnumerable<string> shapes)
        {
            _ActiveShapes = shapes.ToList();
            _ = UpdateModel(_Model);
        }


        /// <summary>
        /// Update the light position values on the sliders
        /// </summary>
        private void UpdateLightSliders()
        {
            var lightValues = GetLightOffset(_CheckedLight);

            LightingXValue = (float)lightValues.x;
            LightingYValue = (float)lightValues.y;
            LightingZValue = (float)lightValues.z;
        }

        #endregion
    }
}
