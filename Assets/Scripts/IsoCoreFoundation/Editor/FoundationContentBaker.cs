using UnityEditor;
using UnityEngine;

namespace IsoCore.Foundation.EditorTools
{
    /// <summary>
    /// Optional: bakes the code-built default content to ScriptableObject .asset files
    /// so designers can edit them. The runtime does NOT require these (FoundationContent
    /// builds in code); this is purely an authoring convenience.
    /// </summary>
    public static class FoundationContentBaker
    {
        const string Root = "Assets/IsoCoreFoundation/GeneratedContent";

        public static string Bake(bool showDialog)
        {
            // Clean rebuild for idempotency.
            if (AssetDatabase.IsValidFolder(Root)) AssetDatabase.DeleteAsset(Root);
            EnsureFolder("Assets", "IsoCoreFoundation");
            EnsureFolder("Assets/IsoCoreFoundation", "GeneratedContent");
            foreach (var sub in new[] { "Blocks", "BlockGroups", "Biomes", "Items", "Placeables", "Nodes", "Mobs", "Recipes", "Crops" })
                EnsureFolder(Root, sub);

            var c = FoundationContent.BuildDefault();

            // Create ALL assets before SaveAssets so cross-references serialize by GUID.
            foreach (var b in c.Blocks.All) Create(b, $"{Root}/Blocks/{b.id}.asset");
            foreach (var g in c.BlockGroups.All) Create(g, $"{Root}/BlockGroups/{g.id}.asset");
            foreach (var b in c.Biomes.All) Create(b, $"{Root}/Biomes/{b.id}.asset");
            foreach (var i in c.Items.All) Create(i, $"{Root}/Items/{i.id}.asset");
            foreach (var p in c.Placeables.All) Create(p, $"{Root}/Placeables/{p.id}.asset");
            foreach (var n in c.Nodes.All) Create(n, $"{Root}/Nodes/{n.id}.asset");
            foreach (var m in c.Mobs.All) Create(m, $"{Root}/Mobs/{m.id}.asset");
            foreach (var r in c.Recipes.All) Create(r, $"{Root}/Recipes/{r.id}.asset");
            foreach (var cr in c.Crops.All) Create(cr, $"{Root}/Crops/{cr.id}.asset");

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            int total = c.Blocks.Count + c.BlockGroups.Count + c.Biomes.Count + c.Items.Count +
                        c.Placeables.Count + c.Nodes.Count + c.Mobs.Count + c.Recipes.Count + c.Crops.Count;
            string log = $"[ISO-Core] Baked {total} content assets to {Root} (authoring convenience; runtime builds in code).";
            if (showDialog) EditorUtility.DisplayDialog("ISO-Core Foundation — Bake Content", log, "OK");
            Debug.Log(log);
            return log;
        }

        static void Create(Object obj, string path)
        {
            if (obj == null) return;
            AssetDatabase.CreateAsset(obj, path);
        }

        static void EnsureFolder(string parent, string name)
        {
            if (!AssetDatabase.IsValidFolder($"{parent}/{name}"))
                AssetDatabase.CreateFolder(parent, name);
        }
    }
}
