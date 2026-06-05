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
            sr.sprite = PlaceholderArt.Cube(col, levels);
            sr.transform.position = IsoGrid.CellToWorld(wx, wy, cell.Height);
            sr.sortingOrder = IsoGrid.SortingOrder(wx, wy, cell.Height, IsoGrid.LayerGround);
        }
    }
}
