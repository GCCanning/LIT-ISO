using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Renders up to <see cref="MaxSlots"/> item stacks in a bottom-center horizontal bar.
///
/// Items are assigned to slots in discovery order (first item picked up → slot 0, etc.).
/// Slots are built programmatically at Awake if the <see cref="slots"/> array is not
/// pre-populated via the Inspector.
///
/// Subscribes to PlayerInventory.OnStackChanged. Works with legacy UnityEngine.UI.Text
/// (no TMP dependency).
/// </summary>
public class HotbarUI : MonoBehaviour
{
    public const int MaxSlots = 7;

    [Header("Slot references (auto-created at runtime if empty)")]
    public HotbarSlotUI[] slots;

    [Header("Slot appearance")]
    public float slotSize = 52f;
    public float slotSpacing = 6f;
    public Color slotBgColor        = new Color(0.07f, 0.09f, 0.13f, 0.90f);
    public Color slotBorderColor    = new Color(0.30f, 0.36f, 0.42f, 1.00f);
    public Color emptyIconTint      = new Color(1.00f, 1.00f, 1.00f, 0.18f);
    public Color occupiedIconTint   = Color.white;
    public Color countTextColor     = new Color(0.92f, 0.87f, 0.68f, 1.00f);

    // itemId → slot index
    private readonly Dictionary<string, int> slotByItemId = new Dictionary<string, int>();
    private int nextFreeSlot = 0;

    // -------------------------------------------------------------------------
    // Unity lifecycle
    // -------------------------------------------------------------------------

    private void Awake()
    {
        if (slots == null || slots.Length == 0)
            BuildSlots();
    }

    private void OnEnable()  => Subscribe();
    private void OnDisable() => Unsubscribe();

    private void Start()
    {
        // Re-subscribe in case PlayerInventory.Instance was null during OnEnable
        Unsubscribe();
        Subscribe();
    }

    // -------------------------------------------------------------------------
    // Event handling
    // -------------------------------------------------------------------------

    private void Subscribe()
    {
        if (PlayerInventory.Instance != null)
            PlayerInventory.Instance.OnStackChanged += HandleStackChanged;
    }

    private void Unsubscribe()
    {
        if (PlayerInventory.Instance != null)
            PlayerInventory.Instance.OnStackChanged -= HandleStackChanged;
    }

    private void HandleStackChanged(ItemDefinition item, int newTotal, int delta)
    {
        if (item == null) return;

        // Assign a slot the first time we see this item
        if (!slotByItemId.TryGetValue(item.itemId, out int slotIndex))
        {
            if (nextFreeSlot >= MaxSlots) return;   // Bar is full
            slotIndex = nextFreeSlot++;
            slotByItemId[item.itemId] = slotIndex;

            if (slotIndex < slots.Length)
                slots[slotIndex].SetItem(item, emptyIconTint, occupiedIconTint);
        }

        if (slotIndex < slots.Length)
            slots[slotIndex].SetCount(newTotal, countTextColor, emptyIconTint, occupiedIconTint);
    }

    // -------------------------------------------------------------------------
    // Slot construction
    // -------------------------------------------------------------------------

    private void BuildSlots()
    {
        slots = new HotbarSlotUI[MaxSlots];

        float totalWidth = MaxSlots * slotSize + (MaxSlots - 1) * slotSpacing;
        float startX     = -totalWidth * 0.5f + slotSize * 0.5f;

        for (int i = 0; i < MaxSlots; i++)
        {
            // --- Slot root ---
            var slotGO = new GameObject($"HotbarSlot_{i}", typeof(RectTransform));
            slotGO.transform.SetParent(transform, false);
            var rt = slotGO.GetComponent<RectTransform>();
            rt.sizeDelta     = new Vector2(slotSize, slotSize);
            rt.anchoredPosition = new Vector2(startX + i * (slotSize + slotSpacing), 0f);

            // Background panel
            var bg = slotGO.AddComponent<Image>();
            bg.color = slotBgColor;

            // Border outline (a slightly larger Image behind)
            var borderGO = new GameObject("Border", typeof(RectTransform));
            borderGO.transform.SetParent(slotGO.transform, false);
            borderGO.transform.SetAsFirstSibling();
            var borderRt = borderGO.GetComponent<RectTransform>();
            borderRt.anchorMin = Vector2.zero;
            borderRt.anchorMax = Vector2.one;
            borderRt.offsetMin = new Vector2(-1.5f, -1.5f);
            borderRt.offsetMax = new Vector2(1.5f, 1.5f);
            var borderImg = borderGO.AddComponent<Image>();
            borderImg.color = slotBorderColor;
            borderGO.transform.SetAsFirstSibling();

            // --- Icon ---
            var iconGO = new GameObject("Icon", typeof(RectTransform));
            iconGO.transform.SetParent(slotGO.transform, false);
            var iconRt = iconGO.GetComponent<RectTransform>();
            iconRt.anchorMin = Vector2.zero;
            iconRt.anchorMax = Vector2.one;
            iconRt.offsetMin = new Vector2(7f, 11f);
            iconRt.offsetMax = new Vector2(-7f, -7f);
            var iconImg = iconGO.AddComponent<Image>();
            iconImg.color = emptyIconTint;
            iconImg.preserveAspect = true;

            // --- Count label ---
            var countGO = new GameObject("Count", typeof(RectTransform));
            countGO.transform.SetParent(slotGO.transform, false);
            var countRt = countGO.GetComponent<RectTransform>();
            countRt.anchorMin = new Vector2(0f, 0f);
            countRt.anchorMax = new Vector2(1f, 0.32f);
            countRt.offsetMin = new Vector2(2f, 2f);
            countRt.offsetMax = new Vector2(-2f, 0f);
            var countTxt = countGO.AddComponent<Text>();
            countTxt.text      = "";
            countTxt.alignment = TextAnchor.MiddleCenter;
            countTxt.color     = countTextColor;
            LitIsoFont.Apply(countTxt, 13, FontStyle.Bold);

            slots[i] = new HotbarSlotUI
            {
                background = bg,
                icon       = iconImg,
                countLabel = countTxt,
            };
        }
    }

}

// ---------------------------------------------------------------------------
// Slot data class (serializable so Inspector wiring works too)
// ---------------------------------------------------------------------------

[System.Serializable]
public class HotbarSlotUI
{
    public Image background;
    public Image icon;
    public Text  countLabel;

    [System.NonSerialized] public ItemDefinition boundItem;

    public void SetItem(ItemDefinition def, Color emptyTint, Color activeTint)
    {
        boundItem = def;
        if (icon == null) return;
        icon.sprite = def.icon;
        icon.color  = (def.icon != null) ? activeTint : emptyTint;
    }

    public void SetCount(int count, Color textColor, Color emptyTint, Color activeTint)
    {
        if (countLabel != null)
        {
            countLabel.text  = count > 0 ? count.ToString() : "";
            countLabel.color = textColor;
        }
        if (icon != null)
        {
            icon.color = count > 0 ? activeTint : emptyTint;
        }
    }
}
