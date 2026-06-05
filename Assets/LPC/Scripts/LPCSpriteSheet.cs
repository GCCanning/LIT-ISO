// LPCSpriteSheet.cs - ScriptableObject wrapping one LPC PNG sheet.
//
// A sprite sheet is a single PNG that contains all animations for ONE layer
// (e.g. male body in light skin, plain black hair, leather chest armor).
//
// At runtime LPCCharacter loads multiple LPCSpriteSheets - one per slot
// (body, hair, torso, etc.) - and renders them as stacked SpriteRenderers
// that all play the same animation in sync.

using UnityEngine;

namespace LITISO.LPC
{
    [CreateAssetMenu(menuName = "LIT-ISO/LPC/Sprite Sheet")]
    public class LPCSpriteSheet : ScriptableObject
    {
        [Header("Identity")]
        public string assetId;          // Stable ID, e.g. "body_male_light"
        public string displayName;      // "Light Skinned Male Body"
        public LPCLayer layer;          // Where this sits in the z-order stack

        [Header("Source PNG")]
        [Tooltip("Source texture. Must be set to 'Texture Type: Sprite (2D and UI)' with 'Sprite Mode: Multiple' and sliced into 64x64 cells.")]
        public Texture2D texture;

        [Header("Supported Animations")]
        [Tooltip("Which animations this sheet contains. If a sheet lacks an animation, that frame will be transparent.")]
        public LPCAnimation[] supportedAnimations = {
            LPCAnimation.Walk,
            LPCAnimation.Idle,
            LPCAnimation.Slash
        };

        [Header("Compatibility")]
        public LPCBodyType[] compatibleBodyTypes;

        /// <summary>
        /// Cached Sprites generated from the texture (one per frame).
        /// Generated on demand by LPCSpriteSlicer.
        /// </summary>
        [System.NonSerialized]
        public Sprite[,,] frameCache;   // [animation, direction, frameIndex]

        /// <summary>Quick lookup if this sheet supports a specific animation.</summary>
        public bool Supports(LPCAnimation anim)
        {
            foreach (var a in supportedAnimations) if (a == anim) return true;
            return false;
        }
    }
}
