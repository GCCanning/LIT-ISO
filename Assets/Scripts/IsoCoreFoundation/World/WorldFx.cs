using UnityEngine;

namespace IsoCore.Foundation
{
    /// <summary>
    /// Fire-and-forget particle bursts for game-feel (harvest debris, depletion pop, footstep
    /// dust). Each call spawns a short-lived ParticleSystem that plays once and self-destroys,
    /// so callers don't manage lifetime. Built entirely in code (Sprites/Default material).
    /// </summary>
    public static class WorldFx
    {
        static Material _mat;
        static Material Mat => _mat != null ? _mat
            : (_mat = new Material(Shader.Find("Sprites/Default")) { name = "WorldFxMat" });

        /// <summary>A quick outward burst of small coloured chips (e.g. wood/stone debris).</summary>
        public static void Debris(Vector3 pos, Color color, int count = 8, float size = 0.07f, float speed = 2.2f)
        {
            var go = new GameObject("FxDebris");
            go.transform.position = pos;
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.duration = 0.5f;
            main.loop = false;
            main.startLifetime = 0.45f;
            main.startSpeed = speed;
            main.startSize = size;
            main.startColor = color;
            main.gravityModifier = 1.6f;
            main.maxParticles = count;
            main.playOnAwake = false;
            main.stopAction = ParticleSystemStopAction.Destroy; // auto-cleanup

            var emission = ps.emission;
            emission.enabled = true;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.12f;

            var psr = go.GetComponent<ParticleSystemRenderer>();
            psr.material = Mat;
            psr.sortingOrder = 8000;

            ps.Play();
        }

        /// <summary>A soft upward dust puff (footsteps, landing).</summary>
        public static void Dust(Vector3 pos, int count = 4)
        {
            var go = new GameObject("FxDust");
            go.transform.position = pos;
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.duration = 0.4f;
            main.loop = false;
            main.startLifetime = 0.35f;
            main.startSpeed = 0.4f;
            main.startSize = 0.10f;
            main.startColor = new Color(0.85f, 0.82f, 0.70f, 0.5f);
            main.gravityModifier = -0.2f; // gentle rise
            main.maxParticles = count;
            main.playOnAwake = false;
            main.stopAction = ParticleSystemStopAction.Destroy;

            var emission = ps.emission;
            emission.enabled = true;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.1f;

            var psr = go.GetComponent<ParticleSystemRenderer>();
            psr.material = Mat;
            psr.sortingOrder = 8000;

            ps.Play();
        }
    }
}
