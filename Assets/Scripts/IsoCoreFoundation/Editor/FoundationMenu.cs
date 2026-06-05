using UnityEditor;
using UnityEngine;

namespace IsoCore.Foundation.EditorTools
{
    /// <summary>
    /// Repeatable editor workflows under Tools/LIT-ISO/ISO-Core Foundation.
    /// Each [MenuItem] is a thin wrapper over a static core (the reusable
    /// GoldenPath compose pattern from the legacy project).
    /// </summary>
    public static class FoundationMenu
    {
        const string Root = "Tools/LIT-ISO/ISO-Core Foundation/";

        [MenuItem(Root + "Audit Project", priority = 40)]
        public static void AuditProject() => FoundationReports.AuditProject(true);

        [MenuItem(Root + "Inventory ISO-CORE Reference", priority = 41)]
        public static void InventoryReference() => FoundationReports.InventoryReference(true);

        [MenuItem(Root + "Build Foundation Scene", priority = 52)]
        public static void BuildScene() => FoundationSceneBuilder.BuildScene(true);

        [MenuItem(Root + "Generate Content Assets", priority = 53)]
        public static void GenerateContent() => FoundationContentBaker.Bake(true);

        [MenuItem(Root + "Validate Foundation", priority = 54)]
        public static void ValidateFoundation() => FoundationValidator.Validate(true);

        [MenuItem(Root + "Run Golden Path", priority = 65)]
        public static void RunGoldenPath()
        {
            string a = FoundationSceneBuilder.BuildScene(false);
            string b = FoundationValidator.Validate(false);
            EditorUtility.DisplayDialog("ISO-Core Foundation — Golden Path",
                "Build → Validate complete.\n\n" + a + "\n\n" + b, "OK");
        }
    }
}
