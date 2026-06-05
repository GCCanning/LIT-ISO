using System.Collections.Generic;
using UnityEngine;

namespace IsoCore.Foundation
{
    /// <summary>
    /// Renders ground cells as pooled SpriteRenderers with a single deterministic
    /// iso sort (IsoGrid.SortingOrder). Reads cell data only; never writes it.
    /// Placeables / nodes / mobs / player render via their own objects.
    /// </summary>
    public class IsoWorldRenderer
    {
        readonly Transform _parent;
        readonly FoundationContent _content;
        readonly Dictionary<long, SpriteRenderer> _active = new();
        readonly Stack<SpriteRenderer> _pool = new();
        readonly List<long> _removeBuffer = new();
        // Per-ground-tile stack of child SpriteRenderers (one per height level below the
        // surface). Filled lazily when stacking is enabled; reused across pool rents.
        readonly Dictionary<SpriteRenderer, List<SpriteRenderer>> _stacks = new();

        public IsoWorldRenderer(Transform parent, FoundationContent content)
        {
            _parent = parent; _content = content;
        }

        static long Key(int wx, int wy) => ((long)(uint)wx << 32) | (uint)wy;

        SpriteRenderer Rent()
        {
            if (_pool.Count > 0)
            {
                var pooled = _pool.Pop();
                pooled.gameObject.SetActive(true);
                return pooled;
            }
            var go = new GameObject("GroundTile");
            go.transform.SetParent(_parent, false);
            return go.AddComponent<SpriteRenderer>();
        }

        void Recycle(SpriteRenderer sr)
        {
            // Hide stack children too — they live as children of the pooled GameObject
            // and would otherwise still render after recycling. (Re-shown by EnsureStack.)
            if (_stacks.TryGetValue(sr, out var stack))
                foreach (var child in stack) if (child != null) child.gameObject.SetActive(false);
            sr.gameObject.SetActive(false);
            _pool.Push(sr);
        }

        public void SetVisible(IsoWorld world, List<Vector2Int> cells)
        {
            var desired = new HashSet<long>(cells.Count);
            foreach (var c in cells)
            {
                long k = Key(c.x, c.y);
                desired.Add(k);
                if (!_active.TryGetValue(k, out var sr))
                {
                    sr = Rent();
                    _active[k] = sr;
                }
                Configure(sr, world, c.x, c.y);
            }

            _removeBuffer.Clear();
            foreach (var kv in _active)
                if (!desired.Contains(kv.Key)) _removeBuffer.Add(kv.Key);
            foreach (var k in _removeBuffer)
            {
                Recycle(_active[k]);
                _active.Remove(k);
            }
        }

        public void RefreshCell(IsoWorld world, int wx, int wy)
        {
            if (_active.TryGetValue(Key(wx, wy), out var sr))
                Configure(sr, world, wx, wy);
        }

        void Configure(SpriteRenderer sr, IsoWorld world, int wx, int wy)
        {
            var cell = world.GetCell(wx, wy);
            var block = _content.Blocks.Get(cell.SurfaceBlockId);
            Color col = block != null ? block.color : Color.magenta;
            // Iso cube: side faces visualize the height column (water/flat -> 0 levels).
            int levels = cell.Water ? 0 : cell.Height;
            // Try a per-block tile sprite from Resources/Tiles/<blockId>.png first;
            // fall back to the procedural placeholder cube if no art is present.
            // Real pixel-art tiles already carry their own colour; PlaceholderArt.Cube
            // bakes 'col' into its texture, so sr.color stays white either way.
            var surfaceSprite = TileSpriteResolver.Resolve(block);
            sr.sprite = surfaceSprite ?? PlaceholderArt.Cube(col, levels);
            sr.transform.position = IsoGrid.CellToWorld(wx, wy, cell.Height);
            sr.sortingOrder = IsoGrid.SortingOrder(wx, wy, cell.Height, IsoGrid.LayerGround);

            // Stacked tiles for height > 0 — only when using real tile sprites.
            // (PlaceholderArt.Cube already paints side faces in-sprite, so stacking
            // there would double up.) Each level below the surface gets its own
            // SpriteRenderer drawing the same surface sprite, positioned one cell
            // lower. Adjacent cells at the same height naturally tessellate at every
            // level because each level's tile sits at IsoGrid.CellToWorld(wx, wy, h).
            if (surfaceSprite != null && !cell.Water && cell.Height > 0)
                EnsureStack(sr, world, wx, wy, cell.Height, surfaceSprite);
            else
                HideStack(sr);
        }

        void EnsureStack(SpriteRenderer sr, IsoWorld world, int wx, int wy, int height, Sprite sprite)
        {
            if (!_stacks.TryGetValue(sr, out var stack))
            {
                stack = new List<SpriteRenderer>();
                _stacks[sr] = stack;
            }

            // Grow the children list to cover every level below the surface.
            while (stack.Count < height)
            {
                var go = new GameObject("StackTile");
                go.transform.SetParent(sr.transform.parent, false); // sibling of the surface SR (same flat parent)
                stack.Add(go.AddComponent<SpriteRenderer>());
            }

            // Configure the active levels, hide the rest.
            for (int i = 0; i < stack.Count; i++)
            {
                var child = stack[i];
                if (i < height)
                {
                    child.gameObject.SetActive(true);
                    child.sprite = sprite;
                    child.transform.position = IsoGrid.CellToWorld(wx, wy, i);
                    child.sortingOrder = IsoGrid.SortingOrder(wx, wy, i, IsoGrid.LayerGround);
                }
                else child.gameObject.SetActive(false);
            }
        }

        void HideStack(SpriteRenderer sr)
        {
            if (!_stacks.TryGetValue(sr, out var stack)) return;
            foreach (var child in stack)
                if (child != null) child.gameObject.SetActive(false);
        }
    }
}
