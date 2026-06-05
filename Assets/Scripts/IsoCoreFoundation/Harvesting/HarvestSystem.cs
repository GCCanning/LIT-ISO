using UnityEngine;

namespace IsoCore.Foundation
{
    /// <summary>Shared weighted-drop roller used by resource nodes and mobs.</summary>
    public static class HarvestSystem
    {
        public static void RollDrops(ItemDrop[] drops, Inventory inv)
        {
            if (drops == null || inv == null) return;
            foreach (var d in drops)
            {
                if (string.IsNullOrEmpty(d.itemId)) continue;
                if (Random.value <= d.chance)
                {
                    int amount = Random.Range(d.min, d.max + 1);
                    if (amount > 0) inv.Add(d.itemId, amount);
                }
            }
        }
    }
}
