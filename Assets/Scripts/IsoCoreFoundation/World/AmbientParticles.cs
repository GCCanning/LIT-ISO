using UnityEngine;

namespace IsoCore.Foundation
{
    /// <summary>
    /// Atmospheric motes that follow the camera: soft drifting pollen by day, glowing
    /// fireflies at night. Built entirely from code (one ParticleSystem) and cross-faded by
    /// the day/night cycle. World simulation space + a camera-sized emitter box means motes
    /// drift in the world while new ones always spawn within view.
    /// </summary>
    [RequireComponent(typeof(ParticleSystem))]
    public class AmbientParticles : MonoBehaviour
    {
        public DayNightSystem dayNight;
        public Camera cam;

        [Tooltip("Max particles on screen at full density.")]
        public int maxParticles = 90;

        ParticleSystem _ps;
        ParticleSystem.EmissionModule _emission;
        ParticleSystem.MainModule _main;
        Material _particleMat;

        static readonly Color Pollen   = new Color(1.00f, 0.97f, 0.75f, 0.30f);
        static readonly Color Firefly  = new Color(0.75f, 1.00f, 0.45f, 0.95f);

        void Awake()
        {
            if (dayNight == null) dayNight = Object.FindFirstObjectByType<DayNightSystem>();
            if (cam == null) cam = Camera.main;

            _ps = GetComponent<ParticleSystem>();
            _ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            _main = _ps.main;
            _main.simulationSpace = ParticleSystemSimulationSpace.World;
            _main.startLifetime = 7f;
            _main.startSpeed = 0.15f;
            _main.startSize = 0.06f;
            _main.maxParticles = maxParticles;
            _main.gravityModifier = 0f;
            _main.playOnAwake = false;

            _emission = _ps.emission;
            _emission.rateOverTime = 12f;

            var shape = _ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(16f, 10f, 1f); // covers the view; repositioned each frame

            var vel = _ps.velocityOverLifetime;
            vel.enabled = true;
            vel.space = ParticleSystemSimulationSpace.World;
            // All three axes must share the same curve mode (TwoConstants here) or Unity
            // logs "Particle Velocity curves must all be in the same mode" every frame.
            vel.x = new ParticleSystem.MinMaxCurve(-0.12f, 0.12f);
            vel.y = new ParticleSystem.MinMaxCurve(0.02f, 0.18f); // gentle upward drift
            vel.z = new ParticleSystem.MinMaxCurve(0f, 0f);

            // Soft fade in/out over life.
            var col = _ps.colorOverLifetime;
            col.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.25f),
                        new GradientAlphaKey(1f, 0.75f), new GradientAlphaKey(0f, 1f) });
            col.color = new ParticleSystem.MinMaxGradient(grad);

            // Twinkle (fireflies) via size pulsing.
            var sol = _ps.sizeOverLifetime;
            sol.enabled = true;
            sol.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 0.6f, 1f, 1.2f));

            // Additive, soft round particle so fireflies glow.
            var psr = GetComponent<ParticleSystemRenderer>();
            _particleMat = new Material(Shader.Find("Sprites/Default")) { name = "AmbientParticleMat" };
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
            if (dayNight == null) return;

            float night = dayNight.NightFactor;          // 0 day, 1 deep night
            // Fireflies appear at night, pollen by day; cross-fade colour + density.
            Color c = Color.Lerp(Pollen, Firefly, night);
            _main.startColor = c;
            _emission.rateOverTime = Mathf.Lerp(10f, 22f, night); // a few more at night
            _main.startSize = Mathf.Lerp(0.055f, 0.085f, night);
        }

        void OnDestroy()
        {
            if (_particleMat != null) Destroy(_particleMat);
        }
    }
}
