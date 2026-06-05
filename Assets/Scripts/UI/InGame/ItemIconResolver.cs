using System.Collections.Generic;
using UnityEngine;
using IsoCore.Foundation;

namespace LitIso.UI.InGame
{
    /// <summary>
    /// Resolves an item id → Sprite for HUD/inventory/crafting views.
    ///
    /// Order of resolution:
    ///   1. Codex's content database: `content.Items.Get(itemId)?.Icon`
    ///      (populated when ItemDefinition assets have their icon field assigned).
    ///   2. Fallback: `Resources.Load&lt;Sprite&gt;("Items/" + itemId)`.
    ///      Drop pixel-art icons named exactly &lt;itemId&gt;.png into Assets/Resources/Items/
    ///      and they appear in the UI with no further wiring.
    ///   3. null → the view shows its empty-slot placeholder.
    ///
    /// Results are cached so the lookup happens once per id. Cache clears on
    /// FoundationBootstrap.Ready so reloaded scenes get fresh sprites.
    /// </summary>
    public static class ItemIconResolver
    {
        static readonly Dictionary<string, Sprite> _cache = new();
        static FoundationContent _content;

        public static void Bind(FoundationContent content)
        {
            _content = content;
            _cache.Clear();
        }

        public static Sprite Resolve(string itemId)
        {
            if (string.IsNullOrEmpty(itemId)) return null;
            if (_cache.TryGetValue(itemId, out var s)) return s;

            Sprite sprite = null;
            if (_content != null)
            {
                var def = _content.Items.Get(itemId);
                if (def != null) sprite = def.Icon;
            }
            if (sprite == null) sprite = Resources.Load<Sprite>("Items/" + itemId);

            _cache[itemId] = sprite; // cache nulls too (avoids repeat lookups for unknown ids)
            return sprite;
        }
    }
}
