using System.Collections.Generic;
using UnityEngine;

namespace IsoCore.Foundation
{
    /// <summary>
    /// Plays authored 48-frame atmosphere strips according to biome, weather, and time.
    /// This is presentation-only and renders beneath the runtime UI.
    /// </summary>
    public sealed class FoundationAtmosphereOverlay : MonoBehaviour
    {
        const string ResourceRoot = "VFX/Atmosphere/Alenia/";
        const int FrameCount = 48;
        const float FrameRate = 12f;
        const float FadeSeconds = 1.5f;

        const string Rain = "rain";
        const string RainSplash = "rain_splash";
        const string Wind = "wind";
        const string Leaves = "leaves";
        const string Fireflies = "fireflies";
        const string Fog = "fog";
        const string Snow = "snow";
        const string CinematicSnow = "snow_cinematic";
        const string GodRays = "god_rays";
        const string SwampBubbles = "swamp_bubbles";
        const string Caustics = "caustics";
        const string Embers = "embers";
        const string MagicWind = "magic_wind";
        const string Frost = "frost";
        const string Aurora = "aurora";

        DayNightSystem _dayNight;
        Camera _camera;
        IsoWorld _world;
        IsoFoundationPlayer _player;
        FoundationInstanceSystem _instances;
        FoundationWeatherVisuals _weather;

        OverlayLayer _back;
        OverlayLayer _ground;
        OverlayLayer _front;
        RainSplashField _rainSplashes;
        float _nextWaterCheck;
        bool _nearWater;

        public void Init(DayNightSystem dayNight, Camera camera, IsoWorld world,
            IsoFoundationPlayer player, FoundationInstanceSystem instances,
            FoundationWeatherVisuals weather)
        {
            _dayNight = dayNight;
            _camera = camera;
            _world = world;
            _player = player;
            _instances = instances;
            _weather = weather;

            var shader = Shader.Find("LIT-ISO/AtmosphereStrip");
            if (shader == null)
            {
                Debug.LogError("[Atmosphere] LIT-ISO/AtmosphereStrip shader was not found.");
                enabled = false;
                return;
            }

            _back = new OverlayLayer(transform, "AtmosphereBack", shader, 8680);
            _ground = new OverlayLayer(transform, "AtmosphereGround", shader, 8730);
            _front = new OverlayLayer(transform, "AtmosphereFront", shader, 8770);
            _rainSplashes = new RainSplashField(transform, shader);
            _weather.AuthoredOverlaysActive = true;
        }

        void LateUpdate()
        {
            if (_weather == null || _back == null)
                return;

            if (_camera == null)
                _camera = Camera.main;
            if (_camera == null)
                return;

            var cameraPosition = _camera.transform.position;
            transform.position = new Vector3(cameraPosition.x, cameraPosition.y, 0f);

            float viewportHeight = _camera.orthographicSize * 2f;
            float viewportWidth = viewportHeight * _camera.aspect;
            float coverScale = Mathf.Max(viewportWidth / 3.2f, viewportHeight / 1.8f) * 1.02f;
            _back.SetLayout(coverScale, 0f);
            _ground.SetLayout(coverScale, 0f);
            _front.SetLayout(coverScale, 0f);
            _rainSplashes.SetLayout(coverScale, viewportHeight);

            bool inside = _instances != null && _instances.IsInsideInstance;
            ResolveTargets(inside, out var back, out var backOpacity,
                out var ground, out var groundOpacity, out var front, out var frontOpacity);

            _back.SetTarget(back, backOpacity);
            _ground.SetTarget(ground, groundOpacity);
            _front.SetTarget(front, frontOpacity, front == Rain ? 1.18f : 1f);
            _rainSplashes.SetActive(!inside && _weather.Mood == FoundationWeatherMood.Drizzle);

            int frame = Mathf.FloorToInt(Time.unscaledTime * FrameRate) % FrameCount;
            _back.Tick(Time.unscaledDeltaTime, frame);
            _ground.Tick(Time.unscaledDeltaTime, frame);
            _front.Tick(Time.unscaledDeltaTime, frame);
            _rainSplashes.Tick(Time.unscaledDeltaTime, frame);
        }

