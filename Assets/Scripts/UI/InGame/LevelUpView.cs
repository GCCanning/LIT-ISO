using UnityEngine;
using UnityEngine.UI;

namespace LitIso.UI.InGame
{
    /// <summary>
    /// LEVEL UP — celebration overlay matching the approved LIT-ISO front-end design.
    /// Centered content: star kicker, level transition, stat gain cards in a row,
    /// skill unlock card, CONTINUE button. Demo trigger F2.
    /// Call Show() from the progression system.
    /// </summary>
    public sealed class LevelUpView : MonoBehaviour
    {
        static LevelUpView s_instance;
        Canvas _canvas;
        bool IsOpen => _canvas != null;

        // Sample data used when called without explicit arguments (F2 demo).
        static readonly (string label, string value)[] SampleGains =
        {
            ("Max HP", "+18"), ("Max MP", "+12"), ("STR", "+1"), ("INT", "+2"),
        };

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            if (s_instance != null) return;
            var go = new GameObject("LevelUpView");
            s_instance = go.AddComponent<LevelUpView>();
            DontDestroyOnLoad(go);
        }

        /// <summary>
        /// Show the level-up flourish. Gains are (label, value) pairs.
        /// skillName/skillDesc are optional; pass null to hide the unlock card.
        /// </summary>
        public static void Show(
            int from, int to,
            (string label, string value)[] gains = null,
            string skillName = "Flame Wall",
            string skillDesc = "Raise a searing barrier that blocks foes and burns those who cross it.")
        {
            if (s_instance != null)
                s_instance.Open(from, to, gains ?? SampleGains, skillName, skillDesc);
        }

