using UnityEngine;

namespace IsoCore.Foundation
{
    public class FoundationAbilityDefinition : FoundationDefinition
    {
        public FoundationAbilityKind kind = FoundationAbilityKind.Skill;
        public FoundationAbilityResource resource = FoundationAbilityResource.Stamina;
        public FoundationAbilityElement element = FoundationAbilityElement.None;
        public FoundationProgressionActivity activity = FoundationProgressionActivity.Combat;
        public int resourceCost = 8;
        public float cooldownSeconds = 1f;
        public float basePower = 1f;
        public float range = 1.4f;
        public int activityXp = 6;
        public string[] skillIds;
        public string evidenceId;
        public string affinityId;
        [TextArea] public string description;
        [TextArea] public string systemMessage;

        public bool IsSpell => kind == FoundationAbilityKind.Spell;
        public bool IsSkill => kind == FoundationAbilityKind.Skill;
        public bool UsesAffinity => !string.IsNullOrWhiteSpace(affinityId);
    }

    public class FoundationAbilityDatabase : Database<FoundationAbilityDefinition> { }
}
