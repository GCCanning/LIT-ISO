using UnityEngine;

public enum EnemyVariant
{
    Common,
    Rare,
    Boss
}

/// <summary>Rank determines XP reward tier and dungeon difficulty weighting.</summary>
public enum EnemyRank
{
    F,  // Level 1-10 — starter mobs
    E,  // Level 11-25
    D,  // Level 26-45
    C,  // Level 46-60
    B,  // Level 61-75
    A,  // Level 76-90 — world bosses
    S   // Level 91-100 — legendary bosses
}

[CreateAssetMenu(fileName = "EnemyDefinition", menuName = "LIT-ISO/Enemies/Enemy Definition")]
public class EnemyDefinition : ScriptableObject
{
    [Header("Identity")]
    public string enemyId = "slime_common";
    public string displayName = "Common Slime";
    public EnemyVariant variant = EnemyVariant.Common;

    [Header("Rank & Progression")]
    [Tooltip("Enemy rank determines XP reward tier and dungeon difficulty.")]
    public EnemyRank rank = EnemyRank.F;

    [Tooltip("XP awarded to the player when this enemy is killed. F=5-15, E=20-50, etc.")]
    [Min(0)] public int xpReward = 10;

    [Tooltip("Gold coins dropped on death (random range: goldMin to goldMax).")]
    [Min(0)] public int goldMin = 0;
    [Min(0)] public int goldMax = 5;

    [Header("Stats")]
    [Min(1)] public int maxHealth = 20;
    [Min(0f)] public float moveSpeed = 1.4f;
    [Min(0f)] public float detectionRadius = 4.5f;
    [Min(0f)] public float leashRadius = 7f;
    [Min(0f)] public float attackRange = 0.45f;
    [Min(0)] public int contactDamage = 5;
    [Min(0f)] public float attackCooldown = 0.9f;

    [Header("Feel")]
    [Min(0.1f)] public float visualScale = 1f;
    [Min(0.05f)] public float colliderRadius = 0.22f;
    public Color tint = Color.white;
    public float wanderRadius = 2.5f;
    public float wanderPauseMin = 0.5f;
    public float wanderPauseMax = 1.4f;

    [Header("Animation Frames")]
    public Texture2D[] idleFrames;
    public Texture2D[] moveFrames;
    public Texture2D[] attackFrames;
    public Texture2D[] hurtFrames;
    public Texture2D[] dieFrames;
    [Min(1f)] public float pixelsPerUnit = 32f;
    [Min(0.02f)] public float idleFrameDuration = 0.18f;
    [Min(0.02f)] public float moveFrameDuration = 0.12f;
    [Min(0.02f)] public float attackFrameDuration = 0.08f;
    [Min(0.02f)] public float hurtFrameDuration = 0.08f;
    [Min(0.02f)] public float dieFrameDuration = 0.10f;
}
