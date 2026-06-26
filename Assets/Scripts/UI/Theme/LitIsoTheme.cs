using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// LIT-ISO front-end DESIGN SYSTEM — single source of truth for the shared
/// visual theme used by BOTH the main-menu flow (WelcomeScreenManager and its
/// sub-screens) and the in-game tabbed panels (CharacterPanelView + the InGame
/// views via UiBuilder).
///
/// Tokens transcribed from Docs/handoff/frontend_design/LIT-ISO_Frontend.dc.html
/// (the approved Vue mockup + its STYLE GUIDE / DESIGN TOKENS section).
///
/// Direction: cozy dark-fantasy, isometric pixel-art. Hard shadows, no blur,
/// point-filtered. Display face = "Press Start 2P"; body/UI face = "Pixelify Sans".
///
/// This class is global (no namespace) and public so the global-namespace menu
/// AND the LitIso.UI.InGame views can both call into it. It centralises colour
/// constants, font access, and helper builders for the three panel frames and
/// the four button states (rest / hover / pressed / disabled) with the signature
/// 5px hard bottom shadow + 4px press offset.
///
/// IMPORTANT — fonts: the two design TTFs ("Press Start 2P", "Pixelify Sans")
/// are NOT yet in Resources/Fonts. Until the owner drops them in, Display falls
/// back to lumos (the current display face) and Body falls back to the readable
/// body font. See DisplayFont / BodyFont.
/// </summary>
public static class LitIsoTheme
{
    // ----------------------------- PALETTE -----------------------------
    // Names + hex match the STYLE GUIDE palette swatches.
    public static readonly Color Scene     = Hex("#0a0c14"); // deepest scene base
    public static readonly Color Base      = Hex("#0a0b0e"); // hard border / shadow black
    public static readonly Color Panel     = Hex("#15171C"); // main stone panel fill
    public static readonly Color Panel2    = Hex("#1D2027"); // slightly raised fill
    public static readonly Color Raised    = Hex("#23262E"); // raised / secondary button fill
    public static readonly Color Stone     = Hex("#2c313c"); // inset stone bevel
    public static readonly Color GoldDeep  = Hex("#5a4d2a"); // gold inner-frame line / shadow

    public static readonly Color Gold      = Hex("#E8C468"); // primary gold accent
    public static readonly Color GoldLit   = Hex("#f5d98a"); // light gold (hover / bevel)
    public static readonly Color GoldHi    = Hex("#fff0c8"); // hover inner bevel highlight
    public static readonly Color GoldPress = Hex("#d9b658"); // pressed gold fill
    public static readonly Color GoldShadow= Hex("#8a6f24"); // gold button hard bottom shadow
    public static readonly Color GoldText  = Hex("#1a1408"); // text drawn ON gold buttons

    public static readonly Color Parchment    = Hex("#d8d4c8"); // primary parchment text
    public static readonly Color ParchmentLit = Hex("#e8e4d8"); // brighter parchment (inputs)
    public static readonly Color WarmTan      = Hex("#8a8578"); // dim label / subtitle text
    public static readonly Color TextDimmer   = Hex("#5a564c"); // disabled / footer text

    // Semantic / feedback colours
    public static readonly Color Red      = Hex("#d96a55"); // warning / error / hard difficulty
    public static readonly Color RedHard  = Hex("#d9425a"); // danger / death / critical
    public static readonly Color Green    = Hex("#6fae6f"); // success / buff / easy difficulty
    public static readonly Color Amber    = Hex("#e8a03c"); // warning / durability mid / hunger

    // Rarity palette — matches the mockup STYLE GUIDE exactly
    public static readonly Color RarityCommon    = Hex("#5a6068"); // grey
    public static readonly Color RarityUncommon  = Hex("#5aa05a"); // green
    public static readonly Color RarityRare      = Hex("#4f8ad9"); // blue
    public static readonly Color RarityEpic      = Hex("#a060d9"); // purple
    public static readonly Color RarityLegendary = Gold;           // #E8C468 gold

    // Overlay / modal scrim
    public static readonly Color ModalScrim = new Color(0.016f, 0.020f, 0.027f, 0.60f); // rgba(4,5,7,.6)

    // Window border grammar constants (used by BuildPixelPanel in UiBuilder)
    public const float BorderInk   = 3f;   // outer hard ink border
    public const float BorderSteel = 1f;   // inset steel line
    public const float BorderGold  = 1f;   // innermost gold hairline
    public const float StudSize    = 10f;  // corner stud diamond half-size

