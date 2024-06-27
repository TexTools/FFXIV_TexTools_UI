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

using System;
using System.Drawing;
using System.Windows;
using System.Windows.Media.Effects;
using System.Windows.Media.Media3D;

namespace FFXIV_TexTools.Textures
{
    public class ColorChannels : ShaderEffect, IDisposable
    {
        public static readonly DependencyProperty InputProperty = RegisterPixelShaderSamplerProperty("Input", typeof(ColorChannels), 0);

        public static readonly DependencyProperty ChannelProperty = DependencyProperty.Register("Channels", typeof(Point4D), typeof(ColorChannels),
            new UIPropertyMetadata(new Point4D(1.0f, 1.0f, 1.0f, 1.0f), PixelShaderConstantCallback(0)));

        private static readonly PixelShader _pixelShader = new PixelShader
            { UriSource = new Uri("pack://application:,,,/Resources/rgbaChannels.cso") };

        private Brush Input
        {
            get {

                if(InputProperty == null)
                {
                    return null;
                }
                
                var val = GetValue(InputProperty);
                if(val == null)
                {
                    return null;
                }
                return val as Brush;
            }
            set => SetValue(InputProperty, value);
        }

        /// <summary>
        /// The color channel shader
        /// </summary>
        public ColorChannels()
        {
            PixelShader = _pixelShader;

            var eventInfo = typeof(PixelShader).GetEvent("_shaderBytecodeChanged", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            var fieldInfo = typeof(PixelShader).GetField(eventInfo.Name, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            fieldInfo.SetValue(_pixelShader, null);

            UpdateShaderValue(InputProperty);
            UpdateShaderValue(ChannelProperty);
        }

        public Point4D Channel
        {
            get => (Point4D)GetValue(ChannelProperty);
            set => SetValue(ChannelProperty, value);
        }

        public void Dispose()
        {
            if (Input != null)
            {
                Input.Dispose();
                Input = null;
            }
        }
    }
}