using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

public static class BiomeAssetRuleSetSetup
{
    private const string RuleFolder = "Assets/World/BiomeRules";

    [MenuItem("Tools/LIT-ISO/Assets/Create Or Update Biome Asset Rules", false, 203)]
    public static void CreateOrUpdateBiomeAssetRules()
    {
        EnsureFolder("Assets/World");
        EnsureFolder(RuleFolder);

        CreateOrUpdatePlains();
        CreateOrUpdateDesert();
        CreateOrUpdateFrozenHighland();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[BiomeAssetRuleSetSetup] Biome asset rules created/updated in Assets/World/BiomeRules.");
    }

    private static void CreateOrUpdatePlains()
    {
        BiomeAssetRuleSet rules = GetOrCreate("BiomeRules_Plains.asset", BiomeKind.Plains, "Plains");
        rules.decorationDensity = 0.032f;
        rules.resourceDensity = 0.022f;
        rules.transitionDecorationMultiplier = 0.45f;
        rules.transitionResourceMultiplier = 0.55f;
        rules.minAllowedHeight = 0;
        rules.maxAllowedHeight = 2;
        rules.allowOnHeightEdges = false;
        rules.allowOnTransitionCells = true;

        rules.flatGroundTiles = new[]
        {
            TileRule("Flat Grass", "Assets/Tilemaps/Isometric/RuleTiles/NeighbourTiles/NeighbourTile_Plains_FlatGrass.asset", 2f, 0, 2),
            TileRule("Flat Grass Random", "Assets/Tilemaps/Isometric/RuleTiles/RandomTiles/RandomTile_Plains_FlatGrass.asset", 1f, 0, 2),
        };
        rules.raisedGroundTiles = new[]
        {
            TileRule("Raised Grass", "Assets/Tilemaps/Isometric/RuleTiles/NeighbourTiles/NeighbourTile_Plains_RaisedGrass.asset", 2f, 1, 2),
            TileRule("Raised Grass Random", "Assets/Tilemaps/Isometric/RuleTiles/RandomTiles/RandomTile_Plains_RaisedGrass.asset", 1f, 1, 2),
        };
        rules.decorationTiles = new[]
        {
            TileRule("Plants", "Assets/Tilemaps/Isometric/RuleTiles/RandomTiles/RandomTile_Plains_Plants.asset", 1f, 0, 1),
        };
        rules.prefabDecorations = new[]
        {
            PrefabRule("Tree Round", "Assets/Prefabs/Decorations/Plains/Tree_Round.prefab", 1f, 0.018f, 3.0f, 0, 1, true),
            PrefabRule("Tree Stump", "Assets/Prefabs/Decorations/Plains/TreeStump.prefab", 0.35f, 0.006f, 2.5f, 0, 1, false),
            PrefabRule("Rare Chest", "Assets/Prefabs/Decorations/Plains/Chest.prefab", 0.05f, 0.001f, 12.0f, 0, 1, true),
        };
        rules.resourceNodes = new[]
        {
            ResourceRule("Oak Tree", "Assets/World/ResourceNodes/Node_OakTree.asset", 1f, 0.035f, 3.0f, 0, 1),
            ResourceRule("Rock", "Assets/World/ResourceNodes/Node_Rock.asset", 0.35f, 0.012f, 3.0f, 0, 2),
        };
        EditorUtility.SetDirty(rules);
    }

    private static void CreateOrUpdateDesert()
    {
        BiomeAssetRuleSet rules = GetOrCreate("BiomeRules_Desert.asset", BiomeKind.Desert, "Desert");
        rules.decorationDensity = 0.010f;
        rules.resourceDensity = 0.026f;
        rules.transitionDecorationMultiplier = 0.35f;
        rules.transitionResourceMultiplier = 0.65f;
        rules.minAllowedHeight = 0;
        rules.maxAllowedHeight = 2;
        rules.allowOnHeightEdges = false;
        rules.allowOnTransitionCells = true;

        rules.flatGroundTiles = new[]
        {
            TileRule("Flat Sand", "Assets/Tilemaps/Isometric/RuleTiles/NeighbourTiles/NeighbourTile_Desert_FlatSand.asset", 2f, 0, 2),
            TileRule("Flat Sand Wave", "Assets/Tilemaps/Isometric/RuleTiles/NeighbourTiles/NeighbourTile_Desert_FlatSandWave.asset", 0.65f, 0, 2),
            TileRule("Flat Sand Random", "Assets/Tilemaps/Isometric/RuleTiles/RandomTiles/RandomTile_Desert_FlatSand.asset", 1f, 0, 2),
        };
        rules.raisedGroundTiles = new[]
        {
            TileRule("Raised Sand", "Assets/Tilemaps/Isometric/RuleTiles/NeighbourTiles/NeighbourTile_Desert_RaisedSand.asset", 2f, 1, 2),
            TileRule("Raised Sand Wave", "Assets/Tilemaps/Isometric/RuleTiles/NeighbourTiles/NeighbourTile_Desert_RaisedSandWave.asset", 0.65f, 1, 2),
            TileRule("Raised Sand Random", "Assets/Tilemaps/Isometric/RuleTiles/RandomTiles/RandomTile_Desert_RaisedSand.asset", 1f, 1, 2),
        };
        rules.decorationTiles = new[]
        {
            TileRule("Coral And Rocks", "Assets/Tilemaps/Isometric/RuleTiles/RandomTiles/RandomTile_Desert_Coral.asset", 0.7f, 0, 2),
            TileRule("Dry Planks", "Assets/Tilemaps/Isometric/RuleTiles/RandomTiles/RandomTile_Desert_Planks.asset", 0.35f, 0, 1),
        };
        rules.prefabDecorations = new WeightedPrefabRule[0];
        rules.resourceNodes = new[]
        {
            ResourceRule("Rock", "Assets/World/ResourceNodes/Node_Rock.asset", 1f, 0.030f, 3.0f, 0, 2),
        };
        EditorUtility.SetDirty(rules);
    }

