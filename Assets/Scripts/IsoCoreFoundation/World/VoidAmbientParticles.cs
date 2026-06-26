using UnityEngine;

namespace IsoCore.Foundation
{
    /// <summary>
    /// Drifting ash/mote VFX for "void" cells — the empty space outside a dungeon's
    /// room/corridor + wall-ring layout (2026-06-13 dungeon overhaul, item 4).
    /// Follows the camera like <see cref="AmbientParticles"/>, but only emits while
    /// the player is near at least one "void" surface block, and fades out again once
    /// they move back over real floor. Built entirely from code — no external
    /// texture/material dependency — so it can be added to any scene that has an
    /// <see cref="IsoWorld"/> without extra asset wiring.
    /// </summary>
    [RequireComponent(typeof(ParticleSystem))]
    public class VoidAmbientParticles : MonoBehaviour
    {
        public IsoWorld world;
        public Camera cam;
        public Transform follow; // typically the player; defaults to cam if unset

        [Tooltip("Cells to scan around the follow target for 'void' surface blocks.")]
        public int scanRadius = 6;

        [Tooltip("Max particles on screen when fully over void.")]
        public int maxParticles = 60;

        const string VoidBlockId = "void";
        const float FadeSpeed = 1.5f; // intensity units per second

        ParticleSystem _ps;
        ParticleSystem.EmissionModule _emission;
        ParticleSystem.MainModule _main;
        Material _particleMat;

        float _intensity;     // 0..1, smoothed
        float _rescanTimer;
        bool _nearVoid;

        static readonly Color AshColor = new Color(0.55f, 0.55f, 0.65f, 0.5f);

        void Awake()
        {
            if (cam == null) cam = Camera.main;
            if (follow == null) follow = cam != null ? cam.transform : null;
            _ps = GetComponent<ParticleSystem>();
            _ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            _main = _ps.main;
            _main.simulationSpace = ParticleSystemSimulationSpace.World;
            _main.startLifetime = 5f;
            _main.startSpeed = 0.05f;
            _main.startSize = 0.05f;
            _main.startColor = AshColor;
            _main.maxParticles = maxParticles;
            _main.gravityModifier = 0f;
            _main.playOnAwake = false;

            _emission = _ps.emission;
            _emission.rateOverTime = 0f;

            var shape = _ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(16f, 10f, 1f); // covers the view; repositioned each frame

            var vel = _ps.velocityOverLifetime;
            vel.enabled = true;
            vel.space = ParticleSystemSimulationSpace.World;
            vel.x = new ParticleSystem.MinMaxCurve(-0.05f, 0.05f);
            vel.y = new ParticleSystem.MinMaxCurve(-0.08f, -0.01f); // gentle downward ash drift
            vel.z = new ParticleSystem.MinMaxCurve(0f, 0f);

            var rot = _ps.rotationOverLifetime;
            rot.enabled = true;
            rot.z = new ParticleSystem.MinMaxCurve(-20f, 20f);

            var col = _ps.colorOverLifetime;
            col.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.3f),
                        new GradientAlphaKey(1f, 0.7f), new GradientAlphaKey(0f, 1f) });
            col.color = new ParticleSystem.MinMaxGradient(grad);

            var psr = GetComponent<ParticleSystemRenderer>();
            _particleMat = new Material(Shader.Find("Sprites/Default")) { name = "VoidAmbientParticleMat" };
            psr.material = _particleMat;
            psr.sortingLayerName = "Default";
            psr.sortingOrder = 9000; // above the world, below UI

            _ps.Play();
        }

        void LateUpdate()
        {
            if (cam != null)
            {
                var p = cam.transform.position;
                transform.position = new Vector3(p.x, p.y, 0f);
            }

            // Rescan periodically (cheap grid sample, not every frame).
            _rescanTimer -= Time.deltaTime;
            if (_rescanTimer <= 0f)
            {
                _rescanTimer = 0.25f;
                _nearVoid = ScanForVoid();
            }

            float target = _nearVoid ? 1f : 0f;
            _intensity = Mathf.MoveTowards(_intensity, target, FadeSpeed * Time.deltaTime);
            if (_intensity <= 0f)
            {
                if (_emission.rateOverTime.constant != 0f) _emission.rateOverTime = 0f;
                return;
            }

            _emission.rateOverTime = Mathf.Lerp(0f, 16f, _intensity);
            _main.startSize = Mathf.Lerp(0.03f, 0.07f, _intensity);
        }

        bool ScanForVoid()
        {
            if (world == null || follow == null) return false;
            var c = IsoGrid.WorldToCell(follow.position);

            for (int dy = -scanRadius; dy <= scanRadius; dy++)
            for (int dx = -scanRadius; dx <= scanRadius; dx++)
            {
                var cell = world.GetCell(c.x + dx, c.y + dy);
                if (cell.SurfaceBlockId == VoidBlockId)
                    return true;
            }
            return false;
        }

        void OnDestroy()
        {
            if (_particleMat != null) Destroy(_particleMat);
        }
    }
}
