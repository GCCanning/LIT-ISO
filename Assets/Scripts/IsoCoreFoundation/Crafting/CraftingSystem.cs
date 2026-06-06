using System;

namespace IsoCore.Foundation
{
    /// <summary>Consume-inputs -&gt; produce-outputs crafting, gated by station proximity.</summary>
    public class CraftingSystem
    {
        readonly FoundationContent _content;
        readonly Inventory _inv;

        /// <summary>Set by the bootstrap: is a station of this type within reach?</summary>
        public Func<StationType, bool> StationAvailable;
        public event Action<RecipeDefinition> Crafted;

        public CraftingSystem(FoundationContent content, Inventory inv)
        {
            _content = content; _inv = inv;
        }

        public FoundationContent Content => _content;

        bool StationOk(StationType st)
        {
            if (st == StationType.None || st == StationType.Hand) return true;
            return StationAvailable != null && StationAvailable(st);
        }

        public bool CanCraft(RecipeDefinition r)
        {
            if (r == null || r.inputs == null) return false;
            if (!StationOk(r.station)) return false;
            foreach (var i in r.inputs)
                if (!_inv.Has(i.itemId, i.count)) return false;
            // Don't consume inputs if the outputs would overflow the inventory.
            if (r.outputs != null)
                foreach (var o in r.outputs)
                    if (!_inv.CanFit(o.itemId, o.count)) return false;
            return true;
        }

        public bool TryCraft(RecipeDefinition r)
        {
            if (!CanCraft(r)) return false;
            foreach (var i in r.inputs) _inv.Remove(i.itemId, i.count);
            if (r.outputs != null)
                foreach (var o in r.outputs) _inv.Add(o.itemId, o.count);
            SfxManager.Play("craft");
            Crafted?.Invoke(r);
            return true;
        }
    }
}
