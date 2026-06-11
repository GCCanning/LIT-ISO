using System.Collections.Generic;
using UnityEngine;

namespace IsoCore.Foundation
{
    public sealed class FoundationDungeonPortalSystem : MonoBehaviour
    {
        IsoWorld _world;
        FoundationContent _content;
        FoundationConfig _config;
        IsoFoundationPlayer _player;
        FoundationInstanceSystem _instances;
        MobSpawner _mobSpawner;
        FoundationInteractionOverlay _overlay;
        FoundationProgression _progression;
        FoundationLaunchMode _launchMode;
        Transform _parent;

        readonly Dictionary<long, FoundationDungeonPortalInstance> _byCell = new();
        readonly Dictionary<string, FoundationDungeonPortalInstance> _byId = new();
        readonly Dictionary<string, FoundationSavedDungeonHistory> _historyByPortal = new();
        FoundationSavedDungeon _activeDungeon;

        static long Key(int x, int y) => ((long)(uint)x << 32) | (uint)y;

        public void Init(IsoWorld world, FoundationContent content, FoundationConfig config,
            IsoFoundationPlayer player, FoundationInstanceSystem instances, MobSpawner mobSpawner,
            FoundationInteractionOverlay overlay, FoundationProgression progression = null,
            FoundationLaunchMode launchMode = FoundationLaunchMode.Standard)
        {
            _world = world;
            _content = content;
            _config = config;
            _player = player;
            _instances = instances;
            _mobSpawner = mobSpawner;
            _overlay = overlay;
            _progression = progression;
            _launchMode = launchMode;

            _parent = new GameObject("DungeonPortals").transform;
            _parent.SetParent(transform, false);
            SpawnInitialPortals();
        }

        public FoundationDungeonPortalInstance PortalAtCell(int wx, int wy)
        {
            return _byCell.TryGetValue(Key(wx, wy), out var portal) && portal ? portal : null;
        }

        public FoundationDungeonPortalInstance[] SnapshotPortals()
        {
            if (_byId.Count == 0)
                return System.Array.Empty<FoundationDungeonPortalInstance>();

            var portals = new FoundationDungeonPortalInstance[_byId.Count];
            int i = 0;
            foreach (var portal in _byId.Values)
                if (portal != null)
                    portals[i++] = portal;

            if (i == portals.Length)
                return portals;

            System.Array.Resize(ref portals, i);
            return portals;
        }

        public bool TryGetPortalUnderCursor(Camera cam, Vector2 screenPosition, out FoundationDungeonPortalInstance portal)
        {
            portal = null;
            if (cam == null || _byId.Count == 0)
                return false;

            var wp = cam.ScreenToWorldPoint(screenPosition);
            var point = new Vector2(wp.x, wp.y);
            int bestOrder = int.MinValue;
            foreach (var candidate in _byId.Values)
            {
                if (candidate == null || !candidate.ContainsWorldPoint(point))
                    continue;

                int order = candidate.SortingOrder;
                if (portal != null && order < bestOrder)
                    continue;

                portal = candidate;
                bestOrder = order;
            }

            return portal != null;
        }

        public bool Enter(FoundationDungeonPortalInstance portal)
        {
            if (portal == null || _instances == null || _world == null)
                return false;

            var origin = OriginFor(portal.PortalId);
            int seed = _config != null ? _config.seed : 1337;
            var build = FoundationDungeonGenerator.Generate(_content, portal.DungeonId,
                portal.DisplayName, seed, portal.Cell, origin, portal.Tier);

            if (!_instances.EnterDungeon(build, portal.Cell))
                return false;

            bool hasHistory = TryGetHistory(portal.PortalId, out var history);
            _activeDungeon = new FoundationSavedDungeon
            {
                active = true,
                portalId = portal.PortalId,
                dungeonId = portal.DungeonId,
                displayName = portal.DisplayName,
                tier = portal.Tier,
                layoutSeed = build.layoutSeed,
                portalX = portal.Cell.x,
                portalY = portal.Cell.y,
                resultId = ResultIdFor(portal.DungeonId),
                rewardOpened = hasHistory && history.rewardOpened,
                completed = hasHistory && history.completed,
            };
            SpawnDungeonMobs(build);
            if (_activeDungeon.rewardOpened)
                _overlay?.Flash("This portal's reward has already been claimed.", 3f);
            return true;
        }

