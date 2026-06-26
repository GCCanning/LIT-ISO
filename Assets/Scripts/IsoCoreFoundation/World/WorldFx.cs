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

        /// <summary>
        /// A soft, rising smoke puff (dashes, teleports, magic). Larger, fainter and
        /// longer-lived than Dust, and it fades + grows over its lifetime so it dissipates
        /// like smoke rather than popping out.
        /// </summary>
        public static void Smoke(Vector3 pos, Color color, int count = 12, float size = 0.18f,
                                 float radius = 0.16f, float rise = 0.6f, float life = 0.55f,
                                 int sortingOrder = 9000)
        {
            var go = new GameObject("FxSmoke");
            go.transform.position = pos;
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.duration = 0.6f;
            main.loop = false;
            main.startLifetime = life;
            main.startSpeed = rise;
            main.startSize = size;
            main.startColor = color;
            main.gravityModifier = -0.35f; // gentle upward drift
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
            shape.radius = radius;

            // Fade alpha to zero over life so puffs dissolve instead of snapping out.
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(
                new[] { new GradientColorKey(color, 0f), new GradientColorKey(color, 1f) },
                new[] { new GradientAlphaKey(color.a, 0f), new GradientAlphaKey(0f, 1f) });
            col.color = grad;

            // Grow slightly as it rises for a billowing read.
            var sol = ps.sizeOverLifetime;
            sol.enabled = true;
            sol.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 0.7f, 1f, 1.4f));

            var psr = go.GetComponent<ParticleSystemRenderer>();
            psr.material = Mat;
            psr.sortingOrder = sortingOrder;

            ps.Play();
        }

        /// <summary>
        /// A line of fading smoke puffs between two points — the dash / projectile trail.
        /// Puffs grow fainter toward the start so the streak reads as pointing forward.
        /// </summary>
        public static void Trail(Vector3 from, Vector3 to, Color color, int puffs = 6, float size = 0.14f)
        {
            puffs = Mathf.Max(2, puffs);
            for (int i = 0; i < puffs; i++)
            {
                float t = i / (float)(puffs - 1);
                Vector3 p = Vector3.Lerp(from, to, t);
                var c = color;
                c.a *= Mathf.Lerp(0.35f, 0.9f, t);
                Smoke(p, c, count: 6, size: size, radius: 0.08f, rise: 0.3f, life: 0.4f);
            }
        }
    }
}
