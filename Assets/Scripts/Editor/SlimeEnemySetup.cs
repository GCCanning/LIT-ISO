using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class SlimeEnemySetup
{
    private const string EnemyAssetFolder = "Assets/World/Enemies";
    private const string SlimeAssetFolder = "Assets/Resources/Enemies/Slime";
    private const string SlimeFrameFolder = "Assets/Resources/Enemies/Slime/Individual Sprites";
    private const string SlimePrefabFolder = "Assets/Resources/Enemies/Slime/Prefabs";
    private const string SampleRootName = "Enemy Test Spawns";

    [MenuItem("Tools/LIT-ISO/Assets/Create Or Update Slime Enemies", false, 35)]
    public static void CreateOrUpdateSlimeEnemiesMenu()
    {
        CreateOrUpdateSlimeEnemies();
        EditorUtility.DisplayDialog(
            "Slime Enemies Ready",
            "Created or updated common, rare, and boss slime definitions and prefabs.",
            "OK");
    }

    public static EnemyDefinition[] CreateOrUpdateSlimeEnemies()
    {
        EnsureFolders();
        ConfigureFrameImporters();

        Texture2D[] idleFrames = LoadFrames("idle");
        Texture2D[] moveFrames = LoadFrames("move");
        Texture2D[] attackFrames = LoadFrames("attack");
        Texture2D[] hurtFrames = LoadFrames("hurt");
        Texture2D[] dieFrames = LoadFrames("die");

        EnemyDefinition common = CreateOrUpdateDefinition(
            $"{EnemyAssetFolder}/Enemy_Slime_Common.asset",
            "slime_common",
            "Common Slime",
            EnemyVariant.Common,
            20,
            1.45f,
            4.3f,
            6.5f,
            0.42f,
            4,
            0.85f,
            0.20f,
            new Color(0.78f, 1f, 0.72f, 1f),
            idleFrames,
            moveFrames,
            attackFrames,
            hurtFrames,
            dieFrames);

        EnemyDefinition rare = CreateOrUpdateDefinition(
            $"{EnemyAssetFolder}/Enemy_Slime_Rare.asset",
            "slime_rare",
            "Rare Slime",
            EnemyVariant.Rare,
            48,
            1.95f,
            5.2f,
            7.5f,
            0.48f,
            8,
            1.08f,
            0.24f,
            new Color(0.55f, 0.78f, 1f, 1f),
            idleFrames,
            moveFrames,
            attackFrames,
            hurtFrames,
            dieFrames);

        EnemyDefinition boss = CreateOrUpdateDefinition(
            $"{EnemyAssetFolder}/Enemy_Slime_Boss.asset",
            "slime_boss",
            "Boss Slime",
            EnemyVariant.Boss,
            180,
            1.05f,
            6.0f,
            8.5f,
            0.72f,
            18,
            1.8f,
            0.34f,
            new Color(1f, 0.66f, 0.48f, 1f),
            idleFrames,
            moveFrames,
            attackFrames,
            hurtFrames,
            dieFrames);

        CreateOrUpdatePrefab(common);
        CreateOrUpdatePrefab(rare);
        CreateOrUpdatePrefab(boss);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        return new[] { common, rare, boss };
    }

    public static void CreateOrUpdateSlimeEnemiesBatch()
    {
        CreateOrUpdateSlimeEnemies();
    }

    public static void CreateOrUpdateSceneSamples(Transform player, IsoWorldChunkManager world, System.Text.StringBuilder log = null)
    {
        EnemyDefinition[] definitions = CreateOrUpdateSlimeEnemies();
        GameObject root = GameObject.Find(SampleRootName);
        if (root == null)
        {
            root = new GameObject(SampleRootName);
        }

        List<SlimeEnemyController> stale = root.GetComponentsInChildren<SlimeEnemyController>(true).ToList();
        foreach (SlimeEnemyController slime in stale)
        {
            Object.DestroyImmediate(slime.gameObject);
        }

        Vector3 origin = player != null ? player.position : Vector3.zero;
        Vector3[] offsets =
        {
            new Vector3(1.8f, 0.8f, 0f),
            new Vector3(-2.0f, 1.2f, 0f),
            new Vector3(2.6f, -1.4f, 0f)
        };

        for (int i = 0; i < definitions.Length; i++)
        {
            GameObject slime = CreateSceneSlime(definitions[i], root.transform);
            slime.transform.position = SnapToGround(origin + offsets[i], world);
            SlimeEnemyController controller = slime.GetComponent<SlimeEnemyController>();
            controller.target = player;
            controller.world = world;
            EditorUtility.SetDirty(controller);
        }

        log?.AppendLine("✓  Slime enemy variants wired: common, rare, boss sample spawns.");
    }

    public static void ConfigureWorldSlimeSpawns(IsoWorldChunkManager world, System.Text.StringBuilder log = null)
    {
        if (world == null)
        {
            return;
        }

        EnemyDefinition[] definitions = CreateOrUpdateSlimeEnemies();
        world.spawnSlimesInPlains = true;
        world.commonSlime = definitions.FirstOrDefault(definition => definition.variant == EnemyVariant.Common);
        world.rareSlime = definitions.FirstOrDefault(definition => definition.variant == EnemyVariant.Rare);
        world.bossSlime = definitions.FirstOrDefault(definition => definition.variant == EnemyVariant.Boss);
        world.commonSlimeChance = 0.012f;
        world.rareSlimeChance = 0.003f;
        world.bossSlimeChance = 0.00035f;
        world.maxSlimesPerChunk = 8;
        world.maxRareSlimesPerChunk = 2;
        world.maxBossSlimesPerChunk = 1;
        world.bossSlimeExclusionRadius = 1;

        EditorUtility.SetDirty(world);
        log?.AppendLine("✓  Plains slime spawning configured: common/rare/boss rarity, chunk caps, 3x3 boss exclusion.");
    }

    private static void EnsureFolders()
    {
        EnsureFolder("Assets/World", "Enemies");
        EnsureFolder("Assets/Resources", "Enemies");
        EnsureFolder("Assets/Resources/Enemies", "Slime");
        EnsureFolder(SlimeAssetFolder, "Prefabs");
    }

    private static void EnsureFolder(string parent, string child)
    {
        string path = $"{parent}/{child}";
        if (!AssetDatabase.IsValidFolder(path))
        {
            AssetDatabase.CreateFolder(parent, child);
        }
    }

    private static void ConfigureFrameImporters()
    {
        foreach (string path in Directory.GetFiles(SlimeAssetFolder, "*.png", SearchOption.AllDirectories))
        {
            string assetPath = path.Replace("\\", "/");
            TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null)
            {
                continue;
            }

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 32f;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.SaveAndReimport();
        }
    }

    private static Texture2D[] LoadFrames(string animationName)
    {
        if (!Directory.Exists(SlimeFrameFolder))
        {
            return new Texture2D[0];
        }

        return Directory.GetFiles(SlimeFrameFolder, $"slime-{animationName}-*.png")
            .OrderBy(path => path)
            .Select(path => AssetDatabase.LoadAssetAtPath<Texture2D>(path.Replace("\\", "/")))
            .Where(texture => texture != null)
            .ToArray();
    }

    private static EnemyDefinition CreateOrUpdateDefinition(
        string path,
        string enemyId,
        string displayName,
        EnemyVariant variant,
        int maxHealth,
        float moveSpeed,
        float detectionRadius,
        float leashRadius,
        float attackRange,
        int contactDamage,
        float visualScale,
        float colliderRadius,
        Color tint,
        Texture2D[] idleFrames,
        Texture2D[] moveFrames,
        Texture2D[] attackFrames,
        Texture2D[] hurtFrames,
        Texture2D[] dieFrames)
    {
        EnemyDefinition definition = AssetDatabase.LoadAssetAtPath<EnemyDefinition>(path);
        if (definition == null)
        {
            definition = ScriptableObject.CreateInstance<EnemyDefinition>();
            AssetDatabase.CreateAsset(definition, path);
        }

        definition.enemyId = enemyId;
        definition.displayName = displayName;
        definition.variant = variant;
        definition.maxHealth = maxHealth;
        definition.moveSpeed = moveSpeed;
        definition.detectionRadius = detectionRadius;
        definition.leashRadius = leashRadius;
        definition.attackRange = attackRange;
        definition.contactDamage = contactDamage;
        definition.visualScale = visualScale;
        definition.colliderRadius = colliderRadius;
        definition.tint = tint;
        definition.wanderRadius = variant == EnemyVariant.Boss ? 1.4f : 2.5f;
        definition.wanderPauseMin = 0.4f;
        definition.wanderPauseMax = variant == EnemyVariant.Boss ? 1.2f : 1.8f;
        definition.idleFrames = idleFrames;
        definition.moveFrames = moveFrames;
        definition.attackFrames = attackFrames;
        definition.hurtFrames = hurtFrames;
        definition.dieFrames = dieFrames;
        definition.pixelsPerUnit = 32f;
        definition.idleFrameDuration = 0.18f;
        definition.moveFrameDuration = 0.12f;
        definition.attackFrameDuration = 0.08f;
        definition.hurtFrameDuration = 0.08f;
        definition.dieFrameDuration = 0.10f;

        EditorUtility.SetDirty(definition);
        return definition;
    }

    private static void CreateOrUpdatePrefab(EnemyDefinition definition)
    {
        GameObject instance = CreateSceneSlime(definition, null);
        string prefabPath = $"{SlimePrefabFolder}/{definition.enemyId}.prefab";
        PrefabUtility.SaveAsPrefabAsset(instance, prefabPath);
        Object.DestroyImmediate(instance);
    }

    private static GameObject CreateSceneSlime(EnemyDefinition definition, Transform parent)
    {
        GameObject slime = new GameObject(definition.displayName);
        if (parent != null)
        {
            slime.transform.SetParent(parent);
        }

        slime.AddComponent<Rigidbody2D>();
        slime.AddComponent<CircleCollider2D>();
        GameObject sprite = new GameObject("SpriteRenderer");
        sprite.transform.SetParent(slime.transform);
        sprite.transform.localPosition = Vector3.zero;
        SpriteRenderer renderer = sprite.AddComponent<SpriteRenderer>();
        renderer.spriteSortPoint = SpriteSortPoint.Pivot;
        renderer.color = definition.tint;

        SlimeEnemyController controller = slime.AddComponent<SlimeEnemyController>();
        controller.definition = definition;
        controller.spriteGroundLift = 0.04f;
        controller.sortingOrderOffset = 5;
        return slime;
    }

    private static Vector3 SnapToGround(Vector3 position, IsoWorldChunkManager world)
    {
        if (world == null)
        {
            return position;
        }

        Vector3Int cell = world.WorldToGroundCell(position);
        return world.GetCellCenterWorld(cell);
    }
}