    private static void CreateOrUpdateFrozenHighland()
    {
        BiomeAssetRuleSet rules = GetOrCreate("BiomeRules_FrozenHighland.asset", BiomeKind.FrozenMountain, "Frozen / Highland Hills");
        rules.decorationDensity = 0.018f;
        rules.resourceDensity = 0.032f;
        rules.transitionDecorationMultiplier = 0.30f;
        rules.transitionResourceMultiplier = 0.70f;
        rules.minAllowedHeight = 0;
        rules.maxAllowedHeight = 3;
        rules.allowOnHeightEdges = false;
        rules.allowOnTransitionCells = true;

        rules.flatGroundTiles = new[]
        {
            TileRule("Flat Snow", "Assets/Tilemaps/Isometric/RuleTiles/NeighbourTiles/NeighbourTile_FrozenMountain_FlatSnow.asset", 2f, 0, 3),
            TileRule("Flat Snow Random", "Assets/Tilemaps/Isometric/RuleTiles/RandomTiles/RandomTile_FrozenMountain_FlatSnow.asset", 1f, 0, 3),
            TileRule("Flat Ground Breakup", "Assets/Tilemaps/Isometric/RuleTiles/RandomTiles/RandomTile_FrozenMountain_FlatGround.asset", 0.55f, 0, 2),
        };
        rules.raisedGroundTiles = new[]
        {
            TileRule("Raised Snow", "Assets/Tilemaps/Isometric/RuleTiles/NeighbourTiles/NeighbourTile_FrozenMountain_RaisedSnow.asset", 2f, 1, 3),
            TileRule("Raised Snow Random", "Assets/Tilemaps/Isometric/RuleTiles/RandomTiles/RandomTile_FrozenMountain_RaisedSnow.asset", 1f, 1, 3),
        };
        rules.decorationTiles = new WeightedTileRule[0];
        rules.prefabDecorations = new WeightedPrefabRule[0];
        rules.resourceNodes = new[]
        {
            ResourceRule("Rock", "Assets/World/ResourceNodes/Node_Rock.asset", 1f, 0.035f, 3.0f, 1, 3),
        };
        EditorUtility.SetDirty(rules);
    }

    private static BiomeAssetRuleSet GetOrCreate(string fileName, BiomeKind kind, string displayName)
    {
        string path = $"{RuleFolder}/{fileName}";
        BiomeAssetRuleSet rules = AssetDatabase.LoadAssetAtPath<BiomeAssetRuleSet>(path);
        if (rules == null)
        {
            rules = ScriptableObject.CreateInstance<BiomeAssetRuleSet>();
            AssetDatabase.CreateAsset(rules, path);
        }

        rules.biomeKind = kind;
        rules.displayName = displayName;
        return rules;
    }

    private static WeightedTileRule TileRule(string name, string path, float weight, int minHeight, int maxHeight, bool transitionOnly = false)
    {
        return new WeightedTileRule
        {
            ruleName = name,
            tile = AssetDatabase.LoadAssetAtPath<TileBase>(path),
            weight = weight,
            minHeight = minHeight,
            maxHeight = maxHeight,
            transitionOnly = transitionOnly
        };
    }

    private static WeightedPrefabRule PrefabRule(string name, string path, float weight, float chance, float spacing, int minHeight, int maxHeight, bool blocksMovement)
    {
        return new WeightedPrefabRule
        {
            ruleName = name,
            prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path),
            weight = weight,
            spawnChance = chance,
            minSpacing = spacing,
            minHeight = minHeight,
            maxHeight = maxHeight,
            blocksMovement = blocksMovement
        };
    }

    private static WeightedResourceNodeRule ResourceRule(string name, string path, float weight, float chance, float spacing, int minHeight, int maxHeight)
    {
        return new WeightedResourceNodeRule
        {
            ruleName = name,
            node = AssetDatabase.LoadAssetAtPath<ResourceNodeDefinition>(path),
            weight = weight,
            spawnChance = chance,
            minSpacing = spacing,
            minHeight = minHeight,
            maxHeight = maxHeight
        };
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
        {
            return;
        }

        string parent = System.IO.Path.GetDirectoryName(path).Replace("\\", "/");
        string folder = System.IO.Path.GetFileName(path);
        AssetDatabase.CreateFolder(parent, folder);
    }
}
