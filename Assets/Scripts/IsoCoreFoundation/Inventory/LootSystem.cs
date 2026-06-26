using System.Collections.Generic;
using UnityEngine;

namespace IsoCore.Foundation
{
    /// <summary>
    /// Phase 2 loot. Owns named loot tables (id -> weighted <see cref="ItemDrop"/>[]) and two
    /// grant paths:
    ///   (1) mob defeats — subscribes to <see cref="MobSpawner.MobDefeated"/>, rolls the mob's
    ///       own <see cref="MobDefinition.drops"/>, and adds the results to the player inventory;
    ///   (2) containers — populates a placed <see cref="StorageContainer"/> from a named loot
    ///       table the first time it is opened (chests assigned a lootTableId).
    /// Pure data/logic, Foundation-only.
    /// </summary>
    public sealed class LootSystem
    {
        readonly FoundationContent _content;
        readonly Inventory _inventory;
        readonly StorageSystem _storage;
        MobSpawner _spawner;

        readonly Dictionary<string, ItemDrop[]> _tables = new();
        // Containers already filled, keyed by cell, so re-opening a chest doesn't re-roll loot.
        readonly HashSet<string> _filledContainers = new();

        public LootSystem(FoundationContent content, Inventory inventory, StorageSystem storage)
        {
            _content = content;
            _inventory = inventory;
            _storage = storage;
            RegisterDefaultTables();
        }

        public void Init(MobSpawner spawner)
        {
            _spawner = spawner;
            if (_spawner != null)
                _spawner.MobDefeated += OnMobDefeated;
            if (_storage != null)
                _storage.ContainerOpened += OnContainerOpened;
        }

        public void Shutdown()
        {
            if (_spawner != null)
                _spawner.MobDefeated -= OnMobDefeated;
            if (_storage != null)
                _storage.ContainerOpened -= OnContainerOpened;
        }

        public void RegisterTable(string id, ItemDrop[] drops)
        {
            if (string.IsNullOrEmpty(id) || drops == null) return;
            _tables[id] = drops;
        }

        public bool HasTable(string id) => !string.IsNullOrEmpty(id) && _tables.ContainsKey(id);

        // ---- Mob path: roll the mob definition's own drops into the inventory ----
        void OnMobDefeated(MobDefinition def)
        {
            if (def == null || def.drops == null || _inventory == null) return;
            foreach (var drop in def.drops)
                GrantDrop(drop, (id, count) => _inventory.Add(id, count));
        }

        // ---- Container path: fill a freshly-opened chest from its assigned loot table ----
        void OnContainerOpened(StorageContainer container)
        {
            if (container == null) return;
            string key = $"{container.X}:{container.Y}";
            if (_filledContainers.Contains(key)) return;

            var placeable = _content?.Placeables.Get(container.PlaceableId);
            string tableId = placeable != null ? placeable.lootTableId : null;
            if (string.IsNullOrEmpty(tableId) || !_tables.TryGetValue(tableId, out var drops))
            {
                _filledContainers.Add(key); // nothing to roll, but don't keep re-checking
                return;
            }

            // Only populate an empty container so we never overwrite player-stored or
            // save-restored contents.
            if (!container.IsEmpty)
            {
                _filledContainers.Add(key);
                return;
            }

            foreach (var drop in drops)
                GrantDrop(drop, (id, count) => container.Add(id, count));
            _filledContainers.Add(key);
        }

        /// <summary>Marks a container cell as already-filled (e.g. after a save restore) so loot
        /// is not re-rolled into it on first open.</summary>
        public void MarkContainerFilled(int x, int y) => _filledContainers.Add($"{x}:{y}");

        static void GrantDrop(ItemDrop drop, System.Func<string, int, int> grant)
        {
            if (string.IsNullOrEmpty(drop.itemId)) return;
            if (drop.chance < 1f && Random.value > drop.chance) return;
            int lo = Mathf.Min(drop.min, drop.max);
            int hi = Mathf.Max(drop.min, drop.max);
            int count = Random.Range(lo, hi + 1);
            if (count > 0) grant(drop.itemId, count);
        }

        void RegisterDefaultTables()
        {
            // Equipment-bearing chest tables. Items reference the Phase 1 starter gear so a
            // looted chest can yield a real wearable upgrade.
            RegisterTable("chest_common", new[]
            {
                new ItemDrop("apple", 1, 3, 1f),
                new ItemDrop("leather", 1, 2, 0.8f),
                new ItemDrop("leather_jerkin", 1, 1, 0.35f),
                new ItemDrop("oak_shortbow", 1, 1, 0.20f),
                new ItemDrop("travelers_cape", 1, 1, 0.25f),
            });
            RegisterTable("chest_armoury", new[]
            {
                new ItemDrop("iron_ore", 1, 3, 1f),
                new ItemDrop("iron_helmet", 1, 1, 0.45f),
                new ItemDrop("plate_greaves", 1, 1, 0.40f),
                new ItemDrop("iron_kite_shield", 1, 1, 0.35f),
                new ItemDrop("iron_longsword", 1, 1, 0.30f),
                new ItemDrop("plate_cuirass", 1, 1, 0.20f),
            });
        }
    }
}
