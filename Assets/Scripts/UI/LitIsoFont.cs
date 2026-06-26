using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Shared font access for LIT-ISO UI text.
///
/// Two faces ship in Resources/Fonts/:
///   press-start-2p.ttf  — display / headings / numeric callouts  (all-caps, hard 1-bit)
///   pixelify-sans.ttf   — body / button labels / longer text      (400-700 weight feel)
///
/// Fallback chain: press-start-2p → lumos → Legacy built-in
///                 pixelify-sans  → body  → Legacy built-in → UI face
/// </summary>
public static class LitIsoFont
{
    private const string DisplayPath  = "Fonts/press-start-2p";
    private const string DisplayFallback = "Fonts/lumos";
    private const string BodyPath     = "Fonts/pixelify-sans";
    private const string BodyFallback = "Fonts/body";

    public const string TextScalePrefKey = "ui.textScale";

    // Sizes >= this threshold use the display face; smaller sizes use the body face.
    // Press Start 2P is very wide — only use it for headings / numbers / tab labels.
    private const int DisplayMinSize = 19;

    private static Font _display;
    private static Font _body;
    private static float _lastBroadcastScale = float.NaN;

    /// <summary>Display face — "Press Start 2P". Titles, headings, numeric callouts, tab labels.</summary>
    public static Font UI
    {
        get
        {
            if (_display == null)
            {
                _display = Resources.Load<Font>(DisplayPath)
                        ?? Resources.Load<Font>(DisplayFallback)
                        ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")
                        ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
            }
            return _display;
        }
    }

    /// <summary>Body face — "Pixelify Sans". Button labels, inventory text, body copy.</summary>
    public static Font Body
    {
        get
        {
            if (_body == null)
            {
                _body = Resources.Load<Font>(BodyPath)
                     ?? Resources.Load<Font>(BodyFallback)
                     ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")
                     ?? UI;
            }
            return _body;
        }
    }

    public static float TextScale =>
        Mathf.Clamp(PlayerPrefs.GetFloat(TextScalePrefKey, 1.0f), 0.8f, 1.45f);

    public static event Action<float> TextScaleChanged;

    public static void SetTextScale(float scale)
    {
        scale = Mathf.Clamp(scale, 0.8f, 1.45f);
        if (Mathf.Abs(TextScale - scale) < 0.001f) return;
        PlayerPrefs.SetFloat(TextScalePrefKey, scale);
        PlayerPrefs.Save();
        NotifyTextScaleChanged(scale);
    }

    internal static void NotifyTextScaleChanged(float scale)
    {
        scale = Mathf.Clamp(scale, 0.8f, 1.45f);
        if (!float.IsNaN(_lastBroadcastScale) && Mathf.Abs(_lastBroadcastScale - scale) < 0.001f)
            return;
        _lastBroadcastScale = scale;
        TextScaleChanged?.Invoke(scale);
    }

    /// <summary>Snap a requested point size through the text-scale pref.</summary>
    public static int SnapSize(int requestedSize)
    {
        // Press Start 2P renders large — display sizes need no inflation.
        // Body sizes render true.
        return Mathf.Max(9, Mathf.RoundToInt(requestedSize * TextScale));
    }

    /// <summary>Apply the correct face and snapped size to a legacy Text component.</summary>
    public static void Apply(Text text, int requestedSize, FontStyle style = FontStyle.Normal)
    {
        if (text == null) return;
        text.font      = requestedSize >= DisplayMinSize ? UI : Body;
        text.fontSize  = SnapSize(requestedSize);
        text.fontStyle = style;
        text.resizeTextForBestFit = false;
    }

    public static void Apply(TextMesh text, int requestedSize, FontStyle style = FontStyle.Normal)
    {
        if (text == null) return;
        text.font      = UI;
        text.fontSize  = SnapSize(requestedSize);
        text.fontStyle = style;
        var r = text.GetComponent<Renderer>();
        if (r != null && text.font != null) r.sharedMaterial = text.font.material;
    }

    /// <summary>Force the cached fonts to reload (call after dropping new TTFs into Resources/Fonts).</summary>
    public static void InvalidateCache()
    {
        _display = null;
        _body    = null;
    }
}
