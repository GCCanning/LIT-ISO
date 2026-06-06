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

        // Per-surface-sprite cache of a copy that has a very light border baked ONTO the
        // tile's top-face edge. Because the border is part of the tile texture (not a
        // separate overlay above it), each tile carries its own faint outline that hugs
        // the texture exactly and can never float above the ground.
        readonly Dictionary<Sprite, Sprite> _bordered = new(); // full cube (box) + border
        // Flat, top-face-only copies (cube side walls removed) used for floor (height 0) cells.
        readonly Dictionary<Sprite, Sprite> _flat = new();

        // Border tint + blend strength.Light and low so it reads as a gentle cell edge
        // that is clearly part of the tile rather than a grid laid over the world.
        static readonly Color BorderColor = Color.white;
        const float BorderStrength = 0.18f;

        // Ground renders on its own sorting layer (behind "Default" where props/actors
        // live), so it is always beneath everything else. Within the layer, the block
        // tiles are drawn NEAR-on-top (painter's order): a cell closer to the viewer
        // (smaller cx+cy, lower on screen) draws over the cells behind it, so each tile's
        // grass top covers the cube sides of the tile behind — giving a flush surface.
        const string GroundSortingLayer = "Ground";

        // Sub-surface body sprite for raised columns. The surface (top) cell keeps its
        // own block art (grass etc.); every level beneath it is drawn with this dirt
        // sprite so a hill reads as a grass cap over a solid earth body, instead of the
        // surface's grass top peeking out at every level. Resolved once, lazily.
        Sprite _subsurfaceSprite;
        bool _subsurfaceResolved;

        Sprite SubsurfaceSprite()
        {
            if (_subsurfaceResolved) return _subsurfaceSprite;
            var dirt = _content.Blocks.Get("dirt");
            _subsurfaceSprite = dirt != null ? TileSpriteResolver.Resolve(dirt) : null;
            _subsurfaceResolved = true;
            return _subsurfaceSprite;
        }

        // sub: 0 = stacked sub-levels, 1 = surface tile, 2 = surface outline.
        static int GroundOrder(int cx, int cy, int height, int sub) =>
            -(cx + cy) * IsoGrid.DepthScale + height * IsoGrid.HeightScale + sub;

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
            var newSr = go.AddComponent<SpriteRenderer>();
            newSr.sharedMaterial = SpriteAmbient.Material; // day/night world tint
            return newSr;
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
                    // Only configure cells that just became visible. Already-active cells are
                    // unchanged by a camera move, so re-Configuring the whole view every
                    // chunk crossing was pure waste (the streaming hitch). Per-cell data
                    // edits (placement, harvest) refresh through RefreshCell/OnCellChanged.
                    sr = Rent();
                    _active[k] = sr;
                    Configure(sr, world, c.x, c.y);
                }
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

        /// <summary>True if this cell currently has an active (rendered) ground tile.</summary>
        public bool IsShown(int wx, int wy) => _active.ContainsKey(Key(wx, wy));

        /// <summary>Shows one cell if not already active. Returns true if it was newly shown.
        /// Used by frame-amortized streaming to bound per-frame work.</summary>
        public bool ShowCell(IsoWorld world, int wx, int wy)
        {
            long k = Key(wx, wy);
            if (_active.ContainsKey(k)) return false;
            var sr = Rent();
            _active[k] = sr;
            Configure(sr, world, wx, wy);
            return true;
        }

        /// <summary>Recycles every active cell whose key is not in <paramref name="desired"/>.
        /// Hiding is cheap (SetActive(false)) so it is done in one pass.</summary>
        public void RetainOnly(HashSet<long> desired)
        {
            _removeBuffer.Clear();
            foreach (var kv in _active)
                if (!desired.Contains(kv.Key)) _removeBuffer.Add(kv.Key);
            foreach (var k in _removeBuffer)
            {
                Recycle(_active[k]);
                _active.Remove(k);
            }
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
            if (surfaceSprite != null)
            {
                // Floor cells (height 0 / water) use a FLAT top-face-only diamond so the
                // ground reads perfectly flat with no fake cube-side height. Raised cells
                // keep the full cube (with baked border) and stack a dirt body beneath.
                bool raised = !cell.Water && cell.Height > 0;
                sr.sprite = raised ? Bordered(surfaceSprite) : FlatTile(surfaceSprite);
            }
            else
            {
                sr.sprite = PlaceholderArt.Cube(col, levels);
            }
            sr.transform.position = IsoGrid.CellToWorld(wx, wy, cell.Height);
            sr.sortingLayerName = GroundSortingLayer;
            sr.sortingOrder = GroundOrder(wx, wy, cell.Height, 1);

            // Stacked tiles for height > 0 — only when using real tile sprites.
            // (PlaceholderArt.Cube already paints side faces in-sprite, so stacking
            // there would double up.) Each level below the surface gets its own
            // SpriteRenderer drawing the same surface sprite, positioned one cell
            // lower. Adjacent cells at the same height naturally tessellate at every
            // level because each level's tile sits at IsoGrid.CellToWorld(wx, wy, h).
            if (surfaceSprite != null && !cell.Water && cell.Height > 0)
                EnsureStack(sr, world, wx, wy, cell.Height, SubsurfaceSprite() ?? surfaceSprite);
            else
                HideStack(sr);
        }

        // Returns a copy of the surface tile sprite with a very light border blended onto
        // the top-face diamond edge. The border is baked into the texture itself, so it
        // sits on the tile art (never above it) and hugs the exact tile edge. Cached per
        // source sprite (a handful of tile types) so the bake runs once each.
        Sprite Bordered(Sprite src)
        {
            if (_bordered.TryGetValue(src, out var cached) && cached != null) return cached;

            var rect = src.textureRect;
            int w = (int)rect.width, h = (int)rect.height;
            Color[] pixels;
            try { pixels = src.texture.GetPixels((int)rect.x, (int)rect.y, w, h); }
            catch { _bordered[src] = src; return src; } // texture not readable -> use as-is

            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };
            tex.SetPixels(pixels);

            // Top-face diamond edge of the 32px block tiles.
            BlendBorderLine(tex, new Vector2Int(15, 27), new Vector2Int(31, 16));
            BlendBorderLine(tex, new Vector2Int(31, 16), new Vector2Int(15, 5));
            BlendBorderLine(tex, new Vector2Int(15, 5), new Vector2Int(0, 16));
            BlendBorderLine(tex, new Vector2Int(0, 16), new Vector2Int(15, 27));
            tex.Apply();

            var ns = Sprite.Create(tex, new Rect(0, 0, w, h),
                new Vector2(src.pivot.x / w, src.pivot.y / h), src.pixelsPerUnit);
            ns.name = src.name + "_bordered";
            _bordered[src] = ns;
            return ns;
        }

        // Returns a FLAT, top-face-only copy of the tile: the cube side walls are cleared so
        // only the diamond top remains, with the light border baked on the diamond edge.
        // Used for floor (height 0) cells so the ground reads flat. Cached per source sprite.
        Sprite FlatTile(Sprite src)
        {
            if (_flat.TryGetValue(src, out var cached) && cached != null) return cached;

            var rect = src.textureRect;
            int w = (int)rect.width, h = (int)rect.height;
            Color[] pixels;
            try { pixels = src.texture.GetPixels((int)rect.x, (int)rect.y, w, h); }
            catch { _flat[src] = src; return src; }

            // Keep only pixels inside the top-face diamond (rhombus); clear the side walls.
            // Diamond centre ≈ (15.5, 16), half-width 16, half-height 11 (top vertex at y=27).
            const float cx = 15.5f, cy = 16f, hw = 16f, hh = 11f;
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float nx = Mathf.Abs(x - cx) / hw;
                float ny = Mathf.Abs(y - cy) / hh;
                if (nx + ny > 1.0f) pixels[y * w + x] = new Color(0, 0, 0, 0);
            }

            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
            { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            tex.SetPixels(pixels);
            BlendBorderLine(tex, new Vector2Int(15, 27), new Vector2Int(31, 16));
            BlendBorderLine(tex, new Vector2Int(31, 16), new Vector2Int(15, 5));
            BlendBorderLine(tex, new Vector2Int(15, 5), new Vector2Int(0, 16));
            BlendBorderLine(tex, new Vector2Int(0, 16), new Vector2Int(15, 27));
            tex.Apply();

            var ns = Sprite.Create(tex, new Rect(0, 0, w, h),
                new Vector2(src.pivot.x / w, src.pivot.y / h), src.pixelsPerUnit);
            ns.name = src.name + "_flat";
            _flat[src] = ns;
            return ns;
        }

        // Lightens the existing (opaque) tile pixels along a line — so the border hugs the
        // tile art and never paints onto transparent areas outside the top face.
        static void BlendBorderLine(Texture2D tex, Vector2Int a, Vector2Int b)
        {
            int x0 = a.x, y0 = a.y, x1 = b.x, y1 = b.y;
            int dx = Mathf.Abs(x1 - x0), dy = Mathf.Abs(y1 - y0);
            int sx = x0 < x1 ? 1 : -1, sy = y0 < y1 ? 1 : -1;
            int err = dx - dy;
            while (true)
            {
                if (x0 >= 0 && x0 < tex.width && y0 >= 0 && y0 < tex.height)
                {
                    var c = tex.GetPixel(x0, y0);
                    if (c.a > 0.4f)
                    {
                        c = Color.Lerp(c, BorderColor, BorderStrength);
                        c.a = 1f;
                        tex.SetPixel(x0, y0, c);
                    }
                }
                if (x0 == x1 && y0 == y1) break;
                int e2 = 2 * err;
                if (e2 > -dy) { err -= dy; x0 += sx; }
                if (e2 < dx) { err += dx; y0 += sy; }
            }
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
                var stackSr = go.AddComponent<SpriteRenderer>();
                stackSr.sharedMaterial = SpriteAmbient.Material;
                stack.Add(stackSr);
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
                    child.sortingLayerName = GroundSortingLayer;
                    child.sortingOrder = GroundOrder(wx, wy, i, 0);
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
