using UnityEngine;
using UnityEngine.UI;

namespace LitIso.UI.InGame
{
    /// <summary>
    /// OWNER LAYOUT EVALUATION TOOL (2026-06-10) — not shipping UI.
    ///
    /// Press F9 in any scene to toggle a full wireframe preview of the approved
    /// UI overhaul ("Layout 1 — Corners classic" + 9-tab System Window with the
    /// approved Minecraft-style-but-clean inventory paper-doll). Pure outlines
    /// and text so layout can be judged before any art exists.
    ///
    /// While open: 1-9 switch tabs, or click the tab headers. H toggles the HUD
    /// wireframe separately so it can be judged with the panel closed.
    /// </summary>
    public sealed class WireframeUiPreview : MonoBehaviour
    {
        static WireframeUiPreview s_instance;

        static readonly string[] TabNames =
            { "Inventory", "Crafting", "Skills", "Spells", "Character", "Journal", "Map", "Settings", "System" };
        const int MetaTabsFrom = 7; // Settings & System render separated

        static readonly Color Fill    = new Color(0.95f, 0.94f, 0.91f, 0.97f);
        static readonly Color FillDim = new Color(0.95f, 0.94f, 0.91f, 0.55f);
        static readonly Color Line    = new Color(0.35f, 0.34f, 0.31f, 1f);
        static readonly Color InkCol  = new Color(0.28f, 0.27f, 0.24f, 1f);
        static readonly Color InkMut  = new Color(0.50f, 0.49f, 0.45f, 1f);

        GameObject _root;
        GameObject _hudRoot;
        GameObject _panelRoot;
        RectTransform _content;
        Image[] _tabBgs;
        int _tab;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            if (s_instance != null) return;
            var go = new GameObject("WireframeUiPreview");
            DontDestroyOnLoad(go);
            s_instance = go.AddComponent<WireframeUiPreview>();
            // auto-close on scene change so the preview never lingers over a
            // freshly loaded scene's real UI (2026-06-11 audit)
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += (_, __) =>
            {
                if (s_instance != null && s_instance._root != null)
                    s_instance._root.SetActive(false);
            };
        }

        void Update()
        {
            if (Input.GetKeyDown(KeyCode.F9)) Toggle();
            if (_root == null || !_root.activeSelf) return;

            if (Input.GetKeyDown(KeyCode.H))
                _hudRoot.SetActive(!_hudRoot.activeSelf);
            // P (not Tab): Tab is the live game's Character key — don't double-bind
            if (Input.GetKeyDown(KeyCode.P))
                _panelRoot.SetActive(!_panelRoot.activeSelf);
            if (_wheelRoot != null && _hudRoot.activeSelf)
            {
                bool held = Input.GetKey(KeyCode.X);
                if (held && !_wheelRoot.activeSelf)
                    _wheelRoot.transform.SetAsLastSibling(); // render above panel
                _wheelRoot.SetActive(held);
            }
            for (int i = 0; i < TabNames.Length; i++)
                if (Input.GetKeyDown(KeyCode.Alpha1 + i))
                    SwitchTab(i);
        }

        void Toggle()
        {
            if (_root == null) Build();
            else _root.SetActive(!_root.activeSelf);
        }

        // ------------------------------------------------------------------
        // construction helpers (top-left coordinate system inside parents)
        // ------------------------------------------------------------------

