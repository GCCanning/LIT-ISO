using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Drives the scripted tutorial sequence for new players.
/// Tracks objectives (kill, gather, rest) across 3 in-game days, then
/// triggers Class Assignment at the end.
///
/// Add as a component on the StarterZone root object (or a persistent Managers object).
/// Assign a reference to StarterZoneConfig and the world's TrialWeekManager.
///
/// Flow:
///   Day 1 → Kill 3 slimes + gather 10 wood
///   Day 2 → Clear the starter dungeon (Goblin Den)
///   Day 3 → Class Trial: kill 5 enemies in your dominant combat style
///   End   → ClassSystem.AssignClass() → portal to main world unlocks
///
/// To extend with new objectives: add an ObjectiveType and handle it in NotifyXxx().
/// No structural changes required.
/// </summary>
public class TutorialSequence : MonoBehaviour
{
    public static TutorialSequence Instance { get; private set; }

    // -------------------------------------------------------------------------
    // Events
    // -------------------------------------------------------------------------

    public static event Action<TutorialObjective> OnObjectiveUpdated;
    public static event Action<int>               OnDayAdvanced;       // in-tutorial day
    public static event Action                    OnTutorialComplete;

    // -------------------------------------------------------------------------
    // Tutorial objective definition
    // -------------------------------------------------------------------------

    [System.Serializable]
    public class TutorialObjective
    {
        public string id;
        public string description;
        public int    required;
        public int    current;
        public bool   IsComplete => current >= required;

        public TutorialObjective(string id, string description, int required)
        {
            this.id          = id;
            this.description = description;
            this.required    = required;
            this.current     = 0;
        }

        public void Progress(int amount = 1)
        {
            current = Mathf.Min(required, current + amount);
        }
    }

    // -------------------------------------------------------------------------
    // Inspector
    // -------------------------------------------------------------------------

    [Header("Settings")]
    [Tooltip("Seconds per in-tutorial day (independent of world day length).")]
    public float dayDurationSeconds = 180f;

    [Tooltip("GameObject that is activated when the portal to the main world opens.")]
    public GameObject worldPortal;

    // -------------------------------------------------------------------------
    // State
    // -------------------------------------------------------------------------

    public int   TutorialDay   { get; private set; } = 1;
    public bool  IsRunning     { get; private set; } = false;
    public bool  IsComplete    { get; private set; } = false;

    private readonly List<TutorialObjective> activeObjectives = new();
    private float dayTimer = 0f;

    // Day 1 objectives
    private TutorialObjective objKillSlimes;
    private TutorialObjective objGatherWood;

    // Day 2 objectives
    private TutorialObjective objClearDungeon;

    // Day 3 objectives (class trial)
    private TutorialObjective objClassTrialKills;

    // -------------------------------------------------------------------------
    // Singleton lifecycle
    // -------------------------------------------------------------------------

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // -------------------------------------------------------------------------
    // Public API
    // -------------------------------------------------------------------------

    /// <summary>Begin the tutorial. Called by StarterZoneGenerator after the zone is built.</summary>
    public void StartTutorial()
    {
        if (IsRunning) return;

        IsRunning  = true;
        TutorialDay = 1;
        dayTimer   = 0f;

        BeginDay1();
        StartCoroutine(DayTimerRoutine());
    }

    // --- Notify methods called by game systems ---

    /// <summary>Call when the player kills any enemy during the tutorial.</summary>
    public void NotifyKill(string enemyId)
    {
        if (!IsRunning || IsComplete) return;

        if (TutorialDay == 1 && objKillSlimes != null && !objKillSlimes.IsComplete)
        {
            objKillSlimes.Progress();
            OnObjectiveUpdated?.Invoke(objKillSlimes);
            CheckDay1Complete();
        }
        else if (TutorialDay == 3 && objClassTrialKills != null && !objClassTrialKills.IsComplete)
        {
            objClassTrialKills.Progress();

            // Feed ClassSystem trial counters
            ClassSystem.Instance?.RecordKill();

            OnObjectiveUpdated?.Invoke(objClassTrialKills);
            CheckDay3Complete();
        }
    }

    /// <summary>Call when the player harvests resources during the tutorial.</summary>
    public void NotifyGather(string itemId, int amount)
    {
        if (!IsRunning || IsComplete) return;

        if (TutorialDay == 1 && objGatherWood != null && !objGatherWood.IsComplete
                              && itemId.ToLower().Contains("wood"))
        {
            objGatherWood.Progress(amount);
            ClassSystem.Instance?.RecordResourceGathered();
            OnObjectiveUpdated?.Invoke(objGatherWood);
            CheckDay1Complete();
        }
    }

    /// <summary>Call when the player clears the starter dungeon boss.</summary>
    public void NotifyDungeonCleared(string dungeonId)
    {
        if (!IsRunning || IsComplete) return;

        if (TutorialDay == 2 && objClearDungeon != null && !objClearDungeon.IsComplete)
        {
            objClearDungeon.Progress();
            OnObjectiveUpdated?.Invoke(objClearDungeon);
            CheckDay2Complete();
        }
    }

