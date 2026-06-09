using UnityEngine;

namespace IsoCore.Foundation
{
    public enum FoundationWeatherMood
    {
        Clear,
        Mist,
        Drizzle,
        Snow
    }

    /// <summary>
    /// Visual-only weather pass. It follows the camera and reacts to biome climate, day/night,
    /// and the world seed without changing gameplay yet.
    /// </summary>
    [RequireComponent(typeof(ParticleSystem))]
    public sealed class FoundationWeatherVisuals : MonoBehaviour
    {
        public static FoundationWeatherVisuals Active { get; private set; }

        public FoundationWeatherMood Mood { get; private set; } = FoundationWeatherMood.Clear;
        public float AmbientDimming { get; private set; }
        public Color AmbientTint { get; private set; } = Color.white;
        public string Label => Mood.ToString();

        DayNightSystem _dayNight;
        Camera _camera;
        IsoWorld _world;
        IsoFoundationPlayer _player;
        FoundationInstanceSystem _instances;
        int _seed;

        ParticleSystem _ps;
        ParticleSystem.MainModule _main;
        ParticleSystem.EmissionModule _emission;
        ParticleSystem.ShapeModule _shape;
        ParticleSystem.VelocityOverLifetimeModule _velocity;
        ParticleSystemRenderer _renderer;
        Material _material;

        FoundationWeatherMood _targetMood;
        float _moodBlend;
        float _nextMoodTime;

        public void Init(DayNightSystem dayNight, Camera camera, IsoWorld world,
            IsoFoundationPlayer player, FoundationInstanceSystem instances, int seed)
        {
            EnsureParticleSystemInitialized();
            _dayNight = dayNight;
            _camera = camera;
            _world = world;
            _player = player;
            _instances = instances;
            _seed = seed;
            ChooseMood(true);
        }

        void Awake()
        {
            if (Active != null && Active != this)
                Destroy(Active);
            Active = this;

            EnsureParticleSystemInitialized();
        }

