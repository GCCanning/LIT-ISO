using UnityEngine;

/// <summary>
/// Defines a dungeon instance: rank, floors, enemies, loot, and boss.
/// Create via Assets → Create → LIT-ISO → Dungeons → Dungeon Definition.
///
/// To add a new dungeon: create a DungeonDefinition.asset — no code changes needed.
/// </summary>
[CreateAssetMenu(fileName = "DungeonDefinition", menuName = "LIT-ISO/Dungeons/Dungeon Definition")]
public class DungeonDefinition : ScriptableObject
{
    [Header("Identity")]
    public string dungeonId;
    public string dungeonName;          // "Goblin Warrens", "Frozen Crypt"
    [TextArea(1, 2)] public string description;
    public Sprite entranceIcon;

    [Header("Rank & Scale")]
    public DungeonRank rank;

    public enum DungeonRank { F, E, D, C, B, A, S }

    [Tooltip("Recommended player level.")]
    [Min(1)] public int recommendedLevel = 1;
    [Tooltip("Number of floors (auto-set by rank if 0).")]
    [Min(0)] public int floorCountOverride = 0;
    public int FloorCount => floorCountOverride > 0 ? floorCountOverride : DefaultFloorCount();

    [Header("Theme")]
    public DungeonTheme theme;

    public enum DungeonTheme
    {
        Goblin, Undead, Elemental, Nature, Void, Mechanical,
        Bandit, Spider, Aquatic, Shadow, Dragon, Plague,
    }

    [Header("Instancing")]
    [Tooltip("Each entry spawns a private instance for the party.")]
    public bool isInstanced = true;
    [Tooltip("Real-time hours before the dungeon resets.")]
    [Min(0f)] public float respawnTimerHours = 24f;

    [Header("Enemies")]
    [Tooltip("Regular mob pool — randomly selected to populate floors.")]
    public EnemyDefinition[] mobs;

    [Tooltip("Mini-boss on floor 1 of E+ rank dungeons (optional).")]
    public EnemyDefinition miniBossDefinition;

    [Tooltip("Final boss definition.")]
    public EnemyDefinition bossDefinition;

    [Header("Boss HP Override (0 = use EnemyDefinition)")]
    [Min(0)] public int bossHPOverride = 0;

    [Header("Loot")]
    [Tooltip("Items that can drop from any floor enemy.")]
    public ItemDefinition[] floorDropPool;
    [Tooltip("Guaranteed boss drops (one randomly selected).")]
    public ItemDefinition[] uniqueBossDrops;
    [Tooltip("XP awarded on full clear (all floors complete).")]
    [Min(0)] public int clearXPReward = 100;
    [Tooltip("Gold (in copper) awarded on full clear.")]
    [Min(0)] public long clearCopperReward = 1000;

    [Header("First Clear")]
    [Tooltip("Title unlocked by the first player to clear this dungeon.")]
    public string firstClearTitleId;
    [Tooltip("If true, a world announcement fires on first clear.")]
    public bool announceFirstClear = true;

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    public int DefaultFloorCount() => rank switch
    {
        DungeonRank.F => 1,
        DungeonRank.E => 2,
        DungeonRank.D => 3,
        DungeonRank.C => 5,
        DungeonRank.B => 7,
        DungeonRank.A => 10,
        DungeonRank.S => 15,
        _ => 1,
    };

    public int RecommendedPartySize() => rank switch
    {
        DungeonRank.F => 1,
        DungeonRank.E => 2,
        DungeonRank.D => 3,
        _ => 4,
    };

    public Color RankColour() => rank switch
    {
        DungeonRank.F => new Color(0.7f, 0.7f, 0.7f),
        DungeonRank.E => new Color(0.4f, 0.9f, 0.4f),
        DungeonRank.D => new Color(0.3f, 0.6f, 1f),
        DungeonRank.C => new Color(0.7f, 0.3f, 1f),
        DungeonRank.B => new Color(1f, 0.6f, 0.1f),
        DungeonRank.A => new Color(1f, 0.2f, 0.2f),
        DungeonRank.S => new Color(1f, 0.85f, 0.1f),
        _ => Color.white,
    };
}
