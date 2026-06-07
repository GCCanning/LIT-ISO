using System;
using System.Collections.Generic;
using UnityEngine;

namespace IsoCore.Foundation
{
    /// <summary>
    /// Runtime state for placed storage containers. UI can bind to this later, while
    /// save/load can already preserve chest contents by cell.
    /// </summary>
    public sealed class StorageSystem
    {
        readonly FoundationContent _content;
        readonly int _defaultSlotCount;
        readonly Dictionary<string, StorageContainer> _containers = new();

        public event Action<StorageContainer> ContainerChanged;
        public event Action<StorageContainer> ContainerOpened;

        public int ContainerCount => _containers.Count;
        public int DefaultSlotCount => _defaultSlotCount;

        public StorageSystem(FoundationContent content, int defaultSlotCount)
        {
            _content = content;
            _defaultSlotCount = Mathf.Max(1, defaultSlotCount);
        }

        public StorageContainer EnsureContainer(PlaceableDefinition placeable, int x, int y)
        {
            if (!IsContainer(placeable))
                return null;

            return EnsureContainer(placeable.id, x, y, _defaultSlotCount);
        }

        public StorageContainer EnsureContainer(string placeableId, int x, int y, int slotCount = 0)
        {
            var placeable = _content?.Placeables.Get(placeableId);
            if (!IsContainer(placeable))
                return null;

            string key = Key(x, y);
            if (_containers.TryGetValue(key, out var existing))
                return existing;

            var container = new StorageContainer(_content, placeable.id, x, y,
                slotCount > 0 ? slotCount : _defaultSlotCount);
            container.Changed += HandleContainerChanged;
            _containers.Add(key, container);
            return container;
        }

        public bool TryGetContainer(int x, int y, out StorageContainer container) =>
            _containers.TryGetValue(Key(x, y), out container);

        public bool TryOpenContainer(PlaceableInstance placeable, out StorageContainer container)
        {
            container = null;
            if (placeable == null)
                return false;

            container = EnsureContainer(placeable.Def, placeable.Wx, placeable.Wy);
            if (container == null)
                return false;

            ContainerOpened?.Invoke(container);
            return true;
        }

        public bool HasContents(int x, int y) =>
            TryGetContainer(x, y, out var container) && !container.IsEmpty;

        public bool RemoveContainer(int x, int y, bool requireEmpty = true)
        {
            string key = Key(x, y);
            if (!_containers.TryGetValue(key, out var container))
                return true;

            if (requireEmpty && !container.IsEmpty)
                return false;

            container.Changed -= HandleContainerChanged;
            _containers.Remove(key);
            return true;
        }

        public FoundationSavedStorageContainer[] CaptureState()
        {
            if (_containers.Count == 0)
                return Array.Empty<FoundationSavedStorageContainer>();

            var containers = new List<StorageContainer>(_containers.Values);
            containers.Sort((a, b) =>
            {
                int byX = a.X.CompareTo(b.X);
                return byX != 0 ? byX : a.Y.CompareTo(b.Y);
            });

            var saved = new FoundationSavedStorageContainer[containers.Count];
            for (int i = 0; i < containers.Count; i++)
            {
                var container = containers[i];
                saved[i] = new FoundationSavedStorageContainer
                {
                    placeableId = container.PlaceableId,
                    x = container.X,
                    y = container.Y,
                    slotCount = container.SlotCount,
                    slots = container.SnapshotSlots(),
                };
            }
            return saved;
        }

        public void RestoreState(FoundationSavedStorageContainer[] saved,
            Func<FoundationSavedStorageContainer, bool> canRestore = null)
        {
            if (saved == null)
                return;

            Clear();
            foreach (var entry in saved)
            {
                if (canRestore != null && !canRestore(entry))
                    continue;

                int slotCount = entry.slotCount > 0
                    ? entry.slotCount
                    : entry.slots != null && entry.slots.Length > 0
                        ? entry.slots.Length
                        : _defaultSlotCount;
                var container = EnsureContainer(entry.placeableId, entry.x, entry.y,
                    slotCount);
                container?.RestoreSlots(entry.slots);
            }
        }

        void Clear()
        {
            foreach (var container in _containers.Values)
                container.Changed -= HandleContainerChanged;
            _containers.Clear();
        }

        void HandleContainerChanged(StorageContainer container) => ContainerChanged?.Invoke(container);

        static bool IsContainer(PlaceableDefinition placeable) =>
            placeable != null && placeable.interaction == InteractionKind.Container;

        static string Key(int x, int y) => $"{x}:{y}";
    }

    public sealed class StorageContainer
    {
        readonly FoundationContent _content;
        readonly ItemStack[] _slots;

