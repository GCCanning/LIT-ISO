using UnityEngine;

namespace IsoCore.Foundation
{
    /// <summary>
    /// A small world-space label that rises and fades, then self-destroys. Used for pickup
    /// feedback ("+2 Wood") and damage numbers. Built from a legacy TextMesh so it needs no
    /// Canvas and sorts above the world via its MeshRenderer.
    /// </summary>
    public class FloatingText : MonoBehaviour
    {
        float _life, _maxLife = 1.0f;
        TextMesh _tm;
        MeshRenderer _mr;
        Vector3 _vel;

        public static void Spawn(Vector3 worldPos, string text, Color color, float rise = 0.9f)
        {
            var go = new GameObject("FloatingText");
            go.transform.position = worldPos + new Vector3(0f, 0.4f, 0f);
            var ft = go.AddComponent<FloatingText>();
            ft.Setup(text, color, rise);
        }

        void Setup(string text, Color color, float rise)
        {
            _tm = gameObject.AddComponent<TextMesh>();
            _tm.text = text;
            _tm.characterSize = 0.06f;
            _tm.fontSize = 64;
            _tm.anchor = TextAnchor.LowerCenter;
            _tm.alignment = TextAlignment.Center;
            _tm.color = color;
            _mr = GetComponent<MeshRenderer>();
            _mr.sortingOrder = 9500; // above world & FX, below UI
            _vel = new Vector3(0f, rise, 0f);
        }

        void Update()
        {
            _life += Time.deltaTime;
            float t = _life / _maxLife;
            transform.position += _vel * Time.deltaTime;
            _vel *= 0.92f; // ease out the rise
            if (_tm != null)
            {
                var c = _tm.color; c.a = Mathf.Clamp01(1f - t); _tm.color = c;
                float s = 1f + 0.15f * Mathf.Sin(t * Mathf.PI); // subtle pop
                transform.localScale = Vector3.one * s;
            }
            if (_life >= _maxLife) Destroy(gameObject);
        }
    }
}
