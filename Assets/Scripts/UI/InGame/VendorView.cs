using UnityEngine;
using UnityEngine.UI;

namespace LitIso.UI.InGame
{
    /// <summary>
    /// VENDOR / SHOP — net-new screen from the LIT-ISO front-end design.
    /// Two columns: vendor stock to BUY and the player's goods to SELL, with a
    /// gold tally. Opened only by interacting with a merchant NPC (see
    /// PlayerInteraction) or from the Stations Hub "Vendor" card — there is no
    /// global hotkey, so the shop can't be summoned anywhere.
    /// Sample stock mirrors the design; live shop binding is the follow-up.
    /// </summary>
    public sealed class VendorView : MonoBehaviour
    {
        static VendorView s_instance;
        Canvas _canvas;
        bool IsOpen => _canvas != null;

        struct Item { public string name; public string icon, edge, rarity; public int price; public int stack;
            public Item(string n, string ic, string e, string r, int p, int s=0){ name=n; icon=ic; edge=e; rarity=r; price=p; stack=s; } }

        static readonly Item[] BuyStock =
        {
            new Item("Steel Longsword","#cdd2da","#7a8089","#4f8ad9",240),
            new Item("Health Draught","#d9425a","#7a2030","#5aa05a",35),
            new Item("Iron Ingot","#aeb4bc","#6a7079","#5a6068",18),
            new Item("Torch ×5","#e8a03c","#8a5a1c","#5a6068",12),
            new Item("Travel Cloak","#3a5a7a","#1e3450","#5aa05a",90),
        };
        static readonly Item[] SellStock =
        {
            new Item("Wolf Pelt","#8a7458","#52442f","#5a6068",8,7),
            new Item("Ash Berries","#9a3a5a","#5a2030","#5a6068",2,12),
            new Item("Iron Sword","#c7ccd4","#7a8089","#4f8ad9",60,1),
        };
        const int PlayerGold = 1284;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            if (s_instance != null) return;
            var go = new GameObject("VendorView");
            s_instance = go.AddComponent<VendorView>();
            DontDestroyOnLoad(go);
        }

        public static void OpenExternal() { if (s_instance != null) s_instance.Open(); }

        void Update()
        {
            // No global hotkey to open the shop — the vendor is reached only via a
            // merchant NPC (PlayerInteraction) or the Stations Hub. Esc still closes.
            if (IsOpen && Input.GetKeyDown(KeyCode.Escape))
            {
                Close();
                IsoCore.Foundation.FoundationUiCoordinator.Active?.ConsumeInputThisFrame();
            }
        }

        void Close()
        {
            if (_canvas != null) Destroy(_canvas.gameObject);
            _canvas = null;
            IsoCore.Foundation.FoundationUiCoordinator.Active?.SetModalOpen("vendor", false);
        }

        void OnDestroy()
        {
            IsoCore.Foundation.FoundationUiCoordinator.Active?.SetModalOpen("vendor", false);
        }

        void Open()
        {
            if (IsOpen) return;
            EnsureEventSystem();
            _canvas = UiBuilder.NewCanvas(transform, "VendorCanvas", 231);
            IsoCore.Foundation.FoundationUiCoordinator.Active?.SetModalOpen("vendor", true);
            var root = UiBuilder.NewRect("Root", _canvas.transform);
            UiBuilder.Stretch(root);
            var scrim = UiBuilder.NewScrim(root);
            var sb = scrim.gameObject.AddComponent<Button>(); sb.transition = Selectable.Transition.None; sb.onClick.AddListener(Close);

            var panel = UiBuilder.NewPanel(root, "Panel", "system_panel", LitIsoTheme.Panel);
            var pr = panel.rectTransform;
            pr.anchorMin = pr.anchorMax = new Vector2(0.5f, 0.5f); pr.pivot = new Vector2(0.5f, 0.5f);
            pr.sizeDelta = new Vector2(1000f, 700f);

            var title = UiBuilder.NewText(panel.transform, "Title", "VENDOR", 28, TextAnchor.UpperLeft, LitIsoTheme.Gold);
            Place(title.rectTransform, 32f, -22f, 500f, 40f);
            var gold = UiBuilder.NewText(panel.transform, "Gold", $"{PlayerGold:N0} G", 20, TextAnchor.UpperRight, LitIsoTheme.GoldLit);
            var gr = gold.rectTransform; gr.anchorMin = gr.anchorMax = new Vector2(1f,1f); gr.pivot = new Vector2(1f,1f);
            gr.anchoredPosition = new Vector2(-72f, -24f); gr.sizeDelta = new Vector2(260f, 32f);

            var close = UiBuilder.NewButton(panel.transform, "Close", "btn_close", "X", 18);
            close.onClick.AddListener(Close);
            var cr = close.GetComponent<RectTransform>(); cr.anchorMin = cr.anchorMax = new Vector2(1f,1f); cr.pivot = new Vector2(1f,1f);
            cr.anchoredPosition = new Vector2(-18f,-18f); cr.sizeDelta = new Vector2(42f,42f);

            // BUY column
            var buyHdr = UiBuilder.NewText(panel.transform, "BuyHdr", "BUY", 14, TextAnchor.UpperLeft, LitIsoTheme.Gold);
            Place(buyHdr.rectTransform, 32f, -78f, 200f, 22f);
            var buyContent = BuildScroll(panel.transform, new Vector2(20f, 20f), new Vector2(-512f, -108f));
            foreach (var it in BuyStock) BuildRow(buyContent, it, true);

            // SELL column
            var sellHdr = UiBuilder.NewText(panel.transform, "SellHdr", "SELL", 14, TextAnchor.UpperLeft, LitIsoTheme.Gold);
            Place(sellHdr.rectTransform, 512f, -78f, 200f, 22f);
            var sellContent = BuildScroll(panel.transform, new Vector2(512f, 20f), new Vector2(-20f, -108f));
            foreach (var it in SellStock) BuildRow(sellContent, it, false);
        }

