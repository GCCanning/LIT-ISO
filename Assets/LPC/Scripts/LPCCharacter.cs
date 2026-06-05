// LPCCharacter.cs - Layered sprite character composed of LPC sheets.
//
// One LPCCharacter GameObject has multiple child SpriteRenderers, one per
// equipment slot. All renderers play the same animation/direction in sync,
// driven by LPCAnimator.
//
// Equipment changes: just assign a new LPCSpriteSheet to the corresponding
// slot via SetEquipment(LPCLayer, LPCSpriteSheet). The renderer auto-updates.

using System.Collections.Generic;
using UnityEngine;

namespace LITISO.LPC
{
    [DisallowMultipleComponent]
    public class LPCCharacter : MonoBehaviour
    {
        [Header("World Sorting")]
        public bool useWorldYSorting = true;
        public int sortPixelsPerUnitY = 100;
        public int sortBaseOffset = 0;

        [Header("Base Identity")]
        public LPCBodyType bodyType = LPCBodyType.Male;

        [Header("Equipment Slots (assign LPCSpriteSheet assets)")]
        public LPCSpriteSheet bodySheet;
        public LPCSpriteSheet eyesSheet;
        public LPCSpriteSheet hairSheet;
        public LPCSpriteSheet facialHairSheet;
        public LPCSpriteSheet headSheet;
        public LPCSpriteSheet neckSheet;
        public LPCSpriteSheet torsoSheet;
        public LPCSpriteSheet legsSheet;
        public LPCSpriteSheet feetSheet;
        public LPCSpriteSheet beltSheet;
        public LPCSpriteSheet armsSheet;
        public LPCSpriteSheet shouldersSheet;
        public LPCSpriteSheet shieldSheet;
        public LPCSpriteSheet weaponSheet;

        [Header("Runtime State (read-only at runtime)")]
        public LPCDirection currentDirection = LPCDirection.South;
        public LPCAnimation currentAnimation = LPCAnimation.Idle;
        public int currentFrame = 0;

        // One SpriteRenderer per layer slot, created lazily.
        private readonly Dictionary<LPCLayer, SpriteRenderer> renderers = new();

        void Awake()
        {
            RebuildRenderers();
        }

        void LateUpdate()
        {
            if (useWorldYSorting)
                ApplySortingOrders();
        }

        void OnValidate()
        {
            if (Application.isPlaying)
                RebuildRenderers();
        }

        /// <summary>
        /// Rebuilds the renderer dictionary based on currently assigned sheets.
        /// Creates child GameObjects with SpriteRenderers if needed, and removes
        /// renderers for slots that are no longer used.
        /// </summary>
        public void RebuildRenderers()
        {
            // ----- Reconcile with existing children -----
            // The 'renderers' dict starts empty after a domain reload or
            // re-component, but children GameObjects from previous runs may
            // still be in the scene. Pick them up by name so we don't end up
            // with orphan duplicates next to fresh ones.
            //
            // Handles Unity's auto-rename duplicates like "Body (1)" by
            // stripping the trailing " (n)" before parsing the enum.
            renderers.Clear();
            var deadChildren = new System.Collections.Generic.List<GameObject>();
            foreach (Transform child in transform)
            {
                string baseName = StripUnityDuplicateSuffix(child.name);
                if (System.Enum.TryParse<LPCLayer>(baseName, out var layer))
                {
                    var sr = child.GetComponent<SpriteRenderer>();
                    if (sr == null) { deadChildren.Add(child.gameObject); continue; }
                    // First-come-wins; destroy any later duplicates of the same layer
                    if (renderers.ContainsKey(layer)) deadChildren.Add(child.gameObject);
                    else { renderers[layer] = sr; child.name = baseName; }  // Restore canonical name
                }
                // else: not an LPC child (effect, marker, etc.) - leave alone
            }
            foreach (var dead in deadChildren)
            {
                if (Application.isPlaying) Destroy(dead);
                else DestroyImmediate(dead);
            }

            ApplySlot(LPCLayer.Body,       bodySheet);
            ApplySlot(LPCLayer.Eyes,       eyesSheet);
            ApplySlot(LPCLayer.Hair,       hairSheet);
            ApplySlot(LPCLayer.FacialHair, facialHairSheet);
            ApplySlot(LPCLayer.Head,       headSheet);
            ApplySlot(LPCLayer.Neck,       neckSheet);
            ApplySlot(LPCLayer.Torso,      torsoSheet);
            ApplySlot(LPCLayer.Legs,       legsSheet);
            ApplySlot(LPCLayer.Feet,       feetSheet);
            ApplySlot(LPCLayer.Belt,       beltSheet);
            ApplySlot(LPCLayer.Arms,       armsSheet);
            ApplySlot(LPCLayer.Shoulders,  shouldersSheet);
            ApplySlot(LPCLayer.Shield,     shieldSheet);
            ApplySlot(LPCLayer.WeaponMain, weaponSheet);

            // Push current frame to all renderers
            ApplyCurrentFrame();
        }

