using System;
using IsoCore.Foundation;
using FoundationRecipeDefinition = IsoCore.Foundation.RecipeDefinition;
using UnityEngine;

namespace LitIso.UI.InGame
{
    /// <summary>
    /// Wires CraftingView to Foundation's CraftingSystem + Inventory.
    /// Shows every recipe defined in FoundationContent; highlights craftable ones.
    /// Subscribes to Inventory.OnChanged so the ingredient counts update live.
    /// </summary>
    public sealed class FoundationCraftingAdapter : ICraftingViewModel, IDisposable
    {
        readonly IsoCore.Foundation.CraftingSystem _crafting;
        readonly Inventory _inv;
        readonly FoundationContent _content;

        public event Action Changed;

        public FoundationCraftingAdapter(IsoCore.Foundation.CraftingSystem crafting, Inventory inv, FoundationContent content)
        {
            _crafting = crafting;
            _inv      = inv;
            _content  = content;
            if (_inv != null) _inv.OnChanged += OnChanged;
        }

        public void Dispose()
        {
            if (_inv != null) _inv.OnChanged -= OnChanged;
        }

        void OnChanged() => Changed?.Invoke();

        public int RecipeCount => _content?.Recipes?.Count ?? 0;

        public CraftingRecipeRow GetRecipe(int i)
        {
            if (_content?.Recipes == null || i < 0 || i >= _content.Recipes.Count) return default;
            var r = _content.Recipes[i];
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
            };
        }

        public void Craft(string recipeId)
        {
            if (_content?.Recipes == null || _crafting == null) return;
            var r = _content.Recipes.Get(recipeId);
            if (r != null) _crafting.TryCraft(r);
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
                for (int i = 0; i < recipe.inputs.Length; i++)
                {
                    var input = recipe.inputs[i];
                    if (!_inv.Has(input.itemId, input.count))
                    {
                        var def = _content?.Items?.Get(input.itemId);
                        string display = def?.displayName ?? input.itemId;
                        return $"Need {display} x{input.count}";
                    }
                }
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
