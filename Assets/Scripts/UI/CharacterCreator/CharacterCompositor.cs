using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace LitIso.CharacterCreator
{
    /// <summary>
    /// CPU compositor (v4, LPC runtime-palette model). For ONE animation it:
    ///  1. loads each selected item's base sheet for the chosen bodyType (one
    ///     whole-sheet decode per item x body, cached),
    ///  2. slices the item's animation band (rowOffset/rows/cols),
    ///  3. recolors the band from the item's baseVariant ramp to the chosen
    ///     variant ramp (per-pixel tolerance LUT; skin-tone for matchBodyColor),
    ///  4. composites all bands by sortOrder into one baked sheet.
    /// The output sheet has anim.rows rows (4 cardinal, or 1 for hurt/climb), not
    /// a global 8. Base sheets ship as Resources .png.bytes (TextAsset) so they
    /// are CPU-readable regardless of import settings. Palettes ship as
    /// Resources/Characters/Palettes/&lt;material&gt;.json.bytes.
    /// Baking happens only when appearance/animation changes, never per frame.
    /// See Docs/design/LPC_IMPORT_PIPELINE.md.
    ///
    /// Oversize swings (deviation fix): great weapons ship attack bands authored at
    /// 128/192px so the weapon arc clears the 64px body. AnimationDef carries
    /// frameSize/frameOffset/rowOffsetPx for those bands; when any selected item's
    /// band for an animation is oversize the whole baked sheet uses that frame size
    /// and the 64px body/head bands are centred inside each frame. This oversize
    /// path is UNTESTED-BY-DESIGN (not compiled here; Unity project).
    /// </summary>
    public static class CharacterCompositor
    {
        static readonly int ColorTolerance = 1; // +/- per channel when matching the base ramp

        // Whole base-sheet pixel cache, keyed by sheet resource path.
        static readonly Dictionary<string, Color32[]> s_SheetPixels = new();
        static readonly Dictionary<string, Vector2Int> s_SheetSize = new();

        // Parsed palettes, keyed by material; value maps variant -> ramp.
        static readonly Dictionary<string, Dictionary<string, Color32[]>> s_Palettes = new();

        // Recolor LUTs, keyed material|base|target.
        static readonly Dictionary<string, Dictionary<int, Color32>> s_Luts = new();

        public class BakeResult
        {
            public Texture2D texture;
            public Sprite[] sprites;   // [row * cols + frame], row index follows catalog.rowOrder
            public string animId;
            public int framesPerRow;   // = anim.cols
            public int rowCount;       // = anim.rows (4 or 1)
            public float fps;
        }

        public static BakeResult Bake(LayeredAppearance appearance, string animId = null)
        {
            var cat = CharacterLayerCatalog.Instance;
            int frame = cat.frameSize;
            string body = appearance.bodyType;
            animId ??= cat.defaultAnimation;

            // First pass: resolve each selected item's band for this animation.
            // Each band records its own frame size (64 normally, 128/192 for
            // oversize great-weapon swings). The output frame size is the largest
            // band's frame; smaller bands (the 64px body/head) are centred inside
            // each oversize frame by (outFrame - bandFrame)/2.
            var bands = new List<(Color32[] px, int cols, int rows, int bandFrame, int sort)>();
            int maxCols = 1, rows = 1, outFrame = frame;

            foreach (var (itemId, variant) in appearance.Selection())
            {
                var def = cat.Find(itemId);
                if (def == null) { Debug.LogWarning($"[Compositor] Unknown item '{itemId}'"); continue; }
                var anim = def.Anim(animId);
                if (anim == null) continue;                  // item doesn't ship this animation
                string sheet = def.SheetFor(body);
                if (string.IsNullOrEmpty(sheet)) continue;

                var sheetPx = LoadSheetPixels(sheet, out var size);
                if (sheetPx == null) continue;

                int bandFrame = anim.FrameSizeOr(frame);
                var band = SliceBand(sheetPx, size, frame, bandFrame, anim);
                if (band == null) continue;

                string targetVariant = def.matchBodyColor ? appearance.skinVariant : variant;
                band = RecolorBand(band, def, targetVariant);

                bands.Add((band, anim.cols, anim.rows, bandFrame, def.DrawFor(body)?.sortOrder ?? 0));
                if (anim.cols > maxCols) maxCols = anim.cols;
                if (anim.rows > rows) rows = anim.rows;
                if (bandFrame > outFrame) outFrame = bandFrame;
            }

            bands.Sort((a, b) => a.sort.CompareTo(b.sort));

            // Output sheet uses the largest band frame so oversize swings are not
            // clipped. frame (slicing/sprite unit) becomes outFrame from here on.
            frame = outFrame;
            int w = maxCols * frame, h = rows * frame;
            var outPx = new Color32[w * h];
            foreach (var (px, cols, brows, bandFrame, _) in bands)
                BlendBand(outPx, w, h, frame, px, cols, brows, bandFrame);

            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                name = $"BakedCharacter_{animId}",
            };
            tex.SetPixels32(outPx);
            tex.Apply(false, false);

            var pivot = new Vector2(cat.pivotX, cat.pivotY);
            var result = new BakeResult
            {
                texture = tex,
                sprites = new Sprite[rows * maxCols],
                animId = animId,
                framesPerRow = maxCols,
                rowCount = rows,
                fps = GetFps(cat, animId),
            };
            for (int row = 0; row < rows; row++)
            {
                for (int f = 0; f < maxCols; f++)
                {
                    // Texture y=0 is bottom; band row 0 (first rowOrder dir) is the TOP.
                    var rect = new Rect(f * frame, h - (row + 1) * frame, frame, frame);
                    var sp = Sprite.Create(tex, rect, pivot, cat.pixelsPerUnit, 0, SpriteMeshType.FullRect);
                    sp.name = $"BakedCharacter_{animId}_{row * maxCols + f}";
                    result.sprites[row * maxCols + f] = sp;
                }
            }
            return result;
        }

        static float GetFps(CharacterLayerCatalog cat, string animId)
        {
            var body = cat.Find("lpc/body") ?? cat.First("body");
            var a = body?.Anim(animId);
            return (a != null && a.fps > 0) ? a.fps : 10f;
        }

        // ----- sheet slicing -------------------------------------------------

        // Slice the animation band (origin bottom-left). bandFrame is the band's
        // native frame size (64, or 128/192 for oversize). The band top is located
        // by anim.TopPx (exact pixels), so a 192px-row oversize band addressed from
        // a sheet that also holds 64px bands slices correctly. Returns a
        // cols*bandFrame x rows*bandFrame buffer; row 0 = the TOP source row.
        static Color32[] SliceBand(Color32[] sheet, Vector2Int size, int sheetFrame, int bandFrame, AnimationDef anim)
        {
            int bw = anim.cols * bandFrame, bh = anim.rows * bandFrame;
            int topPx = anim.TopPx(sheetFrame);
            if (bw > size.x || topPx + bh > size.y) return null;
            var band = new Color32[bw * bh];
            // topPx is the band top measured from the sheet TOP. In a bottom-origin
            // texture its bottom y = size.y - topPx - bh.
            int srcBottomY = size.y - topPx - bh;
            for (int y = 0; y < bh; y++)
            {
                int sy = srcBottomY + y;
                int srcRow = sy * size.x;
                int dstRow = y * bw;
                for (int x = 0; x < bw; x++)
                    band[dstRow + x] = sheet[srcRow + x];
            }
            return band;
        }

        // Alpha-composite a band onto the output sheet (both bottom-left origin),
        // aligning per frame cell. The output uses outFrame-sized cells; a band
        // with a smaller bandFrame (e.g. the 64px body under a 192px weapon swing)
        // is centred inside each output frame by inset = (outFrame - bandFrame)/2,
        // matching the LPC generator's centring of the body in oversize frames.
        static void BlendBand(Color32[] dst, int dw, int dh, int outFrame,
                              Color32[] src, int cols, int rows, int bandFrame)
        {
            int sw = cols * bandFrame;
            int inset = (outFrame - bandFrame) / 2;   // 0 when band == output frame
            for (int row = 0; row < rows; row++)
            for (int f = 0; f < cols; f++)
            {
                // Frame cell origins (bottom-left). Row 0 = TOP, so its output
                // cell is the highest (largest dy).
                int dCellX = f * outFrame + inset;
                int dCellYBottom = dh - (row + 1) * outFrame + inset;
                int sCellX = f * bandFrame;
                int sCellYBottom = (rows - 1 - row) * bandFrame;
                for (int yy = 0; yy < bandFrame; yy++)
                {
                    int dy = dCellYBottom + yy;
                    if (dy < 0 || dy >= dh) continue;
                    int sy = sCellYBottom + yy;
                    int sRow = sy * sw, dRow = dy * dw;
                    for (int xx = 0; xx < bandFrame; xx++)
                    {
                        int dx = dCellX + xx;
                        if (dx < 0 || dx >= dw) continue;
                        BlendPixel(dst, dRow + dx, src[sRow + (sCellX + xx)]);
                    }
                }
            }
        }

        static void BlendPixel(Color32[] dst, int i, Color32 s)
        {
            if (s.a == 0) return;
            if (s.a == 255) { dst[i] = s; return; }
            var d = dst[i];
            float sa = s.a / 255f, da = d.a / 255f * (1f - sa);
            float outA = sa + da;
            if (outA <= 0f) return;
            dst[i] = new Color32(
                (byte)((s.r * sa + d.r * da) / outA),
                (byte)((s.g * sa + d.g * da) / outA),
                (byte)((s.b * sa + d.b * da) / outA),
                (byte)(outA * 255f));
        }

        // ----- palette recolor ----------------------------------------------

        static Color32[] RecolorBand(Color32[] band, ItemDef def, string targetVariant)
        {
            string material = def.matchBodyColor ? "body" : def.paletteMaterial;
            if (string.IsNullOrEmpty(material)) return band;       // no palette family: pass through
            // baseVariant is the variant the stored sheet is authored in (detected at import).
            string baseVariant = def.baseVariant;

            var lut = GetLut(material, baseVariant, targetVariant);
            if (lut == null) return band;                          // no-op / unavailable

            var outPx = new Color32[band.Length];
            for (int i = 0; i < band.Length; i++)
            {
                var p = band[i];
                if (p.a == 0) { outPx[i] = p; continue; }
                if (TryMatch(lut, p, out var rep))
                    outPx[i] = new Color32(rep.r, rep.g, rep.b, p.a);
                else
                    outPx[i] = p;
            }
            return outPx;
        }

        // LUT maps a packed RGB key -> replacement. Tolerance matching is done at
        // lookup by probing the small +/-1 neighbourhood, so the dict stays exact.
        static Dictionary<int, Color32> GetLut(string material, string baseVariant, string targetVariant)
        {
            var pal = LoadPalette(material);
            if (pal == null) return null;

            // body matchBodyColor has no explicit baseVariant: use the first key.
            if (string.IsNullOrEmpty(baseVariant))
            {
                foreach (var k in pal.Keys) { baseVariant = k; break; }
            }
            if (baseVariant == targetVariant) return null;         // no-op
            if (!pal.TryGetValue(baseVariant, out var baseRamp)) return null;
            if (!pal.TryGetValue(targetVariant, out var targetRamp)) return null;
            int n = Mathf.Min(baseRamp.Length, targetRamp.Length);
            if (n == 0) return null;

            string key = material + "|" + baseVariant + "|" + targetVariant;
            if (s_Luts.TryGetValue(key, out var cached)) return cached;

            var lut = new Dictionary<int, Color32>(n);
            for (int i = 0; i < n; i++)
                lut[Pack(baseRamp[i])] = targetRamp[i];
            s_Luts[key] = lut;
            return lut;
        }

        static bool TryMatch(Dictionary<int, Color32> lut, Color32 p, out Color32 rep)
        {
            // exact first
            if (lut.TryGetValue(Pack(p), out rep)) return true;
            if (ColorTolerance <= 0) return false;
            for (int dr = -ColorTolerance; dr <= ColorTolerance; dr++)
            for (int dg = -ColorTolerance; dg <= ColorTolerance; dg++)
            for (int db = -ColorTolerance; db <= ColorTolerance; db++)
            {
                if (dr == 0 && dg == 0 && db == 0) continue;
                int r = p.r + dr, g = p.g + dg, b = p.b + db;
                if (r < 0 || r > 255 || g < 0 || g > 255 || b < 0 || b > 255) continue;
                if (lut.TryGetValue((r << 16) | (g << 8) | b, out rep)) return true;
            }
            rep = default;
            return false;
        }

        static int Pack(Color32 c) => (c.r << 16) | (c.g << 8) | c.b;

        static Dictionary<string, Color32[]> LoadPalette(string material)
        {
            if (s_Palettes.TryGetValue(material, out var cached)) return cached;
            var ta = Resources.Load<TextAsset>($"Characters/Palettes/{material}.json");
            Dictionary<string, Color32[]> pal = null;
            if (ta != null) pal = ParsePalette(ta.text);
            else Debug.LogWarning($"[Compositor] Missing palette Resources/Characters/Palettes/{material}.json.bytes");
            s_Palettes[material] = pal;
            return pal;
        }

        // Parse { "variant": ["#RRGGBB", ...], ... } without JsonUtility (arbitrary keys).
        static Dictionary<string, Color32[]> ParsePalette(string json)
        {
            var pal = new Dictionary<string, Color32[]>();
            int i = 0, len = json.Length;
            while (i < len)
            {
                // find a key string
                int ks = json.IndexOf('"', i);
                if (ks < 0) break;
                int ke = json.IndexOf('"', ks + 1);
                if (ke < 0) break;
                string keyName = json.Substring(ks + 1, ke - ks - 1);
                // find the value: expect ':' then either '[' (ramp) or '{'/'"' (skip non-arrays)
                int colon = json.IndexOf(':', ke + 1);
                if (colon < 0) break;
                int vstart = colon + 1;
                while (vstart < len && char.IsWhiteSpace(json[vstart])) vstart++;
                if (vstart >= len) break;
                if (json[vstart] == '[')
                {
                    int vend = json.IndexOf(']', vstart);
                    if (vend < 0) break;
                    var ramp = ParseRamp(json.Substring(vstart + 1, vend - vstart - 1));
                    if (ramp.Length > 0) pal[keyName] = ramp;
                    i = vend + 1;
                }
                else
                {
                    // not a ramp (e.g. nested object / scalar) — skip this key
                    i = vstart + 1;
                }
            }
            return pal;
        }

        static Color32[] ParseRamp(string body)
        {
            var list = new List<Color32>();
            int i = 0, len = body.Length;
            while (i < len)
            {
                int qs = body.IndexOf('"', i);
                if (qs < 0) break;
                int qe = body.IndexOf('"', qs + 1);
                if (qe < 0) break;
                string hex = body.Substring(qs + 1, qe - qs - 1).Trim();
                if (TryParseHex(hex, out var c)) list.Add(c);
                i = qe + 1;
            }
            return list.ToArray();
        }

        static bool TryParseHex(string hex, out Color32 c)
        {
            c = new Color32(0, 0, 0, 255);
            if (string.IsNullOrEmpty(hex)) return false;
            if (hex[0] == '#') hex = hex.Substring(1);
            if (hex.Length < 6) return false;
            if (byte.TryParse(hex.Substring(0, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var r) &&
                byte.TryParse(hex.Substring(2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var g) &&
                byte.TryParse(hex.Substring(4, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var b))
            {
                byte a = 255;
                if (hex.Length >= 8)
                    byte.TryParse(hex.Substring(6, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out a);
                c = new Color32(r, g, b, a);
                return true;
            }
            return false;
        }

        // ----- base sheet loading -------------------------------------------

        static Color32[] LoadSheetPixels(string sheet, out Vector2Int size)
        {
            if (s_SheetPixels.TryGetValue(sheet, out var cached))
            {
                size = s_SheetSize[sheet];
                return cached;
            }
            size = Vector2Int.zero;
            var ta = Resources.Load<TextAsset>(sheet + ".png");
            if (ta == null)
            {
                Debug.LogWarning($"[Compositor] Missing base sheet Resources/{sheet}.png.bytes");
                s_SheetPixels[sheet] = null;
                s_SheetSize[sheet] = size;
                return null;
            }
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            tex.LoadImage(ta.bytes, false);
            var px = tex.GetPixels32();
            size = new Vector2Int(tex.width, tex.height);
            Object.Destroy(tex);
            s_SheetPixels[sheet] = px;
            s_SheetSize[sheet] = size;
            return px;
        }

        // ----- UI swatch -----------------------------------------------------

        static readonly Dictionary<string, Color> s_Swatch = new();

        /// <summary>
        /// Representative colour of an item variant for the creator's swatch:
        /// slices the walk (or default) band, recolors to the variant, then
        /// averages non-outline opaque pixels. Cached per (item, variant, body).
        /// </summary>
        public static Color SwatchColor(ItemDef def, string variant, string bodyType)
        {
            if (def == null) return Color.gray;
            var cat = CharacterLayerCatalog.Instance;
            string v = def.SafeVariant(variant);
            string key = def.id + "|" + v + "|" + bodyType;
            if (s_Swatch.TryGetValue(key, out var cachedC)) return cachedC;

            Color result = Color.gray;
            var anim = def.Anim(cat.defaultAnimation) ?? (def.animations != null && def.animations.Length > 0 ? def.animations[0] : null);
            string sheet = def.SheetFor(bodyType);
            if (anim != null && !string.IsNullOrEmpty(sheet))
            {
                var sheetPx = LoadSheetPixels(sheet, out var size);
                if (sheetPx != null)
                {
                    var band = SliceBand(sheetPx, size, cat.frameSize, anim.FrameSizeOr(cat.frameSize), anim);
                    if (band != null)
                    {
                        band = RecolorBand(band, def, v);
                        double r = 0, g = 0, b = 0; long n = 0;
                        double r2 = 0, g2 = 0, b2 = 0; long n2 = 0;
                        foreach (var p in band)
                        {
                            if (p.a < 200) continue;
                            r2 += p.r; g2 += p.g; b2 += p.b; n2++;
                            int bright = p.r + p.g + p.b;
                            if (bright < 90 || bright > 720) continue;
                            r += p.r; g += p.g; b += p.b; n++;
                        }
                        if (n > 0) result = new Color((float)(r / n) / 255f, (float)(g / n) / 255f, (float)(b / n) / 255f, 1f);
                        else if (n2 > 0) result = new Color((float)(r2 / n2) / 255f, (float)(g2 / n2) / 255f, (float)(b2 / n2) / 255f, 1f);
                    }
                }
            }
            s_Swatch[key] = result;
            return result;
        }
    }
}
