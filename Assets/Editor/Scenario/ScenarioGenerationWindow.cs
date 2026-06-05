using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Main editor window for designing prompts and triggering Scenario generations.
///
/// Workflow:
///   1. Pick a category (Character Base, Hair, Clothes, Prop, Tile)
///   2. Pick a sub-type (Male, Female, Plain... or Oak Tree, Stone Rock...)
///   3. Edit the prompt + negative prompt + size
///   4. Click "Generate" — submits to Scenario
///   5. Polls until complete, downloads images to Assets/Generated/_Review/
///   6. Approve / regenerate / file into final folder
///
/// Open via:  Tools > LIT-ISO > AI Generation > Generation Window
/// </summary>
public class ScenarioGenerationWindow : EditorWindow
{
    // -------------------------------------------------------------------------
    // State
    // -------------------------------------------------------------------------

    private enum AssetCategory
    {
        CharacterBase,
        CharacterHair,
        CharacterFace,
        CharacterClothes,
        CharacterArmor,
        Prop,
        Tile,
        Custom
    }

    private AssetCategory category = AssetCategory.CharacterBase;
    private string variantName = "Male_Adventurer";
    private string biomeFolder = "";        // optional sub-folder (e.g. "Plains")
    private string prompt = "";
    private string negativePrompt = "";
    private int width = 1024;
    private int height = 1024;
    private int numSamples = 4;
    private bool runPixal3DAfter = false;

    private string statusText = "Idle";
    private string currentInferenceId = "";
    private List<ScenarioImage> lastResults = new List<ScenarioImage>();
    private List<Texture2D> previewTextures = new List<Texture2D>();

    private List<ScenarioModelSummary> discoveredModels = new List<ScenarioModelSummary>();
    private Vector2 modelScroll;
    private Vector2 mainScroll;

    // -------------------------------------------------------------------------
    // Menu
    // -------------------------------------------------------------------------

    [MenuItem("Tools/LIT-ISO/AI Generation/Generation Window", false, 201)]
    public static void ShowWindow()
    {
        ScenarioGenerationWindow win = GetWindow<ScenarioGenerationWindow>("Scenario Generation");
        win.minSize = new Vector2(720, 640);
        win.Show();
    }

    // -------------------------------------------------------------------------
    // GUI
    // -------------------------------------------------------------------------

