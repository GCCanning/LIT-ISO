using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace LitIso.UI.InGame
{
    /// <summary>
    /// LOOT PICKUP TOASTS — net-new feedback element from the LIT-ISO front-end
    /// design. A bottom-right stack of transient pickup toasts (icon, name×qty,
    /// rarity) that rise and fade. Demo trigger F4; call <see cref="ShowLoot"/>
    /// when an item is actually picked up.
    /// </summary>
    public sealed class LootToastView : MonoBehaviour
    {
        static LootToastView s_instance;
        Canvas _canvas;
        RectTransform _stack;

        struct Drop { public string name, icon, rarity, rl; public int qty;
            public Drop(string n, int q, string ic, string r, string l){ name=n; qty=q; icon=ic; rarity=r; rl=l; } }

        static readonly Drop[] Sample =
        {
            new Drop("Frostfang Pelt",1,"#bcd2e8","#4f8ad9","Rare"),
            new Drop("Ember Rune",2,"#e8852e","#a060d9","Epic"),
            new Drop("Iron Ingot",3,"#aeb4bc","#5a6068","Common"),
            new Drop("Ash Berries",7,"#9a3a5a","#5a6068","Common"),
            new Drop("Mana Crystal",1,"#5f9ae8","#4f8ad9","Rare"),
        };

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            if (s_instance != null) return;
            var go = new GameObject("LootToastView");
            s_instance = go.AddComponent<LootToastView>();
            DontDestroyOnLoad(go);
        }

        /// <summary>Pop a pickup toast (wire to real item pickups).</summary>
        public static void ShowLoot(string name, int qty, Color icon, Color rarity, string rarityLabel)
        {
            if (s_instance == null) return;
            s_instance.EnsureCanvas();
            s_instance.Spawn(name, qty, icon, rarity, rarityLabel);
        }

        void Update()
        {
            if (Input.GetKeyDown(KeyCode.F4))
            {
                EnsureCanvas();
                foreach (var d in Sample)
                    Spawn(d.name, d.qty, LitIsoTheme.Hex(d.icon), LitIsoTheme.Hex(d.rarity), d.rl);
            }
        }

        void EnsureCanvas()
        {
            if (_canvas != null) return;
            _canvas = UiBuilder.NewCanvas(transform, "LootToastCanvas", 250);
            _stack = UiBuilder.NewRect("Stack", _canvas.transform);
            _stack.anchorMin = _stack.anchorMax = new Vector2(1f, 0f);
            _stack.pivot = new Vector2(1f, 0f);
            _stack.anchoredPosition = new Vector2(-24f, 120f);
            _stack.sizeDelta = new Vector2(340f, 600f);
            var layout = _stack.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 8f; layout.childAlignment = TextAnchor.LowerRight;
            layout.childControlWidth = true; layout.childControlHeight = false;
            layout.childForceExpandWidth = true; layout.childForceExpandHeight = false;
        }

        void Spawn(string name, int qty, Color icon, Color rarity, string rarityLabel)
        {
            var row = UiBuilder.NewPanel(_stack, "Toast", "system_row", UiBuilder.SlotBg);
            var le = row.gameObject.AddComponent<LayoutElement>(); le.preferredHeight = 56f; le.minHeight = 56f;
            var ol = row.gameObject.AddComponent<Outline>(); ol.effectColor = rarity; ol.effectDistance = new Vector2(2f,-2f); ol.useGraphicAlpha = false;

            var tile = UiBuilder.NewImage(row.transform, "Icon", null, icon);
            var tr = tile.rectTransform; tr.anchorMin = tr.anchorMax = new Vector2(0f,0.5f); tr.pivot = new Vector2(0f,0.5f);
            tr.anchoredPosition = new Vector2(10f,0f); tr.sizeDelta = new Vector2(36f,36f);

            var label = UiBuilder.NewText(row.transform, "Label", $"+{qty}  {name}", 15, TextAnchor.MiddleLeft, LitIsoTheme.Parchment);
            var lr = label.rectTransform; lr.anchorMin = new Vector2(0f,0f); lr.anchorMax = new Vector2(1f,1f); lr.offsetMin = new Vector2(56f,4f); lr.offsetMax = new Vector2(-90f,-4f);
            UiBuilder.FitText(label);

            var tag = UiBuilder.NewText(row.transform, "Tag", rarityLabel.ToUpperInvariant(), 11, TextAnchor.MiddleRight, rarity);
            var gr = tag.rectTransform; gr.anchorMin = new Vector2(1f,0f); gr.anchorMax = new Vector2(1f,1f); gr.pivot = new Vector2(1f,0.5f);
            gr.anchoredPosition = new Vector2(-10f,0f); gr.sizeDelta = new Vector2(80f,0f);

            var cg = row.gameObject.AddComponent<CanvasGroup>();
            StartCoroutine(FadeAndDestroy(row.gameObject, cg));
        }

        IEnumerator FadeAndDestroy(GameObject go, CanvasGroup cg)
        {
            // rise + hold
            float t = 0f;
            while (t < 0.25f) { t += Time.unscaledDeltaTime; if (cg != null) cg.alpha = Mathf.Clamp01(t / 0.25f); yield return null; }
            yield return new WaitForSecondsRealtime(2.6f);
            t = 0f;
            while (t < 0.5f) { t += Time.unscaledDeltaTime; if (cg != null) cg.alpha = 1f - Mathf.Clamp01(t / 0.5f); yield return null; }
            if (go != null) Destroy(go);
        }

        static void EnsureEventSystem() { } // toasts are non-interactive
    }
}
