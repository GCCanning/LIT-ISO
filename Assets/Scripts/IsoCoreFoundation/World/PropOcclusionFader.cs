using UnityEngine;

namespace IsoCore.Foundation
{
    /// <summary>
    /// Fades this prop (tree, bush, rock…) to semi-transparent while it visually occludes
    /// the player — i.e. the prop renders in front of the player (same sorting layer, higher
    /// order) AND their sprite bounds overlap — so the player is never lost behind a tree.
    /// Opacity eases back to full when the player steps clear. Purely visual; reads only.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class PropOcclusionFader : MonoBehaviour
    {
        [Tooltip("Alpha the prop fades to while it covers the player.")]
        public float fadedAlpha = 0.4f;
        [Tooltip("How fast opacity eases toward its target (alpha units per second).")]
        public float fadeSpeed = 8f;

        SpriteRenderer _sr;
        float _alpha = 1f;

        // Shared across all props: the one player sprite. Lazily found, re-found if destroyed.
        static SpriteRenderer s_playerSr;

        void Awake() => _sr = GetComponent<SpriteRenderer>();

        static SpriteRenderer PlayerSprite()
        {
            if (s_playerSr != null) return s_playerSr; // Unity-null catches destroyed players
            var player = Object.FindFirstObjectByType<IsoFoundationPlayer>();
            if (player != null) s_playerSr = player.GetComponent<SpriteRenderer>();
            return s_playerSr;
        }

        void LateUpdate()
        {
            var psr = PlayerSprite();
            bool occluding =
                psr != null && psr.enabled && _sr.enabled &&
                _sr.sortingLayerID == psr.sortingLayerID &&
                _sr.sortingOrder > psr.sortingOrder &&   // this prop draws in front of the player
                _sr.bounds.Intersects(psr.bounds);        // and overlaps the player on screen

            float target = occluding ? fadedAlpha : 1f;
            if (!Mathf.Approximately(_alpha, target))
            {
                _alpha = Mathf.MoveTowards(_alpha, target, fadeSpeed * Time.deltaTime);
                var c = _sr.color;
                c.a = _alpha;
                _sr.color = c;
            }
        }
    }
}