        public event Action<StorageContainer> Changed;

        public string PlaceableId { get; }
        public int X { get; }
        public int Y { get; }
        public int SlotCount => _slots.Length;

        public bool IsEmpty
        {
            get
            {
                for (int i = 0; i < _slots.Length; i++)
                    if (!_slots[i].IsEmpty)
                        return false;
                return true;
            }
        }

        public int UsedSlots
        {
            get
            {
                int used = 0;
                for (int i = 0; i < _slots.Length; i++)
                    if (!_slots[i].IsEmpty)
                        used++;
                return used;
            }
        }

        public StorageContainer(FoundationContent content, string placeableId, int x, int y, int slotCount)
        {
            _content = content;
            PlaceableId = placeableId;
            X = x;
            Y = y;
            _slots = new ItemStack[Mathf.Max(1, slotCount)];
        }

        public ItemStack GetSlot(int index) =>
            index >= 0 && index < _slots.Length ? _slots[index] : default;

        public ItemStack[] SnapshotSlots()
        {
            var copy = new ItemStack[_slots.Length];
            Array.Copy(_slots, copy, _slots.Length);
            return copy;
        }

        public void RestoreSlots(ItemStack[] slots)
        {
            for (int i = 0; i < _slots.Length; i++)
                _slots[i] = default;

            if (slots != null)
            {
                int count = Mathf.Min(_slots.Length, slots.Length);
                for (int i = 0; i < count; i++)
                {
                    var stack = slots[i];
                    if (stack.IsEmpty || !IsKnownItem(stack.itemId))
                        continue;

                    _slots[i] = new ItemStack(stack.itemId,
                        Mathf.Min(stack.count, MaxStack(stack.itemId)));
                }
            }

            Changed?.Invoke(this);
        }

        public int Add(string itemId, int count)
        {
            if (string.IsNullOrEmpty(itemId) || count <= 0)
                return count;
            if (!IsKnownItem(itemId))
                return count;

            int max = MaxStack(itemId);
            for (int i = 0; i < _slots.Length && count > 0; i++)
            {
                if (_slots[i].itemId == itemId && _slots[i].count < max)
                {
                    int add = Mathf.Min(max - _slots[i].count, count);
                    _slots[i].count += add;
                    count -= add;
                }
            }

            for (int i = 0; i < _slots.Length && count > 0; i++)
            {
                if (_slots[i].IsEmpty)
                {
                    int add = Mathf.Min(max, count);
                    _slots[i] = new ItemStack(itemId, add);
                    count -= add;
                }
            }

            Changed?.Invoke(this);
            return count;
        }

        public bool CanFit(string itemId, int count)
        {
            if (string.IsNullOrEmpty(itemId) || count <= 0)
                return true;
            if (!IsKnownItem(itemId))
                return false;

            int remaining = count;
            int max = MaxStack(itemId);
            for (int i = 0; i < _slots.Length && remaining > 0; i++)
            {
                if (_slots[i].IsEmpty)
                    remaining -= max;
                else if (_slots[i].itemId == itemId && _slots[i].count < max)
                    remaining -= max - _slots[i].count;
            }
            return remaining <= 0;
        }

        public int Count(string itemId)
        {
            int count = 0;
            for (int i = 0; i < _slots.Length; i++)
                if (_slots[i].itemId == itemId)
                    count += _slots[i].count;
            return count;
        }

        public bool Remove(string itemId, int count)
        {
            if (count <= 0)
                return true;
            if (Count(itemId) < count)
                return false;

            for (int i = 0; i < _slots.Length && count > 0; i++)
            {
                if (_slots[i].itemId != itemId)
                    continue;

                int take = Mathf.Min(_slots[i].count, count);
                _slots[i].count -= take;
                count -= take;
                if (_slots[i].count <= 0)
                    _slots[i] = default;
            }

            Changed?.Invoke(this);
            return true;
        }

        public ItemStack TakeFromSlot(int index, int count)
        {
            if (index < 0 || index >= _slots.Length || count <= 0 || _slots[index].IsEmpty)
                return default;

            var stack = _slots[index];
            int take = Mathf.Min(stack.count, count);
            _slots[index].count -= take;
            if (_slots[index].count <= 0)
                _slots[index] = default;

            Changed?.Invoke(this);
            return new ItemStack(stack.itemId, take);
        }

        int MaxStack(string itemId)
        {
            var def = _content?.Items.Get(itemId);
            return def != null ? Mathf.Max(1, def.maxStack) : 99;
        }

        bool IsKnownItem(string itemId) => _content?.Items.Get(itemId) != null;
    }
}
