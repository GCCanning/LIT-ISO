using UnityEngine;

namespace IsoCore.Foundation
{
    /// <summary>Data-only quest definition. Runtime progress lives in FoundationProgression.</summary>
    public class FoundationQuestDefinition : FoundationDefinition
    {
        public FoundationQuestType type;
        public string act;
        [TextArea] public string description;
        public FoundationQuestObjective[] objectives;
        public FoundationQuestReward[] rewards;
    }

    public class FoundationQuestDatabase : Database<FoundationQuestDefinition> { }
}
