// LPCGoldenPathSetup.cs - Editor tool that wires the LPC layered character
// onto the active scene's Player GameObject.
//
// Designed to slot into the golden-path flow. Run AFTER QuickPlayTestSetup so
// the Player already exists. Safe to re-run - it finds existing components and
// updates them rather than duplicating.
//
// What it does:
//   1. Finds the Player (by IsoPlayerController) in the open scene.
//   2. Ensures a child "LPCRoot" GameObject exists under it.
//   3. Adds LPCCharacter, LPCAnimator, LPCPlayerBridge if missing.
//   4. Hides the player's legacy SpriteRenderer (kept for fallback, not deleted).
//   5. If default LPC sheet assets exist in Assets/LPC/Data/, auto-equips them.
//
// All steps are idempotent. The original IsoPlayerController is never modified.

#if UNITY_EDITOR
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using EthraClone.TrialWeek;

namespace LITISO.LPC.EditorTools
{
    public static class LPCGoldenPathSetup
    {
        private const string LPCRootName     = "LPCRoot";
        private const string LPCDataFolder   = "Assets/LPC/Data";
        private const string LPCSpritesFolder = "Assets/LPC/Sprites";

        // Default starter equipment - matches the sprites copied during initial setup
        private static readonly (LPCLayer slot, string assetName)[] DefaultEquipment =
        {
            (LPCLayer.Body,       "body_male_light"),
            (LPCLayer.Hair,       "hair_plain_black"),
            (LPCLayer.Torso,      "torso_chainmail_gray"),
            (LPCLayer.Legs,       "legs_plate_brass"),
            (LPCLayer.Feet,       "feet_boots_black"),
        };

        [MenuItem("Tools/LIT-ISO/Golden Path/Wire LPC Player", false, -95)]
        public static void WireLPCPlayerMenu()
        {
            string log = WireLPCPlayer();
            EditorUtility.DisplayDialog("LPC Wire", log, "OK");
            Debug.Log("[LPC Golden Path]\n" + log);
        }

        [MenuItem("Tools/LIT-ISO/Golden Path/Reset LPC Player (delete and re-wire)", false, -94)]
        public static void ResetLPCPlayerMenu()
        {
            var playerCtrl = Object.FindFirstObjectByType<IsoPlayerController>();
            if (playerCtrl != null)
            {
                var existingRoot = playerCtrl.transform.Find(LPCRootName);
                if (existingRoot != null)
                {
                    Object.DestroyImmediate(existingRoot.gameObject);
                    Debug.Log($"[LPC] Destroyed existing {LPCRootName}");
                }
            }
            string log = WireLPCPlayer();
            EditorUtility.DisplayDialog("LPC Reset", log, "OK");
            Debug.Log("[LPC Reset]\n" + log);
        }

        /// <summary>
        /// If raw PNGs exist in Assets/LPC/Sprites/ but no matching .asset files
        /// exist in Assets/LPC/Data/, run the importer so assignment can happen.
        /// Idempotent: skips if data folder already has matching assets.
        /// </summary>
        private static void EnsureSheetAssets(System.Text.StringBuilder sb)
        {
            if (!Directory.Exists(LPCSpritesFolder))
            {
                sb.AppendLine($"·  No sprite folder at {LPCSpritesFolder} - skipping import.");
                return;
            }

            // Count PNGs (recursive) vs existing .asset files
            int pngCount = Directory.GetFiles(LPCSpritesFolder, "*.png", SearchOption.AllDirectories).Length;
            int assetCount = Directory.Exists(LPCDataFolder)
                ? Directory.GetFiles(LPCDataFolder, "*.asset", SearchOption.TopDirectoryOnly).Length
                : 0;

            if (pngCount == 0)
            {
                sb.AppendLine($"·  No PNGs in {LPCSpritesFolder} yet.");
                return;
            }

            if (assetCount >= pngCount)
            {
                sb.AppendLine($"·  Sheet assets already exist ({assetCount} for {pngCount} PNGs).");
                return;
            }

            sb.AppendLine($"·  Auto-importing: {pngCount} PNGs found, only {assetCount} assets exist.");
            LPCImporter.ImportSheets();
            sb.AppendLine($"✓  Import complete.");
        }

