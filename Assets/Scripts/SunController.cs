using UnityEngine;

/// <summary>
/// Invisible orbital sun controller that drives the dynamic lighting system.
///
/// The sun itself is never rendered (isometric games have no background/sky layer),
/// but its calculated position determines:
///   1. The directional light's rotation (where shadows fall)
///   2. The directional light's intensity (bright at noon, dim at night)
///   3. Which lighting profile is currently active (Day, Dusk, Night)
///   4. Where drop shadows appear under objects
///
/// Synchronizes with DayNightMusicManager.normalizedCycleTime:
///   - 0.00 = Dawn (sun rising in east)
///   - 0.25 = Noon (sun directly overhead)
///   - 0.50 = Dusk (sun setting in west)
///   - 0.50–1.0 = Night (sun below horizon)
///
/// Setup:
///   - Attach to an invisible GameObject in the scene (e.g. "Sun")
///   - Assign cycleManager (DayNightMusicManager)
///   - Assign directionalLight (the scene's main Directional Light)
///   - Optionally assign lightingController (IsoLightingController) for auto-profile selection
///
/// The sun's "position" is conceptual — it's used only to derive the light direction.
/// </summary>
[DisallowMultipleComponent]
public class SunController : MonoBehaviour
{
    // -------------------------------------------------------------------------
    // Inspector fields
    // -------------------------------------------------------------------------

    [Header("References")]
    [Tooltip("Source of the day/night cycle timing. If null, will auto-find on Start.")]
    public DayNightMusicManager cycleManager;

    [Tooltip("The scene's directional light that gets rotated to follow the sun.")]
    public Light directionalLight;

    [Tooltip("Optional: IsoLightingController for automatic profile switching by time of day.")]
    public IsoLightingController lightingController;

    [Header("Orbital Settings")]
    [Tooltip("Conceptual orbit radius (abstract units — only direction matters for lighting).")]
    public float orbitRadius = 50f;

    [Tooltip("World position around which the sun orbits. Usually the world center (0,0,0).")]
    public Vector3 orbitCenter = Vector3.zero;

    [Tooltip("Tilt of the sun's orbit plane. 0 = equator orbit, 90 = straight overhead.")]
    [Range(0f, 90f)]
    public float orbitTiltDegrees = 60f;

    [Tooltip("Rotation offset for the orbit plane. Adjusts where sunrise/sunset appear.")]
    [Range(-180f, 180f)]
    public float orbitYawDegrees = 0f;

    [Header("Lighting Intensity")]
    [Tooltip("Maximum directional light intensity at solar noon.")]
    [Range(0f, 3f)]
    public float maxLightIntensity = 1.2f;

    [Tooltip("Minimum directional light intensity at midnight (moonlight ambiance).")]
    [Range(0f, 1f)]
    public float minLightIntensity = 0.15f;

    [Tooltip("How quickly the light blends to new intensity/rotation (higher = snappier).")]
    [Range(0.5f, 10f)]
    public float lightBlendSpeed = 2.5f;

    [Header("Sun Color (Override Mode)")]
    [Tooltip("If true, SunController directly controls the light's color (overrides IsoLightingController). " +
             "If false, color is controlled by the IsoLightingController's active profile (recommended).")]
    public bool overrideLightColor = false;

    [Tooltip("Color of the sunlight at sunrise (warm orange). Only used if overrideLightColor is true.")]
    public Color dawnColor = new Color(1f, 0.65f, 0.35f, 1f);

    [Tooltip("Color of the sunlight at noon (warm white). Only used if overrideLightColor is true.")]
    public Color noonColor = new Color(1f, 0.96f, 0.86f, 1f);

    [Tooltip("Color of the sunlight at sunset (deep orange/red). Only used if overrideLightColor is true.")]
    public Color duskColor = new Color(1f, 0.55f, 0.3f, 1f);

    [Tooltip("Color of the moonlight at midnight (cool blue). Only used if overrideLightColor is true.")]
    public Color nightColor = new Color(0.42f, 0.52f, 0.9f, 1f);

    [Header("Lighting Profile Auto-Selection")]
    [Tooltip("If true, automatically transitions through Day → Dusk → Night profiles.")]
    public bool autoSelectLightingProfile = true;

    [Tooltip("Index of the Day profile in IsoLightingController.profiles array.")]
    public int dayProfileIndex = 0;

    [Tooltip("Index of the Dusk profile in IsoLightingController.profiles array.")]
    public int duskProfileIndex = 1;

