using UnityEngine;
using UnityEngine.UI;

namespace LitIso.UI.InGame
{
    /// <summary>
    /// BUILD / PLACE — net-new screen from the LIT-ISO front-end design.
    /// Category tabs (Camp / Crafting / Storage / Structures / Decor) over a grid
    /// of placeable items with their resource cost. Opened with P, or from the
    /// Stations Hub "Build" card. Selecting an item logs a place request; the
    /// live placement hook is the follow-up.
    /// </summary>
    public sealed class BuildView : MonoBehaviour
    {
        static BuildView s_instance;
        public KeyCode toggleKey = KeyCode.P;
        Canvas _canvas;
        int _cat;
        bool IsOpen => _canvas != null;

        static readonly string[] Cats = { "Camp", "Crafting", "Storage", "Structures", "Decor" };

        struct BItem { public string name, icon, edge, cost; public int cat;
            public BItem(string n, string ic, string e, string c, int cat){ name=n; icon=ic; edge=e; cost=c; this.cat=cat; } }

        static readonly BItem[] Items =
        {
            new BItem("Campfire","#e8852e","#8a4a18","5 Wood",0),
            new BItem("Bedroll","#7a4a5a","#46283a","4 Cloth",0),
            new BItem("Workbench","#c9a26a","#7a5e36","12 Wood",1),
            new BItem("Furnace","#e07b3a","#8a4418","20 Stone",1),
            new BItem("Chest","#8a6238","#523a20","8 Wood",2),
            new BItem("Barrel","#6b4a2a","#3a2614","6 Wood",2),
            new BItem("Wood Wall","#7a5230","#46301c","3 Wood",3),
            new BItem("Stone Wall","#7a7d84","#4a4d54","4 Stone",3),
            new BItem("Torch Post","#e8a03c","#8a5a1c","2 Wood",4),
            new BItem("Banner","#9a3030","#5a1818","3 Cloth",4),
        };

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            if (s_instance != null) return;
            var go = new GameObject("BuildView");
            s_instance = go.AddComponent<BuildView>();
            DontDestroyOnLoad(go);
        }

        public static void OpenExternal() { if (s_instance != null) s_instance.Open(); }

        void Update()
        {
            if (Input.GetKeyDown(toggleKey)) { if (IsOpen) Close(); else Open(); }
            else if (IsOpen && Input.GetKeyDown(KeyCode.Escape)) Close();
        }

        void Close()
        {
            if (_canvas != null) Destroy(_canvas.gameObject);
            _canvas = null;
            IsoCore.Foundation.FoundationUiCoordinator.Active?.SetModalOpen("build", false);
        }

        void OnDestroy()
        {
            IsoCore.Foundation.FoundationUiCoordinator.Active?.SetModalOpen("build", false);
        }

        void Open()
        {
            if (IsOpen) return;
            EnsureEventSystem();
            _canvas = UiBuilder.NewCanvas(transform, "BuildCanvas", 231);
            IsoCore.Foundation.FoundationUiCoordinator.Active?.SetModalOpen("build", true);
            Rebuild();
        }

