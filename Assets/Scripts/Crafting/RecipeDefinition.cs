using UnityEngine;

/// <summary>
/// Defines a craftable recipe.
/// Create via Assets → Create → LIT-ISO → Crafting → Recipe Definition.
///
/// To add a new recipe: create a RecipeDefinition.asset — no code changes needed.
/// </summary>
[CreateAssetMenu(fileName = "RecipeDefinition", menuName = "LIT-ISO/Crafting/Recipe Definition")]
public class RecipeDefinition : ScriptableObject
{
    [Header("Identity")]
    public string recipeId;
    public string displayName;
    [TextArea(1, 2)] public string description;
    public Sprite icon;

    [Header("Output")]
    public ItemDefinition outputItem;
    [Min(1)] public int outputAmount = 1;

    [Header("Ingredients")]
    public Ingredient[] ingredients;

    [System.Serializable]
    public struct Ingredient
    {
        public ItemDefinition item;
        [Min(1)] public int amount;
    }

    [Header("Requirements")]
    [Tooltip("Which crafting station is required.")]
    public CraftingStation requiredStation;

    public enum CraftingStation
    {
        Anywhere,       // Can craft from inventory (campfire, basic crafting)
        Workbench,      // Basic crafting table
        Blacksmith,     // Weapons, armour, tools
        Alchemist,      // Potions, elixirs, poisons
        Enchanter,      // Imbue gear with magic
        Jeweler,        // Rings, amulets, gems
        Loom,           // Cloth, cloaks, bags
        Cookfire,       // Food and meal buffs
        Sawmill,        // Bows, staves, building materials
    }

    [Tooltip("Minimum profession level required.")]
    [Min(1)] public int requiredProfessionLevel = 1;

    [Tooltip("Which profession this recipe belongs to.")]
    public CraftingProfession profession;

    public enum CraftingProfession
    {
        General,
        Blacksmithing,
        Alchemy,
        Enchanting,
        Tailoring,
        Woodworking,
        Jewelcrafting,
        Cooking,
    }

    [Header("Progression")]
    [Tooltip("Profession XP awarded on craft.")]
    [Min(0)] public int professionXpReward = 10;

    [Tooltip("Time in seconds to craft. 0 = instant.")]
    [Min(0f)] public float craftTimeSeconds = 3f;

    [Header("Category")]
    public RecipeCategory category;

    public enum RecipeCategory
    {
        Weapon,
        Armour,
        Tool,
        Potion,
        Food,
        Material,
        Building,
        Accessory,
        Enchantment,
        Special,
    }
}
