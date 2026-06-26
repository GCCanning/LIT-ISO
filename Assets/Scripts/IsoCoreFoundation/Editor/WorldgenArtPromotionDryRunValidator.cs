using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

#pragma warning disable 0649 // JsonUtility populates these DTO fields by reflection.

namespace IsoCore.Foundation.EditorTools
{
    /// <summary>
    /// Dry-run gate for generated world art promotion. It validates the staged
    /// selection manifest and proposed Unity destinations without copying art
    /// into Resources or changing runtime worldgen rules.
    /// </summary>
    public static class WorldgenArtPromotionDryRunValidator
    {
        const string MenuRoot = "Tools/LIT-ISO/ISO-Core Foundation/";
        const string DefaultReviewRoot = "C:/tmp/LitIsoWorldGen";
        const string ManifestFileName = "art_promotion_selection/art_promotion_selection_manifest.json";
        const string ReportFileName = "art_promotion_selection/unity_art_promotion_dry_run_report.json";

        [MenuItem(MenuRoot + "Validate Art Promotion Dry Run", priority = 56)]
        public static void ValidateFromMenu() => Validate(true);

        public static void ValidateBatch() => Validate(false);

        public static string Validate(bool showDialog)
        {
            string manifestPath = ReviewPath(ManifestFileName);
            string reportPath = ReviewPath(ReportFileName);
            var report = new ArtPromotionDryRunReport
            {
                schema = "litiso.unity_art_promotion_dry_run_report.v1",
                generated_at_utc = DateTime.UtcNow.ToString("O"),
                manifest_path = manifestPath,
                report_path = reportPath,
                errors = new List<ArtPromotionReportIssue>(),
                warnings = new List<ArtPromotionReportIssue>(),
                candidates = new List<ArtPromotionCandidateReport>(),
            };

            if (!File.Exists(manifestPath))
            {
                AddIssue(report.errors, "manifest_missing", "", $"Missing manifest: {manifestPath}");
                FinishReport(report, reportPath, showDialog);
                return Summary(report);
            }

            ArtPromotionSelectionManifest manifest = null;
            try
            {
                manifest = JsonUtility.FromJson<ArtPromotionSelectionManifest>(File.ReadAllText(manifestPath));
            }
            catch (Exception ex)
            {
                AddIssue(report.errors, "manifest_parse_failed", "", ex.Message);
            }

            if (manifest == null)
            {
                AddIssue(report.errors, "manifest_null", "", "JsonUtility returned null manifest.");
                FinishReport(report, reportPath, showDialog);
                return Summary(report);
            }

            report.manifest_schema = manifest.schema;
            if (manifest.schema != "litiso.art_promotion_selection.v1")
            {
                AddIssue(report.errors, "manifest_schema_unexpected", "", manifest.schema ?? "<null>");
            }
            if (manifest.apply_policy != "dry_run_only" && manifest.apply_policy != "review_only")
            {
                AddIssue(report.warnings, "apply_policy_missing_or_unexpected", "",
                    string.IsNullOrWhiteSpace(manifest.apply_policy) ? "<missing>" : manifest.apply_policy);
            }

            ValidateContactSheets(report, manifest.contact_sheets);
            ValidateSelection(report, manifest);

            FinishReport(report, reportPath, showDialog);
            return Summary(report);
        }

        static void ValidateContactSheets(ArtPromotionDryRunReport report, string[] contactSheets)
        {
            if (contactSheets == null || contactSheets.Length == 0)
            {
                AddIssue(report.warnings, "contact_sheets_missing", "", "Manifest does not list contact sheets.");
                return;
            }

            report.contact_sheet_count = contactSheets.Length;
            foreach (string sheet in contactSheets)
            {
                if (string.IsNullOrWhiteSpace(sheet))
                {
                    AddIssue(report.warnings, "contact_sheet_blank", "", "Blank contact sheet entry.");
                    continue;
                }
                if (!File.Exists(ProjectPath(sheet)))
                    AddIssue(report.warnings, "contact_sheet_file_missing", "", sheet);
            }
        }

