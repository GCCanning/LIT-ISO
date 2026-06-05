using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using EthraClone.TrialWeek;

/// <summary>
/// Top-centre HUD for the 7-day Trial Week: a day counter (Day X / 7), a day/night
/// time indicator with a day-progress bar, and eight "hidden scoring category" pips
/// (Combat, Magic, Crafting, Exploration, Homesteading, Social, Wealth, Survival)
/// that fill with relative score and pulse when the player does a relevant action.
///
/// Design notes:
///   • Built entirely from procedural colour panels + LitIsoFont — NO sprites required.
///     Every icon is an Image you can later assign a sprite to (sprite-ready).
///   • Reads only public APIs (DayNightMusicManager, TrialWeekManager, ActionTracker).
///     Touches NO world/height/sorting code. Degrades gracefully if Phase 2 systems
///     aren't in the scene (shows Day 1/7 and a working clock).
///   • Self-builds its hierarchy at Awake if not pre-populated, like HotbarUI.
/// </summary>
public class TrialWeekHUD : MonoBehaviour
{
    [Header("Player")]
    [Tooltip("Player id used to read scoring categories from ActionTracker.")]
    public string playerId = "local";

    [Header("Layout")]
    public float panelWidth = 360f;
    public float topMargin = 14f;
    public float pipSize = 26f;
    public float pipSpacing = 8f;

    [Header("Palette")]
    public Color panelBg      = new Color(0.07f, 0.09f, 0.13f, 0.82f);
    public Color panelBorder  = new Color(0.30f, 0.36f, 0.42f, 1f);
    public Color dayTextColor = new Color(0.95f, 0.91f, 0.74f, 1f);
    public Color barTrack     = new Color(1f, 1f, 1f, 0.10f);
    public Color barFill      = new Color(0.98f, 0.85f, 0.45f, 1f);
    public Color daySunColor  = new Color(1f, 0.86f, 0.45f, 1f);
    public Color nightMoonColor = new Color(0.62f, 0.72f, 0.95f, 1f);
    public Color pipEmpty     = new Color(1f, 1f, 1f, 0.14f);

    private const int TotalDays = 7;

    // 8 scoring categories — must match ScoringWeightCalculator's category strings.
    private struct Category
    {
        public string key;     // ActionTracker category string
        public string label;   // short placeholder label (until icon sprites exist)
        public Color color;
        public Category(string key, string label, Color color) { this.key = key; this.label = label; this.color = color; }
    }

    private static readonly Category[] Categories =
    {
        new Category("combat",       "CMB", new Color(0.86f, 0.30f, 0.28f)),
        new Category("magic",        "MAG", new Color(0.62f, 0.40f, 0.90f)),
        new Category("crafting",     "CRF", new Color(0.92f, 0.58f, 0.26f)),
        new Category("exploration",  "EXP", new Color(0.36f, 0.78f, 0.44f)),
        new Category("homesteading", "HOM", new Color(0.74f, 0.60f, 0.36f)),
        new Category("social",       "SOC", new Color(0.34f, 0.62f, 0.92f)),
        new Category("wealth",       "WLT", new Color(0.93f, 0.80f, 0.34f)),
        new Category("survival",     "SUR", new Color(0.30f, 0.78f, 0.74f)),
    };

    // Runtime references built procedurally.
    private Text dayText;
    private Image timeDot;
    private Image dayBarFill;
    private RectTransform dayBarFillRect;
    private readonly List<Image> pipFills = new List<Image>();
    private readonly Dictionary<string, float> lastPoints = new Dictionary<string, float>();
    private readonly Dictionary<string, float> pulseTimers = new Dictionary<string, float>();

    private int currentDay = 1;
    private float pollTimer;

    private void Awake()
    {
        BuildUI();
        SubscribeTrialEvents();
        RefreshDay(ResolveCurrentDay());
    }

    private void OnDestroy()
    {
        UnsubscribeTrialEvents();
    }

    private void Update()
    {
        UpdateClock();
        UpdatePulse();

        pollTimer -= Time.deltaTime;
        if (pollTimer <= 0f)
        {
            pollTimer = 0.4f;
            PollCategoryScores();
        }
    }

    // -------------------------------------------------------------------------
    // Data wiring (read-only, all guarded)
    // -------------------------------------------------------------------------

    private void SubscribeTrialEvents()
    {
        TrialWeekManager tw = TrialWeekManager.Instance;
        if (tw != null)
        {
            tw.OnPlayerDayChanged += HandlePlayerDayChanged;
        }
    }

