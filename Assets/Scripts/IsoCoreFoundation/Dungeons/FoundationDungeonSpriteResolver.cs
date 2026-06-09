using System.Collections.Generic;
using UnityEngine;

namespace IsoCore.Foundation
{
    public static class FoundationDungeonSpriteResolver
    {
        static readonly Dictionary<string, Sprite> Cache = new();
        static readonly Dictionary<string, Sprite[]> FrameCache = new();

        public static Sprite Portal()
        {
            var frames = PortalFrames();
            if (frames.Length > 0 && frames[0] != null)
                return frames[0];

            return LoadTextureSprite("FoundationPortals/Isometric_Portal", 64f, new Vector2(0.5f, 0.03f));
        }

        public static Sprite[] PortalFrames()
        {
            var frames = LoadTextureGrid("FoundationPortals/Dimensional_Portal", 3, 2, 24f,
                new Vector2(0.5f, 0.08f));
            if (frames.Length > 0)
                return frames;

            var fallback = LoadTextureSprite("FoundationPortals/Isometric_Portal", 64f, new Vector2(0.5f, 0.03f));
            return fallback != null ? new[] { fallback } : System.Array.Empty<Sprite>();
        }

        public static Sprite Decoration(string key)
        {
            if (string.IsNullOrWhiteSpace(key)) return null;
            return LoadTextureSprite($"FoundationDungeon/Kenney/{key}", 100f, new Vector2(0.5f, 0.08f));
        }

        static Sprite LoadTextureSprite(string resourcePath, float pixelsPerUnit, Vector2 pivot)
        {
            if (Cache.TryGetValue(resourcePath, out var cached))
                return cached;

            var sprite = Resources.Load<Sprite>(resourcePath);
            if (sprite == null)
            {
                var tex = Resources.Load<Texture2D>(resourcePath);
                if (tex != null)
                {
                    sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), pivot, pixelsPerUnit);
                    sprite.name = tex.name;
                }
            }

            Cache[resourcePath] = sprite;
            return sprite;
        }

        static Sprite[] LoadTextureGrid(string resourcePath, int columns, int rows, float pixelsPerUnit, Vector2 pivot)
        {
            if (FrameCache.TryGetValue(resourcePath, out var cached))
                return cached;

            var tex = Resources.Load<Texture2D>(resourcePath);
            if (tex == null || columns <= 0 || rows <= 0)
            {
                FrameCache[resourcePath] = System.Array.Empty<Sprite>();
                return FrameCache[resourcePath];
            }

            int frameW = tex.width / columns;
            int frameH = tex.height / rows;
            if (frameW <= 0 || frameH <= 0)
            {
                FrameCache[resourcePath] = System.Array.Empty<Sprite>();
                return FrameCache[resourcePath];
            }

            var frames = new List<Sprite>(columns * rows);
            int index = 0;
            for (int row = rows - 1; row >= 0; row--)
            for (int col = 0; col < columns; col++)
            {
                var rect = new Rect(col * frameW, row * frameH, frameW, frameH);
                var sprite = Sprite.Create(tex, rect, pivot, pixelsPerUnit);
                sprite.name = $"{tex.name}_{index++}";
                frames.Add(sprite);
            }

            FrameCache[resourcePath] = frames.ToArray();
            return FrameCache[resourcePath];
        }
    }
}
