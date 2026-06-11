using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace IsoCore.Foundation.EditorTools
{
    /// <summary>
    /// Builds/refreshes the playable foundation scene from scratch: a single
    /// FoundationBootstrap object + a camera. Idempotent, no manual scene surgery.
    /// </summary>
    public static class FoundationSceneBuilder
    {
        public static string BuildScene(bool showDialog)
        {
            // Non-destructive: let the user save any unsaved scene first.
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return "[ISO-Core] Build cancelled (unsaved changes kept).";

            // Fix #22: destructive guard — rebuilding REPLACES the canonical scene
            // contents, so confirm explicitly (naming the target path) before saving
            // over an existing scene. "Cancel" is deliberately the first (default) button.
            if (!Application.isBatchMode &&
                AssetDatabase.LoadAssetAtPath<SceneAsset>(FoundationPaths.ScenePath) != null)
            {
                bool cancelled = EditorUtility.DisplayDialog(
                    "ISO-Core Foundation — Overwrite Scene?",
                    "This will OVERWRITE the existing scene at:\n\n" +
                    FoundationPaths.ScenePath + "\n\n" +
                    "All current contents of that scene will be lost and rebuilt from " +
                    "scratch (Main Camera + FoundationBootstrap only).",
                    "Cancel", "Overwrite");
                if (cancelled)
                    return $"[ISO-Core] Build cancelled (kept existing scene at {FoundationPaths.ScenePath}).";
            }

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 6f;
            cam.backgroundColor = new Color(0.10f, 0.12f, 0.16f);
            cam.clearFlags = CameraClearFlags.SolidColor;
            camGo.transform.position = new Vector3(0, 0, -10);

            var bootGo = new GameObject("FoundationBootstrap");
            bootGo.AddComponent<FoundationBootstrap>();

            if (!AssetDatabase.IsValidFolder("Assets/Scenes"))
                AssetDatabase.CreateFolder("Assets", "Scenes");

            bool ok = EditorSceneManager.SaveScene(scene, FoundationPaths.ScenePath);
            AssetDatabase.Refresh();

            string log = ok
                ? $"[ISO-Core] Built foundation scene at {FoundationPaths.ScenePath}\n" +
                  "Contains: Main Camera (orthographic) + FoundationBootstrap.\n" +
                  "Press Play to generate the world and run the survival loop."
                : "[ISO-Core] FAILED to save foundation scene.";

            if (showDialog) EditorUtility.DisplayDialog("ISO-Core Foundation", log, "OK");
            Debug.Log(log);
            return log;
        }
    }
}
