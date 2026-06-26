using System.Collections.Generic;
using IsoCore.Foundation;
using UnityEngine;

namespace LitIso.CharacterCreator
{
    /// <summary>
    /// Drop-in visual driver for a layered (paperdoll) character (v3, LPC
    /// wardrobe). Same direction/row mapping as IsoCore.Foundation.PlayerAnimator
    /// (8 rows S,SE,E,NE,N,NW,W,SW; row 0 faces camera).
    ///
    /// Supports all 6 classic LPC animations baked by CharacterCompositor:
    /// "walk" (doubles as idle/standing + the walk cycle, frame `idleFrame`
    /// while still, frames walkStartFrame.. while moving — driven automatically
    /// by movement every frame) plus "cast", "thrust", "slash", "shoot", "hurt"
    /// — one-shot action animations triggered via <see cref="PlayOneShot"/>,
    /// after which playback returns to "walk" automatically.
    ///
    /// Each animation is baked lazily and cached per appearance; outfit /
    /// equipment changes (<see cref="Apply"/>) clear the cache and re-bake.
    ///
    /// This is what makes the character built in CharacterCreatorUI the actual
    /// sprite rendered for the player: FoundationBootstrap adds this component
    /// to the player GameObject (alongside/instead of PlayerAnimator) and it
    /// loads the saved LayeredAppearance on Awake.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class LayeredCharacterAnimator : MonoBehaviour
    {
        [Tooltip("Load saved appearance and bake on Awake.")]
        public bool autoLoadSavedAppearance = true;
        [Tooltip("Tint with the world ambient material (enable for in-world use, leave off for UI previews).")]
        public bool useWorldAmbientTint = false;

        const string WalkAnim = "walk";

        SpriteRenderer _sr;
        IsoFoundationPlayer _player;
        LayeredAppearance _appearance;

        // Lazily-baked per-animation results for the current appearance.
        readonly Dictionary<string, CharacterCompositor.BakeResult> _bakes = new();

        int _row;                 // current facing row (0 = S ... 7 = SW)
        int _walkFrame;           // offset into the walk cycle (0-based)
        float _timer;
        Vector2 _externalDir = Vector2.down;
        bool _externalMoving;

        // One-shot action animation state ("cast"/"thrust"/"slash"/"shoot"/"hurt").
        string _activeAnim = WalkAnim;
        int _oneShotFrame;
        float _oneShotTimer;
        System.Action _onOneShotComplete;

        public LayeredAppearance Appearance => _appearance;

        /// <summary>True while a one-shot action animation (not "walk") is playing.</summary>
        public bool IsPlayingOneShot => _activeAnim != WalkAnim;

        public string CurrentAnimation => _activeAnim;

        void Awake()
        {
            _sr = GetComponent<SpriteRenderer>();
            _player = GetComponent<IsoFoundationPlayer>();
            if (useWorldAmbientTint)
                _sr.sharedMaterial = SpriteAmbient.Material;
            if (autoLoadSavedAppearance)
                Apply(LayeredAppearance.LoadOrDefault());
        }

        /// <summary>Re-bake (lazily) and display the given appearance.</summary>
        public void Apply(LayeredAppearance appearance)
        {
            _appearance = appearance ?? new LayeredAppearance();
            ClearBakes();
            _activeAnim = WalkAnim;
            _walkFrame = 0;
            _timer = 0f;
            _oneShotFrame = 0;
            _oneShotTimer = 0f;
            _onOneShotComplete = null;
            var baked = GetBake(WalkAnim);
            if (_sr != null && baked != null && baked.sprites.Length > 0)
                _sr.sprite = baked.sprites[_row * baked.framesPerRow + baked.idleFrame];
        }

        /// <summary>Drive facing/walk externally (e.g. creator preview).</summary>
        public void SetMotion(Vector2 dir, bool moving)
        {
            if (dir.sqrMagnitude > 0.0001f) _externalDir = dir;
            _externalMoving = moving;
        }

        /// <summary>Directly face a row (0 = S ... 7 = SW). Preview helper.</summary>
        public void SetRow(int row) => _row = ((row % 8) + 8) % 8;

        public int CurrentRow => _row;

        /// <summary>
        /// Play a one-shot action animation ("cast"/"thrust"/"slash"/"shoot"/"hurt")
        /// once at its facing row, then return to "walk"/idle. Calls
        /// <paramref name="onComplete"/> when finished (or immediately if the
        /// animation/bake is unavailable).
        /// </summary>
        public void PlayOneShot(string animId, System.Action onComplete = null)
        {
            var baked = GetBake(animId);
            if (baked == null) { onComplete?.Invoke(); return; }
            _activeAnim = animId;
            _oneShotFrame = 0;
            _oneShotTimer = 0f;
            _onOneShotComplete = onComplete;
            _sr.sprite = baked.sprites[_row * baked.framesPerRow + 0];
        }

        /// <summary>Cancel any in-progress one-shot and return to walk/idle immediately.</summary>
        public void CancelOneShot()
        {
            if (_activeAnim == WalkAnim) return;
            _activeAnim = WalkAnim;
            _onOneShotComplete = null;
        }

        // Same mapping as PlayerAnimator: sectors start at East going CCW;
        // sheet starts at South going clockwise => row = (sector + 2) % 8.
        static int RowForDirection(Vector2 dir)
        {
            float ang = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            if (ang < 0f) ang += 360f;
            int sector = Mathf.RoundToInt(ang / 45f) & 7;
            return (sector + 2) % 8;
        }

        void Update()
        {
            bool moving = _player != null ? _player.IsMoving : _externalMoving;
            Vector2 dir = _player != null ? _player.MoveDir : _externalDir;
            if (moving) _row = RowForDirection(dir);

            if (_activeAnim != WalkAnim)
            {
                UpdateOneShot();
                return;
            }

            var baked = GetBake(WalkAnim);
            if (baked == null || baked.sprites.Length == 0) return;

            int walkFrameCount = Mathf.Max(1, baked.framesPerRow - baked.walkStartFrame);
            int frame;
            if (moving)
            {
                float spf = 1f / Mathf.Max(0.01f, baked.fps);
                _timer += Time.deltaTime;
                while (_timer >= spf)
                {
                    _timer -= spf;
                    _walkFrame = (_walkFrame + 1) % walkFrameCount;
                }
                frame = baked.walkStartFrame + _walkFrame;
            }
            else
            {
                _timer = 0f;
                _walkFrame = 0;
                frame = baked.idleFrame;
            }
            _sr.sprite = baked.sprites[_row * baked.framesPerRow + frame];
        }

        void UpdateOneShot()
        {
            var baked = GetBake(_activeAnim);
            if (baked == null || baked.sprites.Length == 0) { _activeAnim = WalkAnim; return; }

            float spf = 1f / Mathf.Max(0.01f, baked.fps);
            _oneShotTimer += Time.deltaTime;
            while (_oneShotTimer >= spf)
            {
                _oneShotTimer -= spf;
                _oneShotFrame++;
                if (_oneShotFrame >= baked.framesPerRow)
                {
                    // finished: drop back to walk/idle and notify.
                    var cb = _onOneShotComplete;
                    _onOneShotComplete = null;
                    _activeAnim = WalkAnim;
                    _walkFrame = 0;
                    _timer = 0f;
                    var walk = GetBake(WalkAnim);
                    if (walk != null && walk.sprites.Length > 0)
                        _sr.sprite = walk.sprites[_row * walk.framesPerRow + walk.idleFrame];
                    cb?.Invoke();
                    return;
                }
            }
            _sr.sprite = baked.sprites[_row * baked.framesPerRow + _oneShotFrame];
        }

        /// <summary>Get (lazily baking if needed) the BakeResult for an animation.</summary>
        CharacterCompositor.BakeResult GetBake(string animId)
        {
            if (_appearance == null) return null;
            if (_bakes.TryGetValue(animId, out var existing)) return existing;
            var baked = CharacterCompositor.Bake(_appearance, animId);
            _bakes[animId] = baked;
            return baked;
        }

        void ClearBakes()
        {
            foreach (var b in _bakes.Values)
                if (b?.texture != null) Destroy(b.texture);
            _bakes.Clear();
        }

        void OnDestroy() => ClearBakes();
    }
}
