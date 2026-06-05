using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Top-right HUD element showing the current in-game time and day/night phase.
///
/// Reads from <see cref="DayNightMusicManager.normalizedCycleTime"/> and maps it
/// to a 24-hour clock display, with dawn at 06:00 and midnight at 00:00.
///
/// Cycle layout:
///   normalizedCycleTime 0.00  =  06:00 (dawn — start of day)
///   normalizedCycleTime 0.25  =  12:00 (noon)
///   normalizedCycleTime 0.50  =  18:00 (dusk — start of night)
///   normalizedCycleTime 0.75  =  00:00 (midnight)
///   normalizedCycleTime 1.00  =  06:00 (next dawn — loops)
///
/// Auto-creates child Text widgets on Start. Attach this to any RectTransform
/// inside a Canvas, or use GameplayLayerSetup.SetupTimeAndZoomHUD() to wire it.
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class GameTimeUI : MonoBehaviour
{
    // -------------------------------------------------------------------------
    // Inspector
    // -------------------------------------------------------------------------

    [Header("References")]
    [Tooltip("Source of cycle time. If null, will auto-find on Start.")]
    public DayNightMusicManager cycleManager;

    [Header("Display Options")]
    [Tooltip("Show seconds in the time display (e.g. '12:34:56' vs '12:34').")]
    public bool showSeconds = false;

    [Tooltip("Use 24-hour format (16:30) instead of 12-hour (4:30 PM).")]
    public bool use24HourFormat = true;

    [Header("Visual")]
    public Color timeColor = new Color(1f, 0.95f, 0.7f, 1f);
    public Color phaseColor = new Color(0.8f, 0.85f, 0.95f, 0.85f);
    public int timeFontSize = 26;
    public int phaseFontSize = 13;

    // -------------------------------------------------------------------------
    // Runtime
    // -------------------------------------------------------------------------

    private Text timeText;
    private Text phaseText;

    private void Start()
    {
        AutoWireReferences();
        BuildChildren();
    }

    private void Update()
    {
        if (cycleManager == null || timeText == null) return;

        float normalizedTime = cycleManager.normalizedCycleTime;
        UpdateTimeDisplay(normalizedTime);
        UpdatePhaseDisplay(normalizedTime);
    }

    // -------------------------------------------------------------------------
    // Setup
    // -------------------------------------------------------------------------

    private void AutoWireReferences()
    {
        if (cycleManager == null)
        {
            cycleManager = FindFirstObjectByType<DayNightMusicManager>();
        }
    }

    private void BuildChildren()
    {
        RectTransform parentRect = GetComponent<RectTransform>();

        // Time display (big)
        timeText = CreateTextChild("TimeText", timeFontSize, timeColor, TextAnchor.UpperRight);
        RectTransform timeRect = timeText.GetComponent<RectTransform>();
        timeRect.anchorMin = new Vector2(1f, 1f);
        timeRect.anchorMax = new Vector2(1f, 1f);
        timeRect.pivot = new Vector2(1f, 1f);
        timeRect.anchoredPosition = new Vector2(0f, 0f);
        timeRect.sizeDelta = new Vector2(180f, 32f);
        timeText.text = "—";

        // Phase indicator (smaller, below)
        phaseText = CreateTextChild("PhaseText", phaseFontSize, phaseColor, TextAnchor.UpperRight);
        RectTransform phaseRect = phaseText.GetComponent<RectTransform>();
        phaseRect.anchorMin = new Vector2(1f, 1f);
        phaseRect.anchorMax = new Vector2(1f, 1f);
        phaseRect.pivot = new Vector2(1f, 1f);
        phaseRect.anchoredPosition = new Vector2(0f, -32f);
        phaseRect.sizeDelta = new Vector2(180f, 20f);
        phaseText.text = "";
    }

    private Text CreateTextChild(string name, int fontSize, Color color, TextAnchor alignment)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(transform, false);
        Text text = go.AddComponent<Text>();
        text.color = color;
        text.alignment = alignment;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.raycastTarget = false;
        LitIsoFont.Apply(text, fontSize);
        return text;
    }

    // -------------------------------------------------------------------------
    // Time conversion
    // -------------------------------------------------------------------------

    /// <summary>
    /// Convert normalized cycle time (0-1) to a "hours of day" value where:
    ///   dawn (t=0)   → 6.0  (06:00)
    ///   noon (t=.25) → 12.0 (12:00)
    ///   dusk (t=.5)  → 18.0 (18:00)
    ///   mid  (t=.75) → 0.0  (00:00) — wraps
    ///   dawn (t=1)   → 6.0  (06:00)
    /// </summary>
    private static float NormalizedTimeToHours(float t)
    {
        return ((t * 24f) + 6f) % 24f;
    }

    private void UpdateTimeDisplay(float normalizedTime)
    {
        float hoursFloat = NormalizedTimeToHours(normalizedTime);
        int hours = Mathf.FloorToInt(hoursFloat);
        float minutesFloat = (hoursFloat - hours) * 60f;
        int minutes = Mathf.FloorToInt(minutesFloat);
        int seconds = Mathf.FloorToInt((minutesFloat - minutes) * 60f);

        string display;
        if (use24HourFormat)
        {
            display = showSeconds
                ? $"{hours:D2}:{minutes:D2}:{seconds:D2}"
                : $"{hours:D2}:{minutes:D2}";
        }
        else
        {
            int hours12 = ((hours + 11) % 12) + 1;
            string suffix = hours < 12 ? "AM" : "PM";
            display = showSeconds
                ? $"{hours12}:{minutes:D2}:{seconds:D2} {suffix}"
                : $"{hours12}:{minutes:D2} {suffix}";
        }

        timeText.text = display;
    }

    private void UpdatePhaseDisplay(float t)
    {
        string phase;
        if      (t < 0.08f) phase = "✦ Dawn";
        else if (t < 0.20f) phase = "☀ Morning";
        else if (t < 0.30f) phase = "☀ Noon";
        else if (t < 0.42f) phase = "☀ Afternoon";
        else if (t < 0.58f) phase = "✦ Dusk";
        else if (t < 0.70f) phase = "☾ Evening";
        else if (t < 0.80f) phase = "☾ Midnight";
        else if (t < 0.92f) phase = "☾ Late Night";
        else                phase = "✦ Pre-Dawn";

        phaseText.text = phase;
    }
}
