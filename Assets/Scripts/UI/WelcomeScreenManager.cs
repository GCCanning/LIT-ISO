using System.Collections.Generic;
using System.IO;
using IsoCore.Foundation;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Welcome / Main Menu system for LIT-ISO. Procedurally builds three screens:
///   1. Main Menu (New Game / Load Game / Options / Quit)
///   2. Create World (name + seed input, difficulty slider)
///   3. Load Game (list of saved worlds)
///
/// All built with colored panels + LitIsoFont, sprite-ready (no sprites required yet).
/// Saves world config as JSON in a dedicated folder.
/// </summary>
public class WelcomeScreenManager : MonoBehaviour
{
    private enum Screen { MainMenu, CreateWorld, CallingSelect, LoadGame, Options }

    [Header("Menu skin (all optional)")]
    [Tooltip("Each slot can be assigned in the inspector OR auto-loaded by filename from " +
             "Resources/UI/Menu/. Any slot left empty falls back to the procedural look, " +
             "so the menu always works even with no images.")]
    public Sprite backgroundImage;   // Resources/UI/Menu/background  (full-screen splash)
    public Sprite logoImage;         // Resources/UI/Menu/logo        (title wordmark, top of main menu)
    public Sprite panelImage;        // Resources/UI/Menu/panel       (9-sliced card frame)
    public Sprite buttonImage;       // Resources/UI/Menu/button      (9-sliced button, rest)
    public Sprite buttonHoverImage;  // Resources/UI/Menu/button_hover(9-sliced button, hover)

    [Tooltip("Logo display height in px (its width scales to preserve aspect).")]
    public float logoHeight = 180f;

    [Tooltip("Build identifier appended to the version string, e.g. '1154'. Leave empty to show only the version.")]
    public string buildNumber = "";

    [Header("Layout")]
    public float panelWidth = 500f;
    public float panelHeight = 600f;
    public float buttonHeight = 50f;
    public float spacing = 12f;

    [Header("Palette (LIT-ISO design system — LitIsoTheme)")]
    // Defaults now mirror the approved design tokens so the menu matches the
    // in-game panels out of the box. Inspector overrides still win.
    public Color panelBg = LitIsoTheme.Panel;          // #15171C stone panel
    public Color panelBorder = LitIsoTheme.Stone;      // #2c313c inset bevel
    public Color buttonBg = LitIsoTheme.Raised;        // #23262E raised button
    public Color buttonBgHover = LitIsoTheme.Hex("#2c303a");
    public Color buttonText = LitIsoTheme.Parchment;   // #d8d4c8 parchment
    public Color inputBg = LitIsoTheme.Hex("#0e1014");  // dark inset input field
    public Color inputText = LitIsoTheme.ParchmentLit; // #e8e4d8
    public Color labelText = LitIsoTheme.WarmTan;      // #8a8578 dim label
    [Tooltip("Gold accent used for titles/headings + selected calling card.")]
    public Color goldAccent = LitIsoTheme.Gold;        // #E8C468

    private Screen currentScreen = Screen.MainMenu;
    private Canvas mainCanvas;
    private RectTransform contentPanel;

    // World creation data
    private InputField worldNameInput;
    private InputField seedInput;
    private int selectedDifficulty = 1; // 0 Easy, 1 Normal, 2 Hard
    private readonly List<(int idx, Image bg, Text label)> diffSegmentCards = new();
    private Text difficultyDescLabel;
    private Text difficultyValueLabel;   // colour-coded label shown beside "DIFFICULTY"

    // Difficulty options — labels, colours and copy transcribed verbatim from the
    // LIT-ISO front-end design (diffData). Replaces the old slider.
    private static readonly (string label, Color color, string desc)[] DifficultyOptions =
    {
        ("Easy",   LitIsoTheme.Green, "Forgiving foes, generous loot, no permadeath. A gentle way into the realm."),
        ("Normal", LitIsoTheme.Gold,  "The intended balance — fair fights, meaningful scarcity, honest risk."),
        ("Hard",   LitIsoTheme.Red,   "Punishing enemies, sparse resources, harsh nights. For seasoned survivors."),
    };

    // Load game data
    private List<WorldSaveData> savedWorlds = new List<WorldSaveData>();
    private RectTransform worldListContent;
    private int pendingDeleteIndex = -1;     // two-step delete confirmation
    private float pendingDeleteTime;

    // Calling select data — the world is created first, then the player picks a Calling
    // before the Foundation scene loads. selectedCallingId is passed to ConfigureLaunch.
    private WorldSaveData pendingWorld;
    private string selectedCallingId;
    private readonly List<(string id, Image bg, Outline outline)> callingCards = new();

    // Save path
    private string savePath => Path.Combine(Application.persistentDataPath, "LitIsoWorlds");

    [System.Serializable]
    public class WorldSaveData
    {
        public string worldName = "Untitled World";
        public string seed = "12345";
        public int difficulty = 2; // 0=easy, 1=normal, 2=hard
        public long createdTicks;

        // createdTicks is the unique key; the name is sanitized so worlds named with
        // characters illegal in filenames (: / ? * " etc.) don't silently fail to save.
        public string filename => $"{SanitizeName(worldName)}_{createdTicks}.world.json";

