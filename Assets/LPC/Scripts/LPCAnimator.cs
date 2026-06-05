// LPCAnimator.cs - Drives the LPCCharacter through animation frames over time.
//
// Handles:
//   - Frame-rate based animation playback
//   - Looping vs one-shot animations (returns to Idle automatically)
//   - 8-direction movement input snapped to 4 cardinal sprite directions
//   - State change events
//
// External code (player controller, AI) calls Play(animation) and FaceDirection(angle).

using System;
using UnityEngine;

namespace LITISO.LPC
{
    [RequireComponent(typeof(LPCCharacter))]
    public class LPCAnimator : MonoBehaviour
    {
        [Header("Defaults")]
        public LPCAnimation defaultAnimation = LPCAnimation.Idle;
        public LPCDirection startingDirection = LPCDirection.South;

        [Header("Runtime")]
        [SerializeField, Tooltip("Currently playing animation")]
        private LPCAnimation playingAnim;
        [SerializeField]
        private LPCDirection facing;
        [SerializeField]
        private int frameIndex;
        [SerializeField]
        private float frameTimer;

        private LPCCharacter character;

        /// <summary>Fires when a non-looping animation finishes.</summary>
        public event Action<LPCAnimation> OnAnimationComplete;

        void Awake()
        {
            character = GetComponent<LPCCharacter>();
            facing = startingDirection;
            Play(defaultAnimation);
        }

        void Update()
        {
            if (!LPCAnimationData.Animations.TryGetValue(playingAnim, out var info))
                return;

            frameTimer += Time.deltaTime;
            float frameDuration = 1f / info.FPS;

            while (frameTimer >= frameDuration)
            {
                frameTimer -= frameDuration;
                frameIndex++;

                if (frameIndex >= info.FrameCount)
                {
                    if (info.Loops)
                    {
                        frameIndex = 0;
                    }
                    else
                    {
                        // Non-looping animation finished
                        frameIndex = info.FrameCount - 1;
                        var finished = playingAnim;
                        // Auto-return to idle after one-shot animations
                        Play(defaultAnimation);
                        OnAnimationComplete?.Invoke(finished);
                        return;
                    }
                }

                character.SetFrame(playingAnim, facing, frameIndex);
            }
        }

        /// <summary>Start playing a specific animation from frame 0.</summary>
        public void Play(LPCAnimation anim)
        {
            playingAnim = anim;
            frameIndex = 0;
            frameTimer = 0f;
            character.SetFrame(anim, facing, 0);
        }

        /// <summary>Set the cardinal direction the character is facing.</summary>
        public void FaceDirection(LPCDirection dir)
        {
            facing = dir;
            character.SetFrame(playingAnim, facing, frameIndex);
        }

        /// <summary>
        /// Snap a 2D movement vector to the nearest cardinal direction.
        /// This is the 8-direction -> 4-direction conversion mentioned in the design:
        /// diagonal movement displays the nearest cardinal sprite.
        /// </summary>
        public void FaceMovement(Vector2 movement)
        {
            if (movement.sqrMagnitude < 0.001f) return;

            float angle = Mathf.Atan2(movement.y, movement.x) * Mathf.Rad2Deg;
            // Bins: East (-45..45), North (45..135), West (135..-135), South (-135..-45)
            if      (angle >= -45f && angle <  45f)  FaceDirection(LPCDirection.East);
            else if (angle >=  45f && angle < 135f)  FaceDirection(LPCDirection.North);
            else if (angle >= 135f || angle < -135f) FaceDirection(LPCDirection.West);
            else                                      FaceDirection(LPCDirection.South);
        }

        /// <summary>Convenience: play Walk while moving in a direction, Idle when stopped.</summary>
        public void SetMovement(Vector2 movement)
        {
            if (movement.sqrMagnitude > 0.001f)
            {
                FaceMovement(movement);
                if (playingAnim != LPCAnimation.Walk) Play(LPCAnimation.Walk);
            }
            else
            {
                if (playingAnim == LPCAnimation.Walk) Play(LPCAnimation.Idle);
            }
        }

        public LPCAnimation CurrentAnimation => playingAnim;
        public LPCDirection CurrentFacing    => facing;
    }
}
