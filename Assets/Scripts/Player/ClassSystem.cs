using System;
using System.Collections.Generic;
using UnityEngine;
using EthraClone.TrialWeek;

/// <summary>
/// Manages the player's class — assignment at the end of the tutorial,
/// and evolution at Level 25.
///
/// The System watches combat behaviour during the tutorial (tracked via
/// ClassTrialTracker) and selects the best-matching class when AssignClass() is called.
///
/// Add as a component on the Player GameObject (or Managers object).
/// Assign all available ClassDefinition assets to the classList array.
///
/// Events:
///   OnClassAssigned(ClassDefinition)   — fired once when the class is first assigned.
///   OnClassEvolved(ClassDefinition)    — fired when the player evolves their class at Level 25.
/// </summary>
public class ClassSystem : MonoBehaviour
{
    public static ClassSystem Instance { get; private set; }

    public static event Action<ClassDefinition> OnClassAssigned;
    public static event Action<ClassDefinition> OnClassEvolved;

    // -------------------------------------------------------------------------
    // Inspector
    // -------------------------------------------------------------------------

    [Header("All Available Classes")]
    [Tooltip("Add every ClassDefinition.asset here. The system picks from this pool.")]
    public ClassDefinition[] classList;

    [Header("Rarity Weights (must sum to 100)")]
    [Tooltip("Chance of getting Common.")]
    [Range(0, 100)] public int commonWeight    = 63;
    [Tooltip("Chance of getting Uncommon.")]
    [Range(0, 100)] public int uncommonWeight  = 22;
    [Tooltip("Chance of getting Rare.")]
    [Range(0, 100)] public int rareWeight      = 10;
    [Tooltip("Chance of getting Epic.")]
    [Range(0, 100)] public int epicWeight      = 4;
    [Tooltip("Chance of getting Legendary.")]
    [Range(0, 100)] public int legendaryWeight = 1;

    // -------------------------------------------------------------------------
    // State
    // -------------------------------------------------------------------------

    public ClassDefinition AssignedClass { get; private set; }
    public bool HasClass => AssignedClass != null;

    // Trial behaviour counters (fed by ClassTrialTracker during tutorial)
    private int killCount       = 0;
    private int meleeDamageDealt = 0;
    private int spellsUsed      = 0;
    private int resourcesGathered = 0;
    private int dashCount       = 0;

    // -------------------------------------------------------------------------
    // Singleton lifecycle
    // -------------------------------------------------------------------------

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;

