using UnityEngine;

namespace IsoCore.Foundation
{
    /// <summary>
    /// Simple isometric melee attack. A short, cooldowned swing that damages every mob inside
    /// a forward arc in the player's facing direction. Bound to a dedicated key (default F) so it
    /// never clashes with PlayerInteraction's LMB harvest/place; optionally also fires on LMB when
    /// a Sword is equipped. Hits route damage into Mob.ApplyDamage; kills flow through the mob's
    /// own death path (drops + XP + the existing MobDefeated progression hook).
    /// </summary>
    public sealed class PlayerMelee : MonoBehaviour
    {
        IsoFoundationPlayer _player;
        MobSpawner _mobs;
        FoundationPlayerStats _stats;
        Hotbar _hotbar;
        FoundationContent _content;

        [Tooltip("Dedicated attack key (kept off LMB to avoid clashing with harvest/place).")]
        public KeyCode attackKey = KeyCode.F;
        [Tooltip("Also swing on left-click when a Sword is the selected hotbar item.")]
        public bool allowLeftClickWithSword = true;

        public float range = 1.3f;
        public float arcDegrees = 120f;
        public float cooldownSeconds = 0.45f;
        public float baseDamage = 8f;
        [Tooltip("Extra damage added when a Sword tool is equipped, scaled by its tier.")]
        public float swordTierDamage = 6f;
        public float knockback = 0.9f;

        float _cooldown;

        public void Init(IsoFoundationPlayer player, MobSpawner mobs, FoundationPlayerStats stats,
            Hotbar hotbar = null, FoundationContent content = null)
        {
            _player = player; _mobs = mobs; _stats = stats; _hotbar = hotbar; _content = content;
        }

        void Update()
        {
            if (_player == null || _mobs == null) return;
            if (_cooldown > 0f) _cooldown -= Time.deltaTime;
            if (_cooldown > 0f) return;
            if (WorldInputBlocked()) return;

            bool keyPressed = Input.GetKeyDown(attackKey);
            bool clickSwing = allowLeftClickWithSword && Input.GetMouseButtonDown(0) && SwordEquipped();
            if (keyPressed || clickSwing) Swing();
        }

        public void Swing()
        {
            _cooldown = Mathf.Max(0.1f, cooldownSeconds);

            Vector2 facing = _player.MoveDir.sqrMagnitude > 0.0001f ? _player.MoveDir.normalized : Vector2.down;
            Vector2 origin = _player.Ground;
            float dmg = baseDamage + SwordBonus();
            float cosHalfArc = Mathf.Cos(Mathf.Deg2Rad * Mathf.Clamp(arcDegrees, 10f, 360f) * 0.5f);

            SfxManager.Play("hit", 0.6f, 0.1f);
            FloatingText.Spawn(_player.transform.position + Vector3.up * 1.0f, "swish",
                new Color(0.85f, 0.9f, 1f), 0.6f);

            var mobsList = _mobs.ActiveMobs;
            if (mobsList == null) return;

            int hits = 0;
            // Iterate a snapshot-safe way: ApplyDamage can destroy a mob, mutating the list.
            for (int i = mobsList.Count - 1; i >= 0; i--)
            {
                if (i >= mobsList.Count) continue;
                var mob = mobsList[i];
                if (mob == null || mob.IsDead) continue;

                Vector2 to = mob.Ground - origin;
                float dist = to.magnitude;
                if (dist > range + mob.HitRadius) continue;
                if (dist > 0.05f)
                {
                    float dot = Vector2.Dot(to / dist, facing);
                    if (dot < cosHalfArc) continue; // outside the forward arc
                }
                mob.ApplyDamage(dmg, to);
                hits++;
            }
        }

        bool SwordEquipped() => SwordTier() >= 0;

        float SwordBonus()
        {
            int tier = SwordTier();
            return tier >= 0 ? swordTierDamage * Mathf.Max(1, tier) : 0f;
        }

        // Returns the sword tier (>=1) when a Sword is selected, else -1.
        int SwordTier()
        {
            if (_hotbar == null || _content == null) return -1;
            var stack = _hotbar.SelectedStack;
            if (stack.IsEmpty) return -1;
            var def = _content.Items.Get(stack.itemId);
            if (def != null && def.category == ItemCategory.Tool && def.toolType == ToolType.Sword)
                return Mathf.Max(1, def.toolTier);
            return -1;
        }

        bool WorldInputBlocked() =>
            FoundationUiCoordinator.Active != null && FoundationUiCoordinator.Active.BlocksWorldInput;
    }
}
