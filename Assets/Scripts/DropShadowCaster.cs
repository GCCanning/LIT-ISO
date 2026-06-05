using UnityEngine;

/// <summary>
/// Renders a procedurally-generated drop shadow beneath an object, dynamically
/// positioned and scaled based on the sun's current direction.
///
/// How it works:
///   1. Spawns a child GameObject ("Shadow") with a SpriteRenderer
///   2. Generates a soft elliptical shadow texture at runtime (no asset needed)
///   3. Each frame, queries the SunController for sun direction and altitude
///   4. Offsets the shadow based on light direction (long at sunset, short at noon)
///   5. Adjusts opacity based on sun altitude (darker at noon, fades at night)
///
/// Setup:
///   - Attach to any GameObject that should cast a shadow (e.g. Player, Tree)
///   - Optionally assign the parent SpriteRenderer (for shadow z-sorting)
///   - SunController is auto-found in scene on Start
///
/// The shadow stays at ground level (Y position matches the object's foot position).
/// </summary>
[DisallowMultipleComponent]
public class DropShadowCaster : MonoBehaviour
{
    // -------------------------------------------------------------------------
    // Inspector fields
    // -------------------------------------------------------------------------

    [Header("Shadow Appearance")]
    [Tooltip("Width of the shadow ellipse in world units.")]
    [Range(0.1f, 5f)]
    public float shadowWidth = 1.0f;

    [Tooltip("Height (depth) of the shadow ellipse in world units. Smaller for isometric squash.")]
    [Range(0.05f, 3f)]
    public float shadowHeight = 0.45f;

    [Tooltip("Maximum opacity of the shadow (0 = invisible, 1 = solid black).")]
    [Range(0f, 1f)]
    public float maxOpacity = 0.7f;

    [Tooltip("Minimum opacity at night (so shadow is still slightly visible by moonlight).")]
    [Range(0f, 1f)]
    public float minOpacity = 0.15f;

    [Tooltip("Color tint applied to the shadow.")]
    public Color shadowColor = Color.black;

    [Header("Shadow Behavior")]
    [Tooltip("How far the shadow stretches as the sun moves toward horizon.")]
    [Range(0f, 2f)]
    public float shadowStretchAmount = 0.8f;

    [Tooltip("Maximum lateral offset (in world units) when sun is near horizon.")]
    [Range(0f, 2f)]
    public float maxLateralOffset = 0.6f;

    [Tooltip("Vertical offset from object's transform position to place the shadow. " +
             "Use a negative value to put the shadow at the object's feet.")]
    public float groundYOffset = -0.25f;

    [Header("Sorting")]
    [Tooltip("Sorting order offset relative to the parent sprite (negative = below).")]
    public int sortingOrderOffset = -10;

    [Tooltip("Sorting layer name for the shadow (must exist in Unity project).")]
    public string sortingLayerName = "Default";

    [Header("References")]
    [Tooltip("SunController source. If null, will be auto-found.")]
    public SunController sunController;

    [Tooltip("Optional: parent SpriteRenderer to inherit sorting layer from.")]
    public SpriteRenderer parentSprite;

    [Header("Debug")]
    [Tooltip("Disable shadow rendering entirely (useful for debugging).")]
    public bool shadowEnabled = true;

    // -------------------------------------------------------------------------
    // Static shared shadow sprite (generated once, reused by all instances)
    // -------------------------------------------------------------------------

    private static Sprite sharedShadowSprite;
    private const int ShadowTexSize = 64;

    // -------------------------------------------------------------------------
    // Runtime state
    // -------------------------------------------------------------------------

    private GameObject shadowObject;
    private SpriteRenderer shadowRenderer;
    private Transform shadowTransform;

    // -------------------------------------------------------------------------
    // Unity lifecycle
    // -------------------------------------------------------------------------

    private void Start()
    {
        AutoWireReferences();
        EnsureShadowSpriteCreated();
        CreateShadowChild();
    }

    private void LateUpdate()
    {
        if (!shadowEnabled || shadowObject == null) return;
        UpdateShadow();
    }

