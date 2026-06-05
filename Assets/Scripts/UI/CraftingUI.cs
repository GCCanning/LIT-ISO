using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Crafting panel UI.
/// Open with Open(station, profession) from a crafting station interactable.
/// </summary>
public class CraftingUI : MonoBehaviour
{
    public static CraftingUI Instance { get; private set; }

    // -------------------------------------------------------------------------
    // Inspector
    // -------------------------------------------------------------------------

    [Header("Panel")]
    public GameObject panelRoot;

    [Header("Header")]
    public TMP_Text stationNameText;
    public TMP_Text professionLevelText;
    public TMP_Text professionXPText;

    [Header("Recipe List")]
    public Transform   recipeListParent;
    public GameObject  recipeRowPrefab;  // Icon, name, ingredients summary, Craft button

    [Header("Colours")]
    public Color canCraftColour  = Color.white;
    public Color cantCraftColour = new Color(0.5f, 0.5f, 0.5f);

    private RecipeDefinition.CraftingStation   _station;
    private RecipeDefinition.CraftingProfession _profession;
    private readonly List<GameObject> _rows = new();

    // -------------------------------------------------------------------------
    // Singleton lifecycle
    // -------------------------------------------------------------------------

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;
    }

    private void Start()
    {
        if (panelRoot != null) panelRoot.SetActive(false);
        CraftingSystem.OnCraftCompleted   += (_, _) => Refresh();
        CraftingSystem.OnProfessionLevelUp += (_, _) => Refresh();
    }

    // -------------------------------------------------------------------------
    // Public API
    // -------------------------------------------------------------------------

    public void Open(RecipeDefinition.CraftingStation station, RecipeDefinition.CraftingProfession profession)
    {
        _station    = station;
        _profession = profession;
        if (panelRoot != null) panelRoot.SetActive(true);
        Refresh();
    }

    public void Close()
    {
        if (panelRoot != null) panelRoot.SetActive(false);
    }

    // -------------------------------------------------------------------------
    // Private helpers
    // -------------------------------------------------------------------------

    private void Refresh()
    {
        var cs = CraftingSystem.Instance;
        if (cs == null) return;

        if (stationNameText != null)    stationNameText.text    = _station.ToString();
        if (professionLevelText != null) professionLevelText.text = $"{_profession} Lv.{cs.GetProfessionLevel(_profession)}";

        int currentXP = cs.GetProfessionXP(_profession);
        int nextXP    = CraftingSystem.XPForLevel(cs.GetProfessionLevel(_profession) + 1);
        if (professionXPText != null) professionXPText.text = $"{currentXP} / {nextXP} XP";

        foreach (var r in _rows) Destroy(r);
        _rows.Clear();

        if (recipeListParent == null || recipeRowPrefab == null) return;

        var inv     = FindFirstObjectByType<PlayerInventory>();
        var recipes = cs.GetAvailableRecipes(_station, _profession);

        foreach (var recipe in recipes)
        {
            var row = Instantiate(recipeRowPrefab, recipeListParent);
            _rows.Add(row);

            var texts   = row.GetComponentsInChildren<TMP_Text>();
            var images  = row.GetComponentsInChildren<Image>();
            var buttons = row.GetComponentsInChildren<Button>();

            bool canCraft = cs.GetProfessionLevel(_profession) >= recipe.requiredProfessionLevel
                            && HasIngredients(recipe, inv);

            if (texts.Length > 0) { texts[0].text = recipe.displayName; texts[0].color = canCraft ? canCraftColour : cantCraftColour; }
            if (texts.Length > 1) texts[1].text = IngredientsString(recipe, inv);
            if (texts.Length > 2) texts[2].text = $"→ {recipe.outputAmount}× {recipe.outputItem?.displayName}";

            if (images.Length > 0 && recipe.icon != null) images[0].sprite = recipe.icon;

            if (buttons.Length > 0)
            {
                var capturedId = recipe.recipeId;
                buttons[0].interactable = canCraft;
                buttons[0].onClick.RemoveAllListeners();
                buttons[0].onClick.AddListener(() => cs.Craft(capturedId, _station, inv));
            }
        }
    }

    private static bool HasIngredients(RecipeDefinition recipe, PlayerInventory inv)
    {
        if (recipe.ingredients == null || inv == null) return true;
        foreach (var ing in recipe.ingredients)
            if (ing.item != null && inv.GetCount(ing.item.itemId) < ing.amount) return false;
        return true;
    }

    private static string IngredientsString(RecipeDefinition recipe, PlayerInventory inv)
    {
        if (recipe.ingredients == null) return "";
        var sb = new System.Text.StringBuilder();
        foreach (var ing in recipe.ingredients)
        {
            if (ing.item == null) continue;
            int have = inv != null ? inv.GetCount(ing.item.itemId) : 0;
            sb.Append($"{have}/{ing.amount} {ing.item.displayName}  ");
        }
        return sb.ToString().TrimEnd();
    }
}
