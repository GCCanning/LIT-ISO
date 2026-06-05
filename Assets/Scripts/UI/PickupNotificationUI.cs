using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Stacks pickup notifications on the right side of the screen.
///
/// When an item is added to PlayerInventory a panel slides in from the right, holds for
/// a configurable time, then fades out. Duplicate item pickups within the hold period
/// are batched into the existing panel (amount counter increments, timer resets).
///
/// Layout: panels anchor to top-right, stack downward. Oldest panel is at the top;
/// new panels appear below and push existing ones down via simple repositioning.
/// </summary>
public class PickupNotificationUI : MonoBehaviour
{
    [Header("Timing")]
    public float holdDuration    = 2.4f;
    public float fadeOutDuration = 0.45f;
    public float slideInDuration = 0.15f;
    public float slideInPixels   = 28f;

    [Header("Layout")]
    public int   maxVisible      = 5;
    public float panelHeight     = 30f;
    public float panelSpacing    = 4f;

    [Header("Appearance")]
    public Color panelBgColor   = new Color(0.06f, 0.09f, 0.13f, 0.84f);
    public Color itemNameColor  = new Color(0.92f, 0.87f, 0.62f, 1.00f);
    public Color amountColor    = new Color(0.48f, 0.90f, 0.40f, 1.00f);
    public float panelWidth     = 160f;
    public float iconSize       = 22f;

    // -------------------------------------------------------------------------
    // Private helpers
    // -------------------------------------------------------------------------

    private class NotificationEntry
    {
        public string        itemId;
        public int           accumulatedDelta;
        public GameObject    root;
        public CanvasGroup   group;
        public Text          amountLabel;
        public Text          nameLabel;
        public Image         iconImage;
        public RectTransform rect;
        public Coroutine     lifetime;
    }

    private readonly List<NotificationEntry> active = new List<NotificationEntry>();

    // We track deltas ourselves because PlayerInventory fires (item, newTotal, delta)
    // — we use the delta argument directly.

    // -------------------------------------------------------------------------
    // Unity lifecycle
    // -------------------------------------------------------------------------

    private void OnEnable()  => Subscribe();
    private void OnDisable() => Unsubscribe();

    private void Start()
    {
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
        if (delta <= 0) return;  // Removals don't get notifications
        ShowPickup(item, delta);
    }

    // -------------------------------------------------------------------------
    // Core display logic
    // -------------------------------------------------------------------------

    /// <summary>
    /// Display or batch a pickup notification for <paramref name="item"/>.
    /// Can also be called directly from other systems (e.g. quest rewards).
    /// </summary>
    public void ShowPickup(ItemDefinition item, int amount)
    {
        if (item == null || amount <= 0) return;

        // Check for an existing active notification for this item
        foreach (var entry in active)
        {
            if (entry.itemId == item.itemId)
            {
                entry.accumulatedDelta += amount;
                RefreshEntryLabels(entry);

                if (entry.lifetime != null) StopCoroutine(entry.lifetime);
                entry.lifetime = StartCoroutine(EntryLifetime(entry));
                return;
            }
        }

        // Cull oldest if at max capacity
        while (active.Count >= maxVisible)
            RemoveEntry(active[0], immediate: true);

        // Create new entry
        NotificationEntry newEntry = CreateEntry(item, amount);
        active.Add(newEntry);
        RepositionAll();

        newEntry.lifetime = StartCoroutine(EntryLifetime(newEntry));
        StartCoroutine(SlideIn(newEntry));
    }

    // -------------------------------------------------------------------------
    // Entry construction
    // -------------------------------------------------------------------------

