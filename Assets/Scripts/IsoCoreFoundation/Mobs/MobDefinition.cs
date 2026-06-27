using UnityEngine;

namespace IsoCore.Foundation
{
    [CreateAssetMenu(menuName = "ISO-Core Foundation/Mob", fileName = "Mob")]
    public class MobDefinition : FoundationDefinition
    {
        [Header("Render")]
        public Color color = new Color(0.4f, 0.8f, 0.4f);
        public float sizeUnits = 0.55f;
        [Tooltip("If set, the mob plays the directional sheet under this key in the MobAnimationLibrary; else a coloured blob.")]
        public string animationKey = "";

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

        [Header("Combat (0 = inert / use legacy behaviour)")]
        [Tooltip("Hit points. 0 keeps the legacy one-hit wildlife behaviour.")]
        public float maxHealth = 0f;
        [Tooltip("Damage of a telegraphed attack. If >0 it replaces contactDamage as the aggressive strike.")]
        public float attackDamage = 0f;
        [Tooltip("Run speed when chasing from far. 0 falls back to moveSpeed.")]
        public float chaseSpeed = 0f;
        [Tooltip("The mob aggros when the player comes within this radius. 0 = never aggro on sight.")]
        public float aggroRadius = 0f;
        [Tooltip("Distance from its spawn point before an aggro'd mob leashes back home.")]
        public float leashRadius = 12f;
        [Tooltip("Telegraph window before an attack's damage lands.")]
        public float attackWindupSeconds = 0.35f;
        [Tooltip("Knockback impulse applied to whatever this mob hits / is hit by.")]
        public float knockbackStrength = 0.6f;
        [Tooltip("Character XP awarded to the player on kill (on top of skill/trial evidence).")]
        public int xpReward = 0;

        [Header("Drops")]
        public ItemDrop[] drops;
    }

    public class MobDatabase : Database<MobDefinition> { }
}
