using UnityEngine;

/// <summary>
/// Defines a building type that can be placed in a settlement.
/// Create instances via Assets → Create → LIT-ISO → Towns → Building Definition.
///
/// To add a new building: create a SettlementDefinition.asset — no code changes needed.
/// </summary>
[CreateAssetMenu(fileName = "BuildingDefinition", menuName = "LIT-ISO/Towns/Building Definition")]
public class SettlementDefinition : ScriptableObject
{
    [Header("Identity")]
    public string buildingId;
    public string displayName;
    [TextArea(1, 3)] public string description;
    public Sprite icon;
    public GameObject worldPrefab;     // Placed in world when built

    [Header("Requirements")]
    [Tooltip("Minimum settlement tier required to place this building.")]
    [Min(0)] public int requiredTier = 0;
    [Tooltip("Player level required to unlock this building.")]
    [Min(1)] public int requiredPlayerLevel = 1;

    [Header("Build Cost")]
    public ItemCost[] buildCost;

    [System.Serializable]
    public struct ItemCost
    {
        public ItemDefinition item;
        [Min(1)] public int amount;
    }

    [Header("Construction Time")]
    [Tooltip("Seconds to build (0 = instant).")]
    [Min(0f)] public float buildTimeSeconds = 10f;

    [Header("Production (optional)")]
    [Tooltip("Item this building generates over time (null = no production).")]
    public ItemDefinition producesItem;
    [Tooltip("Amount produced per cycle.")]
    [Min(0)] public int produceAmount = 1;
    [Tooltip("Seconds per production cycle.")]
    [Min(10f)] public float productionCycleSeconds = 60f;

    [Header("Settlement Bonuses")]
    [Tooltip("Population capacity contributed to the settlement.")]
    [Min(0)] public int populationBonus = 0;
    [Tooltip("XP multiplier bonus for all players in the settlement.")]
    [Range(0f, 0.5f)] public float xpBonusPercent = 0f;

    [Header("Category")]
    public BuildingCategory category;
    public enum BuildingCategory
    {
        Production,   // Lumber Mill, Quarry, Farm, Mine
        Service,      // Inn, Market, Blacksmith, Alchemist
        Defense,      // Wall, Tower, Ballista
        Special,      // Mage Tower, Auction House, Guild Hall, Temple
        Housing,      // Adds population capacity
    }
}