    [Tooltip("Index of the Night profile in IsoLightingController.profiles array.")]
    public int nightProfileIndex = 2;

    [Header("Debug — Read-Only")]
    [Tooltip("Current normalized cycle time (0 = dawn, 0.5 = dusk, 1 = next dawn).")]
    [Range(0f, 1f)]
    public float debugNormalizedTime = 0f;

    [Tooltip("Current sun altitude (1 = directly overhead, 0 = horizon, -1 = under world).")]
    [Range(-1f, 1f)]
    public float debugSunAltitude = 0f;

    [Tooltip("Current sun position in world space (calculated, not rendered).")]
    public Vector3 debugSunPosition = Vector3.zero;

    [Header("Debug Visualization")]
    [Tooltip("Draw sun orbit path and current position in Scene view.")]
    public bool drawDebugGizmos = true;

    // -------------------------------------------------------------------------
    // Internal state
    // -------------------------------------------------------------------------

    private int currentProfileIndex = -1;

    // -------------------------------------------------------------------------
    // Public read-only properties
    // -------------------------------------------------------------------------

    /// <summary>Current sun altitude: 1 = noon, 0 = horizon, negative = below horizon.</summary>
    public float SunAltitude => debugSunAltitude;

    /// <summary>Current calculated sun position (relative to orbitCenter).</summary>
    public Vector3 SunPosition => debugSunPosition;

    /// <summary>True if the sun is above the horizon (daytime visually).</summary>
    public bool IsDaytime => debugSunAltitude > 0f;

    // -------------------------------------------------------------------------
    // Unity lifecycle
    // -------------------------------------------------------------------------

    private void Start()
    {
        AutoWireReferences();
        UpdateSun(initialSnap: true);
    }

    private void Update()
    {
        UpdateSun(initialSnap: false);
    }

    // -------------------------------------------------------------------------
    // Setup
    // -------------------------------------------------------------------------

    private void AutoWireReferences()
    {
        if (cycleManager == null)
        {
            cycleManager = FindFirstObjectByType<DayNightMusicManager>();
            if (cycleManager == null)
            {
                Debug.LogWarning("[SunController] No DayNightMusicManager found in scene. Sun will not animate.");
            }
        }

        if (directionalLight == null)
        {
            Light[] lights = FindObjectsByType<Light>(FindObjectsSortMode.None);
            foreach (Light l in lights)
            {
                if (l.type == LightType.Directional)
                {
                    directionalLight = l;
                    break;
                }
            }

            if (directionalLight == null)
            {
                Debug.LogWarning("[SunController] No Directional Light found in scene. Light rotation disabled.");
            }
        }

        if (lightingController == null && autoSelectLightingProfile)
        {
            lightingController = FindFirstObjectByType<IsoLightingController>();
        }
    }

    // -------------------------------------------------------------------------
    // Per-frame logic
    // -------------------------------------------------------------------------

    private void UpdateSun(bool initialSnap)
    {
        if (cycleManager == null) return;

        // Read normalized cycle time (0 = dawn, 0.5 = dusk, 1 = next dawn)
        float normalizedTime = cycleManager.normalizedCycleTime;
        debugNormalizedTime = normalizedTime;

        // Calculate sun position in 3D space
        Vector3 sunWorldPos = CalculateSunPosition(normalizedTime);
        transform.position = sunWorldPos;
        debugSunPosition = sunWorldPos;

        // Compute sun altitude (-1 = directly below, 1 = directly above)
        float altitude = (sunWorldPos.y - orbitCenter.y) / orbitRadius;
        debugSunAltitude = Mathf.Clamp(altitude, -1f, 1f);

        // Update directional light to match sun
        UpdateDirectionalLight(normalizedTime, altitude, initialSnap);

        // Optionally auto-select lighting profile based on time of day
        if (autoSelectLightingProfile && lightingController != null)
        {
            UpdateLightingProfile(normalizedTime);
        }
    }

    // -------------------------------------------------------------------------
    // Sun position math
    // -------------------------------------------------------------------------

