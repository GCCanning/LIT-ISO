using UnityEngine;

/// <summary>
/// Defines a player class — its identity, rarity, starting stat bonuses, and upgrade paths.
/// Create instances via Assets → Create → LIT-ISO → Class → Class Definition.
///
/// To add a new class:
///   1. Create a ClassDefinition.asset in Assets/Data/Classes/
///   2. Fill in the fields below
///   3. Add it to the ClassSystem's classList array
///   No code changes required.
/// </summary>
[CreateAssetMenu(fileName = "ClassDefinition", menuName = "LIT-ISO/Class/Class Definition")]
public class ClassDefinition : ScriptableObject
{
    // -------------------------------------------------------------------------
    // Identity
    // -------------------------------------------------------------------------

    [Header("Identity")]
    [Tooltip("Stable internal ID. Never change this after creating save data.")]
    public string classId;

    [Tooltip("Display name shown to the player.")]
    public string className;

    [TextArea(2, 4)]
    [Tooltip("Short flavour description shown at class assignment.")]
    public string description;

    [Tooltip("Icon shown in class selection UI.")]
    public Sprite icon;

    // -------------------------------------------------------------------------
    // Rarity
    // -------------------------------------------------------------------------

    public enum ClassRarity  { Common, Uncommon, Rare, Epic, Legendary }
    public enum ClassArchetype { Warrior, Rogue, Mage, Ranger, Support, Hybrid }

    [Header("Classification")]
    public ClassRarity  rarity;
    public ClassArchetype archetype;

    // -------------------------------------------------------------------------
    // Starting Stat Bonuses (applied once at class assignment)
    // -------------------------------------------------------------------------

    [Header("Base Stat Bonuses (applied on assignment)")]
    [Tooltip("Bonus added to PlayerStats.STR at assignment.")]
    public int strBonus;
    public int agiBonus;
    public int vitBonus;
    public int intBonus;
    public int wisBonus;
    public int endBonus;

    [Tooltip("Multiplier applied to all derived stats (MaxHP, SpellDmg, etc.). Common=1.0, Legendary=3.0")]
    [Range(1f, 3f)]
    public float statMultiplier = 1f;

    // -------------------------------------------------------------------------
    // Skill Slots
    // -------------------------------------------------------------------------

    [Header("Skill Slots")]
    [Tooltip("Maximum spells the player can equip. Common=3, Rare=5, Epic=7, Legendary=10.")]
    [Range(1, 10)]
    public int maxSkillSlots = 3;

    [Tooltip("Spells automatically equipped when this class is assigned (up to maxSkillSlots).")]
    public SpellDefinition[] startingSkills;

    // -------------------------------------------------------------------------
    // Passive
    // -------------------------------------------------------------------------

    [Header("Passive Ability")]
    [Tooltip("One-line description of the class passive.")]
    public string passiveDescription;

    [Tooltip("Numeric value for the passive (e.g. 0.15 = +15% melee damage).")]
    public float passiveValue;

    public enum PassiveType
    {
        None,
        MeleeDamageBonus,
        SpellDamageBonus,
        MoveSpeedBonus,
        HPRegenBonus,
        ManaRegenBonus,
        XPBonus,
        CritChanceBonus,
        DodgeBonus,
    }
    public PassiveType passiveType;

    // -------------------------------------------------------------------------
    // Class Evolution (Level 25)
    // -------------------------------------------------------------------------

    [Header("Evolution Paths (unlocked at Level 25)")]
    [Tooltip("Up to 3 evolved classes the player can choose from at Level 25.")]
    public ClassDefinition[] evolutionOptions;

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    public Color RarityColor()
    {
        return rarity switch
        {
            ClassRarity.Common    => Color.white,
            ClassRarity.Uncommon  => new Color(0.4f, 0.8f, 0.4f),
            ClassRarity.Rare      => new Color(0.3f, 0.6f, 1.0f),
            ClassRarity.Epic      => new Color(0.7f, 0.4f, 1.0f),
            ClassRarity.Legendary => new Color(1.0f, 0.6f, 0.1f),
            _                     => Color.white,
        };
    }

    public string RarityLabel() => rarity.ToString();
}
