using System;
using System.Collections.Generic;
using UnityEngine;

namespace IsoCore.Foundation
{
    /// <summary>
    /// Spawns biome-appropriate wildlife in a ring around the player up to a cap,
    /// and despawns mobs that wander too far. Bounded per-interval work.
    /// </summary>
    public class MobSpawner : MonoBehaviour
    {
        IsoWorld _world;
        FoundationContent _content;
        FoundationConfig _cfg;
        IsoFoundationPlayer _player;
        FoundationPlayerStats _stats;
        FoundationInstanceSystem _instances;
        FoundationCampingSystem _camping;
        Transform _mobParent;

        readonly List<Mob> _mobs = new();
        float _timer;
        Vector2 _spawnOrigin; // the hearth/spawn point; distance from here drives enemy tier

        public event Action<MobDefinition> MobDefeated;
        public event Action<MobDefinition> MobCalmed;

        public int Count => _mobs.Count;

        /// <summary>Closest living mob within <paramref name="radius"/> world units of a point, or null.
        /// Used by ability projectiles for hit detection.</summary>
        public Mob FindMobNear(Vector2 worldPos, float radius)
        {
            float best = radius * radius;
            Mob hit = null;
            for (int i = _mobs.Count - 1; i >= 0; i--)
            {
                var m = _mobs[i];
                if (!m) continue;
                Vector2 p = m.transform.position;
                float d = (p - worldPos).sqrMagnitude;
                if (d <= best) { best = d; hit = m; }
            }
            return hit;
        }

        public void Init(IsoWorld world, FoundationContent content, FoundationConfig cfg, IsoFoundationPlayer player,
            FoundationPlayerStats stats = null)
        {
            _world = world; _content = content; _cfg = cfg; _player = player; _stats = stats;
            _mobParent = new GameObject("Mobs").transform;
            _mobParent.SetParent(transform, false);
            _timer = 1f;
            _spawnOrigin = player != null ? player.Ground : Vector2.zero; // player starts at the hearth
        }

        public void SetInstanceSystem(FoundationInstanceSystem instances)
        {
            _instances = instances;
        }

        public void SetCampingSystem(FoundationCampingSystem camping)
        {
            _camping = camping;
        }

        void Update()
        {
            if (_world == null) return;
            _timer -= Time.deltaTime;
            if (_timer <= 0f)
            {
                _timer = _cfg.mobSpawnInterval;
                if (_instances == null || !_instances.IsInsideInstance)
                    TrySpawn();
            }
            DespawnFar();
        }

        public Mob SpawnMobAt(MobDefinition def, Vector2 ground)
        {
            if (def == null || _world == null)
                return null;

            var c = IsoGrid.WorldToCell(new Vector3(ground.x, ground.y, 0f));
            if (!_world.IsWalkable(c.x, c.y))
                return null;

            return SpawnMob(def, ground, def.behaviour == MobBehavior.Hostile);
        }

        void TrySpawn()
        {
            _mobs.RemoveAll(m => !m);
            if (_mobs.Count >= _cfg.mobCap) return;

            Vector2 origin = _player.Ground; // height-0 plane (WorldToCell assumes this)
            float ang = UnityEngine.Random.value * Mathf.PI * 2f;
            float dist = UnityEngine.Random.Range(_cfg.mobSpawnRadius * 0.5f, _cfg.mobSpawnRadius);
            Vector2 ground = origin + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * dist;

            var c = IsoGrid.WorldToCell(new Vector3(ground.x, ground.y, 0f));
            if (!_world.IsWalkable(c.x, c.y)) return;

            var biome = _world.GetBiome(c.x, c.y);
            var def = PickMob(biome);
            if (def == null) return;
            bool breachedWard = false;
            if (_camping != null && _camping.RollMobSpawnWard(def, ground, out breachedWard)) return;

            // Night danger (Impact Analysis A2): mobs spawning at night, outside an active
            // campfire ward (or breaching a weak ward), are more dangerous.
            bool nightDanger = _camping != null && _camping.IsNight && (breachedWard || !_camping.AtCampsite);
            bool aggressive = breachedWard || def.behaviour == MobBehavior.Hostile;
            SpawnMob(def, ground, aggressive, nightDanger);
            // Parties: spawn the rest of the group around the leader (bandit pairs, adventurer trios).
            for (int gi = 1; gi < Mathf.Max(1, def.groupSize); gi++)
            {
                Vector2 off = ground + new Vector2(UnityEngine.Random.Range(-1.6f, 1.6f), UnityEngine.Random.Range(-1.6f, 1.6f));
                var oc = IsoGrid.WorldToCell(new Vector3(off.x, off.y, 0f));
                if (_world.IsWalkable(oc.x, oc.y)) SpawnMob(def, off, aggressive, nightDanger);
            }
            // Camp companion (e.g. a campfire) anchors a bandit camp at the group's centre.
            if (_content != null && !string.IsNullOrEmpty(def.companionMobId))
            {
                var comp = _content.Mobs.Get(def.companionMobId);
                if (comp != null) SpawnMob(comp, ground, false, false);
            }
        }

