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

using FFXIV_TexTools.Helpers;
using HelixToolkit.Wpf.SharpDX;
using System;
using System.Collections.Generic;
using System.IO;

namespace FFXIV_TexTools.ViewModels
{
    public class BaseViewPortViewModel: ObservableObject, IDisposable
    {
        private const string Orthographic = "Orthographic Camera";

        private const string Perspective = "Perspective Camera";

        private string _cameraModel;

        private Camera _camera;

        private IRenderTechnique _renderTechnique;

        private string _subTitle;

        private string _title;

        public string Title
        {
            get => _title;
            set => SetValue(ref _title, value, "Title");
        }

        public string SubTitle
        {
            get => _subTitle;
            set => SetValue(ref _subTitle, value, "SubTitle");
        }

        public IRenderTechnique RenderTechnique
        {
            get => _renderTechnique;
            set => SetValue(ref _renderTechnique, value, "RenderTechnique");
        }

        private List<string> CameraModelCollection { get; set; }

        public string CameraModel
        {
            get => _cameraModel;
            set
            {
                if (SetValue(ref _cameraModel, value, "CameraModel"))
                {
                    OnCameraModelChanged();
                }
            }
        }

        public Camera Camera
        {
            get => _camera;

            set
            {
                SetValue(ref _camera, value, "Camera");
                CameraModel = value is PerspectiveCamera
                                       ? Perspective
                                       : value is OrthographicCamera ? Orthographic : null;
            }
        }

        private IEffectsManager effectsManager;

        public IEffectsManager EffectsManager
        {
            get => effectsManager;
            set => SetValue(ref effectsManager, value);
        }

        private string _renderTechniqueName = DefaultRenderTechniqueNames.Mesh;
        public string RenderTechniqueName
        {
            get => _renderTechniqueName;
            set
            {
                _renderTechniqueName = value;
                RenderTechnique = EffectsManager[value];
            }

        }

        private readonly OrthographicCamera _defaultOrthographicCamera = new OrthographicCamera { Position = new System.Windows.Media.Media3D.Point3D(0, 0, 5), LookDirection = new System.Windows.Media.Media3D.Vector3D(-0, -0, -5), UpDirection = new System.Windows.Media.Media3D.Vector3D(0, 1, 0), NearPlaneDistance = 1, FarPlaneDistance = 100 };

        private readonly PerspectiveCamera _defaultPerspectiveCamera = new PerspectiveCamera { Position = new System.Windows.Media.Media3D.Point3D(0, 0, 5), LookDirection = new System.Windows.Media.Media3D.Vector3D(-0, -0, -5), UpDirection = new System.Windows.Media.Media3D.Vector3D(0, 1, 0), NearPlaneDistance = 0.5, FarPlaneDistance = 150 };

        public event EventHandler CameraModelChanged;

        public BaseViewPortViewModel()
        {
            // camera models
            CameraModelCollection = new List<string>()
            {
                Orthographic,
                Perspective,
            };

            // on camera changed callback
            CameraModelChanged += (s, e) =>
            {
                if (_cameraModel == Orthographic)
                {
                    if (!(Camera is OrthographicCamera))
                        Camera = _defaultOrthographicCamera;
                }
                else if (_cameraModel == Perspective)
                {
                    if (!(Camera is PerspectiveCamera))
                        Camera = _defaultPerspectiveCamera;
                }
                else
                {
                    throw new HelixToolkitException("Camera Model Error.");
                }
            };

            // default camera model
            CameraModel = Perspective;

            Title = "Demo (HelixToolkitDX)";
            SubTitle = "Default Base View Model";
        }

        public void OnCameraModelChanged()
        {
            var eh = CameraModelChanged;
            eh?.Invoke(this, new EventArgs());
        }

        public static MemoryStream LoadFileToMemory(string filePath)
        {
            using (var file = new FileStream(filePath, FileMode.Open))
            {
                var memory = new MemoryStream();
                file.CopyTo(memory);
                return memory;
            }
        }

        #region IDisposable Support
        private bool disposedValue = false; // To detect redundant calls

        public virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    // TODO: dispose managed state (managed objects).
                }

                // TODO: free unmanaged resources (unmanaged objects) and override a finalizer below.
                // TODO: set large fields to null.
                if (EffectsManager != null)
                {
                    var effectManager = EffectsManager as IDisposable;
                    Disposer.RemoveAndDispose(ref effectManager);
                }
                disposedValue = true;
                GC.SuppressFinalize(this);
            }
        }

        // TODO: override a finalizer only if Dispose(bool disposing) above has code to free unmanaged resources.
        ~BaseViewPortViewModel()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(false);
        }

        // This code added to correctly implement the disposable pattern.
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);
            // TODO: uncomment the following line if the finalizer is overridden above.
            // GC.SuppressFinalize(this);
        }
        #endregion
    }
}
