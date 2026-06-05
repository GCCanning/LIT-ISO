using UnityEngine;

namespace IsoCore.Foundation
{
    /// <summary>A station-bound crafting recipe: inputs -&gt; outputs.</summary>
    [CreateAssetMenu(menuName = "ISO-Core Foundation/Recipe", fileName = "Recipe")]
    public class RecipeDefinition : FoundationDefinition
    {
        [Header("Station (None/Hand == craftable anywhere)")]
        public StationType station = StationType.Hand;

        [Header("Recipe")]
        public RecipeIngredient[] inputs;
        public ItemStack[] outputs;

        public bool unlockedByDefault = true;
    }

    public class RecipeDatabase : Database<RecipeDefinition> { }
}
