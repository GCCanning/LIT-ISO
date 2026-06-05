using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using EthraClone.TrialWeek;

public static class AssetForgeAutomation
{
    private const float CharacterPixelsPerUnit = 128f;
    private const float TilePixelsPerUnit = 64f;
    private static readonly string[] DirectionOrder = { "S", "SE", "E", "NE", "N", "NW", "W", "SW" };

    [MenuItem("LIT-ISO/Asset Forge/Rebuild Generated Asset Automation")]
    public static void RebuildAll()
    {
        string[] manifests = Directory.GetFiles("Assets/Generated", "manifest.json", SearchOption.AllDirectories);
        foreach (string manifestPath in manifests)
        {
            RebuildManifest(manifestPath.Replace('\\', '/'));
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"Asset Forge automation rebuilt {manifests.Length} manifest(s).");
    }

    [MenuItem("LIT-ISO/Asset Forge/Validate Promotion Readiness")]
    public static void ValidatePromotionReadiness()
    {
        string[] manifests = Directory.Exists("Assets/Generated")
            ? Directory.GetFiles("Assets/Generated", "manifest.json", SearchOption.AllDirectories)
            : Array.Empty<string>();

        List<PromotionAuditEntry> entries = new List<PromotionAuditEntry>();
        foreach (string path in manifests)
        {
            string manifestPath = path.Replace('\\', '/');
            string json = File.ReadAllText(manifestPath);
            ManifestInfo manifest = ManifestInfo.Parse(json, manifestPath);
            string root = Path.GetDirectoryName(manifestPath)?.Replace('\\', '/') ?? "Assets/Generated";
            string automationReport = $"{root}/automation_report.json";
            bool automationReportExists = File.Exists(automationReport);
            bool hasActions = manifest.Actions.Count > 0;
            bool hasSheets = true;
            foreach (ActionInfo action in manifest.Actions)
            {
                if (!File.Exists($"{root}/{action.Sheet}".Replace('\\', '/')))
                {
                    hasSheets = false;
                    break;
                }
            }

            List<string> issues = new List<string>();
            if (!hasActions)
            {
                issues.Add("no action sheets in manifest");
            }
            if (!hasSheets)
            {
                issues.Add("one or more action sheets are missing");
            }
            if (!automationReportExists)
            {
                issues.Add("automation report has not been generated");
            }
            if (manifest.RejectedFrameCount > 0)
            {
                issues.Add($"{manifest.RejectedFrameCount} rejected frame(s)");
            }
            if (manifest.QaFailCount > 0)
            {
                issues.Add($"{manifest.QaFailCount} QA failure(s)");
            }

            entries.Add(new PromotionAuditEntry
            {
                ManifestPath = manifestPath,
                AssetName = manifest.AssetName,
                AssetMode = manifest.AssetMode,
                LoraName = manifest.LoraName,
                LoraCheckpoint = manifest.LoraCheckpoint,
                PromotionReady = issues.Count == 0 && manifest.PromotionReady,
                Issues = issues
            });
        }

        WritePromotionAudit(entries);
        AssetDatabase.Refresh();
        int readyCount = CountReady(entries);
        Debug.Log($"Asset Forge promotion audit complete: {readyCount}/{entries.Count} generated asset(s) ready.");
    }

