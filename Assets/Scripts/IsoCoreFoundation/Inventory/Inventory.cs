using System;
using UnityEngine;

namespace IsoCore.Foundation
{
    /// <summary>
    /// Slot-based, stack-aware inventory. The single inventory in the scene — passed
    /// explicitly to systems (fixes the legacy mixed .Instance/Find access bug).
    /// </summary>
    public class Inventory
    {
        readonly ItemStack[] _slots;
        readonly FoundationContent _content;

        public event Action OnChanged;
        public int SlotCount => _slots.Length;

        public Inventory(int slotCount, FoundationContent content)
        {
            _slots = new ItemStack[Mathf.Max(1, slotCount)];
            _content = content;
        }

        public ItemStack GetSlot(int i) => (i >= 0 && i < _slots.Length) ? _slots[i] : default;

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
                int n = Mathf.Min(_slots.Length, slots.Length);
                for (int i = 0; i < n; i++)
                {
                    var stack = slots[i];
                    if (stack.IsEmpty) continue;
                    if (_content.Items.Get(stack.itemId) == null) continue;
                    _slots[i] = new ItemStack(stack.itemId, Mathf.Min(stack.count, MaxStack(stack.itemId)));
                }
            }

            OnChanged?.Invoke();
        }

        int MaxStack(string itemId)
        {
            var def = _content.Items.Get(itemId);
            return def != null ? Mathf.Max(1, def.maxStack) : 99;
        }

        /// <summary>Adds count; returns leftover that did not fit.</summary>
        public int Add(string itemId, int count)
        {
            if (string.IsNullOrEmpty(itemId) || count <= 0) return count;
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
            OnChanged?.Invoke();
            return count;
        }

        public int Count(string itemId)
        {
            int n = 0;
            for (int i = 0; i < _slots.Length; i++)
                if (_slots[i].itemId == itemId) n += _slots[i].count;
            return n;
        }

        public bool Has(string itemId, int count) => Count(itemId) >= count;

        public bool HasEmptySlot()
        {
            for (int i = 0; i < _slots.Length; i++)
                if (_slots[i].IsEmpty) return true;
            return false;
        }

        /// <summary>Would adding count of itemId fit without overflow?</summary>
        public bool CanFit(string itemId, int count)
        {
            if (string.IsNullOrEmpty(itemId) || count <= 0) return true;
            int max = MaxStack(itemId);
            int remaining = count;
            for (int i = 0; i < _slots.Length && remaining > 0; i++)
            {
                if (_slots[i].IsEmpty) remaining -= max;
                else if (_slots[i].itemId == itemId && _slots[i].count < max) remaining -= (max - _slots[i].count);
            }
            return remaining <= 0;
        }

        /// <summary>Would adding every stack fit without overflow?</summary>
        public bool CanFitAll(ItemStack[] stacks)
        {
            if (stacks == null || stacks.Length == 0) return true;
            var scratch = new ItemStack[_slots.Length];
            Array.Copy(_slots, scratch, _slots.Length);

            foreach (var stack in stacks)
            {
                if (stack.IsEmpty) continue;
                int remaining = stack.count;
                int max = MaxStack(stack.itemId);

                for (int i = 0; i < scratch.Length && remaining > 0; i++)
                {
                    if (scratch[i].itemId == stack.itemId && scratch[i].count < max)
                    {
                        int add = Mathf.Min(max - scratch[i].count, remaining);
                        scratch[i].count += add;
                        remaining -= add;
                    }
                }
                for (int i = 0; i < scratch.Length && remaining > 0; i++)
                {
                    if (scratch[i].IsEmpty)
                    {
                        int add = Mathf.Min(max, remaining);
                        scratch[i] = new ItemStack(stack.itemId, add);
                        remaining -= add;
                    }
                }

                if (remaining > 0) return false;
            }

            return true;
        }

        public bool Remove(string itemId, int count)
        {
            if (count <= 0) return true;
            if (Count(itemId) < count) return false;
            for (int i = 0; i < _slots.Length && count > 0; i++)
            {
                if (_slots[i].itemId == itemId)
                {
                    int take = Mathf.Min(_slots[i].count, count);
                    _slots[i].count -= take;
                    count -= take;
                    if (_slots[i].count <= 0) _slots[i] = default;
                }
            }
            OnChanged?.Invoke();
            return true;
        }
    }
}
