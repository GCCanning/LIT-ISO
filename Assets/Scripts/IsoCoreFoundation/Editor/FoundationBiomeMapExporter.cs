#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace IsoCore.Foundation.EditorTools
{
    /// <summary>
    /// Bakes a detailed, per-tile flyover window from the live Foundation sampler.
    /// The export is centered on world origin and covers +/-1000 tiles in each direction.
    /// It writes JSON plus a file:// friendly JS wrapper for Tools/BiomeSketch.
    /// </summary>
    public static class FoundationBiomeMapExporter
    {
        const int Radius = 1000;     // tiles from origin in each direction
        const int Step = 2;          // same export cost while covering the expanded continent
        const int MinRegion = 64;    // ignore tiny biome islands in marker labels

        [MenuItem("Tools/LIT-ISO/ISO-Core Foundation/Export Biome Flyover (1000 tiles)", priority = 57)]
        public static void ExportFromFoundationMenu() => Export();

        [MenuItem("Tools/LIT-ISO/Export Biome Flyover (1000 tiles)", priority = 60)]
        public static void ExportFromLitIsoMenu() => Export();

        public static void Export()
        {
            var boot = Object.FindObjectOfType<FoundationBootstrap>();
            if (boot == null || boot.Content == null || boot.config == null)
            {
                EditorUtility.DisplayDialog("Biome Flyover Export",
                    "Enter Play mode and load/generate a world first.\n\n" +
                    "This needs a live FoundationBootstrap so it can sample the exact runtime worldgen.",
                    "OK");
                return;
            }

            var sampler = new IsoTerrainSampler(boot.config, boot.Content);
            int n = (Radius * 2) / Step + 1;

            int[,] biomeGrid = new int[n, n];
            int[,] surfaceGrid = new int[n, n];
            int[,] nodeGrid = new int[n, n];
            int[,] heightGrid = new int[n, n];

            var biomeLegend = new List<string>();
            var surfaceLegend = new List<string>();
            var nodeLegend = new List<string>();

            for (int iy = 0; iy < n; iy++)
            for (int ix = 0; ix < n; ix++)
            {
                int wx = -Radius + ix * Step;
                int wy = -Radius + iy * Step;
                var c = sampler.Sample(wx, wy);

                string biomeId;
                if (c.Water) biomeId = "water";
                else
                {
                    var biome = sampler.BiomeAt(c.BiomeIndex);
                    biomeId = biome != null ? biome.id : "void";
                }

                biomeGrid[iy, ix] = LegendIndex(biomeLegend, biomeId);
                surfaceGrid[iy, ix] = LegendIndex(surfaceLegend, string.IsNullOrEmpty(c.SurfaceBlockId) ? "void" : c.SurfaceBlockId);
                nodeGrid[iy, ix] = string.IsNullOrEmpty(c.NodeId) ? -1 : LegendIndex(nodeLegend, c.NodeId);
                heightGrid[iy, ix] = c.Height;
            }

            bool[,] seen = new bool[n, n];
            var markers = BuildBiomeMarkers(biomeGrid, biomeLegend, seen, n);

            var sb = new StringBuilder(16 * 1024 * 1024);
            sb.Append("{\n");
            sb.Append("\"generatedBy\":\"FoundationBiomeMapExporter\",");
            sb.Append("\"view\":\"isometric-flyover\",");
            sb.AppendFormat("\"seed\":{0},\"radius\":{1},\"step\":{2},\"n\":{3},\n", boot.config.seed, Radius, Step, n);
            AppendLegend(sb, "legend", biomeLegend); sb.Append(",\n");
            AppendGrid(sb, "grid", biomeGrid, n); sb.Append(",\n");
            AppendLegend(sb, "surfaceLegend", surfaceLegend); sb.Append(",\n");
            AppendGrid(sb, "surfaceGrid", surfaceGrid, n); sb.Append(",\n");
            AppendLegend(sb, "nodeLegend", nodeLegend); sb.Append(",\n");
            AppendGrid(sb, "nodeGrid", nodeGrid, n); sb.Append(",\n");
            AppendGrid(sb, "heightGrid", heightGrid, n); sb.Append(",\n");
            sb.Append("\"markers\":[");
            for (int i = 0; i < markers.Count; i++) sb.Append((i > 0 ? "," : "") + markers[i]);
            sb.Append("]\n}");

            string outDir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Tools", "BiomeSketch"));
            Directory.CreateDirectory(outDir);
            string json = sb.ToString();
            string outPath = Path.Combine(outDir, "world_biome_map.json");
            string jsPath = Path.Combine(outDir, "world_biome_map_data.js");
            File.WriteAllText(outPath, json);
            File.WriteAllText(jsPath, "window.LIT_ISO_WORLD_BIOME_MAP = " + json + ";\n");

            Debug.Log($"[BiomeMap] Wrote {outPath} and {jsPath} ({n}x{n} exact tiles, {markers.Count} biome markers, seed {boot.config.seed}).");
            EditorUtility.RevealInFinder(outPath);
        }

        static List<string> BuildBiomeMarkers(int[,] biomeGrid, List<string> biomeLegend, bool[,] seen, int n)
        {
            var markers = new List<string>();
            int[] dx = { 1, -1, 0, 0 };
            int[] dy = { 0, 0, 1, -1 };
            var q = new Queue<int>();

            for (int sy = 0; sy < n; sy++)
            for (int sx = 0; sx < n; sx++)
            {
                if (seen[sy, sx]) continue;
                int id = biomeGrid[sy, sx];
                seen[sy, sx] = true;
                q.Clear();
                q.Enqueue(sy * n + sx);
                long accX = 0, accY = 0;
                int cnt = 0;

                while (q.Count > 0)
                {
                    int p = q.Dequeue();
                    int cy = p / n, cx = p % n;
                    accX += cx;
                    accY += cy;
                    cnt++;

                    for (int k = 0; k < 4; k++)
                    {
                        int nx = cx + dx[k], ny = cy + dy[k];
                        if (nx < 0 || ny < 0 || nx >= n || ny >= n || seen[ny, nx] || biomeGrid[ny, nx] != id) continue;
                        seen[ny, nx] = true;
                        q.Enqueue(ny * n + nx);
                    }
                }

                string bid = biomeLegend[id];
                if (cnt < MinRegion || bid == "water" || bid == "void") continue;
                double cxAvg = accX / (double)cnt;
                double cyAvg = accY / (double)cnt;
                int wcx = (int)(-Radius + cxAvg * Step);
                int wcy = (int)(-Radius + cyAvg * Step);
                markers.Add(string.Format(
                    "{{\"biome\":\"{0}\",\"cellX\":{1},\"cellY\":{2},\"samples\":{3},\"approxTiles\":{4}}}",
                    Esc(bid), wcx, wcy, cnt, cnt * Step * Step));
            }

            return markers;
        }

        static int LegendIndex(List<string> legend, string value)
        {
            int idx = legend.IndexOf(value);
            if (idx >= 0) return idx;
            legend.Add(value);
            return legend.Count - 1;
        }

        static void AppendLegend(StringBuilder sb, string name, List<string> legend)
        {
            sb.Append("\"").Append(name).Append("\":[");
            for (int i = 0; i < legend.Count; i++)
                sb.Append(i > 0 ? ",\"" : "\"").Append(Esc(legend[i])).Append("\"");
            sb.Append("]");
        }

        static void AppendGrid(StringBuilder sb, string name, int[,] grid, int n)
        {
            sb.Append("\"").Append(name).Append("\":[");
            for (int iy = 0; iy < n; iy++)
            {
                sb.Append(iy > 0 ? ",[" : "[");
                for (int ix = 0; ix < n; ix++)
                    sb.Append(ix > 0 ? "," : "").Append(grid[iy, ix]);
                sb.Append("]");
            }
            sb.Append("]");
        }

        static string Esc(string s) => (s ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"");
    }
}
#endif
