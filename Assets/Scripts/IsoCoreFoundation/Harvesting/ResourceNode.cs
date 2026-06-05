using UnityEngine;

namespace IsoCore.Foundation
{
    /// <summary>A harvestable world instance (tree/rock/bush). Occupies its cell.</summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class ResourceNode : MonoBehaviour
    {
        public ResourceNodeDefinition Def { get; private set; }
        public int Wx { get; private set; }
        public int Wy { get; private set; }

        IsoWorld _world;
        int _remainingHits;

        public void Init(ResourceNodeDefinition def, IsoWorld world, int wx, int wy)
        {
            Def = def; _world = world; Wx = wx; Wy = wy;
            _remainingHits = Mathf.Max(1, def.hitsToHarvest);

            int height = world.GetHeight(wx, wy);
            transform.position = IsoGrid.CellToWorld(wx, wy, height);
            var sr = GetComponent<SpriteRenderer>();
            sr.sprite = PlaceholderArt.Box(def.color, def.widthUnits, def.heightUnits);
            sr.sortingOrder = IsoGrid.SortingOrder(wx, wy, height, IsoGrid.LayerProp);
        }

        /// <summary>True when this node needs a tool the player isn't holding.</summary>
        public bool RequiresMissingTool(ToolType tool) =>
            Def.toolMandatory && Def.requiredTool != ToolType.None && tool != Def.requiredTool;

        /// <summary>One harvest hit. Better tools (higher tier) deplete faster.
        /// Returns true when fully depleted (drops granted).</summary>
        public bool Harvest(Inventory inv, ToolType tool, int tier, out bool blockedFull)
        {
            blockedFull = false;
            if (RequiresMissingTool(tool)) return false;
            int power = (Def.requiredTool != ToolType.None && tool == Def.requiredTool)
                ? Mathf.Max(1, tier) : 1; // right tool tier is faster; hand/other = 1

            // Only the depleting hit grants drops — block it (without losing yield) if the
            // guaranteed drops can't fit. Considers partial stacks via Inventory.CanFit.
            if (_remainingHits - power <= 0 && !DropsCanFit(inv))
            {
                blockedFull = true;
                return false;
            }

            _remainingHits -= power;
            if (_remainingHits > 0) return false;

            HarvestSystem.RollDrops(Def.drops, inv);
            _world.ClearNode(Wx, Wy); // controller despawns this GO via OnCellChanged
            return true;
        }

        bool DropsCanFit(Inventory inv)
        {
            if (Def.drops == null) return true;
            int count = 0;
            foreach (var d in Def.drops)
                if (!string.IsNullOrEmpty(d.itemId) && d.chance >= 1f) count++;
            if (count == 0) return true;

            var guaranteed = new ItemStack[count];
            int i = 0;
            foreach (var d in Def.drops)
            {
                if (string.IsNullOrEmpty(d.itemId) || d.chance < 1f) continue; // bonus drops may be skipped
                guaranteed[i++] = new ItemStack(d.itemId, Mathf.Max(1, d.max));
            }
            return inv.CanFitAll(guaranteed);
        }
    }
}
