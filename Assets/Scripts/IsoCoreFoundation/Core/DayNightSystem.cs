using UnityEngine;

namespace IsoCore.Foundation
{
    /// <summary>
    /// Deterministic day/night clock. Drives a cozy night tint (rendered by the HUD)
    /// and a readable clock/phase. 0=midnight, 0.25=dawn, 0.5=noon, 0.75=dusk.
    /// </summary>
    public class DayNightSystem : MonoBehaviour
    {
        // Full cycle = 5 min day + 5 min night = 600 s. time advances 0..1 over this span,
        // so day (sun up) lasts ~300 s and night (moon up) ~300 s.
        [Range(10f, 1200f)] public float dayLengthSeconds = 600f;
        [Range(0f, 1f)] public float time = 0.30f; // start mid-morning

        [Header("Moon")]
        [Range(0f, 1f)] public float moonStrength = 0.30f; // moonlight vs. sunlight

        void Update()
        {
            if (dayLengthSeconds > 0f)
                time = Mathf.Repeat(time + Time.deltaTime / dayLengthSeconds, 1f);
        }

        // ----- Sun / Moon orbit -----------------------------------------------------
        // Sun is up for time in [0.25, 0.75) (noon at 0.5); the moon is up the other half.
        // Both follow the same off-screen arc: RISE in the NW, PEAK in the N (top of
        // screen), SET in the SW — so the cast light/shadow sweeps across the ground.

        public bool SunUp => time >= 0.25f && time < 0.75f;

        public void SetTime(float value)
        {
            time = Mathf.Repeat(value, 1f);
        }

        /// <summary>0..1 progress of the currently-active body across its arc (rise→set).</summary>
        public float BodyProgress =>
            SunUp ? (time - 0.25f) / 0.5f
                  : (time >= 0.75f ? (time - 0.75f) / 0.5f : (time + 0.25f) / 0.5f);

        /// <summary>Unit vector pointing TOWARD the light source (sun or moon).</summary>
        public Vector2 LightFromDir => ArcDir(BodyProgress);

        /// <summary>Light strength of the active body: sun up to 1, moon up to moonStrength.</summary>
        public float LightIntensity
        {
            get
            {
                float arc = Mathf.Sin(BodyProgress * Mathf.PI); // 0 at horizon, 1 at peak
                return SunUp ? arc : arc * moonStrength;
            }
        }

        /// <summary>Colour of the active light: warm sun (orange at the horizon), pale-blue moon.</summary>
        public Color LightColor
        {
            get
            {
                float horizon = Mathf.Sin(BodyProgress * Mathf.PI); // 0 at horizon, 1 at peak
                if (SunUp)
                    return Color.Lerp(new Color(1.0f, 0.55f, 0.25f),  // low sun: warm orange
                                      new Color(1.0f, 0.97f, 0.85f),  // high sun: warm white
                                      horizon);
                return new Color(0.55f, 0.62f, 0.85f);                 // moonlight: pale blue
            }
        }

        /// <summary>Dark tint for cast shadows — slightly blue at night, neutral by day.</summary>
        public Color ShadowColor =>
            SunUp ? new Color(0.06f, 0.06f, 0.09f) : new Color(0.05f, 0.07f, 0.16f);

        // RISE(NW) -> PEAK(N) -> SET(SW) as p goes 0 -> 0.5 -> 1.
        static Vector2 ArcDir(float p)
        {
            Vector2 nw = new Vector2(-1f, 1f).normalized;
            Vector2 n = new Vector2(0f, 1f);
            Vector2 sw = new Vector2(-1f, -1f).normalized;
            Vector2 d = p < 0.5f
                ? Vector2.Lerp(nw, n, p / 0.5f)
                : Vector2.Lerp(n, sw, (p - 0.5f) / 0.5f);
            return d.sqrMagnitude > 1e-6f ? d.normalized : Vector2.up;
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

        /// <summary>
        /// Fullscreen ambient overlay (drawn by the HUD). Deep blue at night, a warm
        /// orange wash at dawn/dusk as the sun sits low, and transparent at midday.
        /// </summary>
        public Color NightTint
        {
            get
            {
                float n = NightFactor;
                var night = new Color(0.05f, 0.07f, 0.20f, n * 0.55f);

                // Warm wash peaks right at dawn (0.25) and dusk (0.75), falls off quickly.
                float toEdge = Mathf.Min(Mathf.Abs(time - 0.25f), Mathf.Abs(time - 0.75f));
                float warmth = Mathf.Clamp01(1f - toEdge / 0.08f);
                if (warmth <= 0f) return night;

                var warm = new Color(0.95f, 0.45f, 0.15f, 0.22f * warmth);
                // Composite the warm wash over the night tint.
                float a = warm.a + night.a * (1f - warm.a);
                if (a < 1e-4f) return new Color(0, 0, 0, 0);
                Vector3 rgb = (new Vector3(warm.r, warm.g, warm.b) * warm.a +
                               new Vector3(night.r, night.g, night.b) * night.a * (1f - warm.a)) / a;
                return new Color(rgb.x, rgb.y, rgb.z, a);
            }
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
