using UnityEngine;
using UnityEngine.Tilemaps;

[CreateAssetMenu(menuName = "Iso World/Biome Asset Rule Set", fileName = "BiomeAssetRuleSet")]
public class BiomeAssetRuleSet : ScriptableObject
{
    public BiomeKind biomeKind = BiomeKind.Unknown;
    public string displayName = "Biome";

    [Header("Terrain Tiles")]
    public WeightedTileRule[] flatGroundTiles;
    public WeightedTileRule[] raisedGroundTiles;
    public WeightedTileRule[] decorationTiles;

    [Header("Prefab Decorations")]
    public WeightedPrefabRule[] prefabDecorations;

    [Header("Resource Nodes")]
    public WeightedResourceNodeRule[] resourceNodes;

    [Header("Placement Rules")]
    [Range(0f, 1f)] public float decorationDensity = 0.02f;
    [Range(0f, 1f)] public float resourceDensity = 0.015f;
    [Range(0f, 1f)] public float transitionDecorationMultiplier = 0.45f;
    [Range(0f, 1f)] public float transitionResourceMultiplier = 0.45f;
    public int minAllowedHeight = 0;
    public int maxAllowedHeight = 3;
    public bool allowOnHeightEdges = false;
    public bool allowOnTransitionCells = true;

    public bool AllowsPlacement(int height, bool isTransitionCell, bool isHeightEdge)
    {
        if (height < minAllowedHeight || height > maxAllowedHeight)
        {
            return false;
        }

        if (!allowOnTransitionCells && isTransitionCell)
        {
            return false;
        }

        if (!allowOnHeightEdges && isHeightEdge)
        {
            return false;
        }

        return true;
    }
}

[System.Serializable]
public class WeightedTileRule
{
    public string ruleName;
    public TileBase tile;
    [Min(0f)] public float weight = 1f;
    public int minHeight = 0;
    public int maxHeight = 3;
    public bool transitionOnly = false;

    public bool AllowsHeight(int height, bool isTransitionCell)
    {
        if (height < minHeight || height > maxHeight)
        {
            return false;
        }

        return !transitionOnly || isTransitionCell;
    }
}

[System.Serializable]
public class WeightedPrefabRule
{
    public string ruleName;
    public GameObject prefab;
    [Min(0f)] public float weight = 1f;
    [Range(0f, 1f)] public float spawnChance = 0.01f;
    public float minSpacing = 2.5f;
    public int minHeight = 0;
    public int maxHeight = 0;
    public bool blocksMovement = false;
    public bool transitionOnly = false;

    public bool AllowsHeight(int height, bool isTransitionCell)
    {
        if (height < minHeight || height > maxHeight)
        {
            return false;
        }

        return !transitionOnly || isTransitionCell;
    }
}

[System.Serializable]
public class WeightedResourceNodeRule
{
    public string ruleName;
    public ResourceNodeDefinition node;
    [Min(0f)] public float weight = 1f;
    [Range(0f, 1f)] public float spawnChance = 0.01f;
    public float minSpacing = 2.5f;
    public int minHeight = 0;
    public int maxHeight = 2;
    public bool transitionOnly = false;

    public bool AllowsHeight(int height, bool isTransitionCell)
    {
        if (height < minHeight || height > maxHeight)
        {
            return false;
        }

        return !transitionOnly || isTransitionCell;
    }
}
