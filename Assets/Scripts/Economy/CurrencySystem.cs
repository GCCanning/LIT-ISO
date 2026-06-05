using System;
using UnityEngine;

/// <summary>
/// Manages the five-tier currency: Copper → Silver → Gold → Platinum → Void Crystal.
/// Stores everything internally as Copper for precision.
///
/// Add as a component on a persistent Managers GameObject.
///
/// Events:
///   OnCurrencyChanged(long newCopperTotal)
/// </summary>
public class CurrencySystem : MonoBehaviour
{
    public static CurrencySystem Instance { get; private set; }

    public static event Action<long> OnCurrencyChanged;

    // -------------------------------------------------------------------------
    // Conversion constants
    // -------------------------------------------------------------------------

    public const long CopperPerSilver   = 100L;
    public const long CopperPerGold     = 10_000L;
    public const long CopperPerPlatinum = 1_000_000L;
    public const long CopperPerVoid     = 100_000_000L;

    // -------------------------------------------------------------------------
    // Runtime state
    // -------------------------------------------------------------------------

    [SerializeField] private long totalCopper = 0L;

    // -------------------------------------------------------------------------
    // Derived getters (whole-unit counts)
    // -------------------------------------------------------------------------

    public long TotalCopper  => totalCopper;
    public long VoidCrystals => totalCopper / CopperPerVoid;
    public long Platinum     => (totalCopper % CopperPerVoid) / CopperPerPlatinum;
    public long Gold         => (totalCopper % CopperPerPlatinum) / CopperPerGold;
    public long Silver       => (totalCopper % CopperPerGold) / CopperPerSilver;
    public long Copper       => totalCopper % CopperPerSilver;

    // -------------------------------------------------------------------------
    // Singleton lifecycle
    // -------------------------------------------------------------------------

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void OnDestroy() { if (Instance == this) Instance = null; }

    // -------------------------------------------------------------------------
    // Public API
    // -------------------------------------------------------------------------

    /// <summary>Add copper amount (converts from higher tiers automatically).</summary>
    public void AddCopper(long amount)
    {
        totalCopper += amount;
        OnCurrencyChanged?.Invoke(totalCopper);
    }

    public void AddSilver(long silver)   => AddCopper(silver * CopperPerSilver);
    public void AddGold(long gold)       => AddCopper(gold   * CopperPerGold);
    public void AddPlatinum(long plat)   => AddCopper(plat   * CopperPerPlatinum);

    /// <summary>Returns false and announces if not enough funds.</summary>
    public bool SpendCopper(long amount)
    {
        if (totalCopper < amount)
        {
            SystemNotifier.Instance?.Announce("Not enough currency.", SystemNotifier.MessageType.Warning);
            return false;
        }
        totalCopper -= amount;
        OnCurrencyChanged?.Invoke(totalCopper);
        return true;
    }

    public bool SpendSilver(long silver)   => SpendCopper(silver * CopperPerSilver);
    public bool SpendGold(long gold)       => SpendCopper(gold   * CopperPerGold);
    public bool SpendPlatinum(long plat)   => SpendCopper(plat   * CopperPerPlatinum);

    public bool HasGold(long gold) => totalCopper >= gold * CopperPerGold;

    /// <summary>Load saved copper value (no events fired).</summary>
    public void LoadState(long savedCopper) => totalCopper = savedCopper;

    /// <summary>Human-readable wallet string e.g. "3 Gold 42 Silver 7 Copper".</summary>
    public string FormatWallet()
    {
        var sb = new System.Text.StringBuilder();
        if (VoidCrystals > 0) sb.Append($"{VoidCrystals} Void Crystal ");
        if (Platinum > 0)     sb.Append($"{Platinum} Platinum ");
        if (Gold > 0)         sb.Append($"{Gold} Gold ");
        if (Silver > 0)       sb.Append($"{Silver} Silver ");
        if (Copper > 0 || totalCopper == 0) sb.Append($"{Copper} Copper");
        return sb.ToString().Trim();
    }

    /// <summary>Compact form e.g. "3g 42s 7c".</summary>
    public string FormatCompact()
    {
        if (VoidCrystals > 0) return $"{VoidCrystals}vc {Platinum}p {Gold}g";
        if (Platinum > 0)     return $"{Platinum}p {Gold}g {Silver}s";
        if (Gold > 0)         return $"{Gold}g {Silver}s {Copper}c";
        if (Silver > 0)       return $"{Silver}s {Copper}c";
        return $"{Copper}c";
    }
}
