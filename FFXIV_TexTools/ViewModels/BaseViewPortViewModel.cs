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
using FFXIV_TexTools.Helpers;
using HelixToolkit.Wpf.SharpDX;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace FFXIV_TexTools.ViewModels
{
    public class BaseViewPortViewModel: ObservableObject, IDisposable
    {
        private Camera _Camera;

        private IRenderTechnique _RenderTechnique;

        private string _SubTitle;

        private string _Title;

        public string Title
        {
            get => _Title;
            set => SetValue(ref _Title, value, "Title");
        }

        public string SubTitle
        {
            get => _SubTitle;
            set => SetValue(ref _SubTitle, value, "SubTitle");
        }

        public IRenderTechnique RenderTechnique
        {
            get => _RenderTechnique;
            set => SetValue(ref _RenderTechnique, value, "RenderTechnique");
        }



        public Camera Camera
        {
            get => _Camera;

            set
            {
                SetValue(ref _Camera, value, "Camera");
            }
        }

        private IEffectsManager _EffectsManager;

        public IEffectsManager EffectsManager
        {
            get => _EffectsManager;
            set => SetValue(ref _EffectsManager, value);
        }

        public BaseViewPortViewModel()
        {
            try
            {
                // The effect manager automatically attaches itself to the renderer via AddTechnique() somehow.
                EffectsManager = new CustomEffectsManager();
            }
            catch (Exception ex)
            {
                Trace.WriteLine(ex);
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
