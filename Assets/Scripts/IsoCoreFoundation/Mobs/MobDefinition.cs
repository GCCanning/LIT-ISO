using UnityEngine;

namespace IsoCore.Foundation
{
    /// <summary>Who a mob fights. Bandits and Adventurers are hostile to each other.</summary>
    public enum MobFaction { Wildlife, Bandit, Adventurer }

    [CreateAssetMenu(menuName = "ISO-Core Foundation/Mob", fileName = "Mob")]
    public class MobDefinition : FoundationDefinition
    {
        [Header("Render")]
        public Color color = new Color(0.4f, 0.8f, 0.4f);
        public float sizeUnits = 0.55f;
        [Tooltip("Which way the base art looks. Cheap 2-direction facing flips the sprite " +
                 "so mobs face their chase direction (playtest 2026-07-02 #5). Predator " +
                 "plants face RIGHT (false); the slime sheet faces LEFT (true).")]
        public bool artFacesLeft = false;

        [Header("Behaviour")]
        public MobBehavior behaviour = MobBehavior.Passive;
        [Min(0)] public int threatTier = 1;
        [Range(0f, 1f)] public float campWardIgnoreChance = 0.25f;
        public float contactDamage = 4f;
        public float attackRange = 0.55f;
        public float attackCooldownSeconds = 2.25f;
        public float moveSpeed = 1.2f;
        public float wanderRadius = 4f;
        public float repathSeconds = 2.5f;

        [Header("Groups / appearance")]
        [Tooltip("Spawn this many together as a party. 1 = solo.")]
        [Min(1)] public int groupSize = 1;
        [Tooltip("If set, render this Resources/Decorations/<id> sprite instead of a coloured blob (used by ambient critters like glowbug/wisp).")]
        public string decorationSprite = "";

        [Tooltip("Character-appearance catalog id (FoundationCharacterAppearanceCatalog) for humanoid NPCs — renders the same multi-directional sheets the player/character-creator use. \"random\" picks one. Empty = blob/decoration.")]
        public string appearanceId = "";

        [Header("Town role")]
        [Tooltip("Marks this NPC as a merchant. When the player interacts (RMB) with a merchant NPC, the Vendor/Shop screen opens. Set on town merchant defs (e.g. townsfolk_merchant).")]
        public bool isMerchant = false;

        [Header("Faction combat (mob vs mob)")]
        public MobFaction faction = MobFaction.Wildlife;
        [Tooltip("HP for mob-vs-mob fights (the player kills via the existing one-shot path).")]
        public float maxHealth = 20f;
        [Tooltip("Damage dealt to an opposing-faction mob per hit (separate from contactDamage vs the player).")]
        public float meleeDamage = 5f;
        [Tooltip("If set, spawn one stationary companion (e.g. a campfire) alongside this mob's group — makes a 'camp'.")]
        public string companionMobId = "";

        [Header("Drops")]
        public ItemDrop[] drops;

        [Header("Abilities (Phase 3, optional)")]
        [Tooltip("FoundationAbility ids this NPC may cast. NPCs pay cooldown only (no mana/stamina pool).")]
        public string[] abilityIds;
        [Tooltip("Seconds between this NPC's ability casts.")]
        public float abilityCooldownSeconds = 5f;

        [Header("Tier gear (Phase 3, optional)")]
        [Tooltip("Tier->visual-gear mapping. Each entry's lpcCatalogIds are layered when the mob's Level >= minLevel (cumulative: higher tiers stack their gear over lower ones).")]
        public EquipTier[] equipTiers;
    }

    /// <summary>Phase 3: at/above <see cref="minLevel"/>, an NPC wears these LPC visual ids over its themed base.</summary>
    [System.Serializable]
    public struct EquipTier
    {
        public int minLevel;
        public string[] lpcCatalogIds;
    }

    public class MobDatabase : Database<MobDefinition> { }
}
