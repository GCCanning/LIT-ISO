using System.Collections.Generic;
using UnityEngine;

namespace LitIso.CharacterCreator
{
    /// <summary>
    /// CPU compositor (v3, LPC wardrobe): stacks one or more pre-colored
    /// 8-row x cols-frame, 64px layer sheets per equipped item into a single
    /// baked sheet for ONE animation, ordered by each draw layer's sortOrder
    /// (LPC zPos). No recoloring — every variant ships as its own ready-to-use
    /// sheet, one per animation (walk/cast/thrust/slash/shoot/hurt).
    ///
    /// Layer sheets ship as Resources .png.bytes (TextAsset) so they are always
    /// CPU-readable regardless of texture import settings. Baking happens only
    /// when the appearance/animation changes, never per frame.
    /// </summary>
    public static class CharacterCompositor
    {
        // Raw layer pixel cache (decoded once per resource)
        static readonly Dictionary<string, Color32[]> s_LayerPixels = new();

        public class BakeResult
        {
            public Texture2D texture;
            public Sprite[] sprites;   // [row * cols + frame], row 0 = S
            public string animId;
            public int framesPerRow;   // = animation.cols
            public int rowCount;       // = catalog.rows (8)
            public int idleFrame;      // only meaningful for "walk"
            public int walkStartFrame; // only meaningful for "walk"
            public float fps;
        }

        /// <summary>Bake the given animation (defaults to the catalog's defaultAnimation, e.g. "walk").</summary>
        public static BakeResult Bake(LayeredAppearance appearance, string animId = null)
        {
            var cat = CharacterLayerCatalog.Instance;
            var anim = cat.Animation(animId ?? cat.defaultAnimation);
            int frame = cat.frameSize, cols = anim.cols, rows = cat.rows;
            int w = cols * frame, h = rows * frame;

            // Gather (pixels, sortOrder) for every draw layer of every selected item.
            var layers = new List<(Color32[] px, int sort)>();
            foreach (var (itemId, variant) in appearance.Selection())
            {
                var def = cat.Find(itemId);
                if (def == null) { Debug.LogWarning($"[Compositor] Unknown item '{itemId}'"); continue; }
                var v = def.SafeVariant(variant);
                foreach (var draw in def.draws)
                {
                    var resource = draw.Resolve(anim.id, appearance.bodyType, v);
                    var px = LoadLayerPixels(resource);
                    if (px == null) continue;
                    if (px.Length != w * h)
                    {
                        Debug.LogWarning($"[Compositor] {resource}: size mismatch ({px.Length} px, expected {w * h})");
                        continue;
                    }
                    layers.Add((px, draw.sortOrder));
                }
            }
            layers.Sort((a, b) => a.sort.CompareTo(b.sort));

            var outPx = new Color32[w * h];
            foreach (var (px, _) in layers)
                Blend(outPx, px);

            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                name = $"BakedCharacter_{anim.id}",
            };
            tex.SetPixels32(outPx);
            tex.Apply(false, false);

            var pivot = new Vector2(cat.pivotX, cat.pivotY);
            var result = new BakeResult
            {
                texture = tex,
                sprites = new Sprite[rows * cols],
                animId = anim.id,
                framesPerRow = cols,
                rowCount = rows,
                idleFrame = anim.idleFrame,
                walkStartFrame = anim.walkStartFrame,
                fps = anim.fps > 0 ? anim.fps : 10f,
            };
            for (int row = 0; row < rows; row++)
            {
                for (int f = 0; f < cols; f++)
                {
                    // Texture y=0 is bottom; sheet row 0 (S) is the TOP band.
                    var rect = new Rect(f * frame, h - (row + 1) * frame, frame, frame);
                    var sp = Sprite.Create(tex, rect, pivot, cat.pixelsPerUnit, 0, SpriteMeshType.FullRect);
                    sp.name = $"BakedCharacter_{anim.id}_{row * cols + f}";
                    result.sprites[row * cols + f] = sp;
                }
            }
            return result;
        }

        static void Blend(Color32[] dst, Color32[] src)
        {
            for (int i = 0; i < dst.Length; i++)
            {
                var s = src[i];
                if (s.a == 0) continue;
                if (s.a == 255) { dst[i] = s; continue; }
                var d = dst[i];
                float sa = s.a / 255f, da = d.a / 255f * (1f - sa);
                float outA = sa + da;
                if (outA <= 0f) continue;
                dst[i] = new Color32(
                    (byte)((s.r * sa + d.r * da) / outA),
                    (byte)((s.g * sa + d.g * da) / outA),
                    (byte)((s.b * sa + d.b * da) / outA),
                    (byte)(outA * 255f));
            }
        }

        // Representative colour cache for UI swatches, keyed id|variant|body.
        static readonly Dictionary<string, Color> s_Swatch = new();

        /// <summary>
        /// Average colour of an item variant's non-outline opaque pixels on the
        /// default (walk) sheet — used to draw a live colour swatch next to each
        /// colour slider in the creator. Near-black outline and near-white pixels
        /// are skipped so the swatch reads as the actual fabric/hair/skin hue.
        /// Cached per (item, variant, body).
        /// </summary>
        public static Color SwatchColor(ItemDef def, string variant, string bodyType)
        {
            if (def == null || def.draws == null || def.draws.Length == 0) return Color.gray;
            var cat = CharacterLayerCatalog.Instance;
            string v = def.SafeVariant(variant);
            string key = def.id + "|" + v + "|" + bodyType;
            if (s_Swatch.TryGetValue(key, out var cached)) return cached;

            // Front-most draw layer is the most representative (e.g. cape front, not lining).
            var draw = def.draws[def.draws.Length - 1];
            var px = LoadLayerPixels(draw.Resolve(cat.defaultAnimation, bodyType, v));
            Color result = Color.gray;
            if (px != null && px.Length > 0)
            {
                double r = 0, g = 0, b = 0; long n = 0;
                double r2 = 0, g2 = 0, b2 = 0; long n2 = 0; // any-opaque fallback
                foreach (var p in px)
                {
                    if (p.a < 200) continue;
                    r2 += p.r; g2 += p.g; b2 += p.b; n2++;
                    int bright = p.r + p.g + p.b;
                    if (bright < 90 || bright > 720) continue; // skip outline & near-white
                    r += p.r; g += p.g; b += p.b; n++;
                }
                if (n > 0) result = new Color((float)(r / n) / 255f, (float)(g / n) / 255f, (float)(b / n) / 255f, 1f);
                else if (n2 > 0) result = new Color((float)(r2 / n2) / 255f, (float)(g2 / n2) / 255f, (float)(b2 / n2) / 255f, 1f);
            }
            s_Swatch[key] = result;
            return result;
        }

        static Color32[] LoadLayerPixels(string resource)
        {
            if (s_LayerPixels.TryGetValue(resource, out var cached)) return cached;
            // Sheets are stored as "<name>.png.bytes" => Resources path "<name>.png"
            var ta = Resources.Load<TextAsset>(resource + ".png");
            if (ta == null)
            {
                Debug.LogWarning($"[Compositor] Missing layer sheet Resources/{resource}.png.bytes");
                s_LayerPixels[resource] = null;
                return null;
            }
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            tex.LoadImage(ta.bytes, false);
            var px = tex.GetPixels32();
            Object.Destroy(tex);
            s_LayerPixels[resource] = px;
            return px;
        }
    }
}
