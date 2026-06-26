using System;
using UnityEngine;

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

        /// <summary>An in-progress timed craft (e.g. furnace smelting). One at a time.</summary>
        public class CraftJob
        {
            public RecipeDefinition Recipe;
            public float Elapsed;
            public float Duration;
            public float Progress01 => Duration > 0f ? Mathf.Clamp01(Elapsed / Duration) : 1f;
        }

        public CraftJob ActiveJob { get; private set; }

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

        bool HasFuel(RecipeDefinition r)
        {
            if (string.IsNullOrEmpty(r.fuelItemId)) return true;
            return _inv.Count(r.fuelItemId) >= Mathf.Max(1, r.fuelCount);
        }

        public bool CanCraft(RecipeDefinition r)
        {
            if (r == null || r.inputs == null) return false;
            if (!StationOk(r.station)) return false;
            // Timed recipes occupy the station's single job slot until they finish.
            if (r.craftTimeSeconds > 0f && ActiveJob != null) return false;
            if (!HasFuel(r)) return false;
            return _inv.CanExchange(r.inputs, r.outputs);
        }

        public bool TryCraft(RecipeDefinition r)
        {
            if (!CanCraft(r)) return false;

            foreach (var i in r.inputs) _inv.Remove(i.itemId, i.count);
            if (!string.IsNullOrEmpty(r.fuelItemId))
                _inv.Remove(r.fuelItemId, Mathf.Max(1, r.fuelCount));

            if (r.craftTimeSeconds > 0f)
            {
                // Outputs are deferred until Tick() completes the job.
                ActiveJob = new CraftJob { Recipe = r, Elapsed = 0f, Duration = r.craftTimeSeconds };
                SfxManager.Play("craft");
                return true;
            }

            if (r.outputs != null)
                foreach (var o in r.outputs) _inv.Add(o.itemId, o.count);
            SfxManager.Play("craft");
            Crafted?.Invoke(r);
            return true;
        }

        /// <summary>Advances the active timed craft, if any. Call once per frame.</summary>
        public void Tick(float dt)
        {
            if (ActiveJob == null) return;
            ActiveJob.Elapsed += dt;
            if (ActiveJob.Elapsed < ActiveJob.Duration) return;

            var r = ActiveJob.Recipe;
            if (r.outputs != null)
                foreach (var o in r.outputs) _inv.Add(o.itemId, o.count);
            ActiveJob = null;
            SfxManager.Play("craft");
            Crafted?.Invoke(r);
        }
    }
}