    private void OnDestroy()
    {
        if (shadowObject != null)
        {
            Destroy(shadowObject);
        }
    }

    // -------------------------------------------------------------------------
    // Setup
    // -------------------------------------------------------------------------

    private void AutoWireReferences()
    {
        if (sunController == null)
        {
            sunController = FindFirstObjectByType<SunController>();
        }

        if (parentSprite == null)
        {
            parentSprite = GetComponentInChildren<SpriteRenderer>();
        }
    }

    /// <summary>
    /// Generate the shadow texture once and cache it as a static sprite.
    /// Soft elliptical falloff using a radial gradient with smoothstep.
    /// The sprite is white with alpha-only mask — SpriteRenderer.color tints it.
    /// </summary>
    private static void EnsureShadowSpriteCreated()
    {
        if (sharedShadowSprite != null) return;

        Texture2D texture = new Texture2D(ShadowTexSize, ShadowTexSize, TextureFormat.RGBA32, false)
        {
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
            hideFlags = HideFlags.HideAndDontSave
        };

        Vector2 center = new Vector2((ShadowTexSize - 1) * 0.5f, (ShadowTexSize - 1) * 0.5f);
        float maxDist = ShadowTexSize * 0.5f;

        Color[] pixels = new Color[ShadowTexSize * ShadowTexSize];

        for (int y = 0; y < ShadowTexSize; y++)
        {
            for (int x = 0; x < ShadowTexSize; x++)
            {
                // Compute normalized distance from center (0 = center, 1 = edge)
                float dx = (x - center.x) / maxDist;
                float dy = (y - center.y) / maxDist;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);

                // Soft falloff: nearly solid in middle, fades to transparent at edge
                // Smoother gradient for a more natural look
                float alpha = 1f - Mathf.SmoothStep(0.2f, 1.0f, dist);
                pixels[y * ShadowTexSize + x] = new Color(1f, 1f, 1f, alpha);
            }
        }

        texture.SetPixels(pixels);
        texture.Apply();

