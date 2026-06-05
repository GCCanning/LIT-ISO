// LPCPlayerBridge.cs - Adapts IsoPlayerController's movement to drive LPCAnimator.
//
// This is the glue between the existing LIT-ISO player controller and the new
// LPC layered character system. We DO NOT modify IsoPlayerController. Instead
// this component watches the player's Rigidbody2D velocity each frame and
// forwards it to LPCAnimator.SetMovement(), which handles facing + walk/idle
// state automatically.
//
// Why this pattern: the golden-path doc explicitly calls IsoPlayerController
// the active runtime system - we shouldn't change its internals. A bridge keeps
// the LPC system optional and removable.

using UnityEngine;

namespace LITISO.LPC
{
    [RequireComponent(typeof(LPCAnimator))]
    public class LPCPlayerBridge : MonoBehaviour
    {
        [Header("Source of movement")]
        [Tooltip("Rigidbody2D to read velocity from. Auto-found on the player if left null.")]
        public Rigidbody2D source;

        [Tooltip("Below this velocity magnitude the character is considered idle.")]
        public float idleThreshold = 0.05f;

        private LPCAnimator anim;

        void Awake()
        {
            anim = GetComponent<LPCAnimator>();

            if (source == null)
                source = GetComponent<Rigidbody2D>();

            // Common case: the player's Rigidbody2D is on the root and this
            // bridge is on a child (the LPC sprite root). Walk up to find it.
            if (source == null && transform.parent != null)
                source = transform.parent.GetComponentInParent<Rigidbody2D>();
        }

        void Update()
        {
            if (source == null || anim == null) return;

            Vector2 velocity = source.linearVelocity;
            if (velocity.sqrMagnitude < idleThreshold * idleThreshold)
                velocity = Vector2.zero;

            anim.SetMovement(velocity);
        }

        /// <summary>
        /// External code (combat, AI) can trigger an attack animation.
        /// The animator returns to Idle automatically after the one-shot finishes.
        /// </summary>
        public void TriggerAttack(LPCAnimation attackAnim = LPCAnimation.Slash)
        {
            anim?.Play(attackAnim);
        }

        public void TriggerHurt() => anim?.Play(LPCAnimation.Hurt);
        public void TriggerCast() => anim?.Play(LPCAnimation.Spellcast);
    }
}
