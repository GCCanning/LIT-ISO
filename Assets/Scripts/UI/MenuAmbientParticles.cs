using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Procedural ambient particle layers for the main menu: embers rising from the
/// campfire, fireflies wandering over the valley grass, and twinkling stars in
/// the sky. Pure uGUI Images, no ParticleSystem, no per-frame allocation.
/// Embers and fireflies render as soft radial glows (additive shader when the
/// LitIso/UI/MenuGlow shader is present) to sit naturally in the painterly
/// night scene; stars stay tiny crisp points. Positions are normalized to the
/// parent rect, so any resolution works.
/// </summary>
[DisallowMultipleComponent]
public sealed class MenuAmbientParticles : MonoBehaviour
{
    // Campfire anchor in normalized screen space (x from left, y from bottom).
    // Matches the fire's position in the night-valley menu background.
    static readonly Vector2 FireAnchor = new Vector2(0.600f, 0.350f);

    const int EmberCount = 14;
    const int FireflyCount = 18;
    const int StarCount = 22;

    struct Particle
    {
        public RectTransform rt;
        public Image img;
        public Vector2 basePos;     // normalized
        public Vector2 target;      // normalized wander target (fireflies)
        public float phase, speed, life, maxLife, size;
    }

    Particle[] _embers, _flies, _stars;
    RectTransform _rect;
    Texture2D _glowTexture;
    Sprite _glowSprite;
    Material _glowMaterial;

    void Awake()
    {
        _rect = (RectTransform)transform;
        _embers = new Particle[EmberCount];
        _flies = new Particle[FireflyCount];
        _stars = new Particle[StarCount];

        Shader glowShader = Resources.Load<Shader>("Shaders/LitIsoMenuGlow");
        if (glowShader != null && glowShader.isSupported)
            _glowMaterial = new Material(glowShader) { hideFlags = HideFlags.HideAndDontSave };

        for (int i = 0; i < EmberCount; i++)
        {
            _embers[i] = Make("Ember", new Color(1f, 0.55f, 0.15f, 0f), Random.Range(5f, 9f), soft: true);
            ResetEmber(ref _embers[i], true);
        }
        for (int i = 0; i < FireflyCount; i++)
        {
            var p = Make("Firefly", new Color(0.95f, 0.92f, 0.45f, 0f), Random.Range(5f, 8f), soft: true);
            p.basePos = RandomFireflySpot();
            p.target = RandomFireflySpot();
            p.phase = Random.Range(0f, 20f);
            p.speed = Random.Range(0.006f, 0.016f);   // wander speed (normalized/s)
            _flies[i] = p;
        }
        for (int i = 0; i < StarCount; i++)
        {
            var p = Make("Star", new Color(0.85f, 0.9f, 1f, 0f), Random.Range(2f, 3.5f), soft: false);
            p.basePos = new Vector2(Random.Range(0.02f, 0.98f), Random.Range(0.68f, 0.98f));
            p.phase = Random.Range(0f, 20f);
            p.speed = Random.Range(0.08f, 0.28f);
            _stars[i] = p;
        }
    }

    // Fireflies live over the grass: full valley width, below the horizon line,
    // with a slight bias toward the darker left/foreground meadow.
    static Vector2 RandomFireflySpot()
    {
        float x = Random.Range(0.04f, 0.96f);
        float y = Random.Range(0.06f, 0.46f);
        return new Vector2(x, y);
    }

    Particle Make(string name, Color col, float size, bool soft)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(transform, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = Vector2.zero;
        rt.sizeDelta = new Vector2(size, size);
        var img = go.AddComponent<Image>();
        if (soft)
        {
            img.sprite = GetGlowSprite();
            if (_glowMaterial != null) img.material = _glowMaterial;
        }
        img.color = col;
        img.raycastTarget = false;
        return new Particle { rt = rt, img = img, size = size };
    }

    void ResetEmber(ref Particle p, bool randomizeLife)
    {
        p.basePos = FireAnchor + new Vector2(Random.Range(-0.012f, 0.012f), Random.Range(-0.01f, 0.01f));
        p.maxLife = Random.Range(3.5f, 6.5f);
        p.life = randomizeLife ? Random.Range(0f, p.maxLife) : 0f;
        p.phase = Random.Range(0f, 10f);
        p.speed = Random.Range(0.03f, 0.06f);    // rise speed — slowed per owner feedback
    }

