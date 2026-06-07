using UnityEngine;

namespace IsoCore.Foundation
{
    /// <summary>Small generated world-space progress bar for break/harvest feedback.</summary>
    public sealed class WorldProgressBar : MonoBehaviour
    {
        SpriteRenderer _back;
        SpriteRenderer _fill;
        float _hideTimer;
        static Sprite _sprite;

        public void Build(int sortingOrder)
        {
            _back = new GameObject("Back").AddComponent<SpriteRenderer>();
            _back.transform.SetParent(transform, false);
            _back.sprite = _sprite != null ? _sprite : (_sprite = MakeSprite());
            _back.color = new Color(0.05f, 0.06f, 0.07f, 0.82f);
            _back.sortingOrder = sortingOrder;
            _back.transform.localScale = new Vector3(0.62f, 0.08f, 1f);

            _fill = new GameObject("Fill").AddComponent<SpriteRenderer>();
            _fill.transform.SetParent(transform, false);
            _fill.sprite = _back.sprite;
            _fill.color = new Color(1f, 0.78f, 0.24f, 0.95f);
            _fill.sortingOrder = sortingOrder + 1;
            _fill.transform.localScale = new Vector3(0f, 0.052f, 1f);

            gameObject.SetActive(false);
        }

        public void Show(float progress01, float seconds = 1.2f)
        {
            progress01 = Mathf.Clamp01(progress01);
            _hideTimer = Mathf.Max(0.1f, seconds);
            gameObject.SetActive(true);

            float width = 0.56f * progress01;
            _fill.transform.localScale = new Vector3(width, 0.052f, 1f);
            _fill.transform.localPosition = new Vector3(-0.28f + width * 0.5f, 0f, 0f);
        }

        void Update()
        {
            if (_hideTimer <= 0f) return;
            _hideTimer -= Time.deltaTime;
            if (_hideTimer <= 0f)
                gameObject.SetActive(false);
        }

        static Sprite MakeSprite()
        {
            const int w = 16, h = 4;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };
            var pixels = new Color32[w * h];
            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = new Color32(255, 255, 255, 255);
            tex.SetPixels32(pixels);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 32f);
        }
    }
}
