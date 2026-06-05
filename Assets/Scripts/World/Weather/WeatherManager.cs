using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using EthraClone.TrialWeek;

/// <summary>
/// Controls the active weather state for the game world.
/// Weather changes are triggered by:
///   - TrialWeekManager.OnWorldDayStart (daily re-roll)
///   - IsoWorldChunkManager firing OnBiomeChanged (player enters a new biome)
///   - WeatherManager.ForceWeather() (world events, Blood Moon, etc.)
///
/// Add as a component on a persistent Managers GameObject.
/// Assign all WeatherDefinition assets to allWeathers[].
///
/// Events:
///   OnWeatherChanged(WeatherDefinition newWeather)
///     → IsoLightingController subscribes to lerp sky colour.
///     → DayNightMusicManager subscribes to crossfade ambient audio.
///     → IsoPlayerController reads CurrentWeather.playerSpeedMultiplier every frame.
/// </summary>
public class WeatherManager : MonoBehaviour
{
    public static WeatherManager Instance { get; private set; }

    public static event Action<WeatherDefinition> OnWeatherChanged;

    // -------------------------------------------------------------------------
    // Inspector
    // -------------------------------------------------------------------------

    [Header("Weather Pool")]
    [Tooltip("All possible WeatherDefinition assets. Add new ones here to expand the system.")]
    public WeatherDefinition[] allWeathers;

    [Tooltip("Default weather when no biome-specific weather matches.")]
    public WeatherDefinition clearWeather;

    [Header("Transition")]
    [Tooltip("Seconds to crossfade particle/audio when weather changes.")]
    [Min(0.5f)] public float transitionDuration = 4f;

    [Header("Duration")]
    [Min(30f)] public float minWeatherDurationSeconds = 120f;
    [Min(60f)] public float maxWeatherDurationSeconds = 600f;

    [Header("Outdoor DoT")]
    [Tooltip("Seconds between outdoor damage ticks (for blizzard, ashfall, etc.).")]
    [Min(1f)] public float outdoorDamageInterval = 3f;

    // -------------------------------------------------------------------------
    // State
    // -------------------------------------------------------------------------

    public WeatherDefinition CurrentWeather { get; private set; }

    private ParticleSystem  activeParticles;
    private AudioSource     ambientAudio;
    private Coroutine       changeRoutine;
    private Coroutine       doTRoutine;
    private string          currentBiomeId = "";

    // -------------------------------------------------------------------------
    // Singleton lifecycle
    // -------------------------------------------------------------------------

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        ambientAudio = GetComponent<AudioSource>();
        if (ambientAudio == null) ambientAudio = gameObject.AddComponent<AudioSource>();
        ambientAudio.loop        = true;
        ambientAudio.spatialBlend = 0f;
        ambientAudio.playOnAwake = false;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Start()
    {
        // Hook into TrialWeekManager day cycle
        var twm = FindFirstObjectByType<EthraClone.TrialWeek.TrialWeekManager>();
        if (twm != null)
            twm.OnWorldDayStart += () => EvaluateWeather(currentBiomeId);

        // Start with clear weather
        if (clearWeather != null)
            ApplyWeatherImmediate(clearWeather);
    }

    // -------------------------------------------------------------------------
    // Public API
    // -------------------------------------------------------------------------

    /// <summary>
    /// Re-evaluate weather for the given biome.
    /// Called by IsoWorldChunkManager when the player's primary biome changes.
    /// </summary>
    public void EvaluateWeather(string biomeId)
    {
        currentBiomeId = biomeId;

        WeatherDefinition next = PickWeather(biomeId);
        if (next == null || next == CurrentWeather) return;

        if (changeRoutine != null) StopCoroutine(changeRoutine);
        changeRoutine = StartCoroutine(TransitionTo(next));
    }

    /// <summary>Force a specific weather immediately (world events, Blood Moon, etc.).</summary>
    public void ForceWeather(WeatherDefinition weather, bool instant = false)
    {
        if (weather == null) return;
        if (changeRoutine != null) StopCoroutine(changeRoutine);

        if (instant) ApplyWeatherImmediate(weather);
        else changeRoutine = StartCoroutine(TransitionTo(weather));
    }

    /// <summary>Force weather by ID string.</summary>
    public void ForceWeatherById(string weatherId)
    {
        if (allWeathers == null) return;
        foreach (var w in allWeathers)
            if (w != null && w.weatherId == weatherId) { ForceWeather(w); return; }
    }

    // -------------------------------------------------------------------------
    // Private — weather selection
    // -------------------------------------------------------------------------

