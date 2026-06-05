using System;
using UnityEngine;

/// <summary>
/// Tracks the local player's current and maximum health.
/// Add as a component on the Player GameObject alongside PlayerInventory.
///
/// Events:
///   OnHealthChanged(int currentHealth, int maxHealth)
///     Fired after TakeDamage, Heal, or SetMaxHealth.
/// </summary>
public class PlayerHealth : MonoBehaviour
{
    public static PlayerHealth Instance { get; private set; }

    [Min(1)]
    public int maxHealth = 100;

    public int CurrentHealth { get; private set; }

    /// <summary>Fired whenever health changes. Args: (currentHealth, maxHealth).</summary>
    public event Action<int, int> OnHealthChanged;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }
        Instance = this;
        CurrentHealth = maxHealth;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // -------------------------------------------------------------------------
    // Public API
    // -------------------------------------------------------------------------

    public void TakeDamage(int amount)
    {
        if (amount <= 0) return;
        CurrentHealth = Mathf.Max(0, CurrentHealth - amount);
        OnHealthChanged?.Invoke(CurrentHealth, maxHealth);
    }

    public void Heal(int amount)
    {
        if (amount <= 0) return;
        CurrentHealth = Mathf.Min(maxHealth, CurrentHealth + amount);
        OnHealthChanged?.Invoke(CurrentHealth, maxHealth);
    }

    /// <summary>
    /// Change the maximum health value.
    /// If <paramref name="refillToMax"/> is true the current health is also set to the new max.
    /// </summary>
    public void SetMaxHealth(int newMax, bool refillToMax = false)
    {
        maxHealth = Mathf.Max(1, newMax);
        CurrentHealth = refillToMax ? maxHealth : Mathf.Min(CurrentHealth, maxHealth);
        OnHealthChanged?.Invoke(CurrentHealth, maxHealth);
    }
}
