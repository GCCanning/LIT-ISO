using System.Reflection;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Hides the speaker/volume icon gizmos that appear in the Game view
/// for AudioListener and AudioSource components.
///
/// These icons are useful in the Scene view for placement but appear as
/// a distracting icon in the center of the Game view (where the camera's
/// AudioListener lives).
///
/// Runs automatically when Unity starts. Can also be invoked manually:
///   Tools > LIT-ISO > Diagnostics > Hide Audio Gizmos In Game View
/// </summary>
[InitializeOnLoad]
public static class HideAudioGizmos
{
    static HideAudioGizmos()
    {
        // Defer one editor frame so Unity is fully initialized.
        EditorApplication.delayCall += () =>
        {
            DisableGizmos();
        };
    }

    [MenuItem("Tools/LIT-ISO/Diagnostics/Hide Audio Gizmos In Game View", false, 320)]
    public static void HideManually()
    {
        DisableGizmos();
        EditorUtility.DisplayDialog(
            "Audio Gizmos Hidden",
            "The speaker icons for AudioListener and AudioSource are now hidden in the Game view.\n\n" +
            "If they reappear, just run this menu again, or click the 'Gizmos' toggle in the Game view toolbar.",
            "OK");
    }

    private static void DisableGizmos()
    {
        SetGizmoIconEnabled("AudioListener", false);
        SetGizmoIconEnabled("AudioSource", false);
        Debug.Log("[HideAudioGizmos] Disabled speaker icons in Game view for AudioListener and AudioSource.");
    }

    /// <summary>
    /// Toggles the icon-in-Game-view setting for a built-in component via reflection.
    /// Unity exposes this internally via the Annotation system.
    /// </summary>
    private static void SetGizmoIconEnabled(string componentName, bool enabled)
    {
        try
        {
            System.Type annotationUtility = System.Type.GetType("UnityEditor.AnnotationUtility,UnityEditor");
            if (annotationUtility == null) return;

            MethodInfo getAnnotations = annotationUtility.GetMethod(
                "GetAnnotations",
                BindingFlags.Static | BindingFlags.NonPublic);
            if (getAnnotations == null) return;

            MethodInfo setIconEnabled = annotationUtility.GetMethod(
                "SetIconEnabled",
                BindingFlags.Static | BindingFlags.NonPublic);
            if (setIconEnabled == null) return;

            System.Array annotations = (System.Array)getAnnotations.Invoke(null, null);
            foreach (object annotation in annotations)
            {
                System.Type annotationType = annotation.GetType();
                FieldInfo classIdField = annotationType.GetField("classID");
                FieldInfo scriptClassField = annotationType.GetField("scriptClass");
                if (classIdField == null || scriptClassField == null) continue;

                int classId = (int)classIdField.GetValue(annotation);
                string scriptClass = (string)scriptClassField.GetValue(annotation);

                // Built-in components have empty scriptClass and we match on the class name
                // by re-fetching info via the GetType system. Simpler: match by classID for
                // common built-ins.
                // AudioListener classID = 81, AudioSource classID = 82
                bool match = false;
                if (componentName == "AudioListener" && classId == 81) match = true;
                if (componentName == "AudioSource" && classId == 82) match = true;
                if (scriptClass == componentName) match = true;

                if (match)
                {
                    setIconEnabled.Invoke(null, new object[] { classId, scriptClass, enabled ? 1 : 0 });
                }
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning("[HideAudioGizmos] Could not toggle gizmo icons (Unity internal API may have changed): " + ex.Message);
        }
    }
}
