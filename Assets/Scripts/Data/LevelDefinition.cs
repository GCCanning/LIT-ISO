using UnityEngine;

/// <summary>
/// Defines the XP thresholds and stat bonuses for every player level.
/// Create a single LevelTable.asset in Assets/Data/Progression/ and assign it to XPSystem.
///
/// To add a new level: append a LevelEntry to the levels array — no code changes required.
/// </summary>
[CreateAssetMenu(fileName = "LevelTable", menuName = "LIT-ISO/Progression/Level Definition")]
public class LevelDefinition : ScriptableObject
{
    [System.Serializable]
    public struct LevelEntry
    {
        [Tooltip("Level number (1-based).")]
        public int level;

        [Tooltip("Total cumulative XP required to reach this level.")]
        public int xpRequired;

        [Tooltip("Stat points awarded on reaching this level. Player allocates them manually.")]
        public int statPointsAwarded;

        [Header("Auto-Applied Bonuses")]
        [Tooltip("Flat HP bonus applied to PlayerHealth.maxHealth on level-up.")]
        public int hpBonus;

        [Tooltip("Flat mana bonus applied to PlayerMana.maxMana on level-up.")]
        public int manaBonus;

        [Tooltip("Milestone unlock description shown in System notification. Leave blank for no extra message.")]
        public string milestoneDescription;
    }

    [Tooltip("One entry per level, ordered level 1 → max. Must start at level 1.")]
    public LevelEntry[] levels;

    /// <summary>Returns the entry for a given level number, or null if not found.</summary>
    public LevelEntry? GetEntry(int level)
    {
        if (levels == null) return null;
        foreach (var entry in levels)
            if (entry.level == level) return entry;
        return null;
    }

    /// <summary>Returns the total XP required to reach the given level (0 if not found).</summary>
    public int XPForLevel(int level)
    {
        var entry = GetEntry(level);
        return entry.HasValue ? entry.Value.xpRequired : int.MaxValue;
    }

    /// <summary>Returns the maximum level defined in this table.</summary>
    public int MaxLevel => (levels != null && levels.Length > 0) ? levels[levels.Length - 1].level : 1;
}