        void BuildRow(RectTransform parent, Item it, bool buy)
        {
            var row = UiBuilder.NewPanel(parent, "Row_" + it.name, "system_row", UiBuilder.SlotBg);
            var le = row.gameObject.AddComponent<LayoutElement>(); le.preferredHeight = 60f; le.minHeight = 60f;

            // icon tile with rarity edge
            var tile = UiBuilder.NewImage(row.transform, "Tile", null, LitIsoTheme.Hex(it.icon));
            var tr = tile.rectTransform; tr.anchorMin = tr.anchorMax = new Vector2(0f,0.5f); tr.pivot = new Vector2(0f,0.5f);
            tr.anchoredPosition = new Vector2(12f,0f); tr.sizeDelta = new Vector2(40f,40f);
            var ol = tile.gameObject.AddComponent<Outline>(); ol.effectColor = LitIsoTheme.Hex(it.rarity); ol.effectDistance = new Vector2(2f,-2f); ol.useGraphicAlpha = false;

            string label = it.stack > 0 ? $"{it.name}  ×{it.stack}" : it.name;
            var name = UiBuilder.NewText(row.transform, "Name", label, 15, TextAnchor.MiddleLeft, LitIsoTheme.Parchment);
            var nr = name.rectTransform; nr.anchorMin = new Vector2(0f,0f); nr.anchorMax = new Vector2(1f,1f);
            nr.offsetMin = new Vector2(62f,4f); nr.offsetMax = new Vector2(-150f,-4f);
            UiBuilder.FitText(name);

            var price = UiBuilder.NewText(row.transform, "Price", $"{it.price} G", 15, TextAnchor.MiddleRight, LitIsoTheme.GoldLit);
            var prc = price.rectTransform; prc.anchorMin = new Vector2(1f,0f); prc.anchorMax = new Vector2(1f,1f); prc.pivot = new Vector2(1f,0.5f);
            prc.anchoredPosition = new Vector2(-14f,0f); prc.sizeDelta = new Vector2(130f,0f);

            var btn = row.gameObject.AddComponent<Button>(); btn.targetGraphic = row;
            btn.interactable = false; // live shop binding pending — render disabled, not a dead click
        }

        RectTransform BuildScroll(Transform parent, Vector2 offMin, Vector2 offMax)
        {
            var root = UiBuilder.NewRect("Scroll", parent);
            root.anchorMin = Vector2.zero; root.anchorMax = Vector2.one; root.offsetMin = offMin; root.offsetMax = offMax;
            var scroll = root.gameObject.AddComponent<ScrollRect>(); scroll.horizontal = false; scroll.vertical = true; scroll.scrollSensitivity = 34f;
            var viewport = UiBuilder.NewRect("Viewport", root); UiBuilder.Stretch(viewport);
            viewport.gameObject.AddComponent<Image>().color = new Color(0f,0f,0f,0.001f);
            viewport.gameObject.AddComponent<RectMask2D>();
            var content = UiBuilder.NewRect("Content", viewport);
            content.anchorMin = new Vector2(0f,1f); content.anchorMax = new Vector2(1f,1f); content.pivot = new Vector2(0.5f,1f); content.sizeDelta = Vector2.zero;
            var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 8f; layout.padding = new RectOffset(6,6,6,6);
            layout.childControlWidth = true; layout.childControlHeight = false; layout.childForceExpandWidth = true; layout.childForceExpandHeight = false;
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
