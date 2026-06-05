using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using EthraClone.TrialWeek;

/// <summary>
/// Manages recipe-based crafting and per-profession level progression.
///
/// Add as a component on a persistent Managers GameObject.
/// Assign all RecipeDefinition assets to availableRecipes[].
///
/// Events:
///   OnCraftStarted(RecipeDefinition)
///   OnCraftCompleted(RecipeDefinition, ItemDefinition output)
///   OnProfessionLevelUp(CraftingProfession, int newLevel)
/// </summary>
public class CraftingSystem : MonoBehaviour
{
    public static CraftingSystem Instance { get; private set; }

    public static event Action<RecipeDefinition>                           OnCraftStarted;
    public static event Action<RecipeDefinition, ItemDefinition>           OnCraftCompleted;
    public static event Action<RecipeDefinition.CraftingProfession, int>   OnProfessionLevelUp;

    // -------------------------------------------------------------------------
    // Profession levelling
    // -------------------------------------------------------------------------

    private readonly Dictionary<RecipeDefinition.CraftingProfession, int> professionXP    = new();
    private readonly Dictionary<RecipeDefinition.CraftingProfession, int> professionLevel = new();

    public int GetProfessionLevel(RecipeDefinition.CraftingProfession p) =>
        professionLevel.GetValueOrDefault(p, 1);

    public int GetProfessionXP(RecipeDefinition.CraftingProfession p) =>
        professionXP.GetValueOrDefault(p, 0);

    public static int XPForLevel(int level) => level * level * 50;   // 50, 200, 450 … 500,000

    // -------------------------------------------------------------------------
    // Inspector
    // -------------------------------------------------------------------------

    [Header("Recipe Pool")]
    [Tooltip("All RecipeDefinition assets. Add new ones here.")]
    public RecipeDefinition[] availableRecipes;

    // -------------------------------------------------------------------------
    // Singleton lifecycle
    // -------------------------------------------------------------------------

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void OnDestroy() { if (Instance == this) Instance = null; }

    // -------------------------------------------------------------------------
    // Public API
    // -------------------------------------------------------------------------

    /// <summary>
    /// Attempt to craft a recipe. Checks station, profession level, and materials.
    /// If craftTimeSeconds > 0 the routine runs as a coroutine (item appears after delay).
    /// Returns false immediately if requirements not met.
    /// </summary>
    public bool Craft(string recipeId, RecipeDefinition.CraftingStation atStation, PlayerInventory inventory)
    {
        var recipe = FindRecipe(recipeId);
        if (recipe == null) return false;

        // Station check
        if (recipe.requiredStation != RecipeDefinition.CraftingStation.Anywhere
            && recipe.requiredStation != atStation)
        {
            SystemNotifier.Instance?.Announce(
                $"Requires a {recipe.requiredStation}.", SystemNotifier.MessageType.Warning);
            return false;
        }

        // Profession level check
        if (GetProfessionLevel(recipe.profession) < recipe.requiredProfessionLevel)
        {
            SystemNotifier.Instance?.Announce(
                $"Requires {recipe.profession} level {recipe.requiredProfessionLevel}.",
                SystemNotifier.MessageType.Warning);
            return false;
        }

        // Material check
        if (!HasIngredients(recipe, inventory)) return false;

        // Consume ingredients
        ConsumeIngredients(recipe, inventory);

        if (recipe.craftTimeSeconds > 0f)
            StartCoroutine(CraftRoutine(recipe, inventory));
        else
            FinishCraft(recipe, inventory);

        return true;
    }

    /// <summary>Returns all recipes available at a given station and profession level.</summary>
    public List<RecipeDefinition> GetAvailableRecipes(
        RecipeDefinition.CraftingStation station,
        RecipeDefinition.CraftingProfession profession)
    {
        var result = new List<RecipeDefinition>();
        if (availableRecipes == null) return result;
        foreach (var r in availableRecipes)
        {
            if (r == null) continue;
            if (r.profession != profession && profession != RecipeDefinition.CraftingProfession.General) continue;
            if (r.requiredStation != RecipeDefinition.CraftingStation.Anywhere && r.requiredStation != station) continue;
            result.Add(r);
        }
        return result;
    }

    // -------------------------------------------------------------------------
    // Private helpers
    // -------------------------------------------------------------------------

    private IEnumerator CraftRoutine(RecipeDefinition recipe, PlayerInventory inventory)
    {
        OnCraftStarted?.Invoke(recipe);

        Vector3 pos = PlayerHealth.Instance != null
            ? PlayerHealth.Instance.transform.position
            : Vector3.zero;

        WorldFloatingText.Spawn(pos + Vector3.up, $"Crafting {recipe.displayName}…", new Color(0.8f, 0.8f, 0.3f));
        yield return new WaitForSeconds(recipe.craftTimeSeconds);
        FinishCraft(recipe, inventory);
    }

    private void FinishCraft(RecipeDefinition recipe, PlayerInventory inventory)
    {
        if (recipe.outputItem != null)
            inventory?.Add(recipe.outputItem, recipe.outputAmount);

        Vector3 pos = PlayerHealth.Instance != null
            ? PlayerHealth.Instance.transform.position
            : Vector3.zero;

        WorldFloatingText.Spawn(pos + Vector3.up,
            $"Crafted {recipe.displayName} ×{recipe.outputAmount}",
            new Color(0.4f, 1f, 0.5f));

        SystemNotifier.Instance?.Announce(
            $"{recipe.displayName} crafted!", SystemNotifier.MessageType.Info);

        OnCraftCompleted?.Invoke(recipe, recipe.outputItem);

        AwardProfessionXP(recipe.profession, recipe.professionXpReward);
        ActionTracker.Instance?.LogAction("local_player", "Crafted", recipe.recipeId, recipe.professionXpReward);
    }

    private bool HasIngredients(RecipeDefinition recipe, PlayerInventory inv)
    {
        if (recipe.ingredients == null || inv == null) return true;
        foreach (var ing in recipe.ingredients)
        {
            if (ing.item == null) continue;
            if (inv.GetCount(ing.item.itemId) < ing.amount)
            {
                SystemNotifier.Instance?.Announce(
                    $"Need {ing.amount}× {ing.item.displayName}.", SystemNotifier.MessageType.Warning);
                return false;
            }
        }
        return true;
    }

    private void ConsumeIngredients(RecipeDefinition recipe, PlayerInventory inv)
    {
        if (recipe.ingredients == null || inv == null) return;
        foreach (var ing in recipe.ingredients)
            if (ing.item != null) inv.Remove(ing.item.itemId, ing.amount);
    }

    private void AwardProfessionXP(RecipeDefinition.CraftingProfession profession, int xp)
    {
        if (!professionXP.ContainsKey(profession))  { professionXP[profession]    = 0; professionLevel[profession] = 1; }
        professionXP[profession] += xp;

        int currentLevel = professionLevel[profession];
        while (professionXP[profession] >= XPForLevel(currentLevel + 1) && currentLevel < 100)
        {
            currentLevel++;
            professionLevel[profession] = currentLevel;
            SystemNotifier.Instance?.Announce(
                $"{profession} reached Level {currentLevel}!", SystemNotifier.MessageType.Achievement);
            OnProfessionLevelUp?.Invoke(profession, currentLevel);
        }
    }

    private RecipeDefinition FindRecipe(string id)
    {
        if (availableRecipes == null) return null;
        foreach (var r in availableRecipes)
            if (r != null && r.recipeId == id) return r;
        return null;
    }
}