        static void ValidateSelection(ArtPromotionDryRunReport report, ArtPromotionSelectionManifest manifest)
        {
            var selected = manifest.selected ?? Array.Empty<ArtPromotionSelectionRecord>();
            report.selected_count = selected.Length;

            if (manifest.summary != null)
            {
                report.summary_selected_total = manifest.summary.selected_total;
                report.summary_selected_tiles = manifest.summary.selected_tiles;
                report.summary_selected_props = manifest.summary.selected_props;
                report.summary_manual_review_required = manifest.summary.manual_review_required;
                if (manifest.summary.selected_total != 0 && manifest.summary.selected_total != selected.Length)
                {
                    AddIssue(report.errors,
                        "summary_selected_total_mismatch",
                        "",
                        $"summary:{manifest.summary.selected_total} actual:{selected.Length}");
                }
            }

            var ids = new HashSet<string>(StringComparer.Ordinal);
            var runtimeDestinations = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var stagedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < selected.Length; i++)
            {
                var record = selected[i];
                string id = string.IsNullOrWhiteSpace(record.stable_id) ? $"<index:{i}>" : record.stable_id;
                ValidateRecord(report, record, id, ids, runtimeDestinations, stagedPaths);
            }
        }

        static void ValidateRecord(
            ArtPromotionDryRunReport report,
            ArtPromotionSelectionRecord record,
            string id,
            HashSet<string> ids,
            HashSet<string> runtimeDestinations,
            HashSet<string> stagedPaths)
        {
            if (record == null)
            {
                AddIssue(report.errors, "selected_record_null", id, "Null selected record.");
                return;
            }

            if (string.IsNullOrWhiteSpace(record.stable_id))
            {
                AddIssue(report.errors, "stable_id_missing", id, "Selected record has no stable_id.");
            }
            else if (!ids.Add(record.stable_id))
            {
                AddIssue(report.errors, "stable_id_duplicate", record.stable_id, "Duplicate selected stable_id.");
            }

            string tier = string.IsNullOrWhiteSpace(record.selection_tier) ? "missing" : record.selection_tier;
            string kind = string.IsNullOrWhiteSpace(record.kind) ? "missing" : record.kind;
            CountTier(report, tier);
            CountKind(report, kind);

            bool tierKnown = tier == "runtime_core" ||
                tier == "building_registry" ||
                tier == "extended_catalog" ||
                tier == "review_only";
            if (!tierKnown) AddIssue(report.errors, "selection_tier_unknown", id, tier);

            bool kindKnown = kind == "tile" || kind == "prop";
            if (!kindKnown) AddIssue(report.errors, "kind_unknown", id, kind);

            if (record.review_status != "candidate")
            {
                AddIssue(report.warnings, "review_status_not_candidate", id, record.review_status ?? "<null>");
            }
            if (record.flags != null && record.flags.Length > 0)
            {
                AddIssue(report.warnings, "candidate_has_flags", id, string.Join(",", record.flags));
            }

            string sourcePath = NormalizePath(record.source_path);
            if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
            {
                report.source_missing_count++;
                AddIssue(report.errors, "source_file_missing", id, record.source_path ?? "<null>");
            }
            ValidateStagedPath(report, record, id, stagedPaths);
            ValidateMetrics(report, record, id);

            if (record.manual_review_required)
            {
                report.manual_review_count++;
                AddIssue(report.warnings, "manual_review_required", id, "Candidate must be manually approved before import.");
            }

            ValidateScale(report, record, id, kind);
            ValidateDestinations(report, record, id, kind, tier, runtimeDestinations);
            AddCandidate(report, record, id, kind, tier);
        }

        static void ValidateMetrics(ArtPromotionDryRunReport report, ArtPromotionSelectionRecord record, string id)
        {
            if (record.metrics == null)
            {
                AddIssue(report.errors, "metrics_missing", id, "Missing art metrics.");
                return;
            }

            if (record.metrics.width <= 0 || record.metrics.height <= 0)
                AddIssue(report.errors, "metrics_dimensions_invalid", id, $"{record.metrics.width}x{record.metrics.height}");
            if (!record.metrics.has_alpha || record.metrics.alpha_pixels <= 0)
                AddIssue(report.errors, "metrics_alpha_missing", id, $"has_alpha:{record.metrics.has_alpha} alpha_pixels:{record.metrics.alpha_pixels}");
            if (record.metrics.alpha_coverage <= 0f || record.metrics.alpha_coverage > 1f)
                AddIssue(report.errors, "metrics_alpha_coverage_invalid", id, record.metrics.alpha_coverage.ToString("0.###"));
            if (record.metrics.corner_white_opaque_ratio > 0.01f)
                AddIssue(report.warnings, "corner_white_background_risk", id, record.metrics.corner_white_opaque_ratio.ToString("0.###"));
            if (record.metrics.corner_dark_opaque_ratio > 0.15f)
                AddIssue(report.warnings, "corner_dark_background_risk", id, record.metrics.corner_dark_opaque_ratio.ToString("0.###"));
        }

