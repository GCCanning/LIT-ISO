using UnityEngine;

namespace IsoCore.Foundation
{
    /// <summary>Simple wandering wildlife. Uses the world query for walkability.</summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class Mob : MonoBehaviour
    {
        MobDefinition _def;
        IsoWorld _world;
        SpriteRenderer _sr;
        Vector2 _ground;
        Vector2 _target;
        int _height;
        float _repathTimer;

        // Animation (optional): when a def has an animationKey with art under
        // Resources/Enemies/<Folder>/Individual Sprites/, the mob plays idle/move frames;
        // otherwise it falls back to a procedural coloured blob.
        Sprite[] _idle, _move, _hurt, _die;
        bool _animated;
        int _frame;
        float _animTimer;
        bool _moving;
        const float AnimFps = 6f;

        public MobDefinition Def => _def;
        public Vector2 Ground => _ground;
        public bool HasHurtFrames => _hurt != null && _hurt.Length > 0;
        public bool HasDieFrames => _die != null && _die.Length > 0;

        public void Init(MobDefinition def, IsoWorld world, Vector2 ground)
        {
            _def = def; _world = world; _ground = ground;
            _sr = GetComponent<SpriteRenderer>();
            _sr.sharedMaterial = SpriteAmbient.Material; // tint with day/night like the world

            LoadAnimation(def);
            if (_animated && _idle.Length > 0) _sr.sprite = _idle[0];
            else _sr.sprite = PlaceholderArt.Blob(def.color, def.sizeUnits);

            PickTarget();
            Place();
        }

        // Convention map: mob id -> Resources subfolder + frame prefix. Only the slime ships
        // with animation art today; deer/fox keep the coloured-blob fallback. Add an entry
        // here (or an animationKey on MobDefinition) to animate a new mob with no other code.
        void LoadAnimation(MobDefinition def)
        {
            string folder = null, prefix = null;
            if (def != null && def.id == "slime") { folder = "Enemies/Slime/Individual Sprites"; prefix = "slime"; }
            if (folder == null) { _animated = false; return; }

            _idle = LoadFrames(folder, prefix, "idle", 4);
            _move = LoadFrames(folder, prefix, "move", 4);
            _hurt = LoadFrames(folder, prefix, "hurt", 4);
            _die  = LoadFrames(folder, prefix, "die", 4);
            _animated = _idle.Length > 0 || _move.Length > 0;
            if (_move.Length == 0) _move = _idle; // move falls back to idle
        }

        static Sprite[] LoadFrames(string folder, string prefix, string state, int max)
        {
            var list = new System.Collections.Generic.List<Sprite>();
            for (int i = 0; i < max; i++)
            {
                var s = Resources.Load<Sprite>($"{folder}/{prefix}-{state}-{i}");
                if (s != null) list.Add(s);
            }
            return list.ToArray();
        }

        void Update()
        {
            if (_world == null) return;
            _repathTimer -= Time.deltaTime;
            if (_repathTimer <= 0f || (_target - _ground).sqrMagnitude < 0.04f) PickTarget();

            _moving = false;
            var dir = _target - _ground;
            if (dir.sqrMagnitude > 0.0001f)
            {
                var step = dir.normalized * _def.moveSpeed * Time.deltaTime;
                var np = _ground + step;
                var c = IsoGrid.WorldToCell(new Vector3(np.x, np.y, 0f));
                if (_world.IsWalkable(c.x, c.y)) { _ground = np; _moving = true; }
                else PickTarget();
            }
            Animate();
            Place();
        }

        void Animate()
        {
            if (!_animated) return;
            var frames = _moving && _move.Length > 0 ? _move : _idle;
            if (frames == null || frames.Length == 0) return;
            _animTimer += Time.deltaTime;
            float spf = 1f / AnimFps;
            while (_animTimer >= spf) { _animTimer -= spf; _frame++; }
            _sr.sprite = frames[_frame % frames.Length];
        }

        void PickTarget()
        {
            _repathTimer = _def.repathSeconds;
            float ang = Random.value * Mathf.PI * 2f;
            float r = Random.value * _def.wanderRadius;
            _target = _ground + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * r;
        }

        void Place()
        {
            var c = IsoGrid.WorldToCell(new Vector3(_ground.x, _ground.y, 0f));
            _height = _world.GetHeight(c.x, c.y);
            transform.position = new Vector3(_ground.x, _ground.y + _height * IsoGrid.HeightStep, 0f);
            _sr.sortingOrder = IsoGrid.SortingOrder(c.x, c.y, _height, IsoGrid.LayerActor);
        }
    }
}
