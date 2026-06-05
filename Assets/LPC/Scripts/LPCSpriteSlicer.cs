// LPCSpriteSlicer.cs - Runtime slicer that converts a Texture2D into per-frame Sprites.
//
// Why runtime instead of editor-time slicing: LPC sheets vary in height
// (some are 21 rows = 1344px BASE layout, some are 46 rows = 2944px EXTENDED).
// This slicer doesn't assume any specific height - it computes Y from the
// universal row offsets and only slices what fits in the actual texture.
//
// Special case: Idle is synthesized from Walk frame 0, since base LPC sheets
// don't contain a real idle animation. This lets us treat Idle as a valid
// state in higher-level code (LPCAnimator) without each sheet needing extra rows.

using UnityEngine;

namespace LITISO.LPC
{
    public static class LPCSpriteSlicer
    {
        /// <summary>
        /// Slice the texture on a sheet into per-frame Sprites and cache them.
        /// Safe to call multiple times - returns immediately if already sliced.
        /// </summary>
        public static void EnsureSliced(LPCSpriteSheet sheet)
        {
            if (sheet == null || sheet.texture == null) return;
            if (sheet.frameCache != null) return;

            int animCount = System.Enum.GetValues(typeof(LPCAnimation)).Length;
            int dirCount = 4;
            int maxFrames = LPCAnimationData.FramesPerRow;

            sheet.frameCache = new Sprite[animCount, dirCount, maxFrames];

            foreach (LPCAnimation anim in System.Enum.GetValues(typeof(LPCAnimation)))
            {
                if (!sheet.Supports(anim)) continue;
                if (!LPCAnimationData.Animations.TryGetValue(anim, out var info)) continue;

                for (int dir = 0; dir < dirCount; dir++)
                {
                    int row = info.StartRow + dir;
                    int rowTopPx = row * LPCAnimationData.FrameSize;

                    // Skip rows that don't fit in this sheet's actual height.
                    if (rowTopPx + LPCAnimationData.FrameSize > sheet.texture.height)
                        continue;

                    for (int frame = 0; frame < info.FrameCount; frame++)
                    {
                        int xPx = frame * LPCAnimationData.FrameSize;
                        if (xPx + LPCAnimationData.FrameSize > sheet.texture.width)
                            continue;

                        // Unity texture Y is bottom-up. Our row index is top-down.
                        float flippedY = sheet.texture.height - rowTopPx - LPCAnimationData.FrameSize;
                        var unityRect = new Rect(xPx, flippedY, LPCAnimationData.FrameSize, LPCAnimationData.FrameSize);

                        sheet.frameCache[(int)anim, dir, frame] = Sprite.Create(
                            sheet.texture,
                            unityRect,
                            new Vector2(0.5f, 0.125f),  // Pivot near feet (X center, Y 1/8 from bottom)
                            LPCAnimationData.FrameSize, // Pixels per unit = 64 = 1 frame per Unity unit
                            0,
                            SpriteMeshType.FullRect,
                            Vector4.zero,
                            false
                        );
                    }
                }
            }
        }

        /// <summary>
        /// Get the Sprite for a specific animation, direction and frame.
        /// Returns null if the sheet doesn't contain that animation/frame.
        ///
        /// Special case: requesting Idle returns Walk[0] of the same direction
        /// (base LPC sheets don't have a dedicated idle row).
        /// </summary>
        public static Sprite GetFrame(LPCSpriteSheet sheet, LPCAnimation anim, LPCDirection dir, int frame)
        {
            if (sheet == null) return null;
            EnsureSliced(sheet);

            // Idle -> first frame of Walk (universal trick for base LPC sheets)
            if (anim == LPCAnimation.Idle)
            {
                return sheet.frameCache[(int)LPCAnimation.Walk, (int)dir, 0];
            }

            // Clamp frame to what this animation actually has
            if (!LPCAnimationData.Animations.TryGetValue(anim, out var info))
                return null;
            if (frame >= info.FrameCount) frame = info.FrameCount - 1;
            if (frame < 0) frame = 0;

            return sheet.frameCache[(int)anim, (int)dir, frame];
        }
    }
}
