using System;

namespace IsoCore.Foundation
{
    /// <summary>
    /// Versioned JSON DTO for Foundation world saves. Runtime dictionaries are flattened
    /// into arrays so Unity's JsonUtility can round-trip them deterministically.
    /// </summary>
    [Serializable]
    public class FoundationSaveData
    {
        public const int CurrentVersion = 4;

        public int version = CurrentVersion;
        public string savedUtc;
        public string worldName;
        public int seed;
        public int difficulty;
        public string callingId;
        public FoundationSavedPlayer player;
        public ItemStack[] inventorySlots;
        public int hotbarSelected;
        public FoundationProgressionSaveData progression;
        public FoundationSavedCell[] modifiedCells;
        public FoundationSavedPlaceable[] placedObjects;
        public FoundationSavedStorageContainer[] storageContainers;
        public FoundationSavedCrop[] crops;
        public FoundationSavedInstance instance;
        public float dayNightTime;
        public FoundationSavedMob[] mobs;
        public string[] regionShifts;

        public FoundationSaveMetadata ToMetadata()
        {
            int inventoryCount = 0;
            if (inventorySlots != null)
                for (int i = 0; i < inventorySlots.Length; i++)
                    if (!inventorySlots[i].IsEmpty)
                        inventoryCount += Math.Max(0, inventorySlots[i].count);

            var stats = progression != null ? progression.stats : null;
            return new FoundationSaveMetadata
            {
                version = version,
                supported = version > 0 && version <= CurrentVersion,
                savedUtc = savedUtc,
                worldName = string.IsNullOrWhiteSpace(worldName) ? "Untitled World" : worldName,
                seed = seed,
                difficulty = difficulty,
                callingId = string.IsNullOrWhiteSpace(callingId) ? progression?.currentCallingId ?? "greenhand" : callingId,
                level = stats != null ? Math.Max(1, stats.level) : 1,
                className = stats != null && !string.IsNullOrWhiteSpace(stats.className) ? stats.className : "Wanderer",
                title = stats != null && !string.IsNullOrWhiteSpace(stats.title) ? stats.title : "Newcomer",
                inventoryItemCount = inventoryCount,
                placedObjectCount = placedObjects != null ? placedObjects.Length : 0,
                storageContainerCount = storageContainers != null ? storageContainers.Length : 0,
                cropCount = crops != null ? crops.Length : 0,
            };
        }
    }

    [Serializable]
    public class FoundationSaveMetadata
    {
        public int version;
        public bool supported;
        public string savedUtc;
        public string worldName;
        public int seed;
        public int difficulty;
        public string callingId;
        public int level;
        public string className;
        public string title;
        public int inventoryItemCount;
        public int placedObjectCount;
        public int storageContainerCount;
        public int cropCount;
    }

    [Serializable]
    public struct FoundationSavedPlayer
    {
        public int cellX;
        public int cellY;
        public float groundX;
        public float groundY;
    }

    [Serializable]
    public struct FoundationSavedCell
    {
        public int x;
        public int y;
        public byte height;
        public byte biomeIndex;
        public string surfaceBlockId;
        public string occupantId;
        public string nodeId;
        public bool solidBlock;
        public bool water;
        public bool occupantBlocks;
        public bool nodeBlocks;
        public string underBlockId;
        public byte underHeight;
    }

    [Serializable]
    public struct FoundationSavedPlaceable
    {
        public string placeableId;
        public int x;
        public int y;
    }

    [Serializable]
    public struct FoundationSavedStorageContainer
    {
        public string placeableId;
        public int x;
        public int y;
        public int slotCount;
        public ItemStack[] slots;
    }

    [Serializable]
    public struct FoundationSavedCrop
    {
        public string cropId;
        public int x;
        public int y;
        public int stage;
        public float stageTimer;
    }

    [Serializable]
    public struct FoundationSavedInstance
    {
        public bool active;
        public string instanceId;
        public string displayName;
        public int originX;
        public int originY;
        public int returnCellX;
        public int returnCellY;
        public float returnGroundX;
        public float returnGroundY;
    }

    [Serializable]
    public struct FoundationSavedMob
    {
        public string mobId;
        public float groundX;
        public float groundY;
    }

    [Serializable]
    public struct FoundationKeyValueInt
    {
        public string id;
        public int value;

        public FoundationKeyValueInt(string id, int value)
        {
            this.id = id;
            this.value = value;
        }
    }

    [Serializable]
    public class FoundationProgressionSaveData
    {
        public string currentCallingId;
        public int callingXp;
        public string selectedBranchId;
        public FoundationPlayerStatsSaveData stats;
        public FoundationKeyValueInt[] skillXp;
        public FoundationQuestSaveData[] quests;
        public FoundationRewardUnlock[] unlockedRewards;
        public string[] recentUnlocks;
        public string[] activeBuffs;
        public string[] regionShifts;
        public FoundationKeyValueInt[] trialScores;
        public FoundationTrialLifecycleSaveData trialLifecycle;
        public FoundationTrialEvidenceEntry[] evidenceLog;
        public FoundationKeyValueInt[] xpChannels;
        public FoundationKeyValueInt[] titleProgress;
        public FoundationKeyValueInt[] affinityScores;
        public string[] acquiredTitles;
        public FoundationSystemMessageEntry[] systemMessages;
    }

    [Serializable]
    public class FoundationPlayerStatsSaveData
    {
        public int level;
        public int experience;
        public int experienceToNextLevel;
        public float health;
        public float maxHealth;
        public float mana;
        public float maxMana;
        public int str;
        public int dex;
        public int intelligence;
        public int vit;
        public int def;
        public int luck;
        public string className;
        public string title;
    }

    [Serializable]
    public class FoundationQuestSaveData
    {
        public string questId;
        public bool completed;
        public FoundationKeyValueInt[] objectives;
    }

    [Serializable]
    public class FoundationTrialLifecycleSaveData
    {
        public int trialDay = 1;
        public int trialDurationDays = 7;
        public bool completed;
        public FoundationGrade gradeSnapshot;
        public FoundationTrialOffer[] classOffers;
        public FoundationTrialOffer[] professionOffers;
        public string selectedClassId;
        public string selectedProfessionId;
    }
}
