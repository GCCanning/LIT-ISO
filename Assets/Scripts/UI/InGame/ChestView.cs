// PHASE_COMPLETE Phase5
using UnityEngine;
using UnityEngine.UI;

namespace LitIso.UI.InGame
{
    /// <summary>
    /// CHEST / STORAGE — net-new screen from the LIT-ISO front-end design.
    /// Two item grids (Chest contents + your Inventory) with rarity-bordered
    /// slots and stack counts. Opened with G, or from the Stations Hub "Chest"
    /// card. Sample contents mirror the design; live container binding follows.
    /// </summary>
    public sealed class ChestView : MonoBehaviour
    {
        static ChestView s_instance;
        public KeyCode toggleKey = KeyCode.G;
        Canvas _canvas;
        bool IsOpen => _canvas != null;

        struct Slot { public string name, icon, edge, rarity; public int stack;
            public Slot(string n, string ic, string e, string r, int s){ name=n; icon=ic; edge=e; rarity=r; stack=s; } }

        static readonly Slot[] ChestItems =
        {
            new Slot("Iron Ore","#8a6a5a","#4a3a30","#5a6068",23),
            new Slot("Coal","#26262b","#000000","#5a6068",41),
            new Slot("Steel Ingot","#cdd2da","#7a8089","#5a6068",6),
            new Slot("Gold Coin","#E8C468","#8a6f24","#5aa05a",340),
            new Slot("Ruby","#d9425a","#7a2030","#4f8ad9",2),
            new Slot("Old Map","#cdbf9a","#8a7c58","#5aa05a",1),
        };
        static readonly Slot[] InvItems =
        {
            new Slot("Oak Wood","#6b4a2a","#3a2614","#5a6068",34),
            new Slot("Cut Stone","#7a7d84","#4a4d54","#5a6068",18),
            new Slot("Iron Sword","#c7ccd4","#7a8089","#4f8ad9",1),
            new Slot("Health Draught","#d9425a","#7a2030","#5aa05a",3),
            new Slot("Torch","#e8a03c","#8a5a1c","#5a6068",5),
        };

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            if (s_instance != null) return;
            var go = new GameObject("ChestView");
            s_instance = go.AddComponent<ChestView>();
            DontDestroyOnLoad(go);
        }

        public static void OpenExternal() { if (s_instance != null) s_instance.Open(); }

        void Update()
        {
            var ui = IsoCore.Foundation.FoundationUiCoordinator.Active;
            if (Input.GetKeyDown(toggleKey))
            {
                if (IsOpen) { Close(); ui?.ConsumeInputThisFrame(); }
                else if (ui != null && ui.CanOpenModal("chest")) { Open(); ui.ConsumeInputThisFrame(); }
            }
            else if (IsOpen && Input.GetKeyDown(KeyCode.Escape)) { Close(); ui?.ConsumeInputThisFrame(); }
        }

        void Close()
        {
            if (_canvas != null) Destroy(_canvas.gameObject);
            _canvas = null;
            IsoCore.Foundation.FoundationUiCoordinator.Active?.SetModalOpen("chest", false);
        }

        void OnDestroy()
        {
            IsoCore.Foundation.FoundationUiCoordinator.Active?.SetModalOpen("chest", false);
        }

        void Open()
        {
            if (IsOpen) return;
            EnsureEventSystem();
            _canvas = UiBuilder.NewCanvas(transform, "ChestCanvas", 231);
            IsoCore.Foundation.FoundationUiCoordinator.Active?.SetModalOpen("chest", true);
            var root = UiBuilder.NewRect("Root", _canvas.transform);
            UiBuilder.Stretch(root);
            var scrim = UiBuilder.NewScrim(root);
            var sb = scrim.gameObject.AddComponent<Button>(); sb.transition = Selectable.Transition.None; sb.onClick.AddListener(Close);

            var panel = UiBuilder.NewPanel(root, "Panel", "system_panel", LitIsoTheme.Panel);
            var pr = panel.rectTransform;
            pr.anchorMin = pr.anchorMax = new Vector2(0.5f, 0.5f); pr.pivot = new Vector2(0.5f, 0.5f);
            pr.sizeDelta = new Vector2(1000f, 700f);

            var title = UiBuilder.NewText(panel.transform, "Title", "STORAGE", 28, TextAnchor.UpperLeft, LitIsoTheme.Gold);
            Place(title.rectTransform, 32f, -22f, 500f, 40f);
            var close = UiBuilder.NewButton(panel.transform, "Close", "btn_close", "X", 18);
            close.onClick.AddListener(Close);
            var cr = close.GetComponent<RectTransform>(); cr.anchorMin = cr.anchorMax = new Vector2(1f,1f); cr.pivot = new Vector2(1f,1f);
            cr.anchoredPosition = new Vector2(-18f,-18f); cr.sizeDelta = new Vector2(42f,42f);

            var chestHdr = UiBuilder.NewText(panel.transform, "ChestHdr", "CHEST", 14, TextAnchor.UpperLeft, LitIsoTheme.Gold);
            Place(chestHdr.rectTransform, 32f, -74f, 200f, 22f);
            var chestGrid = BuildGrid(panel.transform, new Vector2(20f, 20f), new Vector2(-512f, -104f));
            foreach (var s in ChestItems) BuildSlot(chestGrid, s, true);

            var invHdr = UiBuilder.NewText(panel.transform, "InvHdr", "INVENTORY", 14, TextAnchor.UpperLeft, LitIsoTheme.Gold);
            Place(invHdr.rectTransform, 512f, -74f, 200f, 22f);
            var invGrid = BuildGrid(panel.transform, new Vector2(512f, 20f), new Vector2(-20f, -104f));
            foreach (var s in InvItems) BuildSlot(invGrid, s, false);
        }

