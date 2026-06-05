using System.Collections.Generic;
using UnityEngine;

namespace IsoCore.Foundation
{
    /// <summary>
    /// Generates crisp placeholder sprites at runtime (no pre-authored art needed).
    /// Diamonds for ground tiles, bottom-center boxes for props/mobs/player.
    /// All sprites use point filtering and are cached by (kind,color,size).
    /// </summary>
    public static class PlaceholderArt
    {
        const int PPU = 64; // 64 px == 1 world unit
        static readonly Dictionary<string, Sprite> Cache = new();

        static Sprite Build(string key, Texture2D tex, Vector2 pivot)
        {
            tex.filterMode = FilterMode.Point;
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.Apply();
            var sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), pivot, PPU);
            sprite.name = key;
            Cache[key] = sprite;
            return sprite;
        }

        static Texture2D NewTex(int w, int h)
        {
            var t = new Texture2D(w, h, TextureFormat.RGBA32, false);
            var clear = new Color32(0, 0, 0, 0);
            var px = new Color32[w * h];
            for (int i = 0; i < px.Length; i++) px[i] = clear;
            t.SetPixels32(px);
            return t;
        }

        /// <summary>A 2:1 iso diamond ground tile (pivot centered).</summary>
        public static Sprite Diamond(Color color)
        {
            string key = $"diamond:{ColorKey(color)}";
            if (Cache.TryGetValue(key, out var s)) return s;

            int w = 64, h = 32;
            var t = NewTex(w, h);
            var fill = (Color32)color;
            var edge = (Color32)(color * 0.7f);
            float cx = (w - 1) * 0.5f, cy = (h - 1) * 0.5f;
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                // |dx|/halfW + |dy|/halfH <= 1 describes the diamond.
                float dx = Mathf.Abs(x - cx) / (w * 0.5f);
                float dy = Mathf.Abs(y - cy) / (h * 0.5f);
                float d = dx + dy;
                if (d <= 1.0f)
                    t.SetPixel(x, y, d > 0.86f ? (Color)edge : (Color)fill);
            }
            return Build(key, t, new Vector2(0.5f, 0.5f));
        }

        /// <summary>
        /// An isometric block: top diamond + shaded left/right side faces whose depth
        /// = heightLevels * HeightStep. heightLevels 0 == a flat diamond (water/floor).
        /// Pivot is the TOP-face centre, so it positions at CellToWorld(cx,cy,height).
        /// </summary>
        public static Sprite Cube(Color color, int heightLevels)
        {
            heightLevels = Mathf.Clamp(heightLevels, 0, 7); // 7 == sort-order invariant ceiling (IsoGrid)
            string key = $"cube:{ColorKey(color)}:{heightLevels}";
            if (Cache.TryGetValue(key, out var s)) return s;

            int W = 64, TH = 32, step = 16;
            int skirt = heightLevels * step;
            int H = TH + skirt;
            var t = NewTex(W, H);

            float cx = (W - 1) * 0.5f;
            float cyd = skirt + (TH - 1) * 0.5f; // diamond centre (y up)
            Color top = color;
            Color topEdge = color * 0.80f; topEdge.a = 1f;
            Color leftCol = color * 0.62f; leftCol.a = 1f;  // lit side
            Color rightCol = color * 0.45f; rightCol.a = 1f; // shadow side

            for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                float nx = Mathf.Abs(x - cx) / (W * 0.5f);
                if (nx > 1f) continue;
                float ny = Mathf.Abs(y - cyd) / (TH * 0.5f);
                if (nx + ny <= 1f)
                {
                    t.SetPixel(x, y, (nx + ny) > 0.85f ? topEdge : top);
                }
                else if (y < cyd && skirt > 0)
                {
                    float dBot = cyd - (TH * 0.5f) * (1f - nx); // diamond lower edge at this column
                    if (y <= dBot && y >= dBot - skirt)
                        t.SetPixel(x, y, x < cx ? leftCol : rightCol);
                }
            }
            return Build(key, t, new Vector2(cx / W, cyd / H));
        }

        /// <summary>A bottom-center-anchored upright box for props/mobs/player.</summary>
        public static Sprite Box(Color color, float widthUnits = 0.7f, float heightUnits = 1.0f)
        {
            string key = $"box:{ColorKey(color)}:{widthUnits:0.00}:{heightUnits:0.00}";
            if (Cache.TryGetValue(key, out var s)) return s;

            int w = Mathf.Max(4, Mathf.RoundToInt(widthUnits * PPU));
            int h = Mathf.Max(4, Mathf.RoundToInt(heightUnits * PPU));
            var t = NewTex(w, h);
            var fill = (Color32)color;
            var edge = (Color32)(color * 0.6f);
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                bool border = x <= 1 || x >= w - 2 || y <= 1 || y >= h - 2;
                t.SetPixel(x, y, border ? (Color)edge : (Color)fill);
            }
            return Build(key, t, new Vector2(0.5f, 0.06f));
        }

        /// <summary>A bottom-center-anchored round blob (mobs).</summary>
        public static Sprite Blob(Color color, float diameterUnits = 0.6f)
        {
            string key = $"blob:{ColorKey(color)}:{diameterUnits:0.00}";
            if (Cache.TryGetValue(key, out var s)) return s;

            int d = Mathf.Max(4, Mathf.RoundToInt(diameterUnits * PPU));
            var t = NewTex(d, d);
            var fill = (Color32)color;
            var edge = (Color32)(color * 0.65f);
            float c = (d - 1) * 0.5f, r = d * 0.5f;
            for (int y = 0; y < d; y++)
            for (int x = 0; x < d; x++)
            {
                float dist = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c));
                if (dist <= r) t.SetPixel(x, y, dist > r - 1.5f ? (Color)edge : (Color)fill);
            }
            return Build(key, t, new Vector2(0.5f, 0.1f));
        }

        static string ColorKey(Color c) =>
            $"{Mathf.RoundToInt(c.r * 255)}-{Mathf.RoundToInt(c.g * 255)}-{Mathf.RoundToInt(c.b * 255)}-{Mathf.RoundToInt(c.a * 255)}";
    }
}
