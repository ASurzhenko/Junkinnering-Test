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
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        public static void Apply(Renderer renderer, Texture texture)
        {
            MaterialPropertyBlock mpb = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(mpb);
            mpb.SetTexture(BaseMapId, texture);
            renderer.SetPropertyBlock(mpb);
        }

        /// <summary>
        /// Tints the renderer's URP _BaseColor while preserving the rest of the block (reads
        /// the existing block first, so it never clobbers the _BaseMap set by Apply). Used by
        /// the negative-feedback flash.
        /// </summary>
        public static void SetTint(Renderer renderer, Color color)
        {
            MaterialPropertyBlock mpb = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(mpb);
            mpb.SetColor(BaseColorId, color);
            renderer.SetPropertyBlock(mpb);
        }
    }
}
