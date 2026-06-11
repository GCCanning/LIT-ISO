using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Procedural ambient particle layers for the main menu: embers rising from the
/// campfire, fireflies drifting over the grass, and twinkling stars in the sky.
/// Pure uGUI Images (crisp pixel squares — fits the art), no ParticleSystem,
/// no allocation per frame. Sits above the background/scrim, below the buttons.
/// Positions are normalized to the parent rect, so any resolution works.
/// </summary>
[DisallowMultipleComponent]
public sealed class MenuAmbientParticles : MonoBehaviour
{
    // Campfire anchor in normalized screen space (x from left, y from bottom).
    // Matches the fire's position in the generated menu scene.
    static readonly Vector2 FireAnchor = new Vector2(0.485f, 0.30f);

    const int EmberCount = 14;
    const int FireflyCount = 12;
    const int StarCount = 22;

    struct Particle
    {
        public RectTransform rt;
        public Image img;
        public Vector2 basePos;     // normalized
        public float phase, speed, life, maxLife, size;
    }

    Particle[] _embers, _flies, _stars;
    RectTransform _rect;

    void Awake()
    {
        _rect = (RectTransform)transform;
        _embers = new Particle[EmberCount];
        _flies = new Particle[FireflyCount];
        _stars = new Particle[StarCount];

        for (int i = 0; i < EmberCount; i++)
        {
            _embers[i] = Make("Ember", new Color(1f, 0.55f, 0.15f, 0f), Random.Range(3f, 6f));
            ResetEmber(ref _embers[i], true);
        }
        for (int i = 0; i < FireflyCount; i++)
        {
            var p = Make("Firefly", new Color(1f, 0.85f, 0.40f, 0f), Random.Range(3f, 5f));
            p.basePos = new Vector2(Random.Range(0.05f, 0.95f), Random.Range(0.06f, 0.42f));
            p.phase = Random.Range(0f, 20f);
            p.speed = Random.Range(0.12f, 0.32f);   // slowed
            _flies[i] = p;
        }
        for (int i = 0; i < StarCount; i++)
        {
            var p = Make("Star", new Color(0.85f, 0.9f, 1f, 0f), Random.Range(2f, 3.5f));
            p.basePos = new Vector2(Random.Range(0.02f, 0.98f), Random.Range(0.68f, 0.98f));
            p.phase = Random.Range(0f, 20f);
            p.speed = Random.Range(0.08f, 0.28f);   // slowed
            _stars[i] = p;
        }
    }

    Particle Make(string name, Color col, float size)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(transform, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = Vector2.zero;
        rt.sizeDelta = new Vector2(size, size);
        var img = go.AddComponent<Image>();
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

        // fireflies: slow lissajous drift + soft blink
        for (int i = 0; i < _flies.Length; i++)
        {
            ref var p = ref _flies[i];
            float x = p.basePos.x + Mathf.Sin(t * p.speed + p.phase) * 0.025f;
            float y = p.basePos.y + Mathf.Sin(t * p.speed * 1.6f + p.phase * 1.3f) * 0.018f;
            float a = Mathf.Clamp01(0.06f + 0.30f * Mathf.Sin(t * (p.speed * 2.2f) + p.phase));   // dimmed
            var c = p.img.color; c.a = a; p.img.color = c;
            p.rt.anchoredPosition = new Vector2(x * sz.x, y * sz.y);
        }

        // stars: gentle twinkle, fixed positions
        for (int i = 0; i < _stars.Length; i++)
        {
            ref var p = ref _stars[i];
            float a = 0.10f + 0.22f * Mathf.PerlinNoise(t * p.speed, p.phase);   // dimmed
            var c = p.img.color; c.a = a; p.img.color = c;
            p.rt.anchoredPosition = new Vector2(p.basePos.x * sz.x, p.basePos.y * sz.y);
        }
    }
}