        void Rebuild()
        {
            foreach (Transform c in _canvas.transform) Destroy(c.gameObject);
            var root = UiBuilder.NewRect("Root", _canvas.transform);
            UiBuilder.Stretch(root);
            var scrim = UiBuilder.NewScrim(root);
            var sb = scrim.gameObject.AddComponent<Button>(); sb.transition = Selectable.Transition.None; sb.onClick.AddListener(Close);

            var panel = UiBuilder.NewPanel(root, "Panel", "system_panel", LitIsoTheme.Panel);
            var pr = panel.rectTransform;
            pr.anchorMin = pr.anchorMax = new Vector2(0.5f, 0.5f); pr.pivot = new Vector2(0.5f, 0.5f);
            pr.sizeDelta = new Vector2(1000f, 700f);

            var title = UiBuilder.NewText(panel.transform, "Title", "BUILD", 28, TextAnchor.UpperLeft, LitIsoTheme.Gold);
            Place(title.rectTransform, 32f, -22f, 500f, 40f);
            var close = UiBuilder.NewButton(panel.transform, "Close", "btn_close", "X", 18);
            close.onClick.AddListener(Close);
            var cr = close.GetComponent<RectTransform>(); cr.anchorMin = cr.anchorMax = new Vector2(1f,1f); cr.pivot = new Vector2(1f,1f);
            cr.anchoredPosition = new Vector2(-18f,-18f); cr.sizeDelta = new Vector2(42f,42f);

            float tx = 32f;
            for (int i = 0; i < Cats.Length; i++)
            {
                int idx = i;
                var tabImg = UiBuilder.NewPanel(panel.transform, "Cat_" + Cats[i], "system_row",
                    i == _cat ? LitIsoTheme.GoldDeep : UiBuilder.SlotBg);
                Place(tabImg.rectTransform, tx, -74f, 170f, 38f);
                var tb = tabImg.gameObject.AddComponent<Button>(); tb.targetGraphic = tabImg;
                tb.onClick.AddListener(() => { _cat = idx; Rebuild(); });
                var l = UiBuilder.NewText(tabImg.transform, "L", Cats[i], 13, TextAnchor.MiddleCenter,
                    i == _cat ? LitIsoTheme.GoldLit : LitIsoTheme.Parchment);
                UiBuilder.Stretch(l.rectTransform, 4f); UiBuilder.FitText(l);
                tx += 182f;
            }

            var grid = BuildGrid(panel.transform, new Vector2(20f, 20f), new Vector2(-20f, -124f));
            foreach (var it in Items)
                if (it.cat == _cat) BuildCard(grid, it);
        }

        void BuildCard(RectTransform parent, BItem it)
        {
            var cell = UiBuilder.NewPanel(parent, "B_" + it.name, "system_row", UiBuilder.SlotBg);
            var tile = UiBuilder.NewImage(cell.transform, "Icon", null, LitIsoTheme.Hex(it.icon));
            var tr = tile.rectTransform; tr.anchorMin = new Vector2(0.5f,1f); tr.anchorMax = new Vector2(0.5f,1f); tr.pivot = new Vector2(0.5f,1f);
            tr.anchoredPosition = new Vector2(0f,-14f); tr.sizeDelta = new Vector2(56f,56f);
            var ol = tile.gameObject.AddComponent<Outline>(); ol.effectColor = LitIsoTheme.Hex(it.edge); ol.effectDistance = new Vector2(2f,-2f); ol.useGraphicAlpha = false;

            var name = UiBuilder.NewText(cell.transform, "Name", it.name, 14, TextAnchor.UpperCenter, LitIsoTheme.Parchment);
            var nr = name.rectTransform; nr.anchorMin = new Vector2(0f,1f); nr.anchorMax = new Vector2(1f,1f); nr.pivot = new Vector2(0.5f,1f);
            nr.anchoredPosition = new Vector2(0f,-78f); nr.sizeDelta = new Vector2(-8f,22f); UiBuilder.FitText(name);

            var cost = UiBuilder.NewText(cell.transform, "Cost", it.cost, 12, TextAnchor.LowerCenter, LitIsoTheme.GoldLit);
            var cr2 = cost.rectTransform; cr2.anchorMin = new Vector2(0f,0f); cr2.anchorMax = new Vector2(1f,0f); cr2.pivot = new Vector2(0.5f,0f);
            cr2.anchoredPosition = new Vector2(0f,8f); cr2.sizeDelta = new Vector2(-8f,20f);

            var btn = cell.gameObject.AddComponent<Button>(); btn.targetGraphic = cell;
            string n = it.name;
            btn.onClick.AddListener(() => Debug.Log($"[Build] Place: {n} (live placement hook pending)."));
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
            grid.cellSize = new Vector2(150f, 150f); grid.spacing = new Vector2(12f, 12f); grid.padding = new RectOffset(8,8,8,8);
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
