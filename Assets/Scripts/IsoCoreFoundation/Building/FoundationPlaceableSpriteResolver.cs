using System.Collections.Generic;
using UnityEngine;

namespace IsoCore.Foundation
{
    /// <summary>Runtime art hook for placeables, with sprite or texture fallbacks.</summary>
    public static class FoundationPlaceableSpriteResolver
    {
        static readonly Dictionary<string, Sprite> _cache = new();
        static Sprite[] _campfireFrames;

        public static Sprite Resolve(string placeableId)
        {
            if (string.IsNullOrWhiteSpace(placeableId))
                return null;

            if (_cache.TryGetValue(placeableId, out var cached))
                return cached;

            Sprite sprite = DecorationSpriteResolver.Resolve(placeableId);
            if (sprite == null)
                sprite = LoadSprite("FoundationBuildings/" + placeableId, 32f);

            if (sprite == null && IsFirePlaceable(placeableId))
            {
                var frames = CampfireFrames();
                if (frames != null && frames.Length > 0)
                    sprite = frames[0];
            }

            if (sprite == null && IsFirePlaceable(placeableId))
            {
                sprite = LoadSprite("FoundationCampfire/upixelator_campfire", 32f);
                if (sprite == null)
                    sprite = LoadSprite("FoundationCampfire/campfire", 32f);
            }

            _cache[placeableId] = sprite;
            return sprite;
        }

        static Sprite LoadSprite(string path, float pixelsPerUnit)
        {
            var sprite = Resources.Load<Sprite>(path);
            if (sprite != null)
                return sprite;

            var tex = Resources.Load<Texture2D>(path);
            return tex != null
                ? Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height),
                    new Vector2(0.5f, 0.1f), pixelsPerUnit)
                : null;
        }

        public static Sprite[] CampfireFrames()
        {
            if (_campfireFrames != null)
                return _campfireFrames;

            var sliced = Resources.LoadAll<Sprite>("FoundationCampfire/campfire-Sheet");
            if (sliced != null && sliced.Length > 0)
            {
                System.Array.Sort(sliced, (a, b) => string.CompareOrdinal(a.name, b.name));
                _campfireFrames = sliced;
                return _campfireFrames;
            }

            var tex = Resources.Load<Texture2D>("FoundationCampfire/campfire-Sheet");
            if (tex == null)
            {
                _campfireFrames = System.Array.Empty<Sprite>();
                return _campfireFrames;
            }

            int frameCount = tex.width % 6 == 0 ? 6 : Mathf.Max(1, tex.width / Mathf.Max(1, tex.height));
            int frameW = tex.width / frameCount;
            int frameH = tex.height;
            var frames = new Sprite[frameCount];
            for (int i = 0; i < frameCount; i++)
            {
                frames[i] = Sprite.Create(tex, new Rect(i * frameW, 0, frameW, frameH),
                    new Vector2(0.5f, 0.10f), 64f);
                frames[i].name = $"campfire_{i}";
            }
            _campfireFrames = frames;
            return _campfireFrames;
        }

        public static void ClearCache()
        {
            _cache.Clear();
            _campfireFrames = null;
        }

        static bool IsFirePlaceable(string placeableId) =>
            placeableId == "campfire" || placeableId == "fireplace";
    }
}
