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

        public MobDefinition Def => _def;
        public Vector2 Ground => _ground;

        public void Init(MobDefinition def, IsoWorld world, Vector2 ground)
        {
            _def = def; _world = world; _ground = ground;
            _sr = GetComponent<SpriteRenderer>();
            _sr.sprite = PlaceholderArt.Blob(def.color, def.sizeUnits);
            PickTarget();
            Place();
        }

        void Update()
        {
            if (_world == null) return;
            _repathTimer -= Time.deltaTime;
            if (_repathTimer <= 0f || (_target - _ground).sqrMagnitude < 0.04f) PickTarget();

            var dir = _target - _ground;
            if (dir.sqrMagnitude > 0.0001f)
            {
                var step = dir.normalized * _def.moveSpeed * Time.deltaTime;
                var np = _ground + step;
                var c = IsoGrid.WorldToCell(new Vector3(np.x, np.y, 0f));
                if (_world.IsWalkable(c.x, c.y)) _ground = np;
                else PickTarget();
            }
            Place();
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
