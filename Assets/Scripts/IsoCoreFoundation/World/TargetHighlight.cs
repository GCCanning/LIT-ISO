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
        SpriteRenderer _sr;
        bool _active;
        static Sprite _sprite;

        public void Build()
        {
            _sr = gameObject.AddComponent<SpriteRenderer>();
            _sr.sprite = _sprite != null ? _sprite : (_sprite = MakeDiamond());
            _sr.sortingOrder = 8500;
            _sr.color = new Color(1f, 0.95f, 0.5f, 0.6f);
            gameObject.SetActive(false);
        }

        public void SetTarget(bool active, Vector3 worldPos)
        {
            if (active != _active) { _active = active; gameObject.SetActive(active); }
            if (active) transform.position = worldPos;
        }

        void Update()
        {
            if (!_active || _sr == null) return;
            var c = _sr.color;
            c.a = 0.35f + 0.30f * (0.5f + 0.5f * Mathf.Sin(Time.time * 6f));
            _sr.color = c;
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
