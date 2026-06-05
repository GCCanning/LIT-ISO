using System;
using UnityEngine;

/// <summary>
/// Tracks the local player's current and maximum mana.
/// Mirrors PlayerHealth in structure and is driven by PlayerStats for its max value.
///
/// Add as a component on the Player GameObject.
///
/// Events:
///   OnManaChanged(int currentMana, int maxMana)
///     Fired after any mana change (spend, regen, set-max).
/// </summary>
public class PlayerMana : MonoBehaviour
{
    public static PlayerMana Instance { get; private set; }

    /// <summary>Fired whenever mana changes. Args: (currentMana, maxMana).</summary>
    public event Action<int, int> OnManaChanged;

    [Header("Settings")]
    [Min(1)] public int maxMana = 100;

    [Tooltip("Mana restored per second while not casting. Overridden by PlayerStats.ManaRegen when available.")]
    [Min(0f)] public float baseRegenPerSecond = 2f;

    [Tooltip("Flat mana restored on each enemy kill (the classic LitRPG kill-regen feel).")]
    [Min(0)] public int manaOnKill = 5;

    public int CurrentMana { get; private set; }
    public int MaxMana => maxMana;

    private float regenAccumulator;

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
        CurrentMana = maxMana;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Update()
    {
        // Passive regen — use PlayerStats value if available
        float regen = (PlayerStats.Instance != null)
            ? PlayerStats.Instance.ManaRegen
            : baseRegenPerSecond;

        if (regen > 0f && CurrentMana < maxMana)
        {
            regenAccumulator += regen * Time.deltaTime;
            int ticks = Mathf.FloorToInt(regenAccumulator);
            if (ticks > 0)
            {
                regenAccumulator -= ticks;
                Restore(ticks);
            }
        }
    }

    // -------------------------------------------------------------------------
    // Public API
    // -------------------------------------------------------------------------

    /// <summary>
    /// Attempt to spend <paramref name="amount"/> mana.
    /// Returns true and deducts the cost if enough mana is available; false otherwise.
    /// </summary>
    public bool TrySpend(int amount)
    {
        if (amount <= 0) return true;
        if (CurrentMana < amount) return false;

        CurrentMana -= amount;
        OnManaChanged?.Invoke(CurrentMana, maxMana);
        return true;
    }

    /// <summary>Restore mana by <paramref name="amount"/> (capped at MaxMana).</summary>
    public void Restore(int amount)
    {
        if (amount <= 0) return;
        CurrentMana = Mathf.Min(maxMana, CurrentMana + amount);
        OnManaChanged?.Invoke(CurrentMana, maxMana);
    }

    /// <summary>Called by PlayerStats when VIT/INT changes affect the mana cap.</summary>
    public void SetMaxMana(int newMax, bool refill = false)
    {
        maxMana = Mathf.Max(1, newMax);
        CurrentMana = refill ? maxMana : Mathf.Min(CurrentMana, maxMana);
        OnManaChanged?.Invoke(CurrentMana, maxMana);
    }

    /// <summary>Restore mana on kill — call from XPSystem or SpellCaster.</summary>
    public void OnKill()
    {
        if (manaOnKill > 0) Restore(manaOnKill);
    }

    /// <summary>Fill to max (campfire rest, level-up, etc.).</summary>
    public void RefillFull()
    {
        CurrentMana = maxMana;
        OnManaChanged?.Invoke(CurrentMana, maxMana);
    }
}