        Mob SpawnMob(MobDefinition def, Vector2 ground, bool aggressive = false, bool nightDanger = false)
        {
            var go = new GameObject($"Mob_{def.id}");
            go.transform.SetParent(_mobParent, false);
            var mob = go.AddComponent<Mob>();
            mob.SetContent(_content); // Phase 3: lets the mob resolve its abilityIds for casts
            mob.Init(def, _world, ground);
            mob.SetCombatContext(_player, _stats, aggressive);
            // Distance-from-spawn difficulty gradient: every ~24 world units out = +1 level,
            // +35% HP and +20% damage. Enemies far from the hearth are tougher.
            int tierLevel = 1 + Mathf.FloorToInt((ground - _spawnOrigin).magnitude / 24f);
            if (tierLevel > 1) mob.ApplyTier(tierLevel, 1f + (tierLevel - 1) * 0.35f, 1f + (tierLevel - 1) * 0.20f);
            if (nightDanger && _cfg != null)
                mob.ApplyNightDanger(_cfg.nightMobDamageMultiplier, _cfg.nightMobSpeedMultiplier, _cfg.nightAggroRangeMultiplier);
            mob.Defeated += HandleMobDefeated;
            mob.Calmed += HandleMobCalmed;
            _mobs.Add(mob);
            return mob;
        }

        MobDefinition PickMob(BiomeDefinition biome)
        {
            if (biome == null || biome.mobs == null || biome.mobs.Length == 0) return null;
            float total = 0f;
            foreach (var m in biome.mobs) if (m.mob != null) total += Mathf.Max(0f, m.weight);
            if (total <= 0f) return null;

            float roll = UnityEngine.Random.value * total;
            foreach (var m in biome.mobs)
            {
                float w = m.mob != null ? Mathf.Max(0f, m.weight) : 0f;
                if (w <= 0f) continue; // never select a zero/negative-weight entry
                roll -= w;
                if (roll < 0f) return m.mob;
            }
            return null;
        }

        void DespawnFar()
        {
            float r2 = _cfg.mobDespawnRadius * _cfg.mobDespawnRadius;
            Vector2 p = _player.Ground;
            for (int i = _mobs.Count - 1; i >= 0; i--)
            {
                var m = _mobs[i];
                if (!m) { _mobs.RemoveAt(i); continue; }
                if ((m.Ground - p).sqrMagnitude > r2) // planar distance on the height-0 plane
                {
                    m.Defeated -= HandleMobDefeated;
                    m.Calmed -= HandleMobCalmed;
                    Destroy(m.gameObject);
                    _mobs.RemoveAt(i);
                }
            }
        }

        void HandleMobDefeated(Mob mob)
        {
            if (mob == null) return;
            mob.Defeated -= HandleMobDefeated;
            mob.Calmed -= HandleMobCalmed;
            _mobs.Remove(mob);
            MobDefeated?.Invoke(mob.Def);
        }

        void HandleMobCalmed(Mob mob)
        {
            if (mob == null) return;
            mob.Defeated -= HandleMobDefeated;
            mob.Calmed -= HandleMobCalmed;
            MobCalmed?.Invoke(mob.Def);
        }

        public FoundationSavedMob[] SnapshotMobs()
        {
            _mobs.RemoveAll(m => !m);
            var result = new FoundationSavedMob[_mobs.Count];
            for (int i = 0; i < _mobs.Count; i++)
            {
                var mob = _mobs[i];
                result[i] = new FoundationSavedMob
                {
                    mobId = mob.Def != null ? mob.Def.id : "",
                    groundX = mob.Ground.x,
                    groundY = mob.Ground.y,
                };
            }
            return result;
        }

        public void RestoreMobs(FoundationSavedMob[] mobs)
        {
            ClearMobs();
            if (mobs == null || _content == null || _world == null) return;

            foreach (var saved in mobs)
            {
                var def = _content.Mobs.Get(saved.mobId);
                if (def == null) continue;
                var ground = new Vector2(saved.groundX, saved.groundY);
                var c = IsoGrid.WorldToCell(new Vector3(ground.x, ground.y, 0f));
                if (!_world.IsWalkable(c.x, c.y)) continue;
                SpawnMob(def, ground);
            }
        }

        void ClearMobs()
        {
            foreach (var mob in _mobs)
            {
                if (!mob) continue;
                mob.Defeated -= HandleMobDefeated;
                mob.Calmed -= HandleMobCalmed;
                if (Application.isPlaying) Destroy(mob.gameObject);
                else DestroyImmediate(mob.gameObject);
            }
            _mobs.Clear();
        }
    }
}
