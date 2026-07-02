using System.Collections.Generic;
using IsoCore.Foundation;
using UnityEngine;

namespace LitIso.CharacterCreator
{
    /// <summary>
    /// Visual driver for a layered (paperdoll) character (v4, LPC runtime-palette
    /// wardrobe). Source art is 4-directional (rowOrder N,W,S,E). East is rendered
    /// by mirroring the West row (SpriteRenderer.flipX); diagonals snap to the
    /// nearest cardinal. Single-row animations (hurt/climb) use the South row.
    ///
    /// Drives "walk" (idle = the separate idle animation when present, else walk
    /// frame 0; cycles the walk band while moving) plus one-shot actions
    /// "spellcast"/"thrust"/"slash"/"shoot"/"hurt" via <see cref="PlayOneShot"/>.
    /// Each animation is baked lazily and cached per appearance; <see cref="Apply"/>
    /// clears the cache and re-bakes. FoundationBootstrap adds this to the player
    /// (via LayeredCharacterPlayerHook) so the created character is what renders.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class LayeredCharacterAnimator : MonoBehaviour, IsoCore.Foundation.ICharacterActionAnimator
    {
        [Tooltip("Load saved appearance and bake on Awake.")]
        public bool autoLoadSavedAppearance = true;
        [Tooltip("Tint with the world ambient material (enable for in-world use, leave off for UI previews).")]
        public bool useWorldAmbientTint = false;

        const string WalkAnim = "walk";
        const string RunAnim  = "run";
        const string IdleAnim = "idle";

        SpriteRenderer _sr;
        IsoFoundationPlayer _player;
        LayeredAppearance _appearance;

        readonly Dictionary<string, CharacterCompositor.BakeResult> _bakes = new();

        // Facing: row index into the baked sheet (0-based, follows catalog.rowOrder)
        // plus a mirror flag for East.
        int _row;
        bool _flip;

        int _walkFrame;
        float _timer;
        Vector2 _externalDir = Vector2.down;
        bool _externalMoving;

        string _activeAnim = WalkAnim;
        int _oneShotFrame;
        float _oneShotTimer;
        System.Action _onOneShotComplete;
        string _queuedOneShot;
        System.Action _queuedOneShotComplete;

        // Cached rowOrder indices (resolved once from the catalog).
        static int s_idxN = -2, s_idxW, s_idxS, s_idxE;

        public LayeredAppearance Appearance => _appearance;
        public bool IsPlayingOneShot => _activeAnim != WalkAnim && _activeAnim != RunAnim;
        public string CurrentAnimation => _activeAnim;

        void Awake()
        {
            _sr = GetComponent<SpriteRenderer>();
            _player = GetComponent<IsoFoundationPlayer>();
            EnsureRowIndices();
            if (useWorldAmbientTint)
                _sr.sharedMaterial = SpriteAmbient.Material;
            if (autoLoadSavedAppearance)
                Apply(LayeredAppearance.LoadOrDefault());
        }

        static void EnsureRowIndices()
        {
            if (s_idxN != -2) return;
            var cat = CharacterLayerCatalog.Instance;
            s_idxN = Mathf.Max(0, cat.RowIndex("N"));
            s_idxW = Mathf.Max(0, cat.RowIndex("W"));
            s_idxS = Mathf.Max(0, cat.RowIndex("S"));
            s_idxE = Mathf.Max(0, cat.RowIndex("E"));
        }

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
            _queuedOneShot = null;
            _queuedOneShotComplete = null;
            ShowIdle();
        }

        public void SetMotion(Vector2 dir, bool moving)
        {
            if (dir.sqrMagnitude > 0.0001f) _externalDir = dir;
            _externalMoving = moving;
        }

        public int CurrentRow => _row;

        /// <summary>Map a world direction to (row, flip), snapping to the nearest cardinal.</summary>
        void SetFacing(Vector2 dir)
        {
            float ang = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            if (ang < 0f) ang += 360f;
            // 0=E, 90=N, 180=W, 270=S. Snap to nearest cardinal.
            int q = Mathf.RoundToInt(ang / 90f) & 3;
            _flip = false;
            switch (q)
            {
                case 0: _row = s_idxW; _flip = true; break; // East = mirror of West
                case 1: _row = s_idxN; break;
                case 2: _row = s_idxW; break;
                default: _row = s_idxS; break;
            }
        }

        /// <summary>Preview helper: cycle through the 4 cardinal facings by index 0..3.</summary>
        public void SetRow(int facing)
        {
            facing = ((facing % 4) + 4) % 4;
            _flip = false;
            switch (facing)
            {
                case 0: _row = s_idxS; break;
                case 1: _row = s_idxW; break;
                case 2: _row = s_idxN; break;
                default: _row = s_idxW; _flip = true; break; // East mirror
            }
        }

