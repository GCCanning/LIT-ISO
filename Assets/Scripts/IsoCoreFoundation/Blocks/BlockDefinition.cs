using UnityEngine;

namespace IsoCore.Foundation
{
    [System.Flags]
    public enum TerrainSubstrate
    {
        None = 0,
        Organic = 1 << 0,
        Mud = 1 << 1,
        Sand = 1 << 2,
        Snow = 1 << 3,
        Stone = 1 << 4,
        Cinder = 1 << 5,
        Moss = 1 << 6,
        GoldenGrass = 1 << 7,
        Constructed = 1 << 8,
        Water = 1 << 9,
        Natural = Organic | Mud | Sand | Snow | Stone | Cinder | Moss | GoldenGrass
    }

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
        [Tooltip("Ecological substrate used to validate procedural prop placement.")]
        public TerrainSubstrate substrate = TerrainSubstrate.Organic;

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
