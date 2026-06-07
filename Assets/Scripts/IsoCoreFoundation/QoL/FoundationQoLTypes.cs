using System;

namespace IsoCore.Foundation
{
    public enum FoundationSystemFeedChannel
    {
        Notice,
        Warning,
        TrialEvidence,
        LevelUp,
        SkillUnlock,
        Title,
        Affinity,
        Quest,
        Dungeon,
        Party,
        WorldEvent,
        Inventory,
        Building,
        Travel
    }

    public enum FoundationPinnedGoalType
    {
        None,
        Quest,
        Recipe,
        Title,
        Affinity,
        TrialTendency,
        Expedition,
        Building,
        Profession
    }

    public enum FoundationLoadoutTemplateType
    {
        Custom,
        QuickDelve,
        DeepDelve,
        BossAttempt,
        GatheringRun,
        RescueRun,
        TradeRun,
        BuildRun
    }

    [Serializable]
    public class FoundationQoLSaveData
    {
        public FoundationSystemFeedSettingsSaveData feedSettings;
        public FoundationPinnedGoalSaveData[] pinnedGoals;
        public FoundationInventorySlotQoLSaveData[] inventorySlots;
        public FoundationLoadoutSaveData[] loadouts;
        public FoundationAccessibilitySaveData accessibility;
    }

    [Serializable]
    public class FoundationSystemFeedSettingsSaveData
    {
        public FoundationSystemFeedChannelSetting[] channels;
        public int maxVisibleMessages = 12;
        public bool collapseRoutineMessages = true;
    }

    [Serializable]
    public struct FoundationSystemFeedChannelSetting
    {
        public FoundationSystemFeedChannel channel;
        public bool visible;

        public FoundationSystemFeedChannelSetting(FoundationSystemFeedChannel channel, bool visible)
        {
            this.channel = channel;
            this.visible = visible;
        }
    }

    [Serializable]
    public struct FoundationPinnedGoalSaveData
    {
        public FoundationPinnedGoalType type;
        public string targetId;
        public bool shared;
        public int playerId;
    }

    [Serializable]
    public struct FoundationInventorySlotQoLSaveData
    {
        public int slot;
        public bool favorite;
        public bool locked;
    }

    [Serializable]
    public class FoundationLoadoutSaveData
    {
        public string id;
        public FoundationLoadoutTemplateType templateType;
        public string displayName;
        public ItemStack[] items;
    }

    [Serializable]
    public class FoundationAccessibilitySaveData
    {
        public float hudScale = 1f;
        public float systemFeedDuration = 4f;
        public float systemFeedDensity = 1f;
        public bool reducedMotion;
        public bool highContrast;
    }

    [Serializable]
    public class FoundationQoLReadState
    {
        public FoundationSystemFeedSettingsReadState feedSettings;
        public FoundationPinnedGoalReadState[] pinnedGoals;
        public FoundationInventoryQoLReadState inventory;
        public FoundationLoadoutReadState[] loadouts;
        public FoundationAccessibilityReadState accessibility;
        public FoundationSystemFeedMessageReadState[] visibleMessages;
    }

    [Serializable]
    public class FoundationSystemFeedSettingsReadState
    {
        public FoundationSystemFeedChannelSetting[] channels;
        public int maxVisibleMessages;
        public bool collapseRoutineMessages;
    }

    [Serializable]
    public struct FoundationSystemFeedMessageReadState
    {
        public int sequence;
        public FoundationSystemFeedChannel channel;
        public string text;
        public string sourceId;
        public int priority;
        public bool visible;
    }

    [Serializable]
    public struct FoundationPinnedGoalReadState
    {
        public FoundationPinnedGoalType type;
        public string targetId;
        public string displayName;
        public string detail;
        public float progress01;
        public bool completed;
        public bool available;
        public bool shared;
        public int playerId;
    }

    [Serializable]
    public class FoundationInventoryQoLReadState
    {
        public FoundationInventorySlotQoLReadState[] slots;
    }

    [Serializable]
    public struct FoundationInventorySlotQoLReadState
    {
        public int slot;
        public string itemId;
        public int count;
        public bool favorite;
        public bool locked;
    }

    [Serializable]
    public struct FoundationLoadoutReadState
    {
        public string id;
        public FoundationLoadoutTemplateType templateType;
        public string displayName;
        public ItemStack[] items;
    }

    [Serializable]
    public struct FoundationAccessibilityReadState
    {
        public float hudScale;
        public float systemFeedDuration;
        public float systemFeedDensity;
        public bool reducedMotion;
        public bool highContrast;
    }
}