        public bool IsActiveDungeon => _activeDungeon.active;
        public bool RewardOpened => _activeDungeon.rewardOpened;
        public bool Completed => _activeDungeon.completed;
        public string PortalIdForActiveDungeon => _activeDungeon.active ? _activeDungeon.portalId ?? "" : "";

        public bool OpenReward()
        {
            if (!_activeDungeon.active)
                return false;
            if (_activeDungeon.rewardOpened)
            {
                _overlay?.Flash("This reward is already claimed.");
                return false;
            }

            if (!ApplyResult())
                return false;

            _activeDungeon.rewardOpened = true;
            RememberActiveDungeon();
            ApplyHistoryVisual(_activeDungeon.portalId);
            _overlay?.Flash("Dungeon reward claimed. Result recorded.", 3f);
            return true;
        }

        public bool CompleteAndExit()
        {
            if (!_activeDungeon.active)
                return false;

            if (!_activeDungeon.completed)
            {
                if (!_activeDungeon.rewardOpened && !ApplyResult())
                    return false;
                if (!_activeDungeon.rewardOpened)
                    _activeDungeon.rewardOpened = true;
                _activeDungeon.completed = true;
                RememberActiveDungeon();
                ApplyHistoryVisual(_activeDungeon.portalId);
            }

            bool exited = _instances != null && _instances.Exit();
            if (exited)
                _activeDungeon = default;
            return exited;
        }

        /// <summary>
        /// Death respawn (audit rec #3): leave the active dungeon without granting
        /// completion, rewards, or history. Same exit path as CompleteAndExit, minus
        /// the result bookkeeping — the run is simply abandoned.
        /// </summary>
        public bool AbandonAndExit()
        {
            if (!_activeDungeon.active)
                return false;

            bool exited = _instances != null && _instances.Exit();
            if (exited)
                _activeDungeon = default;
            return exited;
        }

        public FoundationSavedDungeon CaptureState()
        {
            if (_instances == null || !_instances.IsInsideInstance ||
                string.IsNullOrWhiteSpace(_instances.ActiveInstanceId) ||
                !_instances.ActiveInstanceId.StartsWith("dungeon_", System.StringComparison.Ordinal))
                return default;

            _activeDungeon.active = true;
            RememberActiveDungeon();
            return _activeDungeon;
        }

        public FoundationSavedDungeonHistory[] CaptureHistory()
        {
            if (_activeDungeon.active)
                RememberActiveDungeon();

            if (_historyByPortal.Count == 0)
                return System.Array.Empty<FoundationSavedDungeonHistory>();

            var result = new FoundationSavedDungeonHistory[_historyByPortal.Count];
            int i = 0;
            foreach (var kv in _historyByPortal)
                result[i++] = kv.Value;
            return result;
        }

        public void RestoreHistory(FoundationSavedDungeonHistory[] history)
        {
            _historyByPortal.Clear();
            if (history == null)
                return;

            for (int i = 0; i < history.Length; i++)
            {
                var entry = history[i];
                if (string.IsNullOrWhiteSpace(entry.portalId))
                    continue;
                _historyByPortal[entry.portalId] = entry;
                ApplyHistoryVisual(entry.portalId);
            }
        }

