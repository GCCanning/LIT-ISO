using System;
using System.Collections.Generic;

namespace IsoCore.Foundation.EditorTools
{
    /// <summary>
    /// Editor-side guard for the StreamingAssets worldgen datapack bridge.
    /// It intentionally validates structure and integration readiness without
    /// importing art or changing the sampler.
    /// </summary>
    public static class WorldgenContractValidator
    {
        public static void Validate(Action<string, bool, string> add)
        {
            if (add == null) return;

            bool exists = System.IO.File.Exists(WorldgenRuntimeContract.DefaultProjectPath);
            add("Worldgen runtime contract exists", exists, WorldgenRuntimeContract.DefaultProjectPath);
            if (!exists) return;

            bool loaded = WorldgenRuntimeContract.TryLoadProjectContract(out var contract, out string error);
            add("Worldgen runtime contract parses and schema is valid",
                loaded,
                loaded ? contract.schema : error);
            if (!loaded || contract == null) return;

            var tiles = contract.tiles ?? Array.Empty<WorldgenTileEntry>();
            var features = contract.features ?? Array.Empty<WorldgenFeatureEntry>();
            var variantGroups = contract.variantGroups ?? Array.Empty<WorldgenVariantGroupEntry>();
            var blendPairs = contract.blendPairs ?? Array.Empty<WorldgenBlendPairEntry>();
            add("Worldgen runtime contract has tile entries", tiles.Length > 0, $"{tiles.Length} tiles");
            add("Worldgen runtime contract has feature entries", features.Length > 0, $"{features.Length} features");
            add("Worldgen runtime contract has variant groups", variantGroups.Length > 0, $"{variantGroups.Length} groups");
            add("Worldgen runtime contract has blend pairs", blendPairs.Length > 0, $"{blendPairs.Length} pairs");

            var content = FoundationContent.BuildDefault();
            ValidateMaterialTags(add, contract, tiles);
            ValidateTileEntries(add, tiles, content);
            ValidateFeatureEntries(add, features, content);
            ValidateVariantGroups(add, variantGroups);
            ValidateBlendPairs(add, blendPairs);
        }

        static void ValidateMaterialTags(Action<string, bool, string> add, WorldgenRuntimeContract contract, WorldgenTileEntry[] tiles)
        {
            var materialTags = new HashSet<string>(StringComparer.Ordinal);
            if (contract.rules != null && contract.rules.materialTagsAreNotTileIds != null)
            {
                foreach (var tag in contract.rules.materialTagsAreNotTileIds)
                    if (!string.IsNullOrWhiteSpace(tag))
                        materialTags.Add(tag);
            }

            bool tagsOk = materialTags.Count >= 5 &&
                materialTags.Contains("grass") &&
                materialTags.Contains("sand") &&
                materialTags.Contains("stone") &&
                materialTags.Contains("water") &&
                materialTags.Contains("dirt");
            add("Worldgen material tags declared", tagsOk, string.Join(",", materialTags));

            bool noTagTiles = true;
            string bad = "";
            foreach (var tile in tiles)
            {
                if (tile == null || string.IsNullOrWhiteSpace(tile.tileId)) continue;
                if (!materialTags.Contains(tile.tileId)) continue;
                noTagTiles = false;
                bad += tile.tileId + " ";
            }
            add("Worldgen material tags are not emitted as concrete tile ids", noTagTiles, bad.Trim());
        }

