#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// SpriteForge -> Unity installer (Claude's lane, SPEC section 5).
/// Slices an APPROVED SpriteForge sheet (sheet.png + sheet.json) into the
/// game's existing animation convention:
///   Assets/Characters/[Name]/AnimationSprites/[Action] [DIR]/[name] [action] [DIR][NN].png
/// (the layout the Witch uses, so existing animators bind with no code changes).
/// Import settings: point filter, no compression, configurable PPU, pivot from
/// the sheet sidecar (bottom-center by SpriteForge convention).
/// </summary>
public class SpriteForgeSheetInstaller : EditorWindow
{
    [System.Serializable]
    class FrameMeta { public int index; public int[] cell_rect; }

    [System.Serializable]
    class SheetMeta
    {
        public string schema;
        public int frames;
        public int[] cell;
        public float fps;
        public bool loop;
        public string character;
        public string action;
        public string direction;
        public FrameMeta[] frame_meta;
    }

    string _sourceRoot = "Tools/SpriteForge/out";
    string _characterOverride = "";
    int _ppu = 32;
    Vector2 _scroll;
    readonly List<string> _log = new();

    [MenuItem("LIT-ISO/SpriteForge/Install Approved Sheets...")]
    static void Open() => GetWindow<SpriteForgeSheetInstaller>("SpriteForge Install");

    void OnGUI()
    {
        EditorGUILayout.HelpBox(
            "Installs APPROVED SpriteForge sheets into Assets/Characters/<Name>/" +
            "AnimationSprites/. Only run on bundles that passed QA + dashboard " +
            "approval (SPEC section 6).", MessageType.Info);

        _sourceRoot = EditorGUILayout.TextField("Source root", _sourceRoot);
        _characterOverride = EditorGUILayout.TextField("Character override", _characterOverride);
        _ppu = EditorGUILayout.IntField("Pixels per unit", Mathf.Max(1, _ppu));

        if (GUILayout.Button("Scan + Install All sheet.json Under Source Root"))
            InstallAll();

        _scroll = EditorGUILayout.BeginScrollView(_scroll);
        foreach (var line in _log) EditorGUILayout.LabelField(line);
        EditorGUILayout.EndScrollView();
    }

    void InstallAll()
    {
        _log.Clear();
        string root = Path.GetFullPath(_sourceRoot);
        if (!Directory.Exists(root)) { _log.Add("Source root not found: " + root); return; }

        var sheets = Directory.GetFiles(root, "sheet.json", SearchOption.AllDirectories);
        if (sheets.Length == 0) { _log.Add("No sheet.json found under " + root); return; }

        int installed = 0;
        try
        {
            AssetDatabase.StartAssetEditing();
            foreach (var jsonPath in sheets)
                if (InstallOne(jsonPath)) installed++;
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
            AssetDatabase.Refresh();
        }
        _log.Add($"Done: {installed}/{sheets.Length} sheets installed.");
    }

    bool InstallOne(string jsonPath)
    {
        var meta = JsonUtility.FromJson<SheetMeta>(File.ReadAllText(jsonPath));
        if (meta == null || meta.frame_meta == null || meta.cell == null)
        { _log.Add("SKIP (bad sidecar): " + jsonPath); return false; }

        string character = string.IsNullOrEmpty(_characterOverride)
            ? (meta.character ?? "Unknown") : _characterOverride;
        if (string.IsNullOrEmpty(meta.action) || string.IsNullOrEmpty(meta.direction))
        { _log.Add("SKIP (missing action/direction): " + jsonPath); return false; }

        string sheetPng = Path.Combine(Path.GetDirectoryName(jsonPath), "sheet.png");
        if (!File.Exists(sheetPng)) { _log.Add("SKIP (no sheet.png): " + jsonPath); return false; }

        var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        tex.LoadImage(File.ReadAllBytes(sheetPng)); // sizes itself

        // Witch convention: "Run E" folder, "witch run E00.png" files.
        string nice = char.ToUpperInvariant(meta.action[0]) + meta.action.Substring(1);
        string dir = meta.direction.ToUpperInvariant();
        string folder = $"Assets/Characters/{character}/AnimationSprites/{nice} {dir}";
        Directory.CreateDirectory(folder);

        int cw = meta.cell[0], ch = meta.cell[1];
        foreach (var f in meta.frame_meta)
        {
            // sheet.png origin is top-left; GetPixels is bottom-left - flip Y.
            int x = f.cell_rect[0];
            var cell = new Texture2D(cw, ch, TextureFormat.RGBA32, false);
            cell.SetPixels(tex.GetPixels(x, tex.height - ch, cw, ch));
            cell.Apply();
            string file = $"{folder}/{character.ToLowerInvariant()} {meta.action.ToLowerInvariant()} {dir}{f.index:00}.png";
            File.WriteAllBytes(file, cell.EncodeToPNG());
            DestroyImmediate(cell);
            _pending.Add(file);
        }
        DestroyImmediate(tex);
        EditorApplication.delayCall += ApplyImportSettings;
        _log.Add($"OK {character}/{nice} {dir}: {meta.frame_meta.Length} frames");
        return true;
    }

    static readonly List<string> _pending = new();

    void ApplyImportSettings()
    {
        foreach (var path in _pending)
        {
            var imp = AssetImporter.GetAtPath(path) as TextureImporter;
            if (imp == null) continue;
            imp.textureType = TextureImporterType.Sprite;
            imp.spriteImportMode = SpriteImportMode.Single;
            imp.filterMode = FilterMode.Point;
            imp.textureCompression = TextureImporterCompression.Uncompressed;
            imp.spritePixelsPerUnit = _ppu;
            var s = new TextureImporterSettings();
            imp.ReadTextureSettings(s);
            s.spriteAlignment = (int)SpriteAlignment.BottomCenter; // SpriteForge pivot
            imp.SetTextureSettings(s);
            imp.SaveAndReimport();
        }
        _pending.Clear();
    }
}
#endif
