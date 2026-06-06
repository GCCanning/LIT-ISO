using UnityEngine;

namespace IsoCore.Foundation
{
    /// <summary>
    /// Soft class identity. Callings give titles, starter bonuses, branches, and
    /// long-term fantasy without locking the player out of other skills.
    /// </summary>
    public class FoundationCallingDefinition : FoundationDefinition
    {
        [TextArea] public string description;
        public FoundationCallingTier startingTier = FoundationCallingTier.Novice;
        public string startingTitle = "Newcomer";
        public string[] branchIds;
        public string[] starterSkillIds;
        public FoundationStatBonus[] statBonuses;
        [TextArea] public string capstone;
    }

    public class FoundationCallingDatabase : Database<FoundationCallingDefinition> { }
}
