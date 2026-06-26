using UnityEngine;

namespace IsoCore.Foundation
{
    /// <summary>
    /// Turns a successful ability use into a real in-world effect + VFX. The economy
    /// (cost, cooldown, XP, affinity) is already handled by FoundationAbilitySystem; this
    /// performs the delivery using world-query APIs and cursor aim, then plays WorldFx.
    /// Slice 1 implements Blink, Projectile, and Heal; everything else gets a generic
    /// cast puff so it still reads on screen.
    /// </summary>
    public sealed class FoundationAbilityDispatcher
    {
        IsoFoundationPlayer _player;
        MobSpawner _mobs;
        FoundationContent _content;
        FoundationPlayerStats _stats;

        public void Init(IsoFoundationPlayer player, MobSpawner mobs, FoundationContent content, FoundationPlayerStats stats)
        {
            _player = player;
            _mobs = mobs;
            _content = content;
            _stats = stats;
        }

        /// <summary>Perform the in-world effect for an ability whose economy already succeeded.</summary>
        public void Execute(FoundationAbilityDefinition ability, FoundationAbilityUseResult result)
        {
            if (ability == null || _player == null) return;

            Vector3 playerPos = _player.transform.position;
            Vector2 aim = AimDir(playerPos);
            Color color = ElementColor(ability.element);

            switch (ability.delivery)
            {
                case FoundationAbilityDelivery.Blink:
                    _player.Blink(aim, ability.dashTiles);
                    break;

                case FoundationAbilityDelivery.Projectile:
                {
                    var go = new GameObject("FoundationProjectile");
                    var proj = go.AddComponent<FoundationProjectile>();
                    float dmg = Mathf.Max(1f, result.scaledPower * Mathf.Max(1f, ability.effectScale));
                    proj.Init(playerPos, aim, ability.projectileSpeed, ability.range, dmg, color, _mobs);
                    break;
                }

                case FoundationAbilityDelivery.Heal:
                {
                    float amount = Mathf.Max(1f, result.scaledPower * Mathf.Max(1f, ability.effectScale));
                    _stats?.Heal(amount);
                    WorldFx.Smoke(playerPos, new Color(0.7f, 1f, 0.7f, 0.85f), count: 14, size: 0.18f, radius: 0.14f, rise: 0.6f, life: 0.6f);
                    FloatingText.Spawn(playerPos + Vector3.up * 0.7f, $"+{Mathf.CeilToInt(amount)} HP", new Color(0.6f, 1f, 0.6f));
                    break;
                }

                default:
                    // Bookkeeping-only abilities still get a small cast puff for feedback.
                    WorldFx.Smoke(playerPos, color, count: 8, size: 0.16f, radius: 0.12f, rise: 0.4f, life: 0.4f);
                    break;
            }
        }

        Vector2 AimDir(Vector3 from)
        {
            var cam = Camera.main;
            if (cam == null) return _player.MoveDir;
            Vector3 m = Input.mousePosition;
            Vector3 w = cam.ScreenToWorldPoint(new Vector3(m.x, m.y, Mathf.Abs(cam.transform.position.z)));
            Vector2 dir = new Vector2(w.x - from.x, w.y - from.y);
            return dir.sqrMagnitude > 0.0004f ? dir.normalized : _player.MoveDir;
        }

        static Color ElementColor(FoundationAbilityElement e) => e switch
        {
            FoundationAbilityElement.Ember => new Color(1f, 0.55f, 0.25f),
            FoundationAbilityElement.Tide => new Color(0.45f, 0.8f, 1f),
            FoundationAbilityElement.Root => new Color(0.55f, 0.85f, 0.4f),
            FoundationAbilityElement.Stone => new Color(0.7f, 0.66f, 0.55f),
            FoundationAbilityElement.Gale => new Color(0.8f, 0.95f, 1f),
            FoundationAbilityElement.Glimmer => new Color(1f, 0.95f, 0.6f),
            FoundationAbilityElement.Hearth => new Color(1f, 0.8f, 0.5f),
            _ => new Color(0.7f, 0.85f, 1f),
        };
    }
}
