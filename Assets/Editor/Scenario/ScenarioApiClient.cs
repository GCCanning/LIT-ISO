using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// HTTP client for the Scenario API.
///
/// All methods are async / coroutine-friendly via EditorApplication.update polling.
/// Uses UnityWebRequest so it works inside the editor without external dependencies.
///
/// Currently supported endpoints:
///   - TestConnection()        — Verify auth works
///   - ListModels()            — Browse available models
///   - GenerateTextToImage()   — Submit a prompt to a model
///   - UploadAsset()           — Upload PNG to get an assetId for Pixal3D
///   - GeneratePixal3D()       — Image-to-3D conversion
///   - PollInference()         — Wait for a job to finish
///   - DownloadFile()          — Save result to disk
/// </summary>
public static class ScenarioApiClient
{
    // -------------------------------------------------------------------------
    // Test connection
    // -------------------------------------------------------------------------

    public static void TestConnection()
    {
        if (!ScenarioConfig.IsConfigured)
        {
            EditorUtility.DisplayDialog("No API Key",
                "Set your API key + secret first.",
                "OK");
            return;
        }

        string url = ScenarioConfig.GetBaseUrl() + "/models?pageSize=1";
        UnityWebRequest req = UnityWebRequest.Get(url);
        req.SetRequestHeader("Authorization", ScenarioConfig.GetBasicAuthHeader());
        req.SetRequestHeader("Accept", "application/json");

        UnityWebRequestAsyncOperation op = req.SendWebRequest();
        op.completed += _ =>
        {
            if (req.result == UnityWebRequest.Result.Success)
            {
                Debug.Log($"[Scenario] ✅ Connection OK. Status: {req.responseCode}");
                EditorUtility.DisplayDialog("Connection OK",
                    "Successfully authenticated with the Scenario API.",
                    "Great");
            }
            else
            {
                Debug.LogError($"[Scenario] ❌ Connection failed: {req.error}\nResponse: {req.downloadHandler.text}");
                EditorUtility.DisplayDialog("Connection Failed",
                    $"Error: {req.error}\n\n" +
                    $"Response: {req.downloadHandler.text}\n\n" +
                    "Verify your API key and secret are correct.",
                    "OK");
            }
            req.Dispose();
        };
    }

    // -------------------------------------------------------------------------
    // List available models
    // -------------------------------------------------------------------------

    /// <summary>
    /// Fetch list of available models from the user's Scenario account.
    /// Callback receives the parsed list (id + name).
    /// </summary>
    public static void ListModels(Action<List<ScenarioModelSummary>> onComplete, Action<string> onError = null)
    {
        if (!RequireConfigured(onError)) return;

        string url = ScenarioConfig.GetBaseUrl() + "/models?pageSize=100";
        UnityWebRequest req = UnityWebRequest.Get(url);
        req.SetRequestHeader("Authorization", ScenarioConfig.GetBasicAuthHeader());
        req.SetRequestHeader("Accept", "application/json");

        UnityWebRequestAsyncOperation op = req.SendWebRequest();
        op.completed += _ =>
        {
            try
            {
                if (req.result != UnityWebRequest.Result.Success)
                {
                    onError?.Invoke($"List models failed: {req.error}\n{req.downloadHandler.text}");
                    return;
                }
                string json = req.downloadHandler.text;
                ScenarioModelListResponse parsed = JsonUtility.FromJson<ScenarioModelListResponse>(WrapModelListJson(json));
                onComplete?.Invoke(parsed?.models ?? new List<ScenarioModelSummary>());
            }
            finally
            {
                req.Dispose();
            }
        };
    }

    // -------------------------------------------------------------------------
    // Generate text-to-image
    // -------------------------------------------------------------------------

