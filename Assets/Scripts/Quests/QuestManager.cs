using System;
using System.Collections.Generic;
using UnityEngine;
using EthraClone.TrialWeek;

/// <summary>
/// Tracks active quests and their objective progress.
/// Receives notifications from game systems (kills, pickups, dungeon clears, etc.)
/// and checks completion automatically.
///
/// Add as a component on a persistent Managers GameObject.
/// Assign all available QuestDefinition assets to availableQuests[].
///
/// Events:
///   OnQuestStarted(QuestDefinition)
///   OnObjectiveProgress(QuestDefinition, int objectiveIndex, int current, int required)
///   OnQuestCompleted(QuestDefinition)
/// </summary>
public class QuestManager : MonoBehaviour
{
    public static QuestManager Instance { get; private set; }

    public static event Action<QuestDefinition>             OnQuestStarted;
    public static event Action<QuestDefinition, int, int, int> OnObjectiveProgress; // (quest, objIdx, current, required)
    public static event Action<QuestDefinition>             OnQuestCompleted;

    // -------------------------------------------------------------------------
    // Inspector
    // -------------------------------------------------------------------------

    [Header("Quest Pool")]
    [Tooltip("All QuestDefinition assets. Auto-started quests (Dungeon/WorldEvent) come from this pool.")]
    public QuestDefinition[] availableQuests;

    // -------------------------------------------------------------------------
    // Runtime state
    // -------------------------------------------------------------------------

    // objectiveProgress[questId][objectiveIndex] = current count
    private readonly Dictionary<string, int[]>      progress      = new();
    private readonly Dictionary<string, QuestDefinition> activeQuests = new();
    private readonly HashSet<string>                completedIds  = new();

    // -------------------------------------------------------------------------
    // Singleton lifecycle
    // -------------------------------------------------------------------------

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Start()
    {
        // Wire up game events
        XPSystem.OnLevelUp += HandleLevelUp;
    }

    // -------------------------------------------------------------------------
    // Public API — Quest lifecycle
    // -------------------------------------------------------------------------

    /// <summary>Start a quest. Does nothing if already active or completed.</summary>
    public bool StartQuest(string questId)
    {
        if (activeQuests.ContainsKey(questId) || completedIds.Contains(questId)) return false;

        var def = FindQuestById(questId);
        if (def == null) { Debug.LogWarning($"[QuestManager] Quest '{questId}' not found."); return false; }

        // Prerequisites
        foreach (var prereq in def.prerequisiteQuestIds)
            if (!completedIds.Contains(prereq)) return false;

        // Level check
        if (XPSystem.Instance != null && XPSystem.Instance.CurrentLevel < def.minimumLevel) return false;

        activeQuests[questId]  = def;
        progress[questId]      = new int[def.objectives.Length];

        SystemNotifier.Instance?.Announce($"New Quest: {def.title}", SystemNotifier.MessageType.Info);
        OnQuestStarted?.Invoke(def);
        ActionTracker.Instance?.LogAction("local_player", "QuestStarted", questId, 10);

        Debug.Log($"[QuestManager] Quest started: {def.title}");
        return true;
    }

    public void StartQuest(QuestDefinition def)
    {
        if (def != null) StartQuest(def.questId);
    }

    public bool IsActive(string questId)    => activeQuests.ContainsKey(questId);
    public bool IsCompleted(string questId) => completedIds.Contains(questId);

    // -------------------------------------------------------------------------
    // Notify methods — called by game systems
    // -------------------------------------------------------------------------

    /// <summary>Call when any enemy is killed (from SlimeEnemyController.Die).</summary>
    public void NotifyKill(string enemyId)
    {
        foreach (var (questId, def) in activeQuests)
        {
            for (int i = 0; i < def.objectives.Length; i++)
            {
                var obj = def.objectives[i];
                if (obj.type == QuestDefinition.QuestObjective.ObjectiveType.KillEnemy
                    && obj.targetId == enemyId)
                {
                    IncrementObjective(questId, i, def);
                }
                else if (obj.type == QuestDefinition.QuestObjective.ObjectiveType.KillAny)
                {
                    IncrementObjective(questId, i, def);
                }
            }
        }
    }

    /// <summary>Call when the player collects an item (from PlayerInventory.Add).</summary>
    public void NotifyCollect(string itemId, int amount)
    {
        foreach (var (questId, def) in activeQuests)
        {
            for (int i = 0; i < def.objectives.Length; i++)
            {
                var obj = def.objectives[i];
                if (obj.type == QuestDefinition.QuestObjective.ObjectiveType.CollectItem
                    && obj.targetId == itemId)
                {
                    IncrementObjective(questId, i, def, amount);
                }
            }
        }
    }

