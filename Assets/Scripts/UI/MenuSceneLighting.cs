using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Animated lighting overlays for the menu background (night valley scene):
///   - warm flickering glow at the campfire (additive, heat-shimmer shader),
///   - soft window light spilling from the cabin, with a gentle lamp flicker,
///   - cool pulsing glow at the portal in the treeline,
///   - slow dawn wash on the left horizon.
/// Uses the LitIso/UI/MenuGlow additive shader when available (Resources/Shaders),
/// falling back to plain alpha-blended uGUI Images otherwise.
/// </summary>
[DisallowMultipleComponent]
public sealed class MenuSceneLighting : MonoBehaviour
{
    [Tooltip("Normalized campfire position in the background image.")]
    public Vector2 campfireAnchor = new Vector2(0.600f, 0.350f);
    [Tooltip("Normalized cabin-window position in the background image.")]
    public Vector2 cabinAnchor = new Vector2(0.638f, 0.585f);
    [Tooltip("Normalized portal position in the right treeline.")]
    public Vector2 portalAnchor = new Vector2(0.845f, 0.660f);
    [Tooltip("Normalized dawn position on the left horizon.")]
    public Vector2 sunriseAnchor = new Vector2(0.07f, 0.82f);

    [Range(0f, 1f)] public float campfireGlowAlpha = 0.30f;
    [Range(0f, 1f)] public float cabinGlowAlpha = 0.20f;
    [Range(0f, 1f)] public float portalGlowAlpha = 0.22f;
    [Range(0f, 1f)] public float sunriseGlowAlpha = 0.20f;
    public float campfirePulseSeconds = 2.8f;
    public float portalPulseSeconds = 6.5f;
    public float sunrisePulseSeconds = 11f;

    Image _campfireGlow;
    Image _cabinGlow;
    Image _portalGlow;
    Image _sunriseGlow;
    Texture2D _radialTexture;
    Material _glowMaterial;

    void Awake()
    {
        var rt = (RectTransform)transform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        // One shared additive glow material (heat shimmer + organic flicker).
        // Missing/unsupported shader → null material → default UI rendering.
        Shader glowShader = Resources.Load<Shader>("Shaders/LitIsoMenuGlow");
        if (glowShader != null && glowShader.isSupported)
            _glowMaterial = new Material(glowShader) { hideFlags = HideFlags.HideAndDontSave };

        // Painted back-to-front: dawn, portal, cabin, campfire.
        _sunriseGlow = CreateGlow("SunriseWash", sunriseAnchor, new Vector2(900f, 560f),
            new Color(1f, 0.74f, 0.25f, sunriseGlowAlpha), additive: false);
        _portalGlow = CreateGlow("PortalGlow", portalAnchor, new Vector2(230f, 260f),
            new Color(0.45f, 0.62f, 1f, portalGlowAlpha), additive: true);
        _cabinGlow = CreateGlow("CabinWindowGlow", cabinAnchor, new Vector2(190f, 130f),
            new Color(1f, 0.66f, 0.28f, cabinGlowAlpha), additive: true);
        _campfireGlow = CreateGlow("CampfireGlow", campfireAnchor, new Vector2(470f, 330f),
            new Color(1f, 0.45f, 0.10f, campfireGlowAlpha), additive: true);
    }

    void Update()
    {
        float t = Time.unscaledTime;

        if (_campfireGlow != null)
        {
            // fire: fast noise flicker + slow breathe
            float pulse = 0.72f + 0.28f * Mathf.PerlinNoise(t * 1.7f, 4.3f);
            SetAlpha(_campfireGlow, campfireGlowAlpha * pulse);
            float scale = 0.96f + 0.05f * Mathf.Sin(t * (2f * Mathf.PI / Mathf.Max(0.1f, campfirePulseSeconds)));
            _campfireGlow.rectTransform.localScale = new Vector3(scale, scale, 1f);
        }

        if (_cabinGlow != null)
        {
            // lamp light: mostly steady with a soft occasional waver
            float waver = 0.88f + 0.12f * Mathf.PerlinNoise(t * 0.9f, 11.7f);
            SetAlpha(_cabinGlow, cabinGlowAlpha * waver);
        }

        if (_portalGlow != null)
        {
            // portal: slow magical pulse + faint scale breathe, slightly eerie
            float pulse = 0.65f + 0.35f * Mathf.PerlinNoise(t * 0.45f, 23.1f);
            SetAlpha(_portalGlow, portalGlowAlpha * pulse);
            float scale = 0.94f + 0.08f * Mathf.Sin(t * (2f * Mathf.PI / Mathf.Max(0.1f, portalPulseSeconds)));
            _portalGlow.rectTransform.localScale = new Vector3(scale, scale, 1f);
        }

        if (_sunriseGlow != null)
        {
            float pulse = 0.82f + 0.18f * Mathf.Sin(t * (2f * Mathf.PI / Mathf.Max(0.1f, sunrisePulseSeconds)));
            SetAlpha(_sunriseGlow, sunriseGlowAlpha * pulse);
        }
    }

    static void SetAlpha(Image img, float a)
    {
        Color c = img.color;
        c.a = a;
        img.color = c;
    }

    Image CreateGlow(string name, Vector2 anchor, Vector2 size, Color color, bool additive)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(transform, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = anchor;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = size;

        var img = go.AddComponent<Image>();
        img.sprite = GetRadialSprite();
        img.color = color;
        img.raycastTarget = false;
        img.preserveAspect = false;
        if (additive && _glowMaterial != null)
            img.material = _glowMaterial;
        return img;
    }

    void OnDestroy()
    {
        // All glow Images share one sprite + one texture + one material.
        if (_sunriseGlow != null && _sunriseGlow.sprite != null) Destroy(_sunriseGlow.sprite);
        if (_radialTexture != null) Destroy(_radialTexture);
        if (_glowMaterial != null) Destroy(_glowMaterial);
    }

    Sprite _radialSprite;

    Sprite GetRadialSprite()
    {
        if (_radialSprite != null) return _radialSprite;

        const int size = 96;
        _radialTexture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = "MenuGlowRadial",
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
                a = a * a * (3f - 2f * a);   // smoothstep falloff
                cols[y * size + x] = new Color(1f, 1f, 1f, a);
            }
        }

        _radialTexture.SetPixels32(cols);
        _radialTexture.Apply(false, true);
        _radialSprite = Sprite.Create(_radialTexture, new Rect(0, 0, size, size),
            new Vector2(0.5f, 0.5f), size);
        return _radialSprite;
    }
}
