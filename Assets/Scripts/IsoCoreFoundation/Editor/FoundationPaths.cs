using System.IO;
using UnityEngine;

namespace IsoCore.Foundation.EditorTools
{
    /// <summary>Shared paths for the foundation editor tools.</summary>
    public static class FoundationPaths
    {
        public const string ScenePath = "Assets/Scenes/IsoCoreFoundation.unity";

        public static string ProjectRoot => Directory.GetParent(Application.dataPath).FullName;
        public static string DocsDir => Path.Combine(ProjectRoot, "Docs", "IsoCoreFoundation");
        public static string ReferenceInventoryJson => Path.Combine(DocsDir, "iso_core_reference_inventory.json");

        public static void EnsureDocsDir()
        {
            if (!Directory.Exists(DocsDir)) Directory.CreateDirectory(DocsDir);
        }
    }
}