        void ResolveTargets(bool inside,
            out string back, out float backOpacity,
            out string ground, out float groundOpacity,
            out string front, out float frontOpacity)
        {
            back = null;
            ground = null;
            front = null;
            backOpacity = groundOpacity = frontOpacity = 0f;
            if (inside)
                return;

            string biome = _weather.CurrentBiomeId;
            var mood = _weather.Mood;
            float time = _dayNight != null ? _dayNight.time : 0.5f;
            bool dawn = time >= 0.22f && time < 0.32f;
            bool dusk = time >= 0.68f && time < 0.80f;
            bool night = time < 0.22f || time >= 0.80f;

            if (Time.time >= _nextWaterCheck)
            {
                _nearWater = IsNearWater();
                _nextWaterCheck = Time.time + 1f;
            }

            switch (mood)
            {
                case FoundationWeatherMood.Drizzle:
                    front = Rain;
                    frontOpacity = 0.72f;
                    break;
                case FoundationWeatherMood.Snow:
                    front = biome == "frozenmountain" ? CinematicSnow : Snow;
                    frontOpacity = biome == "frozenmountain" ? 0.58f : 0.72f;
                    if (biome == "frozenmountain")
                    {
                        ground = Frost;
                        groundOpacity = 0.12f;
                    }
                    break;
                case FoundationWeatherMood.Wind:
                    front = Wind;
                    frontOpacity = biome == "mountain" || biome == "frozenmountain" ? 0.62f : 0.46f;
                    if (biome == "forest")
                    {
                        ground = Leaves;
                        groundOpacity = 0.62f;
                    }
                    else if (biome == "badlands")
                    {
                        ground = Embers;
                        groundOpacity = 0.40f;
                    }
                    else if (biome == "sunspool" && (dusk || night))
                    {
                        ground = MagicWind;
                        groundOpacity = 0.28f;
                    }
                    break;
                case FoundationWeatherMood.Mist:
                    back = Fog;
                    backOpacity = biome == "marsh" || biome == "grotto" ? 0.24f : 0.16f;
                    break;
            }

            if (ground == null && biome == "marsh" && mood != FoundationWeatherMood.Drizzle)
            {
                ground = SwampBubbles;
                groundOpacity = 0.52f;
            }

            if (back == null && _nearWater && (biome == "beach" || biome == "grotto"))
            {
                back = Caustics;
                backOpacity = 0.10f;
            }

            if (mood != FoundationWeatherMood.Clear)
                return;

            if (back == null && dawn && (biome == "meadow" || biome == "forest"))
            {
                back = GodRays;
                backOpacity = biome == "forest" ? 0.18f : 0.13f;
            }
            else if (back == null && night && (biome == "snow" || biome == "frozenmountain"))
            {
                back = Aurora;
                backOpacity = biome == "frozenmountain" ? 0.24f : 0.18f;
            }
            else if (back == null && biome == "grotto")
            {
                back = Fog;
                backOpacity = 0.16f;
            }

            if ((dusk || night) && (biome == "meadow" || biome == "forest" || biome == "marsh"))
            {
                front = Fireflies;
                frontOpacity = biome == "marsh" ? 0.78f : 0.66f;
            }
            else if ((dusk || night) && (biome == "grotto" || biome == "sunspool"))
            {
                front = MagicWind;
                frontOpacity = biome == "grotto" ? 0.34f : 0.24f;
            }
        }

        bool IsNearWater()
        {
            if (_world == null || _player == null)
                return false;

            var center = _player.CurrentCell;
            for (int y = -2; y <= 2; y++)
            for (int x = -2; x <= 2; x++)
                if (_world.GetCell(center.x + x, center.y + y).Water)
                    return true;
            return false;
        }

        void OnDestroy()
        {
            _back?.Dispose();
            _ground?.Dispose();
            _front?.Dispose();
            _rainSplashes?.Dispose();
            if (_weather != null)
                _weather.AuthoredOverlaysActive = false;
        }

        sealed class OverlayLayer
        {
            readonly GameObject _gameObject;
            readonly SpriteRenderer _renderer;
            readonly Material _material;
            readonly float _frameHeightFraction;
            readonly int _framePhase;

            string _current;
            string _target;
            Texture2D _texture;
            Sprite _sprite;
            float _opacity;
            float _targetOpacity;
            float _tiling = 1f;

            public OverlayLayer(Transform parent, string name, Shader shader, int sortingOrder,
                float frameHeightFraction = 1f, int framePhase = 0)
            {
                _frameHeightFraction = Mathf.Clamp(frameHeightFraction, 0.05f, 1f);
                _framePhase = framePhase;
                _gameObject = new GameObject(name);
                _gameObject.transform.SetParent(parent, false);
                _renderer = _gameObject.AddComponent<SpriteRenderer>();
                _renderer.sortingLayerName = "Default";
                _renderer.sortingOrder = sortingOrder;
                _material = new Material(shader) { name = name + "Material" };
                _renderer.material = _material;
                _renderer.enabled = false;
            }

            public void SetLayout(float scale, float localY)
            {
                _gameObject.transform.localScale = new Vector3(scale, scale, 1f);
                _gameObject.transform.localPosition = new Vector3(0f, localY, 0f);
            }

            public void SetTarget(string effect, float opacity, float tiling = 1f)
            {
                _target = effect;
                _targetOpacity = Mathf.Clamp01(opacity);
                _tiling = Mathf.Max(1f, tiling);
            }