        static void ValidateTileEntries(Action<string, bool, string> add, WorldgenTileEntry[] tiles, FoundationContent content)
        {
            var ids = new HashSet<string>(StringComparer.Ordinal);
            var statusCounts = new Dictionary<string, int>(StringComparer.Ordinal);
            bool valid = true;
            string detail = "";
            int runtimeBlocks = 0;

            foreach (var tile in tiles)
            {
                if (tile == null || string.IsNullOrWhiteSpace(tile.tileId))
                {
                    valid = false;
                    detail += "missing_tile_id ";
                    continue;
                }
                if (!ids.Add(tile.tileId))
                {
                    valid = false;
                    detail += $"duplicate:{tile.tileId} ";
                }
                string status = tile.promotion != null ? tile.promotion.status : tile.status;
                if (!KnownPromotionStatus(status))
                {
                    valid = false;
                    detail += $"{tile.tileId}:status:{status} ";
                }
                if (IsPromotableTileStatus(status))
                {
                    if (content != null && content.Blocks.Has(tile.tileId))
                        runtimeBlocks++;
                    else
                    {
                        valid = false;
                        detail += $"{tile.tileId}:runtime_block_missing ";
                    }
                }
                Count(statusCounts, status);
            }

            add("Worldgen tile contract ids/statuses/runtime blocks valid",
                valid,
                valid ? $"{Counts(statusCounts)}, blocks:{runtimeBlocks}" : detail.Trim());
        }

        static void ValidateFeatureEntries(Action<string, bool, string> add, WorldgenFeatureEntry[] features, FoundationContent content)
        {
            bool valid = true;
            string detail = "";
            var statusCounts = new Dictionary<string, int>(StringComparer.Ordinal);
            var ids = new HashSet<string>(StringComparer.Ordinal);
            int twoByOne = 0;
            int centers = 0;
            int runtimeAliases = 0;

            foreach (var feature in features)
            {
                if (feature == null ||
                    string.IsNullOrWhiteSpace(feature.featureId) ||
                    string.IsNullOrWhiteSpace(feature.visualId) ||
                    string.IsNullOrWhiteSpace(feature.gameplayPrototype) ||
                    string.IsNullOrWhiteSpace(feature.archetype))
                {
                    valid = false;
                    detail += "missing_feature_fields ";
                    continue;
                }

                if (!ids.Add(feature.featureId))
                {
                    valid = false;
                    detail += $"duplicate:{feature.featureId} ";
                }

                string footprint = string.IsNullOrWhiteSpace(feature.footprint) ? "1x1" : feature.footprint;
                if (footprint != "1x1" && footprint != "2x1")
                {
                    valid = false;
                    detail += $"{feature.visualId}:footprint:{footprint} ";
                }
                if (footprint == "2x1") twoByOne++;
                if (feature.role == "cluster-center") centers++;

                string status = feature.promotion != null ? feature.promotion.status : null;
                if (!KnownPromotionStatus(status))
                {
                    valid = false;
                    detail += $"{feature.visualId}:status:{status} ";
                }

                if (content == null || !content.Nodes.Has(feature.gameplayPrototype))
                {
                    valid = false;
                    detail += $"{feature.visualId}:prototype:{feature.gameplayPrototype} ";
                }

                if (IsPromotableFeatureStatus(status))
                {
                    if (content != null && content.Nodes.Has(feature.visualId))
                        runtimeAliases++;
                    else
                    {
                        valid = false;
                        detail += $"{feature.visualId}:runtime_alias_missing ";
                    }
                }
                Count(statusCounts, status);
            }

            add("Worldgen feature contract ids/statuses/footprints valid",
                valid,
                valid ? $"{Counts(statusCounts)}, 2x1:{twoByOne}, centers:{centers}, aliases:{runtimeAliases}" : detail.Trim());
        }

        static void ValidateVariantGroups(Action<string, bool, string> add, WorldgenVariantGroupEntry[] groups)
        {
            bool valid = true;
            string detail = "";
            var ids = new HashSet<string>(StringComparer.Ordinal);
            int variants = 0;

            foreach (var group in groups)
            {
                if (group == null ||
                    string.IsNullOrWhiteSpace(group.sourceTileId) ||
                    string.IsNullOrWhiteSpace(group.group) ||
                    string.IsNullOrWhiteSpace(group.variantKind) ||
                    !KnownPromotionStatus(group.status))
                {
                    valid = false;
                    detail += "bad_variant_group ";
                    continue;
                }

                var entries = group.variants ?? Array.Empty<WorldgenVariantEntry>();
                if (entries.Length == 0)
                {
                    valid = false;
                    detail += $"{group.sourceTileId}:empty ";
                    continue;
                }

                foreach (var entry in entries)
                {
                    variants++;
                    if (entry == null ||
                        string.IsNullOrWhiteSpace(entry.tileId) ||
                        string.IsNullOrWhiteSpace(entry.path) ||
                        !entry.path.StartsWith("assets/tile/", StringComparison.Ordinal) ||
                        !KnownPromotionStatus(entry.status))
                    {
                        valid = false;
                        detail += $"{group.sourceTileId}:bad_variant ";
                        continue;
                    }

                    if (!ids.Add(entry.tileId))
                    {
                        valid = false;
                        detail += $"duplicate:{entry.tileId} ";
                    }
                }
            }

            add("Worldgen variant groups valid",
                valid && groups.Length > 0 && variants > 0,
                valid ? $"{groups.Length} groups, {variants} variants" : detail.Trim());
        }