    /// <summary>
    /// Submit a text prompt to a model and run an inference.
    /// Returns the inference ID to poll for completion.
    /// </summary>
    public static void GenerateTextToImage(
        string modelId,
        string prompt,
        string negativePrompt,
        int width,
        int height,
        int numSamples,
        Action<string> onInferenceCreated,
        Action<string> onError = null)
    {
        if (!RequireConfigured(onError)) return;
        if (string.IsNullOrWhiteSpace(modelId))
        {
            onError?.Invoke("Text-to-image model ID is empty. Set it in Configure Scenario API.");
            return;
        }

        string url = $"{ScenarioConfig.GetBaseUrl()}/models/{modelId}/inferences";

        // Build payload manually so we have control over the JSON shape.
        // Scenario's text-to-image inference accepts a "parameters" object with
        // prompt, negativePrompt, width, height, numSamples, etc.
        string bodyJson = BuildTextToImageJson(prompt, negativePrompt, width, height, numSamples);

        UnityWebRequest req = new UnityWebRequest(url, "POST");
        byte[] bodyBytes = Encoding.UTF8.GetBytes(bodyJson);
        req.uploadHandler = new UploadHandlerRaw(bodyBytes);
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/json");
        req.SetRequestHeader("Accept", "application/json");
        req.SetRequestHeader("Authorization", ScenarioConfig.GetBasicAuthHeader());

        UnityWebRequestAsyncOperation op = req.SendWebRequest();
        op.completed += _ =>
        {
            try
            {
                if (req.result != UnityWebRequest.Result.Success)
                {
                    onError?.Invoke($"Text-to-image inference failed: {req.error}\n{req.downloadHandler.text}");
                    return;
                }

                string responseJson = req.downloadHandler.text;
                ScenarioInferenceResponse parsed = JsonUtility.FromJson<ScenarioInferenceResponse>(responseJson);
                if (parsed == null || parsed.inference == null || string.IsNullOrEmpty(parsed.inference.id))
                {
                    onError?.Invoke($"Could not parse inference ID from response:\n{responseJson}");
                    return;
                }
                onInferenceCreated?.Invoke(parsed.inference.id);
            }
            finally
            {
                req.Dispose();
            }
        };
    }

    // -------------------------------------------------------------------------
    // Poll an inference until it finishes
    // -------------------------------------------------------------------------

    /// <summary>
    /// Poll an inference repeatedly until it succeeds, fails, or times out.
    /// Returns the result URLs (one per generated image).
    /// </summary>
    public static void PollInference(
        string modelId,
        string inferenceId,
        Action<ScenarioInferenceResult> onComplete,
        Action<string> onError = null,
        float timeoutSec = 300f)
    {
        if (!RequireConfigured(onError)) return;

        float startedAt = (float)EditorApplication.timeSinceStartup;
        float intervalSec = ScenarioConfig.PollIntervalSec;
        float lastPollAt = -1000f;

        EditorApplication.CallbackFunction tick = null;
        tick = () =>
        {
            float now = (float)EditorApplication.timeSinceStartup;
            if (now - startedAt > timeoutSec)
            {
                EditorApplication.update -= tick;
                onError?.Invoke($"Inference {inferenceId} timed out after {timeoutSec}s.");
                return;
            }
            if (now - lastPollAt < intervalSec) return;
            lastPollAt = now;

            string url = $"{ScenarioConfig.GetBaseUrl()}/models/{modelId}/inferences/{inferenceId}";
            UnityWebRequest req = UnityWebRequest.Get(url);
            req.SetRequestHeader("Authorization", ScenarioConfig.GetBasicAuthHeader());
            req.SetRequestHeader("Accept", "application/json");

            UnityWebRequestAsyncOperation op = req.SendWebRequest();
            op.completed += _ =>
            {
                try
                {
                    if (req.result != UnityWebRequest.Result.Success)
                    {
                        EditorApplication.update -= tick;
                        onError?.Invoke($"Polling failed: {req.error}\n{req.downloadHandler.text}");
                        return;
                    }

                    string json = req.downloadHandler.text;
                    ScenarioInferenceResponse parsed = JsonUtility.FromJson<ScenarioInferenceResponse>(json);
                    if (parsed == null || parsed.inference == null)
                    {
                        EditorApplication.update -= tick;
                        onError?.Invoke($"Unexpected response:\n{json}");
                        return;
                    }

                    string status = parsed.inference.status ?? "";
                    if (status == "succeeded")
                    {
                        EditorApplication.update -= tick;
                        ScenarioInferenceResult result = new ScenarioInferenceResult
                        {
                            id = parsed.inference.id,
                            images = parsed.inference.images ?? new List<ScenarioImage>(),
                            rawJson = json
                        };
                        onComplete?.Invoke(result);
                    }
                    else if (status == "failed" || status == "canceled")
                    {
                        EditorApplication.update -= tick;
                        onError?.Invoke($"Inference {status}: {parsed.inference.reason ?? "(no reason)"}");
                    }
                    // Otherwise still running — keep polling
                }
                finally
                {
                    req.Dispose();
                }
            };
        };

        EditorApplication.update += tick;
    }

    // -------------------------------------------------------------------------
    // Upload an image as an asset (for Pixal3D input)
    // -------------------------------------------------------------------------

