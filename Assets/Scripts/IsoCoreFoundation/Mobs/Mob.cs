using System;
using UnityEngine;

namespace IsoCore.Foundation
{
    /// <summary>Simple wandering wildlife. Uses the world query for walkability.</summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class Mob : MonoBehaviour
    {
        MobDefinition _def;
        IsoWorld _world;
        IsoFoundationPlayer _player;
        FoundationPlayerStats _stats;
        SpriteRenderer _sr;
        Vector2 _ground;
        Vector2 _target;
        int _height;
        float _repathTimer;
        float _attackTimer;
        bool _aggressive;

        // Night danger (Impact Analysis A2): per-instance multipliers applied on top of the
        // shared MobDefinition values when this mob was spawned at night outside a campfire
        // ward. Defaults to 1 (no change) so behaviour is identical by day / when warded.
        float _nightDamageMul = 1f;
        float _nightSpeedMul = 1f;
        float _nightAggroMul = 1f;
        bool _nightDanger;

        // Distance-from-spawn tier: enemies further from the hearth are higher level/tougher.
        int _level = 1;
        float _tierDmgMul = 1f;
        public int Level => _level;

        float EffectiveMoveSpeed => _def.moveSpeed * _nightSpeedMul;
        float EffectiveContactDamage => _def.contactDamage * _nightDamageMul * _tierDmgMul;
        float EffectiveAttackRange => _def.attackRange * _nightAggroMul;

        // Animation (optional): when a def has an animationKey with art under
        // Resources/Enemies/<Folder>/Individual Sprites/, the mob plays idle/move frames;
        // otherwise it falls back to a procedural coloured blob.
        Sprite[] _idle, _move, _hurt, _die;
        bool _animated;
        int _frame;
        float _animTimer;
        bool _moving;
        bool _resolved;
        const float AnimFps = 6f;

        // Hit feedback kit (playtest 2026-07-02 #6): flash + knockback + hit-stop +
        // hurt frames on every TakeMobDamage, Romestead-class action feel.
        Color _baseColor = Color.white;   // restore target for the flash (night tint aware)
        float _flashTimer;                // > 0 → sprite is flashed to the hit colour
        float _hitStopTimer;              // > 0 → this mob's own update is frozen
        float _hurtAnimTimer;             // > 0 → Animate() plays the _hurt frames
        const float FlashSeconds = 0.08f;
        const float HitStopSeconds = 0.06f;
        const float HurtAnimSeconds = 0.30f;
        const float KnockbackUnits = 0.20f;
        static readonly Color FlashColor = new Color(1f, 0.35f, 0.30f, 1f);

        // Faction combat (mob vs mob) — independent of the player->mob kill path.
        float _hp = 20f;
        float _mobAtkTimer;
        Mob _foe;
        static readonly System.Collections.Generic.List<Mob> _active = new System.Collections.Generic.List<Mob>();
        void OnEnable() { if (!_active.Contains(this)) _active.Add(this); }
        void OnDisable() { _active.Remove(this); }
        /// <summary>All live mobs. Used by the player's directional melee (mobs have no
        /// physics colliders, so Physics2D overlaps cannot find them).</summary>
        public static System.Collections.Generic.IReadOnlyList<Mob> Active => _active;

        // Humanoid NPCs are dressed by the layered character creator. The Assembly-CSharp
        // LayeredNpcHook listens to Spawned, attaches a LayeredCharacterAnimator, and drives it
        // from FaceDir/IsMoving; the mob then yields its own sprite via UseExternalAppearance().
        Vector2 _faceDir = Vector2.down;
        bool _externalAppearance;
        public static event System.Action<Mob> Spawned;
        public void UseExternalAppearance() => _externalAppearance = true;
        public Vector2 FaceDir => _faceDir;
        public bool IsMoving => _moving;
        public string AppearanceTheme => _def != null ? _def.appearanceId : "";

        public event Action<Mob> Defeated;
        public event Action<Mob> Calmed;

        // Phase 3: raised when this NPC casts one of its abilityIds. The Assembly-CSharp
        // LayeredNpcDriver listens and plays the matching one-shot animation. Foundation never
        // touches the animator directly — this event is the cross-assembly seam.
        public event Action<string> AbilityUsed;
        float _abilityTimer;
        FoundationContent _content;
        public void SetContent(FoundationContent content) => _content = content;

        public MobDefinition Def => _def;
        public Vector2 Ground => _ground;
        public bool HasHurtFrames => _hurt != null && _hurt.Length > 0;
        public bool HasDieFrames => _die != null && _die.Length > 0;

        public void Init(MobDefinition def, IsoWorld world, Vector2 ground)
        {
            _def = def; _world = world; _ground = ground;
            _hp = Mathf.Max(1f, def.maxHealth);
            _sr = GetComponent<SpriteRenderer>();
            _sr.sharedMaterial = SpriteAmbient.Material; // tint with day/night like the world

            if (!string.IsNullOrEmpty(def.appearanceId))
            {
                // Humanoid NPC: placeholder until the layered character creator dresses it on spawn.
                _sr.sprite = PlaceholderArt.Blob(JitterColor(def.color), Mathf.Max(0.9f, def.sizeUnits));
            }
            else
            {
                LoadAnimation(def);
                if (_animated && _idle.Length > 0) _sr.sprite = _idle[0];
                else if (!string.IsNullOrEmpty(def.decorationSprite))
                {
                    var ds = DecorationSpriteResolver.Resolve(def.decorationSprite);
                    _sr.sprite = ds != null ? ds : PlaceholderArt.Blob(def.color, def.sizeUnits);
                }
                else _sr.sprite = PlaceholderArt.Blob(JitterColor(def.color), def.sizeUnits);
            }
            FoundationDepthPolish.Attach(gameObject, fadeWhenOccluding: false, castLongShadow: true,
                contactScale: Mathf.Clamp(def.sizeUnits, 0.45f, 1.2f), contactAlpha: 0.24f);

            // Body collider (owner, 2026-07-02): a trigger circle so projectiles, physics
            // overlaps and click-targeting can find the mob. Trigger-only on purpose —
            // player movement stays world-query (invariant), nothing is physically blocked;
            // body separation is handled in ApplySeparation().
            var body = gameObject.AddComponent<CircleCollider2D>();
            body.isTrigger = true;
            body.radius = Mathf.Clamp(def.sizeUnits * 0.45f, 0.18f, 0.65f);
            body.offset = new Vector2(0f, Mathf.Max(0.1f, def.sizeUnits * 0.3f));

            _baseColor = _sr.color; // hit flash restores here (ApplyNightDanger retints it)
            PickTarget();
            Place();
            Spawned?.Invoke(this); // lets the layered character creator dress humanoid NPCs
        }

        // Small per-instance tint variety so a crowd of the same NPC type isn't identical.
        static Color JitterColor(Color c)
        {
            float j = UnityEngine.Random.Range(0.85f, 1.12f);
            return new Color(Mathf.Clamp01(c.r * j), Mathf.Clamp01(c.g * j), Mathf.Clamp01(c.b * j), c.a);
        }

        public void SetCombatContext(IsoFoundationPlayer player, FoundationPlayerStats stats, bool aggressive)
        {
            _player = player;
            _stats = stats;
            _aggressive = aggressive || (_def != null && _def.behaviour == MobBehavior.Hostile);
        }

        /// <summary>
        /// Marks this mob as "night-empowered" (Impact Analysis A2): applies multipliers to
        /// contact damage, move speed, and attack/aggro range on top of the shared
        /// MobDefinition values. Multipliers &lt;= 0 are treated as 1 (no change).
        /// </summary>
        public void ApplyNightDanger(float damageMul, float speedMul, float aggroMul)
        {
            _nightDanger = true;
            _nightDamageMul = damageMul > 0f ? damageMul : 1f;
            _nightSpeedMul = speedMul > 0f ? speedMul : 1f;
            _nightAggroMul = aggroMul > 0f ? aggroMul : 1f;
            // Night hunters must be READABLE as more dangerous, not just numerically
            // buffed (reference-integration pass, 2026-07-02): a warm blood-tinged cast
            // multiplies over the shared day/night ambient so empowered mobs stand out
            // against the cool night palette at a glance.
            _baseColor = new Color(1.0f, 0.72f, 0.70f, 1f); // hit flash restores to THIS, not white
            if (_sr != null)
                _sr.color = _baseColor;
        }

        /// <summary>Distance-from-spawn scaling: bumps level, HP and damage so enemies further
        /// from the hearth are tougher. Called once by the spawner right after Init.</summary>
        public void ApplyTier(int level, float hpMul, float dmgMul)
        {
            _level = Mathf.Max(1, level);
            _tierDmgMul = dmgMul > 0f ? dmgMul : 1f;
            if (_def != null) _hp = Mathf.Max(1f, _def.maxHealth * (hpMul > 0f ? hpMul : 1f));
        }

        public void MarkDefeated()
        {
            if (_resolved) return;
            _resolved = true;
            Defeated?.Invoke(this);
        }

        public void MarkCalmed()
        {
            if (_resolved) return;
            _resolved = true;
            Calmed?.Invoke(this);
        }

        // Convention map: mob id -> Resources subfolder + frame prefix. Only the slime ships
        // with animation art today; deer/fox keep the coloured-blob fallback. Add an entry
        // here (or an animationKey on MobDefinition) to animate a new mob with no other code.
        void LoadAnimation(MobDefinition def)
        {
            string folder = null, prefix = null;
            // Base slime + the biome variants (slime_common/rare/boss) all share the slime
            // frames (single-direction blob; no directional rows). Without this the variants
            // rendered as static coloured blobs.
            if (def != null && (def.id == "slime" || def.id.StartsWith("slime_", System.StringComparison.Ordinal)))
            { folder = "Enemies/Slime/Individual Sprites"; prefix = "slime"; }
            // Humanoid NPCs animate from character-creator sheets dropped into
            // Resources/Characters/<id>/ (frames named "<id>-idle-0", "<id>-move-0", ...).
            if (def != null && (def.id == "bandit" || def.id.StartsWith("adventurer_", System.StringComparison.Ordinal)))
            { folder = "Characters/" + def.id; prefix = def.id; }
            // Predator plants ship sliced idle/move frames in Resources/Characters/<id>/.
            if (def != null && def.id.StartsWith("predator_plant_", System.StringComparison.Ordinal))
            { folder = "Characters/" + def.id; prefix = def.id; }
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

            // Hit-stop (playtest #6): the mob's own simulation freezes for a few
            // hundredths after taking a hit; only the flash-restore keeps ticking.
            TickHitFeedback();
            if (_hitStopTimer > 0f)
            {
                _hitStopTimer -= Time.deltaTime;
                return;
            }

            _attackTimer -= Time.deltaTime;
            _mobAtkTimer -= Time.deltaTime;
            _abilityTimer -= Time.deltaTime;

            // Night danger (A2): a normally-passive mob spawned at night outside a campfire
            // ward will start hunting the player once they wander within its (boosted) aggro range.
            if (!_aggressive && _nightDanger && _player != null && _def != null)
            {
                float baseAggroRange = Mathf.Max(_def.wanderRadius, _def.attackRange);
                float aggroRange = baseAggroRange * _nightAggroMul;
                if ((_player.Ground - _ground).sqrMagnitude <= aggroRange * aggroRange)
                    _aggressive = true;
            }

            _foe = null;
            if (_def != null && (_def.faction == MobFaction.Bandit || _def.faction == MobFaction.Adventurer))
            {
                float fr = Mathf.Max(_def.wanderRadius, 6f);
                _foe = NearestFoe(fr * fr);
            }

            if (_foe != null &&
                (!(_aggressive && _player != null) ||
                 (_foe.Ground - _ground).sqrMagnitude <= (_player.Ground - _ground).sqrMagnitude))
            {
                _target = _foe.Ground;               // chase the opposing-faction mob
            }
            else if (_aggressive && _player != null)
            {
                _foe = null;
                _target = _player.Ground;
            }
            else
            {
                _foe = null;
                _repathTimer -= Time.deltaTime;
                if (_repathTimer <= 0f || (_target - _ground).sqrMagnitude < 0.04f) PickTarget();
            }

            _moving = false;
            var dir = _target - _ground;
            // Hunters face their prey even while standing still (in attack range or
            // blocked) — playtest 2026-07-02 #5: mob sprites never turned.
            if ((_aggressive || _foe != null) && dir.sqrMagnitude > 1e-4f)
                _faceDir = dir.normalized;
            if (dir.sqrMagnitude > 0.0001f)
            {
                var step = dir.normalized * EffectiveMoveSpeed * Time.deltaTime;
                var np = _ground + step;
                var c = IsoGrid.WorldToCell(new Vector3(np.x, np.y, 0f));
                if (_world.IsWalkable(c.x, c.y)) { _ground = np; _moving = true; _faceDir = dir.normalized; }
                else PickTarget();
            }
            ApplySeparation();
            TryAttack();
            if (_foe != null) TryAttackMob(_foe);
            TryCastAbility();
            Animate();
            Place();
        }

        /// <summary>
        /// Soft body separation (owner, 2026-07-02): mobs no longer stack inside each
        /// other or stand in the player. A gentle planar push, capped per frame and
        /// respecting walkability, so crowds spread naturally without physics.
        /// </summary>
        void ApplySeparation()
        {
            const float MobRadius = 0.38f;
            const float PlayerRadius = 0.45f;
            Vector2 push = Vector2.zero;

            for (int i = 0; i < _active.Count; i++)
            {
                var m = _active[i];
                if (m == null || m == this || m._resolved) continue;
                Vector2 d = _ground - m._ground;
                float sq = d.sqrMagnitude;
                if (sq >= MobRadius * MobRadius || sq < 1e-6f) continue;
                float dist = Mathf.Sqrt(sq);
                push += (d / dist) * (MobRadius - dist);
            }
            if (_player != null)
            {
                Vector2 d = _ground - _player.Ground;
                float sq = d.sqrMagnitude;
                if (sq < PlayerRadius * PlayerRadius && sq > 1e-6f)
                {
                    float dist = Mathf.Sqrt(sq);
                    push += (d / dist) * (PlayerRadius - dist) * 1.5f;
                }
            }
            if (push.sqrMagnitude < 1e-6f) return;

            Vector2 np = _ground + Vector2.ClampMagnitude(push, Mathf.Max(0.5f, EffectiveMoveSpeed) * Time.deltaTime * 1.5f);
            var c = IsoGrid.WorldToCell(new Vector3(np.x, np.y, 0f));
            if (_world.IsWalkable(c.x, c.y)) _ground = np;
        }

        // ---- faction combat (mob vs mob) ----
        static bool IsFoe(MobFaction a, MobFaction b)
        {
            return (a == MobFaction.Bandit && b == MobFaction.Adventurer)
                || (a == MobFaction.Adventurer && b == MobFaction.Bandit);
        }

        Mob NearestFoe(float maxSqr)
        {
            Mob best = null; float bestSqr = maxSqr;
            var myF = _def.faction;
            for (int i = 0; i < _active.Count; i++)
            {
                var m = _active[i];
                if (m == null || m == this || m._resolved || m._def == null) continue;
                if (!IsFoe(myF, m._def.faction)) continue;
                float sq = (m._ground - _ground).sqrMagnitude;
                if (sq < bestSqr) { bestSqr = sq; best = m; }
            }
            return best;
        }

        void TryAttackMob(Mob foe)
        {
            if (foe == null || foe._resolved || _mobAtkTimer > 0f || _def == null) return;
            float range = Mathf.Max(0.45f, _def.attackRange);
            if ((foe._ground - _ground).sqrMagnitude > range * range) return;
            _mobAtkTimer = Mathf.Max(0.5f, _def.attackCooldownSeconds);
            foe.TakeMobDamage(Mathf.Max(1f, _def.meleeDamage * _tierDmgMul), _ground);
        }

        public void TakeMobDamage(float dmg) => TakeMobDamage(dmg, null);

        /// <summary>
        /// Damage + the full hit-feedback kit (playtest 2026-07-02 #6): damage number,
        /// hit flash, knockback nudge away from <paramref name="attackerGround"/>,
        /// per-mob hit-stop, hurt frames and hurt/death SFX. Pass the attacker's ground
        /// position when known so the knockback direction is honest; null falls back to
        /// the player (the common case) or skips the nudge.
        /// </summary>
        public void TakeMobDamage(float dmg, Vector2? attackerGround)
        {
            if (_resolved) return;
            _hp -= dmg;
            FloatingText.Spawn(transform.position + Vector3.up * 0.7f,
                $"-{Mathf.CeilToInt(dmg)}", new Color(1f, 0.7f, 0.3f));

            // 1) Hit flash — a warm red multiply tint (a true white "additive" flash
            //    can't brighten under the multiplied ambient material). Restores to
            //    _baseColor, which tracks the night-danger tint, NOT plain white.
            if (_sr != null) { _sr.color = FlashColor; _flashTimer = FlashSeconds; }

            // 2) Knockback nudge — 0.2u away from the attacker, walkable-checked
            //    (same clamp pattern as ApplySeparation). Instant, so it reads on the
            //    exact hit frame even through the hit-stop.
            Vector2? from = attackerGround;
            if (from == null && _player != null) from = _player.Ground;
            if (from.HasValue)
            {
                Vector2 d = _ground - from.Value;
                if (d.sqrMagnitude > 1e-6f)
                {
                    Vector2 np = _ground + d.normalized * KnockbackUnits;
                    var kc = IsoGrid.WorldToCell(new Vector3(np.x, np.y, 0f));
                    if (_world != null && _world.IsWalkable(kc.x, kc.y)) { _ground = np; Place(); }
                }
            }

            // 3) Hit-stop — freeze this mob's own update briefly so the hit "lands".
            _hitStopTimer = HitStopSeconds;

            // 5) Hurt frames — play the loaded _hurt strip for a beat (they loaded
            //    before but never actually played on damage).
            if (_hurt != null && _hurt.Length > 0) { _hurtAnimTimer = HurtAnimSeconds; _frame = 0; _animTimer = 0f; }

            if (_hp <= 0f)
            {
                _resolved = true;
                string mobId = _def != null ? _def.id : "";
                SfxManager.PlayAtBest($"mob_{mobId}_death", "hit", transform.position, 0.65f);
                Destroy(gameObject); // the spawner prunes destroyed mobs; no player credit for NPC kills
            }
            else
            {
                string mobId = _def != null ? _def.id : "";
                SfxManager.PlayAtBest($"mob_{mobId}_hurt", "hit", transform.position, 0.45f);
            }
        }

