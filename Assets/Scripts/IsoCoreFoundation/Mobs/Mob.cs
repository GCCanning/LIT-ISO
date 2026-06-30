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

        // Faction combat (mob vs mob) — independent of the player->mob kill path.
        float _hp = 20f;
        float _mobAtkTimer;
        Mob _foe;
        static readonly System.Collections.Generic.List<Mob> _active = new System.Collections.Generic.List<Mob>();
        void OnEnable() { if (!_active.Contains(this)) _active.Add(this); }
        void OnDisable() { _active.Remove(this); }

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
            FoundationDepthPolish.Attach(gameObject, fadeWhenOccluding: false, castLongShadow: false,
                contactScale: Mathf.Clamp(def.sizeUnits, 0.45f, 1.2f), contactAlpha: 0.24f);

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
            if (def != null && def.id == "slime") { folder = "Enemies/Slime/Individual Sprites"; prefix = "slime"; }
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
            if (dir.sqrMagnitude > 0.0001f)
            {
                var step = dir.normalized * EffectiveMoveSpeed * Time.deltaTime;
                var np = _ground + step;
                var c = IsoGrid.WorldToCell(new Vector3(np.x, np.y, 0f));
                if (_world.IsWalkable(c.x, c.y)) { _ground = np; _moving = true; _faceDir = dir.normalized; }
                else PickTarget();
            }
            TryAttack();
            if (_foe != null) TryAttackMob(_foe);
            TryCastAbility();
            Animate();
            Place();
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
            foe.TakeMobDamage(Mathf.Max(1f, _def.meleeDamage * _tierDmgMul));
        }

        public void TakeMobDamage(float dmg)
        {
            if (_resolved) return;
            _hp -= dmg;
            FloatingText.Spawn(transform.position + Vector3.up * 0.7f,
                $"-{Mathf.CeilToInt(dmg)}", new Color(1f, 0.7f, 0.3f));
            if (_hp <= 0f)
            {
                _resolved = true;
                SfxManager.Play("hit", 0.6f);
                Destroy(gameObject); // the spawner prunes destroyed mobs; no player credit for NPC kills
            }
        }

        void TryAttack()
        {
            if (!_aggressive || _player == null || _stats == null || _attackTimer > 0f)
                return;
            if (_def == null || _def.contactDamage <= 0f)
                return;

            float range = Mathf.Max(0.1f, EffectiveAttackRange);
            if ((_player.Ground - _ground).sqrMagnitude > range * range)
                return;

            _attackTimer = Mathf.Max(0.5f, _def.attackCooldownSeconds);
            float damage = Mathf.Max(1f, EffectiveContactDamage);
            _stats.Damage(damage);
            FloatingText.Spawn(_player.transform.position + Vector3.up * 0.75f,
                $"-{Mathf.CeilToInt(damage)} HP", new Color(1f, 0.35f, 0.25f));
            SfxManager.Play("hit", 0.75f);
        }

        // Phase 3: occasional NPC ability cast, gated by a per-mob cooldown. NPCs have no
        // mana/stamina pool, so the cost is cooldown-only. Damage scales by _tierDmgMul via the
        // ability's basePower; the actual VFX/animation is the Assembly-CSharp listener's job
        // (AbilityUsed event). Targets the current foe (mob-vs-mob) or the player when aggressive.
        void TryCastAbility()
        {
            if (_resolved || _def == null || _abilityTimer > 0f) return;
            var ids = _def.abilityIds;
            if (ids == null || ids.Length == 0) return;

            Mob foeTarget = _foe;
            bool playerTarget = _aggressive && _player != null && foeTarget == null;
            if (foeTarget == null && !playerTarget) return;

            string abilityId = ids[UnityEngine.Random.Range(0, ids.Length)];
            var ability = _content != null ? _content.Abilities.Get(abilityId) : null;

            float range = ability != null ? Mathf.Max(1.5f, ability.range) : Mathf.Max(2f, _def.attackRange * 2f);
            Vector2 targetGround = foeTarget != null ? foeTarget.Ground : _player.Ground;
            if ((targetGround - _ground).sqrMagnitude > range * range) return;

            _abilityTimer = Mathf.Max(1f, _def.abilityCooldownSeconds);

            float power = ability != null ? Mathf.Max(0.1f, ability.basePower) : 1f;
            float damage = Mathf.Max(1f, power * _def.meleeDamage * _tierDmgMul);
            if (foeTarget != null) foeTarget.TakeMobDamage(damage);
            else if (_stats != null) _stats.Damage(damage);

            AbilityUsed?.Invoke(abilityId);
        }

        void Animate()
        {
            if (_externalAppearance) return; // the layered character creator owns the sprite
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
            float ang = UnityEngine.Random.value * Mathf.PI * 2f;
            float r = UnityEngine.Random.value * _def.wanderRadius;
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
