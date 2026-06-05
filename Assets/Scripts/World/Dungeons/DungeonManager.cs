using System;
using System.Collections.Generic;
using UnityEngine;
using EthraClone.TrialWeek;

/// <summary>
/// Manages all dungeon instances — tracking discovered dungeons, active runs,
/// first-clear records, and loot distribution.
///
/// Add as a component on a persistent Managers GameObject.
/// Assign all DungeonDefinition assets to availableDungeons[].
///
/// Events:
///   OnDungeonDiscovered(DungeonDefinition)
///   OnDungeonEntered(DungeonDefinition)
///   OnFloorCleared(DungeonDefinition, int floorIndex)
///   OnDungeonCleared(DungeonDefinition)
///   OnFirstClear(DungeonDefinition, string playerId)
/// </summary>
public class DungeonManager : MonoBehaviour
{
    public static DungeonManager Instance { get; private set; }

    public static event Action<DungeonDefinition>          OnDungeonDiscovered;
    public static event Action<DungeonDefinition>          OnDungeonEntered;
    public static event Action<DungeonDefinition, int>     OnFloorCleared;
    public static event Action<DungeonDefinition>          OnDungeonCleared;
    public static event Action<DungeonDefinition, string>  OnFirstClear;

    // -------------------------------------------------------------------------
    // Runtime state
    // -------------------------------------------------------------------------

    private readonly HashSet<string>                discoveredIds = new();
    private readonly HashSet<string>                firstClearIds = new();
    private readonly List<DungeonEntrance>          entrances     = new();
    private readonly Dictionary<string, int>        clearCounts   = new();    // dungeonId → times cleared

    private DungeonDefinition activeDungeon;
    private int               activeFloor;

    // -------------------------------------------------------------------------
    // Inspector
    // -------------------------------------------------------------------------

    [Header("Dungeon Pool")]
    [Tooltip("All DungeonDefinition assets.")]
    public DungeonDefinition[] availableDungeons;

    [Header("Player Return")]
    [Tooltip("World position player returns to after exiting a dungeon.")]
    public Transform returnPoint;

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
    // Entrance registration (called by DungeonEntrance.Start())
    // -------------------------------------------------------------------------

    public void RegisterEntrance(DungeonEntrance entrance)
    {
        if (entrance == null || entrances.Contains(entrance)) return;
        entrances.Add(entrance);

        // Auto-discover on registration
        if (entrance.definition != null)
            DiscoverDungeon(entrance.definition);
    }

    public void UnregisterEntrance(DungeonEntrance entrance) => entrances.Remove(entrance);

    // -------------------------------------------------------------------------
    // Public API
    // -------------------------------------------------------------------------

    /// <summary>Discover a dungeon (show System message on first discovery).</summary>
    public void DiscoverDungeon(DungeonDefinition def)
    {
        if (def == null || discoveredIds.Contains(def.dungeonId)) return;

        discoveredIds.Add(def.dungeonId);
        SystemNotifier.Instance?.Announce(
            $"New Dungeon Discovered: {def.dungeonName} ({def.rank}-Rank)",
            SystemNotifier.MessageType.Info);
        OnDungeonDiscovered?.Invoke(def);
        QuestManager.Instance?.NotifyDungeonCleared(def.dungeonId);  // Trigger dungeon-type quest auto-start
        ActionTracker.Instance?.LogAction("local_player", "DungeonDiscovered", def.dungeonId, 20);
    }

    /// <summary>Enter a dungeon. Loads the dungeon scene/chunk and begins floor tracking.</summary>
    public void EnterDungeon(DungeonDefinition def, DungeonEntrance entrance)
    {
        if (def == null) return;

        // Level recommendation warning
        if (XPSystem.Instance != null && XPSystem.Instance.CurrentLevel < def.recommendedLevel)
            SystemNotifier.Instance?.Announce(
                $"Warning: This dungeon is recommended for Level {def.recommendedLevel}+.",
                SystemNotifier.MessageType.Warning);

        activeDungeon = def;
        activeFloor   = 0;

        SystemNotifier.Instance?.Announce(
            $"Entering {def.dungeonName} ({def.rank}-Rank) — Floor 1 of {def.FloorCount}",
            SystemNotifier.MessageType.Info);
        OnDungeonEntered?.Invoke(def);

        // NOTE: Actual scene/chunk loading is handled by your scene management system.
        // Hook in here: SceneManager.LoadScene(...) or IsoWorldChunkManager.LoadDungeonChunk(def)
        ActionTracker.Instance?.LogAction("local_player", "DungeonEntered", def.dungeonId, 10);
    }

