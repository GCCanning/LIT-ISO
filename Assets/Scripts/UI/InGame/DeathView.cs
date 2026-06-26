using UnityEngine;
using UnityEngine.UI;

namespace LitIso.UI.InGame
{
    /// <summary>
    /// DEATH / RESPAWN — net-new screen from the LIT-ISO front-end design.
    /// Dark overlay, "YOU DIED", a run recap, and Respawn / Quit actions.
    /// Demo trigger F3; call <see cref="Show"/> from the real death event.
    /// </summary>
    public sealed class DeathView : MonoBehaviour
    {
        static DeathView s_instance;
        Canvas _canvas;
        bool IsOpen => _canvas != null;

        static readonly (string k, string v)[] Recap =
        {
            ("SURVIVED","Day 4 of 7"), ("TIME PLAYED","2h 18m"), ("REGION","Emberfall Woods"),
            ("MONSTERS SLAIN","37"), ("DEEPEST DEPTH","-240m · Ashen Mines"), ("GOLD ON HAND","1,284 G"),
        };

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            if (s_instance != null) return;
            var go = new GameObject("DeathView");
            s_instance = go.AddComponent<DeathView>();
            DontDestroyOnLoad(go);
        }

        /// <summary>Show the death screen (wire to the real death event).</summary>
        public static void Show() { if (s_instance != null) s_instance.Open(); }

        void Update()
        {
            if (Input.GetKeyDown(KeyCode.F3)) { if (IsOpen) Close(); else Open(); }
        }

        void Close()
        {
            if (_canvas != null) Destroy(_canvas.gameObject);
            _canvas = null;
            IsoCore.Foundation.FoundationUiCoordinator.Active?.SetModalOpen("death", false);
        }

        void OnDestroy()
        {
            IsoCore.Foundation.FoundationUiCoordinator.Active?.SetModalOpen("death", false);
        }

        void Open()
        {
            if (IsOpen) return;
            EnsureEventSystem();
            _canvas = UiBuilder.NewCanvas(transform, "DeathCanvas", 245);
            IsoCore.Foundation.FoundationUiCoordinator.Active?.SetModalOpen("death", true);
            var root = UiBuilder.NewRect("Root", _canvas.transform);
            UiBuilder.Stretch(root);
            // heavy dark scrim
            var scrim = UiBuilder.NewImage(root, "Scrim", null, new Color(0.02f, 0.01f, 0.02f, 0.88f));
            UiBuilder.Stretch(scrim.rectTransform);

            var died = UiBuilder.NewText(root, "Died", "YOU DIED", 56, TextAnchor.UpperCenter, LitIsoTheme.Red);
            var dr = died.rectTransform; dr.anchorMin = dr.anchorMax = new Vector2(0.5f, 0.5f); dr.pivot = new Vector2(0.5f, 0.5f);
            dr.anchoredPosition = new Vector2(0f, 230f); dr.sizeDelta = new Vector2(900f, 90f);

            var panel = UiBuilder.NewPanel(root, "Panel", "system_panel", LitIsoTheme.Panel);
            var pr = panel.rectTransform;
            pr.anchorMin = pr.anchorMax = new Vector2(0.5f, 0.5f); pr.pivot = new Vector2(0.5f, 0.5f);
            pr.anchoredPosition = new Vector2(0f, -30f); pr.sizeDelta = new Vector2(560f, 380f);

            float y = -22f;
            foreach (var r in Recap)
            {
                var row = UiBuilder.NewRect("R_" + r.k, panel.transform);
                row.anchorMin = row.anchorMax = new Vector2(0.5f,1f); row.pivot = new Vector2(0.5f,1f);
                row.anchoredPosition = new Vector2(0f, y); row.sizeDelta = new Vector2(490f, 34f);
                var k = UiBuilder.NewText(row, "K", r.k, 13, TextAnchor.MiddleLeft, LitIsoTheme.WarmTan);
                UiBuilder.Stretch(k.rectTransform, 0f);
                var v = UiBuilder.NewText(row, "V", r.v, 15, TextAnchor.MiddleRight, LitIsoTheme.Parchment);
                UiBuilder.Stretch(v.rectTransform, 0f);
                y -= 40f;
            }

            var respawn = UiBuilder.NewButton(panel.transform, "Respawn", "craft_button", "Respawn", 16, LitIsoTheme.ButtonStyle.Gold);
            respawn.onClick.AddListener(Close);
            var rr2 = respawn.GetComponent<RectTransform>(); rr2.anchorMin = rr2.anchorMax = new Vector2(0.5f,0f); rr2.pivot = new Vector2(1f,0f);
            rr2.anchoredPosition = new Vector2(-8f, 22f); rr2.sizeDelta = new Vector2(230f, 52f);

            var quit = UiBuilder.NewButton(panel.transform, "Quit", "craft_row", "Quit to Menu", 15, LitIsoTheme.ButtonStyle.Stone);
            quit.onClick.AddListener(Close);
            var qr = quit.GetComponent<RectTransform>(); qr.anchorMin = qr.anchorMax = new Vector2(0.5f,0f); qr.pivot = new Vector2(0f,0f);
            qr.anchoredPosition = new Vector2(8f, 22f); qr.sizeDelta = new Vector2(230f, 52f);
        }

        static void EnsureEventSystem()
        {
            if (FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
                new GameObject("EventSystem", typeof(UnityEngine.EventSystems.EventSystem), typeof(UnityEngine.EventSystems.StandaloneInputModule));
        }
    }
}
