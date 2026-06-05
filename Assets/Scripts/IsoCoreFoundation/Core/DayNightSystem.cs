using UnityEngine;

namespace IsoCore.Foundation
{
    /// <summary>
    /// Deterministic day/night clock. Drives a cozy night tint (rendered by the HUD)
    /// and a readable clock/phase. 0=midnight, 0.25=dawn, 0.5=noon, 0.75=dusk.
    /// </summary>
    public class DayNightSystem : MonoBehaviour
    {
        [Range(10f, 1200f)] public float dayLengthSeconds = 120f;
        [Range(0f, 1f)] public float time = 0.30f; // start mid-morning

        void Update()
        {
            if (dayLengthSeconds > 0f)
                time = Mathf.Repeat(time + Time.deltaTime / dayLengthSeconds, 1f);
        }

        /// <summary>0 at full day, 1 at deep night.</summary>
        public float NightFactor
        {
            get
            {
                float daylight = Mathf.Clamp01(Mathf.Sin(time * Mathf.PI * 2f - Mathf.PI * 0.5f) * 0.5f + 0.5f);
                return 1f - daylight;
            }
        }

        /// <summary>Translucent deep-blue overlay colour; transparent by day.</summary>
        public Color NightTint
        {
            get { float n = NightFactor; return new Color(0.05f, 0.07f, 0.20f, n * 0.55f); }
        }

        public string PhaseLabel
        {
            get
            {
                if (time < 0.22f || time >= 0.80f) return "Night";
                if (time < 0.32f) return "Dawn";
                if (time < 0.68f) return "Day";
                return "Dusk";
            }
        }

        public string Clock
        {
            get { int mins = (int)(time * 24 * 60); return $"{mins / 60:00}:{mins % 60:00}"; }
        }
    }
}
