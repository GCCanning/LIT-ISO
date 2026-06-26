using UnityEngine;

namespace IsoCore.Foundation
{
    /// <summary>
    /// A simple cursor-aimed magic projectile. Travels straight until it hits a mob
    /// (via MobSpawner.FindMobNear) or reaches max range, then plays an impact burst and
    /// destroys itself. Built entirely in code — no prefab. Spawned by
    /// FoundationAbilityDispatcher.
    /// </summary>
    public sealed class FoundationProjectile : MonoBehaviour
    {
        Vector2 _dir;
        float _speed, _rangeLeft, _damage, _hitRadius, _trailTimer;
        Color _color;
        MobSpawner _mobs;

        public void Init(Vector3 start, Vector2 dir, float speed, float range, float damage,
                         Color color, MobSpawner mobs, float hitRadius = 0.55f)
        {
            transform.position = new Vector3(start.x, start.y, 0f);
            _dir = dir.sqrMagnitude > 0.0001f ? dir.normalized : Vector2.down;
            _speed = Mathf.Max(1f, speed);
            _rangeLeft = Mathf.Max(0.5f, range);
            _damage = Mathf.Max(1f, damage);
            _color = color;
            _mobs = mobs;
            _hitRadius = hitRadius;

            var sr = gameObject.AddComponent<SpriteRenderer>();
            sr.sprite = PlaceholderArt.Box(color, 0.28f, 0.28f);
            sr.sortingOrder = 9000;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            float move = _speed * dt;
            if (move > _rangeLeft) move = _rangeLeft;

            Vector2 p = (Vector2)transform.position + _dir * move;
            transform.position = new Vector3(p.x, p.y, 0f);
            _rangeLeft -= move;

            // Light trailing puff.
            _trailTimer -= dt;
            if (_trailTimer <= 0f)
            {
                _trailTimer = 0.04f;
                WorldFx.Smoke(transform.position, _color, count: 3, size: 0.1f, radius: 0.05f, rise: 0.1f, life: 0.25f);
            }

            var mob = _mobs != null ? _mobs.FindMobNear(p, _hitRadius) : null;
            if (mob != null) { mob.TakeMobDamage(_damage); Impact(); return; }
            if (_rangeLeft <= 0.001f) Impact();
        }

        void Impact()
        {
            WorldFx.Debris(transform.position, _color, count: 10, size: 0.08f, speed: 2.4f);
            WorldFx.Smoke(transform.position, _color, count: 8, size: 0.16f, radius: 0.12f, rise: 0.3f, life: 0.4f);
            Destroy(gameObject);
        }
    }
}
