using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Settlement management panel.
/// Shows current tier, population, built buildings, and allows placing new ones.
///
/// Call Open(settlement) from a TownSign/NPC interaction to show this panel.
/// </summary>
public class TownUI : MonoBehaviour
{
    public static TownUI Instance { get; private set; }

    // -------------------------------------------------------------------------
    // Inspector
    // -------------------------------------------------------------------------

    [Header("Panel")]
    public GameObject panelRoot;

    [Header("Settlement Info")]
    public TMP_Text settlementNameText;
    public TMP_Text tierText;
    public TMP_Text populationText;

    [Header("Buildings List")]
    public Transform buildingListParent;
    public GameObject buildingEntryPrefab;   // Row: icon, name, cost, Build button

    [Header("Colour")]
    public Color canBuildColour  = new Color(0.4f, 1f, 0.4f);
    public Color cantBuildColour = new Color(1f, 0.4f, 0.4f);

    // -------------------------------------------------------------------------
    // Runtime state
    // -------------------------------------------------------------------------

    private TownManager.Settlement _current;
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
        TownManager.OnBuildingPlaced  += (s, _) => { if (s == _current) Refresh(); };
        TownManager.OnSettlementTierUp += (s, _) => { if (s == _current) Refresh(); };
    }

    // -------------------------------------------------------------------------
    // Public API
    // -------------------------------------------------------------------------

    public void Open(TownManager.Settlement settlement)
    {
        _current = settlement;
        if (panelRoot != null) panelRoot.SetActive(true);
        Refresh();
    }

    public void Close()
    {
        if (panelRoot != null) panelRoot.SetActive(false);
        _current = null;
    }

    // -------------------------------------------------------------------------
    // Private helpers
    // -------------------------------------------------------------------------

    private void Refresh()
    {
        if (_current == null) return;

        if (settlementNameText != null) settlementNameText.text = _current.name;
        if (tierText != null)           tierText.text  = $"{_current.TierName} (Tier {_current.tier})";
        if (populationText != null)     populationText.text = $"Population: {_current.population} / {_current.PopCap}";

        // Clear existing rows
        foreach (var r in _rows) Destroy(r);
        _rows.Clear();

        if (buildingListParent == null || buildingEntryPrefab == null) return;

        var manager  = TownManager.Instance;
        var inventory = FindFirstObjectByType<PlayerInventory>();

        foreach (var def in manager?.availableBuildings ?? System.Array.Empty<SettlementDefinition>())
        {
            if (def == null) continue;

            var row = Instantiate(buildingEntryPrefab, buildingListParent);
            _rows.Add(row);

            var texts   = row.GetComponentsInChildren<TMP_Text>();
            var images  = row.GetComponentsInChildren<Image>();
            var buttons = row.GetComponentsInChildren<Button>();

            bool alreadyBuilt = _current.HasBuilding(def.buildingId);
            bool tierOk       = _current.tier >= def.requiredTier;
            bool levelOk      = XPSystem.Instance == null || XPSystem.Instance.CurrentLevel >= def.requiredPlayerLevel;
            bool canBuild     = !alreadyBuilt && tierOk && levelOk;

            // Name text
            if (texts.Length > 0)
            {
                texts[0].text  = alreadyBuilt ? $"✓ {def.displayName}" : def.displayName;
                texts[0].color = alreadyBuilt ? canBuildColour : (canBuild ? Color.white : cantBuildColour);
            }

            // Cost text
            if (texts.Length > 1)
                texts[1].text = BuildCostString(def);

            // Icon
            if (images.Length > 0 && def.icon != null)
                images[0].sprite = def.icon;

            // Build button
            if (buttons.Length > 0)
            {
                var capturedDef = def;
                buttons[0].interactable = canBuild;
                buttons[0].GetComponentInChildren<TMP_Text>()?.SetText(alreadyBuilt ? "Built" : "Build");
                buttons[0].onClick.RemoveAllListeners();
                buttons[0].onClick.AddListener(() =>
                {
                    manager?.PlaceBuilding(_current, capturedDef.buildingId, inventory);
                    Refresh();
                });
            }
        }
    }

    private static string BuildCostString(SettlementDefinition def)
    {
        if (def.buildCost == null || def.buildCost.Length == 0) return "Free";
        var sb = new System.Text.StringBuilder();
        foreach (var c in def.buildCost)
            if (c.item != null) sb.Append($"{c.amount}× {c.item.displayName}  ");
        return sb.ToString().TrimEnd();
    }
}
