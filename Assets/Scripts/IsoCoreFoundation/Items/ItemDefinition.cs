using UnityEngine;

namespace IsoCore.Foundation
{
    /// <summary>
    /// An inventory item. Optional references let an item place a block
    /// (placeBlockId) or a placeable (placeableId), or act as a tool/food.
    /// </summary>
    [CreateAssetMenu(menuName = "ISO-Core Foundation/Item", fileName = "Item")]
    public class ItemDefinition : FoundationDefinition
    {
        [Header("Render")]
        public Color color = Color.white;
        public Sprite icon;

        [Header("Classification")]
        public ItemCategory category = ItemCategory.Resource;
        public int maxStack = 99;

        [Header("Tool (if category == Tool)")]
        public ToolType toolType = ToolType.None;
        public int toolTier = 1; // 1=wood, 2=stone, 3=copper... higher harvests faster

        [Header("Food (if category == Food)")]
        public int foodRestore = 0;

        [Header("Placement references (optional)")]
        public string placeBlockId;     // places this block when used
        public string placeableId;      // places this placeable when used
        public string plantCropId;      // plants this crop (a seed) when used on soil

        public bool PlacesBlock => !string.IsNullOrEmpty(placeBlockId);
        public bool PlacesPlaceable => !string.IsNullOrEmpty(placeableId);
        public bool IsPlaceable => PlacesBlock || PlacesPlaceable;
        public bool IsSeed => !string.IsNullOrEmpty(plantCropId);
        public Sprite Icon => icon;
    }

    public class ItemDatabase : Database<ItemDefinition> { }
}
