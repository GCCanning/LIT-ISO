using System;
using System.Collections.Generic;
using UnityEngine;

namespace IsoCore.Foundation
{
    /// <summary>
    /// Wildlife + combat mob. Wanders when calm; aggros, chases (walk→run), telegraphs and
    /// strikes when a def carries combat stats. Takes melee from the player (hurt flash +
    /// knockback + floating numbers), dies through a Death animation, then drops loot + XP.
    /// Movement is world-query based (no physics), matching the rest of the foundation.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class Mob : MonoBehaviour
    {
        MobDefinition _def;
        IsoWorld _world;
        IsoFoundationPlayer _player;
        FoundationPlayerStats _stats;
        Inventory _loot;
        SpriteRenderer _sr;
        Vector2 _ground;
        Vector2 _home;
        Vector2 _target;
        Vector2 _knock;
        int _height;
        float _repathTimer;
        float _attackTimer;
        bool _aggressive;          // hostile-by-def or ward-breach: always pursues
        bool _engaged;             // currently aggro'd via sight (leashable)

        // Combat
        float _maxHp = 1f, _hp = 1f;
        bool _dead;
        float _hurtFlashTimer;
        float _windupTimer;
        bool _windingUp;
        float _deathTimer;
        bool _resolved;
        Color _baseColor = Color.white;

        // Animation
        MobAnimEntry _anim;        // directional library entry (predator plants)
        Sprite[] _idle, _move, _hurt, _die; // legacy single-direction frames (slime)
        bool _legacyAnimated;
        int _frame;
        float _animTimer;
        MobAnimState _animState = MobAnimState.Idle;
        int _dir;
        bool _moving;
        const float LegacyFps = 6f;

        public event Action<Mob> Defeated;
        public event Action<Mob> Calmed;

        public MobDefinition Def => _def;
        public Vector2 Ground => _ground;
        public bool IsDead => _dead;
        public bool IsCombatant => _def != null && (_def.maxHealth > 0f || _def.aggroRadius > 0f || _def.attackDamage > 0f);
        public float Health01 => _maxHp > 0f ? Mathf.Clamp01(_hp / _maxHp) : 0f;
        public float HitRadius => Mathf.Clamp(_def != null ? _def.sizeUnits : 0.5f, 0.3f, 1.2f) * 0.6f;
        public bool HasHurtFrames => (_hurt != null && _hurt.Length > 0) || (_anim != null && _anim.hurt.HasAny);
        public bool HasDieFrames => (_die != null && _die.Length > 0) || (_anim != null && _anim.death.HasAny);

        public void Init(MobDefinition def, IsoWorld world, Vector2 ground)
        {
            _def = def; _world = world; _ground = ground; _home = ground;
            _sr = GetComponent<SpriteRenderer>();
            _sr.sharedMaterial = SpriteAmbient.Material; // tint with day/night like the world

            _maxHp = def != null && def.maxHealth > 0f ? def.maxHealth : 1f;
            _hp = _maxHp;

            LoadAnimation(def);
            if (_anim != null) { _baseColor = Color.white; _sr.sprite = FirstFrame() ?? PlaceholderArt.Blob(def.color, def.sizeUnits); }
            else if (_legacyAnimated && _idle.Length > 0) { _baseColor = Color.white; _sr.sprite = _idle[0]; }
            else { _baseColor = def.color; _sr.sprite = PlaceholderArt.Blob(def.color, def.sizeUnits); }
            _sr.color = _baseColor;

            FoundationDepthPolish.Attach(gameObject, fadeWhenOccluding: false, castLongShadow: false,
                contactScale: Mathf.Clamp(def.sizeUnits, 0.45f, 1.2f), contactAlpha: 0.24f);

            PickTarget();
            Place();
        }

        public void SetCombatContext(IsoFoundationPlayer player, FoundationPlayerStats stats, bool aggressive)
        {
            _player = player;
            _stats = stats;
            _aggressive = aggressive || (_def != null && _def.behaviour == MobBehavior.Hostile);
        }

        /// <summary>Where loot is deposited on death (player inventory). Optional.</summary>
        public void SetLootSink(Inventory inv) => _loot = inv;

        // ---------- Damage intake ----------

        /// <summary>Player melee (or any source) hits this mob. Returns true if the hit was lethal.</summary>
        public bool ApplyDamage(float amount, Vector2 fromDir)
        {
            if (_dead || _def == null || amount <= 0f) return false;

            _hp -= amount;
            _hurtFlashTimer = 0.16f;
            _windingUp = false; // a solid hit interrupts the wind-up
            if (fromDir.sqrMagnitude > 0.0001f)
                _knock = fromDir.normalized * Mathf.Max(0f, _def.knockbackStrength);

            FloatingText.Spawn(transform.position + Vector3.up * 0.7f,
                $"-{Mathf.CeilToInt(amount)}", new Color(1f, 0.85f, 0.3f));
            SfxManager.Play("hit", 0.7f, 0.12f);

            // Sight aggro on being hit, even outside aggroRadius.
            _engaged = true;

            if (_hp <= 0f) { Die(); return true; }
            SetState(MobAnimState.Hurt, restart: true);
            return false;
        }

        void Die()
        {
            if (_dead) return;
            _dead = true;
            _knock = Vector2.zero;
            _deathTimer = DurationFor(MobAnimState.Death, fallback: 0.6f);
            SetState(MobAnimState.Death, restart: true);
            SfxManager.Play("hit", 0.5f, 0.2f);
        }

        void FinalizeDeath()
        {
            AwardLootAndXp();
            MarkDefeated();                 // fires Defeated -> spawner removes + progression XP/evidence
            Destroy(gameObject);
        }

        void AwardLootAndXp()
        {
            if (_def == null) return;

            if (_loot != null && _def.drops != null)
            {
                foreach (var d in _def.drops)
                {
                    if (string.IsNullOrWhiteSpace(d.itemId)) continue;
                    if (d.chance < 1f && UnityEngine.Random.value > d.chance) continue;
                    int count = UnityEngine.Random.Range(Mathf.Min(d.min, d.max), Mathf.Max(d.min, d.max) + 1);
                    if (count <= 0) continue;
                    int leftover = _loot.Add(d.itemId, count);
                    int gained = count - leftover;
                    if (gained > 0)
                    {
                        var item = _loot.Content != null ? _loot.Content.Items.Get(d.itemId) : null;
                        FloatingText.Spawn(transform.position + Vector3.up * 0.95f,
                            $"+{gained} {(item != null ? item.Display : d.itemId)}", new Color(0.6f, 1f, 0.6f));
                    }
                }
            }

            if (_stats != null && _def.xpReward > 0)
            {
                _stats.AddExperience(_def.xpReward);
                FloatingText.Spawn(transform.position + Vector3.up * 1.15f,
                    $"+{_def.xpReward} XP", new Color(0.7f, 0.85f, 1f));
            }
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

        // ---------- Per-frame ----------

        void Update()
        {
            if (_world == null) return;
            float dt = Time.deltaTime;

            DecayHurtFlash(dt);
            ApplyKnockback(dt);

            if (_dead)
            {
                _deathTimer -= dt;
                Animate(dt);
                Place();
                if (_deathTimer <= 0f) FinalizeDeath();
                return;
            }

            _attackTimer -= dt;

            bool wantsCombat = ResolveAggro();
            _moving = false;

            if (_windingUp)
            {
                _windupTimer -= dt;
                FaceTowards(_player != null ? _player.Ground - _ground : _target - _ground);
                if (_windupTimer <= 0f) LandAttack();
            }
            else
            {
                if (wantsCombat) ChaseAndStrike(dt);
                else Wander(dt);
            }

            ChooseLocomotionState();
            Animate(dt);
            Place();
        }

        bool ResolveAggro()
        {
            if (_player == null || _def == null) return false;
            if (_def.contactDamage <= 0f && _def.attackDamage <= 0f && !_aggressive) return false;

            float dist = (_player.Ground - _ground).magnitude;
            if (_aggressive) return true;

            if (_engaged)
            {
                // Leash back home if dragged too far from the spawn point.
                if ((_ground - _home).magnitude > Mathf.Max(1f, _def.leashRadius)) _engaged = false;
            }
            else if (_def.aggroRadius > 0f && dist <= _def.aggroRadius)
            {
                _engaged = true;
            }
            return _engaged;
        }

        void ChaseAndStrike(float dt)
        {
            if (_player == null) return;
            Vector2 toPlayer = _player.Ground - _ground;
            float dist = toPlayer.magnitude;
            float range = Mathf.Max(0.1f, _def.attackRange);

            if (dist <= range)
            {
                FaceTowards(toPlayer);
                TryBeginAttack();
                return;
            }

            // Walk when close, run when far.
            float runFrom = range + 2.5f;
            float walk = Mathf.Max(0.1f, _def.moveSpeed);
            float run = _def.chaseSpeed > 0f ? _def.chaseSpeed : walk;
            bool running = dist > runFrom;
            float speed = running ? run : walk;
            _target = _player.Ground;
            if (MoveToward(_target, speed, dt)) _moving = true;
            _runningLocomotion = running;
        }

        bool _runningLocomotion;

        void Wander(float dt)
        {
            _runningLocomotion = false;
            // Leashing mob walks home; otherwise idle-wanders.
            Vector2 dest = _target;
            if (!_engaged && (_ground - _home).magnitude > _def.wanderRadius + 1f)
                dest = _home;

            _repathTimer -= dt;
            if (_repathTimer <= 0f || (dest - _ground).sqrMagnitude < 0.04f) { PickTarget(); dest = _target; }
            if (MoveToward(dest, Mathf.Max(0.1f, _def.moveSpeed), dt)) _moving = true;
        }

        bool MoveToward(Vector2 dest, float speed, float dt)
        {
            Vector2 dir = dest - _ground;
            if (dir.sqrMagnitude <= 0.0001f) return false;
            Vector2 step = dir.normalized * speed * dt;
            Vector2 np = _ground + step;
            var c = IsoGrid.WorldToCell(new Vector3(np.x, np.y, 0f));
            if (_world.IsWalkable(c.x, c.y)) { _ground = np; FaceTowards(dir); return true; }
            PickTarget();
            return false;
        }

        void TryBeginAttack()
        {
            if (_attackTimer > 0f || _windingUp) return;
            if (_def.attackDamage <= 0f && _def.contactDamage <= 0f) return;
            _windingUp = true;
            _windupTimer = Mathf.Max(0.05f, _def.attackWindupSeconds);
            SetState(MobAnimState.Attack, restart: true);
            // Telegraph: briefly tint toward a warning colour.
            if (_anim == null) _sr.color = Color.Lerp(_baseColor, new Color(1f, 0.6f, 0.3f), 0.6f);
        }

        void LandAttack()
        {
            _windingUp = false;
            _attackTimer = Mathf.Max(0.4f, _def.attackCooldownSeconds);
            if (_anim == null) _sr.color = _baseColor;

            if (_player == null || _stats == null) return;
            float range = Mathf.Max(0.1f, _def.attackRange) + 0.25f; // small lunge tolerance
            if ((_player.Ground - _ground).sqrMagnitude > range * range) return; // player escaped the tell

            float dmg = _def.attackDamage > 0f ? _def.attackDamage : _def.contactDamage;
            if (dmg <= 0f) return;
            _stats.Damage(dmg);
            FloatingText.Spawn(_player.transform.position + Vector3.up * 0.75f,
                $"-{Mathf.CeilToInt(dmg)} HP", new Color(1f, 0.35f, 0.25f));
            SfxManager.Play("hit", 0.8f);
        }

        // ---------- Movement helpers ----------

        void ApplyKnockback(float dt)
        {
            if (_knock.sqrMagnitude < 0.0004f) { _knock = Vector2.zero; return; }
            Vector2 np = _ground + _knock * dt;
            var c = IsoGrid.WorldToCell(new Vector3(np.x, np.y, 0f));
            if (_world.IsWalkable(c.x, c.y)) _ground = np;
            _knock = Vector2.Lerp(_knock, Vector2.zero, Mathf.Clamp01(dt * 8f));
        }

        void DecayHurtFlash(float dt)
        {
            if (_hurtFlashTimer <= 0f) return;
            _hurtFlashTimer -= dt;
            float t = Mathf.Clamp01(_hurtFlashTimer / 0.16f);
            _sr.color = Color.Lerp(_anim != null ? Color.white : _baseColor, new Color(1f, 0.4f, 0.4f), t);
            if (_hurtFlashTimer <= 0f) _sr.color = _anim != null ? Color.white : _baseColor;
        }

        void FaceTowards(Vector2 dir)
        {
            if (dir.sqrMagnitude > 0.0001f) _dir = MobAnimationLibrary.DirIndex(dir);
        }

        void PickTarget()
        {
            _repathTimer = _def.repathSeconds;
            float ang = UnityEngine.Random.value * Mathf.PI * 2f;
            float r = UnityEngine.Random.value * _def.wanderRadius;
            _target = _home + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * r;
        }

        void Place()
        {
            var c = IsoGrid.WorldToCell(new Vector3(_ground.x, _ground.y, 0f));
            _height = _world.GetHeight(c.x, c.y);
            transform.position = new Vector3(_ground.x, _ground.y + _height * IsoGrid.HeightStep, 0f);
            _sr.sortingOrder = IsoGrid.SortingOrder(c.x, c.y, _height, IsoGrid.LayerActor);
        }

        // ---------- Animation ----------

        void ChooseLocomotionState()
        {
            if (_dead || _windingUp || _animState == MobAnimState.Hurt && _hurtFlashTimer > 0f) return;
            if (_moving) SetState(_runningLocomotion ? MobAnimState.Run : MobAnimState.Walk, restart: false);
            else SetState(MobAnimState.Idle, restart: false);
        }

        void SetState(MobAnimState s, bool restart)
        {
            if (_animState == s && !restart) return;
            _animState = s;
            if (restart) { _frame = 0; _animTimer = 0f; }
        }

        void Animate(float dt)
        {
            if (_anim != null) { AnimateDirectional(dt); return; }
            if (!_legacyAnimated) return;

            var frames = _moving && _move.Length > 0 ? _move : _idle;
            if (_animState == MobAnimState.Hurt && _hurt != null && _hurt.Length > 0) frames = _hurt;
            if (_animState == MobAnimState.Death && _die != null && _die.Length > 0) frames = _die;
            if (frames == null || frames.Length == 0) return;
            _animTimer += dt;
            float spf = 1f / LegacyFps;
            while (_animTimer >= spf) { _animTimer -= spf; _frame++; }
            _sr.sprite = frames[_frame % frames.Length];
        }

        void AnimateDirectional(float dt)
        {
            var frames = _anim.State(_animState).For(_dir);
            if ((frames == null || frames.Length == 0) && _animState != MobAnimState.Idle)
                frames = _anim.idle.For(_dir);
            if (frames == null || frames.Length == 0) return;

            float spf = Mathf.Max(0.03f, _anim.secondsPerFrame);
            _animTimer += dt;
            while (_animTimer >= spf) { _animTimer -= spf; _frame++; }

            // Non-looping states clamp on their last frame.
            int idx;
            if (_animState == MobAnimState.Death || _animState == MobAnimState.Attack || _animState == MobAnimState.Hurt)
                idx = Mathf.Min(_frame, frames.Length - 1);
            else
                idx = _frame % frames.Length;
            _sr.sprite = frames[idx];
        }

        float DurationFor(MobAnimState s, float fallback)
        {
            if (_anim != null)
            {
                var frames = _anim.State(s).For(_dir);
                if (frames != null && frames.Length > 0)
                    return frames.Length * Mathf.Max(0.03f, _anim.secondsPerFrame);
            }
            if (_legacyAnimated && s == MobAnimState.Death && _die != null && _die.Length > 0)
                return _die.Length / LegacyFps;
            return fallback;
        }

        Sprite FirstFrame()
        {
            if (_anim == null) return null;
            var f = _anim.idle.For(0);
            return f != null && f.Length > 0 ? f[0] : null;
        }

        // Directional library first (predator plants), then the legacy slime convention, then blob.
        void LoadAnimation(MobDefinition def)
        {
            if (def == null) return;

            var lib = MobAnimationLibrary.Load();
            if (lib != null)
            {
                _anim = lib.Find(string.IsNullOrEmpty(def.animationKey) ? def.id : def.animationKey);
                if (_anim != null && (_anim.idle.HasAny || _anim.walk.HasAny)) return;
                _anim = null;
            }

            string folder = null, prefix = null;
            if (def.id == "slime") { folder = "Enemies/Slime/Individual Sprites"; prefix = "slime"; }
            if (folder == null) { _legacyAnimated = false; return; }

            _idle = LoadFrames(folder, prefix, "idle", 4);
            _move = LoadFrames(folder, prefix, "move", 4);
            _hurt = LoadFrames(folder, prefix, "hurt", 4);
            _die = LoadFrames(folder, prefix, "die", 4);
            _legacyAnimated = _idle.Length > 0 || _move.Length > 0;
            if (_move.Length == 0) _move = _idle;
        }

        static Sprite[] LoadFrames(string folder, string prefix, string state, int max)
        {
            var list = new List<Sprite>();
            for (int i = 0; i < max; i++)
            {
                var s = Resources.Load<Sprite>($"{folder}/{prefix}-{state}-{i}");
                if (s != null) list.Add(s);
            }
            return list.ToArray();
        }
    }
}