    /// <summary>
    /// Signal that the current floor's enemies are all dead.
    /// Call this from your enemy spawn system when the kill count reaches 0.
    /// </summary>
    public void NotifyFloorCleared(int floorIndex)
    {
        if (activeDungeon == null) return;

        OnFloorCleared?.Invoke(activeDungeon, floorIndex);
        activeFloor = floorIndex + 1;

        if (activeFloor < activeDungeon.FloorCount)
        {
            SystemNotifier.Instance?.Announce(
                $"Floor {floorIndex + 1} Cleared — Advancing to Floor {activeFloor + 1}",
                SystemNotifier.MessageType.Info);
        }
        else
        {
            CompleteDungeon();
        }
    }

    /// <summary>Force-complete the active dungeon (use for boss kill callback).</summary>
    public void NotifyBossKilled(string dungeonId)
    {
        if (activeDungeon == null || activeDungeon.dungeonId != dungeonId) return;
        CompleteDungeon();
    }

    /// <summary>Called when the player exits a dungeon without completing it.</summary>
    public void ExitDungeon()
    {
        activeDungeon = null;
        activeFloor   = 0;
    }

    public bool IsInDungeon    => activeDungeon != null;
    public DungeonDefinition ActiveDungeon => activeDungeon;
    public int  ActiveFloor    => activeFloor;

    public int  GetClearCount(string dungeonId) => clearCounts.GetValueOrDefault(dungeonId, 0);
    public bool IsDiscovered(string dungeonId)  => discoveredIds.Contains(dungeonId);
    public bool IsFirstCleared(string dungeonId)=> firstClearIds.Contains(dungeonId);

    public IReadOnlyList<DungeonEntrance> AllEntrances => entrances;

    // -------------------------------------------------------------------------
    // Private helpers
    // -------------------------------------------------------------------------

    private void CompleteDungeon()
    {
        var def = activeDungeon;
        if (def == null) return;

        clearCounts[def.dungeonId] = clearCounts.GetValueOrDefault(def.dungeonId, 0) + 1;

        // XP & Gold rewards
        if (def.clearXPReward > 0 && XPSystem.Instance != null)
        {
            Vector3 pos = PlayerHealth.Instance != null ? PlayerHealth.Instance.transform.position : Vector3.zero;
            XPSystem.Instance.AwardXP(def.clearXPReward, pos);
        }
        if (def.clearCopperReward > 0 && CurrencySystem.Instance != null)
            CurrencySystem.Instance.AddCopper(def.clearCopperReward);

        // Unique boss drop
        GrantBossDrop(def);

        SystemNotifier.Instance?.Announce(
            $"Dungeon Cleared: {def.dungeonName}! ({def.rank}-Rank)",
            SystemNotifier.MessageType.Achievement);

        // First-clear handling
        if (!firstClearIds.Contains(def.dungeonId))
        {
            firstClearIds.Add(def.dungeonId);

            if (def.announceFirstClear)
                SystemNotifier.Instance?.AnnounceFirstClear("Player", def.dungeonName, def.rank.ToString());

            if (!string.IsNullOrEmpty(def.firstClearTitleId))
                TitleSystem.Instance?.UnlockTitle(def.firstClearTitleId);

            OnFirstClear?.Invoke(def, "local_player");
        }

        // Notify external systems
        TitleSystem.Instance?.RecordDungeonClear();
        QuestManager.Instance?.NotifyDungeonCleared(def.dungeonId);
        ActionTracker.Instance?.LogAction("local_player", "DungeonCleared", def.dungeonId, def.clearXPReward);

        OnDungeonCleared?.Invoke(def);

        activeDungeon = null;
        activeFloor   = 0;
    }

    private void GrantBossDrop(DungeonDefinition def)
    {
        if (def.uniqueBossDrops == null || def.uniqueBossDrops.Length == 0) return;

        var inv = FindFirstObjectByType<PlayerInventory>();
        if (inv == null) return;

        var drop = def.uniqueBossDrops[UnityEngine.Random.Range(0, def.uniqueBossDrops.Length)];
        if (drop == null) return;

        inv.Add(drop, 1);
        WorldFloatingText.Spawn(
            PlayerHealth.Instance != null ? PlayerHealth.Instance.transform.position + Vector3.up : Vector3.up,
            $"★ {drop.displayName}",
            new Color(1f, 0.85f, 0.1f),
            fontSize: 30);
    }
}