        static void ValidateStagedPath(
            ArtPromotionDryRunReport report,
            ArtPromotionSelectionRecord record,
            string id,
            HashSet<string> stagedPaths)
        {
            if (string.IsNullOrWhiteSpace(record.staged_path))
            {
                AddIssue(report.errors, "staged_path_missing", id, "No staged copy path.");
                return;
            }
            if (!IsSafeStagingPath(record.staged_path))
            {
                AddIssue(report.errors, "staged_path_unsafe", id, record.staged_path);
                return;
            }

            string stagedAbs = ProjectPath(record.staged_path);
            if (!File.Exists(stagedAbs))
            {
                report.staged_missing_count++;
                AddIssue(report.errors, "staged_file_missing", id, record.staged_path);
            }
            if (!stagedPaths.Add(record.staged_path))
            {
                AddIssue(report.errors, "staged_path_duplicate", id, record.staged_path);
            }
            if (!record.staged_path.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
            {
                AddIssue(report.errors, "staged_extension_not_png", id, record.staged_path);
            }
        }

        static void ValidateScale(ArtPromotionDryRunReport report, ArtPromotionSelectionRecord record, string id, string kind)
        {
            if (record.scale == null)
            {
                AddIssue(report.errors, "scale_missing", id, "Missing scale metadata.");
                return;
            }

            if (record.scale.ppu <= 0)
            {
                AddIssue(report.errors, "ppu_invalid", id, record.scale.ppu.ToString());
            }

            string footprint = string.IsNullOrWhiteSpace(record.scale.footprint) ? "1x1" : record.scale.footprint;
            if (!TryParseFootprint(footprint, out int width, out int height))
            {
                AddIssue(report.errors, "footprint_unknown", id, footprint);
            }
            if (kind == "tile" && footprint != "1x1")
            {
                AddIssue(report.warnings, "tile_non_1x1_footprint", id, footprint);
            }
            if (footprint != "1x1")
            {
                report.multi_cell_footprint_count++;
                if (kind == "prop" && (width > 2 || height > 1))
                {
                    AddIssue(report.warnings, "multi_cell_runtime_occupancy_required", id, footprint);
                }
            }

            if (kind == "tile" && record.scale.anchor != "tilemap cell")
                AddIssue(report.warnings, "tile_anchor_unexpected", id, record.scale.anchor ?? "<null>");
            if (kind == "prop" && record.scale.anchor != "bottom-center")
                AddIssue(report.warnings, "prop_anchor_unexpected", id, record.scale.anchor ?? "<null>");
        }

        static void ValidateDestinations(
            ArtPromotionDryRunReport report,
            ArtPromotionSelectionRecord record,
            string id,
            string kind,
            string tier,
            HashSet<string> runtimeDestinations)
        {
            if (record.destinations == null)
            {
                AddIssue(report.errors, "destinations_missing", id, "Missing proposed destinations.");
                return;
            }

            string runtime = record.destinations.runtime ?? "";
            string generatedReview = record.destinations.generated_review ?? "";
            if (string.IsNullOrWhiteSpace(runtime))
            {
                AddIssue(report.errors, "runtime_destination_missing", id, "No runtime destination.");
            }
            else
            {
                if (!IsSafeProjectRelativePath(runtime, "Assets/Resources/"))
                {
                    AddIssue(report.errors, "runtime_destination_unsafe", id, runtime);
                }
                if (!runtime.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                {
                    AddIssue(report.errors, "runtime_extension_not_png", id, runtime);
                }
                if (!runtimeDestinations.Add(runtime))
                {
                    AddIssue(report.errors, "runtime_destination_duplicate", id, runtime);
                }

                bool expectedRoot = kind == "tile"
                    ? runtime.StartsWith("Assets/Resources/Tiles/", StringComparison.Ordinal)
                    : runtime.StartsWith("Assets/Resources/Decorations/", StringComparison.Ordinal);
                if (!expectedRoot && tier != "review_only")
                {
                    AddIssue(report.errors, "runtime_destination_unexpected_root", id, runtime);
                }

                string runtimeAbs = ProjectPath(runtime);
                if (File.Exists(runtimeAbs))
                {
                    report.runtime_destination_conflict_count++;
                    AddIssue(report.warnings, "runtime_destination_exists", id, runtime);
                }
            }

            if (string.IsNullOrWhiteSpace(generatedReview))
            {
                AddIssue(report.warnings, "generated_review_destination_missing", id, "No generated review destination.");
            }
            else if (!IsSafeProjectRelativePath(generatedReview, "Assets/Generated/"))
            {
                AddIssue(report.warnings, "generated_review_destination_unexpected_root", id, generatedReview);
            }

            if (tier == "review_only")
            {
                AddIssue(report.warnings, "review_only_not_runtime_importable", id, "Kept out of automatic runtime import.");
            }
            if (tier == "building_registry")
            {
                AddIssue(report.warnings, "building_registry_requires_entrance_review", id,
                    "Building ranks need door/entrance cell approval before placement.");
            }
        }

        static void AddCandidate(ArtPromotionDryRunReport report, ArtPromotionSelectionRecord record, string id, string kind, string tier)
        {
            string action = "ready_for_art_lane_import";
            bool hasFlags = record.flags != null && record.flags.Length > 0;
            if (tier == "review_only")
                action = "skip_review_only";
            else if (tier == "building_registry")
                action = "hold_for_entrance_review";
            else if (record.manual_review_required)
                action = "hold_for_manual_review";
            else if (record.review_status != "candidate" || hasFlags)
                action = "hold_for_art_review";

            if (action == "ready_for_art_lane_import")
                report.ready_for_art_lane_import_count++;

            report.candidates.Add(new ArtPromotionCandidateReport
            {
                stable_id = record.stable_id,
                kind = kind,
                selection_tier = tier,
                action = action,
                source_path = record.source_path,
                runtime_destination = record.destinations != null ? record.destinations.runtime : "",
                generated_review_destination = record.destinations != null ? record.destinations.generated_review : "",
                ppu = record.scale != null ? record.scale.ppu : 0,
                footprint = record.scale != null && !string.IsNullOrWhiteSpace(record.scale.footprint) ? record.scale.footprint : "1x1",
                manual_review_required = record.manual_review_required,
            });
        }

        static void CountTier(ArtPromotionDryRunReport report, string tier)
        {
            if (tier == "runtime_core") report.runtime_core_count++;
            else if (tier == "building_registry") report.building_registry_count++;
            else if (tier == "extended_catalog") report.extended_catalog_count++;
            else if (tier == "review_only") report.review_only_count++;
        }

        static void CountKind(ArtPromotionDryRunReport report, string kind)
        {
            if (kind == "tile") report.tile_count++;
            else if (kind == "prop") report.prop_count++;
        }

        static bool TryParseFootprint(string footprint, out int width, out int height)
        {
            width = 0;
            height = 0;
            if (string.IsNullOrWhiteSpace(footprint)) return false;
            var parts = footprint.Split(new[] { 'x', 'X' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 2) return false;
            if (!int.TryParse(parts[0], out width)) return false;
            if (!int.TryParse(parts[1], out height)) return false;
            return width > 0 && height > 0 && width <= 8 && height <= 8;
        }

        static bool IsSafeProjectRelativePath(string path, string requiredPrefix)
        {
            if (string.IsNullOrWhiteSpace(path)) return false;
            string normalized = path.Replace('\\', '/');
            if (Path.IsPathRooted(normalized)) return false;
            if (normalized.Contains(":")) return false;
            if (normalized.StartsWith("../", StringComparison.Ordinal) ||
                normalized.Contains("/../") ||
                normalized.EndsWith("/..", StringComparison.Ordinal))
                return false;
            return normalized.StartsWith(requiredPrefix, StringComparison.Ordinal);
        }

        static void FinishReport(ArtPromotionDryRunReport report, string reportPath, bool showDialog)
        {
            report.error_count = report.errors != null ? report.errors.Count : 0;
            report.warning_count = report.warnings != null ? report.warnings.Count : 0;
            report.passed = report.error_count == 0;
            Directory.CreateDirectory(Path.GetDirectoryName(reportPath));
            File.WriteAllText(reportPath, JsonUtility.ToJson(report, true));
            AssetDatabase.Refresh();

            string summary = Summary(report);
            Debug.Log(summary);
            if (showDialog)
                EditorUtility.DisplayDialog("ISO-Core Foundation - Art Promotion Dry Run", summary, "OK");
        }

        static string Summary(ArtPromotionDryRunReport report) =>
            $"[ISO-Core] Art promotion dry-run {(report.passed ? "PASS" : "FAIL")}: " +
            $"{report.selected_count} selected, ready {report.ready_for_art_lane_import_count}, " +
            $"errors {report.error_count}, warnings {report.warning_count}. " +
            $"Report: {ReviewPath(ReportFileName)}";

        static void AddIssue(List<ArtPromotionReportIssue> issues, string code, string stableId, string detail)
        {
            if (issues == null) return;
            issues.Add(new ArtPromotionReportIssue
            {
                code = code,
                stable_id = stableId ?? "",
                detail = detail ?? "",
            });
        }

        static string NormalizePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return "";
            path = path.Replace('\\', '/');
            if (Path.IsPathRooted(path)) return path;
            return ProjectPath(path);
        }

        static string ProjectPath(string relativePath)
        {
            if (string.IsNullOrWhiteSpace(relativePath)) return FoundationPaths.ProjectRoot;
            if (Path.IsPathRooted(relativePath)) return relativePath;
            return Path.Combine(FoundationPaths.ProjectRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
        }

        static string ReviewRoot()
        {
            string configured = Environment.GetEnvironmentVariable("LITISO_WORLDGEN_REVIEW_ROOT");
            return string.IsNullOrWhiteSpace(configured) ? DefaultReviewRoot : configured;
        }

        static string ReviewPath(string relativePath) =>
            Path.Combine(ReviewRoot(), relativePath.Replace('/', Path.DirectorySeparatorChar));

        static bool IsSafeStagingPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return false;
            string normalized = path.Replace('\\', '/');
            if (Path.IsPathRooted(normalized))
            {
                string root = ReviewRoot().Replace('\\', '/').TrimEnd('/') + "/";
                return normalized.StartsWith(root, StringComparison.OrdinalIgnoreCase) &&
                    normalized.EndsWith(".png", StringComparison.OrdinalIgnoreCase) &&
                    !normalized.Contains("/../");
            }
            return IsSafeProjectRelativePath(normalized, "TempEvalDryRun/WorldGen/");
        }

        [Serializable]
        sealed class ArtPromotionSelectionManifest
        {
            public string schema;
            public string apply_policy;
            public string apply_policy_note;
            public ArtPromotionSelectionSummary summary;
            public string[] contact_sheets;
            public ArtPromotionSelectionRecord[] selected;
        }

        [Serializable]
        sealed class ArtPromotionSelectionSummary
        {
            public int selected_total;
            public int selected_tiles;
            public int selected_props;
            public int manual_review_required;
            public int skipped;
        }

        [Serializable]
        sealed class ArtPromotionSelectionRecord
        {
            public string kind;
            public string stable_id;
            public string source_type;
            public string family;
            public string source_path;
            public string staged_path;
            public string review_status;
            public bool manual_review_required;
            public string[] flags;
            public PromotionMetrics metrics;
            public PromotionScale scale;
            public PromotionDestinations destinations;
            public string selection_tier;
        }

        [Serializable]
        sealed class PromotionScale
        {
            public int ppu;
            public string footprint;
            public string height_class;
            public bool blocks_movement;
            public string anchor;
            public string[] notes;
        }

        [Serializable]
        sealed class PromotionMetrics
        {
            public int width;
            public int height;
            public string mode;
            public bool has_alpha;
            public int alpha_pixels;
            public float alpha_coverage;
            public float bbox_coverage;
            public float corner_white_opaque_ratio;
            public float corner_dark_opaque_ratio;
            public int unique_rgb_sample;
        }

        [Serializable]
        sealed class PromotionDestinations
        {
            public string generated_review;
            public string runtime;
        }

        [Serializable]
        sealed class ArtPromotionDryRunReport
        {
            public string schema;
            public string generated_at_utc;
            public string manifest_path;
            public string manifest_schema;
            public string report_path;
            public bool passed;
            public int selected_count;
            public int summary_selected_total;
            public int summary_selected_tiles;
            public int summary_selected_props;
            public int summary_manual_review_required;
            public int tile_count;
            public int prop_count;
            public int runtime_core_count;
            public int building_registry_count;
            public int extended_catalog_count;
            public int review_only_count;
            public int manual_review_count;
            public int multi_cell_footprint_count;
            public int contact_sheet_count;
            public int source_missing_count;
            public int staged_missing_count;
            public int runtime_destination_conflict_count;
            public int ready_for_art_lane_import_count;
            public int error_count;
            public int warning_count;
            public List<ArtPromotionReportIssue> errors;
            public List<ArtPromotionReportIssue> warnings;
            public List<ArtPromotionCandidateReport> candidates;
        }

        [Serializable]
        sealed class ArtPromotionReportIssue
        {
            public string code;
            public string stable_id;
            public string detail;
        }

        [Serializable]
        sealed class ArtPromotionCandidateReport
        {
            public string stable_id;
            public string kind;
            public string selection_tier;
            public string action;
            public string source_path;
            public string runtime_destination;
            public string generated_review_destination;
            public int ppu;
            public string footprint;
            public bool manual_review_required;
        }
    }
}

#pragma warning restore 0649