    private void UnsubscribeTrialEvents()
    {
        TrialWeekManager tw = TrialWeekManager.Instance;
        if (tw != null)
        {
            tw.OnPlayerDayChanged -= HandlePlayerDayChanged;
        }
    }

    private void HandlePlayerDayChanged(string changedPlayerId, int newDay)
    {
        // Accept the local player's day, or any player's if we can't match ids.
        if (string.IsNullOrEmpty(playerId) || changedPlayerId == playerId)
        {
            RefreshDay(newDay);
        }
    }

    private int ResolveCurrentDay()
    {
        // Default to day 1; authoritative updates arrive via the event.
        return Mathf.Clamp(currentDay, 1, TotalDays);
    }

    private void RefreshDay(int day)
    {
        currentDay = Mathf.Clamp(day, 1, TotalDays);
        if (dayText != null)
        {
            dayText.text = $"DAY {currentDay} / {TotalDays}";
        }
    }

    private void UpdateClock()
    {
        DayNightMusicManager dn = DayNightMusicManager.Instance;
        float t = dn != null ? Mathf.Repeat(dn.normalizedCycleTime, 1f) : 0.25f;

        // Day fraction across the cycle (0 dawn → 0.5 dusk → 1 next dawn).
        // First half = daytime, second half = night.
        bool isDay = t < 0.5f;
        if (timeDot != null)
        {
            timeDot.color = isDay ? daySunColor : nightMoonColor;
        }

        // Day-progress bar shows progress through the current day/night half.
        float halfProgress = isDay ? (t / 0.5f) : ((t - 0.5f) / 0.5f);
        if (dayBarFillRect != null)
        {
            dayBarFillRect.anchorMax = new Vector2(Mathf.Clamp01(halfProgress), 1f);
        }
    }

    private void PollCategoryScores()
    {
        ActionTracker tracker = ActionTracker.Instance;
        if (tracker == null) return;

        Dictionary<string, float> points = tracker.GetPointsByCategory(playerId);
        if (points == null) return;

        // Find the max for relative fill scaling.
        float max = 1f;
        foreach (var kv in points) max = Mathf.Max(max, kv.Value);

        for (int i = 0; i < Categories.Length; i++)
        {
            string key = Categories[i].key;
            points.TryGetValue(key, out float value);

            // Pulse when this category gained points since last poll.
            lastPoints.TryGetValue(key, out float prev);
            if (value > prev + 0.01f)
            {
                pulseTimers[key] = 0.5f;
            }
            lastPoints[key] = value;

            // Fill alpha by relative magnitude.
            if (i < pipFills.Count && pipFills[i] != null)
            {
                float fill = Mathf.Clamp01(value / max);
                Color c = Categories[i].color;
                c.a = Mathf.Lerp(0.18f, 1f, fill);
                pipFills[i].color = c;
            }
        }
    }

    private void UpdatePulse()
    {
        for (int i = 0; i < Categories.Length; i++)
        {
            string key = Categories[i].key;
            if (!pulseTimers.TryGetValue(key, out float timer) || timer <= 0f) continue;

            timer -= Time.deltaTime;
            pulseTimers[key] = timer;

            if (i < pipFills.Count && pipFills[i] != null)
            {
                float p = Mathf.Clamp01(timer / 0.5f);
                float scale = 1f + 0.35f * p;
                pipFills[i].transform.parent.localScale = new Vector3(scale, scale, 1f);
            }
        }
    }

    // -------------------------------------------------------------------------
    // Procedural UI construction (colour panels + LitIsoFont, sprite-ready)
    // -------------------------------------------------------------------------

