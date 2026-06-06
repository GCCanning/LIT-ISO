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
        public const int CurrentVersion = 1;

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
        public FoundationSavedCrop[] crops;
        public float dayNightTime;
        public FoundationSavedMob[] mobs;
        public string[] regionShifts;
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
    public struct FoundationSavedCrop
    {
        public string cropId;
        public int x;
        public int y;
        public int stage;
        public float stageTimer;
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
}
