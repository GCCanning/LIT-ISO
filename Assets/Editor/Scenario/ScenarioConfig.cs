using UnityEditor;
using UnityEngine;

/// <summary>
/// Per-machine Scenario API credentials and settings, stored in Unity EditorPrefs.
///
/// EditorPrefs keep the API key OUT of project files — it never lands in git,
/// never appears in the Asset database, and is shared across all Unity projects
/// on this machine.
///
/// Setup:
///   Tools > LIT-ISO > AI Generation > Configure Scenario API
///
/// The keys are scoped per-project so different projects can have different keys.
/// </summary>
public static class ScenarioConfig
{
    // -------------------------------------------------------------------------
    // EditorPrefs keys (per-project namespace)
    // -------------------------------------------------------------------------

    private const string KeyPrefix = "LITISO_Scenario_";
    private const string KeyApiKey       = KeyPrefix + "ApiKey";
    private const string KeyApiSecret    = KeyPrefix + "ApiSecret";
    private const string KeyTextToImage  = KeyPrefix + "TextToImageModelId";
    private const string KeyPixal3D      = KeyPrefix + "Pixal3DModelId";
    private const string KeyOutputRoot   = KeyPrefix + "OutputRoot";
    private const string KeyPollInterval = KeyPrefix + "PollIntervalSec";

    // -------------------------------------------------------------------------
    // Public accessors
    // -------------------------------------------------------------------------

    public static string ApiKey
    {
        get => EditorPrefs.GetString(KeyApiKey, "");
        set => EditorPrefs.SetString(KeyApiKey, value ?? "");
    }

    public static string ApiSecret
    {
        get => EditorPrefs.GetString(KeyApiSecret, "");
        set => EditorPrefs.SetString(KeyApiSecret, value ?? "");
    }

    public static string TextToImageModelId
    {
        get => EditorPrefs.GetString(KeyTextToImage, "");
        set => EditorPrefs.SetString(KeyTextToImage, value ?? "");
    }

    public static string Pixal3DModelId
    {
        get => EditorPrefs.GetString(KeyPixal3D, "model_pixal3d");
        set => EditorPrefs.SetString(KeyPixal3D, value ?? "model_pixal3d");
    }

    public static string OutputRoot
    {
        get => EditorPrefs.GetString(KeyOutputRoot, "Assets/Generated");
        set => EditorPrefs.SetString(KeyOutputRoot, value ?? "Assets/Generated");
    }

    public static float PollIntervalSec
    {
        get => EditorPrefs.GetFloat(KeyPollInterval, 4f);
        set => EditorPrefs.SetFloat(KeyPollInterval, Mathf.Max(1f, value));
    }

    public static bool IsConfigured =>
        !string.IsNullOrWhiteSpace(ApiKey) && !string.IsNullOrWhiteSpace(ApiSecret);

    public static string GetBasicAuthHeader()
    {
        if (!IsConfigured) return null;
        string raw = $"{ApiKey}:{ApiSecret}";
        byte[] bytes = System.Text.Encoding.UTF8.GetBytes(raw);
        return "Basic " + System.Convert.ToBase64String(bytes);
    }

    public static string GetBaseUrl() => "https://api.cloud.scenario.com/v1";

    // -------------------------------------------------------------------------
    // Setup dialog
    // -------------------------------------------------------------------------

    [MenuItem("Tools/LIT-ISO/AI Generation/Configure Scenario API", false, 200)]
    public static void OpenSetupWindow()
    {
        ScenarioConfigWindow.ShowWindow();
    }

    public static void ClearAll()
    {
        EditorPrefs.DeleteKey(KeyApiKey);
        EditorPrefs.DeleteKey(KeyApiSecret);
        EditorPrefs.DeleteKey(KeyTextToImage);
        EditorPrefs.DeleteKey(KeyPixal3D);
        EditorPrefs.DeleteKey(KeyOutputRoot);
        EditorPrefs.DeleteKey(KeyPollInterval);
    }
}