    void Update()
    {
        float t = Time.unscaledTime;
        float dt = Time.unscaledDeltaTime;
        Vector2 sz = _rect.rect.size;
        if (sz.x < 1f) return;

        // embers: rise from the fire with wobble, fade out, respawn
        for (int i = 0; i < _embers.Length; i++)
        {
            ref var p = ref _embers[i];
            p.life += dt;
            if (p.life >= p.maxLife) ResetEmber(ref p, false);
            float k = p.life / p.maxLife;                       // 0..1
            float x = p.basePos.x + Mathf.Sin(t * 1.1f + p.phase) * 0.008f * k;
            float y = p.basePos.y + p.speed * p.life;
            float a = (k < 0.15f) ? k / 0.15f : 1f - (k - 0.15f) / 0.85f;
            p.img.color = Color.Lerp(new Color(1f, 0.62f, 0.18f), new Color(0.7f, 0.16f, 0.05f), k)
                          * new Color(1f, 1f, 1f, 0.5f * a);   // dimmed per owner feedback
            p.rt.anchoredPosition = new Vector2(x * sz.x, y * sz.y);
            float s = p.size * (1f - 0.45f * k);
            p.rt.sizeDelta = new Vector2(s, s);
        }

        // fireflies: wander toward a drifting target, blink softly. The pause/
        // re-target rhythm reads as curiosity rather than orbiting (lissajous
        // paths look mechanical over a painted scene).
        for (int i = 0; i < _flies.Length; i++)
        {
            ref var p = ref _flies[i];
            Vector2 to = p.target - p.basePos;
            float dist = to.magnitude;
            if (dist < 0.012f)
            {
                p.target = RandomFireflySpot();
            }
            else
            {
                p.basePos += to * (p.speed / Mathf.Max(0.04f, dist)) * dt;
            }

            // local jitter on top of the wander path
            float jx = Mathf.Sin(t * 1.9f + p.phase) * 0.004f;
            float jy = Mathf.Sin(t * 2.7f + p.phase * 1.3f) * 0.004f;

            // soft asymmetric blink: quick rise, slow decay
            float blink = Mathf.PerlinNoise(t * 0.55f, p.phase * 3.1f);
            float a = Mathf.Clamp01((blink - 0.35f) / 0.65f);
            a = 0.05f + 0.45f * a * a;

            var c = p.img.color; c.a = a; p.img.color = c;
            p.rt.anchoredPosition = new Vector2((p.basePos.x + jx) * sz.x, (p.basePos.y + jy) * sz.y);
        }

        // stars: gentle twinkle, fixed positions
        for (int i = 0; i < _stars.Length; i++)
        {
            ref var p = ref _stars[i];
            float a = 0.10f + 0.22f * Mathf.PerlinNoise(t * p.speed, p.phase);
            var c = p.img.color; c.a = a; p.img.color = c;
            p.rt.anchoredPosition = new Vector2(p.basePos.x * sz.x, p.basePos.y * sz.y);
        }
    }

    Sprite GetGlowSprite()
    {
        if (_glowSprite != null) return _glowSprite;

        const int size = 32;
        _glowTexture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = "AmbientParticleGlow",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.HideAndDontSave
        };

        var cols = new Color32[size * size];
        float center = (size - 1) * 0.5f;
        float inv = 1f / center;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = (x - center) * inv;
                float dy = (y - center) * inv;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                float a = Mathf.Clamp01(1f - d);
                // bright core + soft halo
                a = a * a * (0.35f + 0.65f * a);
                cols[y * size + x] = new Color(1f, 1f, 1f, a);
            }
        }

        _glowTexture.SetPixels32(cols);
        _glowTexture.Apply(false, true);
        _glowSprite = Sprite.Create(_glowTexture, new Rect(0, 0, size, size),
            new Vector2(0.5f, 0.5f), size);
        return _glowSprite;
    }

    void OnDestroy()
    {
        if (_glowSprite != null) Destroy(_glowSprite);
        if (_glowTexture != null) Destroy(_glowTexture);
        if (_glowMaterial != null) Destroy(_glowMaterial);
    }
}