        void BuildSlot(RectTransform parent, Slot s, bool inChest)
        {
            var cell = UiBuilder.NewPanel(parent, "Slot_" + s.name, "system_row", UiBuilder.SlotBg);
            var ol = cell.gameObject.AddComponent<Outline>(); ol.effectColor = LitIsoTheme.Hex(s.rarity); ol.effectDistance = new Vector2(2f,-2f); ol.useGraphicAlpha = false;

            var tile = UiBuilder.NewImage(cell.transform, "Icon", null, LitIsoTheme.Hex(s.icon));
            var tr = tile.rectTransform; tr.anchorMin = new Vector2(0f,0f); tr.anchorMax = new Vector2(1f,1f);
            tr.offsetMin = new Vector2(12f,12f); tr.offsetMax = new Vector2(-12f,-22f);

            var count = UiBuilder.NewText(cell.transform, "Count", s.stack > 1 ? s.stack.ToString() : "", 13, TextAnchor.LowerRight, LitIsoTheme.Parchment);
            var qr = count.rectTransform; qr.anchorMin = new Vector2(0f,0f); qr.anchorMax = new Vector2(1f,0f); qr.pivot = new Vector2(1f,0f);
            qr.offsetMin = new Vector2(0f,2f); qr.offsetMax = new Vector2(-6f,18f);

            var btn = cell.gameObject.AddComponent<Button>(); btn.targetGraphic = cell;
            btn.interactable = false; // live container binding pending — render disabled, not a dead click
        }

        RectTransform BuildGrid(Transform parent, Vector2 offMin, Vector2 offMax)
        {
            var root = UiBuilder.NewRect("Scroll", parent);
            root.anchorMin = Vector2.zero; root.anchorMax = Vector2.one; root.offsetMin = offMin; root.offsetMax = offMax;
            var scroll = root.gameObject.AddComponent<ScrollRect>(); scroll.horizontal = false; scroll.vertical = true; scroll.scrollSensitivity = 34f;
            var viewport = UiBuilder.NewRect("Viewport", root); UiBuilder.Stretch(viewport);
            viewport.gameObject.AddComponent<Image>().color = new Color(0f,0f,0f,0.001f);
            viewport.gameObject.AddComponent<RectMask2D>();
            var content = UiBuilder.NewRect("Content", viewport);
            content.anchorMin = new Vector2(0f,1f); content.anchorMax = new Vector2(1f,1f); content.pivot = new Vector2(0.5f,1f); content.sizeDelta = Vector2.zero;
            var grid = content.gameObject.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(92f, 92f); grid.spacing = new Vector2(8f, 8f); grid.padding = new RectOffset(6,6,6,6);
            var fitter = content.gameObject.AddComponent<ContentSizeFitter>(); fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.viewport = viewport; scroll.content = content;
            return content;
        }

        static void Place(RectTransform rt, float x, float y, float w, float h)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0f,1f); rt.pivot = new Vector2(0f,1f);
            rt.anchoredPosition = new Vector2(x,y); rt.sizeDelta = new Vector2(w,h);
        }

        static void EnsureEventSystem()
        {
            if (FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
                new GameObject("EventSystem", typeof(UnityEngine.EventSystems.EventSystem), typeof(UnityEngine.EventSystems.StandaloneInputModule));
        }
    }
}
