using UnityEngine;

namespace IsoCore.Foundation
{
    /// <summary>A station-bound crafting recipe: inputs -&gt; outputs.</summary>
    [CreateAssetMenu(menuName = "ISO-Core Foundation/Recipe", fileName = "Recipe")]
    public class RecipeDefinition : FoundationDefinition
    {
        [Header("Station (None/Hand == craftable anywhere)")]
        public StationType station = StationType.Hand;

        [Header("Station tier gate (0 = any tier of this station)")]
        // Minimum station tier required to see/craft this recipe. Default 0 means
        // every station of the matching type can craft it (current behaviour
        // unchanged). Higher-tier world stations report their tier via
        // CraftingStationContext.ActiveStationTier; the Crafting UI can hide recipes
        // whose minStationTier exceeds it so higher-tier stations expose better
        // outputs. TODO(tiers): author higher-tier recipes with minStationTier > 0.
        public int minStationTier = 0;

        [Header("Recipe")]
        public RecipeIngredient[] inputs;
        public ItemStack[] outputs;

        [Header("Timed crafts (0 = instant)")]
        public float craftTimeSeconds = 0f;

        [Header("Optional fuel (consumed alongside inputs)")]
        public string fuelItemId;
        public int fuelCount = 1;

        public bool unlockedByDefault = true;
    }

    public class RecipeDatabase : Database<RecipeDefinition> { }
}
