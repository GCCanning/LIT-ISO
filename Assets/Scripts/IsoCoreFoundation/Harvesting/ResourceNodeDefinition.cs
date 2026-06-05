using UnityEngine;

namespace IsoCore.Foundation
{
    /// <summary>
    /// A harvestable world object (tree, rock, bush). Distinct from items and
    /// blocks per Reference Study §7 ("world resource nodes as a distinct sub-type").
    /// </summary>
    [CreateAssetMenu(menuName = "ISO-Core Foundation/Resource Node", fileName = "ResourceNode")]
    public class ResourceNodeDefinition : FoundationDefinition
    {
        [Header("Render")]
        public Color color = new Color(0.3f, 0.5f, 0.2f);
        public float widthUnits = 0.7f;
        public float heightUnits = 1.2f;

        [Header("Harvest")]
        public ToolType requiredTool = ToolType.None; // preferred tool (faster)
        public bool toolMandatory = false;            // if true, the required tool is required
        public int hitsToHarvest = 2;
        public ItemDrop[] drops;

        [Header("Respawn")]
        public float respawnSeconds = 0f; // 0 == does not respawn
        public bool blocksMovement = true;
    }

    public class ResourceNodeDatabase : Database<ResourceNodeDefinition> { }
}
