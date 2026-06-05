using UnityEngine;

/// <summary>
/// Defines a single spell or active skill.
/// Create instances via Assets → Create → LIT-ISO → Combat → Spell Definition.
///
/// To add a new spell:
///   1. Create a SpellDefinition.asset in Assets/Data/Spells/
///   2. Set delivery type, damage, cost, VFX
///   3. Drag into a ClassDefinition's startingSkills array or SpellCaster's equippedSpells
///   No code changes required for new spells.
///
/// To add a new delivery type:
///   Add a value to DeliveryType and implement a new class that implements ISpellDelivery.
/// </summary>
[CreateAssetMenu(fileName = "SpellDefinition", menuName = "LIT-ISO/Combat/Spell Definition")]
public class SpellDefinition : ScriptableObject
{
    // -------------------------------------------------------------------------
    // Identity
    // -------------------------------------------------------------------------

    [Header("Identity")]
    public string spellId;
    public string displayName;
    [TextArea(1, 3)]
    public string description;
    public Sprite icon;

    // -------------------------------------------------------------------------
    // Cost & Timing
    // -------------------------------------------------------------------------

    [Header("Cost & Timing")]
    [Tooltip("Mana spent when cast.")]
    [Min(0)] public int manaCost = 20;

    [Tooltip("Seconds before this spell can be cast again.")]
    [Min(0f)] public float cooldownSeconds = 1.5f;

    [Tooltip("Seconds the player is locked into the cast animation before the spell fires. 0 = instant.")]
    [Min(0f)] public float castTimeSeconds = 0f;

    // -------------------------------------------------------------------------
    // Damage
    // -------------------------------------------------------------------------

    [Header("Damage")]
    [Min(0f)] public float baseDamage = 25f;
    [Range(0f, 1f)] public float critChance = 0.10f;
    [Min(1f)]       public float critMultiplier = 2f;

    public enum DamageType { Physical, Fire, Ice, Lightning, Arcane, Nature, Void, Holy, Poison }
    public DamageType damageType = DamageType.Arcane;

    // -------------------------------------------------------------------------
    // Delivery
    // -------------------------------------------------------------------------

    public enum DeliveryType
    {
        Projectile,   // Fires a moving projectile toward cursor/target
        AoE,          // Instant area explosion at cursor position
        SelfBuff,     // Applies a buff to the caster immediately
        TargetBuff,   // Applies a buff to a friendly target
        Beam,         // Instant line from player to range
        Chain,        // Bounces between nearby enemies
        Summon,       // Spawns a minion
    }

    [Header("Delivery")]
    public DeliveryType delivery = DeliveryType.Projectile;

    [Tooltip("Prefab for Projectile delivery. Must have SpellProjectile component.")]
    public GameObject projectilePrefab;

    [Tooltip("Projectile travel speed in world units per second.")]
    [Min(1f)] public float projectileSpeed = 8f;

    [Tooltip("Explosion radius for AoE delivery (world units).")]
    [Min(0.1f)] public float aoeRadius = 2.5f;

    [Tooltip("Maximum cast range. Projectiles despawn, AoE clamps target, beams stop here.")]
    [Min(1f)] public float range = 12f;

    [Tooltip("Number of chain bounces (Chain delivery only).")]
    [Min(1)] public int chainCount = 3;

    // -------------------------------------------------------------------------
    // Status Effect
    // -------------------------------------------------------------------------

    [Header("Status Effect on Hit")]
    [Tooltip("StatusEffectDefinition to apply on hit. Leave null for no effect.")]
    public StatusEffectDefinition appliesEffect;

    [Range(0f, 1f)]
    [Tooltip("Chance (0-1) that the status effect is applied per hit.")]
    public float statusChance = 0.30f;

    // -------------------------------------------------------------------------
    // VFX / SFX
    // -------------------------------------------------------------------------

    [Header("VFX / SFX")]
    [Tooltip("Particle system played at the caster on cast start.")]
    public ParticleSystem castVFX;

    [Tooltip("Particle system played at the hit point.")]
    public ParticleSystem hitVFX;

    [Tooltip("Particle system that follows the projectile (Projectile delivery).")]
    public ParticleSystem trailVFX;

    public AudioClip castSound;
    public AudioClip hitSound;

    [Range(0f, 1f)] public float castVolume = 0.7f;
    [Range(0f, 1f)] public float hitVolume  = 0.6f;

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    /// <summary>Compute final damage, including crit roll.</summary>
    public float RollDamage(float spellDmgBonus = 0f)
    {
        float dmg = baseDamage + spellDmgBonus;
        bool  crit = UnityEngine.Random.value < critChance;
        return crit ? dmg * critMultiplier : dmg;
    }

    public bool IsCrit() => UnityEngine.Random.value < critChance;
}