        public void RestoreState(FoundationSavedDungeon state, FoundationSavedInstance instance)
        {
            _activeDungeon = default;
            if (!state.active || string.IsNullOrWhiteSpace(state.dungeonId) ||
                string.IsNullOrWhiteSpace(state.portalId) || _instances == null || _world == null)
                return;

            int seed = _config != null ? _config.seed : 1337;
            var portalCell = new Vector2Int(state.portalX, state.portalY);
            var origin = OriginFor(state.portalId);
            var build = FoundationDungeonGenerator.Generate(_content, state.dungeonId,
                state.displayName, seed, portalCell, origin, state.tier);

            _instances.EnterDungeon(build,
                new Vector2Int(instance.returnCellX, instance.returnCellY),
                new Vector2(instance.returnGroundX, instance.returnGroundY));

            _activeDungeon = state;
            _activeDungeon.layoutSeed = build.layoutSeed;
            if (TryGetHistory(state.portalId, out var history))
            {
                _activeDungeon.rewardOpened |= history.rewardOpened;
                _activeDungeon.completed |= history.completed;
            }
            RememberActiveDungeon();
            ApplyHistoryVisual(_activeDungeon.portalId);
            SpawnDungeonMobs(build);
        }

        bool ApplyResult()
        {
            string resultId = string.IsNullOrWhiteSpace(_activeDungeon.resultId)
                ? ResultIdFor(_activeDungeon.dungeonId)
                : _activeDungeon.resultId;
            var result = !string.IsNullOrWhiteSpace(resultId)
                ? _content?.DungeonResults.Get(resultId)
                : null;
            if (result == null)
            {
                _overlay?.Flash("Dungeon result unavailable.");
                return false;
            }

            _progression?.ApplyDungeonResult(result, Mathf.Max(1, _activeDungeon.tier));
            return true;
        }

        bool TryGetHistory(string portalId, out FoundationSavedDungeonHistory history)
        {
            if (!string.IsNullOrWhiteSpace(portalId) &&
                _historyByPortal.TryGetValue(portalId, out history))
                return true;

            history = default;
            return false;
        }

        void RememberActiveDungeon()
        {
            if (!_activeDungeon.active || string.IsNullOrWhiteSpace(_activeDungeon.portalId))
                return;

            _historyByPortal[_activeDungeon.portalId] = new FoundationSavedDungeonHistory
            {
                portalId = _activeDungeon.portalId,
                dungeonId = _activeDungeon.dungeonId,
                resultId = string.IsNullOrWhiteSpace(_activeDungeon.resultId)
                    ? ResultIdFor(_activeDungeon.dungeonId)
                    : _activeDungeon.resultId,
                tier = Mathf.Max(1, _activeDungeon.tier),
                rewardOpened = _activeDungeon.rewardOpened,
                completed = _activeDungeon.completed,
            };
        }

        void ApplyHistoryVisual(string portalId)
        {
            if (string.IsNullOrWhiteSpace(portalId) ||
                !_byId.TryGetValue(portalId, out var portal) ||
                portal == null)
                return;

            if (TryGetHistory(portalId, out var history))
                portal.SetHistoryState(history.rewardOpened, history.completed);
            else
                portal.SetHistoryState(false, false);
        }

        string ResultIdFor(string dungeonId)
        {
            var dungeon = _content?.Dungeons.Get(dungeonId);
            return dungeon != null ? dungeon.resultId : "";
        }

        void SpawnInitialPortals()
        {
            if (_world == null || _content == null)
                return;

            if (_launchMode == FoundationLaunchMode.CreationInstance)
            {
                SpawnCreationInstancePortals();
                return;
            }

            var dungeon = _content.Dungeons.Get("rootcellar_starter");
            string dungeonId = dungeon != null ? dungeon.id : "rootcellar_starter";
            string display = dungeon != null ? dungeon.Display : "Rootcellar Gate";

            int seed = _config != null ? _config.seed : 1337;
            var rng = new System.Random(seed ^ 0x51c0de);
            int[] radii = { 10, 18, 28, 42, 60, 82, 108, 138 };

            for (int i = 0; i < radii.Length; i++)
            {
                int radius = radii[i];
                float angle = (float)(rng.NextDouble() * Mathf.PI * 2f);
                int x = Mathf.RoundToInt(Mathf.Cos(angle) * radius);
                int y = Mathf.RoundToInt(Mathf.Sin(angle) * radius);
                var cell = FindNearestPortalCell(new Vector2Int(x, y), 8);
                if (cell == null) continue;

                int tier = TierForDistance(cell.Value);
                string id = $"portal_{dungeonId}_{cell.Value.x}_{cell.Value.y}";
                SpawnPortal(id, dungeonId, $"{display} T{tier}", tier, cell.Value);
            }
        }

