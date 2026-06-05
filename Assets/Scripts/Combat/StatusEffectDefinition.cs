using UnityEngine;

/// <summary>
/// Defines a single status effect — its duration, tick behaviour, and stat modifiers.
/// Create instances via Assets → Create → LIT-ISO → Combat → Status Effect.
///
/// To add a new effect: create a StatusEffectDefinition.asset — no code changes needed.
/// The StatusEffectHandler reads these values and applies them generically.
/// </summary>
[CreateAssetMenu(fileName = "StatusEffect", menuName = "LIT-ISO/Combat/Status Effect")]
public class StatusEffectDefinition : ScriptableObject
{
    // -------------------------------------------------------------------------
    // Identity
    // -------------------------------------------------------------------------

    [Header("Identity")]
    [Tooltip("Stable internal ID. Never change after creating save data.")]
    public string effectId;

    public string displayName;
    public Sprite icon;
    public Color  particleTint = Color.white;

    // -------------------------------------------------------------------------
    // Duration & Ticking
    // -------------------------------------------------------------------------

    [Header("Duration & Ticking")]
    [Tooltip("How long the effect lasts in seconds.")]
    [Min(0.1f)] public float duration = 5f;

    [Tooltip("Seconds between damage/heal ticks. 0 = no tick.")]
    [Min(0f)] public float tickInterval = 1f;

    [Tooltip("Damage dealt per tick. 0 = no damage tick.")]
    [Min(0f)] public float tickDamage = 0f;

    [Tooltip("HP restored per tick. 0 = no heal tick.")]
    [Min(0f)] public float tickHeal = 0f;

    // -------------------------------------------------------------------------
    // Stat Modifiers (multiplicative — 1.0 = no change)
    // -------------------------------------------------------------------------

    [Header("Stat Modifiers")]
    [Tooltip("Movement speed multiplier. 0.2 = very slow (Freeze), 1.5 = fast (Haste).")]
    [Range(0f, 3f)] public float speedMultiplier = 1f;

    [Tooltip("Damage dealt multiplier. 0.75 = Weakened, 1.3 = Empowered.")]
    [Range(0f, 3f)] public float damageMuliplier = 1f;

    [Tooltip("Defence multiplier. 0.7 = Vulnerable, 1.0 = normal.")]
    [Range(0f, 2f)] public float defenceMultiplier = 1f;

    // -------------------------------------------------------------------------
    // Flags
    // -------------------------------------------------------------------------

    [Header("Flags")]
    [Tooltip("Entity cannot move while this effect is active.")]
    public bool preventsMovement = false;

    [Tooltip("Entity cannot cast spells while this effect is active.")]
    public bool preventsCasting  = false;

    [Tooltip("If true, a new application of the same effect refreshes its duration rather than stacking.")]
    public bool refreshOnReapply = true;

    // -------------------------------------------------------------------------
    // VFX
    // -------------------------------------------------------------------------

    [Header("VFX")]
    [Tooltip("Particle system played on the target while the effect is active.")]
    public ParticleSystem onApplyVFX;

    [Tooltip("Particle system played when the effect expires.")]
    public ParticleSystem onExpireVFX;
}
