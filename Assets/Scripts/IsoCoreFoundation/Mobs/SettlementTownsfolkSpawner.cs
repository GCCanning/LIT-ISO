using System.Collections.Generic;
using UnityEngine;

namespace IsoCore.Foundation
{
    /// <summary>
    /// Brings settlements to life: while the player stands in or near a settlement (detected by
    /// nearby stone-path road/plaza cells), maintains a small population of passive townsfolk
    /// that wander the streets. Townsfolk spawn through <see cref="MobSpawner"/> (passive, zero
    /// contact damage, humanoid appearance) and are cleared when the player leaves the settlement
    /// or enters an interior/dungeon instance. Population scales loosely with how much road is
    /// nearby, so a hamlet feels sparse and a city feels busy.
    /// </summary>
    public sealed class SettlementTownsfolkSpawner : MonoBehaviour
    {
        IsoWorld _world;
        IsoFoundationPlayer _player;
        MobSpawner _mobs;
        FoundationContent _content;
        FoundationInstanceSystem _instances;

        readonly List<Mob> _townsfolk = new();
        readonly List<Vector2Int> _roadCells = new();
        static readonly string[] TownsfolkIds = { "townsfolk_villager", "townsfolk_merchant" };

        float _timer;
        const float Interval = 1.5f;   // re-evaluate population a few times a second is overkill
        const int ScanRadius = 10;     // cells around the player to scan for settlement roads
        const int RoadThreshold = 5;   // >= this many path cells nearby == "in a settlement"
        const int MaxTownsfolk = 8;

        public void Init(IsoWorld world, IsoFoundationPlayer player, MobSpawner mobs,
                         FoundationContent content, FoundationInstanceSystem instances)
        {
            _world = world; _player = player; _mobs = mobs; _content = content; _instances = instances;
            _timer = 1f;
        }

        void Update()
        {
            if (_world == null || _player == null || _mobs == null) return;
            _timer -= Time.deltaTime;
            if (_timer > 0f) return;
            _timer = Interval;

            _townsfolk.RemoveAll(m => !m);

            // Never populate interiors/dungeons.
            if (_instances != null && _instances.IsInsideInstance) { DespawnAll(); return; }

            ScanRoads();
            if (_roadCells.Count < RoadThreshold) { DespawnAll(); return; }

            // Population scales with nearby road density (hamlet sparse, city busy).
            int target = Mathf.Clamp(2 + _roadCells.Count / 12, 2, MaxTownsfolk);
            int guard = 0;
            while (_townsfolk.Count < target && guard++ < 10)
            {
                var cell = _roadCells[Random.Range(0, _roadCells.Count)];
                var def = _content.Mobs.Get(TownsfolkIds[Random.Range(0, TownsfolkIds.Length)]);
                if (def == null) break;
                var world = IsoGrid.CellToWorld(cell.x, cell.y, 0);
                var mob = _mobs.SpawnMobAt(def, new Vector2(world.x, world.y));
                if (mob == null) break;
                _townsfolk.Add(mob);
            }
        }

        void ScanRoads()
        {
            _roadCells.Clear();
            var pc = _player.CurrentCell;
            for (int dy = -ScanRadius; dy <= ScanRadius; dy++)
            for (int dx = -ScanRadius; dx <= ScanRadius; dx++)
            {
                int x = pc.x + dx, y = pc.y + dy;
                if (!_world.IsWalkable(x, y)) continue;
                if (_world.GetCell(x, y).SurfaceBlockId == "stone_path")
                    _roadCells.Add(new Vector2Int(x, y));
            }
        }

        void DespawnAll()
        {
            for (int i = _townsfolk.Count - 1; i >= 0; i--)
                if (_townsfolk[i]) Destroy(_townsfolk[i].gameObject);
            _townsfolk.Clear();
        }
    }
}
