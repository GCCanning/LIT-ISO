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
   