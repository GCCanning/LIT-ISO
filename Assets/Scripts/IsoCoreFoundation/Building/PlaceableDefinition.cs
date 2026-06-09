using UnityEngine;

namespace IsoCore.Foundation
{
    /// <summary>
    /// A placeable object (workbench, chest, lantern, …). Occupies a cell, may
    /// block movement, and may expose an interaction (crafting station / container).
    /// </summary>
    [CreateAssetMenu(menuName = "ISO-Core Foundation/Placeable", fileName = "Placeable")]
    public class PlaceableDefinition : FoundationDefinition
    {
        [Header("Render")]
        public Color color = new Color(0.6f, 0.45f, 0.3f);
        public float widthUnits = 0.8f;
        public float heightUnits = 1.0f;

        [Header("Placement")]
        public bool blocksMovement = true;
        public string requiredItemId; // item consumed to place this
        [Min(1)] public int footprintWidth = 1;
        [Min(1)] public int footprintHeight = 1;
        [Tooltip("Optional short label shown on multi-tile placement previews.")]
        public string footprintLabel;

        [Header("Interaction")]
        public InteractionKind interaction = InteractionKind.Decoration;
        public StationType stationType = StationType.None;
        public string entranceLabel = "Enter";
        public string destinationId;
        public string destinationDisplayName;

        [Header("Construction (if interaction == Construction)")]
        public string constructionResultPlaceableId;
        public RecipeIngredient[] constructionCost = System.Array.Empty<RecipeIngredient>();

        [Header("Light (campfire / lantern)")]
        [Tooltip("When true, spawns a warm additive glow that fades in at night and flickers.")]
        public bool emitsLight = false;
        public Color lightColor = new Color(1f, 0.7f, 0.35f);
        [Tooltip("Glow radius in world units.")]
        public float lightRadius = 2.4f;

        [Header("Campsite")]
        [Tooltip("When true, this object creates a campsite safety/recovery aura.")]
        public bool isCampsite = false;
        [Tooltip("Monster threat tier fully warded by this fire. Higher tiers may breach.")]
        [Min(0)] public int campTier = 0;
        [Tooltip("World-unit radius used for campsite recovery and warding.")]
        public float campWardRadius = 0f;
        [Tooltip("Recovery multiplier while standing in this campsite aura.")]
        public float campRecoveryMultiplier = 1f;

        public bool IsConstructionPlot =>
            interaction == InteractionKind.Construction &&
            !string.IsNullOrWhiteSpace(constructionResultPlaceableId);

        public int FootprintWidth => Mathf.Max(1, footprintWidth);
        public int FootprintHeight => Mathf.Max(1, footprintHeight);
        public bool HasMultiCellFootprint => FootprintWidth > 1 || FootprintHeight > 1;
        public string FootprintDisplay =>
            string.IsNullOrWhiteSpace(footprintLabel) ? Display : footprintLabel;
    }

    public class PlaceableDatabase : Database<PlaceableDefinition> { }
}
