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
    // Optional readable body font: drop any .ttf at Resources/Fonts/body to override.
    private const string BodyFontResourcePath = "Fonts/body";
    public const string TextScalePrefKey = "ui.textScale";

    // Owner feedback 2026-06-10: the decorative Antiquity face is unreadable at
    // body sizes. Split: Antiquity stays for big display text (titles, headers);
    // everything below this requested size renders in a clean readable font.
    private const int DisplayMinRequestedSize = 19;

    private static Font cachedFont;
    private static Font cachedBodyFont;
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

    /// <summary>Readable font for body copy, lists, and small labels.</summary>
    public static Font Body
    {
        get
        {
            if (cachedBodyFont == null)
            {
                cachedBodyFont = Resources.Load<Font>(BodyFontResourcePath);
                if (cachedBodyFont == null)
                    cachedBodyFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                if (cachedBodyFont == null)
                    cachedBodyFont = UI;
            }

            return cachedBodyFont;
        }
    }

    public static int SnapSize(int requestedSize)
    {
        // 1.2x inflation existed to compensate for Antiquity rendering small.
        // Body text now uses a normal font, so inflate ONLY display-size text;
        // body sizes render true (fixes text overflowing panels/slots/bars).
        float compensate = requestedSize >= DisplayMinRequestedSize ? 1.2f : 1.0f;
        return Mathf.Max(11, Mathf.RoundToInt(requestedSize * compensate * TextScale));
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

        // Display face only at heading sizes; readable body face below.
        text.font = requestedSize >= DisplayMinRequestedSize ? UI : Body;
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
