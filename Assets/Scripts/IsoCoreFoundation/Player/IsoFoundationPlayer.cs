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
        SpriteRenderer _sr;
        Vector2 _ground;
        int _height;

        public Vector2Int CurrentCell => IsoGrid.WorldToCell(new Vector3(_ground.x, _ground.y, 0f));
        public int Height => _height;
        /// <summary>Height-0 ground position (what world/cell math expects, unlike the lifted transform).</summary>
        public Vector2 Ground => _ground;

        /// <summary>Last non-zero movement direction (screen space). Drives sprite facing.</summary>
        public Vector2 MoveDir { get; private set; } = Vector2.down;
        /// <summary>True on frames the player is actively moving (for walk vs. idle anim).</summary>
        public bool IsMoving { get; private set; }

        public void Init(IsoWorld world, FoundationConfig cfg)
        {
            _world = world; _cfg = cfg;
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

            float ix = Input.GetAxisRaw("Horizontal");
            float iy = Input.GetAxisRaw("Vertical");
            var dir = new Vector2(ix, iy);
            if (dir.sqrMagnitude < 0.0001f) return;
            if (dir.sqrMagnitude > 1f) dir.Normalize();
            IsMoving = true;
            MoveDir = dir;

            // Substep so a single large frame delta (hitch / high speed) cannot tunnel
            // through a one-cell-thick blocker. maxStep stays below TileHalfW (0.5).
            float dist = _cfg.moveSpeed * Time.deltaTime;
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

        bool Walkable(Vector2 g)
        {
            var c = IsoGrid.WorldToCell(new Vector3(g.x, g.y, 0f));
            return _world.IsWalkable(c.x, c.y);
        }

        void Refresh()
        {
            var c = CurrentCell;
            _height = _world.GetHeight(c.x, c.y);
            transform.position = new Vector3(_ground.x, _ground.y + _height * IsoGrid.HeightStep, 0f);
            _sr.sortingOrder = IsoGrid.SortingOrder(c.x, c.y, _height, IsoGrid.LayerActor);
        }
    }
}