    private void BuildUI()
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null)
        {
            GameObject canvasGO = new GameObject("TrialWeekHUD Canvas");
            canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            canvasGO.AddComponent<GraphicRaycaster>();
            transform.SetParent(canvasGO.transform, false);
        }

        // Root panel, top-centre.
        RectTransform root = CreatePanel("TrialWeekPanel", transform, panelBg, panelBorder);
        root.anchorMin = new Vector2(0.5f, 1f);
        root.anchorMax = new Vector2(0.5f, 1f);
        root.pivot = new Vector2(0.5f, 1f);
        root.anchoredPosition = new Vector2(0f, -topMargin);
        root.sizeDelta = new Vector2(panelWidth, 86f);

        // --- Top row: time dot + day text + day-progress bar ---
        timeDot = CreateImage("TimeDot", root, daySunColor);
        RectTransform dotRect = timeDot.rectTransform;
        dotRect.anchorMin = new Vector2(0f, 1f);
        dotRect.anchorMax = new Vector2(0f, 1f);
        dotRect.pivot = new Vector2(0f, 1f);
        dotRect.anchoredPosition = new Vector2(14f, -12f);
        dotRect.sizeDelta = new Vector2(20f, 20f);

        dayText = CreateText("DayText", root, "DAY 1 / 7", 26, dayTextColor, TextAnchor.MiddleLeft);
        RectTransform dayRect = dayText.rectTransform;
        dayRect.anchorMin = new Vector2(0f, 1f);
        dayRect.anchorMax = new Vector2(0f, 1f);
        dayRect.pivot = new Vector2(0f, 1f);
        dayRect.anchoredPosition = new Vector2(44f, -8f);
        dayRect.sizeDelta = new Vector2(160f, 28f);

        // Day-progress bar track + fill
        RectTransform track = CreateImage("DayBarTrack", root, barTrack).rectTransform;
        track.anchorMin = new Vector2(0f, 1f);
        track.anchorMax = new Vector2(0f, 1f);
        track.pivot = new Vector2(0f, 1f);
        track.anchoredPosition = new Vector2(210f, -16f);
        track.sizeDelta = new Vector2(panelWidth - 226f, 10f);

        dayBarFill = CreateImage("DayBarFill", track, barFill);
        dayBarFillRect = dayBarFill.rectTransform;
        dayBarFillRect.anchorMin = new Vector2(0f, 0f);
        dayBarFillRect.anchorMax = new Vector2(0.5f, 1f);
        dayBarFillRect.offsetMin = Vector2.zero;
        dayBarFillRect.offsetMax = Vector2.zero;

        // --- Bottom row: 8 category pips ---
        float totalPipsWidth = Categories.Length * pipSize + (Categories.Length - 1) * pipSpacing;
        float startX = (panelWidth - totalPipsWidth) * 0.5f;

        for (int i = 0; i < Categories.Length; i++)
        {
            RectTransform pip = CreatePanel($"Pip_{Categories[i].key}", root, pipEmpty, panelBorder);
            pip.anchorMin = new Vector2(0f, 1f);
            pip.anchorMax = new Vector2(0f, 1f);
            pip.pivot = new Vector2(0.5f, 1f);
            pip.anchoredPosition = new Vector2(startX + pipSize * 0.5f + i * (pipSize + pipSpacing), -44f);
            pip.sizeDelta = new Vector2(pipSize, pipSize);

            // Fill image (the coloured part that scales/pulses) — sprite-ready slot.
            Image fill = CreateImage("Fill", pip, pipEmpty);
            RectTransform fillRect = fill.rectTransform;
            fillRect.anchorMin = new Vector2(0.5f, 0.5f);
            fillRect.anchorMax = new Vector2(0.5f, 0.5f);
            fillRect.pivot = new Vector2(0.5f, 0.5f);
            fillRect.sizeDelta = new Vector2(pipSize - 8f, pipSize - 8f);
            pipFills.Add(fill);

            // Short placeholder label beneath the pip.
            Text label = CreateText($"Label_{Categories[i].key}", pip, Categories[i].label, 13, dayTextColor, TextAnchor.UpperCenter);
            RectTransform labelRect = label.rectTransform;
            labelRect.anchorMin = new Vector2(0.5f, 0f);
            labelRect.anchorMax = new Vector2(0.5f, 0f);
            labelRect.pivot = new Vector2(0.5f, 1f);
            labelRect.anchoredPosition = new Vector2(0f, -2f);
            labelRect.sizeDelta = new Vector2(pipSize + 12f, 16f);

            lastPoints[Categories[i].key] = 0f;
        }
    }

    private RectTransform CreatePanel(string name, Transform parent, Color bg, Color border)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        RectTransform rt = go.GetComponent<RectTransform>();

        Image bgImage = go.AddComponent<Image>();
        bgImage.color = bg;

        // Thin border via Outline (cheap, no sprite needed).
        Outline outline = go.AddComponent<Outline>();
        outline.effectColor = border;
        outline.effectDistance = new Vector2(1.5f, -1.5f);

        return rt;
    }

    private Image CreateImage(string name, Transform parent, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        Image img = go.AddComponent<Image>();
        img.color = color;
        return img;
    }

    private Text CreateText(string name, Transform parent, string content, int size, Color color, TextAnchor anchor)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        Text text = go.AddComponent<Text>();
        text.text = content;
        text.color = color;
        text.alignment = anchor;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        LitIsoFont.Apply(text, size);
        return text;
    }
}
