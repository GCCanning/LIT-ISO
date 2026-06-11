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
        // Pooled (perf audit 2026-06-11, applied by Claude on owner instruction):
        // Instantiate+Destroy per damage/pickup event caused GC spikes in combat.
        static readonly System.Collections.Generic.Queue<FloatingText> s_pool
            = new System.Collections.Generic.Queue<FloatingText>();

        float _life, _maxLife = 1.0f;
        TextMesh _tm;
        MeshRenderer _mr;
        Vector3 _vel;

        public static void Spawn(Vector3 worldPos, string text, Color color, float rise = 0.9f)
        {
            FloatingText ft = null;
            while (s_pool.Count > 0 && ft == null)
                ft = s_pool.Dequeue();           // skip any destroyed entries

            if (ft == null)
            {
                var go = new GameObject("FloatingText");
                ft = go.AddComponent<FloatingText>();
                ft._tm = go.AddComponent<TextMesh>();
                ft._tm.characterSize = 0.06f;
                ft._tm.fontSize = 64;
                ft._tm.anchor = TextAnchor.LowerCenter;
                ft._tm.alignment = TextAlignment.Center;
                ft._mr = go.GetComponent<MeshRenderer>();
                ft._mr.sortingOrder = 9500; // above world & FX, below UI
            }

            ft.gameObject.SetActive(true);
            ft.transform.position = worldPos + new Vector3(0f, 0.4f, 0f);
            ft.transform.localScale = Vector3.one;
            ft._tm.text = text;
            ft._tm.color = color;
            ft._vel = new Vector3(0f, rise, 0f);
            ft._life = 0f;
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
            if (_life >= _maxLife)
            {
                gameObject.SetActive(false);
                s_pool.Enqueue(this);
            }
        }
    }
}
