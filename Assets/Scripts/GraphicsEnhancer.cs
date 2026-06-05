using UnityEngine;

/// <summary>
/// Adds atmospheric polish to the scene without requiring URP, post-processing
/// stack, or any custom shaders. Built only with built-in Unity components.
///
/// Effects applied (all toggleable):
///   1. Vignette overlay   — Subtle darkening at the edges of the screen
///   2. Color grading      — Warm/cool tint that breathes with day/night
///   3. Atmospheric particles — Floating dust motes (day) / fireflies (night)
///   4. Soft sprite outline glow — Subtle rim around the player at night
///
/// Drop this on any GameObject in the scene (e.g. on the Main Camera).
/// Auto-finds DayNightMusicManager, SunController, and main camera on Start.
/// </summary>
[DisallowMultipleComponent]
public class GraphicsEnhancer : MonoBehaviour
{
    // -------------------------------------------------------------------------
    // Vignette
    // -------------------------------------------------------------------------

    [Header("Vignette (edge darkening)")]
    public bool enableVignette = true;

    [Tooltip("Strength of edge darkening. 0 = no vignette, 1 = strong.")]
    [Range(0f, 1f)]
    public float vignetteStrength = 0.5f;

    [Tooltip("Size of the clear center area. Larger = vignette only on far edges.")]
    [Range(0.2f, 1.5f)]
    public float vignetteRadius = 0.85f;

    [Tooltip("Color of the vignette (typically near-black).")]
    public Color vignetteColor = new Color(0f, 0f, 0f, 1f);

    // -------------------------------------------------------------------------
    // Atmospheric Particles
    // -------------------------------------------------------------------------

    [Header("Atmospheric Particles")]
    [Tooltip("Spawn dust motes / fireflies floating in the air.")]
    public bool enableAtmosphericParticles = true;

    [Tooltip("How many particles to spawn (kept low for performance).")]
    [Range(10, 200)]
    public int particleCount = 60;

    [Tooltip("Radius around the camera where particles spawn (world units).")]
    public float particleSpawnRadius = 8f;

    [Tooltip("Color of dust during day time.")]
    public Color dustDayColor = new Color(1f, 0.95f, 0.7f, 0.25f);

    [Tooltip("Color of fireflies during night time.")]
    public Color fireflyNightColor = new Color(0.9f, 1f, 0.4f, 0.9f);

    // -------------------------------------------------------------------------
    // Color Grading (tilemap/sprite tints already handled by IsoLightingController)
    // -------------------------------------------------------------------------

    [Header("Color Grading Polish")]
    [Tooltip("Additional saturation boost for color profiles (1 = normal).")]
    [Range(0.5f, 1.5f)]
    public float saturationBoost = 1.1f;

    // -------------------------------------------------------------------------
    // Cloud Shadows
    // -------------------------------------------------------------------------

    [Header("Cloud Shadows")]
    public bool enableCloudShadows = true;
    public float cloudShadowStrength = 0.12f;
    public float cloudScale = 0.5f;
    public float cloudSpeed = 0.2f;

    // -------------------------------------------------------------------------
    // Light Beams (God Rays)
    // -------------------------------------------------------------------------

    [Header("Light Beams (God Rays)")]
    public bool enableLightBeams = true;
    public Color beamColor = new Color(1f, 0.9f, 0.7f, 0.05f);
    public float beamIntensity = 0.4f;

    // -------------------------------------------------------------------------
    // References (auto-wired)
    // -------------------------------------------------------------------------

    [Header("References")]
    public Camera targetCamera;
    public SunController sunController;
    public DayNightMusicManager cycleManager;

    // -------------------------------------------------------------------------
    // Internal state
    // -------------------------------------------------------------------------

    private GameObject vignetteObject;
    private Canvas vignetteCanvas;
    private UnityEngine.UI.RawImage vignetteImage;

    private GameObject cloudShadowObject;
    private UnityEngine.UI.RawImage cloudShadowImage;
    private Material cloudMaterial;

    private ParticleSystem atmosphereParticles;
    private ParticleSystem.MainModule particleMain;
    private ParticleSystemRenderer particleRenderer;

    private GameObject beamObject;
    private UnityEngine.UI.RawImage beamImage;

    // -------------------------------------------------------------------------
    // Lifecycle
    // -------------------------------------------------------------------------