    /// <summary>Call when the player uses a spell/skill during the tutorial.</summary>
    public void NotifySpellUsed()
    {
        ClassSystem.Instance?.RecordSpellUsed();
    }

    /// <summary>Call when the player performs a melee attack during the tutorial.</summary>
    public void NotifyMeleeHit(int damage)
    {
        ClassSystem.Instance?.RecordMeleeDamage(damage);
    }

    /// <summary>Call when the player dashes during the tutorial.</summary>
    public void NotifyDash()
    {
        ClassSystem.Instance?.RecordDash();
    }

    // -------------------------------------------------------------------------
    // Day progression
    // -------------------------------------------------------------------------

    private IEnumerator DayTimerRoutine()
    {
        while (IsRunning && !IsComplete)
        {
            dayTimer += Time.deltaTime;
            if (dayTimer >= dayDurationSeconds)
            {
                dayTimer = 0f;
                AdvanceDay();
            }
            yield return null;
        }
    }

    private void AdvanceDay()
    {
        TutorialDay++;
        OnDayAdvanced?.Invoke(TutorialDay);

        SystemNotifier.Instance?.Announce(
            $"Tutorial — Day {TutorialDay} of 3.",
            SystemNotifier.MessageType.Info);

        activeObjectives.Clear();

        switch (TutorialDay)
        {
            case 2: BeginDay2(); break;
            case 3: BeginDay3(); break;
            default:
                if (TutorialDay > 3) CompleteTutorial();
                break;
        }
    }

    // -------------------------------------------------------------------------
    // Per-day setup
    // -------------------------------------------------------------------------

    private void BeginDay1()
    {
        objKillSlimes = new TutorialObjective("kill_slimes", "Kill 3 Slimes", 3);
        objGatherWood = new TutorialObjective("gather_wood",  "Gather 10 Wood",  10);

        activeObjectives.Add(objKillSlimes);
        activeObjectives.Add(objGatherWood);

        SystemNotifier.Instance?.Announce(
            "Welcome to LIT-ISO. Survive 3 days to earn your Class.",
            SystemNotifier.MessageType.Info);

        SystemNotifier.Instance?.Announce(
            "Day 1 — Kill 3 Slimes and Gather 10 Wood.",
            SystemNotifier.MessageType.Info);
    }

    private void BeginDay2()
    {
        objClearDungeon = new TutorialObjective("clear_dungeon", "Clear the Goblin Den", 1);
        activeObjectives.Add(objClearDungeon);

        SystemNotifier.Instance?.Announce(
            "Day 2 — A Goblin Den has appeared to the north. Clear it.",
            SystemNotifier.MessageType.Info);
    }

    private void BeginDay3()
    {
        objClassTrialKills = new TutorialObjective("class_trial", "Class Trial: Kill 5 enemies", 5);
        activeObjectives.Add(objClassTrialKills);

        SystemNotifier.Instance?.Announce(
            "Day 3 — Class Trial. Kill 5 enemies. The System is watching.",
            SystemNotifier.MessageType.Info);
    }

    // -------------------------------------------------------------------------
    // Completion checks
    // -------------------------------------------------------------------------

    private void CheckDay1Complete()
    {
        if (objKillSlimes.IsComplete && objGatherWood.IsComplete)
            SystemNotifier.Instance?.Announce("Day 1 objectives complete! Rest and prepare for tomorrow.", SystemNotifier.MessageType.Info);
    }

    private void CheckDay2Complete()
    {
        if (objClearDungeon.IsComplete)
            SystemNotifier.Instance?.Announce("Goblin Den cleared! Return to camp for the night.", SystemNotifier.MessageType.DungeonClear);
    }

    private void CheckDay3Complete()
    {
        if (objClassTrialKills.IsComplete)
            CompleteTutorial();
    }

    private void CompleteTutorial()
    {
        if (IsComplete) return;
        IsComplete = true;
        IsRunning  = false;

        StartCoroutine(ClassAssignmentCeremony());
    }

    private IEnumerator ClassAssignmentCeremony()
    {
        SystemNotifier.Instance?.Announce(
            "The System has observed your trial. Calculating your Class...",
            SystemNotifier.MessageType.Info);

        yield return new WaitForSeconds(2.5f);

        // Assign class
        ClassSystem.Instance?.AssignClass();

        yield return new WaitForSeconds(1.5f);

        SystemNotifier.Instance?.Announce(
            "The path to LIT-ISO is open. Your journey begins.",
            SystemNotifier.MessageType.Info);

        // Open world portal
        if (worldPortal != null)
            worldPortal.SetActive(true);

        OnTutorialComplete?.Invoke();
    }

    // -------------------------------------------------------------------------
    // Public read helpers
    // -------------------------------------------------------------------------

    public IReadOnlyList<TutorialObjective> ActiveObjectives => activeObjectives;
}