        void SpawnCreationInstancePortals()
        {
            var dungeon = _content.Dungeons.Get("rootcellar_starter");
            string dungeonId = dungeon != null ? dungeon.id : "rootcellar_starter";
            string display = dungeon != null ? dungeon.Display : "Rootcellar Gate";
            int[] tierColumns = { 12, 18, 24, 30, 36, 42 };
            int[] variantRows = { 11, 20 };
            string[] variantLabels = { "Baseline", "Reroll Variant" };

            for (int row = 0; row < variantRows.Length; row++)
            for (int tier = 1; tier <= 6; tier++)
            {
                var cell = new Vector2Int(tierColumns[tier - 1], variantRows[row]);
                string id = $"creation_portal_{dungeonId}_t{tier}_v{row + 1}";
                SpawnPortal(id, dungeonId, $"{display} T{tier} - {variantLabels[row]}", tier, cell);
            }
        }

        Vector2Int? FindNearestPortalCell(Vector2Int around, int searchRadius)
        {
            Vector2Int? best = null;
            int bestDist = int.MaxValue;
            for (int r = 0; r <= searchRadius; r++)
            for (int y = around.y - r; y <= around.y + r; y++)
            for (int x = around.x - r; x <= around.x + r; x++)
            {
                if (Mathf.Max(Mathf.Abs(x - around.x), Mathf.Abs(y - around.y)) != r)
                    continue;
                if (_byCell.ContainsKey(Key(x, y)))
                    continue;
                var cell = _world.GetCell(x, y);
                if (cell.Blocked || cell.HasNode || cell.HasOccupant || cell.Water)
                    continue;

                int d = (x - around.x) * (x - around.x) + (y - around.y) * (y - around.y);
                if (d < bestDist)
                {
                    bestDist = d;
                    best = new Vector2Int(x, y);
                }
            }
            return best;
        }

        void SpawnPortal(string id, string dungeonId, string display, int tier, Vector2Int cell)
        {
            var go = new GameObject($"DungeonPortal_{tier}_{cell.x}_{cell.y}");
            go.transform.SetParent(_parent, false);
            var portal = go.AddComponent<FoundationDungeonPortalInstance>();
            portal.Init(id, dungeonId, display, tier, cell, _world, ColorForTier(tier));
            _byCell[Key(cell.x, cell.y)] = portal;
            _byId[id] = portal;
            ApplyHistoryVisual(id);
        }

        void SpawnDungeonMobs(FoundationDungeonBuild build)
        {
            if (_mobSpawner == null || build?.mobs == null) return;

            foreach (var spawn in build.mobs)
            {
                var def = _content?.Mobs.Get(spawn.mobId);
                if (def == null) continue;
                var world = IsoGrid.CellToWorld(spawn.x, spawn.y, 0);
                _mobSpawner.SpawnMobAt(def, new Vector2(world.x, world.y));
            }

            _overlay?.Flash($"Dungeon tier {build.tier}: mobs scale with distance from spawn", 3.5f);
        }

        static int TierForDistance(Vector2Int cell)
        {
            float d = Mathf.Sqrt(cell.x * cell.x + cell.y * cell.y);
            return Mathf.Clamp(1 + Mathf.FloorToInt(d / 30f), 1, 6);
        }

        static Color ColorForTier(int tier)
        {
            return FoundationPortalVisual.ColorForTier(tier);
        }

        static Vector2Int OriginFor(string id)
        {
            unchecked
            {
                int hash = 23;
                if (!string.IsNullOrEmpty(id))
                    for (int i = 0; i < id.Length; i++)
                        hash = hash * 31 + id[i];

                int bucketX = Mathf.Abs(hash % 128);
                int bucketY = Mathf.Abs((hash / 128) % 128);
                return new Vector2Int(60000 + bucketX * 48, 60000 + bucketY * 48);
            }
        }
    }
}
