using System.Collections.Generic;
using UnityEngine;

namespace IsoCore.Foundation
{
    /// <summary>Loads quarantined interior prop textures as runtime sprites.</summary>
    public static class FoundationInteriorSpriteResolver
    {
        static readonly Dictionary<string, Sprite> _cache = new();

        public static Sprite TavernProp(int index, bool wallProp = false)
        {
            string key = wallProp
                ? $"Wall_props_table_{index}"
                : $"Tables_props_table_{index}";
            return Resolve("FoundationInteriors/Tavern/" + key, key);
        }

        public static Sprite LibraryProp(string key) =>
            string.IsNullOrWhiteSpace(key)
                ? null
                : Resolve("FoundationInteriors/Library/" + key, "library_" + key, 128f);

        public static Sprite DecorV2(string key) =>
            string.IsNullOrWhiteSpace(key)
                ? null
                : Resolve("FoundationInteriors/LitIsoDecorV2/" + key, "litiso_decor_v2_" + key);

        public static Sprite WallBlock() => Resolve("FoundationDungeon/Kenney/stoneWall_S", "tavern_wall_block");

        static Sprite Resolve(string path, string key, float pixelsPerUnit = 64f)
        {
            if (_cache.TryGetValue(key, out var cached))
                return cached;

            var sprite = Resources.Load<Sprite>(path);
            if (sprite == null)
            {
                var tex = Resources.Load<Texture2D>(path);
                if (tex != null)
                    sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height),
                        new Vector2(0.5f, 0.08f), pixelsPerUnit);
            }

            _cache[key] = sprite;
            return sprite;
        }

        public static void ClearCache() => _cache.Clear();
    }
}
