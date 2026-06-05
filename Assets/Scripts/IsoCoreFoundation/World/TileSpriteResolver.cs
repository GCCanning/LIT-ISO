using System.Collections.Generic;
using UnityEngine;

namespace IsoCore.Foundation
{
    /// <summary>
    /// Resolves a per-block tile sprite from Resources/Tiles/&lt;blockId&gt;.png, with
    /// per-block-group fallback to Resources/Tiles/&lt;groupId&gt;.png. Caches results
    /// (including misses) so repeated lookups are cheap. Returns null when nothing
    /// is present — caller (IsoWorldRenderer) falls back to PlaceholderArt.Cube.
    ///
    /// Drop a sprite named exactly after a block id (e.g. "grass_1.png") into
    /// Assets/Resources/Tiles/ and it appears the next time that block renders.
    /// No SO edits required.
    ///
    /// Authored as a minimally invasive injection point so the placeholder cubes
    /// can be replaced one block at a time. Companion: a one-line edit in
    /// IsoWorldRenderer.Configure() calls Resolve(block) before PlaceholderArt.
    /// </summary>
    public static class TileSpriteResolver
    {
        static readonly Dictionary<string, Sprite> _cache = new();

        /// <summary>
        /// Returns a sprite for this block, or null to fall back to PlaceholderArt.
        /// Lookup order: blockId → groupId → null.
        /// </summary>
        public static Sprite Resolve(BlockDefinition block)
        {
            if (block == null) return null;

            // 1. By specific block id (e.g. "grass_2", "stone_block", "water").
            if (!string.IsNullOrEmpty(block.id) && TryLoad(block.id, out var s)) return s;

            // 2. By group id (e.g. "grass_blocks") so multiple variants can share one art.
            if (!string.IsNullOrEmpty(block.groupId) && TryLoad(block.groupId, out s)) return s;

            return null;
        }

        /// <summary>Clears the cache; useful after editor hot reload of new tiles.</summary>
        public static void ClearCache() => _cache.Clear();

        static bool TryLoad(string key, out Sprite sprite)
        {
            if (_cache.TryGetValue(key, out sprite)) return sprite != null;
            sprite = Resources.Load<Sprite>("Tiles/" + key);
            _cache[key] = sprite; // cache nulls too
            return sprite != null;
        }
    }
}
