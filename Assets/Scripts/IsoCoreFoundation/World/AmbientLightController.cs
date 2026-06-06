using UnityEngine;

namespace IsoCore.Foundation
{
    /// <summary>
    /// Each frame, converts the day/night state into a global world tint (via SpriteAmbient)
    /// so the whole map — ground, props, player — warms at dawn/dusk, brightens to near-white
    /// at noon, and cools to a moonlit blue at night. One Shader.SetGlobalColor per frame.
    /// </summary>
    public class AmbientLightController : MonoBehaviour
    {
        public DayNightSystem dayNight;

        // Tuned floors/peaks. Values are multipliers on sprite colour, so kept fairly high
        // to avoid crushing the art to black.
        static readonly Color DuskFloor  = new Color(0.42f, 0.40f, 0.50f); // sun at horizon
        static readonly Color NoonColor  = new Color(1.00f, 0.99f, 0.96f); // bright daylight
        static readonly Color NightDeep  = new Color(0.30f, 0.35f, 0.55f); // no moon
        static readonly Color NightMoon  = new Color(0.48f, 0.54f, 0.74f); // full moon

        void Awake()
        {
            if (dayNight == null) dayNight = Object.FindFirstObjectByType<DayNightSystem>();
            _ = SpriteAmbient.Material; // ensures a safe default ambient is set immediately
        }

        void LateUpdate()
        {
            if (dayNight == null) return;
            SpriteAmbient.SetAmbient(Compute());
        }

        Color Compute()
        {
            float i = dayNight.LightIntensity; // sun: 0..1, moon: 0..moonStrength
            if (dayNight.SunUp)
            {
                // Horizon -> noon, warmed by the sun's own colour near the horizon.
                Color lit = Color.Lerp(dayNight.LightColor, NoonColor, i);
                return Color.Lerp(DuskFloor, lit, Mathf.Clamp01(i * 1.15f));
            }
            float m = dayNight.moonStrength > 0.001f ? Mathf.Clamp01(i / dayNight.moonStrength) : 0f;
            return Color.Lerp(NightDeep, NightMoon, m);
        }
    }
}
