using UnityEngine;

namespace IsoCore.Foundation
{
    /// <summary>
    /// A warm, flickering additive glow for light-emitting placeables (campfire, lantern). It
    /// fades in as night falls (DayNightSystem.NightFactor) so placed lights become genuinely
    /// useful in the dark — a bright cut-out against the moonlit ambient. Built-in pipeline,
    /// so this is a faked light (additive sprite), not a real Light2D.
    /// </summary>
    public class CampfireGlow : MonoBehaviour
    {
        DayNightSystem _dn;
        SpriteRenderer _sr;
        Color _base;
        float _radius;
        float _seed;

        static Sprite _glowSprite;
        static Material _additive;

        public void Setup(Color color, float radius, int parentSortingOrder)
        {
            _dn = Object.FindFirstObjectByType<DayNightSystem>();
            _base = color;
            _radius = Mathf.Max(0.2f, radius);
            _seed = Random.value * 10f;

            _sr = gameObject.AddComponent<SpriteRenderer>();
            _sr.sprite = _glowSprite != null ? _glowSprite : (_glowSprite = MakeGlow());
            _sr.material = Additive();
            _sr.sortingOrder = parentSortingOrder - 1; // behind the prop, above ground
            transform.localScale = Vector3.one * _radius;
            var c = _base; c.a = 0f; _sr.color = c;
        }

        void Update()
        {
            if (_sr == null) return;
            float night = _dn != null ? Mathf.Clamp01(_dn.NightFactor) : 0f;
            float flicker = 0.82f + 0.18f * Mathf.PerlinNoise(_seed, Time.time * 4.5f);
            var c = _base; c.a = night * flicker; _sr.color = c;
            float s = _radius * (0.95f + 0.08f * flicker);
            transform.localScale = new Vector3(s, s, 1f);
        }

        static Material Additive()
        {
            if (_additive != null) return _additive;
            var sh = Shader.Find("Legacy Shaders/Particles/Additive")
                  ?? Shader.Find("Particles/Standard Unlit")
                  ?? Shader.Find("Sprites/Default");
            _additive = new Material(sh) { name = "CampfireGlowAdditive" };
            return _additive;
        }

        // Soft radial gradient: bright centre fading to transparent edge.
        static Sprite MakeGlow()
        {
            const int s = 128;
            var tex = new Texture2D(s, s, TextureFormat.RGBA32, false)
            { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[s * s];
            Vector2 c = new Vector2(s * 0.5f, s * 0.5f);
            float maxR = s * 0.5f;
            for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), c) / maxR;
                float a = Mathf.Clamp01(1f - d);
                a = a * a; // soft falloff
                px[y * s + x] = new Color(1f, 1f, 1f, a);
            }
            tex.SetPixels32(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), s); // 1 unit
        }
    }
}
