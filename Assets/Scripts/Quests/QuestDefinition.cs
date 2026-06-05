using UnityEngine;

/// <summary>
/// Defines a quest — its objectives, rewards, and category.
/// Create instances via Assets → Create → LIT-ISO → Quests → Quest Definition.
///
/// To add a new quest: create a QuestDefinition.asset and add it to QuestManager.availableQuests[].
/// To add a new objective type: add a value to ObjectiveType and handle it in QuestManager.Notify*().
/// </summary>
[CreateAssetMenu(fileName = "QuestDefinition", menuName = "LIT-ISO/Quests/Quest Definition")]
public class QuestDefinition : ScriptableObject
{
    // -------------------------------------------------------------------------
    // Identity
    // -------------------------------------------------------------------------

    [Header("Identity")]
    public string questId;
    public string title;
    [TextArea(2, 5)] public string description;
    public Sprite icon;

    public enum QuestCategory
    {
        MainStory,    // Linear, world-changing
        SideQuest,    // Optional NPC quests
        GuildQuest,   // Posted on guild boards, party content
        Dungeon,      // Auto-triggered on dungeon discovery
        WorldEvent,   // Blood Moon, invasion, boss spawn
        Daily,        // Resets each in-game day
    }
    public QuestCategory category = QuestCategory.SideQuest;

    [Tooltip("Must complete these quest IDs before this one becomes available. Leave empty for no prerequisite.")]
    public string[] prerequisiteQuestIds;

    [Tooltip("Minimum player level required to accept this quest.")]
    [Min(1)] public int minimumLevel = 1;

    // -------------------------------------------------------------------------
    // Objectives
    // -------------------------------------------------------------------------

    [System.Serializable]
    public struct QuestObjective
    {
        public enum ObjectiveType
        {
            KillEnemy,       // Kill X of a specific enemy
            KillAny,         // Kill X enemies of any type
            CollectItem,     // Collect X of a specific item
            ClearDungeon,    // Clear a specific dungeon
            ReachLevel,      // Reach a specific level
            ReachBiome,      // Enter a specific biome
            BuildStructure,  // Build a specific building type
            SurviveDays,     // Survive X in-game days
            TalkToNPC,       // Interact with a specific NPC
        }

        public ObjectiveType type;

        [Tooltip("For Kill/Collect: the enemyId or itemId. For ClearDungeon: the dungeonId. For Biome: biomeId.")]
        public string targetId;

        [Tooltip("Number required to complete this objective.")]
        [Min(1)] public int requiredCount;

        [Tooltip("Short text shown in the quest tracker: 'Kill 10 Slimes'")]
        public string displayLabel;
    }

    [Header("Objectives")]
    [Tooltip("All objectives must be completed to finish the quest.")]
    public QuestObjective[] objectives;

    // -------------------------------------------------------------------------
    // Rewards
    // -------------------------------------------------------------------------

    [Header("Rewards")]
    public int xpReward;
    public int goldReward;
    public ItemDefinition[] itemRewards;

    [Tooltip("Title unlocked on quest completion. Leave null for no title reward.")]
    public string titleIdReward;
}
