// LPCAnimationData.cs - Static metadata about LPC universal sheet layout.
//
// In a universal LPC sheet, each frame is 64x64 pixels. Animations are stacked
// vertically as rows. Each animation block contains 4 rows (one per direction).
// This file encodes the offsets so we can compute frame coordinates from
// (animation, direction, frame_index).

using System.Collections.Generic;

namespace LITISO.LPC
{
    public static class LPCAnimationData
    {
        public const int FrameSize = 64;
        public const int FramesPerRow = 13;   // Maximum (some animations use fewer)

        /// <summary>
        /// Defines a single animation's location in the universal sheet:
        ///   StartRow = first row of the 4-direction block (multiplied by 4 internally)
        ///   FrameCount = number of frames per direction
        ///   Loops = true if it should loop continuously
        ///   FPS = animation speed
        /// </summary>
        public struct AnimInfo
        {
            public int StartRow;      // Row index of the FIRST row (North) of this animation
            public int FrameCount;    // Frames per direction
            public bool Loops;
            public float FPS;
        }

        // ---------------------------------------------------------------------
        // LPC Universal sheet layout, per https://lpc.opengameart.org/static/LPC-Style-Guide/build/assets.html
        //
        // BASE layout (21 rows / 1344px tall) - what most equipment sheets have:
        //   Rows 0-3   Spellcast  (4 dirs x 7 frames)
        //   Rows 4-7   Thrust     (4 dirs x 8 frames)
        //   Rows 8-11  Walk       (4 dirs x 9 frames)
        //   Rows 12-15 Slash      (4 dirs x 6 frames)
        //   Rows 16-19 Shoot      (4 dirs x 13 frames)
        //   Row  20    Hurt       (south only, 6 frames)
        //
        // EXTENDED rows (some body sheets are 2944px = 46 rows total) add:
        //   Rows 21+   Idle, Run, Jump, Sit, Emote, Climb, Combat (layout varies)
        //
        // We only encode the BASE rows here. Sheets opt-in to each animation via
        // LPCSpriteSheet.supportedAnimations. "Idle" is synthesized from Walk
        // frame 0 because base sheets don't contain a real idle animation.
        // ---------------------------------------------------------------------
        public static readonly Dictionary<LPCAnimation, AnimInfo> Animations = new()
        {
            { LPCAnimation.Spellcast, new AnimInfo { StartRow = 0,  FrameCount = 7,  Loops = false, FPS = 12 } },
            { LPCAnimation.Thrust,    new AnimInfo { StartRow = 4,  FrameCount = 8,  Loops = false, FPS = 14 } },
            { LPCAnimation.Walk,      new AnimInfo { StartRow = 8,  FrameCount = 9,  Loops = true,  FPS = 12 } },
            { LPCAnimation.Slash,     new AnimInfo { StartRow = 12, FrameCount = 6,  Loops = false, FPS = 14 } },
            { LPCAnimation.Shoot,     new AnimInfo { StartRow = 16, FrameCount = 13, Loops = false, FPS = 16 } },
            { LPCAnimation.Hurt,      new AnimInfo { StartRow = 20, FrameCount = 6,  Loops = false, FPS = 10 } },
            // Idle = synthesized from Walk frame 0. Slicer alias: when we ask
            // for Idle, we return Walk[0]. See LPCSpriteSlicer.GetFrame().
            { LPCAnimation.Idle,      new AnimInfo { StartRow = 8,  FrameCount = 1,  Loops = true,  FPS = 1  } },
        };

        /// <summary>
        /// Returns the row index (0-based) in the sprite sheet for a given animation+direction.
        /// </summary>
        public static int GetRow(LPCAnimation anim, LPCDirection dir)
        {
            return Animations[anim].StartRow + (int)dir;
        }

        /// <summary>
        /// Returns the pixel rect (in sheet coordinates) of a specific frame.
        /// </summary>
        public static UnityEngine.Rect GetFrameRect(LPCAnimation anim, LPCDirection dir, int frameIndex)
        {
            int row = GetRow(anim, dir);
            int x = frameIndex * FrameSize;
            int y = row * FrameSize;
            return new UnityEngine.Rect(x, y, FrameSize, FrameSize);
        }
    }
}
