using System;
using UnityEngine;
using EthraClone.TrialWeek;

/// <summary>
/// Tracks the local player's total XP and current level.
/// Fires events on XP gain and on level-up.
///
/// Add as a component on the Player GameObject (or a dedicated Managers object).
/// Assign a LevelDefinition ScriptableObject in the Inspector.
///
/// To award XP: XPSystem.Instance.AwardXP(amount, worldPosition);
///
/// Events:
///   OnXPGained(int gained, int totalXP)    — fired every time XP is awarded.
///   OnLevelUp(int newLevel)                — fired when the player levels up.
/// </summary>
public class XPSystem : MonoBehaviour
{
    public static XPSystem Instance { get; private set; }

    /// <summary>Fired whenever XP is gained. Args: (xpGained, totalXP).</summary>
    public static event Action<int, int> OnXPGained;

    /// <summary>Fired when the player levels up. Arg: new level.</summary>
    public static event Action<int> OnLevelUp;

    [Header("Configuration")]
    [Tooltip("Level table ScriptableObject. Create via Assets → Create → LIT-ISO → Progression → Level Definition.")]
    public LevelDefinition levelTable;

    [Header("State (read-only at runtime)")]
    [SerializeField] private int currentLevel = 1;
    [SerializeField] private int totalXP = 0;

    public int CurrentLevel => currentLevel;
    public int TotalXP => totalXP;

    /// <summary>XP progress within the current level [0, xpForNextLevel).</summary>
    public int XPInCurrentLevel
    {
        get
        {
            if (levelTable == null) return totalXP;
            int xpForCurrent = levelTable.XPForLevel(currentLevel);
            int xpForNext    = levelTable.XPForLevel(currentLevel + 1);
            if (xpForNext == int.MaxValue) return 0;  // already max level
            return Mathf.Max(0, totalXP - xpForCurrent);
        }
    }

    /// <summary>XP needed to reach the next level from the current level boundary.</summary>
    public int XPNeededForNextLevel
    {
        get
        {
            if (levelTable == null) return int.MaxValue;
            int xpForCurrent = levelTable.XPForLevel(currentLevel);
            int xpForNext    = levelTable.XPForLevel(currentLevel + 1);
            if (xpForNext == int.MaxValue) return int.MaxValue;
            return xpForNext - xpForCurrent;
        }
    }

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

    /// <summary>
    /// Award XP to the player and spawn a floating "+X XP" label at <paramref name="worldPos"/>.
    /// Checks for level-up automatically.
    /// </summary>
    public void AwardXP(int amount, Vector3 worldPos)
    {
        if (amount <= 0) return;

        totalXP += amount;

        // Floating text
        WorldFloatingText.Spawn(worldPos + Vector3.up * 0.4f,
                                $"+{amount} XP",
                                new Color(0.4f, 0.9f, 1f));

        OnXPGained?.Invoke(amount, totalXP);

        // Log to ActionTracker
        if (ActionTracker.Instance != null)
            ActionTracker.Instance.LogAction("local_player", "XPGained", amount.ToString(), amount);

        // On-kill mana restore
        if (PlayerMana.Instance != null)
            PlayerMana.Instance.OnKill();

        CheckLevelUp();
    }

    /// <summary>
    /// Load persisted XP and level (called by SaveGameManager on load).
    /// Does NOT fire events — use only for restoring saved state.
    /// </summary>
    public void LoadState(int savedXP, int savedLevel)
    {
        totalXP      = savedXP;
        currentLevel = Mathf.Max(1, savedLevel);
    }

    // -------------------------------------------------------------------------
    // Private helpers
    // -------------------------------------------------------------------------

    private void CheckLevelUp()
    {
        if (levelTable == null) return;

        int maxLevel = levelTable.MaxLevel;
        while (currentLevel < maxLevel)
        {
            int xpForNext = levelTable.XPForLevel(currentLevel + 1);
            if (totalXP < xpForNext) break;

            currentLevel++;
            HandleLevelUp(currentLevel);
        }
    }

    private void HandleLevelUp(int newLevel)
    {
        var entry = levelTable.GetEntry(newLevel);

        // Apply stat-point award and HP/mana bonuses
        if (entry.HasValue)
        {
            if (PlayerStats.Instance != null)
                PlayerStats.Instance.ApplyBonus(statPoints: entry.Value.statPointsAwarded,
                                                vit: 0, INT_bonus: 0);  // raw stats via stat points

            if (PlayerHealth.Instance != null && entry.Value.hpBonus > 0)
                PlayerHealth.Instance.SetMaxHealth(PlayerHealth.Instance.maxHealth + entry.Value.hpBonus, refillToMax: false);

            if (PlayerMana.Instance != null && entry.Value.manaBonus > 0)
                PlayerMana.Instance.SetMaxMana(PlayerMana.Instance.MaxMana + entry.Value.manaBonus);
        }

        // Announce via System notifier
        string msg = $"Level Up! You are now Level {newLevel}.";
        if (entry.HasValue && !string.IsNullOrEmpty(entry.Value.milestoneDescription))
            msg += $" {entry.Value.milestoneDescription}";

        if (SystemNotifier.Instance != null)
            SystemNotifier.Instance.Announce(msg, SystemNotifier.MessageType.LevelUp);

        OnLevelUp?.Invoke(newLevel);

        Debug.Log($"[XPSystem] Level up → {newLevel}  (total XP: {totalXP})");
    }
}
