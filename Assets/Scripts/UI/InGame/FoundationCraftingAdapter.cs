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

        public event Action Changed;

        public FoundationCraftingAdapter(IsoCore.Foundation.CraftingSystem crafting, Inventory inv, FoundationContent content)
        {
            _crafting = crafting;
            _inv      = inv;
            _content  = content;
            BuildOrder();
            if (_inv != null) _inv.OnChanged += OnChanged;
        }

        /// <summary>Stable display order: station group (Hand first), then display name.</summary>
        void BuildOrder()
        {
            _ordered.Clear();
            if (_content?.Recipes == null) return;
            for (int i = 0; i < _content.Recipes.Count; i++)
                _ordered.Add(_content.Recipes[i]);
            _ordered.Sort(CompareRecipes);
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

        public int RecipeCount => _ordered.Count;

        public CraftingRecipeRow GetRecipe(int i)
        {
            if (i < 0 || i >= _ordered.Count) return default;
            var r = _ordered[i];
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
