using UnityEngine;
using EthraClone.TrialWeek;

/// <summary>
/// Plays the day and night music tracks and crossfades smoothly between them
/// as the world day/night cycle advances.
///
/// Two AudioSources run simultaneously at all times; only their volumes change.
/// The crossfade uses a SmoothStep curve centred on the dawn/dusk transition
/// points so the music never snaps.
///
/// Setup:
///   1. Attach to any persistent GameObject (e.g. a "GameManager" or its own object).
///   2. Assign DayMusicClip  → Music_Day_AmbientExploration
///      Assign NightMusicClip → Music_Night_HarpTheme
///   3. If TrialWeekManager is present the manager syncs to it on Start and
///      matches its cycle length automatically.  If it is absent the manager
///      uses its own standalone timer with the same default lengths.
/// </summary>
public class DayNightMusicManager : MonoBehaviour
{
    // -------------------------------------------------------------------------
    // Inspector fields
    // -------------------------------------------------------------------------

    [Header("Tracks")]
    [Tooltip("Daytime ambient track — plays fully during the middle of day.")]
    public AudioClip dayMusicClip;

    [Tooltip("Nighttime harp track — plays fully during the middle of night.")]
    public AudioClip nightMusicClip;

    [Header("Cycle Settings")]
    [Tooltip("Length of the daytime period in real minutes. " +
             "Set to match TrialWeekManager.dayLengthMinutes (default 15).")]
    public float dayLengthMinutes = 15f;

    [Tooltip("Length of the nighttime period in real minutes. " +
             "Set to match TrialWeekManager.nightLengthMinutes (default 15).")]
    public float nightLengthMinutes = 15f;

    [Header("Crossfade")]
    [Tooltip("How long the crossfade lasts in real seconds. " +
             "The fade is centred on the exact dawn/dusk point, so it starts " +
             "half this duration BEFORE the transition and ends half AFTER. " +
             "Recommended: 20–40 s for a 15-min day cycle.")]
    [Range(5f, 120f)]
    public float crossfadeDuration = 30f;

    [Header("Volume")]
    [Range(0f, 1f)]
    [Tooltip("Overall music volume. Adjust to taste.")]
    public float masterVolume = 0.75f;

    [Header("Debug — read-only")]
    [Range(0f, 1f)]
    [Tooltip("Current position in the cycle. " +
             "0 = dawn, dayFraction = dusk, 1 = next dawn.")]
    public float normalizedCycleTime = 0f;

    [Tooltip("Live day-track volume (before master).")]
    public float debugDayVolume = 1f;

    [Tooltip("Live night-track volume (before master).")]
    public float debugNightVolume = 0f;

    // -------------------------------------------------------------------------
    // Private state
    // -------------------------------------------------------------------------

    private AudioSource daySource;
    private AudioSource nightSource;

    /// Internal timer in real minutes.
    private float elapsedMinutes = 0f;

    // Cached so we don't call FindFirstObjectByType every frame.
    private TrialWeekManager trialWeekManager;

    // -------------------------------------------------------------------------
    // Singleton (optional — music manager should be unique per scene)
    // -------------------------------------------------------------------------

    private static DayNightMusicManager instance;
    public static DayNightMusicManager Instance => instance;

    // -------------------------------------------------------------------------
    // Unity lifecycle
    // -------------------------------------------------------------------------

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;
        DontDestroyOnLoad(gameObject);

