using System;
using UnityEngine;

namespace IsoCore.Foundation
{
    /// <summary>
    /// Index-addressable selection over a visible row of inventory slots.
    /// The visible hotbar stays compact (usually 1-9), while ActiveRow pages it
    /// across the full backpack.
    /// </summary>
    public class Hotbar
    {
        public readonly Inventory Inventory;
        public readonly int Size;
        public int Selected { get; private set; }
        public int ActiveRow { get; private set; }

        public int RowCount => Mathf.Max(1, Mathf.CeilToInt(Inventory.SlotCount / (float)Size));
        public int SelectedInventoryIndex => Mathf.Clamp(VisibleToInventoryIndex(Selected), 0, Inventory.SlotCount - 1);

        public event Action OnSelectionChanged;

        public Hotbar(Inventory inventory, int size)
        {
            Inventory = inventory;
            Size = Mathf.Clamp(size, 1, inventory.SlotCount);
        }

        public ItemStack SelectedStack => Inventory.GetSlot(SelectedInventoryIndex);

        public int VisibleToInventoryIndex(int visibleIndex)
        {
            visibleIndex = Mathf.Clamp(visibleIndex, 0, Size - 1);
            return ActiveRow * Size + visibleIndex;
        }

        public ItemStack GetVisibleStack(int visibleIndex)
        {
            int index = VisibleToInventoryIndex(visibleIndex);
            return index >= 0 && index < Inventory.SlotCount ? Inventory.GetSlot(index) : default;
        }

        public void Select(int index)
        {
            index = Mathf.Clamp(index, 0, Size - 1);
            int first = ActiveRow * Size;
            int available = Mathf.Max(1, Inventory.SlotCount - first);
            index = Mathf.Clamp(index, 0, Mathf.Min(Size, available) - 1);
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

        public void SelectRow(int row)
        {
            row = Mathf.Clamp(row, 0, RowCount - 1);
            if (row == ActiveRow) return;
            ActiveRow = row;
            ClampSelectedToActiveRow();
            OnSelectionChanged?.Invoke();
        }

        public void SetSelection(int selectedVisibleIndex, int activeRow)
        {
            activeRow = Mathf.Clamp(activeRow, 0, RowCount - 1);
            selectedVisibleIndex = Mathf.Clamp(selectedVisibleIndex, 0, Size - 1);

            bool changed = selectedVisibleIndex != Selected || activeRow != ActiveRow;
            Selected = selectedVisibleIndex;
            ActiveRow = activeRow;
            ClampSelectedToActiveRow();
            if (changed) OnSelectionChanged?.Invoke();
        }

        public void StepRow(int dir)
        {
            int rows = RowCount;
            if (rows <= 1) return;
            int next = (ActiveRow + dir) % rows;
            if (next < 0) next += rows;
            SelectRow(next);
        }

        void ClampSelectedToActiveRow()
        {
            int first = ActiveRow * Size;
            int available = Mathf.Max(1, Inventory.SlotCount - first);
            Selected = Mathf.Clamp(Selected, 0, Mathf.Min(Size, available) - 1);
        }
    }
}
