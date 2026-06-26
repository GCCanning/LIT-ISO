using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace IsoCore.Foundation.EditorTools
{
    /// <summary>
    /// Task #60: brute-force/verify a world seed with a settlement near spawn that
    /// actually generates Building/DoorLane cells (not just Plaza/Road/Farm/Pasture).
    /// Uses the real IsoTerrainSampler.Sample() pipeline (D1-D7 included via
    /// ApplySettlement), so results are ground-truth - no reimplementation of the
    /// hashing/noise math.
    /// </summary>
    public static class TownSeedFinder
    {
        [MenuItem("ISO-Core/Worldgen/Find Town Seed Near Spawn")]
        public static void Find()
        {
            var content = FoundationContent.BuildDefault();
            var sb = new StringBuilder();
            sb.AppendLine("# Town Seed Search (Task #60)");
            sb.AppendLine();

            // Keep this CHEAP - the previous version scanned +/-280 at step 2 across
            // 10 seeds (~2M Sample() calls, each several Perlin lookups) with no
            // progress bar/yield, which froze the editor for minutes. Hamlet band is
            // chunkRing 3-10 -> centerClearing 36-120 cells, village band 11-28 ->
            // 132-336 cells, so +/-160 at step 3 (seed 1337 first) covers hamlet +
            // most of village while staying well under ~1.2M Sample() calls per seed.
            int[] seedsToTry = { 1337, 2026, 42 };
            int maxRadius = 160;
            int step = 3; // buildings are >=2x2, won't be missed at step 3

            bool found = false;
            bool cancelled = false;
            try
            {
                foreach (var seed in seedsToTry)
                {
                    if (cancelled) break;

                    var cfg = new FoundationConfig { seed = seed };
                    var sampler = new IsoTerrainSampler(cfg, content);

                    // Collect Building/DoorLane cells across the scan area.
                    var buildingCells = new List<Vector2Int>();
                    var doorCells = new List<Vector2Int>();

                    int rows = (2 * maxRadius) / step + 1;
                    int rowIdx = 0;
                    for (int wx = -maxRadius; wx <= maxRadius; wx += step)
                    {
                        rowIdx++;
                        if (rowIdx % 8 == 0)
                        {
                            if (EditorUtility.DisplayCancelableProgressBar(
                                "Town Seed Finder",
                                $"Seed {seed}: scanning x={wx} ({rowIdx}/{rows})",
                                (float)rowIdx / rows))
                            {
                                cancelled = true;
                                break;
                            }
                        }

                        for (int wy = -maxRadius; wy <= maxRadius; wy += step)
                        {
                            var cell = sampler.Sample(wx, wy);
                            if (cell.SolidBlock && cell.SurfaceBlockId == "stone_block")
                                buildingCells.Add(new Vector2Int(wx, wy));
                            else if (!cell.SolidBlock && cell.SurfaceBlockId == "stone_path")
                                doorCells.Add(new Vector2Int(wx, wy));
                        }
                    }

                    sb.AppendLine($"## Seed {seed}");
                    sb.AppendLine($"- Building cells found in +/-{maxRadius} (step {step}): {buildingCells.Count}");
                    sb.AppendLine($"- stone_path (door-lane/road) cells found: {doorCells.Count}");
                    if (cancelled) { sb.AppendLine("- (search cancelled by user)"); break; }

                    if (buildingCells.Count > 0)
                {
                    // Cluster: find centroid + bounding box of building cells closest to origin.
                    var nearest = buildingCells[0];
                    int bestDist = int.MaxValue;
                    foreach (var c in buildingCells)
                    {
                        int d = Mathf.Max(Mathf.Abs(c.x), Mathf.Abs(c.y));
                        if (d < bestDist) { bestDist = d; nearest = c; }
                    }

                    int minX = int.MaxValue, maxX = int.MinValue, minY = int.MaxValue, maxY = int.MinValue;
                    int clusterCount = 0;
                    foreach (var c in buildingCells)
                    {
                        if (Mathf.Max(Mathf.Abs(c.x - nearest.x), Mathf.Abs(c.y - nearest.y)) <= 40)
                        {
                            clusterCount++;
                            minX = Mathf.Min(minX, c.x); maxX = Mathf.Max(maxX, c.x);
                            minY = Mathf.Min(minY, c.y); maxY = Mathf.Max(maxY, c.y);
                        }
                    }

                    sb.AppendLine($"- VERIFIED: nearest building cell at world ({nearest.x},{nearest.y}), " +
                                   $"Chebyshev distance {bestDist} from spawn (0,0).");
                    sb.AppendLine($"- Nearby building cluster bbox: x[{minX}..{maxX}] y[{minY}..{maxY}], " +
                                   $"{clusterCount} building cells sampled in that cluster.");
                    sb.AppendLine($"- Direction from spawn: {(nearest.x >= 0 ? "+X (east)" : "-X (west)")}, " +
                                   $"{(nearest.y >= 0 ? "+Y (north)" : "-Y (south)")}.");
                    found = true;
                }
                else
                {
                    sb.AppendLine("- No Building cells found in scan range - settlement either absent, " +
                                   "too far, or only Plaza/Road/Farm/Pasture (no lots passed D1-D3).");
                }
                sb.AppendLine();

                    // Early-exit once we have a verified candidate within hamlet/village range.
                    if (found) break;
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            string path = "Docs/agent-comms/town-seed-search-result.md";
            File.WriteAllText(path, sb.ToString());
            AssetDatabase.Refresh();
            Debug.Log($"[TownSeedFinder] Wrote {path}\n" + sb.ToString());
            EditorUtility.DisplayDialog("Town Seed Finder", $"Done. See {path} (and Console) for results.", "OK");
        }
    }
}
