// LPCDemoController.cs - Quick keyboard demo so you can verify the LPC system works.
//
// Attach this to a GameObject that also has LPCCharacter + LPCAnimator.
// WASD = move (snaps to 4 cardinal sprite directions)
// Space = play Slash animation
// Q = play Spellcast
// E = play Thrust
// H = play Hurt
//
// This is a placeholder - your real player controller should call
// animator.SetMovement(velocity) and animator.Play(LPCAnimation.X) directly.

using UnityEngine;

namespace LITISO.LPC
{
    [RequireComponent(typeof(LPCAnimator))]
    public class LPCDemoController : MonoBehaviour
    {
        public float moveSpeed = 2f;

        private LPCAnimator animator;

        void Awake()
        {
            animator = GetComponent<LPCAnimator>();
        }

        void Update()
        {
            // Read WASD input
            float h = 0f, v = 0f;
            if (Input.GetKey(KeyCode.A)) h -= 1f;
            if (Input.GetKey(KeyCode.D)) h += 1f;
            if (Input.GetKey(KeyCode.W)) v += 1f;
            if (Input.GetKey(KeyCode.S)) v -= 1f;

            Vector2 move = new Vector2(h, v);
            if (move.sqrMagnitude > 1f) move.Normalize();

            // Move the transform
            transform.position += (Vector3)(move * moveSpeed * Time.deltaTime);

            // Tell the animator how we're moving (handles facing + walk/idle switching)
            animator.SetMovement(move);

            // One-shot action triggers
            if (Input.GetKeyDown(KeyCode.Space)) animator.Play(LPCAnimation.Slash);
            if (Input.GetKeyDown(KeyCode.Q))     animator.Play(LPCAnimation.Spellcast);
            if (Input.GetKeyDown(KeyCode.E))     animator.Play(LPCAnimation.Thrust);
            if (Input.GetKeyDown(KeyCode.H))     animator.Play(LPCAnimation.Hurt);
        }
    }
}
