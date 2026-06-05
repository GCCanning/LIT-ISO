using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using EthraClone.TrialWeek;

/// <summary>
/// Manages all settlements in the world.
/// Handles founding, tier upgrades, building placement, production, and invasion events.
///
/// Add as a component on a persistent Managers GameObject.
/// Assign all SettlementDefinition assets to availableBuildings[].
///
/// Events:
///   OnSettlementFounded(Settlement)
///   OnBuildingPlaced(Settlement, SettlementDefinition)
///   OnSettlementTierUp(Settlement, int newTier)
/// </summary>
public class TownManager : MonoBehaviour
{
    public static TownManager Instance { get; private set; }

    public static event Action<Settlement>                       OnSettlementFounded;
    public static event Action<Settlement, SettlementDefinition> OnBuildingPlaced;
    public static event Action<Settlement, int>                  OnSettlementTierUp;

    // -------------------------------------------------------------------------
    // Data model
    // -------------------------------------------------------------------------

    [System.Serializable]
    public class Settlement
    {
        public string id;
        public string name;
        public int    tier;           // 0=Campsite → 5=Capital
        public Vector3 worldPosition;
        public int    population;

        public readonly List<string> builtBuildingIds = new();
        public readonly List<BuildingInstance> instances = new();

        public bool HasBuilding(string buildingId) => builtBuildingIds.Contains(buildingId);

        public static readonly string[] TierNames = { "Campsite", "Hamlet", "Village", "Town", "City", "Capital" };
        public static readonly int[]    PopCaps   = { 4, 20, 50, 150, 500, int.MaxValue };
        public string TierName => tier < TierNames.Length ? TierNames[tier] : "Capital";
        public int    PopCap   => tier < PopCaps.Length ? PopCaps[tier] : int.MaxValue;
    }

    // -------------------------------------------------------------------------
    // Inspector
    // -------------------------------------------------------------------------

    [Header("Building Pool")]
    public SettlementDefinition[] availableBuildings;

    [Header("Invasion")]
    [Tooltip("In-game days between invasion events.")]
    [Min(1)] public int daysBetweenInvasions = 7;

    // -------------------------------------------------------------------------
    // Runtime state
    // -------------------------------------------------------------------------

    private readonly List<Settlement> settlements = new();

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

    private void Start()
    {
        var twm = FindFirstObjectByType<EthraClone.TrialWeek.TrialWeekManager>();
        if (twm != null)
            twm.OnPlayerDayChanged += (_, day) => CheckInvasion(day);
    }

    // -------------------------------------------------------------------------
    // Public API
    // -------------------------------------------------------------------------

    /// <summary>Found a new settlement at a world position.</summary>
    public Settlement FoundSettlement(string name, Vector3 position)
    {
        var s = new Settlement
        {
            id            = $"settlement_{settlements.Count}",
            name          = name,
            tier          = 0,
            worldPosition = position,
            population    = 1,
        };
        settlements.Add(s);

        SystemNotifier.Instance?.Announce($"Settlement Founded: {name} (Campsite)", SystemNotifier.MessageType.Info);
        OnSettlementFounded?.Invoke(s);
        TitleSystem.Instance?.RecordBuildingPlaced();
        ActionTracker.Instance?.LogAction("local_player", "SettlementFounded", name, 100);

        return s;
    }

