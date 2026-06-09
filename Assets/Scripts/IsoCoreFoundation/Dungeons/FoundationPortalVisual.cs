using UnityEngine;

namespace IsoCore.Foundation
{
    [DisallowMultipleComponent]
    public sealed class FoundationPortalVisual : MonoBehaviour
    {
        SpriteRenderer _renderer;
        SpriteRenderer _glow;
        ParticleSystem _particles;
        Material _particleMaterial;
        Sprite[] _frames = System.Array.Empty<Sprite>();
        Color _tierColor = Color.white;
        Color _stateColor = Color.white;
        float _frameTimer;
        int _frame;

        const float FramesPerSecond = 8.5f;

        public static Color ColorForTier(int tier)
        {
            switch (Mathf.Clamp(tier, 1, 6))
            {
                case 1: return new Color(0.35f, 0.95f, 0.85f, 1f);
                case 2: return new Color(0.35f, 0.70f, 1.00f, 1f);
                case 3: return new Color(0.55f, 0.45f, 1.00f, 1f);
                case 4: return new Color(0.95f, 0.45f, 1.00f, 1f);
                case 5: return new Color(1.00f, 0.55f, 0.25f, 1f);
                default: return new Color(1.00f, 0.20f, 0.18f, 1f);
            }
        }

        public void Init(SpriteRenderer renderer, int tier, Color tierColor, float visualScale = 1f)
        {
            _renderer = renderer != null ? renderer : GetComponent<SpriteRenderer>();
            _frames = FoundationDungeonSpriteResolver.PortalFrames();
            _tierColor = tierColor;
            _stateColor = tierColor;
            transform.localScale = Vector3.one * Mathf.Max(0.1f, visualScale);

            if (_renderer != null)
            {
                _renderer.sprite = _frames.Length > 0 ? _frames[0] : _renderer.sprite;
                _renderer.color = _stateColor;
            }

            EnsureGlow();
            EnsureParticles(Mathf.Max(1, tier));
            ApplyColor(_stateColor);
        }

        public void ApplyColor(Color color)
        {
            _stateColor = color;
            if (_renderer != null)
                _renderer.color = color;

            if (_glow != null)
            {
                _glow.sprite = _renderer != null ? _renderer.sprite : null;
                _glow.color = new Color(color.r, color.g, color.b, 0.28f);
            }

            ApplyParticleColor(color);
        }

        void Update()
        {
            if (_renderer == null)
                _renderer = GetComponent<SpriteRenderer>();

            if (_frames == null || _frames.Length == 0 || _renderer == null)
                return;

            _frameTimer += Time.deltaTime;
            float frameDuration = 1f / FramesPerSecond;
            if (_frameTimer >= frameDuration)
            {
                _frameTimer -= frameDuration;
                _frame = (_frame + 1) % _frames.Length;
                _renderer.sprite = _frames[_frame];
                if (_glow != null)
                    _glow.sprite = _frames[_frame];
            }

            float pulse = 1f + Mathf.Sin(Time.time * 4.4f + transform.position.x * 0.13f) * 0.075f;
            if (_glow != null)
                _glow.transform.localScale = new Vector3(1.45f * pulse, 1.45f * pulse, 1f);
        }

        void EnsureGlow()
        {
            if (_glow != null || _renderer == null)
                return;

            var go = new GameObject("PortalGlow");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0f, 0.03f, 0f);
            _glow = go.AddComponent<SpriteRenderer>();
            _glow.sharedMaterial = SpriteAmbient.Material;
            _glow.sprite = _renderer.sprite;
            _glow.sortingOrder = _renderer.sortingOrder - 1;
        }

        void EnsureParticles(int tier)
        {
            if (_particles != null)
                return;

            var go = new GameObject("PortalTierParticles");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0f, 0.34f, 0f);
            _particles = go.AddComponent<ParticleSystem>();
            _particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = _particles.main;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.75f, 1.55f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.05f, 0.24f + tier * 0.035f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.035f, 0.075f + tier * 0.004f);
            main.maxParticles = 42 + tier * 8;
            main.gravityModifier = -0.015f;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;

            var emission = _particles.emission;
            emission.rateOverTime = 9f + tier * 3.5f;

            var shape = _particles.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.34f;
            shape.arc = 360f;
            shape.randomDirectionAmount = 0.34f;

            var velocity = _particles.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.Local;
            velocity.x = new ParticleSystem.MinMaxCurve(-0.16f, 0.16f);
            velocity.y = new ParticleSystem.MinMaxCurve(0.10f, 0.38f + tier * 0.035f);
            velocity.z = new ParticleSystem.MinMaxCurve(0f, 0f);

            var size = _particles.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f,
                AnimationCurve.EaseInOut(0f, 0.45f, 1f, 0.05f));

            var color = _particles.colorOverLifetime;
            color.enabled = true;

            var renderer = _particles.GetComponent<ParticleSystemRenderer>();
            _particleMaterial = new Material(Shader.Find("Sprites/Default")) { name = "FoundationPortalParticles" };
            renderer.sharedMaterial = _particleMaterial;
            renderer.sortingOrder = _renderer != null ? _renderer.sortingOrder + 2 : 2;

            ApplyParticleColor(_tierColor);
            _particles.Play(true);
        }

        void ApplyParticleColor(Color color)
        {
            if (_particles == null)
                return;

            var gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(Color.white, 0f),
                    new GradientColorKey(color, 0.25f),
                    new GradientColorKey(color, 1f),
                },
                new[]
                {
                    new GradientAlphaKey(0f, 0f),
                    new GradientAlphaKey(0.88f, 0.12f),
                    new GradientAlphaKey(0f, 1f),
                });

            var colorOverLife = _particles.colorOverLifetime;
            colorOverLife.color = new ParticleSystem.MinMaxGradient(gradient);

            if (_particleMaterial != null)
                _particleMaterial.color = color;
        }
    }
}
