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
            var dirt = _content.Blocks.Get("pl_dirt_02") ??
                _content.Blocks.Get("pl_dirt_01") ??
                _content.Blocks.Get("dirt");
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

        // perf (audit 2026-06-11): reused across Retargets instead of allocating
        // a fresh ~7k-entry set on every chunk-boundary crossing
        static readonly HashSet<long> s_desired = new HashSet<long>();

        public void SetVisible(IsoWorld world, List<Vector2Int> cells)
        {
            var desired = s_desired;
            desired.Clear();
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

        // Tracks block ids already warned about (missing tile sprite) so the log fires once
        // per id, not per cell. Surfaces stray placeholder tiles instead of failing silently.
        static readonly System.Collections.Generic.HashSet<string> _warnedMissingSprite = new();

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
            bool authoredFootprint = TileSpriteResolver.UsesAuthoredFootprint(block);
            if (surfaceSprite != null)
            {
                // Floor cells (height 0 / water) use a FLAT top-face-only diamond so the
                // ground reads perfectly flat with no fake cube-side height. Raised cells
                // keep the full cube (with baked border) and stack a dirt body beneath.
                bool raised = !cell.Water && cell.Height > 0;
                var shown = authoredFootprint
                    ? surfaceSprite
                    : raised ? Bordered(surfaceSprite) : FlatTile(surfaceSprite);
                // Snow-line accumulation by height band (playtest #8): tops at h5+ get
                // real snow pixels baked onto the diamond face, so high plateaus read
                // as altitude at a glance instead of a flat checkerboard.
                if (!authoredFootprint && !cell.Water && cell.Height >= SnowLineHeight)
                    shown = SnowCapped(shown, cell.Height);
                sr.sprite = shown;
            }
            else
            {
                if (!string.IsNullOrEmpty(cell.SurfaceBlockId) && _warnedMissingSprite.Add(cell.SurfaceBlockId))
                    Debug.LogWarning($"[WorldRenderer] No tile sprite for block '{cell.SurfaceBlockId}' " +
                        $"(expected Resources/Tiles/{cell.SurfaceBlockId}.png) — rendering a placeholder cube. " +
                        "This is the source of any stray placeholder tiles.");
                sr.sprite = PlaceholderArt.Cube(col, levels);
            }
            sr.transform.position = IsoGrid.CellToWorld(wx, wy, cell.Height);
            sr.sortingLayerName = GroundSortingLayer;
            sr.sortingOrder = GroundOrder(wx, wy, cell.Height, 1);

            // Water cells swap to the shared shimmer/foam material; foam flags mark which
            // diamond edges touch land (playtest #8). Pooled renderers must reset both the
            // material and the property block when they cycle back to ground.
            if (cell.Water)
            {
                sr.sharedMaterial = SpriteAmbient.WaterMaterial;
                s_mpb.Clear();
                s_mpb.SetFloat(FoamNEId, world.GetCell(wx + 1, wy).Water ? 0f : 1f);
                s_mpb.SetFloat(FoamNWId, world.GetCell(wx, wy + 1).Water ? 0f : 1f);
                s_mpb.SetFloat(FoamSWId, world.GetCell(wx - 1, wy).Water ? 0f : 1f);
                s_mpb.SetFloat(FoamSEId, world.GetCell(wx, wy - 1).Water ? 0f : 1f);
                sr.SetPropertyBlock(s_mpb);
            }
            else
            {
                sr.sharedMaterial = SpriteAmbient.Material;
                sr.SetPropertyBlock(null);
            }

            // Stacked tiles for height > 0 — only when using real tile sprites.
            // (PlaceholderArt.Cube already paints side faces in-sprite, so stacking
            // there would double up.) Each level below the surface gets its own
            // SpriteRenderer drawing the same surface sprite, positioned one cell
            // lower. Adjacent cells at the same height naturally tessellate at every
            // level because each level's tile sits at IsoGrid.CellToWorld(wx, wy, h).
            if (surfaceSprite != null && !authoredFootprint && !cell.Water && cell.Height > 0)
                EnsureStack(sr, world, wx, wy, cell.Height, SubsurfaceSprite() ?? surfaceSprite);
            else
                HideStack(sr);

            // Depth/mood tint (Impact Analysis B2b #2): lower-height cells read as
            // slightly darker/cooler "depth fog", taller cells stay neutral. Multiplies
            // on top of the global day/night ambient (a separate shader uniform), so the
            // two combine without fighting. Subtle by design — never crushes the art.
            // Then an ambient cliff shadow where a taller cell looms behind this one — the
            // depth cue that makes elevation drops legible under the orthographic camera.
            var depth = DepthTint(cell.Height);
            float cliff = CliffShade(world, wx, wy, cell.Height);
            sr.color = new Color(depth.r * cliff, depth.g * cliff, depth.b * cliff, 1f);
        }

        // Ambient cliff shadow: a cell at the foot of a taller cell behind/above it
        // (toward screen-back, +x / +y) is darkened in proportion to the height drop, so
        // a cliff visibly "casts" onto the ground at its base. With an orthographic camera
        // there's no perspective to sell elevation, so this is the cue that makes drops
        // readable (the problem the ISO-CORE devlog flagged as unsolved). Pure colour
        // multiply — no sorting/geometry change. Returns 1.0 (no shade) on flat ground.
        const float CliffShadePerLevel = 0.16f; // darkening added per level of drop
        const float CliffShadeMax = 0.50f;       // clamp so a base never goes pitch black
        // Vertical cliff/side faces (the stacked sub-surface body beneath a raised top) are
        // drawn darker than the tops, so every hill/cliff reads as a 3D volume regardless of
        // where the camera sits — the strongest, always-visible elevation cue.
        // 0.70 -> 0.62 (playtest #8): stronger wall/floor contrast so cliff faces pop from
        // the tops, especially on the h5-7 plateaus. (True per-face key lighting from
        // DayNightSystem.LightFromDir would need the two visible faces split into separate
        // sprites or a shader-side face mask — deferred; tiles configure once, not per frame.)
        const float SideFaceShade = 0.62f;

        static float CliffShade(IsoWorld world, int wx, int wy, int height)
        {
            int back = Mathf.Max(world.GetCell(wx + 1, wy).Height, world.GetCell(wx, wy + 1).Height);
            int delta = back - height;
            if (delta <= 0) return 1f;
            return 1f - Mathf.Min(delta * CliffShadePerLevel, CliffShadeMax);
        }

        // Altitude tint ramp (playtest 2026-07-02 #8). The old ramp went neutral at
        // height 3+, so the h5-7 plateau read as a flat checkerboard — no altitude cue.
        // Now EVERY height band gets a distinct value: reference is the max height (7);
        // each level below it is slightly darker and cooler ("depth fog" in the lows,
        // full brightness at the peaks). Cheap per-tile Color multiply — no shader work.
        const int AltitudeReferenceHeight = 7;
        const float AltitudePerLevel = 0.035f;  // brightness lost per level below the peak
        const float AltitudeMaxStrength = 0.25f; // clamp so low ground never goes too dark
        static readonly Color DepthTintCool = new Color(0.92f, 0.96f, 1.05f); // slight blue lean

        static Color DepthTint(int height)
        {
            int below = AltitudeReferenceHeight - Mathf.Clamp(height, 0, AltitudeReferenceHeight);
            if (below <= 0) return Color.white;

            float t = Mathf.Min(below * AltitudePerLevel, AltitudeMaxStrength);
            float shade = 1f - t;
            return new Color(shade * DepthTintCool.r, shade * DepthTintCool.g, shade * DepthTintCool.b, 1f);
        }

        // ---- snow-line accumulation (playtest #8) ----
        // Tops at h5+ get snow pixels baked into a cached sprite variant (multiply tints
        // can only darken, so real lightening has to be baked). Strength grows per band.
        const int SnowLineHeight = 5;
        static readonly Color SnowColor = new Color(0.88f, 0.93f, 1.00f);
        readonly Dictionary<(Sprite, int), Sprite> _snowCapped = new();

        // Water foam MPB plumbing (shared block, cleared per use — no allocation).
        static readonly MaterialPropertyBlock s_mpb = new MaterialPropertyBlock();
        static readonly int FoamNEId = Shader.PropertyToID("_FoamNE");
        static readonly int FoamNWId = Shader.PropertyToID("_FoamNW");
        static readonly int FoamSWId = Shader.PropertyToID("_FoamSW");
        static readonly int FoamSEId = Shader.PropertyToID("_FoamSE");

        Sprite SnowCapped(Sprite src, int height)
        {
            int band = Mathf.Clamp(height, SnowLineHeight, 7);
            if (_snowCapped.TryGetValue((src, band), out var cached) && cached != null) return cached;

            var rect = src.textureRect;
            int w = (int)rect.width, h = (int)rect.height;
            Color[] pixels;
            try { pixels = src.texture.GetPixels((int)rect.x, (int)rect.y, w, h); }
            catch { _snowCapped[(src, band)] = src; return src; }

            // Snow only on the TOP-FACE diamond (same geometry as FlatTile); the cube
            // side walls stay earthen so cliffs keep their contrast. Coverage grows with
            // the band: h5 dusting -> h7 deep cap.
            float strength = 0.30f + 0.20f * (band - SnowLineHeight); // 0.30 / 0.50 / 0.70
            const float cx = 15.5f, cy = 16f, hw = 16f, hh = 11f;
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float nx = Mathf.Abs(x - cx) / hw;
                float ny = Mathf.Abs(y - cy) / hh;
                if (nx + ny > 1.0f) continue;
                int idx = y * w + x;
                var c = pixels[idx];
                if (c.a <= 0.4f) continue;
                // Deterministic per-pixel dither so the snow edge looks organic, not flat.
                float n = Mathf.PerlinNoise(x * 0.55f + band * 7.3f, y * 0.55f);
                float k = Mathf.Clamp01(strength + (n - 0.5f) * 0.35f);
                pixels[idx] = Color.Lerp(c, new Color(SnowColor.r, SnowColor.g, SnowColor.b, c.a), k);
            }

            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
            { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            tex.SetPixels(pixels);
            tex.Apply();

            var ns = Sprite.Create(tex, new Rect(0, 0, w, h),
                new Vector2(src.pivot.x / w, src.pivot.y / h), src.pixelsPerUnit);
            ns.name = src.name + "_snow" + band;
            _snowCapped[(src, band)] = ns;
            return ns;
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

            // Perf (audit 2026-06-11): blend the border in the CPU-side array,
            // then upload once — the old path did per-pixel GetPixel/SetPixel on
            // the texture, causing streaming hitches when new tile types appear.
            BlendBorderLine(pixels, w, h, new Vector2Int(15, 27), new Vector2Int(31, 16));
            BlendBorderLine(pixels, w, h, new Vector2Int(31, 16), new Vector2Int(15, 5));
            BlendBorderLine(pixels, w, h, new Vector2Int(15, 5), new Vector2Int(0, 16));
            BlendBorderLine(pixels, w, h, new Vector2Int(0, 16), new Vector2Int(15, 27));

            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };
            tex.SetPixels(pixels);
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
            if (_fl