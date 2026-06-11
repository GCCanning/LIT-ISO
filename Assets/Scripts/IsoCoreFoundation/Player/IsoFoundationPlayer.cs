using UnityEngine;

namespace IsoCore.Foundation
{
    /// <summary>
    /// Kinematic isometric player. Moves on the height-0 ground plane; the cell's
    /// height only lifts the visual Y and sets sort. Collision is resolved by asking
    /// the world (per-axis slide) — never by physics. If world is null it disables
    /// loudly instead of silently walking through everything (legacy §7.4 fix).
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class IsoFoundationPlayer : MonoBehaviour
    {
        IsoWorld _world;
        FoundationConfig _cfg;
        FoundationPlayerStats _stats;   // optional; sprint runs un-metered without it
        SpriteRenderer _sr;
        Vector2 _ground;
        int _height;

        // ---- jump state (slight hop; the only way to ascend a height step) ----
        bool _jumping;
        float _jumpTimer;
        int _jumpStartHeight;           // takeoff height; climb allowance is relative to this

        // ---- sprint state ----
        // Set when stamina runs dry; sprint stays locked out until stamina regenerates
        // past cfg.sprintRecoverStamina so it cannot flicker on/off at the 0 boundary.
        bool _sprintExhausted;

        public Vector2Int CurrentCell => IsoGrid.WorldToCell(new Vector3(_ground.x, _ground.y, 0f));
        public int Height => _height;
        /// <summary>Height-0 ground position (what world/cell math expects, unlike the lifted transform).</summary>
        public Vector2 Ground => _ground;

        /// <summary>Last non-zero movement direction (screen space). Drives sprite facing.</summary>
        public Vector2 MoveDir { get; private set; } = Vector2.down;
        /// <summary>True on frames the player is actively moving (for walk vs. idle anim).</summary>
        public bool IsMoving { get; private set; }

        public void Init(IsoWorld world, FoundationConfig cfg, FoundationPlayerStats stats = null)
        {
            _world = world; _cfg = cfg; _stats = stats;
            _sr = GetComponent<SpriteRenderer>();
            if (_world == null)
            {
                Debug.LogError("[IsoFoundationPlayer] world is null — movement disabled.");
                enabled = false;
                return;
            }
            _sr.sprite = PlaceholderArt.Box(new Color(0.25f, 0.55f, 0.95f), 0.5f, 1.1f);
            _ground = Vector2.zero; // ground plane of cell (0,0)
            Refresh();
        }

        public void SetGround(Vector2 ground)
        {
            if (_world == null) return;
            _ground = ground;
            Refresh();
        }

        public void SetCell(int wx, int wy)
        {
            var world = IsoGrid.CellToWorld(wx, wy, 0);
            SetGround(new Vector2(world.x, world.y));
        }

        void Update()
        {
            IsMoving = false;
            if (_world == null) return;

            // Auto-eject if we somehow end up standing in a blocked cell (e.g. terrain
            // changed under us): walk toward the nearest walkable neighbour. Belt-and-
            // suspenders against soft-locks; placement also refuses to trap the player.
            if (!_world.IsWalkable(CurrentCell.x, CurrentCell.y))
            {
                EscapeToWalkable();
                Refresh();
                return;
            }

            UpdateJump();

            float ix = Input.GetAxisRaw("Horizontal");
            float iy = Input.GetAxisRaw("Vertical");
            var dir = new Vector2(ix, iy);
            bool hasInput = dir.sqrMagnitude >= 0.0001f;

            // Sprint: hold Left Shift while moving. TrySpendStamina refuses once the pool
            // can't cover this frame's cost, so sprint hard-stops at 0 (never negative)
            // and locks out until stamina regenerates past cfg.sprintRecoverStamina.
            // Any non-sprinting frame regenerates gently (HUD updates for free via the
            // existing FoundationPlayerStats.Changed event — no new UI).
            if (_sprintExhausted && _stats != null && _stats.Stamina >= _cfg.sprintRecoverStamina)
                _sprintExhausted = false;
            bool sprinting = false;
            if (hasInput && Input.GetKey(KeyCode.LeftShift) && !_sprintExhausted)
            {
                sprinting = _stats == null || _stats.TrySpendStamina(_cfg.sprintStaminaPerSecond * Time.deltaTime);
                if (!sprinting) _sprintExhausted = true;
            }
            if (!sprinting && _stats != null && _stats.Stamina < _stats.MaxStamina)
                _stats.RestoreStamina(_cfg.sprintStaminaRegenPerSecond * Time.deltaTime);

            if (!hasInput) return; // UpdateJump already refreshed the visual for an in-place hop
            if (dir.sqrMagnitude > 1f) dir.Normalize();
            IsMoving = true;
            MoveDir = dir;

            // Substep so a single large frame delta (hitch / high speed) cannot tunnel
            // through a one-cell-thick blocker. maxStep stays below TileHalfW (0.5).
            float dist = _cfg.moveSpeed * (sprinting ? _cfg.sprintMultiplier : 1f) * Time.deltaTime;
            const float maxStep = 0.2f;
            int steps = Mathf.Max(1, Mathf.CeilToInt(dist / maxStep));
            Vector2 stepDelta = dir * (dist / steps);
            for (int i = 0; i < steps; i++) StepMove(stepDelta);

            Refresh();
        }

        void StepMove(Vector2 delta)
        {
            Vector2 xy = _ground + delta;
            Vector2 xOnly = new(_ground.x + delta.x, _ground.y);
            Vector2 yOnly = new(_ground.x, _ground.y + delta.y);
            if (Walkable(xy)) _ground = xy;
            else if (Walkable(xOnly)) _ground = xOnly;
            else if (Walkable(yOnly)) _ground = yOnly;
        }

        /// <summary>
        /// Space starts a short hop (cfg.jumpDuration). While airborne, Walkable() permits
        /// ascending up to cfg.jumpClimbSteps above the takeoff height — the only way up a
        /// cliff, since walking keeps the maxWalkStepHeight=0 invariant.
        /// </summary>
        void UpdateJump()
        {
            if (!_jumping)
            {
                if (Input.GetKeyDown(KeyCode.Space))
                {
                    _jumping = true;
                    _jumpTimer = 0f;
                    // Query height fresh: _height can be stale while idle (Refresh only runs
                    // on movement) and the climb allowance must anchor to the real takeoff.
                    var c = CurrentCell;
                    _jumpStartHeight = _world.GetHeight(c.x, c.y);
                }
                return;
            }

            _jumpTimer += Time.deltaTime;
            if (_jumpTimer >= Mathf.Max(0.01f, _cfg.jumpDuration))
                _jumping = false;
            // Refresh even with no horizontal input so an in-place hop animates, and so the
            // sprite snaps back to its ground lift on the landing frame.
            Refresh();
        }

        void EscapeToWalkable()
        {
            var c = CurrentCell;
            for (int r = 1; r <= 4; r++)
            {
                bool found = false; float bestD = float.MaxValue; Vector2 target = _ground;
                for (int dy = -r; dy <= r; dy++)
                for (int dx = -r; dx <= r; dx++)
                {
                    if (Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy)) != r) continue;
                    int nx = c.x + dx, ny = c.y + dy;
                    if (!_world.IsWalkable(nx, ny)) continue;
                    Vector3 g = IsoGrid.CellToWorld(nx, ny, 0);
                    float d = (new Vector2(g.x, g.y) - _ground).sqrMagnitude;
                    if (d < bestD) { bestD = d; target = new Vector2(g.x, g.y); found = true; }
                }
                if (found)
                {
                    _ground = Vector2.MoveTowards(_ground, target, _cfg.moveSpeed * Time.deltaTime);
                    return;
                }
            }
        }

        /// <summary>
        /// Can the player's ground point move to g? Blocked cells (solid blocks, water,
        /// blocking occupants/nodes) always refuse — so a jump can never land in water or
        /// inside a block. Height rules: level/descending is always free (no fall damage);
        /// ascending is forbidden while walking (maxWalkStepHeight=0 invariant) and allowed
        /// up to cfg.jumpClimbSteps above the takeoff height during an active jump.
        /// </summary>
        bool Walkable(Vector2 g)
        {
            var c = IsoGrid.WorldToCell(new Vector3(g.x, g.y, 0f));
            if (!_world.IsWalkable(c.x, c.y)) return false;

            int targetH = _world.GetHeight(c.x, c.y);
            var cur = CurrentCell;
            int curH = _world.GetHeight(cur.x, cur.y);
            if (targetH <= curH) return true;              // level or downhill: always allowed
            if (!_jumping) return false;                   // walking never climbs (invariant)
            // Anchor to takeoff height so one hop can't chain-climb a staircase.
            return targetH - _jumpStartHeight <= Mathf.Max(0, _cfg.jumpClimbSteps);
        }

        void Refresh()
        {
            var c = CurrentCell;
            _height = _world.GetHeight(c.x, c.y);
            // Visual-only hop arc: a parabola peaking at cfg.jumpHeightUnits mid-jump.
            // It only lifts the transform — cell/height queries and sorting are untouched.
            float lift = 0f;
            if (_jumping)
            {
                float t = Mathf.Clamp01(_jumpTimer / Mathf.Max(0.01f, _cfg.jumpDuration));
                lift = _cfg.jumpHeightUnits * 4f * t * (1f - t);
            }
            transform.position = new Vector3(_ground.x, _ground.y + _height * IsoGrid.HeightStep + lift, 0f);
            _sr.sortingOrder = IsoGrid.SortingOrder(c.x, c.y, _height, IsoGrid.LayerActor);
        }
    }
}
