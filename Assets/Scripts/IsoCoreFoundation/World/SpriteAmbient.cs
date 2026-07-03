using UnityEngine;

namespace IsoCore.Foundation
{
    /// <summary>
    /// Shared access to the world-tint sprite material (IsoCore/SpriteAmbient) and the global
    /// "_AmbientColor" it reads. Ground tiles, props, and the player use this one material so
    /// a single Shader.SetGlobalColor (driven by AmbientLightController) tints the whole world
    /// for the day/night cycle, with no per-renderer cost and batching intact.
    /// </summary>
    public static class SpriteAmbient
    {
        static readonly int AmbientId = Shader.PropertyToID("_AmbientColor");
        static readonly int NightGradeId = Shader.PropertyToID("_NightGrade");
        static readonly int WarmExemptId = Shader.PropertyToID("_WarmExempt");
        static Material _material;
        static Material _warmMaterial;

        public static Material Material
        {
            get
            {
                if (_material == null)
                {
                    _material = Resources.Load<Material>("Materials/SpriteAmbient");
                    // Safe default so nothing renders black before the controller runs.
                    Shader.SetGlobalColor(AmbientId, Color.white);
                    Shader.SetGlobalFloat(NightGradeId, 0f);
                }
                return _material;
            }
        }

        /// <summary>
        /// Shared material for warm light sources (campfire/lantern/fireplace sprites):
        /// same shader, but exempt from the night desaturation grade and with its
        /// ambient lifted toward firelight at night so flames glow by contrast
        /// (playtest 2026-07-02 #8). Created once — never per-frame.
        /// </summary>
        public static Material WarmMaterial
        {
            get
            {
                if (_warmMaterial == null && Material != null)
                {
                    _warmMaterial = new Material(Material) { name = "SpriteAmbientWarm" };
                    _warmMaterial.SetFloat(WarmExemptId, 1f);
                }
                return _warmMaterial;
            }
        }

        /// <summary>Sets the global world tint (multiplied into every ambient-material sprite).</summary>
        public static void SetAmbient(Color c) => Shader.SetGlobalColor(AmbientId, c);

        /// <summary>Sets the global night colour-grade strength (0 = day, 1 = deep night).</summary>
        public static void SetNightGrade(float g) => Shader.SetGlobalFloat(NightGradeId, Mathf.Clamp01(g));

        static Material _waterMaterial;

        /// <summary>
        /// Shared water material (IsoCore/WaterAmbient): shimmer + shoreline foam on top
        /// of the same global ambient/night uniforms (playtest 2026-07-02 #8). Foam edges
        /// are per-renderer MaterialPropertyBlock flags. Created once — never per-frame.
        /// </summary>
        public static Material WaterMaterial
        {
            get
            {
                if (_waterMaterial == null)
                {
                    var sh = Resources.Load<Shader>("Shaders/WaterAmbient");
                    _waterMaterial = sh != null
                        ? new Material(sh) { name = "WaterAmbient" }
                        : Material; // fallback: plain ambient (no shimmer) rather than pink
                }
                return _waterMaterial;
            }
        }
    }
}