    /// <summary>
    /// Calculate the sun's position in world space based on normalized cycle time.
    ///
    /// The sun follows a tilted circular orbit:
    ///   - At normalizedTime = 0.0 (dawn), sun is at horizon east
    ///   - At normalizedTime = 0.25 (noon), sun is at peak altitude
    ///   - At normalizedTime = 0.5 (dusk), sun is at horizon west
    ///   - At normalizedTime = 0.5–1.0 (night), sun arcs below horizon
    /// </summary>
    private Vector3 CalculateSunPosition(float normalizedTime)
    {
        // Map normalized time to orbit angle:
        //   t=0   → angle=0   (east horizon)
        //   t=0.25 → angle=90  (zenith)
        //   t=0.5 → angle=180 (west horizon)
        //   t=1.0 → angle=360 (back to east horizon)
        float orbitAngle = normalizedTime * 360f;

        // Convert to local orbit position (X = east-west, Y = up, Z = north-south)
        float angleRad = orbitAngle * Mathf.Deg2Rad;
        Vector3 localPos = new Vector3(
            Mathf.Cos(angleRad) * orbitRadius,           // East-west component
            Mathf.Sin(angleRad) * orbitRadius,           // Vertical component
            0f                                            // North-south (will be tilted)
        );

        // Apply orbit tilt (rotate around X axis to tilt orbit plane)
        Quaternion tiltRotation = Quaternion.Euler(orbitTiltDegrees - 90f, 0f, 0f);
        localPos = tiltRotation * localPos;

        // Apply orbit yaw (rotate around Y axis for sunrise/sunset direction)
        Quaternion yawRotation = Quaternion.Euler(0f, orbitYawDegrees, 0f);
        localPos = yawRotation * localPos;

        return orbitCenter + localPos;
    }

    // -------------------------------------------------------------------------
    // Directional light update
    // -------------------------------------------------------------------------

    private void UpdateDirectionalLight(float normalizedTime, float altitude, bool initialSnap)
    {
        if (directionalLight == null) return;

        // Calculate the direction FROM sun TO world center (this is the light direction)
        Vector3 sunDirection = (orbitCenter - transform.position).normalized;
        if (sunDirection.sqrMagnitude < 0.001f) return;

        Quaternion targetRotation = Quaternion.LookRotation(sunDirection);

        // Compute target intensity:
        //   Above horizon (altitude > 0): scale from 0→max as altitude rises
        //   Below horizon (altitude < 0): clamp to min (moonlight ambiance)
        float normalizedAltitude = Mathf.Clamp01(altitude); // 0 at horizon, 1 at zenith
        float targetIntensity = Mathf.Lerp(minLightIntensity, maxLightIntensity, normalizedAltitude);

        if (initialSnap)
        {
            // Snap immediately on Start to avoid initial visual jump
            directionalLight.transform.rotation = targetRotation;
            directionalLight.intensity = targetIntensity;

            if (overrideLightColor)
            {
                directionalLight.color = ComputeSunColor(normalizedTime, altitude);
            }
        }
        else
        {
            // Smooth blend over time (frame-rate independent)
            float t = 1f - Mathf.Exp(-lightBlendSpeed * Time.deltaTime);

            directionalLight.transform.rotation = Quaternion.Slerp(
                directionalLight.transform.rotation,
                targetRotation,
                t
            );
            directionalLight.intensity = Mathf.Lerp(
                directionalLight.intensity,
                targetIntensity,
                t
            );

            // Only update color if explicitly overriding IsoLightingController.
            // Otherwise, let lighting profiles handle color transitions through
            // their own blending — avoids two systems fighting over the same value.
            if (overrideLightColor)
            {
                Color targetColor = ComputeSunColor(normalizedTime, altitude);
                directionalLight.color = Color.Lerp(
                    directionalLight.color,
                    targetColor,
                    t
                );
            }
        }
    }

    /// <summary>
    /// Compute the sun's color based on time of day and altitude.
    /// Interpolates: dawn → noon → dusk → night → dawn.
    /// </summary>
    private Color ComputeSunColor(float normalizedTime, float altitude)
    {
        // Cycle layout:
        //   0.00 = Dawn  (dawnColor)
        //   0.25 = Noon  (noonColor)
        //   0.50 = Dusk  (duskColor)
        //   0.75 = Midnight (nightColor)
        //   1.00 = Dawn  (dawnColor)

        if (normalizedTime < 0.25f)
        {
            // Dawn → Noon
            float t = normalizedTime / 0.25f;
            return Color.Lerp(dawnColor, noonColor, t);
        }
        else if (normalizedTime < 0.5f)
        {
            // Noon → Dusk
            float t = (normalizedTime - 0.25f) / 0.25f;
            return Color.Lerp(noonColor, duskColor, t);
        }
        else if (normalizedTime < 0.75f)
        {
            // Dusk → Midnight
            float t = (normalizedTime - 0.5f) / 0.25f;
            return Color.Lerp(duskColor, nightColor, t);
        }
        else
        {
            // Midnight → Dawn
            float t = (normalizedTime - 0.75f) / 0.25f;
            return Color.Lerp(nightColor, dawnColor, t);
        }
    }

