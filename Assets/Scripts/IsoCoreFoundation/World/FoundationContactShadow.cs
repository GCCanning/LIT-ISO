using UnityEngine;

namespace IsoCore.Foundation
{
    /// <summary>
    /// Cheap soft oval shadow that grounds sprites on the isometric plane. It is art-agnostic:
    /// final LoRA sprites can change freely while this keeps actors, props, portals, and
    /// furniture from looking pasted onto the tilemap.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class FoundationContactShadow : MonoBehaviour
    {
        public float scaleMultiplier = 1f;
        public float baseAlpha = 0.28f;
        public Vector2 localOffset = new(0f, -0.035f);

        SpriteRenderer _source;
        SpriteRenderer _shadow;
        DayNightSystem _dayNight;
        Sprite _lastSprite;
        static Sprite _sprite;

        public void Configure(float scale = 1f, float alpha = 0.28f)
        {
            scaleMultiplier = Mathf.Max(0.1f, scale);
            baseAlpha = Mathf.Clamp01(alpha);
            RefreshSize();
        }

        void Awake()
        {
            _source = GetComponent<SpriteRenderer>();
            _dayNight = Object.FindFirstObjectByType<DayNightSystem>();
            BuildShadow();
            RefreshSize();
        }

        void LateUpdate()
        {
            if (_source == null || _shadow == null)
                return;

            if (_source.sprite != null && _source.sprite != _lastSprite)
                RefreshSize();

            _shadow.sortingLayerID = _source.sortingLayerID;
            _shadow.sortingOrder = _source.sortingOrder - 2;
            float night = _dayNight != null ? _dayNight.NightFactor : 0f;
            float light = _dayNight != null ? _dayNight.LightIntensity : 0.75f;
            float alpha = baseAlpha * Mathf.Lerp(1f, 0.62f, night) * Mathf.Lerp(0.78f, 1.12f, light);
            _shadow.color = new Color(0.025f, 0.026f, 0.035f, Mathf.Clamp01(alpha));
            _shadow.enabled = _source.enabled && _source.sprite != null && _source.color.a > 0.05f;
        }

        void BuildShadow()
        {
            var go = new GameObject("ContactShadow");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(localOffset.x, localOffset.y, 0f);
            _shadow = go.AddComponent<SpriteRenderer>();
            _shadow.sprite = _sprite != null ? _sprite : (_sprite = MakeOval());
            _shadow.sortingOrder = _source != null ? _source.sortingOrder - 2 : -2;
        }

        void RefreshSize()
        {
            if (_source == null || _shadow == null || _source.sprite == null)
                return;

            Bounds b = _source.sprite.bounds;
            _lastSprite = _source.sprite;
            float w = Mathf.Clamp(b.size.x * 0.92f * scaleMultiplier, 0.34f, 2.6f);
            float h = Mathf.Clamp(w * 0.34f, 0.12f, 0.82f);
            _shadow.transform.localPosition = new Vector3(localOffset.x, localOffset.y, 0f);
            _shadow.transform.localScale = new Vector3(w, h, 1f);
        }

        static Sprite MakeOval()
        {
            const int w = 96, h = 48;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            var px = new Color32[w * h];
            Vector2 c = new(w * 0.5f, h * 0.5f);
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float dx = (x - c.x) / (w * 0.48f);
                float dy = (y - c.y) / (h * 0.38f);
                float d = dx * dx + dy * dy;
                float a = Mathf.Clamp01(1f - d);
                a = a * a * 0.72f;
                px[y * w + x] = new Color(1f, 1f, 1f, a);
            }
            tex.SetPixels32(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), w);
        }
    }
}
