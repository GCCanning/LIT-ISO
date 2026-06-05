using System;
using System.Collections.Generic;
using UnityEngine;
using EthraClone.TrialWeek;

/// <summary>
/// Manages player titles — unlocking them and applying their stat bonuses.
/// Titles are defined as TitleDefinition ScriptableObjects.
///
/// Add as a component on a persistent Managers GameObject.
/// Assign all TitleDefinition assets to allTitles[].
///
/// To add a new title: create a TitleDefinition.asset — no code changes needed.
/// </summary>
public class TitleSystem : MonoBehaviour
{
    public static TitleSystem Instance { get; private set; }

    public static event Action<TitleDefinition> OnTitleUnlocked;

    [Header("Title Pool")]
    [Tooltip("All TitleDefinition assets. Add new ones here.")]
    public TitleDefinition[] allTitles;

    private readonly HashSet<string>      unlockedIds  = new();
    private TitleDefinition               equippedTitle;

    // -------------------------------------------------------------------------
    // Singleton lifecycle
    // -------------------------------------------------------------------------

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;
    }

    private void OnDestroy() { if (Instance == this) Instance = null; }

    // -------------------------------------------------------------------------
    // Kill / collect tracking for auto-unlock
    // -------------------------------------------------------------------------

    private readonly Dictionary<string, int> killCounts    = new();
    private int totalKills = 0;
    private int totalDungeons = 0;
    private int totalBuildings = 0;

    public void RecordKill(string enemyId)
    {
        totalKills++;
        killCounts[enemyId] = killCounts.GetValueOrDefault(enemyId, 0) + 1;
        CheckAutoUnlocks();
    }

    public void RecordDungeonClear()
    {
        totalDungeons++;
        CheckAutoUnlocks();
    }

    public void RecordBuildingPlaced()
    {
        totalBuildings++;
        CheckAutoUnlocks();
    }

    // -------------------------------------------------------------------------
    // Public API
    // -------------------------------------------------------------------------

    public void UnlockTitle(string titleId)
    {
        if (unlockedIds.Contains(titleId)) return;

        var def = FindTitle(titleId);
        if (def == null) { Debug.LogWarning($"[TitleSystem] Title '{titleId}' not found."); return; }

        unlockedIds.Add(titleId);
        ApplyTitleBonus(def);
        SystemNotifier.Instance?.AnnounceTitle(def.titleName);
        OnTitleUnlocked?.Invoke(def);
        ActionTracker.Instance?.LogAction("local_player", "TitleEarned", titleId, 25);
    }

    public void EquipTitle(string titleId)
    {
        if (!unlockedIds.Contains(titleId)) return;
        equippedTitle = FindTitle(titleId);
    }

    public TitleDefinition EquippedTitle => equippedTitle;
    public IEnumerable<string> UnlockedTitleIds => unlockedIds;

    // -------------------------------------------------------------------------
    // Private helpers
    // -------------------------------------------------------------------------

    private void CheckAutoUnlocks()
    {
        if (allTitles == null) return;
        foreach (var t in allTitles)
        {
            if (t == null || unlockedIds.Contains(t.titleId)) continue;

            bool earned = t.unlockCondition switch
            {
                TitleDefinition.UnlockCondition.KillCount    => totalKills >= t.unlockThreshold,
                TitleDefinition.UnlockCondition.KillSpecific => killCounts.GetValueOrDefault(t.targetId, 0) >= t.unlockThreshold,
                TitleDefinition.UnlockCondition.DungeonClears => totalDungeons >= t.unlockThreshold,
                TitleDefinition.UnlockCondition.BuildingsPlaced => totalBuildings >= t.unlockThreshold,
                _ => false,
            };

            if (earned) UnlockTitle(t.titleId);
        }
    }

    private void ApplyTitleBonus(TitleDefinition def)
    {
        if (PlayerStats.Instance == null || def.bonusValue == 0) return;
        // Bonuses are additive flat values applied once on unlock
        PlayerStats.Instance.ApplyBonus(
            str: def.statType == TitleDefinition.StatBonus.STR ? def.bonusValue : 0,
            agi: def.statType == TitleDefinition.StatBonus.AGI ? def.bonusValue : 0,
            vit: def.statType == TitleDefinition.StatBonus.VIT ? def.bonusValue : 0,
            INT_bonus: def.statType == TitleDefinition.StatBonus.INT ? def.bonusValue : 0,
            wis: def.statType == TitleDefinition.StatBonus.WIS ? def.bonusValue : 0,
            end: def.statType == TitleDefinition.StatBonus.END ? def.bonusValue : 0
        );
    }

    private TitleDefinition FindTitle(string titleId)
    {
        if (allTitles == null) return null;
        foreach (var t in allTitles)
            if (t != null && t.titleId == titleId) return t;
        return null;
    }
}