        // Subscribe to level-up to trigger evolution prompt
        XPSystem.OnLevelUp -= HandleLevelUp;
    }

    private void Start()
    {
        XPSystem.OnLevelUp += HandleLevelUp;
    }

    // -------------------------------------------------------------------------
    // Trial tracking — call these from combat/gathering code during the tutorial
    // -------------------------------------------------------------------------

    public void RecordKill()             => killCount++;
    public void RecordMeleeDamage(int d) => meleeDamageDealt += d;
    public void RecordSpellUsed()        => spellsUsed++;
    public void RecordResourceGathered() => resourcesGathered++;
    public void RecordDash()             => dashCount++;

    // -------------------------------------------------------------------------
    // Class assignment
    // -------------------------------------------------------------------------

    /// <summary>
    /// Called at the end of the tutorial to assign the player their class.
    /// Analyses recorded behaviour, picks a rarity, then selects the best-matching class.
    /// </summary>
    public void AssignClass()
    {
        if (HasClass)
        {
            Debug.LogWarning("[ClassSystem] Class already assigned — ignoring AssignClass().");
            return;
        }

        ClassDefinition.ClassRarity rarity = RollRarity();
        ClassDefinition.ClassArchetype archetype = DetermineArchetype();

        ClassDefinition chosen = SelectClass(rarity, archetype);

        if (chosen == null)
        {
            // Fallback: pick any class of the rolled rarity
            chosen = SelectClass(rarity, null);
        }
        if (chosen == null && classList.Length > 0)
        {
            // Last resort: random
            chosen = classList[UnityEngine.Random.Range(0, classList.Length)];
        }

        if (chosen == null)
        {
            Debug.LogError("[ClassSystem] No classes defined! Add ClassDefinition assets to classList.");
            return;
        }

        ApplyClass(chosen);
    }

    /// <summary>
    /// Directly assign a specific class (used for saves, testing, or story-forced classes).
    /// </summary>
    public void ForceAssignClass(ClassDefinition classDef)
    {
        if (classDef == null) return;
        ApplyClass(classDef);
    }

    // -------------------------------------------------------------------------
    // Class evolution (Level 25)
    // -------------------------------------------------------------------------

    /// <summary>
    /// Evolve into one of the evolution options.
    /// Called when the player makes their choice in the evolution UI.
    /// </summary>
    public void EvolveClass(ClassDefinition newClass)
    {
        if (newClass == null) return;
        if (AssignedClass == null)
        {
            Debug.LogWarning("[ClassSystem] Cannot evolve — no base class assigned.");
            return;
        }

        AssignedClass = newClass;

        // Apply evolution bonuses on top of existing stats
        if (PlayerStats.Instance != null)
        {
            PlayerStats.Instance.ApplyBonus(
                str: newClass.strBonus,
                agi: newClass.agiBonus,
                vit: newClass.vitBonus,
                INT_bonus: newClass.intBonus,
                wis: newClass.wisBonus,
                end: newClass.endBonus
            );
        }

        SystemNotifier.Instance?.Announce(
            $"Class Evolution: {newClass.className} ({newClass.RarityLabel()})",
            SystemNotifier.MessageType.ClassAssign);

        OnClassEvolved?.Invoke(newClass);
        ActionTracker.Instance?.LogAction("local_player", "ClassEvolved", newClass.classId, 100);
    }

    // -------------------------------------------------------------------------
    // Private helpers
    // -------------------------------------------------------------------------

    private void ApplyClass(ClassDefinition chosen)
    {
        AssignedClass = chosen;

        // Apply stat bonuses
        if (PlayerStats.Instance != null)
        {
            PlayerStats.Instance.ApplyBonus(
                str: chosen.strBonus,
                agi: chosen.agiBonus,
                vit: chosen.vitBonus,
                INT_bonus: chosen.intBonus,
                wis: chosen.wisBonus,
                end: chosen.endBonus
            );
        }

        // Announce
        SystemNotifier.Instance?.AnnounceClassAssigned(chosen.className, chosen.RarityLabel());
        OnClassAssigned?.Invoke(chosen);
        ActionTracker.Instance?.LogAction("local_player", "ClassAssigned", chosen.classId, 50);

        Debug.Log($"[ClassSystem] Class assigned: {chosen.className} ({chosen.rarity})");
    }

    private ClassDefinition.ClassRarity RollRarity()
    {
        int roll = UnityEngine.Random.Range(0, 100);
        int cum  = 0;

        cum += legendaryWeight; if (roll < cum) return ClassDefinition.ClassRarity.Legendary;
        cum += epicWeight;      if (roll < cum) return ClassDefinition.ClassRarity.Epic;
        cum += rareWeight;      if (roll < cum) return ClassDefinition.ClassRarity.Rare;
        cum += uncommonWeight;  if (roll < cum) return ClassDefinition.ClassRarity.Uncommon;
        return ClassDefinition.ClassRarity.Common;
    }

    private ClassDefinition.ClassArchetype DetermineArchetype()
    {
        // Infer archetype from trial behaviour
        // Whichever metric is dominant wins
        var scores = new Dictionary<ClassDefinition.ClassArchetype, int>
        {
            { ClassDefinition.ClassArchetype.Warrior, meleeDamageDealt },
            { ClassDefinition.ClassArchetype.Mage,    spellsUsed * 15   },
            { ClassDefinition.ClassArchetype.Rogue,   dashCount * 10    },
            { ClassDefinition.ClassArchetype.Ranger,  killCount * 5     },
            { ClassDefinition.ClassArchetype.Support, resourcesGathered * 8 },
        };

        ClassDefinition.ClassArchetype best = ClassDefinition.ClassArchetype.Warrior;
        int bestScore = -1;
        foreach (var pair in scores)
        {
            if (pair.Value > bestScore)
            {
                bestScore = pair.Value;
                best      = pair.Key;
            }
        }
        return best;
    }

    private ClassDefinition SelectClass(ClassDefinition.ClassRarity rarity,
                                         ClassDefinition.ClassArchetype? archetype)
    {
        if (classList == null || classList.Length == 0) return null;

        // Filter by rarity + archetype
        var candidates = new List<ClassDefinition>();
        foreach (var c in classList)
        {
            if (c == null) continue;
            if (c.rarity != rarity) continue;
            if (archetype.HasValue && c.archetype != archetype.Value) continue;
            candidates.Add(c);
        }

        if (candidates.Count == 0) return null;
        return candidates[UnityEngine.Random.Range(0, candidates.Count)];
    }

    private void HandleLevelUp(int newLevel)
    {
        if (newLevel == 25 && HasClass && AssignedClass.evolutionOptions != null
                                       && AssignedClass.evolutionOptions.Length > 0)
        {
            SystemNotifier.Instance?.Announce(
                "Class Evolution available! Open your Class Panel to choose your path.",
                SystemNotifier.MessageType.ClassAssign);

            // The actual choice is made via ClassEvolutionUI → EvolveClass(chosen)
        }
    }
}