    /// <summary>
    /// Attempt to place a building in a settlement.
    /// Returns false if requirements aren't met or materials are missing.
    /// </summary>
    public bool PlaceBuilding(Settlement settlement, string buildingId, PlayerInventory inventory)
    {
        if (settlement == null || settlement.HasBuilding(buildingId)) return false;

        var def = FindBuilding(buildingId);
        if (def == null) return false;

        // Tier check
        if (settlement.tier < def.requiredTier)
        {
            SystemNotifier.Instance?.Announce(
                $"Requires {Settlement.TierNames[Mathf.Min(def.requiredTier, 5)]} tier settlement.",
                SystemNotifier.MessageType.Warning);
            return false;
        }

        // Level check
        if (XPSystem.Instance != null && XPSystem.Instance.CurrentLevel < def.requiredPlayerLevel)
        {
            SystemNotifier.Instance?.Announce(
                $"Requires Level {def.requiredPlayerLevel}.", SystemNotifier.MessageType.Warning);
            return false;
        }

        // Material check
        if (!HasMaterials(def, inventory)) return false;

        // Consume materials
        ConsumeMaterials(def, inventory);

        // Add building
        settlement.builtBuildingIds.Add(buildingId);
        settlement.population += def.populationBonus;

        // Spawn world prefab
        if (def.worldPrefab != null)
        {
            var go   = UnityEngine.Object.Instantiate(def.worldPrefab, settlement.worldPosition, Quaternion.identity);
            var inst = go.GetComponent<BuildingInstance>();
            if (inst == null) inst = go.AddComponent<BuildingInstance>();
            inst.definition = def;
            inst.settlement = settlement;
            settlement.instances.Add(inst);

            if (def.buildTimeSeconds > 0f)
                inst.StartConstruction();
        }

        // Start production
        if (def.producesItem != null)
            StartCoroutine(ProductionRoutine(settlement, def, inventory));

        SystemNotifier.Instance?.Announce($"{def.displayName} built in {settlement.name}!", SystemNotifier.MessageType.Info);
        OnBuildingPlaced?.Invoke(settlement, def);
        TitleSystem.Instance?.RecordBuildingPlaced();
        ActionTracker.Instance?.LogAction("local_player", "BuildingPlaced", buildingId, 30);

        // Check tier upgrade
        CheckTierUpgrade(settlement);
        return true;
    }

    public IReadOnlyList<Settlement> AllSettlements => settlements;

    // -------------------------------------------------------------------------
    // Private helpers
    // -------------------------------------------------------------------------

    private void CheckTierUpgrade(Settlement s)
    {
        int newTier = CalculateTier(s);
        if (newTier <= s.tier) return;

        s.tier = newTier;
        SystemNotifier.Instance?.Announce(
            $"{s.name} has grown into a {s.TierName}!", SystemNotifier.MessageType.WorldEvent);
        OnSettlementTierUp?.Invoke(s, newTier);
    }

    private int CalculateTier(Settlement s)
    {
        // Tier thresholds based on population capacity and key buildings
        if (s.population >= Settlement.PopCaps[4] && s.HasBuilding("auction_house")) return 5;
        if (s.population >= Settlement.PopCaps[3] && s.HasBuilding("mage_tower"))    return 4;
        if (s.population >= Settlement.PopCaps[2] && s.HasBuilding("guild_hall"))    return 3;
        if (s.population >= Settlement.PopCaps[1] && s.HasBuilding("firepit"))       return 2;
        if (s.population >= 2)                                                        return 1;
        return 0;
    }

    private bool HasMaterials(SettlementDefinition def, PlayerInventory inv)
    {
        if (def.buildCost == null || inv == null) return true;
        foreach (var cost in def.buildCost)
            if (cost.item != null && inv.GetCount(cost.item.itemId) < cost.amount)
            {
                SystemNotifier.Instance?.Announce(
                    $"Need {cost.amount}× {cost.item.displayName}.", SystemNotifier.MessageType.Warning);
                return false;
            }
        return true;
    }

    private void ConsumeMaterials(SettlementDefinition def, PlayerInventory inv)
    {
        if (def.buildCost == null || inv == null) return;
        foreach (var cost in def.buildCost)
            if (cost.item != null) inv.Remove(cost.item.itemId, cost.amount);
    }

    private IEnumerator ProductionRoutine(Settlement settlement, SettlementDefinition def, PlayerInventory inv)
    {
        while (settlement.HasBuilding(def.buildingId))
        {
            yield return new WaitForSeconds(def.productionCycleSeconds);
            if (inv != null && def.producesItem != null)
            {
                inv.Add(def.producesItem, def.produceAmount);
                WorldFloatingText.Spawn(settlement.worldPosition + Vector3.up,
                    $"+{def.produceAmount} {def.producesItem.displayName}",
                    new Color(0.9f, 0.8f, 0.3f));
            }
        }
    }

    private void CheckInvasion(int day)
    {
        if (day % daysBetweenInvasions != 0) return;
        foreach (var s in settlements)
            if (s.tier >= 1)
                SystemNotifier.Instance?.Announce(
                    $"Goblin raiders are attacking {s.name}! Defend your settlement!",
                    SystemNotifier.MessageType.WorldEvent);
    }

    private SettlementDefinition FindBuilding(string id)
    {
        if (availableBuildings == null) return null;
        foreach (var b in availableBuildings)
            if (b != null && b.buildingId == id) return b;
        return null;
    }
}