        /// <summary>ICharacterActionAnimator — lets Foundation combat code drive body anims.</summary>
        public void PlayActionAnim(string animId) => PlayOneShot(animId);

        public void PlayOneShot(string animId, System.Action onComplete = null)
        {
            if (IsPlayingOneShot)
            {
                // Repeated held input should not restart frame zero. A different
                // action waits for the next clean one-shot boundary.
                if (_activeAnim == animId)
                    return;
                _queuedOneShot = animId;
                _queuedOneShotComplete = onComplete;
                return;
            }
            var baked = GetBake(animId);
            if (baked == null) { onComplete?.Invoke(); return; }
            _activeAnim = animId;
            _oneShotFrame = 0;
            _oneShotTimer = 0f;
            _onOneShotComplete = onComplete;
            SetSprite(baked, 0);
        }

        public void CancelOneShot()
        {
            if (_activeAnim == WalkAnim) return;
            _activeAnim = WalkAnim;
            _onOneShotComplete = null;
            _queuedOneShot = null;
            _queuedOneShotComplete = null;
            ShowIdle();
        }

        void Update()
        {
            bool moving   = _player != null ? _player.IsMoving    : _externalMoving;
            bool sprinting = _player != null ? _player.IsSprinting : false;
            Vector2 dir   = _player != null ? _player.MoveDir     : _externalDir;
            if (moving) SetFacing(dir);

            if (_activeAnim != WalkAnim && _activeAnim != RunAnim) { UpdateOneShot(); return; }

            // Switch between walk and run sheets when sprint state changes.
            string loopAnim = (moving && sprinting) ? RunAnim : WalkAnim;
            if (_activeAnim != loopAnim)
            {
                _activeAnim = loopAnim;
                _walkFrame = 0;
                _timer = 0f;
            }

            var baked = GetBake(loopAnim);
            if (baked == null || baked.sprites.Length == 0) return;

            if (moving)
            {
                float spf = 1f / Mathf.Max(0.01f, baked.fps);
                _timer += Time.deltaTime;
                while (_timer >= spf)
                {
                    _timer -= spf;
                    _walkFrame = (_walkFrame + 1) % Mathf.Max(1, baked.framesPerRow);
                }
                SetSprite(baked, _walkFrame);
            }
            else
            {
                _timer = 0f;
                _walkFrame = 0;
                _activeAnim = WalkAnim;
                ShowIdle();
            }
        }

        void UpdateOneShot()
        {
            var baked = GetBake(_activeAnim);
            if (baked == null || baked.sprites.Length == 0) { _activeAnim = WalkAnim; ShowIdle(); return; }

            float spf = 1f / Mathf.Max(0.01f, baked.fps);
            _oneShotTimer += Time.deltaTime;
            while (_oneShotTimer >= spf)
            {
                _oneShotTimer -= spf;
                _oneShotFrame++;
                if (_oneShotFrame >= baked.framesPerRow)
                {
                    var cb = _onOneShotComplete;
                    _onOneShotComplete = null;
                    if (!string.IsNullOrEmpty(_queuedOneShot))
                    {
                        string queued = _queuedOneShot;
                        var queuedComplete = _queuedOneShotComplete;
                        _queuedOneShot = null;
                        _queuedOneShotComplete = null;
                        _activeAnim = WalkAnim;
                        cb?.Invoke();
                        PlayOneShot(queued, queuedComplete);
                        return;
                    }
                    _activeAnim = WalkAnim;
                    _walkFrame = 0;
                    _timer = 0f;
                    ShowIdle();
                    cb?.Invoke();
                    return;
                }
            }
            SetSprite(baked, _oneShotFrame);
        }

        // Show the standing pose: dedicated idle animation if present, else walk frame 0.
        void ShowIdle()
        {
            var idle = GetBake(IdleAnim);
            if (idle != null && idle.sprites.Length > 0) { SetSprite(idle, 0); return; }
            var walk = GetBake(WalkAnim);
            if (walk != null && walk.sprites.Length > 0) SetSprite(walk, 0);
        }

        // Resolve the row for this animation (single-row anims clamp to row 0 = South band),
        // pick the frame, set sprite + mirror.
        void SetSprite(CharacterCompositor.BakeResult baked, int frame)
        {
            if (_sr == null || baked == null || baked.sprites.Length == 0) return;
            int row = baked.rowCount <= 1 ? 0 : Mathf.Clamp(_row, 0, baked.rowCount - 1);
            int f = Mathf.Clamp(frame, 0, baked.framesPerRow - 1);
            int idx = row * baked.framesPerRow + f;
            if (idx < 0 || idx >= baked.sprites.Length) return;
            _sr.sprite = baked.sprites[idx];
            _sr.flipX = _flip && baked.rowCount > 1;
        }

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
