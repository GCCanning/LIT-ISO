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

        // ---- In-world effect + VFX (Slice 1) ----
        public FoundationAbilityDelivery delivery = FoundationAbilityDelivery.Melee;
        public FoundationAbilityAnchor vfxAnchor = FoundationAbilityAnchor.Hand;
        public float dashTiles = 2f;          // Blink distance
        public float projectileSpeed = 8f;    // Projectile travel speed (world u/s)
        public float areaRadius = 2f;         // Cone/Nova/Aura radius
        public float buffDuration = 6f;       // SelfBuff/Aura/Snare duration
        public float effectScale = 12f;       // scaledPower -> damage/heal multiplier
        public string statusEffectId;

        // Phase 4: optional LPC one-shot animation id (spellcast/shoot/slash/thrust/hurt). When
        // empty, ResolvedAnimationId derives a sensible default from kind/delivery.
        public string animationId;

        public bool IsSpell => kind == FoundationAbilityKind.Spell;
        public bool IsSkill => kind == FoundationAbilityKind.Skill;
        public bool UsesAffinity => !string.IsNullOrWhiteSpace(affinityId);

        /// <summary>
        /// The LPC one-shot animation to play for this ability. Uses the explicit
        /// <see cref="animationId"/> if set; otherwise: Spell -> "spellcast",
        /// ranged/projectile -> "shoot", melee Skill -> "slash".
        /// </summary>
        public string ResolvedAnimationId
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(animationId)) return animationId;
                if (kind == FoundationAbilityKind.Spell) return "spellcast";
                if (delivery == FoundationAbilityDelivery.Projectile) return "shoot";
                return "slash";
            }
        }
    }

    public class FoundationAbilityDatabase : Database<FoundationAbilityDefinition> { }
}
