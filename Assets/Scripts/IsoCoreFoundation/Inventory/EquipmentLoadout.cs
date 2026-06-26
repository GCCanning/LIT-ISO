using System;
using System.Collections.Generic;
using UnityEngine;

namespace IsoCore.Foundation
{
    /// <summary>
    /// Phase 1 equipment model. Holds one equipped ItemStack per <see cref="EquipSlot"/>,
    /// sums every equipped item's <see cref="FoundationStatBonus"/> set into the player's
    /// <see cref="FoundationPlayerStats"/> via <see cref="FoundationPlayerStats.ApplyEquipmentModifiers"/>,
    /// and raises <see cref="EquipmentChanged"/> so the (Assembly-CSharp) visual layer can
    /// re-bake the LPC wardrobe. This type is Foundation-only and never references the
    /// wardrobe directly — the visual bridge subscribes to the event.
    /// </summary>
    public sealed class EquipmentLoadout
    {
        readonly FoundationContent _content;
        readonly FoundationPlayerStats _stats;
        readonly Dictionary<EquipSlot, ItemStack> _slots = new();

        /// <summary>
        /// Raised after any equip/unequip. Args: (slot, equipped-or-empty stack, item def).
        /// On equip, def is the now-equipped item. On unequip, the stack is empty but def is the
        /// item that was just removed (so the visual layer can unequip its lpcCatalogId).
        /// </summary>
        public event Action<EquipSlot, ItemStack, ItemDefinition> EquipmentChanged;

        public EquipmentLoadout(FoundationContent content, FoundationPlayerStats stats)
        {
            _content = content;
            _stats = stats;
        }

        public ItemStack GetEquipped(EquipSlot slot) =>
            _slots.TryGetValue(slot, out var s) ? s : default;

        public bool IsEquipped(EquipSlot slot) =>
            _slots.TryGetValue(slot, out var s) && !s.IsEmpty;

        public ItemDefinition GetDefinition(EquipSlot slot)
        {
            var s = GetEquipped(slot);
            return s.IsEmpty ? null : _content?.Items.Get(s.itemId);
        }

        /// <summary>
        /// Equips an item by id into its definition's slot. Returns the previously-equipped
        /// stack (or default) so the caller can return it to the inventory. No-op for
        /// non-equippable items.
        /// </summary>
        public ItemStack Equip(string itemId, int durability = 0)
        {
            var def = _content?.Items.Get(itemId);
            if (def == null || !def.IsEquippable)
                return default;

            var previous = GetEquipped(def.equipSlot);
            _slots[def.equipSlot] = new ItemStack(itemId, 1, durability);

            RecomputeStats();
            EquipmentChanged?.Invoke(def.equipSlot, _slots[def.equipSlot], def);
            return previous;
        }

        /// <summary>Unequips whatever occupies <paramref name="slot"/>. Returns the removed stack.</summary>
        public ItemStack Unequip(EquipSlot slot)
        {
            if (slot == EquipSlot.None || !_slots.TryGetValue(slot, out var removed) || removed.IsEmpty)
                return default;

            var removedDef = _content?.Items.Get(removed.itemId);
            _slots.Remove(slot);
            RecomputeStats();
            EquipmentChanged?.Invoke(slot, default, removedDef);
            return removed;
        }

        /// <summary>Sums every equipped item's stat bonuses and pushes them into the player stats.</summary>
        void RecomputeStats()
        {
            int str = 0, dex = 0, intel = 0, vit = 0, def = 0, luck = 0;
            foreach (var kv in _slots)
            {
                if (kv.Value.IsEmpty) continue;
                var item = _content?.Items.Get(kv.Value.itemId);
                if (item?.statBonuses == null) continue;
                foreach (var b in item.statBonuses)
                {
                    switch (b.stat)
                    {
                        case FoundationStatType.STR: str += b.amount; break;
                        case FoundationStatType.DEX: dex += b.amount; break;
                        case FoundationStatType.INT: intel += b.amount; break;
                        case FoundationStatType.VIT: vit += b.amount; break;
                        case FoundationStatType.DEF: def += b.amount; break;
                        case FoundationStatType.LUCK: luck += b.amount; break;
                    }
                }
            }
            _stats?.ApplyEquipmentModifiers(str, dex, intel, vit, def, luck);
        }

        public EquipmentSaveData CaptureState()
        {
            var entries = new List<EquipmentSaveEntry>(_slots.Count);
            foreach (var kv in _slots)
            {
                if (kv.Value.IsEmpty) continue;
                entries.Add(new EquipmentSaveEntry
                {
                    slot = kv.Key,
                    itemId = kv.Value.itemId,
                    durability = kv.Value.durability,
                });
            }
            return new EquipmentSaveData { entries = entries.ToArray() };
        }

        public void RestoreState(EquipmentSaveData data)
        {
            _slots.Clear();
            if (data?.entries != null)
            {
                foreach (var e in data.entries)
                {
                    var def = _content?.Items.Get(e.itemId);
                    if (def == null || !def.IsEquippable) continue;
                    _slots[def.equipSlot] = new ItemStack(e.itemId, 1, e.durability);
                }
            }
            RecomputeStats();
            // Re-raise per equipped slot so the visual layer re-bakes the restored gear.
            foreach (var kv in _slots)
            {
                var def = _content?.Items.Get(kv.Value.itemId);
                EquipmentChanged?.Invoke(kv.Key, kv.Value, def);
            }
        }
    }

    [Serializable]
    public struct EquipmentSaveEntry
    {
        public EquipSlot slot;
        public string itemId;
        public int durability;
    }

    [Serializable]
    public class EquipmentSaveData
    {
        public EquipmentSaveEntry[] entries;
    }
}
