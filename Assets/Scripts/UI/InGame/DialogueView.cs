// PHASE_COMPLETE Phase5
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace LitIso.UI.InGame
{
    /// <summary>
    /// One NPC dialogue line (or choice) surfaced to the view.
    /// Foundation-free — the adapter maps runtime dialogue state onto this.
    /// </summary>
    public struct DialogueLine
    {
        public string speakerName;  // "Elder Mira" / "" for narrator
        public string bodyText;     // the spoken line
    }

    public struct DialogueChoice
    {
        public string label;        // choice button text
        public Action onSelect;     // callback when the player picks this
    }

    /// <summary>
    /// Pixel-art dialogue overlay: semi-transparent Base backdrop, centred stone card,
    /// speaker name in gold (Press Start 2P), body in Pixelify Sans parchment,
    /// scrollable body area, and up to 4 choice buttons.
    ///
    /// Grammar: 3px ink border + 1px steel inset + 1px gold hairline + gold corner studs.
    /// 6px hard bottom shadow on the Confirm / choice buttons.
    ///
    /// Usage:
    ///   dialogueView.Show(new DialogueLine { speakerName="Mira", bodyText="Welcome." });
    ///   dialogueView.SetChoices(choices);    // optional; shown only when non-empty
    ///   dialogueView.Hide();
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DialogueView : MonoBehaviour
    {
        Canvas _canvas;
        GameObject _root;
        Text _speakerText;
        Text _bodyText;
        Transform _choiceContainer;
        readonly List<Button> _choiceButtons = new List<Button>();

        static readonly Color ModalBg   = new Color(0.016f, 0.020f, 0.027f, 0.55f);
        static readonly Color CardBg    = LitIsoTheme.Panel;  // #15171C
        static readonly Color SpeakerCol= LitIsoTheme.Gold;   // #E8C468

        public bool IsOpen => _root != null && _root.activeSelf;
        public event Action Closed;

        void Awake()
        {
            Build();
            Hide();
        }

        // ---------------------------------------------------------------- public API

        /// <summary>Show the dialogue panel with the given speaker + body text.</summary>
        public void Show(DialogueLine line)
        {
            if (_speakerText != null) _speakerText.text = line.speakerName ?? "";
            if (_bodyText    != null) _bodyText.text    = line.bodyText    ?? "";
            ClearChoices();
            if (_root != null) _root.SetActive(true);
        }

        /// <summary>Replace the choice buttons (pass empty array to show none).</summary>
        public void SetChoices(DialogueChoice[] choices)
        {
            ClearChoices();
            if (choices == null || _choiceContainer == null) return;
            for (int i = 0; i < Mathf.Min(choices.Length, 4); i++)
            {
                var c = choices[i];
                var btn = LitIsoTheme.NewButton(_choiceContainer, "Choice" + i,
                    c.label ?? "", 14, LitIsoTheme.ButtonStyle.Stone);
                var btnRt = btn.GetComponent<RectTransform>();
                btnRt.sizeDelta = new Vector2(0f, 40f);
                var le = btn.gameObject.AddComponent<LayoutElement>();
                le.minHeight = 40f; le.preferredHeight = 40f;
                le.flexibleWidth = 1f;
                Action cb = c.onSelect; // capture
                btn.onClick.AddListener(() => cb?.Invoke());
                _choiceButtons.Add(btn);
            }
        }

        public void Hide()
        {
            if (_root != null) _root.SetActive(false);
            Closed?.Invoke();
        }

        // ---------------------------------------------------------------- build

        void Build()
        {
            if (_canvas != null) Destroy(_canvas.gameObject);

            var canvasGo = new GameObject("DialogueCanvas");
            canvasGo.transform.SetParent(transform, false);
            _canvas = canvasGo.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 150; // above HUD, below pause
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<GraphicRaycaster>();

            _root = new GameObject("Root", typeof(RectTransform));
            _root.transform.SetParent(_canvas.transform, false);
            var rootRt = _root.GetComponent<RectTransform>();
            rootRt.anchorMin = Vector2.zero; rootRt.anchorMax = Vector2.one;
            rootRt.offsetMin = rootRt.offsetMax = Vector2.zero;

            // ---- Semi-transparent modal scrim ----
            var scrimImg = _root.AddComponent<Image>();
            scrimImg.color = ModalBg;
            scrimImg.raycastTarget = true;

            // ---- Stone card — bottom-anchored (dialogue sits in lower third) ----
            var card = new GameObject("DialogueCard", typeof(RectTransform));
            card.transform.SetParent(_root.transform, false);
            var cardImg = card.AddComponent<Image>();
            cardImg.color = CardBg;
            var cardRt = cardImg.rectTransform;
            cardRt.anchorMin = new Vector2(0.05f, 0f);
            cardRt.anchorMax = new Vector2(0.95f, 0f);
            cardRt.pivot = new Vector2(0.5f, 0f);
            cardRt.anchoredPosition = new Vector2(0f, 40f);
            cardRt.sizeDelta = new Vector2(0f, 300f);

            // 3px ink border
            var inkOutline = card.AddComponent<Outline>();
            inkOutline.effectColor = LitIsoTheme.Base;
            inkOutline.effectDistance = new Vector2(3f, -3f);
            inkOutline.useGraphicAlpha = false;

            // 1px steel inset
            AddInsetLayer(cardRt, LitIsoTheme.Stone, 0.6f, 3f, 1f);

            // 1px gold hairline
            AddInsetLayer(cardRt, LitIsoTheme.GoldDeep, 0.5f, 5f, 1f);

            // Gold corner studs
            AddCornerStud(cardRt, new Vector2(0f, 1f), new Vector2(7f, -7f));
            AddCornerStud(cardRt, new Vector2(1f, 1f), new Vector2(-7f, -7f));
            AddCornerStud(cardRt, new Vector2(0f, 0f), new Vector2(7f, 7f));
            AddCornerStud(cardRt, new Vector2(1f, 0f), new Vector2(-7f, 7f));

            // 6px hard bottom shadow (sibling, drawn behind card)
            var shadowGo = new GameObject("CardShadow", typeof(RectTransform));
            shadowGo.transform.SetParent(_root.transform, false);
            var shadowImg = shadowGo.AddComponent<Image>();
            shadowImg.color = LitIsoTheme.Base;
            shadowImg.raycastTarget = false;
            var shadowRt = shadowImg.rectTransform;
            shadowRt.anchorMin = cardRt.anchorMin; shadowRt.anchorMax = cardRt.anchorMax;
            shadowRt.pivot = cardRt.pivot;
            shadowRt.anchoredPosition = cardRt.anchoredPosition + new Vector2(0f, -6f);
            shadowRt.sizeDelta = cardRt.sizeDelta;
            shadowGo.transform.SetAsFirstSibling();

            // ---- Portrait diamond (top-left corner of card) ----
            var portrait = new GameObject("Portrait", typeof(RectTransform));
            portrait.transform.SetParent(cardRt, false);
            var portImg = portrait.AddComponent<Image>();
            portImg.color = LitIsoTheme.Panel2;
            portImg.raycastTarget = false;
            var portOutline = portrait.AddComponent<Outline>();
            portOutline.effectColor = LitIsoTheme.Gold;
            portOutline.effectDistance = new Vector2(2f, -2f);
            portOutline.useGraphicAlpha = false;
            var portRt = portImg.rectTransform;
            portRt.anchorMin = portRt.anchorMax = new Vector2(0f, 1f);
            portRt.pivot = new Vector2(0f, 1f);
            portRt.anchoredPosition = new Vector2(18f, -18f);
            portRt.sizeDelta = new Vector2(64f, 64f);
            portrait.transform.localEulerAngles = new Vector3(0f, 0f, 45f);

            // ---- Speaker name — gold, Press Start 2P ----
            _speakerText = NewText(cardRt, "Speaker", "SPEAKER", 16, TextAnchor.UpperLeft);
            LitIsoTheme.ApplyDisplay(_speakerText, 16, SpeakerCol);
            _speakerText.raycastTarget = false;
            var spr = _speakerText.rectTransform;
            spr.anchorMin = new Vector2(0f, 1f); spr.anchorMax = new Vector2(1f, 1f);
            spr.pivot = new Vector2(0f, 1f);
            spr.anchoredPosition = new Vector2(100f, -14f);
            spr.sizeDelta = new Vector2(-120f, 24f);

            // Gold divider below speaker name
            var div = new GameObject("Divider", typeof(RectTransform));
            div.transform.SetParent(cardRt, false);
            var divImg = div.AddComponent<Image>();
            divImg.color = new Color(LitIsoTheme.Gold.r, LitIsoTheme.Gold.g, LitIsoTheme.Gold.b, 0.30f);
            divImg.raycastTarget = false;
            var divRt = divImg.rectTransform;
            divRt.anchorMin = new Vector2(0.02f, 1f); divRt.anchorMax = new Vector2(0.98f, 1f);
            divRt.pivot = new Vector2(0.5f, 1f);
            divRt.anchoredPosition = new Vector2(0f, -42f);
            divRt.sizeDelta = new Vector2(0f, 1f);

            // ---- Body text (Pixelify Sans, parchment) ----
            // Scroll view so long lines wrap without overflow.
            var scrollRoot = new GameObject("BodyScroll", typeof(RectTransform));
            scrollRoot.transform.SetParent(cardRt, false);
            var scrollRt = scrollRoot.GetComponent<RectTransform>();
            scrollRt.anchorMin = new Vector2(0f, 0f); scrollRt.anchorMax = new Vector2(1f, 1f);
            scrollRt.offsetMin = new Vector2(18f, 60f); scrollRt.offsetMax = new Vector2(-18f, -52f);
            var scroll = scrollRoot.AddComponent<ScrollRect>();
            scroll.horizontal = false; scroll.vertical = true;

            var viewport = new GameObject("Viewport", typeof(RectTransform));
            viewport.transform.SetParent(scrollRoot.transform, false);
            viewport.AddComponent<RectMask2D>();
            var viewRt = viewport.GetComponent<RectTransform>();
            viewRt.anchorMin = Vector2.zero; viewRt.anchorMax = Vector2.one;
            viewRt.offsetMin = viewRt.offsetMax = Vector2.zero;
            scroll.viewport = viewRt;

            var content = new GameObject("Content", typeof(RectTransform));
            content.transform.SetParent(viewport.transform, false);
            var contRt = content.GetComponent<RectTransform>();
            contRt.anchorMin = new Vector2(0f, 1f); contRt.anchorMax = new Vector2(1f, 1f);
            contRt.pivot = new Vector2(0.5f, 1f);
            var csf = content.AddComponent<ContentSizeFitter>();
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.content = contRt;

            _bodyText = NewText(contRt, "Body",
                "Dialogue body text appears here. Long lines will wrap within the card.",
                18, TextAnchor.UpperLeft);
            LitIsoTheme.ApplyBody(_bodyText, 18, LitIsoTheme.Parchment);
            _bodyText.horizontalOverflow = HorizontalWrapMode.Wrap;
            _bodyText.verticalOverflow   = VerticalWrapMode.Overflow;
            _bodyText.raycastTarget = false;
            var btr = _bodyText.rectTransform;
            btr.anchorMin = Vector2.zero; btr.anchorMax = Vector2.one;
            btr.offsetMin = btr.offsetMax = Vector2.zero;

            // ---- Choice buttons (VerticalLayoutGroup in the bottom band) ----
            var choicePanel = new GameObject("Choices", typeof(RectTransform));
            choicePanel.transform.SetParent(cardRt, false);
            _choiceContainer = choicePanel.transform;
            var cpRt = choicePanel.GetComponent<RectTransform>();
            cpRt.anchorMin = new Vector2(0f, 0f); cpRt.anchorMax = new Vector2(1f, 0f);
            cpRt.pivot = new Vector2(0.5f, 0f);
            cpRt.anchoredPosition = new Vector2(0f, 10f);
            cpRt.sizeDelta = new Vector2(-36f, 0f);
            var vlg = choicePanel.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 6f;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.childControlHeight = true;
            var csf2 = choicePanel.AddComponent<ContentSizeFitter>();
            csf2.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        }

        void ClearChoices()
        {
            foreach (var b in _choiceButtons)
                if (b != null) Destroy(b.gameObject);
            _choiceButtons.Clear();
        }

        // ---- Frame helpers (3px ink / 1px steel / 1px gold hairline) ----

        static void AddInsetLayer(RectTransform parent, Color color, float alpha, float offset, float thickness)
        {
            var go = new GameObject("Inset", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.color = new Color(color.r, color.g, color.b, alpha);
            img.raycastTarget = false;
            var rt = img.rectTransform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(offset, offset);
            rt.offsetMax = new Vector2(-offset, -offset);
            // Hollow out the centre so only the border line shows.
            var fill = new GameObject("Fill", typeof(RectTransform));
            fill.transform.SetParent(rt, false);
            var fillImg = fill.AddComponent<Image>();
            fillImg.color = LitIsoTheme.Panel;
            fillImg.raycastTarget = false;
            var fr = fillImg.rectTransform;
            fr.anchorMin = Vector2.zero; fr.anchorMax = Vector2.one;
            fr.offsetMin = new Vector2(thickness, thickness);
            fr.offsetMax = new Vector2(-thickness, -thickness);
            go.transform.SetAsFirstSibling();
        }

        static void AddCornerStud(RectTransform parent, Vector2 anchor, Vector2 offset)
        {
            const float half = 5f;
            var go = new GameObject("Stud", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.color = LitIsoTheme.GoldLit;
            img.raycastTarget = false;
            var rt = img.rectTransform;
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = offset;
            rt.sizeDelta = new Vector2(half * 2f, half * 2f);
            rt.localEulerAngles = new Vector3(0f, 0f, 45f);
        }

        static Text NewText(Transform parent, string name, string value, int size, TextAnchor anchor)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var t = go.AddComponent<Text>();
            t.text = value;
            t.alignment = anchor;
            t.color = LitIsoTheme.Parchment;
            LitIsoFont.Apply(t, size);
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            var shadow = go.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.82f);
            shadow.effectDistance = new Vector2(1.5f, -1.5f);
            shadow.useGraphicAlpha = true;
            return t;
        }
    }
}