    private void Start()
    {
        AutoWireReferences();

        if (enableVignette)
        {
            CreateVignetteOverlay();
        }

        if (enableCloudShadows)
        {
            CreateCloudShadows();
        }

        if (enableLightBeams)
        {
            CreateLightBeams();
        }

        if (enableAtmosphericParticles)
        {
            CreateAtmosphereParticles();
        }
    }

    private void Update()
    {
        if (cycleManager == null) return;

        float t = cycleManager.normalizedCycleTime;

        // Drive particles' day/night color based on cycle time
        if (enableAtmosphericParticles && atmosphereParticles != null)
        {
            UpdateAtmosphereParticles();
        }

        if (enableCloudShadows && cloudShadowImage != null)
        {
            UpdateCloudShadows();
        }

        if (enableLightBeams && beamImage != null)
        {
            UpdateLightBeams(t);
        }
    }

    private void AutoWireReferences()
    {
        if (targetCamera == null) targetCamera = GetComponent<Camera>() ?? Camera.main;
        if (sunController == null) sunController = FindFirstObjectByType<SunController>();
        if (cycleManager == null) cycleManager = FindFirstObjectByType<DayNightMusicManager>();
    }

    // -------------------------------------------------------------------------
    // Cloud Shadows
    // -------------------------------------------------------------------------

    private void CreateCloudShadows()
    {
        cloudShadowObject = new GameObject("CloudShadows");
        cloudShadowObject.transform.SetParent(vignetteCanvas != null ? vignetteCanvas.transform : transform, false);
        
        RectTransform rect = cloudShadowObject.AddComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        cloudShadowImage = cloudShadowObject.AddComponent<UnityEngine.UI.RawImage>();
        cloudShadowImage.texture = GenerateCloudTexture();
        cloudShadowImage.color = new Color(0, 0, 0, cloudShadowStrength);
        cloudShadowImage.raycastTarget = false;
        
        cloudShadowObject.transform.SetAsFirstSibling(); // Draw behind vignette
    }

    private Texture2D GenerateCloudTexture()
    {
        const int size = 128;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, true)
        {
            wrapMode = TextureWrapMode.Repeat,
            filterMode = FilterMode.Bilinear
        };

