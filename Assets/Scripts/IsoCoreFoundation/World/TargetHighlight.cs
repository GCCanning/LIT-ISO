using UnityEngine;

namespace IsoCore.Foundation
{
    /// <summary>
    /// A pulsing isometric ring drawn on the ground under whatever the player is about to
    /// interact with, so the harvest target is always clear. The diamond outline sprite is
    /// generated in code; it draws on top so it reads against props and dark night tiles.
    /// </summary>
    public class TargetHighlight : MonoBehaviour
    {
        SpriteRenderer _outline;
        SpriteRenderer _glow;
        bool _active;
        float _scale = 1f;
        float _lift;
        Color _color = new Color(1f, 0.95f, 0.5f, 0.72f);
        static Sprite _sprite;

        public void Build()
        {
            _outline = gameObject.AddComponent<SpriteRenderer>();
            _outline.sprite = _sprite != null ? _sprite : (_sprite = MakeDiamond());
            _outline.sortingOrder = 8500;
            _outline.color = _color;

            var glowGo = new GameObject("TargetGlow");
            glowGo.transform.SetParent(transform, false);
            _glow = glowGo.AddComponent<SpriteRenderer>();
            _glow.sprite = _outline.sprite;
            _glow.sortingOrder = 8499;
            _glow.color = new Color(_color.r, _color.g, _color.b, 0.18f);
            _glow.transform.localScale = Vector3.one * 1.35f;
            gameObject.SetActive(false);
        }

        public void SetTarget(bool active, Vector3 worldPos)
        {
            SetTarget(active, worldPos, 1f, new Color(1f, 0.95f, 0.5f, 0.72f), 0f);
        }

        public void SetTarget(bool active, Vector3 worldPos, float scale, Color color, float lift = 0f)
        {
            _active = active;
            _scale = Mathf.Max(0.75f, scale);
            _lift = Mathf.Max(0f, lift);
            _color = color;
            if (_outline != null)
                _outline.color = _color;
            if (_glow != null)
                _glow.color = new Color(_color.r, _color.g, _color.b, 0.18f);

            if (active)
            {
                transform.position = worldPos + Vector3.up * _lift;
                transform.localScale = Vector3.one * _scale;
            }
            gameObject.SetActive(active);
        }

        void Update()
        {
            if (!_active || _outline == null) return;

            float pulse = 1f + Mathf.Sin(Time.time * 6f) * 0.03f;
            transform.localScale = Vector3.one * _scale * pulse;

            float alpha = 0.30f + 0.25f * (0.5f + 0.5f * Mathf.Sin(Time.time * 6f));
            var outline = _outline.color;
            outline.a = alpha;
            _outline.color = outline;
            if (_glow != null)
            {
                var glow = _glow.color;
                glow.a = alpha * 0.38f;
                _glow.color = glow;
            }
        }

        // 2:1 iso diamond outline, warm yellow, transparent fill.
        static Sprite MakeDiamond()
        {
            const int w = 64, h = 32;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
            { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[w * h];
            for (int i = 0; i < px.Length; i++) px[i] = new Color32(0, 0, 0, 0);

            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float nx = Mathf.Abs(x / (float)(w - 1) - 0.5f) * 2f;
                float ny = Mathf.Abs(y / (float)(h - 1) - 0.5f) * 2f;
                float d = nx + ny;                  // 1.0 exactly on the diamond edge
                if (d > 0.82f && d < 1.0f)          // a ring band near the edge
                    px[y * w + x] = new Color32(255, 240, 150, 255);
            }
            tex.SetPixels32(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 64f);
        }
    }
}
