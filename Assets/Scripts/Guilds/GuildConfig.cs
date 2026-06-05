using UnityEngine;

/// <summary>
/// ScriptableObject configuration for the Guild system.
/// Create one via Assets → Create → LIT-ISO → Guilds → Guild Config
/// and assign it to GuildManager in the inspector.
/// </summary>
[CreateAssetMenu(fileName = "GuildConfig", menuName = "LIT-ISO/Guilds/Guild Config")]
public class GuildConfig : ScriptableObject
{
    [Header("Formation Requirements")]
    [Tooltip("Minimum player level to found a guild.")]
    [Min(1)] public int minimumLevelToFound = 10;
    [Tooltip("Gold cost to register a guild.")]
    [Min(0)] public int foundingGoldCost = 500;
    [Tooltip("If true, a guild can be founded solo (for single-player games).")]
    public bool allowSoloGuild = true;

    [Header("Tier Thresholds (Guild Points)")]
    [Tooltip("GP needed for Bronze, Silver, Gold, Platinum, Diamond tiers.")]
    public int[] tierGPThresholds = { 0, 1000, 5000, 20000, 100000 };

    [Tooltip("Display names for each tier (index matches threshold above).")]
    public string[] tierNames = { "Bronze", "Silver", "Gold", "Platinum", "Diamond" };

    [Header("Member Caps per Tier")]
    public int[] memberCaps = { 20, 50, 100, 200, 500 };

    [Header("GP Awards")]
    public int gpPerDungeonClear  = 50;
    public int gpPerQuestComplete = 20;
    public int gpPerBossKill      = 100;
    public int gpPerBuildingBuilt = 10;

    [Header("Rank Names (index 0 = Recruit, 5 = Guild Master)")]
    public string[] rankNames =
    {
        "Recruit", "Member", "Veteran", "Officer", "Vice Master", "Guild Master"
    };
}