    private WeatherDefinition PickWeather(string biomeId)
    {
        if (allWeathers == null || allWeathers.Length == 0)
            return clearWeather;

        // Build weighted list for this biome
        float totalWeight = 0f;
        var candidates = new List<(WeatherDefinition def, float weight)>();

        foreach (var w in allWeathers)
        {
            if (w == null) continue;
            float weight = w.GetWeightForBiome(biomeId);
            if (weight <= 0f) continue;
            candidates.Add((w, weight));
            totalWeight += weight;
        }

        if (candidates.Count == 0) return clearWeather;

        float roll = UnityEngine.Random.Range(0f, totalWeight);
        float cum  = 0f;
        foreach (var (def, weight) in candidates)
        {
            cum += weight;
            if (roll <= cum) return def;
        }

        return clearWeather;
    }

    // -------------------------------------------------------------------------
    // Private — transitions
    // -------------------------------------------------------------------------

    private void ApplyWeatherImmediate(WeatherDefinition weather)
    {
        CurrentWeather = weather;
        UpdateParticles(weather);
        UpdateAudio(weather, fadeIn: false);
        OnWeatherChanged?.Invoke(weather);
    }

    private IEnumerator TransitionTo(WeatherDefinition next)
    {
        // Fade out existing particles
        if (activeParticles != null)
        {
            var main = activeParticles.main;
            float elapsed = 0f;
            float startRate = main.maxParticles;
            while (elapsed < transitionDuration * 0.5f)
            {
                elapsed += Time.deltaTime;
                // Gradually reduce emission
                yield return null;
            }
            activeParticles.Stop();
            Destroy(activeParticles.gameObject, 2f);
            activeParticles = null;
        }

        // Crossfade audio
        float audioFadeTime = transitionDuration;
        StartCoroutine(FadeAudio(ambientAudio, 0f, audioFadeTime * 0.4f));
        yield return new WaitForSeconds(audioFadeTime * 0.4f);

        // Apply new weather
        CurrentWeather = next;
        UpdateParticles(next);
        UpdateAudio(next, fadeIn: true);
        OnWeatherChanged?.Invoke(next);

        // Announce weather change if notable
        if (next != clearWeather)
            SystemNotifier.Instance?.Announce($"The weather shifts: {next.displayName}.",
                                              SystemNotifier.MessageType.Info);

        // Start outdoor DoT if needed
        if (doTRoutine != null) StopCoroutine(doTRoutine);
        if (next.outdoorDamagePerSecond > 0f)
            doTRoutine = StartCoroutine(OutdoorDamageRoutine(next));

        Debug.Log($"[WeatherManager] Weather → {next.displayName}");
    }

    private void UpdateParticles(WeatherDefinition weather)
    {
        if (weather.particlePrefab == null) return;

        if (activeParticles != null)
        {
            activeParticles.Stop();
            Destroy(activeParticles.gameObject, 2f);
        }

        var follow = Camera.main != null ? Camera.main.transform : transform;
        activeParticles = Instantiate(weather.particlePrefab, follow.position, Quaternion.identity);
        activeParticles.transform.SetParent(follow);
        activeParticles.Play();
    }

    private void UpdateAudio(WeatherDefinition weather, bool fadeIn)
    {
        if (weather.ambientLoop == null)
        {
            ambientAudio.Stop();
            return;
        }

        ambientAudio.clip   = weather.ambientLoop;
        ambientAudio.volume = fadeIn ? 0f : weather.ambientVolume;
        ambientAudio.Play();

        if (fadeIn)
            StartCoroutine(FadeAudio(ambientAudio, weather.ambientVolume, transitionDuration * 0.6f));
    }

    private IEnumerator FadeAudio(AudioSource src, float targetVol, float duration)
    {
        float start   = src.volume;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed    += Time.deltaTime;
            src.volume  = Mathf.Lerp(start, targetVol, elapsed / duration);
            yield return null;
        }
        src.volume = targetVol;
        if (targetVol <= 0f) src.Stop();
    }

    // -------------------------------------------------------------------------
    // Outdoor damage-over-time
    // -------------------------------------------------------------------------

    private IEnumerator OutdoorDamageRoutine(WeatherDefinition weather)
    {
        while (CurrentWeather == weather && weather.outdoorDamagePerSecond > 0f)
        {
            yield return new WaitForSeconds(outdoorDamageInterval);

            if (PlayerHealth.Instance == null) continue;

            float dmg = weather.outdoorDamagePerSecond * outdoorDamageInterval;
            PlayerHealth.Instance.TakeDamage(Mathf.RoundToInt(dmg));

            WorldFloatingText.Spawn(
                PlayerHealth.Instance.transform.position + Vector3.up * 0.5f,
                $"-{Mathf.RoundToInt(dmg)} ({weather.displayName})",
                new Color(0.7f, 0.7f, 0.8f), fontSize: 20);
        }
    }
}
