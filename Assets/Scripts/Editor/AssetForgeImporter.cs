/*
 * AssetForgeImporter.cs
 * ─────────────────────────────────────────────────────────────────────────────
 * Unity Editor automation for the LIT-ISO Asset Forge pipeline.
 *
 * What it does
 * ────────────
 * Given a folder of exported PNG sprite sheets from Asset Forge, it:
 *   1. Slices each sheet into individual sprite frames (uniform grid)
 *   2. Creates an AnimationClip per action/direction combo
 *   3. Builds an AnimatorController with states for each clip
 *   4. Creates a prefab with SpriteRenderer + Animator
 *   5. Writes an automation_report.json beside the imported assets
 *
 * Usage
 * ─────
 *   Tools → Asset Forge → Import Sprite Sheet
 *   — or —
 *   Tools → Asset Forge → Batch Import Folder
 *
 * The importer expects sheets named:
 *   {characterId}_{action}_{direction}.png   e.g.  warrior_idle_S.png
 *   warrior_walk_S.png                       (4 frames, left→right in row)
 *
 * Config is supplied via the AssetForgeImportConfig ScriptableObject
 * (create one via Assets → Create → LitISO → AssetForgeImportConfig).
 */

#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace LitISO.Editor
{
    // ─────────────────────────────────────────────────────────────────────────
    // Config SO
    // ─────────────────────────────────────────────────────────────────────────

    [CreateAssetMenu(menuName = "LitISO/AssetForgeImportConfig", fileName = "AssetForgeImportConfig")]
    public class AssetForgeImportConfig : ScriptableObject
    {
        [Header("Sprite Sheet Settings")]
        [Tooltip("Width of a single frame in pixels")]
        public int  frameWidth     = 64;
        [Tooltip("Height of a single frame in pixels")]
        public int  frameHeight    = 64;
        [Tooltip("Pixels Per Unit")]
        public float pixelsPerUnit = 64f;
        [Tooltip("Number of frames per animation row (-1 = auto-detect from sheet width)")]
        public int  framesPerRow   = -1;

        [Header("Animation Settings")]
        [Tooltip("Frames per second for generated AnimationClips")]
        public float animFPS       = 8f;
        [Tooltip("All clips loop by default")]
        public bool  loopClips     = true;

        [Header("Output")]
        [Tooltip("Root folder under Assets/ where generated assets are saved")]
        public string outputRoot   = "Assets/Generated/Characters";
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Importer window
    // ─────────────────────────────────────────────────────────────────────────

    public class AssetForgeImporterWindow : EditorWindow
    {
        AssetForgeImportConfig _config;
        string _sourceFolder = "";
        string _characterId  = "warrior";
        Vector2 _scroll;
        readonly List<string> _log = new();

        // ── Menu items ──────────────────────────────────────────────────────

        [MenuItem("Tools/Asset Forge/Import Sprite Sheet")]
        static void Open() => GetWindow<AssetForgeImporterWindow>("Asset Forge Importer");

        [MenuItem("Tools/Asset Forge/Batch Import Folder")]
        static void BatchFromMenu()
        {
            string folder = EditorUtility.OpenFolderPanel("Select sprite sheet folder", "C:/Projects/Pixel Pipeline/outputs", "");
            if (string.IsNullOrEmpty(folder)) return;

            var win = GetWindow<AssetForgeImporterWindow>("Asset Forge Importer");
            win._sourceFolder = folder;
            win.RunBatchImport();
        }

        // ── GUI ─────────────────────────────────────────────────────────────

        void OnGUI()
        {
            GUILayout.Label("Asset Forge Importer", EditorStyles.boldLabel);
            EditorGUILayout.Space(6);

            _config = (AssetForgeImportConfig)EditorGUILayout.ObjectField(
                "Config", _config, typeof(AssetForgeImportConfig), false);

            if (_config == null)
            {
                EditorGUILayout.HelpBox(
                    "Create an AssetForgeImportConfig via Assets → Create → LitISO → AssetForgeImportConfig",
                    MessageType.Warning);
                return;
            }

            _characterId  = EditorGUILayout.TextField("Character ID", _characterId);
            _sourceFolder = EditorGUILayout.TextField("Source Folder", _sourceFolder);

            if (GUILayout.Button("Browse…", GUILayout.Width(80)))
                _sourceFolder = EditorUtility.OpenFolderPanel("Select sprite folder", _sourceFolder, "");

            EditorGUILayout.Space(4);

            using (new EditorGUI.DisabledGroupScope(string.IsNullOrEmpty(_sourceFolder)))
            {
                if (GUILayout.Button("Import All Sheets in Folder"))
                    RunBatchImport();
            }

            EditorGUILayout.Space(8);
            GUILayout.Label("Log", EditorStyles.miniBoldLabel);
            _scroll = EditorGUILayout.BeginScrollView(_scroll, GUILayout.Height(200));
            foreach (var line in _log)
                EditorGUILayout.LabelField(line, EditorStyles.miniLabel);
            EditorGUILayout.EndScrollView();
        }

        // ── Batch import ────────────────────────────────────────────────────

        void RunBatchImport()
        {
            if (_config == null) { Log("ERROR: No config assigned."); return; }
            if (!Directory.Exists(_sourceFolder)) { Log($"ERROR: Folder not found: {_sourceFolder}"); return; }

            _log.Clear();
            var pngs = Directory.GetFiles(_sourceFolder, "*.png", SearchOption.TopDirectoryOnly);
            if (pngs.Length == 0) { Log("No PNG files found."); return; }

            var report    = new ImportReport { characterId = _characterId, timestamp = DateTime.UtcNow.ToString("o") };
            var allClips  = new List<AnimationClip>();
            string outRoot = $"{_config.outputRoot}/{_characterId}";

            foreach (string png in pngs)
            {
                try
                {
                    var clip = ImportSingleSheet(png, _characterId, outRoot);
                    if (clip != null)
                    {
                        allClips.Add(clip);
                        report.clips.Add(clip.name);
                        Log($"OK: {Path.GetFileName(png)} → {clip.name} ({clip.frameRate} fps)");
                    }
                }
                catch (Exception ex)
                {
                    Log($"FAIL: {Path.GetFileName(png)}: {ex.Message}");
                    report.errors.Add($"{Path.GetFileName(png)}: {ex.Message}");
                }
            }

            if (allClips.Count > 0)
            {
                var ctrl   = BuildAnimatorController(_characterId, allClips, outRoot);
                var prefab = BuildPrefab(_characterId, ctrl, outRoot);
                report.controllerPath = AssetDatabase.GetAssetPath(ctrl);
                report.prefabPath     = AssetDatabase.GetAssetPath(prefab);
                Log($"Animator controller: {report.controllerPath}");
                Log($"Prefab: {report.prefabPath}");
            }

            WriteReport(report, outRoot);
            AssetDatabase.Refresh();
            Log($"Done — {allClips.Count}/{pngs.Length} sheets imported.");
        }

        // ── Single sheet import ─────────────────────────────────────────────

        AnimationClip ImportSingleSheet(string absPath, string characterId, string outRoot)
        {
            // Copy PNG into Assets if it isn't already
            string relDest = $"{outRoot}/Sheets/{Path.GetFileName(absPath)}";
            string absDest = Path.GetFullPath(Path.Combine(Application.dataPath, "../", relDest));
            Directory.CreateDirectory(Path.GetDirectoryName(absDest));

            if (!File.Exists(absDest) || new FileInfo(absPath).LastWriteTimeUtc > new FileInfo(absDest).LastWriteTimeUtc)
                File.Copy(absPath, absDest, true);

            AssetDatabase.ImportAsset(relDest, ImportAssetOptions.ForceUpdate);

            // Configure texture as sprite sheet
            var ti = (TextureImporter)AssetImporter.GetAtPath(relDest);
            ti.textureType         = TextureImporterType.Sprite;
            ti.spriteImportMode    = SpriteImportMode.Multiple;
            ti.filterMode          = FilterMode.Point;
            ti.mipmapEnabled       = false;
            ti.alphaIsTransparency = true;

            var tex       = AssetDatabase.LoadAssetAtPath<Texture2D>(relDest);
            int sheetW    = tex ? tex.width  : _config.frameWidth  * 4;
            int sheetH    = tex ? tex.height : _config.frameHeight;
            int cols      = _config.framesPerRow > 0 ? _config.framesPerRow : sheetW / _config.frameWidth;
            int rows      = sheetH / _config.frameHeight;
            int frameCount = cols * rows;

            var rects = new SpriteMetaData[frameCount];
            for (int i = 0; i < frameCount; i++)
            {
                int col = i % cols;
                int row = rows - 1 - (i / cols);  // flip Y (Unity UV origin = bottom-left)
                rects[i] = new SpriteMetaData
                {
                    name = $"{Path.GetFileNameWithoutExtension(absPath)}_{i:D2}",
                    rect = new Rect(col * _config.frameWidth, row * _config.frameHeight,
                                    _config.frameWidth, _config.frameHeight),
                    pivot = new Vector2(0.5f, 0f),
                    alignment = (int)SpriteAlignment.BottomCenter,
                };
            }
            ti.spritesheet = rects;
            ti.SetTextureSettings(new TextureImporterSettings { spritePixelsPerUnit = _config.pixelsPerUnit });
            EditorUtility.SetDirty(ti);
            ti.SaveAndReimport();

            // Load sliced sprites
            var sprites = AssetDatabase.LoadAllAssetsAtPath(relDest)
                            .OfType<Sprite>()
                            .OrderBy(s => s.name)
                            .ToArray();

            if (sprites.Length == 0) return null;

            // Parse action/direction from filename: {charId}_{action}_{dir}.png
            var stem   = Path.GetFileNameWithoutExtension(absPath);
            var parts  = stem.Split('_');
            string action = parts.Length > 1 ? parts[1] : "idle";
            string dir    = parts.Length > 2 ? parts[2] : "S";

            return BuildAnimationClip(sprites, $"{action}_{dir}", outRoot);
        }

        // ── AnimationClip ───────────────────────────────────────────────────

        AnimationClip BuildAnimationClip(Sprite[] sprites, string clipName, string outRoot)
        {
            var clip = new AnimationClip { frameRate = _config.animFPS };

            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = _config.loopClips;
            AnimationUtility.SetAnimationClipSettings(clip, settings);

            var binding = new EditorCurveBinding
            {
                type          = typeof(SpriteRenderer),
                path          = "",
                propertyName  = "m_Sprite",
            };

            var keyframes = new ObjectReferenceKeyframe[sprites.Length];
            for (int i = 0; i < sprites.Length; i++)
            {
                keyframes[i] = new ObjectReferenceKeyframe
                {
                    time  = i / _config.animFPS,
                    value = sprites[i],
                };
            }
            AnimationUtility.SetObjectReferenceCurve(clip, binding, keyframes);

            string clipPath = $"{outRoot}/Clips/{clipName}.anim";
            Directory.CreateDirectory(Path.GetFullPath(Path.Combine(
                Application.dataPath, "../", Path.GetDirectoryName(clipPath))));
            AssetDatabase.CreateAsset(clip, clipPath);
            return clip;
        }

        // ── AnimatorController ──────────────────────────────────────────────

        AnimatorController BuildAnimatorController(string charId, List<AnimationClip> clips, string outRoot)
        {
            string path = $"{outRoot}/{charId}_Animator.controller";
            var ctrl    = AnimatorController.CreateAnimatorControllerAtPath(path);
            var layer   = ctrl.layers[0];
            var sm      = layer.stateMachine;

            // Parameter: StringToHash key — we use integer hashes for state switching
            ctrl.AddParameter("StateHash", AnimatorControllerParameterType.Int);

            foreach (var clip in clips)
            {
                var state   = sm.AddState(clip.name);
                state.motion = clip;
                // Add Any State → state transition on hash match
                var trans = sm.AddAnyStateTransition(state);
                trans.AddCondition(AnimatorConditionMode.Equals, Animator.StringToHash(clip.name), "StateHash");
                trans.duration           = 0f;
                trans.hasExitTime        = false;
                trans.canTransitionToSelf = false;
            }

            // Default state = first clip
            if (sm.states.Length > 0)
                sm.defaultState = sm.states[0].state;

            EditorUtility.SetDirty(ctrl);
            return ctrl;
        }

        // ── Prefab ──────────────────────────────────────────────────────────

        GameObject BuildPrefab(string charId, AnimatorController ctrl, string outRoot)
        {
            string prefabPath = $"{outRoot}/{charId}.prefab";

            var go = new GameObject(charId);
            go.AddComponent<SpriteRenderer>();
            var anim = go.AddComponent<Animator>();
            anim.runtimeAnimatorController = ctrl;

            var prefab = PrefabUtility.SaveAsPrefabAsset(go, prefabPath);
            DestroyImmediate(go);
            return prefab;
        }

        // ── Report ──────────────────────────────────────────────────────────

        void WriteReport(ImportReport report, string outRoot)
        {
            string dir  = Path.GetFullPath(Path.Combine(Application.dataPath, "../", outRoot));
            Directory.CreateDirectory(dir);
            string path = Path.Combine(dir, "automation_report.json");
            File.WriteAllText(path, JsonUtility.ToJson(report, true));
            Log($"Report written: {path}");
        }

        void Log(string msg)
        {
            _log.Add(msg);
            Debug.Log($"[AssetForge] {msg}");
            Repaint();
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Report data
    // ─────────────────────────────────────────────────────────────────────────

    [Serializable]
    class ImportReport
    {
        public string       characterId;
        public string       timestamp;
        public string       controllerPath;
        public string       prefabPath;
        public List<string> clips  = new();
        public List<string> errors = new();
    }
}
#endif
