using HelixToolkit.Wpf.SharpDX;
using FFXIV_TexTools.Configuration;
using SharpDX.Direct3D11;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows;
using System.Windows.Controls;

namespace FFXIV_TexTools.Helpers
{
    // Transfers textures between DX9 and DX11 (WPF and Helix Toolkit) using the CPU rather than relying on resource sharing APIs
    class ViewportCanvasRenderer
    {
        private Texture2D stagingTexture = null;
        private ImageBrush canvasBrush = null;
        private WriteableBitmap canvasBitmap = null;

        public ViewportCanvasRenderer(Viewport3DX viewport3DX, Canvas alternateViewportCanvas)
        {
            viewport3DX.OnRendered += Viewport3DX_OnRendered;
            canvasBrush = new ImageBrush();
            alternateViewportCanvas.Visibility = Visibility.Visible;
            alternateViewportCanvas.Background = canvasBrush;
        }

        private PixelFormat? DXGIFormatToPixelFormat(SharpDX.DXGI.Format dxgiFormat)
        {
            switch (dxgiFormat)
            {
                case SharpDX.DXGI.Format.B8G8R8A8_UNorm:
                case SharpDX.DXGI.Format.B8G8R8A8_Typeless:
                case SharpDX.DXGI.Format.B8G8R8A8_UNorm_SRgb:
                    return PixelFormats.Bgra32;

                case SharpDX.DXGI.Format.B8G8R8X8_UNorm:
                case SharpDX.DXGI.Format.B8G8R8X8_Typeless:
                case SharpDX.DXGI.Format.B8G8R8X8_UNorm_SRgb:
                    return PixelFormats.Bgr32;

                case SharpDX.DXGI.Format.B5G6R5_UNorm:
                    return PixelFormats.Bgr565;

                case SharpDX.DXGI.Format.B5G5R5A1_UNorm:
                    return PixelFormats.Bgr555;

                case SharpDX.DXGI.Format.R8G8B8A8_Typeless:
                case SharpDX.DXGI.Format.R8G8B8A8_UInt:
                case SharpDX.DXGI.Format.R8G8B8A8_UNorm:
                case SharpDX.DXGI.Format.R8G8B8A8_UNorm_SRgb:
                case SharpDX.DXGI.Format.R8G8B8A8_SInt:
                case SharpDX.DXGI.Format.R8G8B8A8_SNorm:
                    return PixelFormats.Rgb24;

                case SharpDX.DXGI.Format.R16G16B16A16_Typeless:
                case SharpDX.DXGI.Format.R16G16B16A16_UNorm:
                case SharpDX.DXGI.Format.R16G16B16A16_UInt:
                case SharpDX.DXGI.Format.R16G16B16A16_SNorm:
                case SharpDX.DXGI.Format.R16G16B16A16_SInt:
                    return PixelFormats.Rgba64;

                case SharpDX.DXGI.Format.R32G32B32A32_Float:
                    return PixelFormats.Rgb128Float;

                default:
                    return null;
            }
        }

        private void Viewport3DX_OnRendered(object sender, System.EventArgs e)
        {
            var renderHost = (IRenderHost)sender;

            var deviceContext = renderHost.EffectsManager.Device.ImmediateContext;
            var backbuffer = renderHost.RenderBuffer.BackBuffer;
            var backbufferTexture = backbuffer.Resource as Texture2D;
            var bbDesc = backbufferTexture.Description;
            if (stagingTexture == null || bbDesc.Width != stagingTexture.Description.Width || bbDesc.Height != stagingTexture.Description.Height)
            {
                if (stagingTexture != null)
                    stagingTexture.Dispose();
                var desc = bbDesc;
                var targetFormat = DXGIFormatToPixelFormat(bbDesc.Format);
                desc.BindFlags = BindFlags.None;
                desc.CpuAccessFlags = CpuAccessFlags.Read;
                desc.Usage = ResourceUsage.Staging;
                stagingTexture = new Texture2D(deviceContext.Device, desc);
                canvasBitmap = new WriteableBitmap(bbDesc.Width, bbDesc.Height, 96.0, 96.0, targetFormat ?? PixelFormats.Bgr32, null);
                canvasBrush.ImageSource = canvasBitmap;
            }

            deviceContext.CopyResource(backbufferTexture, stagingTexture);

            SharpDX.DataStream dataStream;
            var dataBox = deviceContext.MapSubresource(stagingTexture, 0, 0, MapMode.Read, MapFlags.None, out dataStream);
            canvasBitmap.Lock();
            try
            {
                for (int row = 0; row < bbDesc.Height; ++row)
                {
                    unsafe
                    {
                        byte* src = (byte*)(dataBox.DataPointer + row * dataBox.RowPitch);
                        byte* dest = (byte*)(canvasBitmap.BackBuffer + row * canvasBitmap.BackBufferStride);
                        System.Buffer.MemoryCopy(src, dest, canvasBitmap.BackBufferStride, dataBox.RowPitch);
                    }
                }

                canvasBitmap.AddDirtyRect(new Int32Rect(0, 0, bbDesc.Width, bbDesc.Height));
            }
            finally
            {
                canvasBitmap.Unlock();
                deviceContext.UnmapSubresource(stagingTexture, 0);
            }
        }
    }
}
