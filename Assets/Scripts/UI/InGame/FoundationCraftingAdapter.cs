using System;
using System.Collections.Generic;
using IsoCore.Foundation;
using FoundationRecipeDefinition = IsoCore.Foundation.RecipeDefinition;
using UnityEngine;

namespace LitIso.UI.InGame
{
    /// <summary>
    /// Wires CraftingView to Foundation's CraftingSystem + Inventory.
    /// Shows every recipe defined in FoundationContent, grouped by station then
    /// sorted by name; highlights craftable ones.
    /// Subscribes to Inventory.OnChanged so the ingredient counts update live.
    /// </summary>
    public sealed class FoundationCraftingAdapter : ICraftingViewModel, IDisposable
    {
        /// <summary>Safety cap for batch crafting / max-craftable estimates.</summary>
        const int MaxBatch = 999;

        readonly IsoCore.Foundation.CraftingSystem _crafting;
        readonly Inventory _inv;
        readonly FoundationContent _content;
        readonly List<FoundationRecipeDefinition> _ordered = new List<FoundationRecipeDefinition>();
        readonly List<FoundationRecipeDefinition> _visible = new List<FoundationRecipeDefinition>();
        StationType? _filterStation;

        public event Action Changed;

        public FoundationCraftingAdapter(IsoCore.Foundation.CraftingSystem crafting, Inventory inv, FoundationContent content)
        {
            _crafting = crafting;
            _inv      = inv;
            _content  = content;
            BuildOrder();
            if (_inv != null) _inv.OnChanged += OnChanged;
        }

        /// <summary>
        /// Narrow the recipe list to <paramref name="station"/> (plus Hand/anywhere recipes),
        /// or null to show everything. Called when a specific prop (furnace, tannery, ...) is
        /// opened so its own recipes are front and centre.
        /// </summary>
        public void SetStationFilter(StationType? station)
        {
            // Always rebuild (so the first open is never empty), but only raise Changed when
            // the filter actually changed. DrawCrafting() calls this on every Refresh; raising
            // Changed unconditionally re-entered Refresh -> DrawCrafting -> here forever
            // (uncatchable StackOverflowException). Refresh also has a re-entrancy guard.
            bool changed = _filterStation != station;
            _filterStation = station;
            BuildVisible();
            if (changed) Changed?.Invoke();
        }

        /// <summary>Stable display order: station group (Hand first), then display name.</summary>
        void BuildOrder()
        {
            _ordered.Clear();
            if (_content?.Recipes == null) return;
            for (int i = 0; i < _content.Recipes.Count; i++)
                _ordered.Add(_content.Recipes[i]);
            _ordered.Sort(CompareRecipes);
            BuildVisible();
        }

        void BuildVisible()
        {
            _visible.Clear();
            for (int i = 0; i < _ordered.Count; i++)
            {
                var r = _ordered[i];
                if (_filterStation.HasValue)
                {
                    var st = r.station;
                    bool anywhere = st == StationType.None || st == StationType.Hand;
                    if (!anywhere && st != _filterStation.Value) continue;

                    // Tier gate: when a tagged world station (with a tier) is driving the
                    // list, hide recipes that need a higher station tier than this one.
                    // anywhere recipes are never tier-gated. ActiveStationTier is 0 for
                    // placeable stations, so this is a no-op until higher-tier world
                    // stations + minStationTier>0 recipes are authored.
                    // TODO(tiers): also surface higher/better OUTPUTS for higher tiers
                    // (e.g. tier-scaled output amounts) once that content exists.
                    if (!anywhere && r.minStationTier > CraftingStationContext.ActiveStationTier)
                        continue;
                }
                _visible.Add(r);
            }
        }

        static int CompareRecipes(FoundationRecipeDefinition a, FoundationRecipeDefinition b)
        {
            int sa = StationOrder(a), sb = StationOrder(b);
            if (sa != sb) return sa.CompareTo(sb);
            return string.Compare(a.displayName ?? a.id, b.displayName ?? b.id, StringComparison.OrdinalIgnoreCase);
        }

        static int StationOrder(FoundationRecipeDefinition r)
        {
            // None and Hand share the "craft anywhere" group.
            var st = r != null ? r.station : StationType.Hand;
            return st == StationType.None ? (int)StationType.Hand : (int)st;
        }

        public void Dispose()
        {
            if (_inv != null) _inv.OnChanged -= OnChanged;
        }

        void OnChanged() => Changed?.Invoke();

        public int RecipeCount => _visible.Count;

        public CraftingRecipeRow GetRecipe(int i)
        {
            if (i < 0 || i >= _visible.Count) return default;
            var r = _visible[i];
            string disabledReason = DisabledReason(r);
            return new CraftingRecipeRow
            {
                id             = r.id,
                display        = r.displayName ?? r.id,
                icon           = ItemIconResolver.Resolve(r.id),
                canCraft       = string.IsNullOrEmpty(disabledReason),
                station        = StationLabel(r.station),
                disabledReason = disabledReason,
            };
        }

        public CraftingRecipeDetails GetDetails(string recipeId)
        {
            if (_content?.Recipes == null) return default;
            var r = _content.Recipes.Get(recipeId);
            if (r == null) return default;

            var inputs  = BuildIngredients(r.inputs);
            var outputs = BuildOutputs(r.outputs);

            string fuelInfo = "";
            if (!string.IsNullOrEmpty(r.fuelItemId))
            {
                var fuelDef = _content?.Items?.Get(r.fuelItemId);
                int have = _inv?.Count(r.fuelItemId) ?? 0;
                fuelInfo = $"Fuel: {fuelDef?.displayName ?? r.fuelItemId} {have}/{r.fuelCount}";
            }

            var job = _crafting?.ActiveJob;
            bool jobActive = job != null && job.Recipe == r;

            return new CraftingRecipeDetails
            {
                id       = r.id,
                display  = r.displayName ?? r.id,
                icon     = ItemIconResolver.Resolve(r.id),
                inputs   = inputs,
                outputs  = outputs,
                canCraft = _crafting != null && _crafting.CanCraft(r),
                disabledReason = DisabledReason(r),
                maxCraftable   = MaxCraftable(r),
                craftTimeSeconds = r.craftTimeSeconds,
                fuelInfo = fuelInfo,
                jobActive = jobActive,
                jobProgress01 = jobActive ? job.Progress01 : 0f,
            };
        }