    // -------------------------------------------------------------------------
    // Lighting profile auto-selection
    // -------------------------------------------------------------------------

    /// <summary>
    /// Automatically switch IsoLightingController profile based on cycle time.
    /// Only triggers SetProfile() when the target profile changes — the
    /// IsoLightingController handles smooth blending internally.
    /// </summary>
    private void UpdateLightingProfile(float normalizedTime)
    {
        if (lightingController == null || lightingController.profiles == null) return;
        if (lightingController.profiles.Length == 0) return;

        int targetProfile = DetermineProfileForTime(normalizedTime);
        if (targetProfile != currentProfileIndex && targetProfile >= 0)
        {
            // Clamp to valid range
            targetProfile = Mathf.Clamp(targetProfile, 0, lightingController.profiles.Length - 1);
            lightingController.SetProfile(targetProfile);
            currentProfileIndex = targetProfile;
        }
    }

    /// <summary>
    /// Decide which lighting profile should be active for the given cycle time.
    ///
    /// Schedule:
    ///   0.00 – 0.10 → Dawn transition zone   (use Day profile, light intensity handles rest)
    ///   0.10 – 0.40 → Day profile
    ///   0.40 – 0.50 → Dusk profile (sunset)
    ///   0.50 – 0.60 → Dusk profile (twilight)
    ///   0.60 – 0.90 → Night profile
    ///   0.90 – 1.00 → Dusk profile (dawn approaching)
    /// </summary>
    private int DetermineProfileForTime(float t)
    {
        if (t < 0.10f) return dayProfileIndex;
        if (t < 0.40f) return dayProfileIndex;
        if (t < 0.60f) return duskProfileIndex;
        if (t < 0.90f) return nightProfileIndex;
        return duskProfileIndex;
    }

    // -------------------------------------------------------------------------
    // Public API
    // -------------------------------------------------------------------------

    /// <summary>
    /// Get the current sun direction (the unit vector FROM the sun TO world center).
    /// Useful for shadow casting calculations.
    /// </summary>
    public Vector3 GetSunDirection()
    {
        Vector3 dir = orbitCenter - transform.position;
        return dir.sqrMagnitude > 0.001f ? dir.normalized : Vector3.down;
    }

    /// <summary>
    /// Get the 2D projected shadow direction (for isometric drop shadows).
    /// Returns a horizontal-plane direction (X-axis) — shadow stays at ground level.
    ///
    /// At noon: returns near-zero (sun overhead, no lateral shadow)
    /// At dawn (sun east):  returns (-1, 0)  → shadow falls west
    /// At dusk (sun west):  returns (+1, 0)  → shadow falls east
    /// </summary>
    public Vector2 GetShadowDirection2D()
    {
        Vector3 dir = GetSunDirection();
        // For 2D drop shadows: use only the horizontal (X) component of the light
        // direction. The shadow extends along the ground from the object's feet.
        Vector2 shadow = new Vector2(dir.x, 0f);
        return shadow;
    }

    /// <summary>
    /// Returns a 0–1 value representing how strong the shadow should be.
    /// 0 = no shadow (night), 1 = strong shadow (noon).
    /// </summary>
    public float GetShadowStrength()
    {
        // Strongest at noon (altitude = 1), weakest at night (altitude < 0)
        return Mathf.Clamp01(debugSunAltitude);
    }

    // -------------------------------------------------------------------------
    // Debug visualization
    // -------------------------------------------------------------------------

    private void OnDrawGizmos()
    {
        if (!drawDebugGizmos) return;

        // Draw orbit center
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(orbitCenter, 0.5f);

        // Draw orbit path (sample 32 positions around the orbit)
        Gizmos.color = new Color(1f, 0.85f, 0f, 0.4f);
        const int segments = 32;
        Vector3 prev = CalculateSunPosition(0f);
        for (int i = 1; i <= segments; i++)
        {
            float t = (float)i / segments;
            Vector3 next = CalculateSunPosition(t);
            Gizmos.DrawLine(prev, next);
            prev = next;
        }

        // Draw current sun position
        Gizmos.color = IsDaytime ? Color.yellow : new Color(0.4f, 0.5f, 0.9f);
        Gizmos.DrawSphere(transform.position, 1.5f);

        // Draw light direction (from sun toward world center)
        Gizmos.color = Color.white;
        Gizmos.DrawLine(transform.position, orbitCenter);
    }
}
