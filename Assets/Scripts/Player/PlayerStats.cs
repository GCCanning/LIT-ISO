using System;
using UnityEngine;

/// <summary>
/// Master stat sheet for the local player.
/// Holds the six core LitRPG attributes (STR/AGI/VIT/INT/WIS/END) and exposes
/// all derived values (MaxHP, MaxMana, MoveSpeed, damage) as computed properties.
///
/// Add as a component on the Player GameObject alongside PlayerHealth and PlayerInventory.
///
/// Events:
///   OnStatsChanged  — fired after any stat allocation or level-up bonus.
///
/// Extending stats:
///   To add a new attribute, add a public int field and a derived property below.
///   No other script changes required — all consumers read properties, not raw fields.
/// </summary>
public class PlayerStats : MonoBehaviour
{
    public static PlayerStats Instance { get; private set; }

    /// <summary>Fired whenever any stat changes (allocation or level-up bonus).</summary>
    public static event Action OnStatsChanged;

    // -------------------------------------------------------------------------
    // Core Attributes — serialized so they're visible in the Inspector
    // -------------------------------------------------------------------------

    [Header("Core Attributes")]
    [Tooltip("Melee damage and carry weight.")]
    [Min(1)] public int STR = 5;

    [Tooltip("Move speed, dodge chance, attack speed.")]
    [Min(1)] public int AGI = 5;

    [Tooltip("Max HP and HP regen rate.")]
    [Min(1)] public int VIT = 5;

    [Tooltip("Spell damage and max mana.")]
    [Min(1)] public int INT = 5;

    [Tooltip("Mana regen and cooldown reduction.")]
    [Min(1)] public int WIS = 5;

    [Tooltip("Stamina pool, dash count, knockback resistance.")]
    [Min(1)] public int END = 5;

    // -------------------------------------------------------------------------
    // Unspent stat points (awarded by XPSystem on level-up)
    // -------------------------------------------------------------------------

    [Header("Stat Points")]
    [Tooltip("Unspent points the player can allocate.")]
    public int StatPointsAvailable = 0;

    // -------------------------------------------------------------------------
    // Derived Values (read-only computed properties)
    // -------------------------------------------------------------------------

    public float MaxHP      => 50f + (VIT * 10f);
    public float MaxMana    => 20f + (INT * 8f);
    public float MoveSpeed  => 3.5f + (AGI * 0.05f);
    public float MeleeDmg   => 5f  + (STR * 2.5f);
    public float SpellDmg   => 5f  + (INT * 2.5f);
    public float ManaRegen  => WIS * 0.5f;          // per second
    public float CritChance => AGI * 0.002f;        // 0.2% per AGI point
    public float DashCount  => 1 + (END / 10);      // +1 dash per 10 END
    public float CooldownReduction => Mathf.Min(0.40f, WIS * 0.004f); // cap 40%

    // -------------------------------------------------------------------------
    // Singleton lifecycle
    // -------------------------------------------------------------------------

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // -------------------------------------------------------------------------
    // Public API
    // -------------------------------------------------------------------------

    public enum StatType { STR, AGI, VIT, INT, WIS, END }

    /// <summary>
    /// Spend one stat point on the chosen attribute.
    /// Does nothing if no points are available.
    /// </summary>
    public void AllocateStat(StatType stat)
    {
        if (StatPointsAvailable <= 0) return;

        StatPointsAvailable--;
        switch (stat)
        {
            case StatType.STR: STR++; break;
            case StatType.AGI: AGI++; break;
            case StatType.VIT: VIT++; break;
            case StatType.INT: INT++; break;
            case StatType.WIS: WIS++; break;
            case StatType.END: END++; break;
        }

        ApplyDerivedChanges();
        OnStatsChanged?.Invoke();
    }

    /// <summary>
    /// Apply flat bonuses directly (called by XPSystem on level-up or class assignment).
    /// </summary>
    public void ApplyBonus(int str = 0, int agi = 0, int vit = 0, int INT_bonus = 0, int wis = 0, int end = 0, int statPoints = 0)
    {
        STR += str;
        AGI += agi;
        VIT += vit;
        INT += INT_bonus;
        WIS += wis;
        END += end;
        StatPointsAvailable += statPoints;

        ApplyDerivedChanges();
        OnStatsChanged?.Invoke();
    }

    // -------------------------------------------------------------------------
    // Private helpers
    // -------------------------------------------------------------------------

    /// <summary>
    /// Push derived stat changes out to other systems (HP cap, Mana cap, speed).
    /// Called after any stat change.
    /// </summary>
    private void ApplyDerivedChanges()
    {
        // Update PlayerHealth max if it exists
        if (PlayerHealth.Instance != null)
        {
            int newMax = Mathf.RoundToInt(MaxHP);
            if (newMax != PlayerHealth.Instance.maxHealth)
                PlayerHealth.Instance.SetMaxHealth(newMax);
        }

        // Update PlayerMana max if it exists
        if (PlayerMana.Instance != null)
        {
            int newMax = Mathf.RoundToInt(MaxMana);
            if (newMax != PlayerMana.Instance.MaxMana)
                PlayerMana.Instance.SetMaxMana(newMax);
        }
    }
}