        sharedShadowSprite = Sprite.Create(
            texture,
            new Rect(0, 0, ShadowTexSize, ShadowTexSize),
            new Vector2(0.5f, 0.5f),  // Pivot at center
            ShadowTexSize              // 1 world unit = ShadowTexSize pixels
        );
        sharedShadowSprite.name = "GeneratedShadowSprite";
        sharedShadowSprite.hideFlags = HideFlags.HideAndDontSave;
    }

    private void CreateShadowChild()
    {
        shadowObject = new GameObject("Shadow");
        shadowTransform = shadowObject.transform;
        shadowTransform.SetParent(transform, worldPositionStays: false);
        shadowTransform.localPosition = new Vector3(0f, groundYOffset, 0f);
        shadowTransform.localRotation = Quaternion.identity;

        // Mark this GameObject so IsoLightingController doesn't tint the shadow
        // (its color is managed dynamically by this script).
        shadowObject.AddComponent<DropShadowSpriteMarker>();

        shadowRenderer = shadowObject.AddComponent<SpriteRenderer>();
        shadowRenderer.sprite = sharedShadowSprite;
        shadowRenderer.color = new Color(shadowColor.r, shadowColor.g, shadowColor.b, maxOpacity);

        // Sorting setup — render below parent.
        // Use sortingLayerName explicitly so it works whether or not parentSprite is set.
        // The negative sortingOrderOffset (default -10) puts it well below the player sprite.
        if (parentSprite != null)
        {
            shadowRenderer.sortingLayerID = parentSprite.sortingLayerID;
            shadowRenderer.sortingOrder = parentSprite.sortingOrder + sortingOrderOffset;
        }
        else
        {
            shadowRenderer.sortingLayerName = sortingLayerName;
            shadowRenderer.sortingOrder = sortingOrderOffset;
        }

        // Set initial scale
        ApplyShadowScale(Vector2.zero, 1f);

        Debug.Log($"[DropShadowCaster] Created shadow for '{gameObject.name}' " +
                  $"(size={shadowWidth}x{shadowHeight}, opacity={maxOpacity}, " +
                  $"sortingOrder={shadowRenderer.sortingOrder}, layer={shadowRenderer.sortingLayerName})");
    }

    // -------------------------------------------------------------------------
    // Per-frame shadow positioning
    // -------------------------------------------------------------------------

    private void UpdateShadow()
    {
        // Get sun info (or use defaults if SunController unavailable)
        Vector2 shadowDir = Vector2.down;
        float sunAltitude = 1f;
        float shadowStrength = 1f;

        if (sunController != null)
        {
            shadowDir = sunController.GetShadowDirection2D();
            sunAltitude = sunController.SunAltitude;
            shadowStrength = sunController.GetShadowStrength();
        }

        // Calculate stretch and lateral offset based on sun altitude:
        //   - High noon (altitude = 1):  short shadow, centered under object
        //   - Sunset (altitude = 0):     long shadow, stretched away from sun
        //   - Night (altitude < 0):      no shadow

        float horizonFactor = 1f - Mathf.Clamp01(sunAltitude); // 0 at noon, 1 at horizon
        float stretchFactor = 1f + (horizonFactor * shadowStretchAmount);
        float lateralOffset = horizonFactor * maxLateralOffset;

        // Compute lateral position. shadowDir is the LIGHT direction along the X axis:
        //   - sun east  (dawn):  shadowDir.x = -1, shadow falls west (negative X)
        //   - sun west  (dusk):  shadowDir.x = +1, shadow falls east (positive X)
        //   - sun noon (overhead): shadowDir.x ≈ 0, shadow stays under object
        Vector2 lateralPos = shadowDir * lateralOffset;

        // Apply shadow position relative to parent (X = lateral shift, Y = ground offset only)
        shadowTransform.localPosition = new Vector3(lateralPos.x, groundYOffset, 0f);

        // Apply scale
        ApplyShadowScale(lateralPos, stretchFactor);

        // Apply opacity. At night (shadowStrength=0), shadow fades to minOpacity
        // instead of vanishing completely — visible as faint moonlight shadow.
        float alphaT = Mathf.Lerp(minOpacity, maxOpacity, shadowStrength);
        Color finalColor = new Color(
            shadowColor.r,
            shadowColor.g,
            shadowColor.b,
            alphaT
        );
        shadowRenderer.color = finalColor;

        // Update sorting if parent sprite changes
        if (parentSprite != null)
        {
            shadowRenderer.sortingLayerID = parentSprite.sortingLayerID;
            shadowRenderer.sortingOrder = parentSprite.sortingOrder + sortingOrderOffset;
        }
    }

    private void ApplyShadowScale(Vector2 lateralOffset, float stretchFactor)
    {
        // Base shadow scale derived from configured width/height
        // The shadow sprite is 64×64 at 64 PPU = 1×1 world units, so scale = size directly.
        float baseScaleX = shadowWidth;
        float baseScaleY = shadowHeight;

        // Apply stretch when sun is low (longer shadows at sunrise/sunset)
        if (stretchFactor > 0f)
        {
            baseScaleX *= stretchFactor;
        }

        shadowTransform.localScale = new Vector3(baseScaleX, baseScaleY, 1f);
    }

    // -------------------------------------------------------------------------
    // Public API
    // -------------------------------------------------------------------------

    /// <summary>Show or hide the shadow at runtime.</summary>
    public void SetShadowVisible(bool visible)
    {
        shadowEnabled = visible;
        if (shadowObject != null)
        {
            shadowObject.SetActive(visible);
        }
    }

    /// <summary>Update shadow size at runtime.</summary>
    public void SetShadowSize(float width, float height)
    {
        shadowWidth = Mathf.Max(0.1f, width);
        shadowHeight = Mathf.Max(0.05f, height);
    }
}

/// <summary>
/// Marker component placed on drop shadow GameObjects so that
/// IsoLightingController and other systems know to skip them when
/// applying scene-wide sprite tints. The DropShadowCaster manages
/// the shadow sprite's color directly each frame.
/// </summary>
public class DropShadowSpriteMarker : MonoBehaviour
{
    // No fields — pure marker component.
}

