using UnityEngine;

/// <summary>
/// Defines a player title — how it's earned and what stat bonus it grants.
/// Create instances via Assets → Create → LIT-ISO → Titles → Title Definition.
/// </summary>
[CreateAssetMenu(fileName = "TitleDefinition", menuName = "LIT-ISO/Titles/Title Definition")]
public class TitleDefinition : ScriptableObject
{
    [Header("Identity")]
    public string titleId;
    public string titleName;          // e.g. "Slime Slayer"
    [TextArea(1, 2)] public string description;

    public enum UnlockCondition
    {
        Manual,           // Only via QuestManager.GrantReward or direct call
        KillCount,        // Total kills >= threshold
        KillSpecific,     // Kills of targetId >= threshold
        DungeonClears,    // Dungeons cleared >= threshold
        BuildingsPlaced,  // Buildings placed >= threshold
        LevelReached,     // Player level >= threshold
    }

    [Header("Unlock Condition")]
    public UnlockCondition unlockCondition = UnlockCondition.Manual;
    [Tooltip("For Kill/Dungeon/Building conditions: the number required.")]
    [Min(1)] public int unlockThreshold = 1;
    [Tooltip("For KillSpecific: the enemyId to count.")]
    public string targetId;

    public enum StatBonus { None, STR, AGI, VIT, INT, WIS, END }

    [Header("Stat Bonus (applied once on unlock)")]
    public StatBonus statType = StatBonus.None;
    [Min(0)] public int bonusValue = 0;

    [Tooltip("Percentage XP bonus while this title is equipped (0.05 = +5%).")]
    [Range(0f, 0.5f)] public float xpBonusPercent = 0f;
}
