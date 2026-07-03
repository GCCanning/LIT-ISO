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
        float _moveSpeedCurrent;   // ramped speed for accel/decel feel
        int _height;
        float _visualHeight;   // eased toward _height for smooth vertical steps

        // ---- jump state (hop, or a directional leap that arcs over water gaps) ----
        bool _jumping;
        float _jumpTimer;
        float _jumpDur = 0.35f;         // this jump's duration: cfg.jumpDuration for a hop,
                                        // scaled by leap distance so short leaps aren't floaty
                                        // and long ones aren't teleports (owner, 2026-07-02)
        int _jumpStartHeight;           // takeoff height; climb allowance is relative to this
        bool _leaping;                  // true when the jump is a forward leap (drives _ground itself)
        Vector2 _leapStart;
        Vector2 _leapTarget;

        // ---- click-to-move (additive A* path; WASD always overrides) ----
        // A* path of cells start->goal and our cursor into it. Set on left-click onto a
        // walkable cell; cleared the instant any keyboard movement is pressed (manual
        // override wins) or when the path completes / gets stuck. Never touches physics.
        System.Collections.Generic.List<Vector2Int> _path;
        int _pathIdx;
        int _pathStuckFrames;                 // consecutive no-progress frames -> abandon path
        Vector2 _pathLastGround;              // _ground at the end of last path-follow frame
        const float WaypointReach = 0.12f;    // world-units within which a waypoint is "reached"
        const int PathStuckLimit = 8;         // frames of no progress before giving up

        // ---- sprint state ----
        // Set when stamina runs dry; sprint stays locked out until stamina regenerates
        // past cfg.sprintRecoverStamina so it cannot flicker on/off at the 0 boundary.
        bool _sprintExhausted;

        // ---- hazard tile damage-over-time (2026-06-13 dungeon overhaul) ----
        // Standing on a lava/fire-trap surface tile ticks damage on an interval,
        // mirroring WeatherManager.OutdoorDamageRoutine.
        const float HazardTickInterval = 1f;
        const int HazardDamagePerTick = 8;
        // Public: single source of truth for hazard surfaces — worldgen guards
        // (IsoTerrainSampler, FoundationDungeonGenerator) reference this set too.
        public static readonly System.Collections.Generic.HashSet<string> HazardSurfaceBlocks = new() { "lava", "fire_trap" };
        float _hazardTimer;

        // ---- Blink (Flash Step) ----
        // Driven by the ability system via Blink(); cost/cooldown live on the ability data.
        // The dash is clamped by the same Walkable() world-query as walking, so it can never
        // tunnel through walls or climb a cliff walking couldn't.
        const float TileToWorld = 1.0f;             // a tile diamond is 1.0 world units wide
        static readonly Color FlashSmokeColor = new(0.82f, 0.93f, 1f, 0.7f);

        public Vector2Int CurrentCell => IsoGrid.WorldToCell(new Vector3(_ground.x, _ground.y, 0f));
        public int Height => _height;
        /// <summary>Height-0 ground position (what world/cell math expects, unlike the lifted transform).</summary>
        public Vector2 Ground => _ground;

        /// <summary>Last non-zero movement direction (screen space). Drives sprite facing.</summary>
        public Vector2 MoveDir { get; private set; } = Vector2.down;
        /// <summary>True on frames the player is actively moving (for walk vs. idle anim).</summary>
        public bool IsMoving { get; private set; }
        /// <summary>True when the player is moving at sprint speed this frame (drives run animation).</summary>
        public bool IsSprinting { get; private set; }

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
            // NearWalkable() (not a strict IsWalkable) so legitimately "leaning into a
            // wall/void edge" within cfg.wallCollisionInset (see Walkable()) doesn't
            // trigger an eject loop.
            if (!NearWalkable(_ground))
            {
                EscapeToWalkable();
                Refresh();
                return;
            }

            UpdateHazardTick();
            UpdateJump();
            if (_jumping && _leaping) return; // the leap drives _ground itself; skip normal walk/sprint

            float ix = Input.GetAxisRaw("Horizontal");
            float iy = Input.GetAxisRaw("Vertical");
            var dir = new Vector2(ix, iy);
            bool hasInput = dir.sqrMagnitude >= 0.0001f;

            // ---- click-to-move DISABLED (owner direction) --------------------------------
            // Left-click is now the basic attack (+ primary tool/item use via PlayerInteraction);
            // movement is WASD only. Right-click interacts. TryHandleClick() is left in the file
            // in case click-to-move is wanted again.
            // TryHandleClick();
            if (hasInput)
            {
                _path = null;   // manual override: drop any active path, behave exactly as before
            }
            else if (_path != null)
            {
                // No keyboard input and a path exists: steer toward the current waypoint by
                // synthesizing `dir`, then fall through to the SAME ramp + StepMove as WASD.
                if (TryGetPathDir(out var pathDir))
                {
                    dir = pathDir;
                    hasInput = true;   // drive the accel ramp exactly like a held key
                }
            }

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

            IsSprinting = sprinting;

            // Smooth accel/decel for weightier, production-feel movement (2026-06).
            // Ramp a current-speed value toward target: starts ease in, stops glide
            // briefly rather than snapping. DEX still scales the target.
            float dexMul = _stats != null ? _stats.MoveSpeedMultiplier : 1f;
            float targetSpeed = hasInput
                ? _cfg.moveSpeed * dexMul * (sprinting ? _cfg.sprintMultiplier : 1f)
                : 0f;
            // ISO-CORE parity: their locomotion is RAW instant velocity (no easing), so
            // the ramp is near-instant. Lower these to reintroduce weighty accel/decel.
            const float accelUp = 120f;  // ~0.03s to full speed (effectively instant)
            const float accelDn = 120f;
            _moveSpeedCurrent = Mathf.MoveTowards(_moveSpeedCurrent, targetSpeed,
                (targetSpeed > _moveSpeedCurrent ? accelUp : accelDn) * Time.deltaTime);

            if (hasInput)
            {
                if (dir.sqrMagnitude > 1f) dir.Normalize();
                MoveDir = dir;
            }
            // Combat input and recovery must tick even while standing still.
            if (Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.Z))
            {
                var es = UnityEngine.EventSystems.EventSystem.current;
                if (es == null || !es.IsPointerOverGameObject())
                    TriggerBasicAttack();
            }
            TickAttack();

            // Fully stopped: idle (UpdateJump already handled any in-place hop visual).
            if (_moveSpeedCurrent <= 0.001f) { IsMoving = false; IsSprinting = false; return; }
            IsMoving = hasInput;

            // Substep so a single large frame delta cannot tunnel a one-cell blocker.
            float dist = _moveSpeedCurrent * Time.deltaTime;
            const float maxStep = 0.2f;
            int steps = Mathf.Max(1, Mathf.CeilToInt(dist / maxStep));
            Vector2 stepDelta = MoveDir * (dist / steps);
            for (int i = 0; i < steps; i++) StepMove(stepDelta);

            // Path-follow stuck guard: if we're auto-walking a path but StepMove made no
            // real progress for several frames (wall/prop changed under us, or the steering
            // is grinding a corner), abandon the path so we don't lock in place.
            if (_path != null)
            {
                if ((_ground - _pathLastGround).sqrMagnitude < 1e-6f)
                {
                    if (++_pathStuckFrames >= PathStuckLimit) _path = null;
                }
                else _pathStuckFrames = 0;
                _pathLastGround = _ground;
            }

            Refresh();
        }

        void StepMove(Vector2 delta)
        {
            if (TryStep(delta)) return;
            // Endpoint blocked at the full per-frame delta (the player is heading into a wall
            // or corner). Instead of stopping dead in open space — the "sticky" feel — retry
            // at progressively smaller magnitudes so the player glides right up to the wall
            // and slides along it. Every accepted move still passes Walkable(), so this can
            // never move into a blocked cell (no clipping); it only fills the gap that the
            // single full-delta endpoint check used to leave.
            for (int i = 0; i < 3; i++)
            {
                delta *= 0.5f;
                if (delta.sqrMagnitude < 1e-6f) return;
                if (TryStep(delta)) return;
            }
        }

        /// <summary>One world-query move attempt: full diagonal, then X-only, then Y-only
        /// (wall-slide). Returns true and commits _ground on the first walkable option.</summary>
        bool TryStep(Vector2 delta)
        {
            Vector2 xy = _ground + delta;
            Vector2 xOnly = new(_ground.x + delta.x, _ground.y);
            Vector2 yOnly = new(_ground.x, _ground.y + delta.y);
            if (Walkable(xy)) { _ground = xy; return true; }
            if (Walkable(xOnly)) { _ground = xOnly; return true; }
            if (Walkable(yOnly)) { _ground = yOnly; return true; }
            return false;
        }

        // --------------------------------------------------------------- click-to-move (additive)

        /// <summary>
        /// On a left-click that isn't over UI, project the cursor onto the height-0 plane,
        /// resolve the target cell, and (if walkable) build an A* path from CurrentCell.
        /// No-ops on a click into the void / a blocked cell. Pure query — no physics.
        /// </summary>
        void TryHandleClick()
        {
            if (!Input.GetMouseButtonDown(0)) return;

            // Ignore clicks consumed by UI (guard for scenes with no EventSystem).
            var es = UnityEngine.EventSystems.EventSystem.current;
            if (es != null && es.IsPointerOverGameObject()) return;

            var cam = Camera.main;
            if (cam == null) return;

            // Camera is orthographic 2D; ScreenToWorldPoint gives the XY we treat as the
            // height-0 plane (same plane WorldToCell expects for player ground tracking).
            Vector3 wp = cam.ScreenToWorldPoint(Input.mousePosition);
            wp.z = 0f;
            var goal = IsoGrid.WorldToCell(wp);
            if (!_world.IsWalkable(goal.x, goal.y) || _world.IsPropBlocked(goal.x, goal.y)) return;

            var path = FoundationPathfinder.FindPath(
                _world, CurrentCell, goal, Mathf.Max(0, _cfg.maxWalkStepHeight));
            if (path == null || path.Count == 0) return;

            _path = path;
            _pathIdx = 0;
            _pathStuckFrames = 0;
            _pathLastGround = _ground;
            // If the first node is the cell we're already in, skip it so we steer to the next.
            if (_path.Count > 1 && _path[0] == CurrentCell) _pathIdx = 1;
        }

        /// <summary>
        /// Steering direction toward the current path waypoint (cell-centre XY on the
        /// height-0 plane, matching how _ground maps via WorldToCell). Advances the
        /// waypoint cursor when within WaypointReach; clears the path at the end.
        /// Returns false (and clears the path) when there's nothing left to steer to.
        /// </summary>
        bool TryGetPathDir(out Vector2 dir)
        {
            dir = Vector2.zero;
            if (_path == null) return false;

            while (_pathIdx < _path.Count)
            {
                var cell = _path[_pathIdx];
                // Steer to the cell centre on the height-0 plane (ignore the height lift):
                // _ground lives on that plane, which is what WorldToCell inverts. X is the
                // plain iso X; Y is the ground-plane Y (CellToWorld with height 0).
                Vector3 w = IsoGrid.CellToWorld(cell.x, cell.y, 0);
                Vector2 target = new(w.x, w.y);
                Vector2 to = target - _ground;
                if (to.sqrMagnitude <= WaypointReach * WaypointReach)
                {
                    _pathIdx++;   // reached this waypoint; aim at the next
                    continue;
                }
                dir = to.normalized;
                return true;
            }

            _path = null; // walked the whole path
            return false;
        }

        /// <summary>
        /// Space starts a short hop (cfg.jumpDuration). While airborne, Walkable() permits
        /// ascending up to cfg.jumpClimbSteps above the takeoff height — for cliffs taller
        /// than cfg.maxWalkStepHeight, which walking alone cannot climb.
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

                    // A held direction turns the hop into a forward leap that arcs over
                    // water/gaps and lands on the far bank (cfg.jumpLeapTiles range).
                    _leaping = false;
                    _jumpDur = Mathf.Max(0.01f, _cfg.jumpDuration);
                    var inDir = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
                    if (inDir.sqrMagnitude >= 0.0001f && _cfg.jumpLeapTiles > 0)
                    {
                        Vector2 target = ComputeLeapTarget(inDir);
                        if ((target - _ground).sqrMagnitude > 0.04f)
                        {
                            _leaping = true;
                            _leapStart = _ground;
                            _leapTarget = target;
                            MoveDir = inDir.normalized;
                            IsMoving = true;
                            // Constant leap SPEED, not constant time: duration scales with
                            // distance (45%..100% of jumpDuration) so a short hop over a
                            // stream is snappy and a max-range leap has real hang time.
                            float frac = (target - _ground).magnitude /
                                Mathf.Max(0.4f, _cfg.jumpLeapTiles * TileToWorld);
                            _jumpDur = Mathf.Max(0.01f, _cfg.jumpDuration * Mathf.Clamp(frac, 0.45f, 1f));
                        }
                    }
                }
                return;
            }

            _jumpTimer += Time.deltaTime;
            float dur = _jumpDur;
            if (_leaping)
            {
                float t = Mathf.Clamp01(_jumpTimer / dur);
                _ground = Vector2.Lerp(_leapStart, _leapTarget, t);
                MoveDir = (_leapTarget - _leapStart).normalized;
                IsMoving = true; // keep the character animating through the arc
            }
            if (_jumpTimer >= dur)
            {
                if (_leaping) _ground = _leapTarget;
                _jumping = false;
                _leaping = false;
            }
            // Refresh even with no horizontal input so an in-place hop animates, and so the
            // sprite snaps back to its ground lift on the landing frame.
            Refresh();
        }

        /// <summary>
        /// Farthest solid landing point along <paramref name="dir"/> within cfg.jumpLeapTiles.
        /// Scans far→near and returns the first Walkable() cell, so intermediate water/gap
        /// cells are arced over while the landing is always solid ground (or a climbable
        /// step, since _jumping is already set when this runs). Returns _ground if none.
        /// </summary>
        Vector2 ComputeLeapTarget(Vector2 dir)
        {
            dir.Normalize();
            float maxDist = Mathf.Max(0.4f, _cfg.jumpLeapTiles) * TileToWorld;
            const float stepWorld = 0.25f;
            int n = Mathf.Max(1, Mathf.CeilToInt(maxDist / stepWorld));
            for (int i = n; i >= 1; i--)
            {
                Vector2 cand = _ground + dir * (maxDist * i / n);
                if (!Walkable(cand)) continue;
                // Land on the cell CENTRE, not wherever the scan sample fell. Edge landings
                // used to leave the ground point half-inside the neighbouring water/void
                // cell, so the wall-inset correction kicked in on the landing frame and the
                // player visibly stuttered / got "caught" on the bank (owner, 2026-07-02).
                var lc = IsoGrid.WorldToCell(new Vector3(cand.x, cand.y, 0f));
                Vector3 centre = IsoGrid.CellToWorld(lc.x, lc.y, 0);
                Vector2 snapped = new Vector2(centre.x, centre.y);
                return Walkable(snapped) ? snapped : cand;
            }
            return _ground;
        }

        /// <summary>Ticks damage while standing on a lava/fire-trap surface tile.</summary>
        void UpdateHazardTick()
        {
            var c = CurrentCell;
            var cell = _world.GetCell(c.x, c.y);
            if (!HazardSurfaceBlocks.Contains(cell.SurfaceBlockId))
            {
                _hazardTimer = 0f;
                return;
            }

            _hazardTimer += Time.deltaTime;
            if (_hazardTimer < HazardTickInterval) return;
            _hazardTimer -= HazardTickInterval;

            if (_stats == null) return;
            _stats.Damage(HazardDamagePerTick);
            FloatingText.Spawn(
                transform.position + Vector3.up * 0.5f,
                $"-{HazardDamagePerTick} (Fire Trap)",
                new Color(1f, 0.45f, 0.15f));
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
        /// ascending while walking is allowed up to cfg.maxWalkStepHeight steps (owner
        /// change 2026-06-13 — previously a hard 0), and up to cfg.jumpClimbSteps above
        /// the takeoff height during an active jump (for taller cliffs).
        /// </summary>
        /// <summary>
        /// True if the cell at g is walkable, or g sits within cfg.wallCollisionInset of
        /// a walkable cell along either grid axis. Lets the player's ground point lean
        /// slightly into a blocked cell (wall/void/water/solid) so they can reach the
        /// drawn edge — the 2:1 collision grid (IsoGrid) is shallower than the drawn
        /// pixel-art tiles, so a strict point-in-cell check stops short of that edge.
        /// Used by both Walkable() and the Update() eject-check so the two stay
        /// consistent (no eject loop at the tolerance boundary).
        /// </summary>
        bool NearWalkable(Vector2 g)
        {
            var c = IsoGrid.WorldToCell(new Vector3(g.x, g.y, 0f));
            if (_world.IsWalkable(c.x, c.y)) return true;

            float inset = Mathf.Max(0f, _cfg.wallCollisionInset);
            if (inset <= 0f) return false;

            Vector2[] probes =
            {
                new Vector2(g.x - inset, g.y),
                new Vector2(g.x + inset, g.y),
                new Vector2(g.x, g.y - inset),
                new Vector2(g.x, g.y + inset),
            };

            foreach (var p in probes)
            {
                var pc = IsoGrid.WorldToCell(new Vector3(p.x, p.y, 0f));
                if (_world.IsWalkable(pc.x, pc.y)) return true;
            }
            return false;
        }

        bool Walkable(Vector2 g)
        {
            var c = IsoGrid.WorldToCell(new Vector3(g.x, g.y, 0f));
            bool blocked = !_world.IsWalkable(c.x, c.y);
            if (blocked)
            {
                // Props (trees/rocks/placeables) are HARD blockers from every direction.
                // The lean tolerance below is only for terrain edges (cliffs/water); an
                // isolated 1-cell prop always has a walkable neighbour within the lean
                // inset, so without this the rescue fires and the player phases through
                // the prop from all but one approach. (2026-06 collision rework.)
                if (_world.IsPropBlocked(c.x, c.y)) return false;
                if (!NearWalkable(g)) return false;
                // g is within the lean-tolerance of a walkable cell's edge: treat this
                // as "still in the current cell" for the height-step logic below (the
                // player isn't really entering the blocked cell, just nudging up to its
                // edge), so a tall wall/block can never be "climbed" via the tolerance.
                c = CurrentCell;
            }

            int targetH = _world.GetHeight(c.x, c.y);
            var cur = CurrentCell;
            int curH = _world.GetHeight(cur.x, cur.y);
            if (targetH <= curH) return true;              // level or downhill: always allowed

            int climb = targetH - curH;
            if (climb <= Mathf.Max(0, _cfg.maxWalkStepHeight)) return true; // small step-up while walking

            if (!_jumping) return false;
            // Anchor to takeoff height so one hop can't chain-climb a staircase.
            // 2026-06-13 (A7): STR adds extra climb allowance via _stats.JumpClimbBonus
            // (0 at the baseline STR of 8, so default characters are unchanged).
            int strBonus = _stats != null ? _stats.JumpClimbBonus : 0;
            return targetH - _jumpStartHeight <= Mathf.Max(0, _cfg.jumpClimbSteps + strBonus);
        }

        // ----------------------------------------------------------------- Blink (Flash Step)

        // ---------------------------------------------------------------- basic attack

        float _attackCooldownTimer = 0f;
        const float AttackCooldown    = 0.5f;   // seconds between attacks
        const float AttackRangeWorld  = 1.2f;   // world-units melee reach
        const int   AttackBaseDamage  = 10;     // flat damage per hit

        /// <summary>
        /// Fires a melee strike in the player's facing direction. Damages every
        /// SlimeEnemyController within AttackRangeWorld. Called by the HUD Attack button
        /// and can also be called from keyboard input (Space).
        /// </summary>
        PlayerHeldTool _heldTool;
        ICharacterActionAnimator _bodyAnim;
        bool _bodyAnimResolved;

        /// <summary>Plays a one-shot body animation (slash / spellcast / thrust / hurt) on the
        /// layered LPC animator if present. No-op on the placeholder animator. Resolved lazily
        /// because the LPC animator is attached after spawn by LayeredCharacterPlayerHook.</summary>
        public void PlayBodyAnim(string animId)
        {
            if (!_bodyAnimResolved) { _bodyAnim = GetComponent<ICharacterActionAnimator>(); _bodyAnimResolved = _bodyAnim != null; }
            _bodyAnim?.PlayActionAnim(animId);
        }

        public void TriggerBasicAttack()
        {
            if (_attackCooldownTimer > 0f) return;
            _attackCooldownTimer = AttackCooldown;

            // LPC sideways sword slash on the character body (no-op if the placeholder
            // animator is active).
            PlayBodyAnim("slash");

            // Swing the held tool/weapon so the attack has an animation (no-op if the hand is
            // empty — equip a weapon/tool to see the swing).
            if (_heldTool == null) _heldTool = GetComponent<PlayerHeldTool>();
            _heldTool?.Swing();

            Vector2 facing2 = MoveDir;
            if (facing2.sqrMagnitude < 0.0001f) facing2 = Vector2.down;
            facing2.Normalize();
            Vector3 facing = new Vector3(facing2.x, facing2.y, 0f);

            // Presentation gate (owner, 2026-07-02): the extra slash arc + sprite flash are
            // opt-in via cfg.attackVfxEnabled so the raw LPC/tool animations stay readable.
            if (_cfg != null && _cfg.attackVfxEnabled)
            {
                WorldFx.Trail(
                    transform.position + facing * 0.2f,
                    transform.position + facing * (AttackRangeWorld + 0.15f),
                    new Color(1f, 0.97f, 0.78f, 0.95f), puffs: 10, size: 0.22f);
                if (_sr != null)
                {
                    _sr.color = new Color(1f, 0.85f, 0.6f, 1f);
                    _attackFlashTimer = 0.16f;
                }
            }

            // Directional melee (owner, 2026-07-02): the swing hits what is IN FRONT of the
            // player, within reach. Mobs carry no physics colliders, so they are tested via
            // the live mob registry with a forward-cone check; world IDamageables (nodes,
            // structures) keep the physics overlap but pass the same facing filter.
            bool hitAnything = false;
            Vector2 origin = (Vector2)transform.position;

            var mobs = Mob.Active;
            for (int i = mobs.Count - 1; i >= 0; i--)
            {
                var mob = mobs[i];
                if (!mob) continue;
                Vector2 to = (Vector2)mob.transform.position - origin;
                float dist = to.magnitude;
                if (dist > AttackRangeWorld * 1.15f) continue;
                // In-front check: generous ~120° cone, and anything practically on top of
                // the player always counts (you don't whiff a swing at point-blank).
                if (dist > 0.35f && Vector2.Dot(to / dist, facing2) < 0.35f) continue;
                mob.TakeMobDamage(AttackBaseDamage);
                hitAnything = true;
            }

            var hits = Physics2D.OverlapCircleAll(origin + facing2 * (AttackRangeWorld * 0.5f), AttackRangeWorld * 0.75f);
            foreach (var hit in hits)
            {
                var damageable = hit.GetComponentInParent<IDamageable>();
                if (damageable == null) continue;
                Vector2 to = (Vector2)hit.transform.position - origin;
                if (to.sqrMagnitude > 0.35f * 0.35f && Vector2.Dot(to.normalized, facing2) < 0.2f) continue;
                damageable.TakeDamage(AttackBaseDamage);
                hitAnything = true;
            }
            if (hitAnything)
                FoundationImpactFeedback.Pulse(1f);
        }

        float _attackFlashTimer = 0f;

        void TickAttack()
        {
            if (_attackCooldownTimer > 0f)
                _attackCooldownTimer -= Time.deltaTime;

            if (_attackFlashTimer > 0f)
            {
                _attackFlashTimer -= Time.deltaTime;
                if (_attackFlashTimer <= 0f && _sr != null)
                    _sr.color = Color.white;
            }
        }

        /// <summary>
        /// Instant teleport along <paramref name="dir"/> for up to <paramref name="tiles"/> tiles,
        /// clamped by the same Walkable() world-query as walking (no wall tunnelling, no climbing
        /// a cliff walking couldn't). Leaves a smoke burst at the takeoff feet and a trailing
        /// streak to the landing point. Driven by the ability system (FoundationAbilityDispatcher).
        /// </summary>
        public void Blink(Vector2 dir, float tiles, bool showVfx = true)
        {
            if (_world == null) return;
            if (dir.sqrMagnitude < 0.0001f) dir = MoveDir;
            if (dir.sqrMagnitude < 0.0001f) dir = Vector2.down;
            dir.Normalize();
            MoveDir = dir;

            // March straight along dir in small substeps, stopping at the last walkable
            // point — same per-cell world-query as walking, so no wall tunnelling.
            float worldDist = Mathf.Max(0.2f, tiles) * TileToWorld;
            const float step = 0.15f;
            int steps = Mathf.Max(1, Mathf.CeilToInt(worldDist / step));
            Vector2 stepVec = dir * (worldDist / steps);

            Vector2 start = _ground;
            Vector3 startWorld = transform.position;
            Vector2 pos = _ground;
            for (int i = 0; i < steps; i++)
            {
                Vector2 next = pos + stepVec;
                if (Walkable(next)) pos = next;
                else break;
            }

            // Always puff at the feet so a blocked blink still reads.
            if (showVfx)
                WorldFx.Smoke(startWorld, FlashSmokeColor, count: 16, size: 0.2f, radius: 0.18f, rise: 0.7f, life: 0.55f);
            if ((pos - start).sqrMagnitude < 0.02f * 0.02f) return;

            _ground = pos;
            Refresh();
            if (showVfx)
                WorldFx.Trail(startWorld, transform.position, FlashSmokeColor, puffs: 6, size: 0.14f);
        }

        void Refresh()
        {
            var c = CurrentCell;
            // While arcing over water/void in a leap, the cell under the player is NOT
            // ground — sampling it made the height (and sorting) pop per-tile mid-flight,
            // which read as stutter over rivers/gaps (owner, 2026-07-02). Hold the takeoff
            // height until landing; the landing frame re-samples the real bank height.
            if (_jumping && _leaping)
                _height = _jumpStartHeight;
            else
                _height = _world.GetHeight(c.x, c.y);
            // Visual-only hop arc: a parabola peaking at cfg.jumpHeightUnits mid-jump.
            // It only lifts the transform — cell/height queries and sorting are untouched.
            float lift = 0f;
            if (_jumping)
            {
                float t = Mathf.Clamp01(_jumpTimer / _jumpDur);
                // 2026-06-13 (A7): STR scales the visual hop arc via
                // _stats.JumpHeightMultiplier (1.0x at the baseline STR of 8).
                float strMul = _stats != null ? _stats.JumpHeightMultiplier : 1f;
                lift = _cfg.jumpHeightUnits * strMul * 4f * t * (1f - t);
            }
            // Ease visual height between integer cell levels so stepping a cliff doesn't
            // pop; sorting still uses the true integer _height.
            _visualHeight = Mathf.MoveTowards(_visualHeight, _height, Time.deltaTime * 8f);
            transform.position = new Vector3(_ground.x