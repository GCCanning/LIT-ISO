using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Shared font access for LIT-ISO UI text.
/// Antiquity Print looks cleanest at sizes divisible by 13.
/// </summary>
public static class LitIsoFont
{
    private const string FontResourcePath = "Fonts/antiquity-print";

    private static Font cachedFont;

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
        if (requestedSize <= 13) return 13;
        return Mathf.Max(13, Mathf.RoundToInt(requestedSize / 13f) * 13);
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