        static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        static RectTransform Place(RectTransform rt, float x, float y, float w, float h)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(x, -y);
            rt.sizeDelta = new Vector2(w, h);
            return rt;
        }

        static Image Box(Transform parent, string name, float x, float y, float w, float h,
            string label = null, int fontSize = 12, bool dim = false)
        {
            var img = Rect(name, parent).gameObject.AddComponent<Image>();
            img.color = dim ? FillDim : Fill;
            Place(img.rectTransform, x, y, w, h);
            var ol = img.gameObject.AddComponent<Outline>();
            ol.effectColor = Line;
            ol.effectDistance = new Vector2(1.5f, -1.5f);
            if (!string.IsNullOrEmpty(label))
            {
                var t = Text(img.transform, "Label", label, fontSize, dim ? InkMut : InkCol);
                t.rectTransform.anchorMin = Vector2.zero;
                t.rectTransform.anchorMax = Vector2.one;
                t.rectTransform.offsetMin = new Vector2(4f, 2f);
                t.rectTransform.offsetMax = new Vector2(-4f, -2f);
                t.alignment = TextAnchor.MiddleCenter;
            }
            return img;
        }

        static Text Text(Transform parent, string name, string value, int size, Color color)
        {
            var t = Rect(name, parent).gameObject.AddComponent<Text>();
            t.text = value;
            t.color = color;
            t.alignment = TextAnchor.UpperLeft;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Truncate;
            // Raw sizes on purpose: LitIsoFont.Apply inflates by snap*textScale
            // (~+30%), which overflowed wireframe boxes. Wireframes must show
            // exact intended proportions.
            t.font = LitIsoFont.Body;
            t.fontSize = size;
            return t;
        }

        static void SlotGrid(Transform parent, float x, float y, int cols, int rows,
            float slot = 52f, float gap = 5f)
        {
            for (int r = 0; r < rows; r++)
                for (int c = 0; c < cols; c++)
                    Box(parent, $"Slot_{r}_{c}", x + c * (slot + gap), y + r * (slot + gap), slot, slot);
        }

        static void Bar(Transform parent, float x, float y, float w, string label, float fill01)
        {
            var track = Box(parent, "Bar_" + label, x, y, w, 24f);
            var fill = Rect("Fill", track.transform).gameObject.AddComponent<Image>();
            fill.color = new Color(0.55f, 0.54f, 0.50f, 0.9f);
            Place(fill.rectTransform, 2f, 2f, (w - 4f) * fill01, 20f);
            var t = Text(track.transform, "Num", label, 12, InkCol);
            t.alignment = TextAnchor.MiddleCenter;
            t.rectTransform.anchorMin = Vector2.zero;
            t.rectTransform.anchorMax = Vector2.one;
            t.rectTransform.offsetMin = t.rectTransform.offsetMax = Vector2.zero;
        }

        // ------------------------------------------------------------------

        void Build()
        {
            _root = new GameObject("WireframeRoot");
            _root.transform.SetParent(transform, false);
            var canvas = _root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 5000;
            var scaler = _root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600, 900);
            _root.AddComponent<GraphicRaycaster>();

            var dim = Rect("Dim", _root.transform).gameObject.AddComponent<Image>();
            dim.color = new Color(0f, 0f, 0f, 0.55f);
            dim.rectTransform.anchorMin = Vector2.zero;
            dim.rectTransform.anchorMax = Vector2.one;
            dim.rectTransform.offsetMin = dim.rectTransform.offsetMax = Vector2.zero;

            BuildHud();
            BuildPanel();

            var foot = Text(_root.transform, "Footer",
                "WIREFRAME PREVIEW — F9 close · P hide panel · H toggle HUD · hold X = wheel · 1-9 tabs", 13, new Color(1f, 1f, 1f, 0.8f));
            foot.alignment = TextAnchor.LowerLeft;
            foot.rectTransform.anchorMin = new Vector2(0f, 0f);
            foot.rectTransform.anchorMax = new Vector2(0f, 0f);
            foot.rectTransform.pivot = new Vector2(0f, 0f);
            foot.rectTransform.anchoredPosition = new Vector2(12f, 8f);
            foot.rectTransform.sizeDelta = new Vector2(700f, 22f);
        }

        // ---- HUD (Layout 1: corners classic, horizontal bars + inline numbers)

        void BuildHud()
        {
            _hudRoot = Rect("HUD", _root.transform).gameObject;
            var rt = (RectTransform)_hudRoot.transform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;

            // top-left: vitals (horizontal bars, numbers inside) + level ring
            var vit = Box(rt, "Vitals", 20, 20, 320, 118);
            Box(vit.transform, "LevelRing", 8, 8, 56, 56, "Lv 7\nring");
            Bar(vit.transform, 74, 8,  236, "HP  82 / 110", 0.74f);
            Bar(vit.transform, 74, 38, 236, "MP  35 / 60", 0.58f);
            Bar(vit.transform, 74, 68, 236, "SP  90 / 100", 0.90f);
            Text(vit.transform, "XpHint", "XP ring fills around level · pulses when point unspent", 10, InkMut)
                .rectTransform.With(r => Place(r, 8, 96, 300, 16));

            // top-right: minimap + clock
            var map = Box(rt, "Minimap", 1600 - 20 - 230, 20, 230, 170, "minimap");
            Box(map.transform, "Clock", 0, 144, 230, 26, "Day 3 · 18:40 · dusk", 11);

            // right: quest tracker
            Box(rt, "Quests", 1600 - 20 - 260, 220, 260, 130,
                "QUEST · A Roof Before Rain\n— Gather wood 14/20\nreward: 40 xp", 12);

            // top-center: notifications
            Box(rt, "Notif", 560, 16, 480, 38, "[System] Evidence recorded: woodcraft +3", 12, true);

            // bottom-center: hotbar + optional ability row (decision pending)
            var hb = Box(rt, "Hotbar", 560, 900 - 84, 480, 64);
            for (int i = 0; i < 9; i++)
                Box(hb.transform, "S" + i, 8 + i * 52, 6, 46, 46, (i + 1).ToString(), 11);

            // CONFIRMED Option B: 4 ability slots; tap = cast, hold X = swap wheel.
            var ab = Box(rt, "AbilityRow", 330, 900 - 84, 220, 64);
            string[] keys = { "Q", "E", "R", "F" };
            for (int i = 0; i < 4; i++)
                Box(ab.transform, "K" + keys[i], 8 + i * 52, 6, 46, 46, keys[i], 12);
            Box(rt, "WheelHint", 330, 900 - 110, 220, 22, "hold X = ability wheel", 10, true);

            BuildWheel(rt);

            // bottom-right: context hint
            Box(rt, "Hint", 1600 - 20 - 280, 900 - 56, 280, 32, "RMB — open Workbench", 11, true);
        }

        // ---- Ability wheel (hold X): radial swap/cast selector --------------

        GameObject _wheelRoot;

        void BuildWheel(RectTransform hud)
        {
            _wheelRoot = Rect("AbilityWheel", hud).gameObject;
            var wrt = (RectTransform)_wheelRoot.transform;
            wrt.anchorMin = wrt.anchorMax = new Vector2(0.5f, 0.5f);
            wrt.pivot = new Vector2(0.5f, 0.5f);
            wrt.sizeDelta = new Vector2(520, 520);

            var dim = _wheelRoot.AddComponent<Image>();
            dim.color = new Color(0f, 0f, 0f, 0.35f);

            // outer ring: known abilities (8 segments)
            string[] abilities = { "Steady\nStrike", "Guard\nStep", "Mana\nBolt", "Ember\nSpark",
                                   "Root\nSnare", "Stone\nSkin", "(empty)", "(empty)" };
            for (int i = 0; i < 8; i++)
            {
                float ang = Mathf.PI / 2f - i * (2f * Mathf.PI / 8f);
                float cx = 260f + 200f * Mathf.Cos(ang) - 40f;
                float cy = 260f - 200f * Mathf.Sin(ang) - 28f;
                Box(wrt, "W" + i, cx, cy, 80, 56, abilities[i], 11, abilities[i] == "(empty)");
            }
            // inner anchors: the 4 slots — drag toward one to assign
            string[] keys = { "Q", "E", "R", "F" };
            for (int i = 0; i < 4; i++)
            {
                float ang = Mathf.PI / 2f - i * (Mathf.PI / 2f) - Mathf.PI / 4f;
                float cx = 260f + 78f * Mathf.Cos(ang) - 24f;
                float cy = 260f - 78f * Mathf.Sin(ang) - 24f;
                Box(wrt, "Anchor" + keys[i], cx, cy, 48, 48, keys[i], 13);
            }
            Box(wrt, "Center", 260f - 70f, 260f - 22f, 140, 44,
                "drag out = assign\nrelease on ability = cast", 10, true);
            _wheelRoot.SetActive(false);
        }

        // ---- System Window (9 tabs, meta tabs separated)

        void BuildPanel()
        {
            _panelRoot = Rect("Panel", _root.transform).gameObject;
            var prt = (RectTransform)_panelRoot.transform;
            prt.anchorMin = prt.anchorMax = new Vector2(0.5f, 0.5f);
            prt.pivot = new Vector2(0.5f, 0.5f);
            prt.sizeDelta = new Vector2(1150, 720);
            var bg = _panelRoot.AddComponent<Image>();
            bg.color = new Color(0.10f, 0.10f, 0.12f, 0.92f);
            var ol = _panelRoot.AddComponent<Outline>();
            ol.effectColor = Line; ol.effectDistance = new Vector2(2f, -2f);

            _tabBgs = new Image[TabNames.Length];
            float x = 10f;
            for (int i = 0; i < TabNames.Length; i++)
            {
                if (i == MetaTabsFrom) x += 60f; // visual gap before Settings/System
                float w = 14f + TabNames[i].Length * 8.5f;
                var tab = Box(prt, "Tab_" + TabNames[i], x, 10, w, 34, TabNames[i], 13, i >= MetaTabsFrom);
                _tabBgs[i] = tab;
                int idx = i;
                var b = tab.gameObject.AddComponent<Button>();
                b.targetGraphic = tab;
                b.onClick.AddListener(() => SwitchTab(idx));
                x += w + 6f;
            }

            _content = Place(Rect("Content", prt), 10, 56, 1130, 652);
            SwitchTab(0);
        }

        void SwitchTab(int idx)
        {
            _tab = idx;
            for (int i = 0; i < _tabBgs.Length; i++)
                _tabBgs[i].color = i == idx ? new Color(0.80f, 0.77f, 0.66f, 1f) : (i >= MetaTabsFrom ? FillDim : Fill);
            for (int i = _content.childCount - 1; i >= 0; i--)
                Destroy(_content.GetChild(i).gameObject);
            switch (idx)
            {
                case 0: DrawInventory(); break;
                case 1: DrawCrafting(); break;
                case 2: DrawSkills(); break;
                case 3: DrawSpells(); break;
                case 4: DrawCharacter(); break;
                case 5: DrawJournal(); break;
                case 6: DrawMap(); break;
                case 7: DrawSettings(); break;
                case 8: DrawSystem(); break;
            }
        }

        // ---- tab contents -------------------------------------------------

        void DrawInventory()
        {
            // left: paper doll (approved wireframe)
            var doll = Box(_content, "Doll", 0, 0, 390, 560);
            Box(doll.transform, "Player", 120, 30, 150, 280, "player model\n(live idle, rotatable)", 12);
            string[] left = { "head", "chest", "legs", "feet" };
            for (int i = 0; i < 4; i++) Box(doll.transform, left[i], 20, 30 + i * 72, 80, 60, left[i], 11);
            string[] right = { "back", "acc 1", "acc 2" };
            for (int i = 0; i < 3; i++) Box(doll.transform, right[i], 290, 30 + i * 72, 80, 60, right[i], 11);
            Box(doll.transform, "Delta", 20, 340, 350, 90,
                "hover any equipment:\nDEF 12 -> 15   ·   SPD -2%", 12, true);
            Box(doll.transform, "EquipHint", 20, 450, 350, 90,
                "drag item onto doll = equip\nRMB item in bag = quick-equip\nhover = compare vs worn", 11, true);

            // right: bag grid + tools row
            Box(_content, "BagLabel", 420, 0, 300, 24, "Bag  (36 slots)", 12, true);
            SlotGrid(_content, 420, 30, 9, 4);
            Box(_content, "SortBar", 420, 270, 520, 30, "sort · filter · search ____________", 11, true);
            Box(_content, "HotbarLabel", 420, 318, 300, 22, "Hotbar (mirrors HUD)", 11, true);
            SlotGrid(_content, 420, 344, 9, 1);
            Box(_content, "Weight", 420, 420, 520, 26, "carry: 41 / 60   ·   coins: 128", 11, true);
        }

        void DrawCrafting()
        {
            Box(_content, "List", 0, 0, 420, 600, null);
            Box(_content, "ListTitle", 10, 10, 400, 26, "Recipes (29) — station filter: All v", 12);
            for (int i = 0; i < 6; i++)
                Box(_content, "R" + i, 10, 46 + i * 64, 400, 56,
                    i == 0 ? "> Campfire — Ready" : "recipe row — name · station · status", 11, i != 0);
            Box(_content, "Details", 440, 0, 690, 600);
            Box(_content, "DTitle", 450, 10, 670, 30, "Craft Campfire — Station: Hand", 13);
            Box(_content, "Ingred", 450, 50, 330, 200, "ingredients\nwood 4/4 · stone 2/2", 12);
            Box(_content, "Preview", 800, 50, 320, 200, "result preview + flavor text", 12, true);
            Box(_content, "Qty", 450, 270, 330, 40, "craft x1  x5  xMax", 12);
            Box(_content, "CraftBtn", 950, 540, 170, 48, "CRAFT", 14);
        }

        void DrawSkills()
        {
            Box(_content, "WebArea", 0, 0, 1130, 560, null);
            Box(_content, "Core", 540, 250, 80, 60, "core", 11);
            string[] spokes = { "Wilds", "Hearth", "Maker", "Deep", "Arcane", "Folk", "Blade" };
            for (int s = 0; s < 7; s++)
            {
                float ang = Mathf.PI / 2f - s * (2f * Mathf.PI / 7f);
                for (int r = 1; r <= 3; r++)
                {
                    float rad = 70f + r * 62f;
                    float cx = 565f + rad * Mathf.Cos(ang) * 1.7f;
                    float cy = 270f - rad * Mathf.Sin(ang);
                    Box(_content, $"N{s}_{r}", cx, cy, r == 3 ? 64 : 44, r == 3 ? 44 : 32,
                        r == 3 ? spokes[s] : null, 10, r != 1);
                }
            }
            Box(_content, "WebInfo", 0, 575, 1130, 70,
                "selected node: name · effect · cost   [ALLOCATE]      points: 6 (banked from trial)", 12);
        }

        void DrawSpells()
        {
            Box(_content, "Known", 0, 0, 560, 600);
            Box(_content, "KTitle", 10, 10, 540, 26, "Known abilities — skills (stamina) & spells (mana)", 12);
            string[] rows = { "Steady Strike · stamina 10 · cd 3s", "Guard Step · stamina 8 · cd 5s",
                "Mana Bolt · mana 12 · cd 2s", "Ember Spark · mana 15 · Ember rank 1+" };
            for (int i = 0; i < rows.Length; i++)
                Box(_content, "A" + i, 10, 46 + i * 60, 540, 52, rows[i], 11, i > 0);
            Box(_content, "Loadout", 590, 0, 540, 320);
            Box(_content, "LTitle", 600, 10, 520, 26, "Loadout — drag ability into a slot", 12);
            string[] keys = { "Q", "E", "R", "F" };
            for (int i = 0; i < 4; i++)
                Box(_content, "K" + keys[i], 610 + i * 130, 50, 110, 90, keys[i] + "\n(slot)", 13);
            Box(_content, "BindNote", 600, 160, 520, 140,
                "CONFIRMED: Q/E/R/F ability slots (tap = cast)\nhold X = radial wheel: drag toward an anchor\nto assign, release on ability to one-shot cast\n(see it on the HUD: hold X with panel closed)", 12, true);
            Box(_content, "Detail", 590, 340, 540, 260, "selected ability detail\npower · scaling affinity · evidence it records", 12, true);
        }

        void DrawCharacter()
        {
            Box(_content, "Identity", 0, 0, 560, 200,
                "Gary of Greenwake\nUNCLASSED — Trial: Day 3 of 7\ngrade forecast: B   ·   rank: —", 13);
            Box(_content, "Stats", 0, 220, 560, 240,
                "STR 8   DEX 11   INT 14\nVIT 9   DEF 7    LUCK 5\n\nHP 110 · MP 60 · SP 100", 13);
            Box(_content, "Titles", 0, 480, 560, 120, "titles: First Night Survivor · +1 more", 12, true);
            Box(_content, "Affinity", 590, 0, 540, 320, "affinity wheel\nEmber 4 · Tide 1 · Root 7 · Stone 2\nGale 3 · Glimmer 5 · Hearth 6\n(awakening at 10)", 12);
            Box(_content, "ClassCard", 590, 340, 540, 260,
                "CLASS (after day 7)\nupgrade paths · class rank Novice->Master\nclass constellation progress", 12, true);
        }

        void DrawJournal()
        {
            Box(_content, "QuestList", 0, 0, 360, 600, null);
            Box(_content, "QTitle", 10, 10, 340, 26, "Quests (2 active)", 12);
            Box(_content, "Q1", 10, 46, 340, 70, "> A Roof Before Rain — 2/3 [pinned]", 11);
            Box(_content, "Q2", 10, 124, 340, 70, "First Flame, First Field — done, claim!", 11, true);
            Box(_content, "QuestDetail", 380, 0, 400, 600, "quest detail\nobjectives · rewards · giver\n[PIN] [ABANDON]", 12);
            Box(_content, "Log", 800, 0, 330, 290, "System log\n[18:02] Evidence: woodcraft +3\n[17:55] Level up! 6 -> 7", 11, true);
            Box(_content, "Evidence", 800, 310, 330, 290, "Trial evidence (day 3/7)\nvariety 6/11 · volume 142\ndifficulty T1 · quality 87%", 11);
        }

        void DrawMap()
        {
            Box(_content, "Map", 0, 0, 1130, 600, "world map\n(pan / zoom · explored fog · markers: player, spawn, portals, camp)", 13);
            Box(_content, "Legend", 10, 540, 380, 50, "legend · filters: portals x resources x", 11, true);
        }

        void DrawSettings()
        {
            string[] rows = { "Master volume  ----o----  80%", "SFX volume     ------o--  90%",
                "Music volume   --o------  40%", "UI scale       ----o----  100%",
                "Text size      ---o-----  100%", "Key bindings   [view / remap]", "HUD layout: Basic / Adventure / Hidden (F1)" };
            for (int i = 0; i < rows.Length; i++)
                Box(_content, "Set" + i, 0, i * 64, 700, 54, rows[i], 12, i % 2 == 1);
            Box(_content, "Note", 740, 0, 390, 180, "applies instantly\nsaved to profile", 12, true);
        }

        void DrawSystem()
        {
            Box(_content, "SaveState", 0, 0, 560, 90, "world: Greenwake · day 3 · last saved 4 min ago", 12, true);
            Box(_content, "Save", 0, 110, 270, 60, "SAVE GAME", 14);
            Box(_content, "SaveQuit", 290, 110, 270, 60, "SAVE & QUIT", 14);
            Box(_content, "Menu", 0, 190, 270, 60, "MAIN MENU", 14);
            Box(_content, "QuitDesk", 290, 190, 270, 60, "QUIT TO DESKTOP", 14);
            Box(_content, "Warn", 0, 280, 560, 60, "unsaved-changes warning appears here when needed", 11, true);
        }
    }

    static class RectExt
    {
        public static void With(this RectTransform rt, System.Action<RectTransform> f) => f(rt);
    }
}