            public void Tick(float deltaTime, int frame)
            {
                float fadeStep = FadeSeconds > 0f ? deltaTime / FadeSeconds : 1f;
                if (_current != _target)
                {
                    _opacity = Mathf.MoveTowards(_opacity, 0f, fadeStep);
                    if (_opacity <= 0.001f)
                        Load(_target);
                }
                else
                {
                    _opacity = Mathf.MoveTowards(_opacity, _targetOpacity, fadeStep);
                }

                _renderer.enabled = _texture != null && _opacity > 0.001f;
                if (!_renderer.enabled)
                    return;

                int phasedFrame = (frame + _framePhase) % FrameCount;
                _material.SetFloat("_FrameOffset", phasedFrame / (float)FrameCount);
                _material.SetColor("_Color", new Color(1f, 1f, 1f, _opacity));
                _material.SetVector("_Tiling", new Vector4(_tiling, _tiling, 0f, 0f));
            }

            void Load(string effect)
            {
                if (_sprite != null)
                {
                    Object.Destroy(_sprite);
                    _sprite = null;
                }
                if (_texture != null)
                {
                    TexturePool.Release(_current);
                    _texture = null;
                }

                _current = effect;
                if (string.IsNullOrEmpty(effect))
                    return;

                _texture = TexturePool.Acquire(effect);
                if (_texture == null)
                {
                    Debug.LogWarning($"[Atmosphere] Missing effect strip '{effect}'.");
                    _current = null;
                    return;
                }

                float frameWidth = _texture.width / (float)FrameCount;
                float frameHeight = _texture.height * _frameHeightFraction;
                _sprite = Sprite.Create(_texture, new Rect(0f, 0f, frameWidth, frameHeight),
                    new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
                _renderer.sprite = _sprite;
            }

            public void Dispose()
            {
                if (_sprite != null)
                    Object.Destroy(_sprite);
                if (_texture != null)
                    TexturePool.Release(_current);
                Object.Destroy(_material);
                Object.Destroy(_gameObject);
            }
        }

        sealed class RainSplashField
        {
            const float CropFraction = 0.20f;
            static readonly float[] Baselines = { 0.32f, 0.08f, -0.16f, -0.40f };
            static readonly float[] Opacities = { 0.30f, 0.42f, 0.56f, 0.70f };
            readonly OverlayLayer[] _bands;

            public RainSplashField(Transform parent, Shader shader)
            {
                _bands = new[]
                {
                    new OverlayLayer(parent, "RainSplashFar", shader, 8721, CropFraction, 5),
                    new OverlayLayer(parent, "RainSplashMidFar", shader, 8722, CropFraction, 17),
                    new OverlayLayer(parent, "RainSplashMidNear", shader, 8723, CropFraction, 29),
                    new OverlayLayer(parent, "RainSplashNear", shader, 8724, CropFraction, 41),
                };
            }

            public void SetLayout(float coverScale, float viewportHeight)
            {
                float halfBandHeight = 1.8f * CropFraction * coverScale * 0.5f;
                for (int i = 0; i < _bands.Length; i++)
                    _bands[i].SetLayout(coverScale, Baselines[i] * viewportHeight + halfBandHeight);
            }

            public void SetActive(bool active)
            {
                for (int i = 0; i < _bands.Length; i++)
                    _bands[i].SetTarget(active ? RainSplash : null, active ? Opacities[i] : 0f);
            }

            public void Tick(float deltaTime, int frame)
            {
                for (int i = 0; i < _bands.Length; i++)
                    _bands[i].Tick(deltaTime, frame);
            }

            public void Dispose()
            {
                for (int i = 0; i < _bands.Length; i++)
                    _bands[i].Dispose();
            }
        }

        static class TexturePool
        {
            sealed class Entry
            {
                public Texture2D Texture;
                public int References;
            }

            static readonly Dictionary<string, Entry> Entries = new Dictionary<string, Entry>();

            public static Texture2D Acquire(string effect)
            {
                if (!Entries.TryGetValue(effect, out var entry))
                {
                    var texture = Resources.Load<Texture2D>(ResourceRoot + effect);
                    if (texture == null)
                        return null;
                    texture.filterMode = FilterMode.Point;
                    texture.wrapMode = TextureWrapMode.Clamp;
                    entry = new Entry { Texture = texture };
                    Entries.Add(effect, entry);
                }

                entry.References++;
                return entry.Texture;
            }

            public static void Release(string effect)
            {
                if (string.IsNullOrEmpty(effect) || !Entries.TryGetValue(effect, out var entry))
                    return;

                entry.References--;
                if (entry.References > 0)
                    return;

                Entries.Remove(effect);
                Resources.UnloadAsset(entry.Texture);
            }
        }
    }
}
