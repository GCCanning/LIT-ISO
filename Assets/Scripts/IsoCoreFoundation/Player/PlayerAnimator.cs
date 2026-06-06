using UnityEngine;

namespace IsoCore.Foundation
{
    /// <summary>
    /// Drives the player's SpriteRenderer from the ReferenceKnight sheet: an 8-row x 4-frame
    /// directional sheet (row 0 = facing the camera / South, rotating clockwise). The
    /// animator picks the row from the player's movement direction (8-way facing), cycles
    /// the 4 frames — faster while walking, slow while idle — and adds a subtle vertical bob
    /// while moving so the idle-only art reads as a walk. Frames are bottom-centre pivoted
    /// so the knight stands on his tile.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class PlayerAnimator : MonoBehaviour
    {
        [Tooltip("Resources path of the multi-sprite sheet (no extension).")]
        public string sheetResource = "Characters/Player/ReferenceKnight_Idle_512x1024";
        public int framesPerRow = 4;
        public int rowCount = 8;
        [Tooltip("Idle playback speed (fps).")]
        public float idleFps = 5f;
        [Tooltip("Walk playback speed (fps).")]
        public float walkFps = 10f;

        SpriteRenderer _sr;
        IsoFoundationPlayer _player;
        Sprite[][] _rows;       // [row][frame]
        int _row;               // current facing row (0 = South)
        int _frame;
        float _timer;

        void Awake()
        {
            _sr = GetComponent<SpriteRenderer>();
            _player = GetComponent<IsoFoundationPlayer>();
            // Tint with the world's ambient light like everything else.
            _sr.sharedMaterial = SpriteAmbient.Material;
            LoadSheet();
        }

        void LoadSheet()
        {
            var all = Resources.LoadAll<Sprite>(sheetResource);
            if (all == null || all.Length == 0)
            {
                Debug.LogWarning($"[PlayerAnimator] No sprites at Resources/{sheetResource}");
                return;
            }
            System.Array.Sort(all, (a, b) => FrameIndex(a).CompareTo(FrameIndex(b)));

            _rows = new Sprite[rowCount][];
            for (int r = 0; r < rowCount; r++)
            {
                _rows[r] = new Sprite[framesPerRow];
                for (int f = 0; f < framesPerRow; f++)
                {
                    int idx = r * framesPerRow + f;
                    _rows[r][f] = idx < all.Length ? all[idx] : all[all.Length - 1];
                }
            }
            _row = 0; // start facing the camera
            _sr.sprite = _rows[0][0];
        }

        static int FrameIndex(Sprite s)
        {
            string n = s.name;
            int u = n.LastIndexOf('_');
            return u >= 0 && int.TryParse(n.Substring(u + 1), out int v) ? v : 0;
        }

        // Movement vector (screen space) -> sheet row. Sectors start at East=0 going CCW;
        // the sheet starts at South (row 0) going clockwise, hence row = (sector + 2) % 8:
        //   E->2, NE->3, N->4, NW->5, W->6, SW->7, S->0, SE->1.
        static int RowForDirection(Vector2 dir)
        {
            float ang = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg; // 0=E, 90=N, 180=W, -90=S
            if (ang < 0f) ang += 360f;
            int sector = Mathf.RoundToInt(ang / 45f) & 7;
            return (sector + 2) % 8;
        }

        void Update()
        {
            if (_rows == null) return;
            bool moving = _player != null && _player.IsMoving;

            if (moving)
                _row = RowForDirection(_player.MoveDir);

            // Frame cycling.
            float fps = moving ? walkFps : idleFps;
            _timer += Time.deltaTime;
            float spf = 1f / Mathf.Max(0.01f, fps);
            while (_timer >= spf)
            {
                _timer -= spf;
                _frame = (_frame + 1) % framesPerRow;
            }
            _sr.sprite = _rows[_row][_frame];
            // NOTE: no positional "bob" here — the player's transform is authored every
            // frame by IsoFoundationPlayer.Refresh() (world position + height lift). Writing
            // transform.y from the animator clobbers that and pins the player vertically
            // (it looked like you could only move along a W–E line). Walk motion is conveyed
            // by the directional frame cycling above. A vertical bob would need a dedicated
            // child visual transform so it never fights the movement transform.

            // Footsteps: rhythmic dust puff + sound while walking.
            if (moving)
            {
                _stepTimer += Time.deltaTime;
                if (_stepTimer >= StepInterval)
                {
                    _stepTimer = 0f;
                    WorldFx.Dust(transform.position + new Vector3(0f, 0.02f, 0f), 3);
                    SfxManager.Play("footstep", 0.45f, 0.12f);
                }
            }
            else _stepTimer = StepInterval; // next move triggers a step immediately
        }

        float _stepTimer;
        const float StepInterval = 0.32f;
    }
}
