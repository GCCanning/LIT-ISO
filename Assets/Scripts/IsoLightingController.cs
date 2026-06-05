using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public class IsoLightingController : MonoBehaviour
{
    [Header("Profiles")]
    public IsoLightingProfile[] profiles;
    public int profileIndex;
    public float transitionSpeed = 3f;
    public bool allowKeyboardPreview = true;

    [Header("Scene References")]
    public Camera targetCamera;
    public Light directionalLight;

    [Header("Coordination With SunController")]
    [Tooltip("If a SunController is in the scene, it controls the directional light's " +
             "rotation and intensity. Set this true to skip IsoLightingController's " +
             "directional light updates and avoid conflicts. Auto-detected on Awake.")]
    public bool yieldDirectionalLightToSunController = false;

    [Header("Performance")]
    [Tooltip("How often the controller rescans the scene for newly-added tilemaps and sprite renderers.")]
    public float rendererRecacheInterval = 0.75f;
    [Tooltip("How often lighting blends and renderer tints are updated. Lower values are smoother; higher values are cheaper.")]
    public float lightingUpdateInterval = 0.05f;

    private readonly Dictionary<Tilemap, Color> tilemapBaseColors = new Dictionary<Tilemap, Color>();
    private readonly Dictionary<SpriteRenderer, Color> spriteBaseColors = new Dictionary<SpriteRenderer, Color>();
    private float nextRendererRecacheTime;
    private float nextLightingBlendTime;

    private Color targetCameraBackground;
    private Color targetAmbient;
    private Color targetDirectionalColor;
    private Color targetTilemapTint;
    private Color targetSpriteTint;
    private float targetDirectionalIntensity;

    private void Awake()
    {
        if (targetCamera == null)
        {
            targetCamera = Camera.main;
        }

        if (directionalLight == null)
        {
            directionalLight = FindFirstObjectByType<Light>();
        }

        // Auto-detect SunController and yield directional light control to it.
        // Profile-based color changes still apply via UpdateDirectionalLightColorOnly().
        if (FindFirstObjectByType<SunController>() != null)
        {
            yieldDirectionalLightToSunController = true;
        }

        CaptureSceneRenderers();
        ApplyProfileImmediate();
    }

    private void Update()
    {
        if (allowKeyboardPreview)
        {
            HandlePreviewInput();
        }

        if (Time.unscaledTime >= nextRendererRecacheTime)
        {
            CaptureSceneRenderers();
            nextRendererRecacheTime = Time.unscaledTime + Mathf.Max(0.1f, rendererRecacheInterval);
        }

        if (Time.unscaledTime >= nextLightingBlendTime)
        {
            BlendSceneLighting();
            nextLightingBlendTime = Time.unscaledTime + Mathf.Max(0.01f, lightingUpdateInterval);
        }
    }

    public void SetProfile(int index)
    {
        if (profiles == null || profiles.Length == 0)
        {
            return;
        }

        profileIndex = Mathf.Clamp(index, 0, profiles.Length - 1);
        ReadProfileTargets();
    }

    public void NextProfile()
    {
        if (profiles == null || profiles.Length == 0)
        {
            return;
        }

        SetProfile((profileIndex + 1) % profiles.Length);
    }

    private void ApplyProfileImmediate()
    {
        ReadProfileTargets();

        if (targetCamera != null)
        {
            targetCamera.backgroundColor = targetCameraBackground;
        }

        RenderSettings.ambientLight = targetAmbient;

        if (directionalLight != null)
        {
            directionalLight.color = targetDirectionalColor;
            if (!yieldDirectionalLightToSunController)
            {
                directionalLight.intensity = targetDirectionalIntensity;
            }
        }

        ApplyRendererTints(targetTilemapTint, targetSpriteTint);
    }

    private void ReadProfileTargets()
    {
        IsoLightingProfile profile = GetCurrentProfile();
        if (profile == null)
        {
            targetCameraBackground = new Color(0.18f, 0.24f, 0.28f, 1f);
            targetAmbient = Color.white;
            targetDirectionalColor = Color.white;
            targetDirectionalIntensity = 1f;
            targetTilemapTint = Color.white;
            targetSpriteTint = Color.white;
            return;
        }

        targetCameraBackground = profile.cameraBackground;
        targetAmbient = profile.ambientLight;
        targetDirectionalColor = profile.directionalLightColor;
        targetDirectionalIntensity = profile.directionalLightIntensity;
        targetTilemapTint = profile.tilemapTint;
        targetSpriteTint = profile.spriteTint;
    }

    private IsoLightingProfile GetCurrentProfile()
    {
        if (profiles == null || profiles.Length == 0)
        {
            return null;
        }

        profileIndex = Mathf.Clamp(profileIndex, 0, profiles.Length - 1);
        return profiles[profileIndex];
    }

    private void HandlePreviewInput()
    {
        if (Input.GetKeyDown(KeyCode.F6))
        {
            NextProfile();
        }

        if (profiles == null)
        {
            return;
        }

        for (int i = 0; i < profiles.Length && i < 9; i++)
        {
            if (Input.GetKeyDown((KeyCode)((int)KeyCode.Alpha1 + i)))
            {
                SetProfile(i);
            }
        }
    }

    private void BlendSceneLighting()
    {
        float t = 1f - Mathf.Exp(-transitionSpeed * Time.deltaTime);

        if (targetCamera != null)
        {
            targetCamera.backgroundColor = Color.Lerp(targetCamera.backgroundColor, targetCameraBackground, t);
        }

        RenderSettings.ambientLight = Color.Lerp(RenderSettings.ambientLight, targetAmbient, t);

        if (directionalLight != null)
        {
            // When SunController is active, it owns rotation and intensity. We still
            // blend color so lighting profiles can tint the sun (e.g. orange at dusk).
            directionalLight.color = Color.Lerp(directionalLight.color, targetDirectionalColor, t);

            if (!yieldDirectionalLightToSunController)
            {
                directionalLight.intensity = Mathf.Lerp(directionalLight.intensity, targetDirectionalIntensity, t);
            }
        }

        ApplyRendererTints(targetTilemapTint, targetSpriteTint);
    }

    private void CaptureSceneRenderers()
    {
        PruneNullRenderers();

        foreach (Tilemap tilemap in FindObjectsByType<Tilemap>(FindObjectsSortMode.None))
        {
            if (!tilemapBaseColors.ContainsKey(tilemap))
            {
                tilemapBaseColors.Add(tilemap, tilemap.color);
            }
        }

        foreach (SpriteRenderer sprite in FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None))
        {
            // Skip drop shadows — their color is managed dynamically by DropShadowCaster.
            // Capturing/tinting them here would fight per-frame updates.
            if (sprite.GetComponent<DropShadowSpriteMarker>() != null) continue;

            if (!spriteBaseColors.ContainsKey(sprite))
            {
                spriteBaseColors.Add(sprite, sprite.color);
            }
        }
    }

    private void PruneNullRenderers()
    {
        List<Tilemap> nullTilemaps = null;
        foreach (KeyValuePair<Tilemap, Color> entry in tilemapBaseColors)
        {
            if (entry.Key != null) continue;
            nullTilemaps ??= new List<Tilemap>();
            nullTilemaps.Add(entry.Key);
        }

        if (nullTilemaps != null)
        {
            foreach (Tilemap tilemap in nullTilemaps)
                tilemapBaseColors.Remove(tilemap);
        }

        List<SpriteRenderer> nullSprites = null;
        foreach (KeyValuePair<SpriteRenderer, Color> entry in spriteBaseColors)
        {
            if (entry.Key != null) continue;
            nullSprites ??= new List<SpriteRenderer>();
            nullSprites.Add(entry.Key);
        }

        if (nullSprites != null)
        {
            foreach (SpriteRenderer sprite in nullSprites)
                spriteBaseColors.Remove(sprite);
        }
    }

    private void ApplyRendererTints(Color tilemapTint, Color spriteTint)
    {
        foreach (KeyValuePair<Tilemap, Color> entry in tilemapBaseColors)
        {
            if (entry.Key != null)
            {
                entry.Key.color = Multiply(entry.Value, tilemapTint);
            }
        }

        foreach (KeyValuePair<SpriteRenderer, Color> entry in spriteBaseColors)
        {
            if (entry.Key != null)
            {
                entry.Key.color = Multiply(entry.Value, spriteTint);
            }
        }
    }

    private static Color Multiply(Color baseColor, Color tint)
    {
        return new Color(
            baseColor.r * tint.r,
            baseColor.g * tint.g,
            baseColor.b * tint.b,
            baseColor.a * tint.a);
    }
}