        private void ApplySlot(LPCLayer layer, LPCSpriteSheet sheet)
        {
            if (sheet == null)
            {
                // Slot empty - remove its renderer if it exists
                if (renderers.TryGetValue(layer, out var existing))
                {
                    if (existing != null)
                    {
                        if (Application.isPlaying)
                            Destroy(existing.gameObject);
                        else
                            DestroyImmediate(existing.gameObject);
                    }
                    renderers.Remove(layer);
                }
                return;
            }

            // Get or create renderer for this layer
            if (!renderers.TryGetValue(layer, out var renderer) || renderer == null)
            {
                var child = new GameObject(layer.ToString());
                child.transform.SetParent(transform, false);
                renderer = child.AddComponent<SpriteRenderer>();
                renderers[layer] = renderer;
            }

            renderer.spriteSortPoint = SpriteSortPoint.Pivot;
            ApplyRendererSorting(renderer, layer);
            // Pre-slice this sheet so we can render frames immediately
            LPCSpriteSlicer.EnsureSliced(sheet);
        }

        /// <summary>
        /// Set the current animation, direction and frame. Updates every renderer.
        /// Called by LPCAnimator each tick.
        /// </summary>
        public void SetFrame(LPCAnimation anim, LPCDirection dir, int frameIndex)
        {
            currentAnimation = anim;
            currentDirection = dir;
            currentFrame = frameIndex;
            ApplyCurrentFrame();
        }

        private void ApplyCurrentFrame()
        {
            foreach (var kvp in renderers)
            {
                var sheet = GetSheetForLayer(kvp.Key);
                if (sheet == null || kvp.Value == null) continue;

                // If this sheet doesn't support the requested animation, fall back to Idle
                var anim = sheet.Supports(currentAnimation) ? currentAnimation : LPCAnimation.Idle;
                kvp.Value.sprite = LPCSpriteSlicer.GetFrame(sheet, anim, currentDirection, currentFrame);
            }
        }

        private LPCSpriteSheet GetSheetForLayer(LPCLayer layer) => layer switch
        {
            LPCLayer.Body       => bodySheet,
            LPCLayer.Eyes       => eyesSheet,
            LPCLayer.Hair       => hairSheet,
            LPCLayer.FacialHair => facialHairSheet,
            LPCLayer.Head       => headSheet,
            LPCLayer.Neck       => neckSheet,
            LPCLayer.Torso      => torsoSheet,
            LPCLayer.Legs       => legsSheet,
            LPCLayer.Feet       => feetSheet,
            LPCLayer.Belt       => beltSheet,
            LPCLayer.Arms       => armsSheet,
            LPCLayer.Shoulders  => shouldersSheet,
            LPCLayer.Shield     => shieldSheet,
            LPCLayer.WeaponMain => weaponSheet,
            _ => null
        };

        /// <summary>
        /// Strip Unity's auto-generated " (n)" suffix from a name.
        /// "Body (3)" -> "Body". Used so duplicate children still resolve to a layer.
        /// </summary>
        private static string StripUnityDuplicateSuffix(string name)
        {
            int paren = name.IndexOf(" (");
            if (paren > 0 && name.EndsWith(")")) return name.Substring(0, paren);
            return name;
        }

        /// <summary>
        /// Equip an item into the appropriate slot. Pass null to unequip.
        /// </summary>
        public void SetEquipment(LPCLayer layer, LPCSpriteSheet sheet)
        {
            switch (layer)
            {
                case LPCLayer.Body:       bodySheet = sheet; break;
                case LPCLayer.Eyes:       eyesSheet = sheet; break;
                case LPCLayer.Hair:       hairSheet = sheet; break;
                case LPCLayer.FacialHair: facialHairSheet = sheet; break;
                case LPCLayer.Head:       headSheet = sheet; break;
                case LPCLayer.Neck:       neckSheet = sheet; break;
                case LPCLayer.Torso:      torsoSheet = sheet; break;
                case LPCLayer.Legs:       legsSheet = sheet; break;
                case LPCLayer.Feet:       feetSheet = sheet; break;
                case LPCLayer.Belt:       beltSheet = sheet; break;
                case LPCLayer.Arms:       armsSheet = sheet; break;
                case LPCLayer.Shoulders:  shouldersSheet = sheet; break;
                case LPCLayer.Shield:     shieldSheet = sheet; break;
                case LPCLayer.WeaponMain: weaponSheet = sheet; break;
            }
            RebuildRenderers();
        }

        private void ApplySortingOrders()
        {
            int baseOrder = Mathf.RoundToInt(-transform.position.y * sortPixelsPerUnitY) + sortBaseOffset;
            foreach (var kvp in renderers)
            {
                if (kvp.Value == null) continue;
                kvp.Value.sortingOrder = baseOrder + GetRelativeSortOrder(kvp.Key);
            }
        }

        private void ApplyRendererSorting(SpriteRenderer renderer, LPCLayer layer)
        {
            int baseOrder = Mathf.RoundToInt(-transform.position.y * sortPixelsPerUnitY) + sortBaseOffset;
            renderer.sortingOrder = baseOrder + GetRelativeSortOrder(layer);
        }

        private static int GetRelativeSortOrder(LPCLayer layer) => layer switch
        {
            LPCLayer.Shadow        => -2,
            LPCLayer.Body          => 0,
            LPCLayer.Eyes          => 1,
            LPCLayer.Hair          => 2,
            LPCLayer.FacialHair    => 3,
            LPCLayer.Head          => 4,
            LPCLayer.Neck          => 5,
            LPCLayer.Torso         => 6,
            LPCLayer.Legs          => 7,
            LPCLayer.Feet          => 8,
            LPCLayer.Belt          => 9,
            LPCLayer.Arms          => 10,
            LPCLayer.Shoulders     => 11,
            LPCLayer.WeaponBack    => 12,
            LPCLayer.Shield        => 13,
            LPCLayer.WeaponMain    => 14,
            LPCLayer.OverlayEffect => 15,
            _ => 0
        };
    }
}