    public static void RebuildManifest(string manifestPath)
    {
        if (!manifestPath.StartsWith("Assets/Generated/", StringComparison.Ordinal) || !File.Exists(manifestPath))
        {
            return;
        }

        List<string> generated = new List<string>();
        List<string> errors = new List<string>();
        string json = File.ReadAllText(manifestPath);
        ManifestInfo manifest = ManifestInfo.Parse(json, manifestPath);
        string root = Path.GetDirectoryName(manifestPath)?.Replace('\\', '/') ?? "Assets/Generated";
        string automationRoot = $"{root}/Automation";
        string clipsRoot = $"{automationRoot}/Clips";
        string controllersRoot = $"{automationRoot}/Controllers";
        string prefabsRoot = $"{automationRoot}/Prefabs";

        EnsureFolder(automationRoot);
        EnsureFolder(clipsRoot);
        EnsureFolder(controllersRoot);
        EnsureFolder(prefabsRoot);
        generated.Add(automationRoot);

        List<AnimationClip> clips = new List<AnimationClip>();
        foreach (ActionInfo action in manifest.Actions)
        {
            string sheetPath = $"{root}/{action.Sheet}".Replace('\\', '/');
            if (!File.Exists(sheetPath))
            {
                errors.Add($"Missing sheet: {sheetPath}");
                continue;
            }

            int columns = Mathf.Max(1, action.FramesPerDirection);
            int rows = action.DirectionMode == "variant" ? 1 : DirectionOrder.Length;
            Vector2 pivot = manifest.Pivot;
            ConfigureSheetImporter(sheetPath, columns, rows, manifest.FrameWidth, manifest.FrameHeight, manifest.PixelsPerUnit, pivot);
            Sprite[] sprites = LoadSprites(sheetPath);
            if (sprites.Length == 0)
            {
                errors.Add($"No sliced sprites loaded from: {sheetPath}");
                continue;
            }

            if (action.DirectionMode == "variant")
            {
                AnimationClip clip = CreateClip(clipsRoot, manifest.AssetName, action.Name, null, action.Fps, sprites);
                clips.Add(clip);
                generated.Add(AssetDatabase.GetAssetPath(clip));
            }
            else
            {
                for (int row = 0; row < DirectionOrder.Length; row++)
                {
                    Sprite[] rowSprites = SliceRow(sprites, row, columns);
                    AnimationClip clip = CreateClip(clipsRoot, manifest.AssetName, action.Name, DirectionOrder[row], action.Fps, rowSprites);
                    clips.Add(clip);
                    generated.Add(AssetDatabase.GetAssetPath(clip));
                }
            }
        }

        if (clips.Count == 0)
        {
            WriteReport(root, manifest, false, generated, errors);
            Debug.LogWarning($"Asset Forge automation found no clips for {manifestPath}.");
            return;
        }

        AnimatorController controller = CreateController(controllersRoot, manifest.AssetName, clips);
        generated.Add(AssetDatabase.GetAssetPath(controller));
        string prefabPath = CreatePrefab(prefabsRoot, manifest.AssetName, clips[0], controller, manifest);
        generated.Add(prefabPath);
        WriteReport(root, manifest, errors.Count == 0, generated, errors);
    }

    private static void ConfigureSheetImporter(string path, int columns, int rows, int frameWidth, int frameHeight, float ppu, Vector2 pivot)
    {
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null)
        {
            return;
        }

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Multiple;
        importer.mipmapEnabled = false;
        importer.filterMode = FilterMode.Point;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.alphaIsTransparency = true;
        importer.isReadable = true;
        importer.spritePixelsPerUnit = ppu;

#pragma warning disable 0618
        SpriteMetaData[] metas = new SpriteMetaData[columns * rows];
        for (int row = 0; row < rows; row++)
        {
            for (int col = 0; col < columns; col++)
            {
                int index = row * columns + col;
                metas[index] = new SpriteMetaData
                {
                    name = $"{Path.GetFileNameWithoutExtension(path)}_{row}_{col}",
                    alignment = (int)SpriteAlignment.Custom,
                    pivot = pivot,
                    rect = new Rect(col * frameWidth, (rows - row - 1) * frameHeight, frameWidth, frameHeight)
                };
            }
        }
        importer.spritesheet = metas;
#pragma warning restore 0618

