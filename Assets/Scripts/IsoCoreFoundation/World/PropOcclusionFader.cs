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
        bool _occluding;
        float _nextCheckTime;

        const float CheckInterval = 0.2f;   // perf: occlusion is visual polish; 5Hz is plenty

        // Shared across all props: the one player sprite. Lazily found, re-found if destroyed.
        static SpriteRenderer s_playerSr;

        void Awake()
        {
            _sr = GetComponent<SpriteRenderer>();
            _nextCheckTime = Time.time + Mathf.Abs(GetInstanceID() % 17) * 0.005f;
        }

        static SpriteRenderer PlayerSprite()
        {
            if (s_playerSr != null) return s_playerSr; // Unity-null catches destroyed players
            var player = Object.FindFirstObjectByType<IsoFoundationPlayer>();
            if (player != null) s_playerSr = player.GetComponent<SpriteRenderer>();
            return s_playerSr;
        }

        void LateUpdate()
        {
            if (Time.time >= _nextCheckTime)
            {
                _nextCheckTime = Time.time + CheckInterval;
                var psr = PlayerSprite();
                // Perf (audit 2026-06-11): cheap squared-distance gate before the
                // bounds reads — distant props can never occlude the player, and
                // bounds property reads accumulate across hundreds of props.
                if (psr == null || !psr.enabled || !_sr.enabled ||
                    (psr.transform.position - transform.position).sqrMagnitude > 16f)
                {
                    _occluding = false;
                }
                else
                {
                    _occluding =
                        _sr.sortingLayerID == psr.sortingLayerID &&
                        _sr.sortingOrder > psr.sortingOrder &&   // this prop draws in front of the player
                        _sr.bounds.Intersects(psr.bounds);        // and overlaps the player on screen
                }
            }

            float target = _occluding ? fadedAlpha : 1f;
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