        private static string SanitizeName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "world";
            foreach (char c in Path.GetInvalidFileNameChars())
            {
                name = name.Replace(c, '_');
            }
            return name.Trim();
        }
    }

    private void Awake()
    {
        EnsureSaveFolder();
        LoadSkin();
        BuildCanvas();
        StartMenuMusic();
        ShowScreen(Screen.MainMenu);
    }

    /// <summary>
    /// Soft looping menu music. Prefers a dedicated track at Resources/Audio/Music/menu
    /// (drop one in and it wins); falls back to the night ambience, then day music.
    /// Volume respects the shared master-volume pref.
    /// </summary>
    private void StartMenuMusic()
    {
        AudioClip clip = Resources.Load<AudioClip>("Audio/Music/menu")
                      ?? Resources.Load<AudioClip>("Audio/Ambient/night")
                      ?? Resources.Load<AudioClip>("Audio/Music/day");
        if (clip == null) return;

        var go = new GameObject("MenuMusic");
        go.transform.SetParent(transform, false);
        var src = go.AddComponent<AudioSource>();
        src.clip = clip;
        src.loop = true;
        src.spatialBlend = 0f;
        src.volume = 0.35f * Mathf.Clamp01(PlayerPrefs.GetFloat("vol_master", 1f));
        src.Play();
    }

    /// <summary>
    /// Fills any unassigned skin slot from Resources/UI/Menu/&lt;name&gt;. Drop PNGs there
    /// (imported as Sprite) and they appear automatically — no inspector wiring needed.
    /// Anything still missing falls back to the procedural look.
    /// </summary>
    private void LoadSkin()
    {
        if (backgroundImage == null)
            backgroundImage = Resources.Load<Sprite>("UI/Menu/background")
                           ?? Resources.Load<Sprite>("UI/CampfireMenu"); // legacy fallback
        if (logoImage == null)        logoImage        = Resources.Load<Sprite>("UI/Menu/logo");
        if (panelImage == null)       panelImage       = Resources.Load<Sprite>("UI/Menu/panel");
        if (buttonImage == null)      buttonImage      = Resources.Load<Sprite>("UI/Menu/button");
        if (buttonHoverImage == null) buttonHoverImage = Resources.Load<Sprite>("UI/Menu/button_hover");
    }

    private void EnsureSaveFolder()
    {
        if (!Directory.Exists(savePath))
        {
            Directory.CreateDirectory(savePath);
        }
    }

    // -------------------------------------------------------------------------
    // Screen Management
    // -------------------------------------------------------------------------

    private void ShowScreen(Screen screen)
    {
        currentScreen = screen;
        if (contentPanel != null)
        {
            Destroy(contentPanel.gameObject);
        }
        StartCoroutine(FadeInNewScreen());

        switch (screen)
        {
            case Screen.MainMenu:
                BuildMainMenu();
                break;
            case Screen.CreateWorld:
                BuildCreateWorld();
                break;
            case Screen.CallingSelect:
                BuildCallingSelect();
                break;
            case Screen.LoadGame:
                BuildLoadGame();
                break;
            case Screen.Options:
                BuildOptions();
                break;
        }
    }

    // -------------------------------------------------------------------------
    // Main Menu
    // -------------------------------------------------------------------------

    private void BuildMainMenu()
    {
        // ====================================================================
        // TITLE / MAIN MENU — transcribed from the front-end design (isTitle).
        // A left-aligned column 120px from the screen's left edge: small gold
        // eyebrow, big "LIT-ISO" wordmark, diamond+rule divider, then the menu
        // buttons (each a left-aligned stone bar with a gold diamond bullet).
        //
        // This screen is NOT a centred stone card — it sits directly on the
        // canvas so the background art reads behind it. We therefore build a
        // transparent full-height container as contentPanel (it still drives
        // the fade-in + Destroy on screen change) instead of CreateMainPanel.
        // ====================================================================
        contentPanel = CreateTransparentContainer("MainMenu");

        // Left-anchored column. Design: margin-left:120px, width:560px,
        // vertically centred. Reference resolution is 1920×1080.
        const float columnWidth = 560f;
        GameObject colGO = new GameObject("Column", typeof(RectTransform));
        colGO.transform.SetParent(contentPanel, false);
        RectTransform col = colGO.GetComponent<RectTransform>();
        col.anchorMin = new Vector2(0f, 0.5f);
        col.anchorMax = new Vector2(0f, 0.5f);
        col.pivot = new Vector2(0f, 0.5f);
        col.anchoredPosition = new Vector2(120f, 0f);
        col.sizeDelta = new Vector2(columnWidth, 0f);

        // Cursor walks downward from the column top; everything is top-anchored
        // inside the column so spacing matches the HTML's stacked margins.
        float y = 0f;

        // Eyebrow — "A LITRPG SURVIVAL CRAFTER", letter-spaced (design: Press Start
        // 2P, +6 tracking). The design's dim #8a8578 vanishes over the bright sunset
        // backdrop, so use the readable parchment tone + a crisp outline.
        Text eyebrow = CreateText("Eyebrow", col, "A  L I T R P G   S U R V I V A L   C R A F T E R",
            15, LitIsoTheme.Parchment, TextAnchor.LowerLeft);
        eyebrow.resizeTextForBestFit = false;
        eyebrow.horizontalOverflow = HorizontalWrapMode.Overflow;
        var eyebrowOutline = eyebrow.gameObject.AddComponent<Outline>();
        eyebrowOutline.effectColor = LitIsoTheme.Base;
        eyebrowOutline.effectDistance = new Vector2(2f, -2f);
        eyebrowOutline.useGraphicAlpha = true;
        PlaceTopLeft(eyebrow.rectTransform, columnWidth, 24f, y);
        y -= 24f + 12f;

        // Wordmark — "LIT-ISO" stacked on two lines. Rendered as a SINGLE rich-text
        // Text (dash recoloured via a <color> tag) so the font's own line metrics
        // space the two lines with no overlap and the glyphs stay tight — the
        // per-glyph layout-group version collided once the font scaled up. The box
        // is sized from the text's own preferredHeight so it's correct at any UI
        // text-scale. Press Start 2P + hard shadow (added by CreateText at size>=30).
        string dashHex = ColorUtility.ToHtmlStringRGB(LitIsoTheme.Parchment);
        Text wordmark = CreateText("Wordmark", col,
            "LIT<color=#" + dashHex + ">-</color>\nISO", 88, goldAccent, TextAnchor.UpperLeft);
        wordmark.resizeTextForBestFit = false;
        wordmark.horizontalOverflow = HorizontalWrapMode.Overflow;
        wordmark.verticalOverflow = VerticalWrapMode.Overflow;
        wordmark.lineSpacing = 0.92f; // design line-height ≈ .96, tightened slightly
        float wordmarkH = wordmark.preferredHeight;
        if (wordmarkH < 60f) wordmarkH = 210f; // fallback if metrics not ready
        PlaceTopLeft(wordmark.rectTransform, columnWidth, wordmarkH, y);
        y -= wordmarkH + 8f;

        // Divider — gold diamond (14px, rotated 45°) + a 2px gradient rule.
        // margin-bottom:42px below it before the buttons begin.
        GameObject divGO = new GameObject("Divider", typeof(RectTransform));
        divGO.transform.SetParent(col, false);
        RectTransform div = divGO.GetComponent<RectTransform>();
        PlaceTopLeft(div, columnWidth, 14f, y);

        GameObject diaGO = new GameObject("Diamond", typeof(RectTransform));
        diaGO.transform.SetParent(div, false);
        RectTransform dia = diaGO.GetComponent<RectTransform>();
        dia.anchorMin = new Vector2(0f, 0.5f);
        dia.anchorMax = new Vector2(0f, 0.5f);
        dia.pivot = new Vector2(0f, 0.5f);
        dia.anchoredPosition = new Vector2(0f, 0f);
        dia.sizeDelta = new Vector2(14f, 14f);
        dia.localRotation = Quaternion.Euler(0f, 0f, 45f);
        Image diaImg = diaGO.AddComponent<Image>();
        diaImg.color = goldAccent;
        diaImg.raycastTarget = false;

        GameObject ruleGO = new GameObject("Rule", typeof(RectTransform));
        ruleGO.transform.SetParent(div, false);
        RectTransform rule = ruleGO.GetComponent<RectTransform>();
        rule.anchorMin = new Vector2(0f, 0.5f);
        rule.anchorMax = new Vector2(0f, 0.5f);
        rule.pivot = new Vector2(0f, 0.5f);
        rule.anchoredPosition = new Vector2(24f, 0f);
        rule.sizeDelta = new Vector2(columnWidth - 24f, 2f);
        Image ruleImg = ruleGO.AddComponent<Image>();
        // Approximates linear-gradient(90deg,#5a4d2a,transparent): a dim gold rule.
        ruleImg.color = new Color(LitIsoTheme.GoldDeep.r, LitIsoTheme.GoldDeep.g, LitIsoTheme.GoldDeep.b, 0.75f);
        ruleImg.raycastTarget = false;

        y -= 14f + 42f;

        // ---- Menu buttons -------------------------------------------------
        // Design: min-width:215px, left-aligned stone bar with a diamond bullet.
        const float titleBtnH = 60f;
        const float titleBtnGap = 12f;
        // Buttons are content-fit (not full column width) — ~260px accommodates
        // the longest label "Creation Instance" at Pixelify Sans 26px.
        const float titleBtnW = 260f;

        // Continue — only shown when at least one saved world exists.
        WorldSaveData mostRecent = GetMostRecentWorld();
        if (mostRecent != null)
        {
            CreateTitleMenuButton("ContinueBtn", col, "Continue", () => LaunchWorld(mostRecent), titleBtnW, titleBtnH, y);
            y -= titleBtnH + titleBtnGap;
        }

        CreateTitleMenuButton("NewGameBtn", col, "New Game", () => ShowScreen(Screen.CreateWorld), titleBtnW, titleBtnH, y);
        y -= titleBtnH + titleBtnGap;
        CreateTitleMenuButton("CreationInstanceBtn", col, "Creation Instance", LaunchCreationInstance, titleBtnW, titleBtnH, y);
        y -= titleBtnH + titleBtnGap;
        CreateTitleMenuButton("LoadGameBtn", col, "Load Game", () => ShowScreen(Screen.LoadGame), titleBtnW, titleBtnH, y);
        y -= titleBtnH + titleBtnGap;
        CreateTitleMenuButton("OptionsBtn", col, "Options", () => ShowScreen(Screen.Options), titleBtnW, titleBtnH, y);
        y -= titleBtnH + titleBtnGap;
        CreateTitleMenuButton("QuitBtn", col, "Quit", () => Application.Quit(), titleBtnW, titleBtnH, y);
        y -= titleBtnH;

        // Size the column to its content (top→bottom span) so the fade group has
        // sensible bounds; it's centred vertically via the anchored column.
        col.sizeDelta = new Vector2(columnWidth, -y);

        // Version tag, bottom-right of the screen — "v… · build …".
        string versionStr = $"v{Application.version}";
        if (!string.IsNullOrEmpty(buildNumber))
            versionStr += $" · build {buildNumber}";
        Text ver = CreateText("Version", contentPanel,
            versionStr, 11, LitIsoTheme.TextDimmer, TextAnchor.LowerRight);
        ver.resizeTextForBestFit = false;
        ver.horizontalOverflow = HorizontalWrapMode.Overflow;
        RectTransform vr = ver.rectTransform;
        vr.anchorMin = vr.anchorMax = new Vector2(1f, 0f);
        vr.pivot = new Vector2(1f, 0f);
        vr.anchoredPosition = new Vector2(-24f, 18f);
        vr.sizeDelta = new Vector2(360f, 18f);
    }

    /// <summary>One glyph segment of the LIT-ISO wordmark (Press Start 2P 88px,
    /// hard 4px black shadow), sized to its own text so the row stays tight.</summary>
    private void AddWordmarkGlyph(Transform parent, string name, string content, Color color)
    {
        Text t = CreateText(name, parent, content, 88, color, TextAnchor.MiddleLeft);
        t.resizeTextForBestFit = false;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        var le = t.gameObject.AddComponent<LayoutElement>();
        le.preferredWidth = MeasureTextWidth(t);
        le.preferredHeight = 88f;
        // The shared 4px hard shadow (design: text-shadow:4px 4px 0 #000).
        var sh = t.GetComponent<Shadow>() ?? t.gameObject.AddComponent<Shadow>();
        sh.effectColor = LitIsoTheme.Base;
        sh.effectDistance = new Vector2(4f, -4f);
        sh.useGraphicAlpha = true;
    }

    /// <summary>Anchors a rect to the top-left of its parent column at a given
    /// downward offset (y is 0 at the column top, negative going down).</summary>
    private static void PlaceTopLeft(RectTransform rt, float width, float height, float y)
    {
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(0f, y);
        rt.sizeDelta = new Vector2(width, height);
    }

    /// <summary>A main-menu title button: left-aligned stone bar, 2px hard
    /// border + inset stone bevel, with a small gold diamond bullet and the
    /// label in the body face. Hover turns it gold (handled by TitleButtonHover).
    /// Faithfully matches the HTML menuButtons styling.</summary>
    private void CreateTitleMenuButton(string name, Transform parent, string text, System.Action onClick,
        float width, float height, float y)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        RectTransform rt = go.GetComponent<RectTransform>();
        // Design: min-width:300px, left-aligned. We use the full column width.
        PlaceTopLeft(rt, Mathf.Max(300f, width), height, y);

        Image bg = go.AddComponent<Image>();
        bg.color = LitIsoTheme.Hex("#15171C");            // background:rgba(21,23,28,.78)
        bg.color = new Color(bg.color.r, bg.color.g, bg.color.b, 0.78f);

        // border:2px solid #0a0b0e
        Outline border = go.AddComponent<Outline>();
        border.effectColor = LitIsoTheme.Base;
        border.effectDistance = new Vector2(2f, -2f);
        border.useGraphicAlpha = false;

        // box-shadow:inset 0 0 0 2px #2c313c → a stone bevel frame inside the border.
        GameObject bevelGO = new GameObject("Bevel", typeof(RectTransform));
        bevelGO.transform.SetParent(rt, false);
        RectTransform bevel = bevelGO.GetComponent<RectTransform>();
        bevel.anchorMin = Vector2.zero; bevel.anchorMax = Vector2.one;
        bevel.offsetMin = new Vector2(2f, 2f); bevel.offsetMax = new Vector2(-2f, -2f);
        Image bevelImg = bevelGO.AddComponent<Image>();
        bevelImg.color = Color.clear;
        bevelImg.raycastTarget = false;
        var bevelOutline = bevelGO.AddComponent<Outline>();
        bevelOutline.effectColor = LitIsoTheme.Stone;
        bevelOutline.effectDistance = new Vector2(2f, 2f);
        bevelOutline.useGraphicAlpha = false;

        Button button = go.AddComponent<Button>();
        button.targetGraphic = bg;
        button.transition = Selectable.Transition.None; // hover handled manually
        button.onClick.AddListener(() => onClick?.Invoke());

        // Diamond bullet — 10px, rotated 45°, currentColor (text colour), opacity .8.
        GameObject bulletGO = new GameObject("Bullet", typeof(RectTransform));
        bulletGO.transform.SetParent(rt, false);
        RectTransform bullet = bulletGO.GetComponent<RectTransform>();
        bullet.anchorMin = new Vector2(0f, 0.5f);
        bullet.anchorMax = new Vector2(0f, 0.5f);
        bullet.pivot = new Vector2(0.5f, 0.5f);
        bullet.anchoredPosition = new Vector2(22f + 5f, 0f); // padding-left:22 + half its width
        bullet.sizeDelta = new Vector2(10f, 10f);
        bullet.localRotation = Quaternion.Euler(0f, 0f, 45f);
        Image bulletImg = bulletGO.AddComponent<Image>();
        Color bulletColor = LitIsoTheme.Parchment; bulletColor.a = 0.8f;
        bulletImg.color = bulletColor;
        bulletImg.raycastTarget = false;

        // Label — Pixelify Sans, 22px, left-aligned after the bullet.
        // NOTE: CreateText would route size>=19 to Press Start 2P (too wide for
        // button labels). We explicitly override to the body face after creation.
        Text label = CreateText("Text", rt, text, 22, LitIsoTheme.Parchment, TextAnchor.MiddleLeft);
        LitIsoTheme.ApplyBody(label, 22, LitIsoTheme.Parchment);  // force Pixelify Sans
        label.raycastTarget = false;
        label.resizeTextForBestFit = false;
        label.horizontalOverflow = HorizontalWrapMode.Wrap;
        label.verticalOverflow = VerticalWrapMode.Overflow;
        RectTransform lr = label.rectTransform;
        lr.anchorMin = new Vector2(0f, 0f); lr.anchorMax = new Vector2(1f, 1f);
        lr.offsetMin = new Vector2(22f + 14f + 10f, 0f); // padding-left + bullet + gap:14
        lr.offsetMax = new Vector2(-30f, 0f);            // padding-right:30

        // Hover: gold fill + dark text + gold bevel + glow (style-hover in HTML).
        var hover = go.AddComponent<TitleButtonHover>();
        hover.Configure(bg, bevelOutline, label, bulletImg, rt);
    }

    /// <summary>Hover behaviour for the title menu buttons — pointer-enter turns
    /// the bar gold with dark text (mirrors the HTML style-hover), pointer-exit
    /// restores the stone rest state. Pure visual; never touches onClick.</summary>
    private sealed class TitleButtonHover : MonoBehaviour,
        UnityEngine.EventSystems.IPointerEnterHandler, UnityEngine.EventSystems.IPointerExitHandler
    {
        private Image _bg, _bullet;
        private Outline _bevel;
        private Text _label;
        private RectTransform _rt;
        private Color _restBg, _restBevel, _restLabel, _restBullet;
        private Vector2 _restPos;

        public void Configure(Image bg, Outline bevel, Text label, Image bullet, RectTransform rt)
        {
            _bg = bg; _bevel = bevel; _label = label; _bullet = bullet; _rt = rt;
            _restBg = bg.color; _restBevel = bevel.effectColor;
            _restLabel = label.color; _restBullet = bullet.color;
            _restPos = rt.anchoredPosition;
        }

        public void OnPointerEnter(UnityEngine.EventSystems.PointerEventData e)
        {
            if (_bg == null) return;
            _bg.color = LitIsoTheme.Gold;                       // background:#E8C468
            _bevel.effectColor = LitIsoTheme.GoldLit;           // bevel:#f5d98a
            Color dark = LitIsoTheme.GoldText;                  // text:#1a1408
            _label.color = dark;
            Color b = dark; b.a = 0.8f; _bullet.color = b;
            _rt.anchoredPosition = _restPos + new Vector2(3f, 0f); // style-active translateX(3px)
        }

        public void OnPointerExit(UnityEngine.EventSystems.PointerEventData e)
        {
            if (_bg == null) return;
            _bg.color = _restBg;
            _bevel.effectColor = _restBevel;
            _label.color = _restLabel;
            _bullet.color = _restBullet;
            _rt.anchoredPosition = _restPos;
        }
    }

    /// <summary>Transparent full-screen container used by screens (like the Title)
    /// that sit directly on the background art rather than inside a stone card.
    /// Still serves as contentPanel so the fade-in + Destroy lifecycle works.</summary>
    private RectTransform CreateTransparentContainer(string name)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(mainCanvas.transform, false);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        return rt;
    }

    /// <summary>
    /// Natural single-line width of a label at its design font size. Best-fit and
    /// wrapping are disabled during measurement so the box can be sized to the
    /// text rather than the text shrinking into the box.
    /// </summary>
    private static float MeasureTextWidth(Text text)
    {
        bool fit = text.resizeTextForBestFit;
        HorizontalWrapMode wrap = text.horizontalOverflow;
        text.resizeTextForBestFit = false;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        float width = text.preferredWidth;
        text.resizeTextForBestFit = fit;
        text.horizontalOverflow = wrap;
        return width;
    }

    // -------------------------------------------------------------------------
    // Create World
    // -------------------------------------------------------------------------

    private void BuildCreateWorld()
    {
        // ====================================================================
        // CREATE WORLD — transcribed from the front-end design (isCreate).
        // A centred 760px stone card with gold corner studs, padded 46×54,
        // containing: heading + subtitle, WORLD NAME input, SEED input, a
        // 3-segment difficulty selector with a live label + description, and a
        // PLAY / BACK action row. All inputs/callbacks preserved verbatim.
        // ====================================================================
        contentPanel = CreateTransparentContainer("CreateWorld");

        const float cardW = 760f;
        const float padX = 54f;     // padding:46px 54px
        const float padTop = 46f;
        const float innerW = cardW - 2f * padX; // 652

        // The centred card.
        GameObject cardGO = new GameObject("Card", typeof(RectTransform));
        cardGO.transform.SetParent(contentPanel, false);
        RectTransform card = cardGO.GetComponent<RectTransform>();
        card.anchorMin = new Vector2(0.5f, 0.5f);
        card.anchorMax = new Vector2(0.5f, 0.5f);
        card.pivot = new Vector2(0.5f, 0.5f);
        Image cardBg = cardGO.AddComponent<Image>();
        cardBg.color = LitIsoTheme.Panel;                 // background:#15171C

        // border:3px solid #0a0b0e
        var cardBorder = cardGO.AddComponent<Outline>();
        cardBorder.effectColor = LitIsoTheme.Base;
        cardBorder.effectDistance = new Vector2(3f, -3f);
        cardBorder.useGraphicAlpha = false;

        // box-shadow: inset 2px #2c313c (stone bevel) + inset 5px #5a4d2a (gold line).
        AddInsetFrame(card, LitIsoTheme.Stone, 3f, 2f);     // stone bevel just inside the border
        AddInsetFrame(card, LitIsoTheme.GoldDeep, 5f, 1f);  // thin gold inner line

        // Four gold corner studs (14×14) at the card's corners.
        AddCornerStud(card, new Vector2(0f, 1f), new Vector2(-3f, 3f));   // top-left
        AddCornerStud(card, new Vector2(1f, 1f), new Vector2(3f, 3f));    // top-right
        AddCornerStud(card, new Vector2(0f, 0f), new Vector2(-3f, -3f));  // bottom-left
        AddCornerStud(card, new Vector2(1f, 0f), new Vector2(3f, -3f));   // bottom-right

        // Content cursor: 0 at the inner top, increasing downward (we negate it
        // when placing rects). All content lives inside the padded inner column.
        float y = padTop;

        // Heading — "CREATE WORLD" (Press Start 2P 30, gold, 3px shadow).
        Text heading = CreateInnerText(card, "Heading", "CREATE WORLD", 30, goldAccent,
            TextAnchor.LowerLeft, padX, innerW, 34f, y);
        y += 34f + 6f;                                     // margin:0 0 6px

        // Subtitle — 20px #8a8578, margin-bottom:34px.
        Text subtitle = CreateInnerText(card, "Subtitle", "Forge a new realm from earth and ember.",
            20, LitIsoTheme.WarmTan, TextAnchor.UpperLeft, padX, innerW, 26f, y);
        subtitle.resizeTextForBestFit = false;
        y += 26f + 34f;

        // ---- WORLD NAME ----------------------------------------------------
        CreateFieldLabel(card, "NameLabel", "WORLD NAME", padX, innerW, y);
        y += 16f + 10f;                                    // 12px caps + margin-bottom:10
        worldNameInput = CreateThemedInput("WorldNameInput", card, "Name your realm…",
            padX, innerW, y);
        y += 52f;                                          // input height (14+24+14)

        // ---- SEED ----------------------------------------------------------
        y += 22f;                                          // label margin-top:22
        CreateFieldLabel(card, "SeedLabel", "SEED", padX, innerW, y);
        y += 16f + 10f;
        seedInput = CreateThemedInput("SeedInput", card, "blank = random", padX, innerW, y);
        y += 52f;

        // ---- DIFFICULTY ----------------------------------------------------
        y += 30f;                                          // row margin-top:30
        // Header row: "DIFFICULTY" on the left, the live colour-coded label on the right.
        CreateFieldLabel(card, "DifficultyLabel", "DIFFICULTY", padX, innerW, y);
        difficultyValueLabel = CreateInnerText(card, "DifficultyValue", "", 14, goldAccent,
            TextAnchor.LowerRight, padX, innerW, 16f, y);
        difficultyValueLabel.resizeTextForBestFit = false;
        difficultyValueLabel.horizontalOverflow = HorizontalWrapMode.Overflow;
        y += 16f + 12f;                                    // margin-bottom:12

        BuildDifficultySelector(card, padX, innerW, y);
        y += 50f;                                          // segment row height
        y += 14f;                                          // desc margin-top:14
        // The description label height (min-height:48px) is set inside the selector.
        y += 48f;

        // ---- ACTIONS -------------------------------------------------------
        y += 30f;                                          // actions margin-top:30
        const float actionH = 58f;                         // padding:18px 0 over a caps line
        // BACK is fixed-width on the right; PLAY flexes to fill the rest.
        const float backW = 170f;                          // padding:18 34 over "BACK"
        const float gap = 16f;
        float playW = innerW - backW - gap;
        CreateThemedActionButton("PlayBtn", card, "PLAY ▸", OnCreateWorldPlay,
            padX, playW, actionH, y, gold: true);
        CreateThemedActionButton("BackBtn", card, "BACK", () => ShowScreen(Screen.MainMenu),
            padX + playW + gap, backW, actionH, y, gold: false);
        y += actionH;

        // Finalise card height = padded content span (+ bottom padding).
        float cardH = y + padTop;
        card.sizeDelta = new Vector2(cardW, cardH);

        // Reflect the initial selection into the highlight + description + value.
        SelectDifficulty(selectedDifficulty);
    }

    /// <summary>A thin inset border frame inside the card, used to reproduce the
    /// HTML's stacked inset box-shadows (stone bevel + gold inner line).</summary>
    private static void AddInsetFrame(RectTransform parent, Color color, float inset, float thickness)
    {
        GameObject go = new GameObject("Inset", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(inset, inset); rt.offsetMax = new Vector2(-inset, -inset);
        Image img = go.AddComponent<Image>();
        img.color = Color.clear;
        img.raycastTarget = false;
        var o = go.AddComponent<Outline>();
        o.effectColor = color;
        o.effectDistance = new Vector2(thickness, -thickness);
        o.useGraphicAlpha = false;
        go.transform.SetAsFirstSibling(); // behind content
    }

    /// <summary>One of the four 14×14 gold corner studs on the Create World card.</summary>
    private void AddCornerStud(RectTransform card, Vector2 anchor, Vector2 offset)
    {
        GameObject go = new GameObject("CornerStud", typeof(RectTransform));
        go.transform.SetParent(card, false);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = anchor; rt.anchorMax = anchor; rt.pivot = anchor;
        rt.anchoredPosition = offset;
        rt.sizeDelta = new Vector2(14f, 14f);
        Image img = go.AddComponent<Image>();
        img.color = goldAccent;
        img.raycastTarget = false;
    }

    /// <summary>Places a text inside the card's padded inner column at a given
    /// downward offset (y measured from the inner top, positive going down).</summary>
    private Text CreateInnerText(RectTransform card, string name, string content, int size,
        Color color, TextAnchor anchor, float padX, float innerW, float height, float y)
    {
        Text t = CreateText(name, card, content, size, color, anchor);
        RectTransform rt = t.rectTransform;
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(padX, -y);
        rt.sizeDelta = new Vector2(innerW, height);
        return t;
    }

    /// <summary>A field label (Press Start 2P 12px, +2 tracking, warm tan).</summary>
    private void CreateFieldLabel(RectTransform card, string name, string text, float padX, float innerW, float y)
    {
        Text t = CreateInnerText(card, name, text, 12, LitIsoTheme.WarmTan, TextAnchor.LowerLeft,
            padX, innerW, 16f, y);
        t.resizeTextForBestFit = false;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
    }

    private void OnCreateWorldPlay()
    {
        string worldName = worldNameInput.text;
        if (string.IsNullOrWhiteSpace(worldName))
        {
            worldName = "Untitled World";
        }

        string seed = seedInput.text;
        if (string.IsNullOrWhiteSpace(seed))
        {
            seed = Random.Range(0, 999999).ToString();
        }

        int difficulty = selectedDifficulty;

        WorldSaveData world = new WorldSaveData
        {
            worldName = worldName,
            seed = seed,
            difficulty = difficulty,
            createdTicks = System.DateTime.Now.Ticks
        };

        if (!SaveWorld(world))
        {
            // Save failed (e.g. disk/permissions) — surface it instead of launching a
            // world that won't appear in Load Game.
            Debug.LogError("World not created: save failed. Not launching.");
            return;
        }

        // Transmigration opening (owner-approved progression rework, 2026-06-10):
        // new characters start CLASSLESS. The first 7 in-game days are the trial;
        // class selection happens in-world at day 7 (Class Selection Instance).
        // The CallingSelect screen is intentionally kept out of this flow — its
        // card UI will be recycled as the day-7 class offer screen.
        //
        // Character creator (owner request 2026-06-12): appearance is chosen
        // before the world loads. Confirm persists via LayeredAppearance.Save,
        // then the trial launches.
        if (contentPanel != null) contentPanel.gameObject.SetActive(false);
        LitIso.CharacterCreator.CharacterCreatorUI.Show(
            _ => LaunchWorld(world, null),
            onCancelled: () =>
            {
                // BACK/ESC from the creator (review 2026-07-03): return to the menu
                // instead of stranding the player. The world was already saved, so it
                // remains available under Load Game.
                if (contentPanel != null) contentPanel.gameObject.SetActive(true);
            });
    }

    /// <summary>Soft 0.22s fade-in for whichever screen was just built — menu
    /// navigation feels intentional instead of snapping.</summary>
    private System.Collections.IEnumerator FadeInNewScreen()
    {
        yield return null;   // wait for the new contentPanel to be built
        if (contentPanel == null) yield break;
        var cg = contentPanel.gameObject.GetComponent<CanvasGroup>()
                 ?? contentPanel.gameObject.AddComponent<CanvasGroup>();
        for (float a = 0f; a < 1f; a += Time.unscaledDeltaTime / 0.22f)
        {
            if (cg == null) yield break;
            cg.alpha = a;
            yield return null;
        }
        if (cg != null) cg.alpha = 1f;
    }

    /// <summary>Builds the three-segment Easy/Normal/Hard difficulty selector plus the
    /// description line beneath it, matching the front-end design (replaces the slider).</summary>
    /// <summary>Builds the 3-segment Easy/Normal/Hard selector and the description
    /// line beneath it, matching the front-end design. The selection logic is
    /// unchanged (SelectDifficulty); only the layout/styling tracks the HTML.</summary>
    private void BuildDifficultySelector(RectTransform card, float padX, float innerW, float y)
    {
        diffSegmentCards.Clear();

        const float rowH = 50f;             // padding:14px 0 over a 22px line
        float segWidth = innerW / DifficultyOptions.Length;

        // The segmented row: border:2px #0a0b0e + inset 2px #2c313c bevel.
        GameObject row = new GameObject("DifficultyRow", typeof(RectTransform));
        row.transform.SetParent(card, false);
        RectTransform rowRect = row.GetComponent<RectTransform>();
        rowRect.anchorMin = new Vector2(0f, 1f);
        rowRect.anchorMax = new Vector2(0f, 1f);
        rowRect.pivot = new Vector2(0f, 1f);
        rowRect.anchoredPosition = new Vector2(padX, -y);
        rowRect.sizeDelta = new Vector2(innerW, rowH);
        Image rowBgImg = row.AddComponent<Image>();
        rowBgImg.color = LitIsoTheme.Base;             // shows through the 2px gaps as the border
        rowBgImg.raycastTarget = false;
        var rowBevel = row.AddComponent<Outline>();
        rowBevel.effectColor = LitIsoTheme.Stone;
        rowBevel.effectDistance = new Vector2(2f, -2f);
        rowBevel.useGraphicAlpha = false;

        for (int i = 0; i < DifficultyOptions.Length; i++)
        {
            int idx = i;
            var opt = DifficultyOptions[i];

            GameObject seg = new GameObject("Diff_" + opt.label, typeof(RectTransform));
            seg.transform.SetParent(rowRect, false);
            RectTransform segRect = seg.GetComponent<RectTransform>();
            segRect.anchorMin = new Vector2(0f, 0f);
            segRect.anchorMax = new Vector2(0f, 1f);
            segRect.pivot = new Vector2(0f, 0.5f);
            // border-right:2px #0a0b0e between segments → inset each by 2px on the
            // right (last segment included; reads as a thin inset, matches design).
            segRect.anchoredPosition = new Vector2(i * segWidth + 2f, 0f);
            segRect.offsetMin = new Vector2(i * segWidth + 2f, 2f);
            segRect.offsetMax = new Vector2(i * segWidth + 2f, -2f);
            segRect.sizeDelta = new Vector2(segWidth - (i == DifficultyOptions.Length - 1 ? 4f : 2f), -4f);

            Image segBg = seg.AddComponent<Image>();
            segBg.color = LitIsoTheme.Panel;           // unselected fill

            Button segBtn = seg.AddComponent<Button>();
            segBtn.targetGraphic = segBg;
            segBtn.transition = Selectable.Transition.None;
            segBtn.onClick.AddListener(() => SelectDifficulty(idx));

            // Pixelify Sans 600, 22px, centred.
            Text segLabel = CreateText("Label", segRect, opt.label, 22, labelText, TextAnchor.MiddleCenter);
            segLabel.raycastTarget = false;
            segLabel.resizeTextForBestFit = false;
            segLabel.horizontalOverflow = HorizontalWrapMode.Overflow;
            RectTransform slr = segLabel.rectTransform;
            slr.anchorMin = Vector2.zero; slr.anchorMax = Vector2.one;
            slr.offsetMin = Vector2.zero; slr.offsetMax = Vector2.zero;

            diffSegmentCards.Add((idx, segBg, segLabel));
        }

        // Description line — 19px #a39e90, min-height 48px, margin-top 14.
        difficultyDescLabel = CreateText("DifficultyDesc", card, DifficultyOptions[selectedDifficulty].desc,
            19, LitIsoTheme.Hex("#a39e90"), TextAnchor.UpperLeft);
        difficultyDescLabel.horizontalOverflow = HorizontalWrapMode.Wrap;
        difficultyDescLabel.resizeTextForBestFit = false;
        RectTransform descRect = difficultyDescLabel.rectTransform;
        descRect.anchorMin = new Vector2(0f, 1f);
        descRect.anchorMax = new Vector2(0f, 1f);
        descRect.pivot = new Vector2(0f, 1f);
        descRect.anchoredPosition = new Vector2(padX, -(y + rowH + 14f));
        descRect.sizeDelta = new Vector2(innerW, 48f);
    }

    /// <summary>Selects a difficulty segment, updating the highlight, the colour-
    /// coded value label, and the description. Selection logic preserved.</summary>
    private void SelectDifficulty(int idx)
    {
        selectedDifficulty = Mathf.Clamp(idx, 0, DifficultyOptions.Length - 1);
        var selOpt = DifficultyOptions[selectedDifficulty];
        for (int i = 0; i < diffSegmentCards.Count; i++)
        {
            var seg = diffSegmentCards[i];
            bool sel = seg.idx == selectedDifficulty;
            var opt = DifficultyOptions[seg.idx];
            if (seg.bg != null) seg.bg.color = sel ? opt.color : LitIsoTheme.Panel;
            if (seg.label != null) seg.label.color = sel ? LitIsoTheme.GoldText : labelText;
        }
        if (difficultyValueLabel != null)
        {
            difficultyValueLabel.text = selOpt.label.ToUpperInvariant();
            difficultyValueLabel.color = selOpt.color;
        }
        if (difficultyDescLabel != null)
            difficultyDescLabel.text = selOpt.desc;
    }

    // -------------------------------------------------------------------------
    // Calling Select  (New Game -> pick a Calling -> launch)
    // -------------------------------------------------------------------------

    private void BuildCallingSelect()
    {
        // ====================================================================
        // CHOOSE YOUR CALLING — pixel-art class-selection card (Phase 1C).
        // Centred 900px stone card with the same border grammar as Create World.
        // A vertically scrollable list of calling cards fills the interior;
        // each card shows a class icon placeholder, name/title row, coloured
        // stat bars, and a description. Selection highlights with a gold outline.
        //
        // NOTE: In the normal New-Game flow, OnCreateWorldPlay() bypasses this
        // screen (classless trial — 2026-06-10). The screen is kept for the
        // day-7 class-offer path via ClassAssignmentView; pendingWorld may be
        // null when reached from the main menu's CallingSelect case directly.
        // ====================================================================
        callingCards.Clear();
        selectedCallingId = null;

        contentPanel = CreateTransparentContainer("CallingSelect");

        const float cardW  = 900f;
        const float padX   = 54f;
        const float padTop = 46f;
        const float innerW = cardW - 2f * padX; // 792

        // ---- Centred stone card --------------------------------------------
        GameObject cardGO = new GameObject("Card", typeof(RectTransform));
        cardGO.transform.SetParent(contentPanel, false);
        RectTransform card = cardGO.GetComponent<RectTransform>();
        card.anchorMin = new Vector2(0.5f, 0.5f);
        card.anchorMax = new Vector2(0.5f, 0.5f);
        card.pivot     = new Vector2(0.5f, 0.5f);
        Image cardBg = cardGO.AddComponent<Image>();
        cardBg.color = LitIsoTheme.Panel;
        var cardBorder = cardGO.AddComponent<Outline>();
        cardBorder.effectColor = LitIsoTheme.Base;
        cardBorder.effectDistance = new Vector2(3f, -3f);
        cardBorder.useGraphicAlpha = false;
        AddInsetFrame(card, LitIsoTheme.Stone, 3f, 2f);
        AddInsetFrame(card, LitIsoTheme.GoldDeep, 5f, 1f);
        AddCornerStud(card, new Vector2(0f, 1f), new Vector2(-3f,  3f));
        AddCornerStud(card, new Vector2(1f, 1f), new Vector2( 3f,  3f));
        AddCornerStud(card, new Vector2(0f, 0f), new Vector2(-3f, -3f));
        AddCornerStud(card, new Vector2(1f, 0f), new Vector2( 3f, -3f));

        float y = padTop;

        // Heading + subtitle
        CreateInnerText(card, "Heading", "CHOOSE YOUR CALLING", 26, goldAccent,
            TextAnchor.LowerLeft, padX, innerW, 30f, y);
        y += 30f + 8f;
        CreateInnerText(card, "Subtitle",
            "Each Calling sets your starting gifts. Every skill remains open to you.",
            18, LitIsoTheme.WarmTan, TextAnchor.UpperLeft, padX, innerW, 24f, y);
        y += 24f + 16f;

        // Gold hairline divider
        GameObject divGO = new GameObject("Divider", typeof(RectTransform));
        divGO.transform.SetParent(card, false);
        RectTransform divRT = divGO.GetComponent<RectTransform>();
        divRT.anchorMin = new Vector2(0f, 1f); divRT.anchorMax = new Vector2(0f, 1f);
        divRT.pivot = new Vector2(0f, 1f);
        divRT.anchoredPosition = new Vector2(padX, -y);
        divRT.sizeDelta = new Vector2(innerW, 1f);
        Image divImg = divGO.AddComponent<Image>();
        divImg.color = new Color(LitIsoTheme.GoldDeep.r, LitIsoTheme.GoldDeep.g,
                                 LitIsoTheme.GoldDeep.b, 0.6f);
        divImg.raycastTarget = false;
        y += 1f + 16f;

        // ---- Scrollable calling list (420 px tall) --------------------------
        const float listH = 420f;
        GameObject scrollGO = new GameObject("CallingScroll", typeof(RectTransform));
        scrollGO.transform.SetParent(card, false);
        RectTransform scrollRT = scrollGO.GetComponent<RectTransform>();
        scrollRT.anchorMin = new Vector2(0f, 1f); scrollRT.anchorMax = new Vector2(0f, 1f);
        scrollRT.pivot = new Vector2(0f, 1f);
        scrollRT.anchoredPosition = new Vector2(padX, -y);
        scrollRT.sizeDelta = new Vector2(innerW, listH);
        Image scrollBg = scrollGO.AddComponent<Image>();
        scrollBg.color = new Color(0f, 0f, 0f, 0.15f);
        scrollBg.raycastTarget = false;

        ScrollRect scroll = scrollGO.AddComponent<ScrollRect>();
        scroll.vertical = true; scroll.horizontal = false;
        scroll.movementType = ScrollRect.MovementType.Clamped;

        GameObject vpGO = new GameObject("Viewport", typeof(RectTransform));
        vpGO.transform.SetParent(scrollRT, false);
        RectTransform vpRT = vpGO.GetComponent<RectTransform>();
        vpRT.anchorMin = Vector2.zero; vpRT.anchorMax = Vector2.one;
        vpRT.offsetMin = Vector2.zero; vpRT.offsetMax = Vector2.zero;
        vpGO.AddComponent<Image>().color = Color.clear;
        vpGO.AddComponent<RectMask2D>();

        GameObject listContentGO = new GameObject("Content", typeof(RectTransform));
        listContentGO.transform.SetParent(vpRT, false);
        RectTransform listContent = listContentGO.GetComponent<RectTransform>();
        listContent.anchorMin = new Vector2(0f, 1f);
        listContent.anchorMax = new Vector2(1f, 1f);
        listContent.pivot = new Vector2(0.5f, 1f);
        listContent.offsetMin = new Vector2(8f, 0f);
        listContent.offsetMax = new Vector2(-8f, 0f);
        listContent.sizeDelta = new Vector2(0f, 0f);

        var vlg = listContentGO.AddComponent<VerticalLayoutGroup>();
        vlg.spacing = 8f;
        vlg.padding = new RectOffset(0, 0, 8, 8);
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;
        vlg.childControlWidth = true;
        vlg.childControlHeight = true;
        listContentGO.AddComponent<ContentSizeFitter>().verticalFit =
            ContentSizeFitter.FitMode.PreferredSize;

        scroll.content = listContent;
        scroll.viewport = vpRT;

        var callings = FoundationContent.BuildDefault().Callings.All;
        for (int i = 0; i < callings.Count; i++)
            BuildCallingCard(listContent, callings[i]);

        if (callings.Count > 0)
            SelectCallingCard(callings[0].id);

        y += listH + 24f;

        // ---- Action row: CONFIRM (gold) + BACK (stone) ---------------------
        const float actionH  = 56f;
        const float backW    = 170f;
        const float gap      = 14f;
        float confirmW = innerW - backW - gap;
        CreateThemedActionButton("ConfirmBtn", card, "CONFIRM ▸", () =>
        {
            if (pendingWorld != null && !string.IsNullOrEmpty(selectedCallingId))
                LaunchWorld(pendingWorld, selectedCallingId);
        }, padX, confirmW, actionH, y, gold: true);
        CreateThemedActionButton("BackBtn", card, "BACK",
            () => ShowScreen(Screen.CreateWorld),
            padX + confirmW + gap, backW, actionH, y, gold: false);
        y += actionH;

        card.sizeDelta = new Vector2(cardW, y + padTop);
    }

    private void BuildCallingCard(Transform parent, FoundationCallingDefinition calling)
    {
        // Stone card: Panel2 fill, 2px ink border, stone bevel inset.
        // Selected state: Panel fill (slightly lighter) + gold border.
        GameObject cardGO = new GameObject("Calling_" + calling.id, typeof(RectTransform));
        cardGO.transform.SetParent(parent, false);

        Image cardBg = cardGO.AddComponent<Image>();
        cardBg.color = LitIsoTheme.Panel2;

        Outline cardBorder = cardGO.AddComponent<Outline>();
        cardBorder.effectColor = LitIsoTheme.Base;
        cardBorder.effectDistance = new Vector2(2f, -2f);
        cardBorder.useGraphicAlpha = false;

        // Inset stone bevel — same grammar as title buttons.
        GameObject bevelGO = new GameObject("Bevel", typeof(RectTransform));
        bevelGO.transform.SetParent(cardGO.transform, false);
        RectTransform bevelRT = bevelGO.GetComponent<RectTransform>();
        bevelRT.anchorMin = Vector2.zero; bevelRT.anchorMax = Vector2.one;
        bevelRT.offsetMin = new Vector2(2f, 2f); bevelRT.offsetMax = new Vector2(-2f, -2f);
        Image bevelImg = bevelGO.AddComponent<Image>();
        bevelImg.color = Color.clear; bevelImg.raycastTarget = false;
        var bevelOut = bevelGO.AddComponent<Outline>();
        bevelOut.effectColor = LitIsoTheme.Stone;
        bevelOut.effectDistance = new Vector2(1f, -1f);
        bevelOut.useGraphicAlpha = false;

        Button btn = cardGO.AddComponent<Button>();
        btn.targetGraphic = cardBg;
        btn.transition = Selectable.Transition.None;
        string id = calling.id;
        btn.onClick.AddListener(() => SelectCallingCard(id));

        callingCards.Add((id, cardBg, cardBorder));

        var le = cardGO.AddComponent<LayoutElement>();
        le.minHeight = 144f; le.preferredHeight = 144f;

        RectTransform cardRT = cardGO.GetComponent<RectTransform>();

        // ---- Left icon column (72px) ----------------------------------------
        // A dark-panel strip with a rotated diamond placeholder; replace with a
        // real class icon (76×76) at Resources/UI/Menu/Icon_<callingId>.
        GameObject iconColGO = new GameObject("IconCol", typeof(RectTransform));
        iconColGO.transform.SetParent(cardRT, false);
        RectTransform iconCol = iconColGO.GetComponent<RectTransform>();
        iconCol.anchorMin = new Vector2(0f, 0f); iconCol.anchorMax = new Vector2(0f, 1f);
        iconCol.pivot = new Vector2(0f, 0.5f);
        iconCol.anchoredPosition = Vector2.zero;
        iconCol.sizeDelta = new Vector2(72f, 0f);
        Image iconColBg = iconColGO.AddComponent<Image>();
        iconColBg.color = LitIsoTheme.Panel;
        iconColBg.raycastTarget = false;

        // Try to load a real icon sprite, fall back to a gold diamond shape.
        Sprite iconSpr = Resources.Load<Sprite>("UI/Menu/Icon_" + calling.id);
        if (iconSpr != null)
        {
            Image iconImg = new GameObject("Icon", typeof(RectTransform)).AddComponent<Image>();
            iconImg.transform.SetParent(iconCol, false);
            iconImg.sprite = iconSpr;
            iconImg.color = Color.white;
            iconImg.raycastTarget = false;
            RectTransform iconRT = iconImg.rectTransform;
            iconRT.anchorMin = iconRT.anchorMax = new Vector2(0.5f, 0.5f);
            iconRT.pivot = new Vector2(0.5f, 0.5f);
            iconRT.anchoredPosition = Vector2.zero;
            iconRT.sizeDelta = new Vector2(48f, 48f);
        }
        else
        {
            GameObject diaGO = new GameObject("Icon", typeof(RectTransform));
            diaGO.transform.SetParent(iconCol, false);
            RectTransform dia = diaGO.GetComponent<RectTransform>();
            dia.anchorMin = dia.anchorMax = new Vector2(0.5f, 0.5f);
            dia.pivot = new Vector2(0.5f, 0.5f);
            dia.anchoredPosition = Vector2.zero;
            dia.sizeDelta = new Vector2(30f, 30f);
            dia.localRotation = Quaternion.Euler(0f, 0f, 45f);
            Image diaImg = diaGO.AddComponent<Image>();
            diaImg.color = new Color(LitIsoTheme.GoldDeep.r, LitIsoTheme.GoldDeep.g,
                                     LitIsoTheme.GoldDeep.b, 0.65f);
            diaImg.raycastTarget = false;
        }

        // ---- Content area (right of icon column) ----------------------------
        GameObject contentGO = new GameObject("Content", typeof(RectTransform));
        contentGO.transform.SetParent(cardRT, false);
        RectTransform contentRT = contentGO.GetComponent<RectTransform>();
        contentRT.anchorMin = Vector2.zero; contentRT.anchorMax = Vector2.one;
        contentRT.offsetMin = new Vector2(76f, 0f); contentRT.offsetMax = new Vector2(-10f, 0f);

        // Name row: class name (left, gold 20px display) + starting title (right, WarmTan 12px).
        Text nameText = CreateText("Name", contentRT, calling.Display, 20, goldAccent, TextAnchor.UpperLeft);
        nameText.raycastTarget = false;
        RectTransform nameRT = nameText.rectTransform;
        nameRT.anchorMin = new Vector2(0f, 1f); nameRT.anchorMax = new Vector2(1f, 1f);
        nameRT.pivot = new Vector2(0f, 1f);
        nameRT.offsetMin = new Vector2(0f, -32f); nameRT.offsetMax = new Vector2(-160f, -8f);

        Text titleText = CreateText("Title", contentRT, calling.startingTitle,
            12, LitIsoTheme.WarmTan, TextAnchor.UpperRight);
        titleText.raycastTarget = false;
        RectTransform titleRT = titleText.rectTransform;
        titleRT.anchorMin = new Vector2(0f, 1f); titleRT.anchorMax = new Vector2(1f, 1f);
        titleRT.pivot = new Vector2(1f, 1f);
        titleRT.offsetMin = new Vector2(-160f, -30f); titleRT.offsetMax = new Vector2(0f, -10f);

        // 1px stone rule under the name row.
        GameObject rowDivGO = new GameObject("NameRule", typeof(RectTransform));
        rowDivGO.transform.SetParent(contentRT, false);
        RectTransform rowDivRT = rowDivGO.GetComponent<RectTransform>();
        rowDivRT.anchorMin = new Vector2(0f, 1f); rowDivRT.anchorMax = new Vector2(1f, 1f);
        rowDivRT.pivot = new Vector2(0f, 1f);
        rowDivRT.offsetMin = new Vector2(0f, -37f); rowDivRT.offsetMax = new Vector2(0f, -36f);
        Image rowDivImg = rowDivGO.AddComponent<Image>();
        rowDivImg.color = LitIsoTheme.Stone; rowDivImg.raycastTarget = false;

        // Stat bars — one bar per bonus, up to 5, laid out in a horizontal row.
        if (calling.statBonuses != null && calling.statBonuses.Length > 0)
        {
            const float barW   = 100f;
            const float barGap = 12f;
            float statX = 0f;
            for (int i = 0; i < Mathf.Min(calling.statBonuses.Length, 5); i++)
            {
                var b = calling.statBonuses[i];
                BuildStatBar(contentRT, b.stat.ToString(), b.amount, statX, -42f, barW);
                statX += barW + barGap;
            }
        }

        // Description — wraps in the lower half of the card.
        Text desc = CreateText("Desc", contentRT, calling.description,
            12, new Color(0.75f, 0.78f, 0.84f, 1f), TextAnchor.UpperLeft);
        desc.raycastTarget = false;
        desc.horizontalOverflow = HorizontalWrapMode.Wrap;
        RectTransform descRT = desc.rectTransform;
        descRT.anchorMin = new Vector2(0f, 0f); descRT.anchorMax = new Vector2(1f, 1f);
        descRT.offsetMin = new Vector2(0f, 10f); descRT.offsetMax = new Vector2(0f, -82f);
    }

    /// <summary>Compact coloured stat bar — label (STAT +N) above a 6px track.
    /// Used inside a calling card to visualise a single stat bonus.</summary>
    private void BuildStatBar(Transform parent, string stat, int amount, float x, float y, float width)
    {
        GameObject go = new GameObject("Stat_" + stat, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 1f); rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(x, y);
        rt.sizeDelta = new Vector2(width, 36f);

        // Label: "STR  +5" in body 11px WarmTan
        string sign = amount >= 0 ? "+" : "";
        Text label = CreateText("Label", rt,
            $"{stat.ToUpperInvariant()}  {sign}{amount}", 11, LitIsoTheme.WarmTan, TextAnchor.UpperLeft);
        label.raycastTarget = false;
        label.horizontalOverflow = HorizontalWrapMode.Overflow;
        RectTransform lr = label.rectTransform;
        lr.anchorMin = new Vector2(0f, 1f); lr.anchorMax = new Vector2(1f, 1f);
        lr.pivot = new Vector2(0f, 1f);
        lr.offsetMin = new Vector2(0f, -16f); lr.offsetMax = Vector2.zero;

        // Track (stone background)
        GameObject track = new GameObject("Track", typeof(RectTransform));
        track.transform.SetParent(rt, false);
        RectTransform trackRT = track.GetComponent<RectTransform>();
        trackRT.anchorMin = new Vector2(0f, 0f); trackRT.anchorMax = new Vector2(0f, 0f);
        trackRT.pivot = new Vector2(0f, 1f);
        trackRT.anchoredPosition = new Vector2(0f, -20f);
        trackRT.sizeDelta = new Vector2(width, 6f);
        Image trackImg = track.AddComponent<Image>();
        trackImg.color = LitIsoTheme.Stone;
        trackImg.raycastTarget = false;

        // Fill — width proportional to amount (up to 10 = 100% fill), colour by stat.
        float fillFrac = Mathf.Clamp01(Mathf.Abs(amount) / 10f);
        if (fillFrac > 0f)
        {
            GameObject fill = new GameObject("Fill", typeof(RectTransform));
            fill.transform.SetParent(trackRT, false);
            RectTransform fillRT = fill.GetComponent<RectTransform>();
            fillRT.anchorMin = Vector2.zero; fillRT.anchorMax = new Vector2(fillFrac, 1f);
            fillRT.offsetMin = Vector2.zero; fillRT.offsetMax = Vector2.zero;
            Image fillImg = fill.AddComponent<Image>();
            fillImg.color = StatBarColor(stat);
            fillImg.raycastTarget = false;
        }
    }

    private static Color StatBarColor(string stat)
    {
        if (stat == null) return LitIsoTheme.Gold;
        switch (stat.ToUpperInvariant())
        {
            case "STR": return LitIsoTheme.Red;
            case "DEX": return LitIsoTheme.Green;
            case "INT": return LitIsoTheme.RarityRare;
            case "VIT": return LitIsoTheme.Amber;
            case "DEF": return LitIsoTheme.RarityUncommon;
            case "LUCK": return LitIsoTheme.RarityEpic;
            default:    return LitIsoTheme.Gold;
        }
    }

    private void SelectCallingCard(string id)
    {
        selectedCallingId = id;
        foreach (var card in callingCards)
        {
            bool sel = card.id == id;
            if (card.bg != null)
                card.bg.color = sel ? LitIsoTheme.Panel : LitIsoTheme.Panel2;
            if (card.outline != null)
            {
                card.outline.effectColor = sel ? LitIsoTheme.Gold : LitIsoTheme.Base;
                card.outline.effectDistance = new Vector2(sel ? 2.5f : 2f, sel ? -2.5f : -2f);
            }
        }
    }

    private static string StatBonusLine(FoundationCallingDefinition calling)
    {
        // Kept as a utility; not used by BuildCallingCard (which now renders visual bars).
        if (calling.statBonuses == null || calling.statBonuses.Length == 0) return "";
        var parts = new List<string>();
        foreach (var b in calling.statBonuses)
            parts.Add($"{b.stat} +{b.amount}");
        return string.Join("    ", parts);
    }

    // -------------------------------------------------------------------------
    // Load Game
    // -------------------------------------------------------------------------

    private void BuildLoadGame()
    {
        contentPanel = CreateMainPanel("LoadGame");

        Text title = CreateText("LoadTitle", contentPanel, "Load World", 40, goldAccent, TextAnchor.MiddleCenter);
        RectTransform titleRect = title.rectTransform;
        titleRect.anchorMin = new Vector2(0.5f, 1f);
        titleRect.anchorMax = new Vector2(0.5f, 1f);
        titleRect.pivot = new Vector2(0.5f, 1f);
        titleRect.anchoredPosition = new Vector2(0f, -20f);
        titleRect.sizeDelta = new Vector2(panelWidth - 40f, 50f);

        // Scroll list of worlds
        worldListContent = CreateScrollList("WorldList", contentPanel, -80f);

        RefreshWorldList();

        // Back button
        CreateMenuButton("BackBtn", contentPanel, "Back", () => ShowScreen(Screen.MainMenu), 0f, -panelHeight + 50f);
    }

    private void RefreshWorldList()
    {
        savedWorlds.Clear();
        if (Directory.Exists(savePath))
        {
            string[] files = Directory.GetFiles(savePath, "*.world.json");
            foreach (string file in files)
            {
                try
                {
                    string json = File.ReadAllText(file);
                    WorldSaveData world = JsonUtility.FromJson<WorldSaveData>(json);
                    savedWorlds.Add(world);
                }
                catch { }
            }
        }

        // Sort by creation time descending (newest first)
        savedWorlds.Sort((a, b) => b.createdTicks.CompareTo(a.createdTicks));

        if (worldListContent != null)
        {
            foreach (Transform child in worldListContent)
            {
                Destroy(child.gameObject);
            }

            if (savedWorlds.Count == 0)
            {
                Text noWorlds = CreateText("NoWorlds", worldListContent, "No saved worlds yet", 20, buttonText, TextAnchor.MiddleCenter);
                RectTransform noRect = noWorlds.rectTransform;
                noRect.sizeDelta = new Vector2(panelWidth - 80f, 50f);
            }
            else
            {
                for (int i = 0; i < savedWorlds.Count; i++)
                {
                    WorldSaveData world = savedWorlds[i];
                    RectTransform entry = CreatePanel("Entry", worldListContent, buttonBg, panelBorder);
                    entry.sizeDelta = new Vector2(panelWidth - 80f, 60f);

                    // Richer row: name, seed, difficulty, and whether a Foundation
                    // save exists (so "Play" = resume vs fresh is visible upfront).
                    string diff = world.difficulty switch { 0 => "Easy", 2 => "Hard", _ => "Normal" };
                    bool hasSave = File.Exists(
                        FoundationBootstrap.DefaultSavePathForWorld(world.worldName, world.seed));
                    string rowLabel = $"{world.worldName}   ·   seed {world.seed}   ·   {diff}"
                                      + (hasSave ? "   ·   saved" : "   ·   new");
                    Text entryText = CreateText("Name", entry, rowLabel, 16, buttonText, TextAnchor.MiddleLeft);
                    RectTransform entryTextRect = entryText.rectTransform;
                    entryTextRect.anchorMin = new Vector2(0f, 0.5f);
                    entryTextRect.anchorMax = new Vector2(1f, 0.5f);
                    entryTextRect.pivot = new Vector2(0f, 0.5f);
                    entryTextRect.offsetMin = new Vector2(12f, 0f);
                    entryTextRect.offsetMax = new Vector2(-12f, 0f);

                    int capturedIdx = i;
                    CreateSmallButton("PlayBtn", entry, "Play", () => LaunchWorld(savedWorlds[capturedIdx]), -60f, 0f);
                    // Two-step delete: first click arms ("Sure?"), second within
                    // 3s actually deletes — instant world deletion was dangerous.
                    bool armed = pendingDeleteIndex == i
                                 && Time.realtimeSinceStartup - pendingDeleteTime < 3f;
                    CreateSmallButton("DeleteBtn", entry, armed ? "Sure?" : "Delete", () =>
                    {
                        if (pendingDeleteIndex == capturedIdx
                            && Time.realtimeSinceStartup - pendingDeleteTime < 3f)
                        {
                            pendingDeleteIndex = -1;
                            DeleteWorld(capturedIdx);
                        }
                        else
                        {
                            pendingDeleteIndex = capturedIdx;
                            pendingDeleteTime = Time.realtimeSinceStartup;
                            RefreshWorldList();
                        }
                    }, 0f, 0f);
                }
            }
        }
    }

    private void DeleteWorld(int index)
    {
        if (index >= 0 && index < savedWorlds.Count)
        {
            WorldSaveData world = savedWorlds[index];
            string file = Path.Combine(savePath, world.filename);
            if (File.Exists(file))
            {
                File.Delete(file);
            }
            // Also remove the Foundation save folder, or deleted worlds leave
            // orphaned save.json data behind forever (2026-06-11 audit).
            try
            {
                string foundationSave = FoundationBootstrap.DefaultSavePathForWorld(world.worldName, world.seed);
                string folder = Path.GetDirectoryName(foundationSave);
                if (!string.IsNullOrEmpty(folder) && Directory.Exists(folder)
                    && folder.StartsWith(Application.persistentDataPath))
                    Directory.Delete(folder, true);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"Could not remove Foundation save folder for '{world.worldName}': {e.Message}");
            }
            RefreshWorldList();
        }
    }

    // -------------------------------------------------------------------------
    // Options
    // -------------------------------------------------------------------------

    // PlayerPrefs keys — kept in sync with PauseMenu.cs
    const string kMaster = "vol_master";
    const string kSfx    = "vol_sfx";
    const string kMusic  = "vol_music";

    private void BuildOptions()
    {
        contentPanel = CreateMainPanel("Options");

        Text title = CreateText("OptionsTitle", contentPanel, "Options", 40, goldAccent, TextAnchor.MiddleCenter);
        RectTransform titleRect = title.rectTransform;
        titleRect.anchorMin = new Vector2(0.5f, 1f);
        titleRect.anchorMax = new Vector2(0.5f, 1f);
        titleRect.pivot     = new Vector2(0.5f, 1f);
        titleRect.anchoredPosition = new Vector2(0f, -20f);
        titleRect.sizeDelta = new Vector2(panelWidth - 40f, 50f);

        float y      = -90f;
        float rowGap = buttonHeight + spacing + 28f;

        // Master volume
        CreateLabel("MasterLabel", contentPanel, "Master Volume", y);
        y -= 24f;
        Slider masterSlider = CreateSlider("MasterSlider", contentPanel, y, 0f, 1f,
            PlayerPrefs.GetFloat(kMaster, 1f));
        y -= rowGap;
        masterSlider.onValueChanged.AddListener(v =>
        {
            PlayerPrefs.SetFloat(kMaster, v);
            AudioListener.volume = v;
        });

        // SFX volume
        CreateLabel("SfxLabel", contentPanel, "SFX Volume", y);
        y -= 24f;
        Slider sfxSlider = CreateSlider("SfxSlider", contentPanel, y, 0f, 1f,
            PlayerPrefs.GetFloat(kSfx, 1f));
        y -= rowGap;
        sfxSlider.onValueChanged.AddListener(v => PlayerPrefs.SetFloat(kSfx, v));

        // Music volume
        CreateLabel("MusicLabel", contentPanel, "Music Volume", y);
        y -= 24f;
        Slider musicSlider = CreateSlider("MusicSlider", contentPanel, y, 0f, 1f,
            PlayerPrefs.GetFloat(kMusic, 0.6f));
        musicSlider.onValueChanged.AddListener(v => PlayerPrefs.SetFloat(kMusic, v));

        // Apply master immediately so the player can hear the change
        AudioListener.volume = PlayerPrefs.GetFloat(kMaster, 1f);

        CreateMenuButton("BackBtn", contentPanel, "Back", () =>
        {
            PlayerPrefs.Save();
            ShowScreen(Screen.MainMenu);
        }, 0f, -panelHeight + 50f);
    }

    // -------------------------------------------------------------------------
    // World Save/Load
    // -------------------------------------------------------------------------

    /// <summary>Returns the most recently played world, or null if none exist.</summary>
    private WorldSaveData GetMostRecentWorld()
    {
        if (!Directory.Exists(savePath)) return null;
        WorldSaveData best = null;
        foreach (string file in Directory.GetFiles(savePath, "*.world.json"))
        {
            try
            {
                var w = JsonUtility.FromJson<WorldSaveData>(File.ReadAllText(file));
                if (best == null || w.createdTicks > best.createdTicks) best = w;
            }
            catch { }
        }
        return best;
    }

    private bool SaveWorld(WorldSaveData world)
    {
        try
        {
            EnsureSaveFolder();
            string json = JsonUtility.ToJson(world, true);
            string filepath = Path.Combine(savePath, world.filename);
            File.WriteAllText(filepath, json);
            Debug.Log($"Saved world metadata: {filepath}");
            return true;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Failed to save world '{world.worldName}': {e.Message}");
            return false;
        }
    }

    private void LaunchWorld(WorldSaveData world, string callingId = null)
    {
        Debug.Log($"Launching world: {world.worldName} (Seed: {world.seed}, Difficulty: {world.difficulty}, Calling: {callingId ?? "default"})");

        string foundationSavePath = FoundationBootstrap.DefaultSavePathForWorld(world.worldName, world.seed);
        bool isExistingWorldLaunch = string.IsNullOrWhiteSpace(callingId);

        if (isExistingWorldLaunch && File.Exists(foundationSavePath))
        {
            Debug.Log($"Loading Foundation save: {foundationSavePath}");
            // A corrupted save must fall back to a fresh seed launch instead of
            // crashing the menu (2026-06-11 audit).
            try
            {
                FoundationBootstrap.ConfigureLoad(foundationSavePath);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Save load failed for '{world.worldName}' — launching fresh from seed. {e.Message}");
                FoundationBootstrap.ConfigureLaunch(world.worldName, world.seed, world.difficulty, callingId);
            }
        }
        else
        {
            if (isExistingWorldLaunch)
                Debug.LogWarning($"No Foundation save found for '{world.worldName}'. Launching fresh from seed. Expected: {foundationSavePath}");

            // Hand the world settings into the isolated Foundation scene (Codex's API).
            // ConfigureLaunch must be called BEFORE LoadScene so FoundationBootstrap.Awake()
            // picks up the seed/name/difficulty/calling. callingId is null for
            // Continue/Load fallback; the New Game flow passes the picked Calling.
            FoundationBootstrap.ConfigureLaunch(world.worldName, world.seed, world.difficulty, callingId);
        }

        // First entry into a brand-new world: play the transmigration boot-up
        // cutscene after the scene loads (owner spec — disoriented arrival).
        if (!File.Exists(foundationSavePath))
            TransmigrationIntro.Arm();

        // Load the Foundation scene (canonical game) behind a fade + tip screen.
        LoadingScreen.Go("IsoCoreFoundation", $"Entering {world.worldName}…");
    }

    private void LaunchCreationInstance()
    {
        Debug.Log("Launching Creation Instance showroom.");
        FoundationBootstrap.ConfigureCreationInstanceLaunch();
        LoadingScreen.Go("IsoCoreFoundation", "Entering the Creation Instance…");
    }

    // -------------------------------------------------------------------------
    // UI Building Helpers
    // -------------------------------------------------------------------------

    private void BuildCanvas()
    {
        GameObject canvasGO = new GameObject("WelcomeScreenCanvas");
        mainCanvas = canvasGO.AddComponent<Canvas>();
        mainCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        CanvasScaler scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        canvasGO.AddComponent<GraphicRaycaster>();
        transform.SetParent(canvasGO.transform, false);

        // Resolve the background sprite. Prefer the inspector-assigned one, but fall
        // back to loading from Resources so the menu ALWAYS has art with zero manual
        // wiring (Resources/UI/CampfireMenu.png).
        Sprite bg = backgroundImage != null
            ? backgroundImage
            : Resources.Load<Sprite>("UI/CampfireMenu");

        if (bg != null)
        {
            GameObject bgGO = new GameObject("Background", typeof(RectTransform));
            bgGO.transform.SetParent(mainCanvas.transform, false);
            bgGO.transform.SetAsFirstSibling();  // Behind everything
            RectTransform bgRT = bgGO.GetComponent<RectTransform>();
            bgRT.anchorMin = new Vector2(0.5f, 0.5f);
            bgRT.anchorMax = new Vector2(0.5f, 0.5f);
            bgRT.pivot = new Vector2(0.5f, 0.5f);
            bgRT.anchoredPosition = Vector2.zero;

            Image bgImage = bgGO.AddComponent<Image>();
            bgImage.sprite = bg;
            bgImage.type = Image.Type.Simple;
            bgImage.color = Color.white;
            bgImage.raycastTarget = false;

            // Cover-fit: fill the screen while preserving aspect (crops the overflow),
            // so the splash never stretches/distorts at any resolution or aspect ratio.
            AspectRatioFitter fitter = bgGO.AddComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            fitter.aspectRatio = (float)bg.rect.width / Mathf.Max(1f, bg.rect.height);

            // Cinematic treatment (owner request): slow Ken Burns drift on the art
            // plus a dark scrim so menu text stays readable over any background.
            bgGO.AddComponent<MenuCinematicBackground>();
            // Keep the old flipbook disabled here: background_frames currently
            // belong to the previous menu image, while this scene is animated by
            // procedural light/particles until a matching frame loop is authored.

            GameObject scrimGO = new GameObject("BackgroundScrim", typeof(RectTransform));
            scrimGO.transform.SetParent(mainCanvas.transform, false);
            scrimGO.transform.SetSiblingIndex(1); // above background, below content
            RectTransform scrimRT = scrimGO.GetComponent<RectTransform>();
            scrimRT.anchorMin = Vector2.zero;
            scrimRT.anchorMax = Vector2.one;
            scrimRT.offsetMin = Vector2.zero;
            scrimRT.offsetMax = Vector2.zero;
            Image scrim = scrimGO.AddComponent<Image>();
            scrim.color = new Color(0.02f, 0.03f, 0.05f, 0.45f);
            scrim.raycastTarget = false;

            // Subtle living light over the landscape: sunrise wash from the left
            // horizon and a warm campfire pulse at the foreground fire.
            GameObject lightingGO = new GameObject("SceneLighting", typeof(RectTransform));
            lightingGO.transform.SetParent(mainCanvas.transform, false);
            lightingGO.transform.SetSiblingIndex(2);
            lightingGO.AddComponent<MenuSceneLighting>();

            // Ambient particle layers (embers / fireflies / stars) — above the
            // scrim so they read bright against it, below all menu content.
            GameObject ambientGO = new GameObject("AmbientParticles", typeof(RectTransform));
            ambientGO.transform.SetParent(mainCanvas.transform, false);
            ambientGO.transform.SetSiblingIndex(3);
            RectTransform ambientRT = (RectTransform)ambientGO.transform;
            ambientRT.anchorMin = Vector2.zero;
            ambientRT.anchorMax = Vector2.one;
            ambientRT.offsetMin = Vector2.zero;
            ambientRT.offsetMax = Vector2.zero;
            ambientGO.AddComponent<MenuAmbientParticles>();

            // version tag, bottom-right corner
            Text ver = CreateText("Version", mainCanvas.transform,
                $"LIT-ISO v{Application.version}", 12,
                new Color(1f, 1f, 1f, 0.35f), TextAnchor.LowerRight);
            RectTransform vr = ver.rectTransform;
            vr.anchorMin = vr.anchorMax = new Vector2(1f, 0f);
            vr.pivot = new Vector2(1f, 0f);
            vr.anchoredPosition = new Vector2(-12f, 8f);
            vr.sizeDelta = new Vector2(300f, 20f);
        }
        else
        {
            // No art found — paint a dark fallback so the menu still reads cleanly.
            GameObject bgGO = new GameObject("BackgroundFallback", typeof(RectTransform));
            bgGO.transform.SetParent(mainCanvas.transform, false);
            bgGO.transform.SetAsFirstSibling();
            RectTransform bgRT = bgGO.GetComponent<RectTransform>();
            bgRT.anchorMin = Vector2.zero;
            bgRT.anchorMax = Vector2.one;
            bgRT.offsetMin = Vector2.zero;
            bgRT.offsetMax = Vector2.zero;
            Image bgImage = bgGO.AddComponent<Image>();
            bgImage.color = new Color(0.06f, 0.07f, 0.10f, 1f);
        }
    }

    private RectTransform CreateMainPanel(string name)
    {
        RectTransform rt = CreatePanel(name, mainCanvas.transform, panelBg, panelBorder);
        // Owner request: menu lives on the LEFT so the animated campfire scene
        // (fire sits center-right) stays visible and unobstructed.
        rt.anchorMin = new Vector2(0f, 0.5f);
        rt.anchorMax = new Vector2(0f, 0.5f);
        rt.pivot = new Vector2(0f, 0.5f);
        rt.anchoredPosition = new Vector2(24f, 0f);
        rt.sizeDelta = new Vector2(panelWidth, panelHeight);
        return rt;
    }

    private RectTransform CreatePanel(string name, Transform parent, Color bg, Color border)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        RectTransform rt = go.GetComponent<RectTransform>();

        Image bgImage = go.AddComponent<Image>();
        if (panelImage != null)
        {
            // Skinned: 9-sliced frame art (set proper Border in the sprite import).
            bgImage.sprite = panelImage;
            bgImage.type = Image.Type.Sliced;
            bgImage.color = Color.white;
        }
        else
        {
            // Procedural fallback: flat fill + 1px outline.
            bgImage.color = bg;
            Outline outline = go.AddComponent<Outline>();
            outline.effectColor = border;
            outline.effectDistance = new Vector2(1.5f, -1.5f);
        }

        return rt;
    }

    private RectTransform CreateMenuButton(string name, Transform parent, string text, System.Action onClick, float x, float y, bool gold = false)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 1f);
        rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(x, y);
        rt.sizeDelta = new Vector2(200f, buttonHeight);

        Image bgImage = go.AddComponent<Image>();
        Button button = go.AddComponent<Button>();
        button.targetGraphic = bgImage;
        button.onClick.AddListener(() => onClick?.Invoke());

        if (buttonImage != null)
        {
            // Skinned button: 9-sliced art, sprite-swap on hover/press if a hover art exists.
            bgImage.sprite = buttonImage;
            bgImage.type = Image.Type.Sliced;
            bgImage.color = Color.white;
            if (buttonHoverImage != null)
            {
                button.transition = Selectable.Transition.SpriteSwap;
                button.spriteState = new SpriteState
                {
                    highlightedSprite = buttonHoverImage,
                    pressedSprite = buttonHoverImage,
                    selectedSprite = buttonHoverImage
                };
            }
        }
        else
        {
            // No sprite skin: use the shared design-system button (gold for the
            // primary action, stone otherwise) with the 5px hard bottom shadow
            // + 4px press offset.
            LitIsoTheme.StyleButton(button, bgImage,
                gold ? LitIsoTheme.ButtonStyle.Gold : LitIsoTheme.ButtonStyle.Stone);
        }

        Text btnText = CreateText("Text", rt, text, 20,
            (buttonImage == null && gold) ? LitIsoTheme.GoldText : this.buttonText,
            TextAnchor.MiddleCenter);
        RectTransform textRect = btnText.rectTransform;
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;
        return rt;
    }

    private void CreateSmallButton(string name, Transform parent, string text, System.Action onClick, float x, float y)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(1f, 0.5f);
        rt.anchorMax = new Vector2(1f, 0.5f);
        rt.pivot = new Vector2(1f, 0.5f);
        rt.anchoredPosition = new Vector2(x, y);
        rt.sizeDelta = new Vector2(50f, 40f);

        Image bgImage = go.AddComponent<Image>();
        bgImage.color = buttonBg;

        Button button = go.AddComponent<Button>();
        button.targetGraphic = bgImage;
        button.onClick.AddListener(() => onClick?.Invoke());

        ColorBlock colors = button.colors;
        colors.normalColor = buttonBg;
        colors.highlightedColor = buttonBgHover;
        button.colors = colors;

        Text btnText = CreateText("Text", rt, text, 14, this.buttonText, TextAnchor.MiddleCenter);
        RectTransform textRect = btnText.rectTransform;
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;
    }

    private void CreateLabel(string name, Transform parent, string text, float y)
    {
        Text label = CreateText(name, parent, text, 16, labelText, TextAnchor.MiddleLeft);
        RectTransform labelRect = label.rectTransform;
        labelRect.anchorMin = new Vector2(0f, 1f);
        labelRect.anchorMax = new Vector2(0f, 1f);
        labelRect.pivot = new Vector2(0f, 1f);
        labelRect.anchoredPosition = new Vector2(20f, y);
        labelRect.sizeDelta = new Vector2(panelWidth - 40f, 24f);
        label.color = labelText;
    }

    private InputField CreateInputField(string name, Transform parent, string placeholder, float y)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 1f);
        rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(0f, y);
        rt.sizeDelta = new Vector2(panelWidth - 40f, buttonHeight);

        Image bgImage = go.AddComponent<Image>();
        bgImage.color = inputBg;

        InputField inputField = go.AddComponent<InputField>();
        inputField.targetGraphic = bgImage;
        inputField.textComponent = CreateText("Text", rt, "", 18, inputText, TextAnchor.MiddleLeft);
        inputField.textComponent.resizeTextForBestFit = false;
        RectTransform textRect = inputField.textComponent.rectTransform;
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(10f, 0f);
        textRect.offsetMax = new Vector2(-10f, 0f);
        inputField.textComponent.color = inputText;

        Text placeholderText = CreateText("Placeholder", inputField.textComponent.transform,
            placeholder, 18, new Color(0.5f, 0.5f, 0.5f, 0.5f), TextAnchor.MiddleLeft);
        placeholderText.resizeTextForBestFit = false;
        RectTransform placeholderRect = placeholderText.rectTransform;
        placeholderRect.anchorMin = Vector2.zero;
        placeholderRect.anchorMax = Vector2.one;
        placeholderRect.offsetMin = new Vector2(10f, 0f);
        placeholderRect.offsetMax = new Vector2(-10f, 0f);
        inputField.placeholder = placeholderText;

        return inputField;
    }

    /// <summary>A Create-World text input styled to the HTML: dark #0e1014 fill,
    /// 2px stone border, inset top shadow, Pixelify Sans 600 24px parchment text,
    /// 14x16 padding. Anchored top-left inside the card's padded column.</summary>
    private InputField CreateThemedInput(string name, RectTransform card, string placeholder,
        float padX, float innerW, float y)
    {
        const float h = 52f;
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(card, false);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(padX, -y);
        rt.sizeDelta = new Vector2(innerW, h);

        Image bgImage = go.AddComponent<Image>();
        bgImage.color = LitIsoTheme.Hex("#0e1014");

        var border = go.AddComponent<Outline>();
        border.effectColor = LitIsoTheme.Stone;
        border.effectDistance = new Vector2(2f, -2f);
        border.useGraphicAlpha = false;

        InputField inputField = go.AddComponent<InputField>();
        inputField.targetGraphic = bgImage;

        Text textComp = CreateText("Text", rt, "", 24, LitIsoTheme.ParchmentLit, TextAnchor.MiddleLeft);
        textComp.resizeTextForBestFit = false;
        textComp.horizontalOverflow = HorizontalWrapMode.Overflow;
        textComp.supportRichText = false;
        RectTransform textRect = textComp.rectTransform;
        textRect.anchorMin = Vector2.zero; textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(16f, 0f); textRect.offsetMax = new Vector2(-16f, 0f);
        inputField.textComponent = textComp;

        Text ph = CreateText("Placeholder", inputField.textComponent.transform, placeholder,
            24, new Color(LitIsoTheme.WarmTan.r, LitIsoTheme.WarmTan.g, LitIsoTheme.WarmTan.b, 0.55f),
            TextAnchor.MiddleLeft);
        ph.resizeTextForBestFit = false;
        ph.horizontalOverflow = HorizontalWrapMode.Overflow;
        RectTransform phRect = ph.rectTransform;
        phRect.anchorMin = Vector2.zero; phRect.anchorMax = Vector2.one;
        phRect.offsetMin = new Vector2(16f, 0f); phRect.offsetMax = new Vector2(-16f, 0f);
        inputField.placeholder = ph;

        return inputField;
    }

    /// <summary>A Create-World action button (PLAY / BACK / CONFIRM). Press Start 2P 18px,
    /// gold or stone fill, 2px hard border, inset bevel, 5px hard bottom shadow.</summary>
    private void CreateThemedActionButton(string name, RectTransform card, string text,
        System.Action onClick, float x, float width, float height, float y, bool gold)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(card, false);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(x, -y);
        rt.sizeDelta = new Vector2(width, height);

        Image bg = go.AddComponent<Image>();
        Button button = go.AddComponent<Button>();
        LitIsoTheme.StyleButton(button, bg,
            gold ? LitIsoTheme.ButtonStyle.Gold : LitIsoTheme.ButtonStyle.Stone);
        button.onClick.AddListener(() => onClick?.Invoke());

        Text label = CreateText("Text", rt, text, 18,
            gold ? LitIsoTheme.GoldText : LitIsoTheme.Parchment, TextAnchor.MiddleCenter);
        label.raycastTarget = false;
        RectTransform lr = label.rectTransform;
        lr.anchorMin = Vector2.zero; lr.anchorMax = Vector2.one;
        lr.offsetMin = Vector2.zero; lr.offsetMax = Vector2.zero;
    }

    private Slider CreateSlider(string name, Transform parent, float y, float minVal, float maxVal, float initialVal)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 1f);
        rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(0f, y);
        rt.sizeDelta = new Vector2(panelWidth - 80f, 30f);

        Image bgImage = go.AddComponent<Image>();
        bgImage.color = inputBg;

        Slider slider = go.AddComponent<Slider>();
        slider.fillRect = null;
        slider.handleRect = CreateHandle(slider.transform).GetComponent<RectTransform>();
        slider.direction = Slider.Direction.LeftToRight;
        slider.minValue = minVal;
        slider.maxValue = maxVal;
        slider.value = initialVal;

        return slider;
    }

    private GameObject CreateHandle(Transform parent)
    {
        GameObject handle = new GameObject("Handle", typeof(RectTransform));
        handle.transform.SetParent(parent, false);
        RectTransform rt = handle.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(20f, 30f);

        Image img = handle.AddComponent<Image>();
        img.color = new Color(0.98f, 0.85f, 0.45f, 1f);

        return handle;
    }

    private RectTransform CreateScrollList(string name, Transform parent, float y)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 1f);
        rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(0f, y);
        rt.sizeDelta = new Vector2(panelWidth - 40f, panelHeight - 200f);

        Image bgImage = go.AddComponent<Image>();
        bgImage.color = new Color(0.04f, 0.05f, 0.07f, 0.8f);

        ScrollRect scroll = go.AddComponent<ScrollRect>();
        scroll.vertical = true;
        scroll.horizontal = false;

        GameObject viewport = new GameObject("Viewport", typeof(RectTransform));
        viewport.transform.SetParent(rt, false);
        RectTransform vpRect = viewport.GetComponent<RectTransform>();
        vpRect.anchorMin = Vector2.zero;
        vpRect.anchorMax = Vector2.one;
        vpRect.offsetMin = Vector2.zero;
        vpRect.offsetMax = Vector2.zero;
        Image vpMask = viewport.AddComponent<Image>();
        vpMask.color = Color.clear;
        viewport.AddComponent<RectMask2D>();

        GameObject content = new GameObject("Content", typeof(RectTransform));
        content.transform.SetParent(vpRect, false);
        RectTransform contentRect = content.GetComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0.5f, 1f);
        contentRect.anchorMax = new Vector2(0.5f, 1f);
        contentRect.pivot = new Vector2(0.5f, 1f);
        contentRect.sizeDelta = new Vector2(panelWidth - 80f, 0f);

        VerticalLayoutGroup layout = content.AddComponent<VerticalLayoutGroup>();
        layout.spacing = spacing;
        layout.childForceExpandHeight = false;
        layout.childControlHeight = true;
        layout.childControlWidth = true;

        ContentSizeFitter fitter = content.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        scroll.content = contentRect;
        scroll.viewport = vpRect;

        return contentRect;
    }

    private Text CreateText(string name, Transform parent, string content, int size, Color textColor, TextAnchor anchor)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        Text text = go.AddComponent<Text>();
        text.text = content;
        text.color = textColor;
        text.alignment = anchor;
        if (size >= 19) LitIsoTheme.ApplyDisplay(text, size, textColor);
        else            LitIsoTheme.ApplyBody(text, size, textColor);
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Truncate;
        text.resizeTextForBestFit = true;
   