using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Shared font access for LIT-ISO UI text.
/// Antiquity Print looks cleanest at sizes divisible by 13.
/// </summary>
public static class LitIsoFont
{
    private const string FontResourcePath = "Fonts/antiquity-print";
    public const string TextScalePrefKey = "ui.textScale";

    private static Font cachedFont;
    private static float lastBroadcastScale = float.NaN;

    public static Font UI
    {
        get
        {
            if (cachedFont == null)
            {
                cachedFont = Resources.Load<Font>(FontResourcePath);
                if (cachedFont == null)
                    cachedFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                if (cachedFont == null)
                    cachedFont = Resources.GetBuiltinResource<Font>("Arial.ttf");
            }

            return cachedFont;
        }
    }

    public static int SnapSize(int requestedSize)
    {
        return Mathf.Max(12, Mathf.RoundToInt(requestedSize * 1.2f * TextScale));
    }

    public static float TextScale => Mathf.Clamp(PlayerPrefs.GetFloat(TextScalePrefKey, 1.08f), 0.8f, 1.45f);

    public static event Action<float> TextScaleChanged;

    public static void SetTextScale(float scale)
    {
        scale = Mathf.Clamp(scale, 0.8f, 1.45f);
        if (Mathf.Abs(TextScale - scale) < 0.001f)
            return;

        PlayerPrefs.SetFloat(TextScalePrefKey, scale);
        PlayerPrefs.Save();
        NotifyTextScaleChanged(scale);
    }

    internal static void NotifyTextScaleChanged(float scale)
    {
        scale = Mathf.Clamp(scale, 0.8f, 1.45f);
        if (!float.IsNaN(lastBroadcastScale) && Mathf.Abs(lastBroadcastScale - scale) < 0.001f)
            return;

        lastBroadcastScale = scale;
        TextScaleChanged?.Invoke(scale);
    }

    public static void Apply(Text text, int requestedSize, FontStyle style = FontStyle.Normal)
    {
        if (text == null) return;

        text.font = UI;
        text.fontSize = SnapSize(requestedSize);
        text.fontStyle = style;
        text.resizeTextForBestFit = false;
    }

    public static void Apply(TextMesh text, int requestedSize, FontStyle style = FontStyle.Normal)
    {
        if (text == null) return;

        text.font = UI;
        text.fontSize = SnapSize(requestedSize);
        text.fontStyle = style;

        Renderer renderer = text.GetComponent<Renderer>();
        if (renderer != null && text.font != null)
            renderer.sharedMaterial = text.font.material;
    }
}