    /// <summary>Call when a dungeon is cleared (from DungeonManager).</summary>
    public void NotifyDungeonCleared(string dungeonId)
    {
        // Auto-start dungeon quests on first clear
        foreach (var def in availableQuests)
        {
            if (def == null) continue;
            if (def.category == QuestDefinition.QuestCategory.Dungeon
                && !IsActive(def.questId) && !IsCompleted(def.questId))
            {
                foreach (var obj in def.objectives)
                    if (obj.type == QuestDefinition.QuestObjective.ObjectiveType.ClearDungeon
                        && obj.targetId == dungeonId)
                        StartQuest(def.questId);
            }
        }

        foreach (var (questId, def) in activeQuests)
        {
            for (int i = 0; i < def.objectives.Length; i++)
            {
                var obj = def.objectives[i];
                if (obj.type == QuestDefinition.QuestObjective.ObjectiveType.ClearDungeon
                    && (obj.targetId == dungeonId || string.IsNullOrEmpty(obj.targetId)))
                    IncrementObjective(questId, i, def);
            }
        }
    }

    /// <summary>Call when the player enters a biome (from WeatherManager/ChunkManager).</summary>
    public void NotifyBiomeReached(string biomeId)
    {
        foreach (var (questId, def) in activeQuests)
        {
            for (int i = 0; i < def.objectives.Length; i++)
            {
                var obj = def.objectives[i];
                if (obj.type == QuestDefinition.QuestObjective.ObjectiveType.ReachBiome
                    && obj.targetId == biomeId)
                    IncrementObjective(questId, i, def);
            }
        }
    }

    /// <summary>Call when the player survives an in-game day (from TrialWeekManager).</summary>
    public void NotifySurvivedDay(int totalDays)
    {
        foreach (var (questId, def) in activeQuests)
        {
            for (int i = 0; i < def.objectives.Length; i++)
            {
                var obj = def.objectives[i];
                if (obj.type == QuestDefinition.QuestObjective.ObjectiveType.SurviveDays)
                {
                    int needed = obj.requiredCount;
                    int cur    = progress[questId][i];
                    if (cur < needed)
                        IncrementObjective(questId, i, def);
                }
            }
        }
    }

    // -------------------------------------------------------------------------
    // Private helpers
    // -------------------------------------------------------------------------

    private void IncrementObjective(string questId, int objIndex, QuestDefinition def, int amount = 1)
    {
        if (!progress.TryGetValue(questId, out var prog)) return;

        int required = def.objectives[objIndex].requiredCount;
        if (prog[objIndex] >= required) return;

        prog[objIndex] = Mathf.Min(required, prog[objIndex] + amount);
        OnObjectiveProgress?.Invoke(def, objIndex, prog[objIndex], required);

        CheckCompletion(questId, def, prog);
    }

    private void CheckCompletion(string questId, QuestDefinition def, int[] prog)
    {
        for (int i = 0; i < def.objectives.Length; i++)
            if (prog[i] < def.objectives[i].requiredCount) return;

        // All objectives done
        GrantRewards(def);
        activeQuests.Remove(questId);
        completedIds.Add(questId);
        OnQuestCompleted?.Invoke(def);

        SystemNotifier.Instance?.Announce(
            $"Quest Complete: {def.title}!", SystemNotifier.MessageType.Achievement);
        ActionTracker.Instance?.LogAction("local_player", "QuestCompleted", questId, def.xpReward);
    }

    private void GrantRewards(QuestDefinition def)
    {
        if (def.xpReward > 0 && XPSystem.Instance != null)
        {
            Vector3 pos = PlayerHealth.Instance != null ? PlayerHealth.Instance.transform.position : Vector3.zero;
            XPSystem.Instance.AwardXP(def.xpReward, pos);
        }

        if (def.itemRewards != null)
        {
            var inv = FindFirstObjectByType<PlayerInventory>();
            if (inv != null)
                foreach (var item in def.itemRewards)
                    if (item != null) inv.Add(item, 1);
        }

        if (!string.IsNullOrEmpty(def.titleIdReward))
            TitleSystem.Instance?.UnlockTitle(def.titleIdReward);
    }

    private void HandleLevelUp(int newLevel)
    {
        foreach (var (questId, def) in activeQuests)
        {
            for (int i = 0; i < def.objectives.Length; i++)
            {
                var obj = def.objectives[i];
                if (obj.type == QuestDefinition.QuestObjective.ObjectiveType.ReachLevel
                    && newLevel >= obj.requiredCount)
                    IncrementObjective(questId, i, def, obj.requiredCount);
            }
        }
    }

    private QuestDefinition FindQuestById(string questId)
    {
        if (availableQuests == null) return null;
        foreach (var q in availableQuests)
            if (q != null && q.questId == questId) return q;
        return null;
    }
}
