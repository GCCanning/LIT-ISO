using System;
using UnityEngine;
using UnityEngine.UI;

namespace LitIso.UI.InGame
{
    /// <summary>
    /// STATIONS HUB — net-new screen from the LIT-ISO front-end design.
    /// A themed grid of crafting stations + world actions, opened with B.
    /// Crafting-station cards route into the live Crafting tab
    /// (<see cref="CharacterPanelView"/>); world cards are placeholders until
    /// their own screens (Chest / Vendor / Build) are wired.
    ///
    /// Self-installs once and toggles on B / closes on Esc. Built entirely from
    /// the shared design system (<see cref="LitIsoTheme"/> + <see cref="UiBuilder"/>)
    /// so it matches every other screen and picks up the design fonts.
    /// </summary>
    public sealed class StationsHubView : MonoBehaviour
    {
        static StationsHubView s_instance;

        public KeyCode toggleKey = KeyCode.B;

        Canvas _canvas;
        bool IsOpen => _canvas != null;

        struct Card
        {
            public string name, desc, accentHex;
            public CharacterPanelTab? opensTab; // crafting stations open the Crafting tab
            public Card(string n, string d, string a, CharacterPanelTab? t = null)
            { name = n; desc = d; accentHex = a; opensTab = t; }
        }

        // Transcribed from the design's hubCrafting / hubWorld data.
        static readonly Card[] CraftingCards =
        {
            new Card("Workbench",        "Assemble tools, weapons & parts", "#c9a26a", CharacterPanelTab.Crafting),
            new Card("Furnace",          "Smelt ore into ingots & glass",   "#e07b3a", CharacterPanelTab.Crafting),
            new Card("Tannery",          "Cure raw hides into leather",     "#8a5a2c", CharacterPanelTab.Crafting),
            new Card("Cooking Pot",      "Cook meals that restore & buff",  "#d98a5a", CharacterPanelTab.Crafting),
            new Card("Enchanting Table", "Bind elemental runes to gear",    "#a060d9", CharacterPanelTab.Crafting),
            new Card("Anvil",            "Repair & reforge equipment",      "#7a8089", CharacterPanelTab.Crafting),
            new Card("Alchemy Bench",    "Brew potions & tonics",           "#5aa05a", CharacterPanelTab.Crafting),
            new Card("Beast Pen",        "Tame & bond with wild creatures", "#5fc9b8", CharacterPanelTab.Crafting),
        };

        static readonly Card[] WorldCards =
        {
            new Card("Chest / Storage", "Move items in & out of containers", "#c9a26a"),
            new Card("Vendor / Shop",   "Buy & sell with gold",              "#E8C468"),
            new Card("Build / Place",   "Place camp, stations & structures", "#6fae6f"),
        };

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            if (s_instance != null) return;
            var go = new GameObject("StationsHubView");
            s_instance = go.AddComponent<StationsHubView>();
            DontDestroyOnLoad(go);
        }

        void Update()
        {
            var ui = IsoCore.Foundation.FoundationUiCoordinator.Active;
            if (Input.GetKeyDown(toggleKey))
            {
                if (IsOpen) { Close(); ui?.ConsumeInputThisFrame(); }
                else if (ui == null || ui.CanOpenModal("stationsHub")) { Open(); ui?.ConsumeInputThisFrame(); }
            }
            else if (IsOpen && Input.GetKeyDown(KeyCode.Escape)) { Close(); ui?.ConsumeInputThisFrame(); }
        }

        void Toggle() { if (IsOpen) Close(); else Open(); }

        void Close()
        {
            if (_canvas != null) Destroy(_canvas.gameObject);
            _canvas = null;
            IsoCore.Foundation.FoundationUiCoordinator.Active?.SetModalOpen("stationsHub", false);
        }

        void OnDestroy()
        {
            IsoCore.Foundation.FoundationUiCoordinator.Active?.SetModalOpen("stationsHub", false);
        }

        void Open()
        {
            EnsureEventSystem();
            _canvas = UiBuilder.NewCanvas(transform, "StationsHubCanvas", 230);
            IsoCore.Foundation.FoundationUiCoordinator.Active?.SetModalOpen("stationsHub", true);

            var root = UiBuilder.NewRect("Root", _canvas.transform);
            UiBuilder.Stretch(root);

            var scrim = UiBuilder.NewScrim(root);
            var scrimBtn = scrim.gameObject.AddComponent<Button>();
            scrimBtn.transition = Selectable.Transition.None;
            scrimBtn.onClick.AddListener(Close);

            var panel = UiBuilder.NewPanel(root, "Panel", "system_panel", LitIsoTheme.Panel);
            var pr = panel.rectTransform;
            pr.anchorMin = pr.anchorMax = new Vector2(0.5f, 0.5f);
            pr.pivot = new Vector2(0.5f, 0.5f);
            pr.sizeDelta = new Vector2(1160f, 760f);

            var title = UiBuilder.NewText(panel.transform, "Title", "STATIONS", 28, TextAnchor.UpperLeft, LitIsoTheme.Gold);
            Place(title.rectTransform, 32f, -22f, 600f, 40f);

            var subtitle = UiBuilder.NewText(panel.transform, "Subtitle",
                "Choose a station to craft, or a world action.", 16, TextAnchor.UpperLeft, LitIsoTheme.WarmTan);
            Place(subtitle.rectTransform, 32f, -66f, 700f, 26f);

            var close = UiBuilder.NewButton(panel.transform, "Close", "btn_close", "X", 18);
            close.onClick.AddListener(Close);
            var cr = close.GetComponent<RectTransform>();
            cr.anchorMin = cr.anchorMax = new Vector2(1f, 1f);
            cr.pivot = new Vector2(1f, 1f);
            cr.anchoredPosition = new Vector2(-18f, -18f);
            cr.sizeDelta = new Vector2(42f, 42f);

            // CRAFTING section
            var craftHdr = UiBuilder.NewText(panel.transform, "CraftHdr", "CRAFTING", 14, TextAnchor.UpperLeft, LitIsoTheme.Gold);
            Place(craftHdr.rectTransform, 32f, -106f, 400f, 22f);
            BuildGrid(panel.transform, CraftingCards, startY: -134f, cols: 4);

            // WORLD section (below the 2 crafting rows)
            var worldHdr = UiBuilder.NewText(panel.transform, "WorldHdr", "WORLD", 14, TextAnchor.UpperLeft, LitIsoTheme.Gold);
            Place(worldHdr.rectTransform, 32f, -420f, 400f, 22f);
            BuildGrid(panel.transform, WorldCards, startY: -448f, cols: 4);
        }