        Color[] pixels = new Color[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float noiseVal = Mathf.PerlinNoise(x * 0.05f, y * 0.05f);
                pixels[y * size + x] = new Color(1, 1, 1, noiseVal);
            }
        }
        tex.SetPixels(pixels);
        tex.Apply();
        return tex;
    }

    private void UpdateCloudShadows()
    {
        Vector2 uvOffset = new Vector2(Time.time * cloudSpeed * 0.1f, Time.time * cloudSpeed * 0.05f);
        cloudShadowImage.uvRect = new Rect(uvOffset.x, uvOffset.y, cloudScale, cloudScale);
    }

    // -------------------------------------------------------------------------
    // Light Beams (God Rays)
    // -------------------------------------------------------------------------

    private void CreateLightBeams()
    {
        beamObject = new GameObject("LightBeams");
        beamObject.transform.SetParent(vignetteCanvas != null ? vignetteCanvas.transform : transform, false);
        
        RectTransform rect = beamObject.AddComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        beamImage = beamObject.AddComponent<UnityEngine.UI.RawImage>();
        beamImage.texture = GenerateBeamTexture();
        beamImage.color = beamColor;
        beamImage.raycastTarget = false;
    }

    private Texture2D GenerateBeamTexture()
    {
        const int width = 256;
        const int height = 128;
        Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false)
        {
            wrapMode = TextureWrapMode.Repeat,
            filterMode = FilterMode.Bilinear
        };

        Color[] pixels = new Color[width * height];
        for (int x = 0; x < width; x++)
        {
            float beam = Mathf.Pow(Mathf.PerlinNoise(x * 0.1f, 0), 3);
            for (int y = 0; y < height; y++)
            {
                // Fade out towards top/bottom
                float yFade = Mathf.Sin((y / (float)height) * Mathf.PI);
                pixels[y * width + x] = new Color(1, 1, 1, beam * yFade);
            }
        }
        tex.SetPixels(pixels);
        tex.Apply();
        return tex;
    }

    private void UpdateLightBeams(float cycleTime)
    {
        // Only visible at dawn (0-0.1) and dusk (0.4-0.6)
        float intensity = 0;
        if (cycleTime < 0.15f) intensity = Mathf.InverseLerp(0, 0.1f, cycleTime);
        else if (cycleTime > 0.45f && cycleTime < 0.65f) intensity = 1f - Mathf.Abs(cycleTime - 0.55f) * 10f;
        
        intensity = Mathf.Clamp01(intensity) * beamIntensity;
        
        Color c = beamImage.color;
        c.a = intensity;
        beamImage.color = c;

        // Animate the beams drifting
        beamImage.uvRect = new Rect(Time.time * 0.02f, 0, 2, 1);
    }

    // -------------------------------------------------------------------------
    // Vignette overlay (built with UI Canvas + procedural radial gradient)
    // -------------------------------------------------------------------------

    private void CreateVignetteOverlay()
    {
        // Create a screen-space canvas above the game
        vignetteObject = new GameObject("VignetteOverlay");
        vignetteCanvas = vignetteObject.AddComponent<Canvas>();
        vignetteCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        vignetteCanvas.sortingOrder = 1000; // Above gameplay UI

        UnityEngine.UI.CanvasScaler scaler = vignetteObject.AddComponent<UnityEngine.UI.CanvasScaler>();
        scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        vignetteObject.AddComponent<UnityEngine.UI.GraphicRaycaster>();

        // Vignette image (fills entire screen, uses generated radial texture)
        GameObject imageGO = new GameObject("VignetteImage");
        imageGO.transform.SetParent(vignetteObject.transform, false);

        RectTransform rect = imageGO.AddComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        vignetteImage = imageGO.AddComponent<UnityEngine.UI.RawImage>();
        vignetteImage.texture = GenerateVignetteTexture();
        vignetteImage.color = new Color(
            vignetteColor.r,
            vignetteColor.g,
            vignetteColor.b,
            vignetteStrength
        );
        vignetteImage.raycastTarget = false;

        Debug.Log($"[GraphicsEnhancer] Created vignette overlay (strength={vignetteStrength})");
    }

    private Texture2D GenerateVignetteTexture()
    {
        const int size = 256;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
            hideFlags = HideFlags.HideAndDontSave
        };

        Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
        float maxDist = size * 0.5f;
        Color[] pixels = new Color[size * size];

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = (x - center.x) / maxDist;
                float dy = (y - center.y) / maxDist;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);

                // 0 alpha in center (clear), 1 alpha at corners (dark)
                float alpha = Mathf.SmoothStep(vignetteRadius * 0.7f, 1.3f, dist);
                pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();
        return tex;
    }

    // -------------------------------------------------------------------------
    // Atmospheric particles (dust motes / fireflies)
    // -------------------------------------------------------------------------

    private void CreateAtmosphereParticles()
    {
        GameObject particleGO = new GameObject("AtmosphereParticles");
        particleGO.transform.SetParent(targetCamera != null ? targetCamera.transform : transform, false);
        particleGO.transform.localPosition = new Vector3(0f, 0f, 5f); // In front of camera

        atmosphereParticles = particleGO.AddComponent<ParticleSystem>();
        particleRenderer = atmosphereParticles.GetComponent<ParticleSystemRenderer>();

        // Main module
        particleMain = atmosphereParticles.main;
        particleMain.loop = true;
        particleMain.startLifetime = 8f;
        particleMain.startSpeed = 0.3f;
        particleMain.startSize = 0.06f;
        particleMain.startColor = dustDayColor;
        particleMain.maxParticles = particleCount;
        particleMain.simulationSpace = ParticleSystemSimulationSpace.World;
        particleMain.scalingMode = ParticleSystemScalingMode.Local;

        // Emission
        ParticleSystem.EmissionModule emission = atmosphereParticles.emission;
        emission.rateOverTime = particleCount / 8f; // Spawn ~count over lifetime

        // Shape: spawn in a box around camera
        ParticleSystem.ShapeModule shape = atmosphereParticles.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(particleSpawnRadius * 2f, particleSpawnRadius * 2f, 0.1f);

        // Velocity: slow drift upward + gentle horizontal sway
        ParticleSystem.VelocityOverLifetimeModule velocity = atmosphereParticles.velocityOverLifetime;
        velocity.enabled = true;
        velocity.x = new ParticleSystem.MinMaxCurve(-0.15f, 0.15f);
        velocity.y = new ParticleSystem.MinMaxCurve(0.05f, 0.2f);

        // Color over lifetime: fade in and out (so particles don't pop)
        ParticleSystem.ColorOverLifetimeModule colorOL = atmosphereParticles.colorOverLifetime;
        colorOL.enabled = true;
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new GradientColorKey[] {
                new GradientColorKey(Color.white, 0f),
                new GradientColorKey(Color.white, 1f)
            },
            new GradientAlphaKey[] {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(1f, 0.2f),
                new GradientAlphaKey(1f, 0.8f),
                new GradientAlphaKey(0f, 1f)
            }
        );
        colorOL.color = new ParticleSystem.MinMaxGradient(gradient);

        // Size variation
        ParticleSystem.SizeOverLifetimeModule sizeOL = atmosphereParticles.sizeOverLifetime;
        sizeOL.enabled = true;
        AnimationCurve sizeCurve = new AnimationCurve(
            new Keyframe(0f, 0.5f),
            new Keyframe(0.5f, 1f),
            new Keyframe(1f, 0.5f)
        );
        sizeOL.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

        // Use a built-in soft circle texture for particles
        // (Default Particle is a Unity built-in)
        particleRenderer.material = new Material(Shader.Find("Sprites/Default"));
        particleRenderer.material.mainTexture = GenerateParticleTexture();
        particleRenderer.sortingLayerName = "Default";
        particleRenderer.sortingOrder = 100; // Above terrain

        Debug.Log($"[GraphicsEnhancer] Created atmosphere particle system ({particleCount} particles)");
    }

    private Texture2D GenerateParticleTexture()
    {
        const int size = 32;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
            hideFlags = HideFlags.HideAndDontSave
        };

        Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
        float maxDist = size * 0.5f;
        Color[] pixels = new Color[size * size];

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = (x - center.x) / maxDist;
                float dy = (y - center.y) / maxDist;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);
                float alpha = 1f - Mathf.SmoothStep(0f, 1f, dist);
                pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();
        return tex;
    }

    private void UpdateAtmosphereParticles()
    {
        // Blend between day dust and night firefly color based on cycle time
        // 0 = dawn, 0.25 = noon, 0.5 = dusk, 0.75 = midnight, 1 = dawn

        float t = cycleManager.normalizedCycleTime;

        // Day weight = 1 at noon (0.25), 0 at midnight (0.75)
        float dayWeight;
        if (t < 0.5f)
        {
            dayWeight = Mathf.SmoothStep(0f, 1f, t / 0.5f);   // dawn → noon → 1, then to dusk → 1
            if (t > 0.25f) dayWeight = Mathf.SmoothStep(1f, 0.5f, (t - 0.25f) / 0.25f);
            else dayWeight = Mathf.SmoothStep(0f, 1f, t / 0.25f);
        }
        else
        {
            // 0.5 (dusk) → 0.75 (midnight) → 1.0 (dawn)
            dayWeight = Mathf.SmoothStep(0.5f, 0f, (t - 0.5f) / 0.25f);
            if (t > 0.75f) dayWeight = Mathf.SmoothStep(0f, 0.5f, (t - 0.75f) / 0.25f);
        }

        Color blended = Color.Lerp(fireflyNightColor, dustDayColor, dayWeight);
        particleMain.startColor = blended;
    }

    // -------------------------------------------------------------------------
    // Public API
    // -------------------------------------------------------------------------

    /// <summary>Toggle vignette at runtime.</summary>
    public void SetVignetteEnabled(bool enabled)
    {
        enableVignette = enabled;
        if (vignetteObject != null) vignetteObject.SetActive(enabled);
    }

    /// <summary>Adjust vignette strength at runtime.</summary>
    public void SetVignetteStrength(float strength)
    {
        vignetteStrength = Mathf.Clamp01(strength);
        if (vignetteImage != null)
        {
            Color c = vignetteImage.color;
            c.a = vignetteStrength;
            vignetteImage.color = c;
        }
    }

    /// <summary>Toggle atmospheric particles at runtime.</summary>
    public void SetParticlesEnabled(bool enabled)
    {
        enableAtmosphericParticles = enabled;
        if (atmosphereParticles != null) atmosphereParticles.gameObject.SetActive(enabled);
    }
}