        CreateAudioSources();
    }

    private void Start()
    {
        SyncWithTrialWeekManager();
        StartPlayback();
    }

    private void OnDestroy()
    {
        if (instance == this)
            instance = null;
    }

    private void Update()
    {
        AdvanceTimer();
        UpdateVolumes();
    }

    // -------------------------------------------------------------------------
    // Setup helpers
    // -------------------------------------------------------------------------

    private void CreateAudioSources()
    {
        // Day source
        daySource = gameObject.AddComponent<AudioSource>();
        daySource.clip = dayMusicClip;
        daySource.loop = true;
        daySource.spatialBlend = 0f;   // 2D — music is never positional
        daySource.priority = 0;        // Highest priority so it isn't culled
        daySource.volume = masterVolume;
        daySource.playOnAwake = false;

        // Night source
        nightSource = gameObject.AddComponent<AudioSource>();
        nightSource.clip = nightMusicClip;
        nightSource.loop = true;
        nightSource.spatialBlend = 0f;
        nightSource.priority = 0;
        nightSource.volume = 0f;       // Starts silent — will be raised as night approaches
        nightSource.playOnAwake = false;
    }

    /// <summary>
    /// If a TrialWeekManager is present, read its current cycle position and
    /// set our internal timer to match, so the music is in sync from frame 1.
    /// </summary>
    private void SyncWithTrialWeekManager()
    {
        trialWeekManager = FindFirstObjectByType<TrialWeekManager>();

        if (trialWeekManager != null)
        {
            // Override our cycle lengths to match the authoritative manager.
            // (We read them every Start rather than every Update to avoid overhead.)
            float cycleLength = dayLengthMinutes + nightLengthMinutes;

            // Reconstruct our elapsed timer from the manager's normalised time.
            float syncedNormalized = trialWeekManager.GetNormalizedCycleTime();
            elapsedMinutes = syncedNormalized * cycleLength;

            Debug.Log($"[DayNightMusicManager] Synced to TrialWeekManager. " +
                      $"Normalised cycle pos = {syncedNormalized:F3}");
        }
        else
        {
            Debug.Log("[DayNightMusicManager] No TrialWeekManager found — " +
                      "running standalone timer starting at dawn.");
        }
    }

    private void StartPlayback()
    {
        if (dayMusicClip != null)
            daySource.Play();
        else
            Debug.LogWarning("[DayNightMusicManager] Day music clip not assigned.");

        if (nightMusicClip != null)
            nightSource.Play();
        else
            Debug.LogWarning("[DayNightMusicManager] Night music clip not assigned.");
    }

    // -------------------------------------------------------------------------
    // Per-frame logic
    // -------------------------------------------------------------------------

    private void AdvanceTimer()
    {
        // Real-time minutes per second = 1/60.
        elapsedMinutes += Time.deltaTime / 60f;
    }

    private void UpdateVolumes()
    {
        float cycleLength = dayLengthMinutes + nightLengthMinutes;
        if (cycleLength <= 0f) return;

        // 0 = dawn, dayFraction = dusk, 1 = next dawn
        normalizedCycleTime = (elapsedMinutes % cycleLength) / cycleLength;

        float dayVol = ComputeDayVolume(normalizedCycleTime);
        float nightVol = 1f - dayVol;

        daySource.volume   = dayVol   * masterVolume;
        nightSource.volume = nightVol * masterVolume;

        // Expose for debugging in the Inspector.
        debugDayVolume   = dayVol;
        debugNightVolume = nightVol;
    }

    /// <summary>
    /// Returns the day track's desired volume (0–1) for a given normalised cycle
    /// position.  Uses SmoothStep curves centred on the dawn and dusk points so
    /// both transitions feel organic.
    ///
    ///   normalizedTime:  0.0          dayFraction        1.0
    ///                    |←── day ──→|←──── night ────→|
    ///   dayVolume:       1     ____1___╲0___         0 /1
    ///                                   ↑dusk       dawn↑
    /// </summary>
    private float ComputeDayVolume(float t)
    {
        float cycleLength     = dayLengthMinutes + nightLengthMinutes;
        float dayFraction     = dayLengthMinutes / cycleLength;  // e.g. 0.5
        float halfFadeNorm    = (crossfadeDuration / 60f) / cycleLength * 0.5f;
        // halfFadeNorm = half the crossfade expressed as a fraction of the full cycle.

        // --- DUSK transition (centred at t = dayFraction) ---
        float dDusk = t - dayFraction;
        if (dDusk >= -halfFadeNorm && dDusk <= halfFadeNorm)
        {
            // t in [dayFraction - half, dayFraction + half]
            // Map to 0→1 then SmoothStep day→night (day goes 1→0).
            float progress = Mathf.InverseLerp(-halfFadeNorm, halfFadeNorm, dDusk);
            return Mathf.SmoothStep(1f, 0f, progress);
        }

        // --- DAWN transition (centred at t = 0 / t = 1, wraps around) ---
        // Remap t so that dawn is at 0: shift values > 0.5 into negative range.
        float dDawn = t > 0.5f ? t - 1f : t;
        if (dDawn >= -halfFadeNorm && dDawn <= halfFadeNorm)
        {
            // Map to 0→1 then SmoothStep night→day (day goes 0→1).
            float progress = Mathf.InverseLerp(-halfFadeNorm, halfFadeNorm, dDawn);
            return Mathf.SmoothStep(0f, 1f, progress);
        }

        // --- Outside any transition zone ---
        // Midday (dawn < t < dusk-halfFade) → full day.
        // Midnight (dusk+halfFade < t < dawn-halfFade wrapping) → full night.
        return (t > halfFadeNorm && t < dayFraction - halfFadeNorm) ? 1f : 0f;
    }

    // -------------------------------------------------------------------------
    // Public API
    // -------------------------------------------------------------------------

    /// <summary>Jump the internal clock to a specific normalised position (0–1).</summary>
    public void SetCycleTime(float normalizedTime)
    {
        float cycleLength = dayLengthMinutes + nightLengthMinutes;
        elapsedMinutes = Mathf.Clamp01(normalizedTime) * cycleLength;
    }

    /// <summary>Change master volume at runtime (e.g. from a settings menu).</summary>
    public void SetMasterVolume(float volume)
    {
        masterVolume = Mathf.Clamp01(volume);
    }

    /// <summary>Instantly mute all music (e.g. when entering a cutscene).</summary>
    public void SetMuted(bool muted)
    {
        float vol = muted ? 0f : masterVolume;
        daySource.volume   = vol * debugDayVolume;
        nightSource.volume = vol * debugNightVolume;
    }
}