    private void OnGUI()
    {
        if (!ScenarioConfig.IsConfigured)
        {
            EditorGUILayout.HelpBox(
                "Set up your Scenario API key first.",
                MessageType.Warning);
            if (GUILayout.Button("Open Configuration"))
            {
                ScenarioConfig.OpenSetupWindow();
            }
            return;
        }

        mainScroll = EditorGUILayout.BeginScrollView(mainScroll);

        EditorGUILayout.LabelField("1. Asset Category", EditorStyles.boldLabel);
        category = (AssetCategory)EditorGUILayout.EnumPopup("Category", category);
        variantName = EditorGUILayout.TextField("Variant Name", variantName);
        biomeFolder = EditorGUILayout.TextField("Biome / Sub-folder", biomeFolder);

        EditorGUILayout.HelpBox("Output: " + ResolveOutputFolder(), MessageType.None);

        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("2. Prompt Design", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("Prompt");
        prompt = EditorGUILayout.TextArea(prompt, GUILayout.MinHeight(80));

        EditorGUILayout.LabelField("Negative Prompt");
        negativePrompt = EditorGUILayout.TextArea(negativePrompt, GUILayout.MinHeight(40));

        if (GUILayout.Button("📋 Load Suggested Prompt for " + category, GUILayout.Height(22)))
        {
            LoadSuggestedPrompt();
        }

        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("3. Generation Settings", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();
        width = EditorGUILayout.IntField("Width", width);
        height = EditorGUILayout.IntField("Height", height);
        EditorGUILayout.EndHorizontal();

        numSamples = EditorGUILayout.IntSlider("Number of Samples", numSamples, 1, 8);

        runPixal3DAfter = EditorGUILayout.ToggleLeft(
            "Run Pixal3D after generation (convert to 3D model)",
            runPixal3DAfter);

        EditorGUILayout.Space(12);
        EditorGUILayout.LabelField("4. Execute", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();
        GUI.enabled = !string.IsNullOrWhiteSpace(prompt)
                      && !string.IsNullOrWhiteSpace(ScenarioConfig.TextToImageModelId);
        if (GUILayout.Button("🚀 Generate", GUILayout.Height(36)))
        {
            StartGeneration();
        }
        GUI.enabled = true;

        if (GUILayout.Button("Refresh Models", GUILayout.Height(36), GUILayout.Width(140)))
        {
            RefreshModels();
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.HelpBox(statusText, MessageType.Info);

        // ---------------- Discovered models ----------------
        if (discoveredModels.Count > 0)
        {
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("📚 Your Scenario Models", EditorStyles.boldLabel);
            modelScroll = EditorGUILayout.BeginScrollView(modelScroll, GUILayout.MaxHeight(160));
            foreach (ScenarioModelSummary m in discoveredModels)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField($"• {m.name}", GUILayout.Width(220));
                EditorGUILayout.LabelField(m.id, GUILayout.Width(280));
                EditorGUILayout.LabelField(m.type, GUILayout.Width(80));
                if (GUILayout.Button("Use as text-to-image", GUILayout.Width(160)))
                {
                    ScenarioConfig.TextToImageModelId = m.id;
                    Debug.Log($"[Scenario] Set text-to-image model to {m.id}");
                }
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndScrollView();
        }

        // ---------------- Preview results ----------------
        if (previewTextures.Count > 0)
        {
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("5. Preview Results", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            for (int i = 0; i < previewTextures.Count; i++)
            {
                if (previewTextures[i] == null) continue;
                GUILayout.Box(previewTextures[i], GUILayout.Width(180), GUILayout.Height(180));
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("✅ Approve to Final Folder", GUILayout.Height(28)))
            {
                MoveReviewToFinalFolder();
            }
            if (GUILayout.Button("🗑 Discard", GUILayout.Height(28)))
            {
                DiscardReviewFiles();
            }
            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.EndScrollView();
    }

    // -------------------------------------------------------------------------
    // Prompt suggestions per category
    // -------------------------------------------------------------------------

    private void LoadSuggestedPrompt()
    {
        switch (category)
        {
            case AssetCategory.CharacterBase:
                prompt =
                    "Isometric RPG character full body, T-pose, neutral expression, " +
                    "centered on transparent background, clean line art, soft cel shading, " +
                    "consistent silhouette, sharp pixel-perfect edges, 4-direction reference sheet, " +
                    "hand-painted texture style, fantasy adventurer, neutral pose, 3/4 view, " +
                    "studio lighting from upper-left.";
                negativePrompt = "blurry, multiple characters, watermark, text, logo, low quality, " +
                                 "background, scene, shadows on ground, dramatic pose, weapon held, " +
                                 "armor, hat, hair (will be added as layer)";
                break;

            case AssetCategory.CharacterHair:
                prompt =
                    "Isolated fantasy hairstyle on transparent background, isometric 3/4 view, " +
                    "hand-painted, clean silhouette, no face visible, cel shaded, " +
                    "soft highlights, suitable for layered character sprite system.";
                negativePrompt = "face, body, character, multiple hairstyles, watermark, blurry, " +
                                 "background, shadow.";
                break;

            case AssetCategory.CharacterFace:
                prompt =
                    "Isometric character face only, 3/4 view, transparent background, " +
                    "cel-shaded, suitable for layered sprite — eyes, nose, mouth, no hair, " +
                    "no shoulders, neutral expression.";
                negativePrompt = "body, hair, hat, multiple faces, watermark, blurry.";
                break;

            case AssetCategory.CharacterClothes:
                prompt =
                    "Isometric fantasy clothing on invisible body, T-pose silhouette, " +
                    "transparent background, hand-painted, cel-shaded, no character visible, " +
                    "consistent body proportions for sprite layering.";
                negativePrompt = "character body, face, hair, hands, feet, watermark.";
                break;

            case AssetCategory.CharacterArmor:
                prompt =
                    "Isometric fantasy armor plate set, isolated on transparent background, " +
                    "T-pose configuration, hand-painted metallic shading, sharp silhouette, " +
                    "designed to overlay character sprite.";
                negativePrompt = "character, face, hair, scene, watermark.";
                break;

            case AssetCategory.Prop:
                prompt =
                    "Isometric fantasy prop on transparent background, hand-painted, " +
                    "cel-shaded, sharp silhouette, designed for top-down isometric RPG, " +
                    "single object centered, soft drop-shadow at base.";
                negativePrompt = "scene, character, multiple objects, watermark.";
                break;

            case AssetCategory.Tile:
                prompt =
                    "Seamless isometric tile, top-down 2:1 ratio diamond shape, hand-painted, " +
                    "cel-shaded, edges tile perfectly with neighbors, single material per tile, " +
                    "soft ambient lighting.";
                negativePrompt = "character, prop, watermark, lighting variation, " +
                                 "shadows extending past tile edges.";
                break;
        }
    }

    // -------------------------------------------------------------------------
    // Folder resolution
    // -------------------------------------------------------------------------

    private string ResolveOutputFolder()
    {
        string root = ScenarioConfig.OutputRoot;
        string typeFolder = category switch
        {
            AssetCategory.CharacterBase    => "Characters/Bases",
            AssetCategory.CharacterHair    => "Characters/Hair",
            AssetCategory.CharacterFace    => "Characters/Faces",
            AssetCategory.CharacterClothes => "Characters/Clothes",
            AssetCategory.CharacterArmor   => "Characters/Armor",
            AssetCategory.Prop             => "Props",
            AssetCategory.Tile             => "Tiles",
            _ => "Custom"
        };
        string biome = string.IsNullOrEmpty(biomeFolder) ? "" : "/" + biomeFolder;
        string variant = string.IsNullOrEmpty(variantName) ? "" : "/" + variantName;
        return $"{root}/{typeFolder}{biome}{variant}";
    }

    private string GetReviewFolder()
    {
        string root = ScenarioConfig.OutputRoot;
        return $"{root}/_Review/{System.DateTime.Now:yyyy-MM-dd_HH-mm-ss}_{variantName}";
    }

    // -------------------------------------------------------------------------
    // Generation flow
    // -------------------------------------------------------------------------

    private void StartGeneration()
    {
        previewTextures.Clear();
        lastResults.Clear();
        statusText = "🚀 Submitting prompt to Scenario...";
        Repaint();

        ScenarioApiClient.GenerateTextToImage(
            ScenarioConfig.TextToImageModelId,
            prompt,
            negativePrompt,
            width,
            height,
            numSamples,
            onInferenceCreated: id =>
            {
                currentInferenceId = id;
                statusText = $"⏳ Polling inference {id} (this can take 30-90s)...";
                Repaint();
                PollAndDownload(id);
            },
            onError: msg =>
            {
                statusText = "❌ " + msg;
                Debug.LogError("[Scenario] " + msg);
                Repaint();
            });
    }

    private void PollAndDownload(string inferenceId)
    {
        ScenarioApiClient.PollInference(
            ScenarioConfig.TextToImageModelId,
            inferenceId,
            onComplete: result =>
            {
                statusText = $"✅ Generation complete — {result.images.Count} image(s). Downloading...";
                lastResults = result.images;
                Repaint();
                DownloadResults(result.images);
            },
            onError: msg =>
            {
                statusText = "❌ " + msg;
                Debug.LogError("[Scenario] " + msg);
                Repaint();
            });
    }

    private void DownloadResults(List<ScenarioImage> images)
    {
        string folder = GetReviewFolder();
        Directory.CreateDirectory(folder);

        int remaining = images.Count;
        for (int i = 0; i < images.Count; i++)
        {
            int index = i;
            string localPath = $"{folder}/sample_{index:D2}.png";
            ScenarioApiClient.DownloadFile(images[i].url, localPath,
                onComplete: () =>
                {
                    Debug.Log($"[Scenario] Saved: {localPath}");
                    LoadPreview(localPath);
                    remaining--;
                    if (remaining == 0)
                    {
                        statusText = $"✅ All {images.Count} images downloaded to {folder}";
                        AssetDatabase.Refresh();
                        Repaint();
                    }
                },
                onError: msg =>
                {
                    Debug.LogError($"[Scenario] Download failed: {msg}");
                    remaining--;
                });
        }
    }

    private void LoadPreview(string path)
    {
        if (!File.Exists(path)) return;
        byte[] bytes = File.ReadAllBytes(path);
        Texture2D tex = new Texture2D(2, 2);
        if (tex.LoadImage(bytes))
        {
            previewTextures.Add(tex);
            Repaint();
        }
    }

    // -------------------------------------------------------------------------
    // Approve / discard
    // -------------------------------------------------------------------------

    private void MoveReviewToFinalFolder()
    {
        string finalFolder = ResolveOutputFolder();
        Directory.CreateDirectory(finalFolder);

        // The most recent review folder is where the files live.
        // For simplicity we move all files from any _Review folder containing variantName.
        string reviewRoot = $"{ScenarioConfig.OutputRoot}/_Review";
        if (!Directory.Exists(reviewRoot))
        {
            EditorUtility.DisplayDialog("No Review Folder", "Nothing to move.", "OK");
            return;
        }

        int moved = 0;
        foreach (string dir in Directory.GetDirectories(reviewRoot))
        {
            if (!dir.Contains(variantName)) continue;
            foreach (string file in Directory.GetFiles(dir, "*.png"))
            {
                string dest = $"{finalFolder}/{Path.GetFileName(file)}";
                File.Move(file, dest);
                if (File.Exists(file + ".meta")) File.Delete(file + ".meta");
                moved++;
            }
            // Clean up empty directory
            try { Directory.Delete(dir); } catch { /* ignore */ }
        }

        AssetDatabase.Refresh();
        statusText = $"✅ Moved {moved} file(s) to {finalFolder}";
        previewTextures.Clear();
    }

    private void DiscardReviewFiles()
    {
        string reviewRoot = $"{ScenarioConfig.OutputRoot}/_Review";
        if (!Directory.Exists(reviewRoot)) return;

        int deleted = 0;
        foreach (string dir in Directory.GetDirectories(reviewRoot))
        {
            if (!dir.Contains(variantName)) continue;
            foreach (string file in Directory.GetFiles(dir, "*"))
            {
                File.Delete(file);
                deleted++;
            }
            try { Directory.Delete(dir); } catch { /* ignore */ }
        }
        AssetDatabase.Refresh();
        statusText = $"🗑 Deleted {deleted} file(s)";
        previewTextures.Clear();
    }

    // -------------------------------------------------------------------------
    // Refresh models from API
    // -------------------------------------------------------------------------

    private void RefreshModels()
    {
        statusText = "🔍 Fetching your models from Scenario...";
        Repaint();
        ScenarioApiClient.ListModels(
            onComplete: models =>
            {
                discoveredModels = models;
                statusText = $"✅ Found {models.Count} model(s). Click 'Use as text-to-image' to select one.";
                Repaint();
            },
            onError: msg =>
            {
                statusText = "❌ " + msg;
                Debug.LogError("[Scenario] " + msg);
                Repaint();
            });
    }
}
