using UnityEngine;

namespace IsoCore.Foundation
{
    /// <summary>Progression track for repeated mastery: farming, crafting, mapping, warding, etc.</summary>
    public class FoundationSkillDefinition : FoundationDefinition
    {
        public FoundationProgressionActivity activity;
        public FoundationSkillNodeKind primaryNodeKind;
        [TextArea] public string description;
        public string[] unlocks;
    }

    public class FoundationSkillDatabase : Database<FoundationSkillDefinition> { }
}