    // Durability colours (inventory slots)
    public static readonly Color DurabilityGood = Hex("#6fae6f"); // green  >60 %
    public static readonly Color DurabilityMid  = Hex("#e8a03c"); // amber  20-60 %
    public static readonly Color DurabilityLow  = Hex("#d96a55"); // red    <20 %

    // Wood + parchment frame fills (sub-panel variants).
    public static readonly Color WoodFill   = Hex("#241a10");
    public static readonly Color WoodBevel1 = Hex("#3a2a18");
    public static readonly Color WoodBevel2 = Hex("#5a3f22");
    public static readonly Color WoodText   = Hex("#c9a26a");

    public static readonly Color ParchFill   = Hex("#d9c9a3");
    public static readonly Color ParchBorder = Hex("#6b5836");
    public static readonly Color ParchBevel  = Hex("#c2ad82");
    public static readonly Color ParchInk    = Hex("#5a4326");

    // Disabled button tokens.
    public static readonly Color DisabledFill  = Hex("#1c1f25");
    public static readonly Color DisabledBevel = Hex("#23262E");

    /// <summary>Standard hard black drop-shadow used under panels / readable text.</summary>
    public static readonly Color ShadowBlack = new Color(0f, 0f, 0f, 0.82f);

    public enum FrameStyle { Stone, Wood, Parchment }

    // ----------------------------- FONTS -----------------------------
    // The approved system uses "Press Start 2P" (display) + "Pixelify Sans"
    // (body). Drop the two TTFs into Assets/Resources/Fonts as:
    //   Resources/Fonts/press-start-2p.ttf  (display)
    //   Resources/Fonts/pixelify-sans.ttf   (body / UI)
    // and they win automatically. Until then we fall back to the shipping
    // fonts so the project always compiles and renders.
    private const string DisplayPath         = "Fonts/press-start-2p";
    private const string DisplayFallbackPath = "Fonts/lumos";          // current display face
    private const string BodyPath            = "Fonts/pixelify-sans";
    private const string BodyFallbackPath    = "Fonts/body";           // owner-droppable body face

    private static Font _display;
    private static Font _body;

    /// <summary>Display face — "Press Start 2P". Titles, headings, gold wordmarks.</summary>
    public static Font DisplayFont
    {
        get
        {
            if (_display == null)
            {
                _display = Resources.Load<Font>(DisplayPath)
                        ?? Resources.Load<Font>(DisplayFallbackPath)
                        ?? LitIsoFont.UI; // LitIsoFont has a deep builtin fallback chain
            }
            return _display;
        }
    }

    /// <summary>Body / UI face — "Pixelify Sans". Labels, buttons, body copy.</summary>
    public static Font BodyFont
    {
        get
        {
            if (_body == null)
            {
                _body = Resources.Load<Font>(BodyPath)
                     ?? Resources.Load<Font>(BodyFallbackPath)
                     ?? LitIsoFont.Body;
            }
            return _body;
        }
    }

    /// <summary>True once the real Press Start 2P TTF is present in Resources/Fonts.</summary>
    public static bool DisplayFontInstalled => Resources.Load<Font>(DisplayPath) != null;

    /// <summary>Apply the display face to a heading Text (keeps LitIsoFont scaling).</summary>
    public static void ApplyDisplay(Text t, int size, Color? color = null)
    {
        if (t == null) return;
        LitIsoFont.Apply(t, size); // sizing / scale / overflow defaults
        t.font = DisplayFont;
        if (color.HasValue) t.color = color.Value;
    }

    /// <summary>Apply the body face to a label / body Text.</summary>
    public static void ApplyBody(Text t, int size, Color? color = null)
    {
        if (t == null) return;
        LitIsoFont.Apply(t, size);
        t.font = BodyFont;
        if (color.HasValue) t.color = color.Value;
    }

    // --------------------------- PANEL FRAMES ---------------------------
    // The mockup builds frames from a 2px #0a0b0e border + inset stone bevel
    // (and, for Stone, an extra gold inner line). uGUI can't stack many insets
    // on one Image, so we approximate with: fill Image + Outline (bevel) and,
    // for Stone, a thin gold inner-border child. Looks consistent at UI scale
    // and stays point-filtered / hard-edged.

