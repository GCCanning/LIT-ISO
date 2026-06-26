using UnityEngine;

namespace IsoCore.Foundation
{
    /// <summary>
    /// Gentle wind sway for bottom-pivoted vegetation: oscillates a small z-rotation so the top
    /// of the sprite drifts while the base stays planted. Each instance gets its own phase so a
    /// field of grass/trees moves organically instead of in lockstep, and the whole world sways
    /// harder during Wind/Drizzle weather (reads FoundationWeatherVisuals.WindStrength). Pure
    /// transform animation — no sorting or geometry change.
    /// </summary>
    public sealed class WindSway : MonoBehaviour
    {
        float _phase;
        float _amplitudeDeg = 2.2f;
        float _speed = 1.0f;

        public void Init(float amplitudeDeg = 2.2f, float speed = 1.0f)
        {
            _amplitudeDeg = amplitudeDeg;
            _speed = speed;
            _phase = Random.value * Mathf.PI * 2f;
        }

        void Update()
        {
            float breeze = 0.6f;
            var w = FoundationWeatherVisuals.Active;
            if (w != null) breeze += w.WindStrength;

            float t = Time.time * _speed + _phase;
            // Two summed sines (a slow drift + a faster gust) read less mechanical than one.
            float sway = (Mathf.Sin(t) * 0.72f + Mathf.Sin(t * 0.37f + 1.3f) * 0.28f) * _amplitudeDeg * breeze;
            transform.localRotation = Quaternion.Euler(0f, 0f, sway);
        }
    }
}
