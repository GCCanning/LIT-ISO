using UnityEngine;

namespace IsoCore.Foundation
{
    public enum FoundationWeatherMood
    {
        Clear,
        Mist,
        Drizzle,
        Snow,
        Wind
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
        /// <summary>0..1 breeze strength for WindSway, scaled by current mood + blend.</summary>
        public float WindStrength { get; private set; }
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

            WindStrength = (target switch
            {
                FoundationWeatherMood.Wind => 1.0f,
                FoundationWeatherMood.Drizzle => 0.5f,
                FoundationWeatherMood.Snow => 0.3f,
                FoundationWeatherMood.Mist => 0.15f,
                _ => 0f,
            }) * _moodBlend;

            UpdateLightningFlash();
        }

        float _flash;
        float _nextFlashTime;

        // Occasional lightning brightening during sustained rain. Modulates AmbientTint (which
        // AmbientLightController already reads) toward white for a quick flash — no new render path.
        void UpdateLightningFlash()
        {
            if (Mood == FoundationWeatherMood.Drizzle && _moodBlend > 0.6f && Time.time >= _nextFlashTime)
            {
                _flash = 1f;
                _nextFlashTime = Time.time + Random.Range(8f, 18f);
            }
            if (_flash <= 0f) return;
            _flash = Mathf.MoveTowards(_flash, 0f, Time.deltaTime * 5.5f);
            AmbientTint = Color.Lerp(AmbientTint, new Color(1.7f, 1.7f, 1.85f), _flash);
            AmbientDimming *= (1f - _flash);
        }

        void ChooseMood(bool instant)
        {
            var biome = CurrentBiome();
            string biomeId = biome != null ? biome.id : null;

            // Biome-driven mapping (originally the Ring-of-Biomes showcase mapping,
            // generalized to ALL worlds — task #25/#26 wired this up but it was only
            // ever reachable via seed 240611, which the normal launch flow never uses,
            // so players never saw it). Snow/mountain/forest get an immediate,
            // reliable per-biome weather mood, rechecked every 1.5s so walking between
            // biomes swaps weather promptly. Other biomes (meadow/beach/desert/etc.)
            // fall through to the slower noise-based climate reroll below for variety.
            if (TryMoodForBiomeId(biomeId, out var mappedMood))
            {
                _nextMoodTime = Time.time + 1.5f;
                _targetMood = mappedMood;

                if (instant)
                {
                    Mood = _targetMood;
                    _moodBlend = Mood == FoundationWeatherMood.Clear ? 0f : 1f;
                    ApplyMood(Mood, _moodBlend);
                }
                return;
            }

            _nextMoodTime = Time.time + 38f + Mathf.Abs(_seed % 23);
            float temp = biome != null ? biome.temperature : 0.55f;
            float moisture = biome != null ? biome.moisture : 0.45f;
            float night = _dayNight != null ? _dayNight.NightFactor : 0f;
            float n = Mathf.PerlinNoise(_seed * 0.013f, Time.time * 0.008f + _seed * 0.0017f);

            if (temp < 0.34f && moisture < 0.30f && n > 0.30f)
                _targetMood = FoundationWeatherMood.Wind;
            else if (temp < 0.34f && n > 0.35f)
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

        /// <summary>
        /// Per-biome immediate weather mapping (originally the Ring-of-Biomes
        /// showcase mapping, now used in all worlds):
        ///   snow -> Snow (snowfall), mountain -> Wind (dust/wind streaks),
        ///   forest -> Mist (light fog/leaf haze).
        /// Other biomes (meadow, beach, desert, etc.) return false so the caller
        /// falls back to the slower climate/noise-based reroll.
        /// </summary>
        static bool TryMoodForBiomeId(string biomeId, out FoundationWeatherMood mood)
        {
            switch (biomeId)
            {
                case "snow": mood = FoundationWeatherMood.Snow; return true;
                case "mountain": mood = FoundationWeatherMood.Wind; return true;
                case "forest": mood = FoundationWeatherMood.Mist; return true;
                default: mood = FoundationWeatherMood.Clear; return false;
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
                case FoundationWeatherMood.Wind:
                    // Mountain quadrant: fast horizontal dust/wind streaks, low alpha.
                    _emission.rateOverTime = Mathf.Lerp(0f, 55f, blend);
                    _main.startLifetime = 1.6f;
                    _main.startSpeed = 0.06f;
                    _main.startSize = 0.05f;
                    _main.startColor = new Color(0.78f, 0.72f, 0.62f, Mathf.Lerp(0f, 0.35f, blend));
                    _velocity.x = new ParticleSystem.MinMaxCurve(2.2f, 3.6f);
                    _velocity.y = new ParticleSystem.MinMaxCurve(-0.25f, 0.25f);
                    AmbientDimming = 0.06f * blend;
                    AmbientTint = Color.Lerp(Color.white, new Color(0.92f, 0.88f, 0.80f), blend);
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