    /// <summary>
    /// Style an existing fill Image as one of the three panel frames.
    /// Pass the panel's background Image; it gets the correct fill colour,
    /// a hard border outline, and (for Stone) a gold inner accent line.
    /// </summary>
    public static void StyleFrame(Image fill, FrameStyle style = FrameStyle.Stone)
    {
        if (fill == null) return;

        switch (style)
        {
            case FrameStyle.Wood:
                fill.color = WoodFill;
                AddOutline(fill.gameObject, WoodBevel2, 2.5f);
                break;
            case FrameStyle.Parchment:
                fill.color = ParchFill;
                AddOutline(fill.gameObject, ParchBorder, 2f);
                break;
            default: // Stone - the locked main frame.
                fill.color = Panel;
                AddOutline(fill.gameObject, Stone, 2f);
                AddInnerLine(fill.rectTransform, GoldDeep);
                break;
        }
    }

    /// <summary>Create a new themed panel under parent.</summary>
    public static Image NewFrame(Transform parent, string name, FrameStyle style = FrameStyle.Stone)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var img = go.AddComponent<Image>();
        StyleFrame(img, style);
        return img;
    }

    // --------------------------- BUTTON STATES ---------------------------
    public enum ButtonStyle { Gold, Stone }

    /// <summary>
    /// Style an existing Button + its background Image to the design-system
    /// look: 2px hard border, inset bevel, and the signature 5px HARD bottom
    /// shadow (no blur). Hover brightens, pressed shifts down 4px (the shadow
    /// child shrinks to 1px to read as "pressed in"). Disabled greys out.
    /// Returns the shadow child so callers can re-layout the button and keep
    /// the press animation working.
    /// </summary>
    public static RectTransform StyleButton(Button btn, Image bg, ButtonStyle style = ButtonStyle.Stone)
    {
        if (btn == null || bg == null) return null;
        btn.targetGraphic = bg;
        btn.transition = Selectable.Transition.ColorTint;

        Color rest, hover, pressed, disabled, bevel;
        if (style == ButtonStyle.Gold)
        {
            rest = Gold; hover = GoldLit; pressed = GoldPress; disabled = DisabledFill; bevel = GoldLit;
        }
        else
        {
            rest = Raised; hover = Hex("#2c303a"); pressed = Hex("#191c22"); disabled = DisabledFill; bevel = Stone;
        }

        bg.color = rest;
        AddOutline(bg.gameObject, Base, 2f);   // 2px hard border
        AddInnerBevel(bg.rectTransform, bevel); // inset stone/gold bevel

        var colors = btn.colors;
        colors.normalColor = Color.white;       // tint multiplier over rest fill
        colors.highlightedColor = Mul(hover, rest);
        colors.pressedColor = Mul(pressed, rest);
        colors.selectedColor = Mul(hover, rest);
        colors.disabledColor = Mul(disabled, rest);
        colors.fadeDuration = 0.06f;
        btn.colors = colors;

        // 5px hard bottom shadow as a sibling rect drawn behind the fill.
        var shadow = AddHardBottomShadow(bg.rectTransform,
            style == ButtonStyle.Gold ? GoldShadow : Base);

        // Press feedback: nudge the button down 4px and flatten its shadow.
        var press = bg.gameObject.GetComponent<LitIsoButtonPress>() ?? bg.gameObject.AddComponent<LitIsoButtonPress>();
        press.Configure(btn, bg.rectTransform, shadow);
        return shadow;
    }

    /// <summary>Build a complete themed button (frame + label) in one call.</summary>
    public static Button NewButton(Transform parent, string name, string label, int size,
        ButtonStyle style = ButtonStyle.Stone)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var bg = go.AddComponent<Image>();
        var btn = go.AddComponent<Button>();
        StyleButton(btn, bg, style);

        if (!string.IsNullOrEmpty(label))
        {
            var labelGo = new GameObject("Label", typeof(RectTransform));
            labelGo.transform.SetParent(go.transform, false);
            var t = labelGo.AddComponent<Text>();
            t.text = label;
            t.alignment = TextAnchor.MiddleCenter;
            ApplyDisplay(t, size, style == ButtonStyle.Gold ? GoldText : Parchment);
            var rt = t.rectTransform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(6f, 4f); rt.offsetMax = new Vector2(-6f, -2f);
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Truncate;
            t.resizeTextForBestFit = true;
            t.resizeTextMaxSize = t.fontSize;
            t.resizeTextMinSize = 8;
        }
        return btn;
    }

    // ----------------------------- rarity / durability helpers -----------------------------

    /// <summary>Border/accent colour for an item slot by rarity string (case-insensitive).</summary>
    public static Color RarityColor(string rarity)
    {
        if (string.IsNullOrEmpty(rarity)) return RarityCommon;
        switch (rarity.ToLowerInvariant())
        {
            case "uncommon":  return RarityUncommon;
            case "rare":      return RarityRare;
            case "epic":      return RarityEpic;
            case "legendary": return RarityLegendary;
            default:          return RarityCommon;
        }
    }

    /// <summary>Fill colour for a durability bar at a 0–1 fraction.</summary>
    public static Color DurabilityColor(float t01)
    {
        if (t01 >= 0.6f) return DurabilityGood;
        if (t01 >= 0.2f) return DurabilityMid;
        return DurabilityLow;
    }

    // ----------------------------- helpers -----------------------------
    public static Color Hex(string hex)
    {
        if (ColorUtility.TryParseHtmlString(hex, out var c)) return c;
        return Color.magenta;
    }

    /// <summary>Multiply tint so ColorTint reproduces a target colour over a known base.</summary>
    private static Color Mul(Color target, Color baseCol)
    {
        float Safe(float t, float b) => b <= 0.0001f ? t : Mathf.Clamp01(t / b);
        return new Color(Safe(target.r, baseCol.r), Safe(target.g, baseCol.g),
                         Safe(target.b, baseCol.b), 1f);
    }

    private static void AddOutline(GameObject go, Color color, float dist)
    {
        var o = go.GetComponent<Outline>() ?? go.AddComponent<Outline>();
        o.effectColor = color;
        o.effectDistance = new Vector2(dist, -dist);
        o.useGraphicAlpha = false;
    }

    /// <summary>Thin gold inner-border line (the Stone frame's gold accent).</summary>
    private static void AddInnerLine(RectTransform parent, Color color)
    {
        var go = new GameObject("InnerLine", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var img = go.AddComponent<Image>();
        img.color = new Color(color.r, color.g, color.b, 0.55f);
        img.raycastTarget = false;
        var rt = img.rectTransform;
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(3f, 3f); rt.offsetMax = new Vector2(-3f, -3f);
        // hollow it out so only the border shows
        var fillGo = new GameObject("Fill", typeof(RectTransform));
        fillGo.transform.SetParent(rt, false);
        var fill = fillGo.GetComponent<RectTransform>();
        fill.anchorMin = Vector2.zero; fill.anchorMax = Vector2.one;
        fill.offsetMin = new Vector2(1.5f, 1.5f); fill.offsetMax = new Vector2(-1.5f, -1.5f);
        var fimg = fillGo.AddComponent<Image>();
        fimg.color = Panel;
        fimg.raycastTarget = false;
        go.transform.SetAsFirstSibling(); // behind content
    }

    private static void AddInnerBevel(RectTransform parent, Color color)
    {
        var go = new GameObject("Bevel", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var img = go.AddComponent<Image>();
        img.color = new Color(color.r, color.g, color.b, 0.85f);
        img.raycastTarget = false;
        var rt = img.rectTransform;
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(2f, 2f); rt.offsetMax = new Vector2(-2f, -2f);
        var innerGo = new GameObject("BevelFill", typeof(RectTransform));
        innerGo.transform.SetParent(rt, false);
        var inner = innerGo.GetComponent<RectTransform>();
        inner.anchorMin = Vector2.zero; inner.anchorMax = Vector2.one;
        inner.offsetMin = new Vector2(2f, 2f); inner.offsetMax = new Vector2(-2f, -2f);
        var iimg = innerGo.AddComponent<Image>();
        iimg.color = Color.clear; // bevel reads as a thin frame inside the border
        iimg.raycastTarget = false;
        go.transform.SetSiblingIndex(0);
    }

    /// <summary>5px HARD bottom shadow as a sibling rect behind the button fill.</summary>
    private static RectTransform AddHardBottomShadow(RectTransform parent, Color color)
    {
        var go = new GameObject("HardShadow", typeof(RectTransform));
        go.transform.SetParent(parent.parent, false); // sibling so it sits behind
        var img = go.AddComponent<Image>();
        img.color = color;
        img.raycastTarget = false;
        var rt = img.rectTransform;
        rt.anchorMin = parent.anchorMin; rt.anchorMax = parent.anchorMax;
        rt.pivot = parent.pivot;
        rt.sizeDelta = parent.sizeDelta;
        rt.anchoredPosition = parent.anchoredPosition + new Vector2(0f, -5f);
        go.transform.SetAsFirstSibling();
        return rt;
    }
}
