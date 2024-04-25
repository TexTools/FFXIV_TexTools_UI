// FFXIV TexTools
// Copyright © 2020 Rafael Gonzalez - All Rights Reserved
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
using HelixToolkit.Wpf.SharpDX.Model.Scene;
using System.Collections.Generic;

namespace FFXIV_TexTools.Custom
{
    public class CustomBoneSkinMeshGeometry3D : BoneSkinMeshGeometryModel3D
    {
        public bool IsBody { get; set; }

        public string ItemType { get; set; }

        public List<string> BoneList { get; set; }

        protected override SceneNode OnCreateSceneNode()
        {
            var node = new BoneSkinMeshNode();

            node.OnSetRenderTechnique = effectsManager =>
            {
                return effectsManager[CustomEffectsManager.CustomShaderNames.CustomShader];
            };

            return node;
        }
    }
}