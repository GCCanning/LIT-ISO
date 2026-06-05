using System;
using UnityEditor;
using UnityEngine;

public static class AssetForgeBatchRunner
{
    private const string ManifestEnvVar = "ASSET_FORGE_MANIFEST";

    [MenuItem("LIT-ISO/Asset Forge/Rebuild Manifest From Environment")]
    public static void RebuildManifestFromEnvironment()
    {
        string manifestPath = Environment.GetEnvironmentVariable(ManifestEnvVar);
        if (string.IsNullOrWhiteSpace(manifestPath))
        {
            Debug.Log($"{ManifestEnvVar} was not set. Rebuilding all generated Asset Forge manifests.");
            AssetForgeAutomation.RebuildAll();
            return;
        }

        string projectRelativePath = ToProjectRelativePath(manifestPath);
        AssetForgeAutomation.RebuildManifest(projectRelativePath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"Asset Forge rebuilt manifest from {ManifestEnvVar}: {projectRelativePath}");
    }

    private static string ToProjectRelativePath(string path)
    {
        string normalized = path.Replace('\\', '/').Trim();
        string dataPath = Application.dataPath.Replace('\\', '/');
        if (normalized.StartsWith(dataPath + "/", StringComparison.OrdinalIgnoreCase))
        {
            return "Assets/" + normalized.Substring(dataPath.Length + 1);
        }

        return normalized;
    }
}