        void EnsureParticleSystemInitialized()
        {
            if (_ps != null)
                return;

            if (Active == null)
                Active = this;

            _ps = GetComponent<ParticleSystem>();
            if (_ps == null)
                _ps = gameObject.AddComponent<ParticleSystem>();

            _ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            _main = _ps.main;
            _main.simulationSpace = ParticleSystemSimulationSpace.World;
            _main.loop = true;
            _main.playOnAwake = false;
            _main.maxParticles = 260;
            _main.startLifetime = 4.5f;
            _main.startSpeed = 0.1f;
            _main.startSize = 0.04f;
            _main.gravityModifier = 0f;

            _emission = _ps.emission;
            _emission.rateOverTime = 0f;

            _shape = _ps.shape;
            _shape.enabled = true;
            _shape.shapeType = ParticleSystemShapeType.Box;
            _shape.scale = new Vector3(18f, 11f, 1f);

            _velocity = _ps.velocityOverLifetime;
            _velocity.enabled = true;
            _velocity.space = ParticleSystemSimulationSpace.World;

            var colorOverLife = _ps.colorOverLifetime;
            colorOverLife.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.18f),
                        new GradientAlphaKey(1f, 0.82f), new GradientAlphaKey(0f, 1f) });
            colorOverLife.color = new ParticleSystem.MinMaxGradient(grad);

            _renderer = GetComponent<ParticleSystemRenderer>();
            _material = new Material(Shader.Find("Sprites/Default")) { name = "FoundationWeatherParticles" };
            _renderer.material = _material;
            _renderer.sortingLayerName = "Default";
            _renderer.sortingOrder = 8750;

            _ps.Play();
        }

        void OnDestroy()
        {
            if (Active == this)
                Active = null;
            if (_material != null)
                Destroy(_material);
        }

        void LateUpdate()
        {
            if (_camera == null)
                _camera = Camera.main;

            if (_camera != null)
            {
                var p = _camera.transform.position;
                transform.position = new Vector3(p.x, p.y, 0f);
                float halfH = Mathf.Max(5f, _camera.orthographicSize + 2f);
                float halfW = halfH * Mathf.Max(1f, _camera.aspect);
                _shape.scale = new Vector3(halfW * 2.1f, halfH * 2.2f, 1f);
            }

            if (Time.time >= _nextMoodTime)
                ChooseMood(false);

            bool inside = _instances != null && _instances.IsInsideInstance;
            FoundationWeatherMood target = inside ? FoundationWeatherMood.Clear : _targetMood;
            Mood = target;
            _moodBlend = Mathf.MoveTowards(_moodBlend, target == FoundationWeatherMood.Clear ? 0f : 1f,
                Time.deltaTime * 0.45f);
            ApplyMood(target, _moodBlend);
        }

        void ChooseMood(bool instant)
        {
            _nextMoodTime = Time.time + 38f + Mathf.Abs(_seed % 23);
            var biome = CurrentBiome();
            float temp = biome != null ? biome.temperature : 0.55f;
            float moisture = biome != null ? biome.moisture : 0.45f;
            float night = _dayNight != null ? _dayNight.NightFactor : 0f;
            float n = Mathf.PerlinNoise(_seed * 0.013f, Time.time * 0.008f + _seed * 0.0017f);

            if (temp < 0.34f && n > 0.35f)
                _targetMood = FoundationWeatherMood.Snow;
            else if (moisture > 0.56f && n > 0.42f)
                _targetMood = FoundationWeatherMood.Drizzle;
            else if ((moisture > 0.48f || night > 0.72f) && n > 0.62f)
                _targetMood = FoundationWeatherMood.Mist;
            else
                _targetMood = FoundationWeatherMood.Clear;

            if (instant)
            {
                Mood = _targetMood;
                _moodBlend = Mood == FoundationWeatherMood.Clear ? 0f : 1f;
                ApplyMood(Mood, _moodBlend);
            }
        }

        BiomeDefinition CurrentBiome()
        {
            if (_world == null || _player == null)
                return null;

            var c = _player.CurrentCell;
            return _world.GetBiome(c.x, c.y);
        }

        void ApplyMood(FoundationWeatherMood mood, float blend)
        {
            EnsureParticleSystemInitialized();
            float night = _dayNight != null ? _dayNight.NightFactor : 0f;
            switch (mood)
            {
                case FoundationWeatherMood.Drizzle:
                    _emission.rateOverTime = Mathf.Lerp(0f, 80f, blend);
                    _main.startLifetime = 2.1f;
                    _main.startSpeed = 0.1f;
                    _main.startSize = 0.035f;
                    _main.startColor = new Color(0.64f, 0.74f, 0.86f, Mathf.Lerp(0f, 0.50f, blend));
                    _velocity.x = new ParticleSystem.MinMaxCurve(-0.18f, -0.05f);
                    _velocity.y = new ParticleSystem.MinMaxCurve(-3.7f, -2.7f);
                    AmbientDimming = 0.18f * blend;
                    AmbientTint = Color.Lerp(Color.white, new Color(0.72f, 0.82f, 0.95f), blend);
                    break;
                case FoundationWeatherMood.Snow:
                    _emission.rateOverTime = Mathf.Lerp(0f, 42f, blend);
                    _main.startLifetime = 6.2f;
                    _main.startSpeed = 0.04f;
                    _main.startSize = 0.075f;
                    _main.startColor = new Color(0.93f, 0.97f, 1f, Mathf.Lerp(0f, 0.72f, blend));
                    _velocity.x = new ParticleSystem.MinMaxCurve(-0.32f, 0.18f);
                    _velocity.y = new ParticleSystem.MinMaxCurve(-0.62f, -0.26f);
                    AmbientDimming = 0.10f * blend;
                    AmbientTint = Color.Lerp(Color.white, new Color(0.86f, 0.92f, 1f), blend);
                    break;
                case FoundationWeatherMood.Mist:
                    _emission.rateOverTime = Mathf.Lerp(0f, 18f, blend);
                    _main.startLifetime = 7.8f;
                    _main.startSpeed = 0.02f;
                    _main.startSize = 0.18f;
                    _main.startColor = new Color(0.78f, 0.84f, 0.90f, Mathf.Lerp(0f, 0.24f + night * 0.12f, blend));
                    _velocity.x = new ParticleSystem.MinMaxCurve(-0.12f, 0.12f);
                    _velocity.y = new ParticleSystem.MinMaxCurve(0.01f, 0.06f);
                    AmbientDimming = 0.08f * blend;
                    AmbientTint = Color.Lerp(Color.white, new Color(0.82f, 0.88f, 0.96f), blend);
                    break;
                default:
                    _emission.rateOverTime = 0f;
                    AmbientDimming = Mathf.MoveTowards(AmbientDimming, 0f, Time.deltaTime * 0.25f);
                    AmbientTint = Color.white;
                    break;
            }
        }
    }
}
