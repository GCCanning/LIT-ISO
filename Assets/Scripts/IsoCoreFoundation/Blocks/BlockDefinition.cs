using UnityEngine;

namespace IsoCore.Foundation
{
    /// <summary>
    /// One surface/block variant. Render (color), collision (mode), navigation
    /// (height contribution) and item/harvest data are distinct fields — never
    /// conflated. See architecture doc §1.
    /// </summary>
    [CreateAssetMenu(menuName = "ISO-Core Foundation/Block", fileName = "Block")]
    public class BlockDefinition : FoundationDefinition
    {
        [Header("Group")]
        public string groupId;

        [Header("Render")]
        public Color color = Color.magenta;

        [Header("Collision / Navigation")]
        public CollisionMode collision = CollisionMode.Walkable;

        [Header("Harvest")]
        public ToolType requiredTool = ToolType.None;
        public ItemDrop[] drops;

        public bool IsSolid => collision == CollisionMode.Solid;
        public bool IsWater => collision == CollisionMode.Water;
        public bool BlocksMovement => collision == CollisionMode.Solid || collision == CollisionMode.Water;
    }
}