        const float CardW = 262f, CardH = 116f, Gap = 16f, PadX = 32f;

        void BuildGrid(Transform parent, Card[] cards, float startY, int cols)
        {
            for (int i = 0; i < cards.Length; i++)
            {
                int col = i % cols;
                int row = i / cols;
                float x = PadX + col * (CardW + Gap);
                float y = startY - row * (CardH + Gap);
                BuildCard(parent, cards[i], x, y);
            }
        }

        void BuildCard(Transform parent, Card card, float x, float y)
        {
            var cardImg = UiBuilder.NewPanel(parent, "Card_" + card.name, "system_row", UiBuilder.SlotBg);
            Place(cardImg.rectTransform, x, y, CardW, CardH);

            var btn = cardImg.gameObject.AddComponent<Button>();
            btn.targetGraphic = cardImg;
            Card captured = card;
            btn.onClick.AddListener(() => OnCardClicked(captured));

            Color accent = LitIsoTheme.Hex(card.accentHex);

            // left accent bar
            var bar = UiBuilder.NewImage(cardImg.transform, "Accent", null, accent);
            var barRt = bar.rectTransform;
            barRt.anchorMin = new Vector2(0f, 0f);
            barRt.anchorMax = new Vector2(0f, 1f);
            barRt.pivot = new Vector2(0f, 0.5f);
            barRt.offsetMin = new Vector2(0f, 8f);
            barRt.offsetMax = new Vector2(6f, -8f);

            var name = UiBuilder.NewText(cardImg.transform, "Name", card.name, 18, TextAnchor.UpperLeft, accent);
            var nameRt = name.rectTransform;
            nameRt.anchorMin = new Vector2(0f, 1f);
            nameRt.anchorMax = new Vector2(1f, 1f);
            nameRt.pivot = new Vector2(0f, 1f);
            nameRt.offsetMin = new Vector2(20f, -40f);
            nameRt.offsetMax = new Vector2(-12f, -12f);
            UiBuilder.FitText(name);

            var desc = UiBuilder.NewText(cardImg.transform, "Desc", card.desc, 14, TextAnchor.UpperLeft, LitIsoTheme.Parchment);
            desc.horizontalOverflow = HorizontalWrapMode.Wrap;
            var descRt = desc.rectTransform;
            descRt.anchorMin = new Vector2(0f, 0f);
            descRt.anchorMax = new Vector2(1f, 1f);
            descRt.offsetMin = new Vector2(20f, 12f);
            descRt.offsetMax = new Vector2(-12f, -46f);
        }

        void OnCardClicked(Card card)
        {
            if (card.opensTab.HasValue)
            {
                var panel = FindFirstObjectByType<CharacterPanelView>();
                if (panel != null)
                {
                    Close();
                    // Opened from the menu, not a specific world station -> show all
                    // stations. (DrawCrafting honors CraftingStationContext.)
                    // TODO(station-filter): map each crafting card to its StationType
                    // and call CraftingStationContext.Set(...) instead of Clear() so
                    // the hub also filters the list per card.
                    CraftingStationContext.Clear();
                    panel.Show(card.opensTab.Value);
                    return;
                }
                Debug.Log($"[StationsHub] {card.name}: crafting panel not present in this scene.");
            }
            else if (card.name.StartsWith("Vendor"))
            {
                Close();
                VendorView.OpenExternal();
            }
            else if (card.name.StartsWith("Chest"))
            {
                Close();
                ChestView.OpenExternal();
            }
            else if (card.name.StartsWith("Build"))
            {
                Close();
                BuildView.OpenExternal();
            }
            else
            {
                Debug.Log($"[StationsHub] {card.name}: screen not built yet.");
            }
        }

        static void Place(RectTransform rt, float x, float y, float w, float h)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(x, y);
            rt.sizeDelta = new Vector2(w, h);
        }

        static void EnsureEventSystem()
        {
            if (FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
                new GameObject("EventSystem",
                    typeof(UnityEngine.EventSystems.EventSystem),
                    typeof(UnityEngine.EventSystems.StandaloneInputModule));
        }
    }
}