    public static void UploadAsset(
        byte[] pngBytes,
        string fileName,
        Action<string> onAssetCreated,
        Action<string> onError = null)
    {
        if (!RequireConfigured(onError)) return;

        string url = ScenarioConfig.GetBaseUrl() + "/assets";

        // Scenario asset upload typically uses multipart/form-data with a "file" field
        List<IMultipartFormSection> form = new List<IMultipartFormSection>
        {
            new MultipartFormFileSection("file", pngBytes, fileName, "image/png")
        };

        UnityWebRequest req = UnityWebRequest.Post(url, form);
        req.SetRequestHeader("Authorization", ScenarioConfig.GetBasicAuthHeader());
        req.SetRequestHeader("Accept", "application/json");

        UnityWebRequestAsyncOperation op = req.SendWebRequest();
        op.completed += _ =>
        {
            try
            {
                if (req.result != UnityWebRequest.Result.Success)
                {
                    onError?.Invoke($"Asset upload failed: {req.error}\n{req.downloadHandler.text}");
                    return;
                }
                string json = req.downloadHandler.text;
                ScenarioAssetResponse parsed = JsonUtility.FromJson<ScenarioAssetResponse>(json);
                if (parsed?.asset == null || string.IsNullOrEmpty(parsed.asset.id))
                {
                    onError?.Invoke($"Could not parse assetId from upload response:\n{json}");
                    return;
                }
                onAssetCreated?.Invoke(parsed.asset.id);
            }
            finally
            {
                req.Dispose();
            }
        };
    }

    // -------------------------------------------------------------------------
    // Download a result file to disk
    // -------------------------------------------------------------------------

    public static void DownloadFile(string url, string localPath, Action onComplete, Action<string> onError = null)
    {
        UnityWebRequest req = UnityWebRequest.Get(url);
        req.downloadHandler = new DownloadHandlerFile(localPath);

        UnityWebRequestAsyncOperation op = req.SendWebRequest();
        op.completed += _ =>
        {
            try
            {
                if (req.result != UnityWebRequest.Result.Success)
                {
                    onError?.Invoke($"Download failed ({req.error}): {url}");
                    return;
                }
                onComplete?.Invoke();
            }
            finally
            {
                req.Dispose();
            }
        };
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static bool RequireConfigured(Action<string> onError)
    {
        if (ScenarioConfig.IsConfigured) return true;
        string msg = "Scenario API key not set. Use 'Tools > LIT-ISO > AI Generation > Configure Scenario API'.";
        Debug.LogWarning("[Scenario] " + msg);
        onError?.Invoke(msg);
        return false;
    }

    private static string BuildTextToImageJson(string prompt, string negativePrompt, int width, int height, int numSamples)
    {
        // Sanitize for JSON (escape quotes and backslashes)
        string p = EscapeJson(prompt ?? "");
        string n = EscapeJson(negativePrompt ?? "");

        return "{"
            + "\"parameters\":{"
            + $"\"prompt\":\"{p}\","
            + (string.IsNullOrEmpty(n) ? "" : $"\"negativePrompt\":\"{n}\",")
            + $"\"width\":{width},"
            + $"\"height\":{height},"
            + $"\"numSamples\":{Mathf.Clamp(numSamples, 1, 8)}"
            + "}"
            + "}";
    }

    private static string EscapeJson(string s)
    {
        return s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "");
    }

    /// <summary>
    /// JsonUtility doesn't deserialize top-level arrays. Wrap the response so it parses cleanly.
    /// </summary>
    private static string WrapModelListJson(string raw)
    {
        // Scenario's /models endpoint returns { "models": [...], "pagination": {...} }
        // which JsonUtility already handles. If response shape differs, we may need to adjust.
        return raw;
    }
}

// -------------------------------------------------------------------------
// JSON DTOs (matching Scenario API response shapes)
// -------------------------------------------------------------------------

[Serializable]
public class ScenarioModelListResponse
{
    public List<ScenarioModelSummary> models;
}

[Serializable]
public class ScenarioModelSummary
{
    public string id;
    public string name;
    public string type;
    public string status;
}

[Serializable]
public class ScenarioInferenceResponse
{
    public ScenarioInferenceDetails inference;
}

[Serializable]
public class ScenarioInferenceDetails
{
    public string id;
    public string status;       // "queued", "in-progress", "succeeded", "failed", "canceled"
    public string reason;
    public List<ScenarioImage> images;
}

[Serializable]
public class ScenarioImage
{
    public string id;
    public string url;
    public string seed;
}

[Serializable]
public class ScenarioInferenceResult
{
    public string id;
    public List<ScenarioImage> images;
    public string rawJson;
}

[Serializable]
public class ScenarioAssetResponse
{
    public ScenarioAsset asset;
}

[Serializable]
public class ScenarioAsset
{
    public string id;
    public string url;
}
