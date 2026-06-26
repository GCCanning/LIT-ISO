using UnityEngine;

namespace IsoCore.Foundation
{
    /// <summary>
    /// Single source of truth for "how big is this sprite in the world." Scales a sprite's
    /// transform so its rendered VISIBLE height matches an intended world height (in units),
    /// so a prop's in-world size is set by design intent -- relative to the ~1.1-unit player --
    /// rather than by whatever pixel canvas (and transparent padding) the source art happened
    /// to be drawn on. Visible height is canvas height x the opaque-content fraction from
    /// FoundationSpriteMetrics, so props with heavy padding (campfire 45%, flowers 38%) no
    /// longer render undersized at the same heightUnits.
    ///
    /// Calibration reference (player ~= 1.1u tall):
    ///   flower/tuft ~= 0.25 . bush/small rock ~= 0.5 . barrel/anvil ~= 0.7-0.9 . person ~= 1.1
    ///   . sapling ~= 1.8 . pine/oak ~= 3.0-3.2 . cottage ~= 2.5 . shop/tavern ~= 3.5-4 . hall ~= 5-6.
    /// </summary>
    public static class FoundationSpriteScale
    {
        /// <summary>Uniformly scales <paramref name="t"/> so the VISIBLE art of
        /// <paramref name="sprite"/> renders at <paramref name="targetHeightUnits"/> tall.
        /// Trim-aware via FoundationSpriteMetrics; no-op for missing data. Clamped so tiny
        /// source art isn't blown up absurdly and huge art isn't crushed.</summary>
        public static void NormalizeHeight(Transform t, Sprite sprite, float targetHeightUnits,
            float minScale = 0.3f, float maxScale = 4f)
        {
            if (t == null || sprite == null || targetHeightUnits <= 0f) return;
            float spriteHeight = sprite.bounds.size.y;
            if (spriteHeight <= 0.001f) return;
            float frac = FoundationSpriteMetrics.ContentFracH(sprite.name);
            float visibleHeight = spriteHeight * frac;
            if (visibleHeight <= 0.001f) visibleHeight = spriteHeight;
            float s = Mathf.Clamp(targetHeightUnits / visibleHeight, minScale, maxScale);
            t.localScale = new Vector3(s, s, 1f);
        }
    }
}
