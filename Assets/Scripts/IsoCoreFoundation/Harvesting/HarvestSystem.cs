using System.Collections.Generic;
using UnityEngine;

namespace IsoCore.Foundation
{
    /// <summary>Shared weighted-drop roller used by resource nodes and mobs.</summary>
    public static class HarvestSystem
    {
        public static void RollDrops(ItemDrop[] drops, Inventory inv) => RollDrops(drops, inv, null);

        /// <summary>
        /// Rolls weighted drops into the inventory. If <paramref name="granted"/> is provided,
        /// it is filled with what was actually added (itemId + amount) so callers can show
        /// pickup feedback ("+2 Wood").
        /// </summary>
        public static void RollDrops(ItemDrop[] drops, Inventory inv, List<ItemStack> granted)
        {
            if (drops == null || inv == null) return;
            foreach (var d in drops)
            {
                if (string.IsNullOrEmpty(d.itemId)) continue;
                if (Random.value <= d.chance)
                {
                    int amount = Random.Range(d.min, d.max + 1);
                    if (amount > 0)
                    {
                        inv.Add(d.itemId, amount);
                        granted?.Add(new ItemStack(d.itemId, amount));
                    }
                }
            }
        }
    }
}