        /// <summary>
        /// Returns a multi-line log of what happened. Callable from other editor scripts
        /// (e.g. GoldenPathTools.RunCurrentGoldenPath can chain this in).
        /// </summary>
        public static string WireLPCPlayer()
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("=== Wire LPC Player ===");

            // 1. Find the player
            var playerCtrl = Object.FindFirstObjectByType<IsoPlayerController>();
            if (playerCtrl == null)
            {
                sb.AppendLine("✗ No IsoPlayerController in scene. Run Quick Play Test first.");
                return sb.ToString();
            }
            var playerGO = playerCtrl.gameObject;
            sb.AppendLine($"·  Found Player: {playerGO.name}");

            // 2. Find or create LPCRoot child
            Transform lpcRootTf = playerGO.transform.Find(LPCRootName);
            GameObject lpcRoot;
            if (lpcRootTf == null)
            {
                lpcRoot = new GameObject(LPCRootName);
                lpcRoot.transform.SetParent(playerGO.transform, false);
                lpcRoot.transform.localPosition = Vector3.zero;
                sb.AppendLine($"✓  Created child {LPCRootName}");
            }
            else
            {
                lpcRoot = lpcRootTf.gameObject;
                sb.AppendLine($"·  Reusing existing {LPCRootName}");
            }

            // 3. Ensure LPC components
            var character = lpcRoot.GetComponent<LPCCharacter>() ?? lpcRoot.AddComponent<LPCCharacter>();
            var animator  = lpcRoot.GetComponent<LPCAnimator>()  ?? lpcRoot.AddComponent<LPCAnimator>();
            var bridge    = lpcRoot.GetComponent<LPCPlayerBridge>() ?? lpcRoot.AddComponent<LPCPlayerBridge>();

            // Bridge needs the player's Rigidbody2D as movement source
            bridge.source = playerGO.GetComponent<Rigidbody2D>();
            EditorUtility.SetDirty(bridge);
            sb.AppendLine("✓  LPCCharacter + LPCAnimator + LPCPlayerBridge ready");

            // 4. Auto-run the importer if PNGs exist but no .asset files do.
            //    This lets the golden path be truly one-click instead of two-step.
            EnsureSheetAssets(sb);

            // 5. Auto-equip default sheets if data folder has them
            if (Directory.Exists(LPCDataFolder))
            {
                int equipped = 0;
                foreach (var (slot, assetName) in DefaultEquipment)
                {
                    string assetPath = $"{LPCDataFolder}/{assetName}.asset";
                    var sheet = AssetDatabase.LoadAssetAtPath<LPCSpriteSheet>(assetPath);
                    if (sheet != null)
                    {
                        character.SetEquipment(slot, sheet);
                        equipped++;
                    }
                }
                if (equipped > 0)
                    sb.AppendLine($"✓  Equipped {equipped} default LPC sheets");
                else
                    sb.AppendLine("·  No LPC sheet assets matched defaults. Drop more PNGs into Assets/LPC/Sprites/.");
            }
            else
            {
                sb.AppendLine($"·  No LPC data folder at {LPCDataFolder}. Place sprites in Assets/LPC/Sprites/ first.");
            }
            EditorUtility.SetDirty(character);

            // 5. Hide legacy single SpriteRenderer (the IsoPlayerController one)
            //    We don't destroy it - the controller may still expect it to exist.
            //    Just disable rendering so LPC layers are what you see.
            foreach (var sr in playerGO.GetComponentsInChildren<SpriteRenderer>(includeInactive: true))
            {
                // Skip our own LPC renderers
                if (sr.transform.IsChildOf(lpcRoot.transform)) continue;
                if (sr.enabled)
                {
                    sr.enabled = false;
                    EditorUtility.SetDirty(sr);
                    sb.AppendLine($"·  Disabled legacy SpriteRenderer on '{sr.gameObject.name}'");
                }
            }

            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(playerGO.scene);
            sb.AppendLine("✓  Done. Press Play to see the layered LPC character.");
            return sb.ToString();
        }
    }
}
#endif
