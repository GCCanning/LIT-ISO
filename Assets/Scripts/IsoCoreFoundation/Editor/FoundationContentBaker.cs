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
            foreach (var sub in new[] { "Blocks", "BlockGroups", "Biomes", "Items", "Placeables", "Nodes", "Mobs", "Recipes", "Crops", "Callings", "Skills", "Quests", "SystemMessages", "EvidenceEvents", "XPChannels", "Titles", "Affinities", "Abilities", "Classes", "Professions", "Dungeons", "Expeditions", "DungeonResults", "GuildBoardEntries", "WorldEvents" })
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
            foreach (var calling in c.Callings.All) Create(calling, $"{Root}/Callings/{calling.id}.asset");
            foreach (var skill in c.Skills.All) Create(skill, $"{Root}/Skills/{skill.id}.asset");
            foreach (var quest in c.Quests.All) Create(quest, $"{Root}/Quests/{quest.id}.asset");
            foreach (var message in c.SystemMessages.All) Create(message, $"{Root}/SystemMessages/{message.id}.asset");
            foreach (var evidence in c.EvidenceEvents.All) Create(evidence, $"{Root}/EvidenceEvents/{evidence.id}.asset");
            foreach (var xp in c.XPChannels.All) Create(xp, $"{Root}/XPChannels/{xp.id}.asset");
            foreach (var title in c.Titles.All) Create(title, $"{Root}/Titles/{title.id}.asset");
            foreach (var affinity in c.Affinities.All) Create(affinity, $"{Root}/Affinities/{affinity.id}.asset");
            foreach (var ability in c.Abilities.All) Create(ability, $"{Root}/Abilities/{ability.id}.asset");
            foreach (var cls in c.Classes.All) Create(cls, $"{Root}/Classes/{cls.id}.asset");
            foreach (var profession in c.Professions.All) Create(profession, $"{Root}/Professions/{profession.id}.asset");
            foreach (var dungeon in c.Dungeons.All) Create(dungeon, $"{Root}/Dungeons/{dungeon.id}.asset");
            foreach (var expedition in c.Expeditions.All) Create(expedition, $"{Root}/Expeditions/{expedition.id}.asset");
            foreach (var result in c.DungeonResults.All) Create(result, $"{Root}/DungeonResults/{result.id}.asset");
            foreach (var entry in c.GuildBoardEntries.All) Create(entry, $"{Root}/GuildBoardEntries/{entry.id}.asset");
            foreach (var evt in c.WorldEvents.All) Create(evt, $"{Root}/WorldEvents/{evt.id}.asset");

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            int total = c.Blocks.Count + c.BlockGroups.Count + c.Biomes.Count + c.Items.Count +
                        c.Placeables.Count + c.Nodes.Count + c.Mobs.Count + c.Recipes.Count + c.Crops.Count +
                        c.Callings.Count + c.Skills.Count + c.Quests.Count +
                        c.SystemMessages.Count + c.EvidenceEvents.Count + c.XPChannels.Count +
                        c.Titles.Count + c.Affinities.Count + c.Abilities.Count + c.Classes.Count + c.Professions.Count +
                        c.Dungeons.Count + c.Expeditions.Count + c.DungeonResults.Count +
                        c.GuildBoardEntries.Count + c.WorldEvents.Count;
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
