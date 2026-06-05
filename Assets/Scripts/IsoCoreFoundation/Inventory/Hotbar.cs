using System;
using UnityEngine;

namespace IsoCore.Foundation
{
    /// <summary>Index-addressable selection over the first N inventory slots.</summary>
    public class Hotbar
    {
        public readonly Inventory Inventory;
        public readonly int Size;
        public int Selected { get; private set; }

        public event Action OnSelectionChanged;

        public Hotbar(Inventory inventory, int size)
        {
            Inventory = inventory;
            Size = Mathf.Clamp(size, 1, inventory.SlotCount);
        }

        public ItemStack SelectedStack => Inventory.GetSlot(Selected);

        public void Select(int index)
        {
            index = Mathf.Clamp(index, 0, Size - 1);
            if (index == Selected) return;
            Selected = index;
            OnSelectionChanged?.Invoke();
        }

        public void Step(int dir)
        {
            int next = (Selected + dir) % Size;
            if (next < 0) next += Size;
            Select(next);
        }
    }
}