        void Update()
        {
            if (Input.GetKeyDown(KeyCode.F2))
            {
                if (IsOpen) Close(); else Show(6, 7);
            }
            else if (IsOpen && (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.Return)))
            {
                Close();
            }
        }

        void Close()
        {
            if (_canvas != null) Destroy(_canvas.gameObject);
            _canvas = null;
            IsoCore.Foundation.FoundationUiCoordinator.Active?.SetModalOpen("levelUp", false);
        }

        void OnDestroy()
        {
            IsoCore.Foundation.FoundationUiCoordinator.Active?.SetModalOpen("levelUp", false);
        }

        void Open(int from, int to, (string label, string value)[] gains,
            string skillName, string skillDesc)
        {
            if (IsOpen) Close();
            EnsureEventSystem();

            _canvas = UiBuilder.NewCanvas(transform, "LevelUpCanvas", 240);
            IsoCore.Foundation.FoundationUiCoordinator.Active?.SetModalOpen("levelUp", true);

            var root = UiBuilder.NewRect("Root", _canvas.transform);
            UiBuilder.Stretch(root);

            // Dark scrim — closes on click.
            var scrim = UiBuilder.NewScrim(root);
            var scrimBtn = scrim.gameObject.AddComponent<Button>();
            scrimBtn.transition = Selectable.Transition.None;
            scrimBtn.onClick.AddListener(Close);

            // Centered content column — all elements laid out top-to-bottom.
            const float colW = 560f;
            var col = UiBuilder.NewRect("Content", root);
            col.anchorMin = col.anchorMax = new Vector2(0.5f, 0.5f);
            col.pivot = new Vector2(0.5f, 0.5f);
            col.anchoredPosition = Vector2.zero;

            float y = 0f; // cursor from col top, advancing downward

            // ── ★  L E V E L   U P  ★ ──────────────────────────────────────
            var kicker = UiBuilder.NewText(col, "Kicker", "★  L E V E L   U P  ★",
                13, TextAnchor.UpperCenter, LitIsoTheme.Gold);
            LitIsoTheme.ApplyDisplay(kicker, 13, LitIsoTheme.Gold);
            Row(kicker.rectTransform, colW, 22f, y); y += 22f + 12f;

            // ── Level transition row: "6  ▶  7" ────────────────────────────
            var transRow = UiBuilder.NewRect("LvlRow", col);
            Row(transRow, colW, 88f, y); y += 88f + 10f;

            // "from" — dim, large, right-aligned in left half.
            var fromTxt = UiBuilder.NewText(transRow, "From", from.ToString(),
                52, TextAnchor.MiddleRight, LitIsoTheme.WarmTan);
            LitIsoTheme.ApplyDisplay(fromTxt, 52, LitIsoTheme.WarmTan);
            var frt = fromTxt.rectTransform;
            frt.anchorMin = new Vector2(0.18f, 0f); frt.anchorMax = new Vector2(0.46f, 1f);
            frt.offsetMin = frt.offsetMax = Vector2.zero;

            // Arrow.
            var arw = UiBuilder.NewText(transRow, "Arrow", "▶",
                18, TextAnchor.MiddleCenter, LitIsoTheme.Hex("#a39e90"));
            LitIsoTheme.ApplyBody(arw, 18, LitIsoTheme.Hex("#a39e90"));
            var art = arw.rectTransform;
            art.anchorMin = new Vector2(0.46f, 0.1f); art.anchorMax = new Vector2(0.54f, 0.9f);
            art.offsetMin = art.offsetMax = Vector2.zero;

            // "to" — gold, very large, left-aligned in right half.
            var toTxt = UiBuilder.NewText(transRow, "To", to.ToString(),
                80, TextAnchor.MiddleLeft, LitIsoTheme.Gold);
            LitIsoTheme.ApplyDisplay(toTxt, 80, LitIsoTheme.Gold);
            var tot = toTxt.rectTransform;
            tot.anchorMin = new Vector2(0.54f, 0f); tot.anchorMax = new Vector2(0.82f, 1f);
            tot.offsetMin = tot.offsetMax = Vector2.zero;

            // ── Bonus subtitle ───────────────────────────────────────────────
            var bonus = UiBuilder.NewText(col, "Bonus", "+1 Skill Point  ·  +1 Attribute Point",
                16, TextAnchor.UpperCenter, LitIsoTheme.Parchment);
            LitIsoTheme.ApplyBody(bonus, 16, LitIsoTheme.Parchment);
            Row(bonus.rectTransform, colW, 24f, y); y += 24f + 20f;

            // ── Stat gain cards ──────────────────────────────────────────────
            // Four equal-width dark stone cards in a horizontal row.
            int cardCount = gains.Length;
            const float cardH = 88f;
            const float cardGap = 10f;
            float cardW = (colW - (cardCount - 1) * cardGap) / cardCount;

            for (int i = 0; i < cardCount; i++)
            {
                var card = UiBuilder.NewImage(col, "Card_" + i, null, LitIsoTheme.Panel2);
                card.raycastTarget = false;
                UiBuilder.AddPixelOutline(card.gameObject, LitIsoTheme.Stone, 2f);
                var cr = card.rectTransform;
                cr.anchorMin = cr.anchorMax = new Vector2(0f, 1f);
                cr.pivot = new Vector2(0f, 1f);
                cr.anchoredPosition = new Vector2(i * (cardW + cardGap), -y);
                cr.sizeDelta = new Vector2(cardW, cardH);

                // Label (warm tan, small, near top).
                var lbl = UiBuilder.NewText(cr, "Lbl", gains[i].label,
                    11, TextAnchor.UpperCenter, LitIsoTheme.WarmTan);
                LitIsoTheme.ApplyBody(lbl, 11, LitIsoTheme.WarmTan);
                var lr = lbl.rectTransform;
                lr.anchorMin = new Vector2(0f, 0.48f); lr.anchorMax = Vector2.one;
                lr.offsetMin = new Vector2(6f, 0f); lr.offsetMax = new Vector2(-6f, -10f);

                // Value (gold, larger, center-bottom).
                var val = UiBuilder.NewText(cr, "Val", gains[i].value,
                    22, TextAnchor.LowerCenter, LitIsoTheme.GoldLit);
                LitIsoTheme.ApplyDisplay(val, 22, LitIsoTheme.GoldLit);
                var vr = val.rectTransform;
                vr.anchorMin = Vector2.zero; vr.anchorMax = new Vector2(1f, 0.52f);
                vr.offsetMin = new Vector2(6f, 8f); vr.offsetMax = new Vector2(-6f, 0f);
            }
            y += cardH + 20f;

            // ── Skill unlock card ────────────────────────────────────────────
            if (!string.IsNullOrEmpty(skillName))
            {
                const float unlockH = 108f;
                var unlock = UiBuilder.NewImage(col, "Unlock", null, LitIsoTheme.Panel);
                unlock.raycastTarget = false;
                // Gold-deep outer border + faint gold inner accent.
                UiBuilder.AddPixelOutline(unlock.gameObject, LitIsoTheme.GoldDeep, 2f);
                var accent = UiBuilder.NewImage(unlock.rectTransform, "Accent", null,
                    new Color(LitIsoTheme.Gold.r, LitIsoTheme.Gold.g, LitIsoTheme.Gold.b, 0.28f));
                accent.raycastTarget = false;
                UiBuilder.Stretch(accent.rectTransform, 3f);

                var ur = unlock.rectTransform;
                ur.anchorMin = ur.anchorMax = new Vector2(0f, 1f);
                ur.pivot = new Vector2(0f, 1f);
                ur.anchoredPosition = new Vector2(0f, -y);
                ur.sizeDelta = new Vector2(colW, unlockH);

                // Orange diamond icon on the left.
                const float iconSz = 54f;
                var iconBg = UiBuilder.NewImage(ur, "IconBg", null, LitIsoTheme.Amber);
                iconBg.raycastTarget = false;
                UiBuilder.AddPixelOutline(iconBg.gameObject, LitIsoTheme.Hex("#5a3010"), 2f);
                var ibr = iconBg.rectTransform;
                ibr.anchorMin = ibr.anchorMax = new Vector2(0f, 0.5f);
                ibr.pivot = new Vector2(0.5f, 0.5f);
                ibr.anchoredPosition = new Vector2(20f + iconSz * 0.5f, 0f);
                ibr.sizeDelta = new Vector2(iconSz, iconSz);
                ibr.localRotation = Quaternion.Euler(0f, 0f, 45f);

                var iconDot = UiBuilder.NewImage(ibr, "Dot", null, LitIsoTheme.GoldText);
                iconDot.raycastTarget = false;
                var idr = iconDot.rectTransform;
                idr.anchorMin = idr.anchorMax = new Vector2(0.5f, 0.5f);
                idr.pivot = new Vector2(0.5f, 0.5f);
                idr.sizeDelta = new Vector2(10f, 10f);
                idr.localRotation = Quaternion.Euler(0f, 0f, 45f);

                // Text content column to the right of the icon.
                float tx = 20f + iconSz + 18f;
                float tw = colW - tx - 16f;

                var badge = UiBuilder.NewText(ur, "Badge", "NEW SKILL UNLOCKED",
                    10, TextAnchor.UpperLeft, LitIsoTheme.Green);
                LitIsoTheme.ApplyDisplay(badge, 10, LitIsoTheme.Green);
                badge.rectTransform.anchorMin = badge.rectTransform.anchorMax = new Vector2(0f, 1f);
                badge.rectTransform.pivot = new Vector2(0f, 1f);
                badge.rectTransform.anchoredPosition = new Vector2(tx, -14f);
                badge.rectTransform.sizeDelta = new Vector2(tw, 16f);

                var nameT = UiBuilder.NewText(ur, "SkillName", skillName,
                    20, TextAnchor.UpperLeft, LitIsoTheme.Parchment);
                LitIsoTheme.ApplyBody(nameT, 20, LitIsoTheme.Parchment);
                nameT.rectTransform.anchorMin = nameT.rectTransform.anchorMax = new Vector2(0f, 1f);
                nameT.rectTransform.pivot = new Vector2(0f, 1f);
                nameT.rectTransform.anchoredPosition = new Vector2(tx, -34f);
                nameT.rectTransform.sizeDelta = new Vector2(tw, 26f);

                var descT = UiBuilder.NewText(ur, "Desc", skillDesc,
                    14, TextAnchor.UpperLeft, LitIsoTheme.WarmTan);
                LitIsoTheme.ApplyBody(descT, 14, LitIsoTheme.WarmTan);
                descT.horizontalOverflow = HorizontalWrapMode.Wrap;
                descT.rectTransform.anchorMin = new Vector2(0f, 0f);
                descT.rectTransform.anchorMax = new Vector2(0f, 1f);
                descT.rectTransform.pivot = new Vector2(0f, 1f);
                descT.rectTransform.anchoredPosition = new Vector2(tx, -64f);
                descT.rectTransform.sizeDelta = new Vector2(tw, 42f);

                y += unlockH + 28f;
            }

            // ── CONTINUE ▸ ─────────────────────────────────────────────────
            var cont = UiBuilder.BuildPixelButton(col, "Continue", "CONTINUE  ▸",
                14, LitIsoTheme.ButtonStyle.Gold);
            cont.onClick.AddListener(Close);
            var contRt = cont.GetComponent<RectTransform>();
            contRt.anchorMin = contRt.anchorMax = new Vector2(0.5f, 1f);
            contRt.pivot = new Vector2(0.5f, 1f);
            contRt.anchoredPosition = new Vector2(0f, -y);
            contRt.sizeDelta = new Vector2(280f, 54f);
            y += 54f;

            col.sizeDelta = new Vector2(colW, y);
        }

        // Anchors rt to the top-left of col at a given downward offset.
        static void Row(RectTransform rt, float w, float h, float y)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(0f, -y);
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
