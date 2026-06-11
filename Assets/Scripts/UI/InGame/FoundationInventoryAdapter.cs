using System;
using System.Collections.Generic;
using IsoCore.Foundation;
using UnityEngine;

namespace LitIso.UI.InGame
{
    /// <summary>
    /// Maps Codex's <see cref="Inventory"/> + <see cref="FoundationContent"/> onto the
    /// inventory panel's view model. Mirrors the slot-resolution rules used by the
    /// HUD adapter (display name from ItemDefinition.Display, icon via
    /// <see cref="ItemIconResolver"/>), but exposes the FULL inventory capacity rather
    /// than just the hotbar window.
    /// </summary>
    public sealed class FoundationInventoryAdapter : IInventoryViewModel, IDisposable
    {
        readonly Inventory _inv;
        readonly FoundationContent _content;

        public event Action Changed;

        public FoundationInventoryAdapter(Inventory inv, FoundationContent content)
        {
            _inv = inv;
            _content = content;
            if (_inv != null) _inv.OnChanged += OnChanged;
        }

        public void Dispose()
        {
            if (_inv != null) _inv.OnChanged -= OnChanged;
        }

        void OnChanged() => Changed?.Invoke();

        public int Capacity => _inv?.SlotCount ?? 0;

        public HudSlot GetSlot(int i)
        {
            if (_inv == null) return default;
            var st = _inv.GetSlot(i);
            bool empty = string.IsNullOrEmpty(st.itemId);
            string label = empty ? "" : st.itemId;
            if (!empty && _content != null)
            {
                var def = _content.Items.Get(st.itemId);
                if (def != null && !string.IsNullOrEmpty(def.Display)) label = def.Display;
            }
            return new HudSlot
            {
                label    = label,
                count    = empty ? 0 : st.count,
                icon     = empty ? null : ItemIconResolver.Resolve(st.itemId),
                selected = false,
            };
        }

        // --------------------------------------------------------------------
        // Smart-inventory ops (2026-06-11). Foundation's Inventory exposes no
        // first-class slot ops yet; move/swap/split/sort are composed from the
        // only slot-mutation surface it does expose (SnapshotSlots/RestoreSlots).
        // That works but is heavy (full-array rewrite + global OnChanged) and
        // RestoreSlots is really a save-load API — first-class ops are requested
        // in Docs/agent-comms/from-claude.md ("Smart inventory ops" handoff).
        // --------------------------------------------------------------------

        int MaxStack(string itemId)
        {
            var def = _content != null ? _content.Items.Get(itemId) : null;
            return def != null ? Mathf.Max(1, def.maxStack) : 99;
        }

        bool ItemHasDurability(string itemId)
        {
            var def = _content != null ? _content.Items.Get(itemId) : null;
            return def != null && def.HasDurability;
        }

        static bool Valid(ItemStack[] slots, int i) => i >= 0 && i < slots.Length;

        public bool MoveSlot(int from, int to)
        {
            if (_inv == null) return false;
            var slots = _inv.SnapshotSlots();
            if (!Valid(slots, from) || !Valid(slots, to) || from == to || slots[from].IsEmpty) return false;

            if (slots[to].IsEmpty)
            {
                slots[to] = slots[from];
                slots[from] = default;
            }
            else if (slots[to].itemId == slots[from].itemId && !ItemHasDurability(slots[to].itemId))
            {
                int max = MaxStack(slots[to].itemId);
                int moved = Mathf.Min(max - slots[to].count, slots[from].count);
                if (moved <= 0) return false;
                slots[to].count += moved;
                slots[from].count -= moved;
                if (slots[from].count <= 0) slots[from] = default;
            }
            else
            {
                return false;
            }

            _inv.RestoreSlots(slots);
            return true;
        }

        public bool SwapSlots(int a, int b)
        {
            if (_inv == null) return false;
            var slots = _inv.SnapshotSlots();
            if (!Valid(slots, a) || !Valid(slots, b) || a == b) return false;
            if (slots[a].IsEmpty && slots[b].IsEmpty) return false;

            var tmp = slots[a];
            slots[a] = slots[b];
            slots[b] = tmp;
            _inv.RestoreSlots(slots);
            return true;
        }

        public bool SplitStack(int slot, int count)
        {
            if (_inv == null) return false;
            var slots = _inv.SnapshotSlots();
            if (!Valid(slots, slot) || slots[slot].IsEmpty) return false;
            if (count <= 0 || count >= slots[slot].count) return false;

            int empty = -1;
            for (int i = 0; i < slots.Length; i++)
                if (slots[i].IsEmpty) { empty = i; break; }
            if (empty < 0) return false;

            slots[empty] = new ItemStack(slots[slot].itemId, count, slots[slot].durability);
            slots[slot].count -= count;
            _inv.RestoreSlots(slots);
            return true;
        }

        public bool DropItem(int slot, int count)
        {
            // TODO(codex): needs Foundation op — drop-to-world. Required shape:
            // remove `count` from `slot` AND spawn a ground pickup at the player.
            // Inventory only exposes Remove(itemId, count), which pulls from
            // arbitrary slots and silently destroys the items, so we no-op
            // rather than vaporise the player's stack.
            return false;
        }

        public void SortInventory()
        {
            if (_inv == null) return;
            var slots = _inv.SnapshotSlots();

            // 1) merge partial stacks (durability items keep their own stack so
            //    a damaged tool never blends into a fresh one), 2) sort by
            //    category → name, 3) re-deal respecting maxStack.
            var stacks = new List<ItemStack>();
            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i].IsEmpty) { slots[i] = default; continue; }
                var s = slots[i];
                slots[i] = default;
                if (!ItemHasDurability(s.itemId))
                {
                    bool merged = false;
                    for (int t = 0; t < stacks.Count; t++)
                    {
                        if (stacks[t].itemId != s.itemId) continue;
                        var e = stacks[t];
                        e.count += s.count;
                        stacks[t] = e;
                        merged = true;
                        break;
                    }
                    if (merged) continue;
                }
                stacks.Add(s);
            }

            stacks.Sort(CompareStacks);

            int idx = 0;
            for (int t = 0; t < stacks.Count && idx < slots.Length; t++)
            {
                int max = MaxStack(stacks[t].itemId);
                int remaining = stacks[t].count;
                while (remaining > 0 && idx < slots.Length)
                {
                    int put = Mathf.Min(max, remaining);
                    slots[idx++] = new ItemStack(stacks[t].itemId, put, stacks[t].durability);
                    remaining -= put;
                }
            }

            _inv.RestoreSlots(slots);
        }

        int CompareStacks(ItemStack a, ItemStack b)
        {
            var da = _content != null ? _content.Items.Get(a.itemId) : null;
            var db = _content != null ? _content.Items.Get(b.itemId) : null;
            int ca = da != null ? (int)da.category : int.MaxValue;
            int cb = db != null ? (int)db.category : int.MaxValue;
            if (ca != cb) return ca.CompareTo(cb);
            // TODO(codex): needs Foundation op — ItemDefinition has no rarity
            // field; once one lands, order by rarity (desc) between category and name.
            string na = da != null ? da.Display : a.itemId;
            string nb = db != null ? db.Display : b.itemId;
            int byName = string.Compare(na, nb, StringComparison.OrdinalIgnoreCase);
            return byName != 0 ? byName : string.Compare(a.itemId, b.itemId, StringComparison.Ordinal);
        }
    }
}
