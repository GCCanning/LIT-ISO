using System.Collections.Generic;
using UnityEngine;

namespace IsoCore.Foundation
{
    /// <summary>
    /// Resolves a resource-node sprite from Resources/Decorations/&lt;nodeId&gt;.png.
    /// Caches results (including misses) so repeated lookups are cheap. Returns null
    /// when nothing is present — caller (ResourceNode) falls back to PlaceholderArt.Box.
    ///
    /// Drop a sprite named exactly after a node id (e.g. "tree.png", "bush.png",
    /// "rock.png") into Assets/Resources/Decorations/ and it replaces the placeholder
    /// box the next time that node spawns. No SO edits required. Mirrors
    /// TileSpriteResolver so ground and props follow the same art-injection pattern.
    /// </summary>
    public static class DecorationSpriteResolver
    {
        static readonly Dictionary<string, Sprite> _cache = new();

        public static Sprite Resolve(string nodeId)
        {
            if (string.IsNullOrEmpty(nodeId)) return null;
            if (_cache.TryGetValue(nodeId, out var sprite)) return sprite;
            sprite = Resources.Load<Sprite>("Decorations/" + nodeId);
            _cache[nodeId] = sprite; // cache nulls too
            return sprite;
        }

        /// <summary>Clears the cache; useful after editor hot reload of new art.</summary>
        public static void ClearCache() => _cache.Clear();
    }
}