        importer.SaveAndReimport();
    }

    private static Sprite[] LoadSprites(string path)
    {
        UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetRepresentationsAtPath(path);
        List<Sprite> sprites = new List<Sprite>();
        foreach (UnityEngine.Object asset in assets)
        {
            if (asset is Sprite sprite)
            {
                sprites.Add(sprite);
            }
        }

        sprites.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
        return sprites.ToArray();
    }

    private static Sprite[] SliceRow(Sprite[] sprites, int row, int columns)
    {
        Sprite[] result = new Sprite[columns];
        int start = row * columns;
        for (int i = 0; i < columns; i++)
        {
            result[i] = sprites[Mathf.Clamp(start + i, 0, sprites.Length - 1)];
        }
        return result;
    }

    private static AnimationClip CreateClip(string folder, string assetName, string action, string direction, float fps, Sprite[] sprites)
    {
        string suffix = string.IsNullOrEmpty(direction) ? action : $"{action}_{direction}";
        string path = AssetDatabase.GenerateUniqueAssetPath($"{folder}/{assetName}_{suffix}.anim");
        AnimationClip clip = new AnimationClip
        {
            frameRate = Mathf.Max(1f, fps)
        };

        ObjectReferenceKeyframe[] keyframes = new ObjectReferenceKeyframe[sprites.Length];
        float frameDuration = 1f / Mathf.Max(1f, fps);
        for (int i = 0; i < sprites.Length; i++)
        {
            keyframes[i] = new ObjectReferenceKeyframe
            {
                time = i * frameDuration,
                value = sprites[i]
            };
        }

        EditorCurveBinding binding = new EditorCurveBinding
        {
            path = "",
            type = typeof(SpriteRenderer),
            propertyName = "m_Sprite"
        };
        AnimationUtility.SetObjectReferenceCurve(clip, binding, keyframes);

        AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = !action.Contains("death", StringComparison.OrdinalIgnoreCase);
        AnimationUtility.SetAnimationClipSettings(clip, settings);

        AssetDatabase.CreateAsset(clip, path);
        return clip;
    }

    private static AnimatorController CreateController(string folder, string assetName, IReadOnlyList<AnimationClip> clips)
    {
        string path = AssetDatabase.GenerateUniqueAssetPath($"{folder}/{assetName}.controller");
        AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(path);
        AnimatorStateMachine machine = controller.layers[0].stateMachine;
        foreach (AnimationClip clip in clips)
        {
            AnimatorState state = machine.AddState(clip.name);
            state.motion = clip;
            if (machine.defaultState == null)
            {
                machine.defaultState = state;
            }
        }

        return controller;
    }

    private static string CreatePrefab(string folder, string assetName, AnimationClip firstClip, RuntimeAnimatorController controller, ManifestInfo manifest)
    {
        string path = AssetDatabase.GenerateUniqueAssetPath($"{folder}/{assetName}.prefab");
        GameObject root = new GameObject(assetName);
        SpriteRenderer renderer = root.AddComponent<SpriteRenderer>();
        renderer.spriteSortPoint = SpriteSortPoint.Pivot;
        renderer.sprite = FirstSprite(firstClip);

        Animator animator = root.AddComponent<Animator>();
        animator.runtimeAnimatorController = controller;

        AssetForgeGeneratedAsset metadata = root.AddComponent<AssetForgeGeneratedAsset>();
        metadata.assetName = manifest.AssetName;
        metadata.assetMode = manifest.AssetMode;
        metadata.productionPreset = manifest.ProductionPreset;
        metadata.loraName = manifest.LoraName;
        metadata.loraCheckpoint = manifest.LoraCheckpoint;
        metadata.loraStrength = manifest.LoraStrength;
        metadata.acceptedFrameCount = manifest.AcceptedFrameCount;
        metadata.rejectedFrameCount = manifest.RejectedFrameCount;
        metadata.qaWarnCount = manifest.QaWarnCount;
        metadata.qaFailCount = manifest.QaFailCount;
        metadata.manifestPath = manifest.ManifestPath;

        if (manifest.AssetMode == "character")
        {
            root.AddComponent<Rigidbody2D>().gravityScale = 0f;
            root.AddComponent<IsoPlayerController>();
        }
        else if (manifest.AssetMode == "mob")
        {
            root.AddComponent<Rigidbody2D>().gravityScale = 0f;
        }
        else if (manifest.AssetMode == "tile")
        {
            root.name = $"{assetName}_TileDraft";
        }

        PrefabUtility.SaveAsPrefabAsset(root, path);
        UnityEngine.Object.DestroyImmediate(root);
        return path;
    }

    private static void WriteReport(string root, ManifestInfo manifest, bool success, IReadOnlyList<string> generated, IReadOnlyList<string> errors)
    {
        string reportPath = $"{root}/automation_report.json";
        string json = "{\n"
            + $"  \"assetName\": \"{Escape(manifest.AssetName)}\",\n"
            + $"  \"assetMode\": \"{Escape(manifest.AssetMode)}\",\n"
            + $"  \"success\": {success.ToString().ToLowerInvariant()},\n"
            + $"  \"promotionReady\": {manifest.PromotionReady.ToString().ToLowerInvariant()},\n"
            + $"  \"productionPreset\": \"{Escape(manifest.ProductionPreset)}\",\n"
            + $"  \"loraName\": \"{Escape(manifest.LoraName)}\",\n"
            + $"  \"loraCheckpoint\": \"{Escape(manifest.LoraCheckpoint)}\",\n"
            + $"  \"loraStrength\": {manifest.LoraStrength.ToString(System.Globalization.CultureInfo.InvariantCulture)},\n"
            + $"  \"acceptedFrameCount\": {manifest.AcceptedFrameCount},\n"
            + $"  \"rejectedFrameCount\": {manifest.RejectedFrameCount},\n"
            + $"  \"qaWarnCount\": {manifest.QaWarnCount},\n"
            + $"  \"qaFailCount\": {manifest.QaFailCount},\n"
            + $"  \"generatedAt\": \"{DateTime.UtcNow:O}\",\n"
            + $"  \"generated\": [{JoinJsonStrings(generated)}],\n"
            + $"  \"errors\": [{JoinJsonStrings(errors)}]\n"
            + "}\n";
        File.WriteAllText(reportPath, json);
        AssetDatabase.ImportAsset(reportPath);
    }

    private static void WritePromotionAudit(IReadOnlyList<PromotionAuditEntry> entries)
    {
        EnsureFolder("Assets/Generated");
        string path = "Assets/Generated/asset_forge_promotion_audit.json";
        List<string> body = new List<string>();
        foreach (PromotionAuditEntry entry in entries)
        {
            body.Add("    {\n"
                + $"      \"assetName\": \"{Escape(entry.AssetName)}\",\n"
                + $"      \"assetMode\": \"{Escape(entry.AssetMode)}\",\n"
                + $"      \"manifestPath\": \"{Escape(entry.ManifestPath)}\",\n"
                + $"      \"loraName\": \"{Escape(entry.LoraName)}\",\n"
                + $"      \"loraCheckpoint\": \"{Escape(entry.LoraCheckpoint)}\",\n"
                + $"      \"promotionReady\": {entry.PromotionReady.ToString().ToLowerInvariant()},\n"
                + $"      \"issues\": [{JoinJsonStrings(entry.Issues)}]\n"
                + "    }");
        }

        string json = "{\n"
            + $"  \"generatedAt\": \"{DateTime.UtcNow:O}\",\n"
            + $"  \"total\": {entries.Count},\n"
            + $"  \"ready\": {CountReady(entries)},\n"
            + $"  \"blocked\": {entries.Count - CountReady(entries)},\n"
            + $"  \"entries\": [\n{string.Join(",\n", body)}\n  ]\n"
            + "}\n";
        File.WriteAllText(path, json);
        AssetDatabase.ImportAsset(path);
    }

    private static int CountReady(IReadOnlyList<PromotionAuditEntry> entries)
    {
        int count = 0;
        foreach (PromotionAuditEntry entry in entries)
        {
            if (entry.PromotionReady)
            {
                count++;
            }
        }

        return count;
    }

    private static string JoinJsonStrings(IReadOnlyList<string> values)
    {
        List<string> escaped = new List<string>();
        foreach (string value in values)
        {
            escaped.Add($"\"{Escape(value)}\"");
        }
        return string.Join(", ", escaped);
    }

    private static string Escape(string value)
    {
        return (value ?? string.Empty).Replace("\\", "\\\\").Replace("\"", "\\\"");
    }

    private static Sprite FirstSprite(AnimationClip clip)
    {
        EditorCurveBinding[] bindings = AnimationUtility.GetObjectReferenceCurveBindings(clip);
        if (bindings.Length == 0)
        {
            return null;
        }

        ObjectReferenceKeyframe[] keys = AnimationUtility.GetObjectReferenceCurve(clip, bindings[0]);
        return keys.Length > 0 ? keys[0].value as Sprite : null;
    }

    private static void EnsureFolder(string path)
    {
        string[] parts = path.Split('/');
        string current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = $"{current}/{parts[i]}";
            if (!AssetDatabase.IsValidFolder(next))
            {
                AssetDatabase.CreateFolder(current, parts[i]);
            }
            current = next;
        }
    }

    private sealed class ManifestInfo
    {
        public string AssetName = "AssetForgeAsset";
        public string AssetMode = "character";
        public string Category = "Characters/Player";
        public int FrameWidth = 256;
        public int FrameHeight = 256;
        public float PixelsPerUnit = CharacterPixelsPerUnit;
        public Vector2 Pivot = new Vector2(0.5f, 0.046875f);
        public string ManifestPath = string.Empty;
        public string ProductionPreset = string.Empty;
        public string LoraName = string.Empty;
        public string LoraCheckpoint = string.Empty;
        public float LoraStrength = 0f;
        public int AcceptedFrameCount = 0;
        public int RejectedFrameCount = 0;
        public int QaWarnCount = 0;
        public int QaFailCount = 0;
        public readonly List<ActionInfo> Actions = new List<ActionInfo>();
        public bool PromotionReady => RejectedFrameCount == 0 && QaFailCount == 0;

        public static ManifestInfo Parse(string json, string manifestPath)
        {
            ManifestInfo info = new ManifestInfo
            {
                AssetName = MatchString(json, "assetName", Path.GetFileName(Path.GetDirectoryName(manifestPath))),
                AssetMode = MatchString(json, "assetMode", "character"),
                Category = MatchString(json, "category", "Characters/Player"),
                FrameWidth = MatchInt(json, "frameWidth", 256),
                FrameHeight = MatchInt(json, "frameHeight", 256),
                PixelsPerUnit = MatchFloat(json, "pixelsPerUnit", CharacterPixelsPerUnit),
                ManifestPath = manifestPath,
                ProductionPreset = MatchNestedString(json, "productionPreset", "id", string.Empty),
                LoraName = MatchNestedString(json, "lora", "name", string.Empty),
                LoraCheckpoint = MatchNestedString(json, "lora", "checkpoint", string.Empty),
                LoraStrength = MatchNestedFloat(json, "lora", "strength", 0f)
            };

            info.AcceptedFrameCount = CountArrayObjects(json, "acceptedFrames");
            info.RejectedFrameCount = CountArrayObjects(json, "rejectedFrames");
            info.QaWarnCount = CountStatus(json, "qaStatus", "warn");
            info.QaFailCount = CountStatus(json, "qaStatus", "fail");

            float pivotX = MatchFloat(json, "x", 128f) / Mathf.Max(1, info.FrameWidth);
            float pivotY = MatchFloat(json, "y", 244f) / Mathf.Max(1, info.FrameHeight);
            info.Pivot = new Vector2(Mathf.Clamp01(pivotX), Mathf.Clamp01(1f - pivotY));
            if (info.Category.Contains("Tiles", StringComparison.Ordinal))
            {
                info.PixelsPerUnit = TilePixelsPerUnit;
            }

            Regex actionRegex = new Regex("\"(?<name>[^\"]+)\"\\s*:\\s*\\{(?<body>[^{}]*?\"sheet\"\\s*:\\s*\"(?<sheet>[^\"]+)\"[^{}]*?)\\}", RegexOptions.Singleline);
            foreach (Match match in actionRegex.Matches(json))
            {
                string body = match.Groups["body"].Value;
                string sheet = match.Groups["sheet"].Value;
                if (!sheet.StartsWith("actions/", StringComparison.Ordinal))
                {
                    continue;
                }

                info.Actions.Add(new ActionInfo
                {
                    Name = match.Groups["name"].Value,
                    Sheet = sheet,
                    Fps = MatchFloat(body, "fps", 8f),
                    FramesPerDirection = MatchInt(body, "framesPerDirection", MatchInt(json, "columns", 4)),
                    DirectionMode = MatchString(body, "directionMode", info.AssetMode == "character" || info.AssetMode == "mob" ? "8-direction" : "variant")
                });
            }

            return info;
        }

        private static string MatchString(string json, string key, string fallback)
        {
            Match match = Regex.Match(json, $"\"{Regex.Escape(key)}\"\\s*:\\s*\"(?<value>[^\"]*)\"");
            return match.Success ? match.Groups["value"].Value : fallback;
        }

        private static int MatchInt(string json, string key, int fallback)
        {
            Match match = Regex.Match(json, $"\"{Regex.Escape(key)}\"\\s*:\\s*(?<value>-?\\d+)");
            return match.Success && int.TryParse(match.Groups["value"].Value, out int value) ? value : fallback;
        }

        private static float MatchFloat(string json, string key, float fallback)
        {
            Match match = Regex.Match(json, $"\"{Regex.Escape(key)}\"\\s*:\\s*(?<value>-?\\d+(\\.\\d+)?)");
            return match.Success && float.TryParse(match.Groups["value"].Value, out float value) ? value : fallback;
        }

        private static string MatchNestedString(string json, string objectKey, string key, string fallback)
        {
            Match objectMatch = Regex.Match(json, $"\"{Regex.Escape(objectKey)}\"\\s*:\\s*\\{{(?<body>.*?)\\}}", RegexOptions.Singleline);
            return objectMatch.Success ? MatchString(objectMatch.Groups["body"].Value, key, fallback) : fallback;
        }

        private static float MatchNestedFloat(string json, string objectKey, string key, float fallback)
        {
            Match objectMatch = Regex.Match(json, $"\"{Regex.Escape(objectKey)}\"\\s*:\\s*\\{{(?<body>.*?)\\}}", RegexOptions.Singleline);
            return objectMatch.Success ? MatchFloat(objectMatch.Groups["body"].Value, key, fallback) : fallback;
        }

        private static int CountStatus(string json, string key, string value)
        {
            return Regex.Matches(json, $"\"{Regex.Escape(key)}\"\\s*:\\s*\"{Regex.Escape(value)}\"").Count;
        }

        private static int CountArrayObjects(string json, string key)
        {
            Match match = Regex.Match(json, $"\"{Regex.Escape(key)}\"\\s*:\\s*\\[(?<body>.*?)\\]", RegexOptions.Singleline);
            if (!match.Success)
            {
                return 0;
            }

            return Regex.Matches(match.Groups["body"].Value, "\\{").Count;
        }
    }

    private sealed class ActionInfo
    {
        public string Name;
        public string Sheet;
        public int FramesPerDirection = 4;
        public float Fps = 8f;
        public string DirectionMode = "8-direction";
    }

    private sealed class PromotionAuditEntry
    {
        public string ManifestPath;
        public string AssetName;
        public string AssetMode;
        public string LoraName;
        public string LoraCheckpoint;
        public bool PromotionReady;
        public List<string> Issues;
    }
}