    private NotificationEntry CreateEntry(ItemDefinition item, int amount)
    {
        // --- Root panel ---
        var panelGO = new GameObject($"Notif_{item.itemId}", typeof(RectTransform));
        panelGO.transform.SetParent(transform, false);

        var rect = panelGO.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(1f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot     = new Vector2(1f, 1f);
        rect.sizeDelta = new Vector2(panelWidth, panelHeight);
        // Initial position: off-screen to the right (slides in)
        rect.anchoredPosition = new Vector2(slideInPixels, 0f);

        var bg = panelGO.AddComponent<Image>();
        bg.color = panelBgColor;

        var cg = panelGO.AddComponent<CanvasGroup>();
        cg.alpha = 1f;

        // --- Icon ---
        var iconGO = new GameObject("Icon", typeof(RectTransform));
        iconGO.transform.SetParent(panelGO.transform, false);
        var iconRt = iconGO.GetComponent<RectTransform>();
        float pad = (panelHeight - iconSize) * 0.5f;
        iconRt.anchorMin = new Vector2(0f, 0f);
        iconRt.anchorMax = new Vector2(0f, 1f);
        iconRt.offsetMin = new Vector2(pad, pad);
        iconRt.offsetMax = new Vector2(pad + iconSize, -pad);
        var iconImg = iconGO.AddComponent<Image>();
        iconImg.sprite          = item.icon;
        iconImg.color           = item.icon != null ? Color.white : new Color(0.5f, 0.5f, 0.5f, 0.6f);
        iconImg.preserveAspect  = true;

        // --- Amount label (e.g. "+3") ---
        float labelLeft = pad + iconSize + 5f;
        var amtGO = new GameObject("Amount", typeof(RectTransform));
        amtGO.transform.SetParent(panelGO.transform, false);
        var amtRt = amtGO.GetComponent<RectTransform>();
        amtRt.anchorMin = new Vector2(0f, 0f);
        amtRt.anchorMax = new Vector2(0f, 1f);
        amtRt.offsetMin = new Vector2(labelLeft, 0f);
        amtRt.offsetMax = new Vector2(labelLeft + 36f, 0f);
        var amtTxt = amtGO.AddComponent<Text>();
        amtTxt.text      = $"+{amount}";
        amtTxt.alignment = TextAnchor.MiddleLeft;
        amtTxt.color     = amountColor;
        LitIsoFont.Apply(amtTxt, 13, FontStyle.Bold);

        // --- Item name label ---
        var nameGO = new GameObject("Name", typeof(RectTransform));
        nameGO.transform.SetParent(panelGO.transform, false);
        var nameRt = nameGO.GetComponent<RectTransform>();
        nameRt.anchorMin = new Vector2(0f, 0f);
        nameRt.anchorMax = new Vector2(1f, 1f);
        nameRt.offsetMin = new Vector2(labelLeft + 40f, 0f);
        nameRt.offsetMax = new Vector2(-6f, 0f);
        var nameTxt = nameGO.AddComponent<Text>();
        nameTxt.text      = item.displayName;
        nameTxt.alignment = TextAnchor.MiddleLeft;
        nameTxt.color     = itemNameColor;
        LitIsoFont.Apply(nameTxt, 13);

        return new NotificationEntry
        {
            itemId           = item.itemId,
            accumulatedDelta = amount,
            root             = panelGO,
            group            = cg,
            amountLabel      = amtTxt,
            nameLabel        = nameTxt,
            iconImage        = iconImg,
            rect             = rect,
        };
    }

    private void RefreshEntryLabels(NotificationEntry entry)
    {
        if (entry.amountLabel != null)
            entry.amountLabel.text = $"+{entry.accumulatedDelta}";
    }

    // -------------------------------------------------------------------------
    // Layout
    // -------------------------------------------------------------------------

    private void RepositionAll()
    {
        // Stack from top downward: first entry sits at y=0 (top), rest move down.
        for (int i = 0; i < active.Count; i++)
        {
            float targetY = -(i * (panelHeight + panelSpacing));
            active[i].rect.anchoredPosition = new Vector2(active[i].rect.anchoredPosition.x, targetY);
        }
    }

    // -------------------------------------------------------------------------
    // Coroutines
    // -------------------------------------------------------------------------

    private IEnumerator EntryLifetime(NotificationEntry entry)
    {
        yield return new WaitForSeconds(holdDuration);
        yield return FadeOut(entry);
        RemoveEntry(entry, immediate: false);
    }

    private IEnumerator FadeOut(NotificationEntry entry)
    {
        float elapsed = 0f;
        while (elapsed < fadeOutDuration)
        {
            if (entry.group != null)
                entry.group.alpha = Mathf.Lerp(1f, 0f, elapsed / fadeOutDuration);
            elapsed += Time.deltaTime;
            yield return null;
        }
        if (entry.group != null) entry.group.alpha = 0f;
    }

    private IEnumerator SlideIn(NotificationEntry entry)
    {
        float elapsed = 0f;
        while (elapsed < slideInDuration)
        {
            if (entry.rect != null)
            {
                float t = elapsed / slideInDuration;
                float x = Mathf.Lerp(slideInPixels, 0f, t);
                entry.rect.anchoredPosition = new Vector2(x, entry.rect.anchoredPosition.y);
            }
            elapsed += Time.deltaTime;
            yield return null;
        }
        if (entry.rect != null)
            entry.rect.anchoredPosition = new Vector2(0f, entry.rect.anchoredPosition.y);
    }

    private void RemoveEntry(NotificationEntry entry, bool immediate)
    {
        if (entry.lifetime != null)
        {
            StopCoroutine(entry.lifetime);
            entry.lifetime = null;
        }

        active.Remove(entry);

        if (entry.root != null)
        {
            if (immediate)
                Destroy(entry.root);
            else
                Destroy(entry.root);
        }

        RepositionAll();
    }

}