        static void ValidateBlendPairs(Action<string, bool, string> add, WorldgenBlendPairEntry[] pairs)
        {
            bool valid = true;
            string detail = "";
            int variants = 0;
            var ids = new HashSet<string>(StringComparer.Ordinal);
            var directions = new HashSet<string>(new[] { "n", "s", "e", "w", "ne", "nw", "se", "sw" }, StringComparer.Ordinal);

            foreach (var pair in pairs)
            {
                if (pair == null ||
                    string.IsNullOrWhiteSpace(pair.fromTileId) ||
                    string.IsNullOrWhiteSpace(pair.toTileId) ||
                    string.IsNullOrWhiteSpace(pair.pattern) ||
                    pair.softness <= 0f ||
                    !KnownPromotionStatus(pair.status))
                {
                    valid = false;
                    detail += "bad_blend_pair ";
                    continue;
                }

                var entries = pair.variants ?? Array.Empty<WorldgenBlendVariantEntry>();
                if (entries.Length == 0)
                {
                    valid = false;
                    detail += $"{pair.fromTileId}->{pair.toTileId}:empty ";
                    continue;
                }

                foreach (var entry in entries)
                {
                    variants++;
                    if (entry == null ||
                        string.IsNullOrWhiteSpace(entry.tileId) ||
                        string.IsNullOrWhiteSpace(entry.path) ||
                        !entry.path.StartsWith("assets/tile/gradient_blends/", StringComparison.Ordinal) ||
                        !directions.Contains(entry.direction) ||
                        !KnownPromotionStatus(entry.status))
                    {
                        valid = false;
                        detail += $"{pair.fromTileId}->{pair.toTileId}:bad_blend ";
                        continue;
                    }

                    if (!ids.Add(entry.tileId))
                    {
                        valid = false;
                        detail += $"duplicate:{entry.tileId} ";
                    }
                }
            }

            add("Worldgen blend pairs valid",
                valid && pairs.Length > 0 && variants > 0,
                valid ? $"{pairs.Length} pairs, {variants} variants" : detail.Trim());
        }

        static bool IsPromotableFeatureStatus(string status) =>
            status == "no-op" ||
            status == "copy-ready" ||
            status == "alias-ready" ||
            status == "unity-live" ||
            status == "generated-only" ||
            status == "biomesketch-only";

        static bool IsPromotableTileStatus(string status) =>
            status == "no-op" ||
            status == "copy-ready" ||
            status == "unity-live" ||
            status == "generated-only" ||
            status == "biomesketch-only";

        static bool KnownPromotionStatus(string status) =>
            status == "no-op" ||
            status == "copy-ready" ||
            status == "alias-ready" ||
            status == "blocked" ||
            status == "unity-live" ||
            status == "generated-only" ||
            status == "biomesketch-only" ||
            status == "missing";

        static void Count(Dictionary<string, int> counts, string key)
        {
            key = string.IsNullOrWhiteSpace(key) ? "null" : key;
            counts.TryGetValue(key, out int value);
            counts[key] = value + 1;
        }

        static string Counts(Dictionary<string, int> counts)
        {
            var parts = new List<string>();
            foreach (var kv in counts)
                parts.Add($"{kv.Key}:{kv.Value}");
            return string.Join(", ", parts);
        }
    }
}
