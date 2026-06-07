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

        [Header("Interaction")]
        public InteractionKind interaction = InteractionKind.Decoration;
        public StationType stationType = StationType.None;
        public string entranceLabel = "Enter";
        public string destinationId;
        public string destinationDisplayName;

        [Header("Light (campfire / lantern)")]
        [Tooltip("When true, spawns a warm additive glow that fades in at night and flickers.")]
        public bool emitsLight = false;
        public Color lightColor = new Color(1f, 0.7f, 0.35f);
        [Tooltip("Glow radius in world units.")]
        public float lightRadius = 2.4f;
    }

    public class PlaceableDatabase : Database<PlaceableDefinition> { }
}