        public void Craft(string recipeId)
        {
            if (_content?.Recipes == null || _crafting == null) return;
            var r = _content.Recipes.Get(recipeId);
            if (r != null) _crafting.TryCraft(r);
        }

        public void Craft(string recipeId, int count)
        {
            if (_content?.Recipes == null || _crafting == null) return;
            var r = _content.Recipes.Get(recipeId);
            if (r == null) return;
            // TryCraft re-validates ingredients + output space each pass, so this
            // stops safely the moment a batch no longer fits.
            int n = Mathf.Clamp(count, 0, MaxBatch);
            for (int i = 0; i < n; i++)
                if (!_crafting.TryCraft(r)) break;
        }

        // ---- helpers --------------------------------------------------------

        CraftingIngredient[] BuildIngredients(RecipeIngredient[] inputs)
        {
            if (inputs == null) return Array.Empty<CraftingIngredient>();
            var result = new CraftingIngredient[inputs.Length];
            for (int i = 0; i < inputs.Length; i++)
            {
                var ing  = inputs[i];
                var def  = _content?.Items?.Get(ing.itemId);
                int have = _inv?.Count(ing.itemId) ?? 0;
                result[i] = new CraftingIngredient
                {
                    itemId  = ing.itemId,
                    display = def?.displayName ?? ing.itemId,
                    icon    = ItemIconResolver.Resolve(ing.itemId),
                    needed  = ing.count,
                    have    = have,
                };
            }
            return result;
        }

        CraftingIngredient[] BuildOutputs(ItemStack[] outputs)
        {
            if (outputs == null) return Array.Empty<CraftingIngredient>();
            var result = new CraftingIngredient[outputs.Length];
            for (int i = 0; i < outputs.Length; i++)
            {
                var o   = outputs[i];
                var def = _content?.Items?.Get(o.itemId);
                result[i] = new CraftingIngredient
                {
                    itemId  = o.itemId,
                    display = def?.displayName ?? o.itemId,
                    icon    = ItemIconResolver.Resolve(o.itemId),
                    needed  = o.count,
                    have    = 0,
                };
            }
            return result;
        }

        /// <summary>How many consecutive crafts the current ingredient counts allow (0 if blocked).</summary>
        int MaxCraftable(FoundationRecipeDefinition recipe)
        {
            if (recipe == null || _crafting == null || _inv == null) return 0;
            if (!_crafting.CanCraft(recipe)) return 0;

            int max = MaxBatch;
            if (recipe.inputs != null)
            {
                for (int i = 0; i < recipe.inputs.Length; i++)
                {
                    var input = recipe.inputs[i];
                    if (input.count <= 0) continue;
                    max = Math.Min(max, _inv.Count(input.itemId) / input.count);
                }
            }
            // CanCraft passed, so at least one craft fits even if inputs is empty.
            return Math.Max(1, max);
        }

        string DisabledReason(FoundationRecipeDefinition recipe)
        {
            if (recipe == null) return "Recipe unavailable";
            if (_crafting == null || _inv == null) return "Crafting unavailable";

            if (recipe.station != StationType.None && recipe.station != StationType.Hand)
            {
                bool stationOk = _crafting.StationAvailable != null && _crafting.StationAvailable(recipe.station);
                if (!stationOk)
                    return $"Requires {recipe.station}";
            }

            var job = _crafting.ActiveJob;
            if (recipe.craftTimeSeconds > 0f && job != null && job.Recipe != recipe)
                return $"Busy: crafting {job.Recipe.displayName ?? job.Recipe.id}";

            if (!string.IsNullOrEmpty(recipe.fuelItemId))
            {
                int haveFuel = _inv.Count(recipe.fuelItemId);
                if (haveFuel < recipe.fuelCount)
                {
                    var fuelDef = _content?.Items?.Get(recipe.fuelItemId);
                    string fuelName = fuelDef?.displayName ?? recipe.fuelItemId;
                    return $"Need {fuelName} x{recipe.fuelCount - haveFuel} (fuel)";
                }
            }

            if (recipe.inputs != null)
            {
                // List every short ingredient (with how many are missing), not just the first.
                System.Text.StringBuilder missing = null;
                for (int i = 0; i < recipe.inputs.Length; i++)
                {
                    var input = recipe.inputs[i];
                    int have = _inv.Count(input.itemId);
                    if (have >= input.count) continue;
                    var def = _content?.Items?.Get(input.itemId);
                    string display = def?.displayName ?? input.itemId;
                    missing ??= new System.Text.StringBuilder("Need ");
                    if (missing.Length > 5) missing.Append(", ");
                    missing.Append(display).Append(" x").Append(input.count - have);
                }
                if (missing != null)
                    return missing.ToString();
            }

            if (!_inv.CanExchange(recipe.inputs, recipe.outputs))
                return "Inventory full";

            return "";
        }

        static string StationLabel(StationType station)
        {
            switch (station)
            {
                case StationType.None:
                case StationType.Hand:
                    return "Hand";
                default:
                    return station.ToString();
            }
        }
    }
}