/// <summary>
/// Editor window for entering Scenario credentials and settings.
/// </summary>
public class ScenarioConfigWindow : EditorWindow
{
    private string apiKey;
    private string apiSecret;
    private string textToImageModelId;
    private string pixal3DModelId;
    private string outputRoot;
    private float pollIntervalSec;
    private bool showSecret;

    public static void ShowWindow()
    {
        ScenarioConfigWindow window = GetWindow<ScenarioConfigWindow>("Scenario API Config");
        window.minSize = new Vector2(500, 380);
        window.LoadValues();
        window.Show();
    }

    private void LoadValues()
    {
        apiKey            = ScenarioConfig.ApiKey;
        apiSecret         = ScenarioConfig.ApiSecret;
        textToImageModelId = ScenarioConfig.TextToImageModelId;
        pixal3DModelId    = ScenarioConfig.Pixal3DModelId;
        outputRoot        = ScenarioConfig.OutputRoot;
        pollIntervalSec   = ScenarioConfig.PollIntervalSec;
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Scenario API Credentials", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Stored in EditorPrefs (per-machine). Never written to project files. " +
            "Get keys from https://app.scenario.com → API Keys.",
            MessageType.Info);

        EditorGUILayout.Space(8);

        apiKey = EditorGUILayout.TextField("API Key", apiKey);

        EditorGUILayout.BeginHorizontal();
        if (showSecret)
            apiSecret = EditorGUILayout.TextField("API Secret", apiSecret);
        else
            apiSecret = EditorGUILayout.PasswordField("API Secret", apiSecret);
        showSecret = EditorGUILayout.ToggleLeft("Show", showSecret, GUILayout.Width(60));
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(16);
        EditorGUILayout.LabelField("Models", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Find your model IDs in the Scenario web app (URL like /models/model_xxx). " +
            "Use 'Refresh Models' in the generation window to auto-discover your models.",
            MessageType.None);

        textToImageModelId = EditorGUILayout.TextField("Text-to-Image Model ID", textToImageModelId);
        pixal3DModelId     = EditorGUILayout.TextField("Pixal3D Model ID", pixal3DModelId);

        EditorGUILayout.Space(16);
        EditorGUILayout.LabelField("Output", EditorStyles.boldLabel);
        outputRoot = EditorGUILayout.TextField("Output Root Folder", outputRoot);
        pollIntervalSec = EditorGUILayout.Slider("Poll Interval (sec)", pollIntervalSec, 1f, 15f);

        EditorGUILayout.Space(16);

        EditorGUILayout.BeginHorizontal();
        GUI.enabled = !string.IsNullOrWhiteSpace(apiKey) && !string.IsNullOrWhiteSpace(apiSecret);
        if (GUILayout.Button("Save", GUILayout.Height(28)))
        {
            SaveValues();
            EditorUtility.DisplayDialog("Scenario Config Saved", "Your API credentials have been saved to EditorPrefs.", "OK");
        }
        GUI.enabled = true;

        if (GUILayout.Button("Test Connection", GUILayout.Height(28)))
        {
            SaveValues();
            ScenarioApiClient.TestConnection();
        }

        if (GUILayout.Button("Clear All", GUILayout.Height(28)))
        {
            if (EditorUtility.DisplayDialog("Clear All Settings?",
                "This removes your API key + all settings from this machine.",
                "Clear", "Cancel"))
            {
                ScenarioConfig.ClearAll();
                LoadValues();
            }
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(8);
        if (ScenarioConfig.IsConfigured)
        {
            EditorGUILayout.HelpBox("✅ API credentials are set.", MessageType.Info);
        }
        else
        {
            EditorGUILayout.HelpBox("⚠ Add your API key and secret to enable generation.", MessageType.Warning);
        }
    }

    private void SaveValues()
    {
        ScenarioConfig.ApiKey             = apiKey;
        ScenarioConfig.ApiSecret          = apiSecret;
        ScenarioConfig.TextToImageModelId = textToImageModelId;
        ScenarioConfig.Pixal3DModelId     = pixal3DModelId;
        ScenarioConfig.OutputRoot         = outputRoot;
        ScenarioConfig.PollIntervalSec    = pollIntervalSec;
    }
}
