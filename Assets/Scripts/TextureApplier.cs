using UnityEngine;

namespace Junkinnering
{
    /// <summary>
    /// Applies a texture to a renderer through a MaterialPropertyBlock, avoiding a per-object
    /// material instance (and its leak). Targets URP's _BaseMap property.
    /// </summary>
    public static class TextureApplier
    {
        private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");

        public static void Apply(Renderer renderer, Texture texture)
        {
            MaterialPropertyBlock mpb = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(mpb);
            mpb.SetTexture(BaseMapId, texture);
            renderer.SetPropertyBlock(mpb);
        }
    }
}
