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

    [Header("Layout")]
    public float panelWidth = 500f;
    public float panelHeight = 600f;
    public float buttonHeight = 50f;
    public float spacing = 12f;

    [Header("Palette")]
    public Color panelBg = new Color(0.07f, 0.09f, 0.13f, 0.95f);
    public Color panelBorder = new Color(0.30f, 0.36f, 0.42f, 1f);
    public Color buttonBg = new Color(0.15f, 0.18f, 0.24f, 0.9f);
    public Color buttonBgHover = new Color(0.22f, 0.26f, 0.34f, 1f);
    public Color buttonText = new Color(0.95f, 0.91f, 0.74f, 1f);
    public Color inputBg = new Color(0.05f, 0.06f, 0.09f, 0.9f);
    public Color inputText = new Color(0.90f, 0.90f, 0.90f, 1f);
    public Color labelText = new Color(0.75f, 0.75f, 0.75f, 1f);

    private Screen currentScreen = Screen.MainMenu;
    private Canvas mainCanvas;
    private RectTransform contentPanel;

    // World creation data
    private InputField worldNameInput;
    private InputField seedInput;
    private Slider difficultySlider;
    private Text difficultyLabel;

    // Load game data
    private List<WorldSaveData> savedWorlds = new List<WorldSaveData>();
    private RectTransform worldListContent;

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
        contentPanel = CreateMainPanel("MainMenu");

        if (logoImage != null)
        {
            // Logo art provided → use it as the title wordmark.
            GameObject logoGO = new GameObject("Logo", typeof(RectTransform));
            logoGO.transform.SetParent(contentPanel, false);
            RectTransform logoRect = logoGO.GetComponent<RectTransform>();
            logoRect.anchorMin = new Vector2(0.5f, 1f);
            logoRect.anchorMax = new Vector2(0.5f, 1f);
            logoRect.pivot = new Vector2(0.5f, 1f);
            logoRect.anchoredPosition = new Vector2(0f, -24f);
            float aspect = (float)logoImage.rect.width / Mathf.Max(1f, logoImage.rect.height);
            logoRect.sizeDelta = new Vector2(logoHeight * aspect, logoHeight);

            Image logo = logoGO.AddComponent<Image>();
            logo.sprite = logoImage;
            logo.preserveAspect = true;
            logo.color = Color.white;
        }
        else
        {
            // No logo art → fall back to a styled text title.
            Text title = CreateText("Title", contentPanel, "LIT-ISO", 52, buttonText, TextAnchor.MiddleCenter);
            RectTransform titleRect = title.rectTransform;
            titleRect.anchorMin = new Vector2(0.5f, 1f);
            titleRect.anchorMax = new Vector2(0.5f, 1f);
            titleRect.pivot = new Vector2(0.5f, 1f);
            titleRect.anchoredPosition = new Vector2(0f, -40f);
            titleRect.sizeDelta = new Vector2(panelWidth - 40f, 60f);
        }

        float buttonY = -120f;
        float buttonSpacing = buttonHeight + spacing;

        // Continue — only shown when at least one saved world exists.
        WorldSaveData mostRecent = GetMostRecentWorld();
        if (mostRecent != null)
        {
            CreateMenuButton("ContinueBtn", contentPanel, "Continue", () => LaunchWorld(mostRecent), 0f, buttonY);
            buttonY -= buttonSpacing;
        }

        CreateMenuButton("NewGameBtn",  contentPanel, "New Game",  () => ShowScreen(Screen.CreateWorld), 0f, buttonY);
        CreateMenuButton("CreationInstanceBtn", contentPanel, "Creation Instance", LaunchCreationInstance, 0f, buttonY - buttonSpacing);
        CreateMenuButton("LoadGameBtn", contentPanel, "Load Game", () => ShowScreen(Screen.LoadGame),    0f, buttonY - 2 * buttonSpacing);
        CreateMenuButton("OptionsBtn",  contentPanel, "Options",   () => ShowScreen(Screen.Options),     0f, buttonY - 3 * buttonSpacing);
        CreateMenuButton("QuitBtn",     contentPanel, "Quit",      () => Application.Quit(),             0f, buttonY - 4 * buttonSpacing);
    }

    // -------------------------------------------------------------------------
    // Create World
    // -------------------------------------------------------------------------

    private void BuildCreateWorld()
    {
        contentPanel = CreateMainPanel("CreateWorld");

        float y = -30f;

        // World Name
        CreateLabel("NameLabel", contentPanel, "World Name", y);
        y -= 24f;
        worldNameInput = CreateInputField("WorldNameInput", contentPanel, "Enter world name...", y);
        y -= buttonHeight + spacing + 12f;

        // Seed
        CreateLabel("SeedLabel", contentPanel, "Seed (leave blank for random)", y);
        y -= 24f;
        seedInput = CreateInputField("SeedInput", contentPanel, "e.g. 12345", y);
        y -= buttonHeight + spacing + 12f;

        // Difficulty
        CreateLabel("DifficultyLabel", contentPanel, "Difficulty", y);
        y -= 24f;
        difficultySlider = CreateSlider("DifficultySlider", contentPanel, y, 0f, 2f, 1f);
        difficultyLabel = CreateText("DifficultyValue", contentPanel, "Normal", 18, labelText, TextAnchor.MiddleCenter);
        RectTransform diffLabelRect = difficultyLabel.rectTransform;
        diffLabelRect.anchorMin = new Vector2(0.5f, 0f);
        diffLabelRect.anchorMax = new Vector2(0.5f, 0f);
        diffLabelRect.pivot = new Vector2(0.5f, 0f);
        diffLabelRect.anchoredPosition = new Vector2(0f, -buttonHeight - spacing * 2);
        diffLabelRect.sizeDelta = new Vector2(panelWidth - 40f, 24f);

        difficultySlider.onValueChanged.AddListener(UpdateDifficultyLabel);
        UpdateDifficultyLabel(1f);

        // Buttons
        float btnY = -panelHeight + 60f;
        CreateMenuButton("PlayBtn", contentPanel, "Play", OnCreateWorldPlay, -80f, btnY);
        CreateMenuButton("BackBtn", contentPanel, "Back", () => ShowScreen(Screen.MainMenu), 80f, btnY);
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

        int difficulty = Mathf.RoundToInt(difficultySlider.value);

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
        LaunchWorld(world, null);
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

    private void UpdateDifficultyLabel(float value)
    {
        int difficulty = Mathf.RoundToInt(value);
        string label = difficulty switch
        {
            0 => "Easy",
            1 => "Normal",
            2 => "Hard",
            _ => "Unknown"
        };
        if (difficultyLabel != null)
        {
            difficultyLabel.text = label;
        }
    }

    // -------------------------------------------------------------------------
    // Calling Select  (New Game -> pick a Calling -> launch)
    // -------------------------------------------------------------------------

    private void BuildCallingSelect()
    {
        callingCards.Clear();
        selectedCallingId = null;

        contentPanel = CreateMainPanel("CallingSelect");

        Text title = CreateText("CallingTitle", contentPanel, "Choose Your Calling", 36, buttonText, TextAnchor.MiddleCenter);
        RectTransform titleRect = title.rectTransform;
        titleRect.anchorMin = new Vector2(0.5f, 1f);
        titleRect.anchorMax = new Vector2(0.5f, 1f);
        titleRect.pivot     = new Vector2(0.5f, 1f);
        titleRect.anchoredPosition = new Vector2(0f, -20f);
        titleRect.sizeDelta = new Vector2(panelWidth - 40f, 44f);

        Text subtitle = CreateText("CallingSubtitle", contentPanel,
            "Your Calling sets your starting gifts. You can still learn every skill.",
            14, labelText, TextAnchor.MiddleCenter);
        subtitle.horizontalOverflow = HorizontalWrapMode.Wrap;
        RectTransform subRect = subtitle.rectTransform;
        subRect.anchorMin = new Vector2(0.5f, 1f);
        subRect.anchorMax = new Vector2(0.5f, 1f);
        subRect.pivot     = new Vector2(0.5f, 1f);
        subRect.anchoredPosition = new Vector2(0f, -64f);
        subRect.sizeDelta = new Vector2(panelWidth - 60f, 36f);

        // Scrollable list of Calling cards, built from the baked content (single source
        // of truth — BuildDefault() constructs all definitions in code, no asset load).
        RectTransform list = CreateScrollList("CallingList", contentPanel, -100f);

        var callings = FoundationContent.BuildDefault().Callings.All;
        for (int i = 0; i < callings.Count; i++)
            BuildCallingCard(list, callings[i]);

        if (callings.Count > 0)
            SelectCallingCard(callings[0].id);

        float btnY = -panelHeight + 50f;
        CreateMenuButton("BeginBtn", contentPanel, "Begin", () =>
        {
            if (pendingWorld != null && !string.IsNullOrEmpty(selectedCallingId))
                LaunchWorld(pendingWorld, selectedCallingId);
        }, -80f, btnY);
        CreateMenuButton("BackBtn", contentPanel, "Back", () => ShowScreen(Screen.CreateWorld), 80f, btnY);
    }

    private void BuildCallingCard(Transform parent, FoundationCallingDefinition calling)
    {
        RectTransform card = CreatePanel("Calling_" + calling.id, parent, buttonBg, panelBorder);
        card.sizeDelta = new Vector2(panelWidth - 80f, 104f);

        // Guarantee the row height under the list's VerticalLayoutGroup.
        var le = card.gameObject.AddComponent<LayoutElement>();
        le.minHeight = 104f; le.preferredHeight = 104f;

        Image cardBg = card.GetComponent<Image>();
        Outline cardOutline = card.GetComponent<Outline>(); // null when a panel skin is used
        Button btn = card.gameObject.AddComponent<Button>();
        btn.targetGraphic = cardBg;
        string id = calling.id;
        btn.onClick.AddListener(() => SelectCallingCard(id));

        callingCards.Add((id, cardBg, cardOutline));

        // Name (gold) + starting title (muted) across the top.
        Text name = CreateText("Name", card, calling.Display, 20, new Color(1f, 0.85f, 0.35f, 1f), TextAnchor.UpperLeft);
        name.raycastTarget = false;
        RectTransform nameRect = name.rectTransform;
        nameRect.anchorMin = new Vector2(0f, 1f); nameRect.anchorMax = new Vector2(1f, 1f);
        nameRect.pivot = new Vector2(0f, 1f);
        nameRect.offsetMin = new Vector2(12f, -30f); nameRect.offsetMax = new Vector2(-150f, -6f);

        Text titleText = CreateText("Title", card, calling.startingTitle, 12, labelText, TextAnchor.UpperRight);
        titleText.raycastTarget = false;
        RectTransform tRect = titleText.rectTransform;
        tRect.anchorMin = new Vector2(0f, 1f); tRect.anchorMax = new Vector2(1f, 1f);
        tRect.pivot = new Vector2(0f, 1f);
        tRect.offsetMin = new Vector2(panelWidth - 80f - 150f, -30f); tRect.offsetMax = new Vector2(-12f, -8f);

        // Stat bonuses line.
        Text stats = CreateText("Stats", card, StatBonusLine(calling), 13, new Color(0.95f, 0.91f, 0.74f, 1f), TextAnchor.UpperLeft);
        stats.raycastTarget = false;
        RectTransform sRect = stats.rectTransform;
        sRect.anchorMin = new Vector2(0f, 1f); sRect.anchorMax = new Vector2(1f, 1f);
        sRect.pivot = new Vector2(0f, 1f);
        sRect.offsetMin = new Vector2(12f, -52f); sRect.offsetMax = new Vector2(-12f, -32f);

        // Description (wrapped) fills the remainder.
        Text desc = CreateText("Desc", card, calling.description, 12, new Color(0.78f, 0.80f, 0.84f, 1f), TextAnchor.UpperLeft);
        desc.raycastTarget = false;
        desc.horizontalOverflow = HorizontalWrapMode.Wrap;
        RectTransform dRect = desc.rectTransform;
        dRect.anchorMin = new Vector2(0f, 0f); dRect.anchorMax = new Vector2(1f, 1f);
        dRect.offsetMin = new Vector2(12f, 8f); dRect.offsetMax = new Vector2(-12f, -54f);
    }

    private void SelectCallingCard(string id)
    {
        selectedCallingId = id;
        Color selBg     = new Color(0.20f, 0.24f, 0.32f, 1f);
        Color selBorder = new Color(0.98f, 0.85f, 0.45f, 1f);
        foreach (var card in callingCards)
        {
            bool sel = card.id == id;
            if (card.bg != null)      card.bg.color = sel ? selBg : buttonBg;
            if (card.outline != null) card.outline.effectColor = sel ? selBorder : panelBorder;
        }
    }

    private static string StatBonusLine(FoundationCallingDefinition calling)
    {
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

        Text title = CreateText("LoadTitle", contentPanel, "Load World", 40, buttonText, TextAnchor.MiddleCenter);
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

                    Text entryText = CreateText("Name", entry, $"{world.worldName} (Seed: {world.seed})", 16, buttonText, TextAnchor.MiddleLeft);
                    RectTransform entryTextRect = entryText.rectTransform;
                    entryTextRect.anchorMin = new Vector2(0f, 0.5f);
                    entryTextRect.anchorMax = new Vector2(1f, 0.5f);
                    entryTextRect.pivot = new Vector2(0f, 0.5f);
                    entryTextRect.offsetMin = new Vector2(12f, 0f);
                    entryTextRect.offsetMax = new Vector2(-12f, 0f);

                    int capturedIdx = i;
                    CreateSmallButton("PlayBtn", entry, "Play", () => LaunchWorld(savedWorlds[capturedIdx]), -60f, 0f);
                    CreateSmallButton("DeleteBtn", entry, "Delete", () => DeleteWorld(capturedIdx), 0f, 0f);
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

        Text title = CreateText("OptionsTitle", contentPanel, "Options", 40, buttonText, TextAnchor.MiddleCenter);
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

            // Cover-fit: fill the screen while preserving aspect (crops the overflow),
            // so the splash never stretches/distorts at any resolution or aspect ratio.
            AspectRatioFitter fitter = bgGO.AddComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            fitter.aspectRatio = (float)bg.rect.width / Mathf.Max(1f, bg.rect.height);

            // Cinematic treatment (owner request): slow Ken Burns drift on the art
            // plus a dark scrim so menu text stays readable over any background.
            bgGO.AddComponent<MenuCinematicBackground>();
            // Ambient animation loop, auto-enabled when frames exist at
            // Resources/UI/Menu/background_frames (disables itself otherwise).
            bgGO.AddComponent<MenuBackgroundFlipbook>();

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

            // Ambient particle layers (embers / fireflies / stars) — above the
            // scrim so they read bright against it, below all menu content.
            GameObject ambientGO = new GameObject("AmbientParticles", typeof(RectTransform));
            ambientGO.transform.SetParent(mainCanvas.transform, false);
            ambientGO.transform.SetSiblingIndex(2);
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

    private void CreateMenuButton(string name, Transform parent, string text, System.Action onClick, float x, float y)
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
            // Procedural fallback: flat fill + outline + colour-tint hover.
            bgImage.color = buttonBg;
            Outline outline = go.AddComponent<Outline>();
            outline.effectColor = panelBorder;
            outline.effectDistance = new Vector2(1.5f, -1.5f);

            ColorBlock colors = button.colors;
            colors.normalColor = buttonBg;
            colors.highlightedColor = buttonBgHover;
            colors.pressedColor = new Color(0.10f, 0.12f, 0.18f, 1f);
            colors.selectedColor = buttonBgHover;
            button.colors = colors;
        }

        Text btnText = CreateText("Text", rt, text, 20, this.buttonText, TextAnchor.MiddleCenter);
        RectTransform textRect = btnText.rectTransform;
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;
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
        // best-fit must stay OFF inside input fields: InputField caret math
        // assumes a fixed font size, and typed text shrinking reads as a glitch
        inputField.textComponent.resizeTextForBestFit = false;
        RectTransform textRect = inputField.textComponent.rectTransform;
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(10f, 0f);
        textRect.offsetMax = new Vector2(-10f, 0f);
        inputField.textComponent.color = inputText;

        Text placeholderText = CreateText("Placeholder", inputField.textComponent.transform, placeholder, 18, new Color(0.5f, 0.5f, 0.5f, 0.5f), TextAnchor.MiddleLeft);
        placeholderText.resizeTextForBestFit = false;
        RectTransform placeholderRect = placeholderText.rectTransform;
        placeholderRect.anchorMin = Vector2.zero;
        placeholderRect.anchorMax = Vector2.one;
        placeholderRect.offsetMin = new Vector2(10f, 0f);
        placeholderRect.offsetMax = new Vector2(-10f, 0f);
        inputField.placeholder = placeholderText;

        return inputField;
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
        vpMask.color = Color.clear; // raycast target only (scroll drag)
        // RectMask2D instead of stencil Mask: a Mask over a fully transparent Image can
        // cull every masked child (empty Calling/world lists). RectMask2D needs no graphic.
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
        LitIsoFont.Apply(text, size);
        // Owner feedback: menu text must NEVER exceed its box. Wrap + best-fit
        // shrinks long labels into their rect instead of spilling out.
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Truncate;
        text.resizeTextForBestFit = true;
        text.resizeTextMaxSize = text.fontSize;
        text.resizeTextMinSize = 11;
        return text;
    }
}
