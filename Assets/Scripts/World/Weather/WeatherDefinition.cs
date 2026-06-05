using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Defines a single weather type — its visuals, audio, and gameplay modifiers.
/// Create instances via Assets → Create → LIT-ISO → Weather → Weather Definition.
///
/// To add a new weather type:
///   1. Create a WeatherDefinition.asset in Assets/Data/Weather/
///   2. Fill in all fields
///   3. Add the asset to WeatherManager.allWeathers[]
///   No code changes required.
/// </summary>
[CreateAssetMenu(fileName = "WeatherDefinition", menuName = "LIT-ISO/Weather/Weather Definition")]
public class WeatherDefinition : ScriptableObject
{
    // -------------------------------------------------------------------------
    // Identity
    // -------------------------------------------------------------------------

    [Header("Identity")]
    [Tooltip("Stable internal ID. Never change after save data is created.")]
    public string weatherId;
    public string displayName;

    // -------------------------------------------------------------------------
    // Visuals
    // -------------------------------------------------------------------------

    [Header("Visuals")]
    [Tooltip("Particle system prefab (rain, snow, fog, ash). Instantiated and follows the player.")]
    public ParticleSystem particlePrefab;

    [Tooltip("Sky/ambient tint to lerp toward during this weather.")]
    public Color skyTint = Color.white;

    [Tooltip("Multiplier on the scene's ambient light intensity (0.4 = very dark storm).")]
    [Range(0.1f, 1.5f)] public float ambientLightMultiplier = 1f;

    [Tooltip("Camera render distance multiplier for fog effect (0.4 = heavy fog, 1 = clear).")]
    [Range(0.1f, 1f)]   public float visibilityMultiplier = 1f;

    // -------------------------------------------------------------------------
    // Audio
    // -------------------------------------------------------------------------

    [Header("Audio")]
    public AudioClip ambientLoop;
    [Range(0f, 1f)] public float ambientVolume = 0.6f;

    // -------------------------------------------------------------------------
    // Gameplay Modifiers
    // -------------------------------------------------------------------------

    [Header("Gameplay Modifiers")]
    [Tooltip("Player and enemy movement speed multiplier.")]
    [Range(0.3f, 1.5f)] public float playerSpeedMultiplier = 1f;

    [Tooltip("Projectile and ranged spell accuracy multiplier. 0.6 = sandstorm, 1.0 = normal.")]
    [Range(0.3f, 1.5f)] public float rangedAccuracyMultiplier = 1f;

    [Tooltip("Spell damage multiplier for matching DamageType (e.g. Ice spells +20% in Snow).")]
    [Range(0.5f, 2f)]   public float matchingSpellDamageBonus = 1f;

    [Tooltip("Which damage type gets the bonus above.")]
    public SpellDefinition.DamageType boostedDamageType;

    [Tooltip("Damage type that is penalised (e.g. Fire -20% in Rain).")]
    public SpellDefinition.DamageType penalisedDamageType;
    [Range(0.3f, 1f)]   public float penalisedDamageMultiplier = 1f;

    [Tooltip("DoT damage dealt per second to players outside a shelter during this weather (0 = none).")]
    [Min(0f)] public float outdoorDamagePerSecond = 0f;

    // -------------------------------------------------------------------------
    // Biome weights — higher = more likely to occur in matching biomes
    // -------------------------------------------------------------------------

    [Header("Biome Spawn Weights")]
    [Tooltip("Each entry pairs a biome ID with a relative spawn weight.")]
    public List<BiomeWeatherWeight> biomeWeights;

    [System.Serializable]
    public struct BiomeWeatherWeight
    {
        [Tooltip("Must match IsoBiomeDefinition.biomeId or a substring of its name.")]
        public string biomeId;
        [Range(0f, 10f)]
        public float  weight;
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    public float GetWeightForBiome(string biomeId)
    {
        if (biomeWeights == null) return 0f;
        foreach (var w in biomeWeights)
            if (!string.IsNullOrEmpty(w.biomeId) && biomeId.ToLower().Contains(w.biomeId.ToLower()))
                return w.weight;
        return 0f;
    }
}
