using UnityEngine;

namespace IsoCore.Foundation
{
    public class SystemMessageDefinition : FoundationDefinition
    {
        public SystemMessageChannel channel;
        public int priority = 1;
        [TextArea] public string text;
    }

    public class SystemMessageDatabase : Database<SystemMessageDefinition> { }

    public class EvidenceEventDefinition : FoundationDefinition
    {
        public SystemMessageChannel messageChannel = SystemMessageChannel.TrialEvidence;
        [TextArea] public string message;
        public FoundationEvidenceWeight[] evidenceWeights;
        public FoundationXpGrant[] xpGrants;
        public FoundationTitleProgressGrant[] titleProgress;
        public FoundationAffinityGrant[] affinityProgress;
    }

    public class EvidenceEventDatabase : Database<EvidenceEventDefinition> { }

    public class XPChannelDefinition : FoundationDefinition
    {
        public FoundationXpChannel channel;
        public int xpPerLevel = 100;
        [TextArea] public string description;
    }

    public class XPChannelDatabase : Database<XPChannelDefinition> { }

    public class TitleDefinition : FoundationDefinition
    {
        public int threshold = 1;
        public bool mechanical;
        public string effectPolicy;
        public string hiddenClassKey;
        [TextArea] public string unlockMessage;
    }

    public class TitleDatabase : Database<TitleDefinition> { }

    public class AffinityDefinition : FoundationDefinition
    {
        public int awakenThreshold = 10;
        public string family;
        public string[] thresholdRewards;
        [TextArea] public string description;
    }

    public class AffinityDatabase : Database<AffinityDefinition> { }

    public class ClassDefinition : FoundationDefinition
    {
        public FoundationClassRarity rarity;
        public FoundationEvidenceWeight[] weights;
        public string[] preferredAffinityIds;
        [TextArea] public string description;
    }

    public class ClassDatabase : Database<ClassDefinition> { }

    public class ProfessionDefinition : FoundationDefinition
    {
        public FoundationProgressionActivity primaryActivity;
        public string[] progressionSkillIds;
        [TextArea] public string description;
    }

    public class ProfessionDatabase : Database<ProfessionDefinition> { }

    public class DungeonDefinition : FoundationDefinition
    {
        public string family;
        public int threatRank;
        public int travelHours;
        public string[] recommendedSupplyItemIds;
        public string resultId;
        [TextArea] public string description;
    }

    public class DungeonDatabase : Database<DungeonDefinition> { }

    public class ExpeditionTemplateDefinition : FoundationDefinition
    {
        public string dungeonId;
        public string[] requiredSupplyItemIds;
        public int expectedHours;
        public int danger;
    }

    public class ExpeditionTemplateDatabase : Database<ExpeditionTemplateDefinition> { }

    public class DungeonResultDefinition : FoundationDefinition
    {
        public string dungeonId;
        public FoundationXpGrant[] xpRewards;
        public FoundationTitleProgressGrant[] titleProgress;
        public FoundationAffinityGrant[] affinityProgress;
        public FoundationQuestReward[] rewards;
        [TextArea] public string summary;
    }

    public class DungeonResultDatabase : Database<DungeonResultDefinition> { }

    public class GuildBoardEntryDefinition : FoundationDefinition
    {
        public string questId;
        public string worldEventId;
        public int rankRequirement;
        public int expiresAfterDays;
        [TextArea] public string description;
    }

    public class GuildBoardEntryDatabase : Database<GuildBoardEntryDefinition> { }

    public class WorldEventDefinition : FoundationDefinition
    {
        public SystemMessageChannel channel = SystemMessageChannel.WorldEvent;
        public int severity;
        public string triggerId;
        public string consequenceId;
        [TextArea] public string message;
    }

    public class WorldEventDatabase : Database<WorldEventDefinition> { }
}
