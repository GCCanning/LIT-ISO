using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace LitIso.CharacterCreator
{
    /// <summary>
    /// Character creator — two-panel layout (APPEARANCE preview left, CHARACTER
    /// options right). Options panel: Randomize, body type toggle, then per-slot
    /// item picker + colour swatches for hair/shirt/pants/shoes/eyes/skin.
    /// Preview panel: animated character with Turn and animation-cycle controls.
    /// Cosmetic only — gear is loot. Drives the LPC compositor via LayeredAppearance.
    /// </summary>
    public class CharacterCreatorUI : MonoBehaviour
    {
        public static event Action<LayeredAppearance> OnConfirmed;

        LayeredAppearance _appearance;
        CharacterLayerCatalog _cat;

        // preview
        Image _previewImage;
        CharacterCompositor.BakeResult _baked;
        int _previewRow;
        bool _previewWalking = true;
        string[] _animIds;
        int _previewAnimIndex;
        int _previewActionFrame;
        float _previewActionTimer;

        readonly List<Action> _refreshers = new();
        RectTransform _charContent;   // rebuilt on item/style change

        Text _dirLabel;
        Text _frameLabel;
        readonly List<Image> _pillImages = new();
        readonly List<Text>  _pillTexts  = new();
        RectTransform _scrollRoot;   // OptionsScroll RT — kept for RebuildOptions
        static readonly string[] DirLabels = { "NORTH", "WEST", "SOUTH", "EAST" };

        // HTML section-label colour (#a39e90) — dimmer than parchment, between
        // WarmTan and Parchment; no exact LitIsoTheme token, so define it here.
        static readonly Color LabelColor = LitIsoTheme.Hex("#a39e90");

        Action<LayeredAppearance> _confirmHandler;
        Action _onCancelled;

        public static CharacterCreatorUI Show(Action<LayeredAppearance> onConfirmed = null,
            Action onCancelled = null)
        {
            var go = new GameObject("CharacterCreatorUI");
            var ui = go.AddComponent<CharacterCreatorUI>();
            ui._onCancelled = onCancelled;
            if (onConfirmed != null)
            {
                Action<LayeredAppearance> handler = null;
                handler = a => { onConfirmed(a); OnConfirmed -= handler; };
                OnConfirmed += handler;
                ui._confirmHandler = handler; // unsubscribed on cancel so it can't fire later
            }
            return ui;
        }

        void Awake()
        {
            _cat = CharacterLayerCatalog.Instance;
            _appearance = LayeredAppearance.LoadOrDefault();
            _animIds = _cat.AnimationIds.ToArray();
            if (_animIds == null || _animIds.Length == 0) _animIds = new[] { "walk" };
            _previewAnimIndex = Mathf.Max(0, Array.IndexOf(_animIds, _cat.defaultAnimation));
            BuildUI();
            Rebake();
        }

        string CurrentAnimId => _animIds[_previewAnimIndex];

        void Update()
        {
            // Review 2026-07-03: the creator had NO exit besides CONFIRM (which
            // launches the world) — a mis-click stranded the player. ESC = cancel.
            if (Input.GetKeyDown(KeyCode.Escape)) { Cancel(); return; }

            if (_baked == null || _previewImage == null) return;
            bool cycling = (CurrentAnimId == "walk" && _previewWalking) || CurrentAnimId != "walk";
            if (cycling)
            {
                float spf = 1f / Mathf.Max(0.01f, _baked.fps);
                _previewActionTimer += Time.unscaledDeltaTime;
                while (_previewActionTimer >= spf)
                {
                    _previewActionTimer -= spf;
                    _previewActionFrame = (_previewActionFrame + 1) % Mathf.Max(1, _baked.framesPerRow);
                }
                SetPreviewSprite(_previewActionFrame);
                if (_frameLabel != null && _baked != null)
                    _frameLabel.text = $"frame {_previewActionFrame + 1}/{Mathf.Max(1, _baked.framesPerRow)}";
            }
            else SetPreviewSprite(0);
        }

        void SetPreviewSprite(int frame)
        {
            if (_baked == null || _baked.sprites.Length == 0 || _previewImage == null) return;
            int row = _baked.rowCount <= 1 ? 0 : Mathf.Clamp(_previewRow, 0, _baked.rowCount - 1);
            int f = Mathf.Clamp(frame, 0, _baked.framesPerRow - 1);
            int idx = row * _baked.framesPerRow + f;
            if (idx >= 0 && idx < _baked.sprites.Length)
                _previewImage.sprite = _baked.sprites[idx];
        }

        void Rebake()
        {
            if (_baked?.texture != null) Destroy(_baked.texture);
            _baked = CharacterCompositor.Bake(_appearance, CurrentAnimId);
            _previewActionFrame = 0;
            _previewActionTimer = 0f;
            SetPreviewSprite(0);
            foreach (var r in _refreshers) r();
        }

        // ----------------------------------------------------------------- build
        void BuildUI()
        {
            var canvasGo = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 220;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            if (FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
                new GameObject("EventSystem",
                    typeof(UnityEngine.EventSystems.EventSystem),
                    typeof(UnityEngine.EventSystems.StandaloneInputModule));

            // Full-screen backdrop
            var bg = NewImage(canvasGo.transform, "BG", LitIsoTheme.Scene);
            Stretch(bg.rectTransform);

            // ── CARD (80px H margins, 60px V margins) ──
            var cardGo = new GameObject("Card", typeof(RectTransform));
            cardGo.transform.SetParent(canvasGo.transform, false);
            var card = cardGo.GetComponent<RectTransform>();
            card.anchorMin = Vector2.zero; card.anchorMax = Vector2.one;
            card.offsetMin = new Vector2(80f, 60f);
            card.offsetMax = new Vector2(-80f, -60f);

            var cardImg = cardGo.AddComponent<Image>();
            cardImg.color = LitIsoTheme.Panel;
            var cardOl = cardGo.AddComponent<Outline>();
            cardOl.effectColor = LitIsoTheme.Stone;
            cardOl.effectDistance = new Vector2(2f, -2f);
            cardOl.useGraphicAlpha = false;
            AddGoldInnerLine(card);
            AddCornerBrackets(card);

            // ── TOP BAR (28px) ──
            var topBar = NewRect("TopBar", card);
            topBar.anchorMin = new Vector2(0, 1); topBar.anchorMax = new Vector2(1, 1);
            topBar.pivot = new Vector2(0.5f, 1);
            topBar.sizeDelta = new Vector2(0, 28f);
            topBar.anchoredPosition = Vector2.zero;
            topBar.gameObject.AddComponent<Image>().color = LitIsoTheme.Base;
            var tbH = topBar.gameObject.AddComponent<HorizontalLayoutGroup>();
            tbH.padding = new RectOffset(16, 16, 0, 0);
            tbH.spacing = 8f;
            tbH.childControlWidth = true; tbH.childControlHeight = true;
            tbH.childForceExpandWidth = false; tbH.childForceExpandHeight = true;
            var tLeft = NewText(topBar, "TBL", "LIT-ISO", 9, TextAnchor.MiddleLeft, LitIsoTheme.WarmTan, true);
            tLeft.gameObject.AddComponent<LayoutElement>().preferredWidth = 110f;
            var tMid = NewText(topBar, "TBC", "[ CHARACTER CREATION ]", 10, TextAnchor.MiddleCenter, LitIsoTheme.Gold, true);
            tMid.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
            var tRight = NewText(topBar, "TBR", "NEW WORLD", 9, TextAnchor.MiddleRight, LitIsoTheme.Amber, true);
            tRight.gameObject.AddComponent<LayoutElement>().preferredWidth = 110f;

            // Gold hairline below top bar
            var topRule = NewRect("TopRule", card);
            topRule.anchorMin = new Vector2(0, 1); topRule.anchorMax = new Vector2(1, 1);
            topRule.pivot = new Vector2(0.5f, 1);
            topRule.sizeDelta = new Vector2(0, 1f);
            topRule.anchoredPosition = new Vector2(0, -28f);
            topRule.gameObject.AddComponent<Image>().color = LitIsoTheme.GoldDeep;

            // ── MAIN ROW ──
            var main = NewRect("Main", card);
            main.anchorMin = Vector2.zero; main.anchorMax = Vector2.one;
            main.offsetMin = new Vector2(0, 0);
            main.offsetMax = new Vector2(0, -29f);
            var mainH = main.gameObject.AddComponent<HorizontalLayoutGroup>();
            mainH.spacing = 0f;
            mainH.childControlWidth = true; mainH.childControlHeight = true;
            mainH.childForceExpandWidth = false; mainH.childForceExpandHeight = true;

            BuildPreviewPanel(main);
            BuildCharacterPanel(main);
        }

        void BuildPreviewPanel(RectTransform parent)
        {
            var panel = Panel(parent, "PreviewPanel");
            var le = panel.gameObject.AddComponent<LayoutElement>();
            // Keep the preview bounded. Its VerticalLayoutGroup also reports flexible
            // width, which previously let this column consume most of the card and
            // squeezed the actual customization controls into a narrow strip.
            le.minWidth = 520f;
            le.preferredWidth = 600f;
            le.flexibleWidth = 0f;
            le.flexibleHeight = 1f;
            var v = panel.gameObject.AddComponent<VerticalLayoutGroup>();
            v.padding = new RectOffset(20, 20, 14, 14);
            v.spacing = 6f;
            v.childControlWidth = true; v.childControlHeight = true;
            v.childForceExpandWidth = true; v.childForceExpandHeight = false;

            // Mini header
            var hdr = NewText(panel.transform, "Hdr", "APPEARANCE", 11, TextAnchor.UpperLeft, LitIsoTheme.Gold, true);
            FixedH(hdr, 22f);
            Divider(panel.transform, LitIsoTheme.GoldDeep, 1f);

            // Let the avatar use the remaining vertical space instead of leaving a
            // large dead panel below the preview controls.
            var avatarHolder = NewRect("Avatar", panel.transform);
            var avLe = avatarHolder.gameObject.AddComponent<LayoutElement>();
            avLe.minHeight = 300f;
            avLe.preferredHeight = 460f;
            avLe.flexibleHeight = 1f;
            var avBg = avatarHolder.gameObject.AddComponent<Image>();
            avBg.color = LitIsoTheme.Base; avBg.raycastTarget = false;
            var avOl = avatarHolder.gameObject.AddComponent<Outline>();
            avOl.effectColor = LitIsoTheme.Stone; avOl.effectDistance = new Vector2(2f, -2f); avOl.useGraphicAlpha = false;
            var charRt = NewRect("Char", avatarHolder);
            charRt.anchorMin = new Vector2(0.5f, 0.5f); charRt.anchorMax = new Vector2(0.5f, 0.5f);
            charRt.pivot = new Vector2(0.5f, 0.5f);
            charRt.sizeDelta = new Vector2(420f, 420f);
            _previewImage = charRt.gameObject.AddComponent<Image>();
            _previewImage.preserveAspect = true;
            _previewImage.raycastTarget = false;

            // Animation pill grid (5 columns)
            var pillHolder = NewRect("Pills", panel.transform);
            pillHolder.gameObject.AddComponent<LayoutElement>().preferredHeight = 100f;
            var pillBg = pillHolder.gameObject.AddComponent<Image>();
            pillBg.color = LitIsoTheme.Base; pillBg.raycastTarget = false;
            var pillOl = pillHolder.gameObject.AddComponent<Outline>();
            pillOl.effectColor = LitIsoTheme.Stone; pillOl.effectDistance = new Vector2(2f, -2f); pillOl.useGraphicAlpha = false;
            var grid = pillHolder.gameObject.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(72f, 26f);
            grid.spacing  = new Vector2(4f, 4f);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 5;
            grid.childAlignment = TextAnchor.UpperLeft;
            grid.padding = new RectOffset(8, 8, 8, 8);

            _pillImages.Clear(); _pillTexts.Clear();
            for (int i = 0; i < _animIds.Length; i++)
            {
                int captured = i;
                string raw = _animIds[i];
                string label = raw.Length > 0 ? char.ToUpperInvariant(raw[0]) + raw.Substring(1) : raw;
                bool active = i == _previewAnimIndex;

                var pill = NewRect("Pill_" + raw, pillHolder.transform);
                var pillImg = pill.gameObject.AddComponent<Image>();
                pillImg.color = active ? LitIsoTheme.Gold : LitIsoTheme.Raised;
                var btnOl = pill.gameObject.AddComponent<Outline>();
                btnOl.effectColor = LitIsoTheme.Base; btnOl.effectDistance = new Vector2(1f, -1f);
                btnOl.useGraphicAlpha = false;
                var pillBtn = pill.gameObject.AddComponent<Button>();
                pillBtn.targetGraphic = pillImg;
                pillBtn.transition = Selectable.Transition.None;
                pillBtn.onClick.AddListener(() =>
                {
                    _previewAnimIndex = captured;
                    if (_baked?.texture != null) Destroy(_baked.texture);
                    _baked = CharacterCompositor.Bake(_appearance, CurrentAnimId);
                    _previewActionFrame = 0; _previewActionTimer = 0f;
                    foreach (var r in _refreshers) r();
                });
                var pillTxt = NewText(pill, "T", label, 9, TextAnchor.MiddleCenter,
                    active ? LitIsoTheme.GoldText : LitIsoTheme.Parchment);
                pillTxt.rectTransform.anchorMin = Vector2.zero;
                pillTxt.rectTransform.anchorMax = Vector2.one;
                pillTxt.rectTransform.offsetMin = pillTxt.rectTransform.offsetMax = Vector2.zero;
                pillTxt.resizeTextForBestFit = true;
                pillTxt.resizeTextMaxSize = 11; pillTxt.resizeTextMinSize = 9;
                pillTxt.raycastTarget = false;
                _pillImages.Add(pillImg); _pillTexts.Add(pillTxt);
            }

            // Pill refresher
            _refreshers.Add(() =>
            {
                for (int i = 0; i < _pillImages.Count && i < _animIds.Length; i++)
                {
                    bool a = i == _previewAnimIndex;
                    _pillImages[i].color = a ? LitIsoTheme.Gold : LitIsoTheme.Raised;
                    _pillTexts[i].color  = a ? LitIsoTheme.GoldText : LitIsoTheme.Parchment;
                }
            });

            // Direction row
            var dirRow = NewRect("DirRow", panel.transform);
            FixedH(dirRow, 44f);
            var dirBg = dirRow.gameObject.AddComponent<Image>();
            dirBg.color = LitIsoTheme.Base; dirBg.raycastTarget = false;
            var dirOl = dirRow.gameObject.AddComponent<Outline>();
            dirOl.effectColor = LitIsoTheme.Stone; dirOl.effectDistance = new Vector2(2f, -2f); dirOl.useGraphicAlpha = false;
            var dirH = dirRow.gameObject.AddComponent<HorizontalLayoutGroup>();
            dirH.spacing = 8f; dirH.childControlWidth = true; dirH.childControlHeight = true;
            dirH.childForceExpandWidth = false; dirH.childForceExpandHeight = true;
            dirH.padding = new RectOffset(8, 8, 4, 4);
            var turnBtn = ThemeButton(dirRow, "Turn", "◀  TURN  ▶", false, () =>
            {
                if (_baked != null && _baked.rowCount > 0)
                    _previewRow = (_previewRow + 1) % _baked.rowCount;
                if (_dirLabel != null) _dirLabel.text = DirLabels[_previewRow % DirLabels.Length];
            });
            turnBtn.GetComponent<LayoutElement>().preferredWidth = 140f;
            _dirLabel = NewText(dirRow, "Dir", DirLabels[0], 12, TextAnchor.MiddleLeft, LitIsoTheme.Parchment);
            _dirLabel.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;

            // Footer: lightweight preview status. Randomize lives in the character
            // header; a second copy here became an oversized duplicate CTA.
            var foot = NewRect("Footer", panel.transform);
            FixedH(foot, 32f);
            var fH = foot.gameObject.AddComponent<HorizontalLayoutGroup>();
            fH.spacing = 8f; fH.childControlWidth = true; fH.childControlHeight = true;
            fH.childForceExpandWidth = false; fH.childForceExpandHeight = true;
            _frameLabel = NewText(foot, "Frame", "frame 1/1", 11, TextAnchor.MiddleLeft, LitIsoTheme.WarmTan);
            _frameLabel.gameObject.AddComponent<LayoutElement>().preferredWidth = 110f;
            var previewNote = NewText(foot, "PreviewNote", "COSMETIC PREVIEW - GEAR IS LOOT",
                10, TextAnchor.MiddleRight, LabelColor, true);
            previewNote.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
        }

        void BuildCharacterPanel(RectTransform parent)
        {
            // Use explicit anchor-based layout — no VerticalLayoutGroup on the panel itself.
            // VLGs fighting with flexibleHeight are the root cause of the oversized buttons.
            var panelImg = Panel(parent, "CharacterPanel");
            var panel    = panelImg.rectTransform;
            panelImg.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;

            const float hdrH = 42f;  // header strip height
            const float ftrH = 50f;  // footer strip height
            const float padH = 12f;  // top / bottom inner padding
            const float padW = 22f;  // left / right inner padding
            const float divH = 1f;

            // ── HEADER (anchored to top, full width) ──────────────────────────
            var header = NewRect("Header", panel);
            header.anchorMin = new Vector2(0f, 1f); header.anchorMax = new Vector2(1f, 1f);
            header.pivot = new Vector2(0.5f, 1f);
            header.sizeDelta = new Vector2(-padW * 2f, hdrH);
            header.anchoredPosition = new Vector2(0f, -padH);

            var hH = header.gameObject.AddComponent<HorizontalLayoutGroup>();
            hH.spacing = 10f; hH.childControlWidth = true; hH.childControlHeight = true;
            hH.childForceExpandWidth = false; hH.childForceExpandHeight = true;

            NewText(header, "Title", "CHARACTER", 13, TextAnchor.MiddleLeft, LitIsoTheme.Gold, true)
                .gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;

            var rnd = ThemeButton(header, "Rnd", "⚄  RANDOMIZE", true, () =>
            {
                var keep = _appearance.equipped;
                _appearance = LayeredAppearance.Random(_cat);
                _appearance.equipped = keep;
                RebuildOptions();
            }, 12, true);
            var rndLe = rnd.GetComponent<LayoutElement>();
            rndLe.preferredWidth = 148f; rndLe.preferredHeight = hdrH;

            // Gold hairline below header
            var topDiv = NewRect("HDivider", panel);
            topDiv.anchorMin = new Vector2(0f, 1f); topDiv.anchorMax = new Vector2(1f, 1f);
            topDiv.pivot = new Vector2(0.5f, 1f);
            topDiv.sizeDelta = new Vector2(0f, divH);
            topDiv.anchoredPosition = new Vector2(0f, -(padH + hdrH + 2f));
            topDiv.gameObject.AddComponent<Image>().color = LitIsoTheme.GoldDeep;

            // ── FOOTER (anchored to bottom, full width) ───────────────────────
            var foot = NewRect("Footer", panel);
            foot.anchorMin = new Vector2(0f, 0f); foot.anchorMax = new Vector2(1f, 0f);
            foot.pivot = new Vector2(0.5f, 0f);
            foot.sizeDelta = new Vector2(-padW * 2f, ftrH);
            foot.anchoredPosition = new Vector2(0f, padH);

            var fH = foot.gameObject.AddComponent<HorizontalLayoutGroup>();
            fH.spacing = 12f; fH.childControlWidth = true; fH.childControlHeight = true;
            fH.childForceExpandWidth = false; fH.childForceExpandHeight = true;

            var back = ThemeButton(foot, "Back", "◂  BACK", false, Cancel, 14, true);
            var backLe = back.GetComponent<LayoutElement>();
            backLe.preferredWidth = 130f; backLe.preferredHeight = ftrH;

            var reset = ThemeButton(foot, "Reset", "RESET", false, () =>
            {
                var keep = _appearance.equipped;
                _appearance = LayeredAppearance.LoadOrDefault();
                _appearance.equipped = keep;
                RebuildOptions();
            }, 14, true);
            var resetLe = reset.GetComponent<LayoutElement>();
            resetLe.preferredWidth = 160f; resetLe.preferredHeight = ftrH;

            NewRect("Spacer", foot).gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;

            var confirm = ThemeButton(foot, "Confirm", "CONFIRM  ▸", true, Confirm, 15, true);
            var confirmLe = confirm.GetComponent<LayoutElement>();
            confirmLe.preferredWidth = 220f; confirmLe.preferredHeight = ftrH;

            // Stone hairline above footer
            var botDiv = NewRect("FDivider", panel);
            botDiv.anchorMin = new Vector2(0f, 0f); botDiv.anchorMax = new Vector2(1f, 0f);
            botDiv.pivot = new Vector2(0.5f, 0f);
            botDiv.sizeDelta = new Vector2(0f, divH);
            botDiv.anchoredPosition = new Vector2(0f, padH + ftrH + 4f);
            botDiv.gameObject.AddComponent<Image>().color = LitIsoTheme.Stone;

            // ── SCROLL AREA (fills everything between dividers) ────────────────
            float scrollTop = padH + hdrH + divH + 6f;
            float scrollBot = padH + ftrH + divH + 6f;

            _scrollRoot = NewRect("OptionsScroll", panel);
            _scrollRoot.anchorMin = Vector2.zero; _scrollRoot.anchorMax = Vector2.one;
            _scrollRoot.offsetMin = new Vector2(0f, scrollBot);
            _scrollRoot.offsetMax = new Vector2(0f, -scrollTop);

            var scroll = _scrollRoot.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false; scroll.vertical = true;
            scroll.scrollSensitivity = 40f;
            scroll.movementType = ScrollRect.MovementType.Clamped;

            var viewport = NewRect("Viewport", _scrollRoot);
            Stretch(viewport);
            viewport.offsetMax = new Vector2(-16f, 0f);
            viewport.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.001f);
            viewport.gameObject.AddComponent<RectMask2D>();

            var content = NewRect("Content", viewport);
            content.anchorMin = new Vector2(0f, 1f); content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f); content.sizeDelta = Vector2.zero;
            var vlg = content.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 14f; vlg.padding = new RectOffset((int)padW, (int)padW, 8, 16);
            vlg.childControlWidth = true; vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = false;
            var fitter = content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // Visible scrollbar: the options extend below the fold, so relying on
            // wheel input alone made shirt/pants/shoes look like missing features.
            var scrollBarImage = NewImage(_scrollRoot, "Scrollbar", LitIsoTheme.Base);
            var scrollBarRt = scrollBarImage.rectTransform;
            scrollBarRt.anchorMin = new Vector2(1f, 0f);
            scrollBarRt.anchorMax = new Vector2(1f, 1f);
            scrollBarRt.pivot = new Vector2(1f, 0.5f);
            scrollBarRt.anchoredPosition = new Vector2(-4f, 0f);
            scrollBarRt.sizeDelta = new Vector2(8f, 0f);

            var slidingArea = NewRect("SlidingArea", scrollBarRt);
            Stretch(slidingArea);
            slidingArea.offsetMin = new Vector2(1f, 2f);
            slidingArea.offsetMax = new Vector2(-1f, -2f);

            var handle = NewImage(slidingArea, "Handle", LitIsoTheme.GoldDeep);
            Stretch(handle.rectTransform);

            var scrollbar = scrollBarImage.gameObject.AddComponent<Scrollbar>();
            scrollbar.handleRect = handle.rectTransform;
            scrollbar.targetGraphic = handle;
            scrollbar.direction = Scrollbar.Direction.BottomToTop;
            scrollbar.colors = new ColorBlock
            {
                normalColor = Color.white,
                highlightedColor = LitIsoTheme.Gold,
                pressedColor = LitIsoTheme.Amber,
                selectedColor = Color.white,
                disabledColor = LitIsoTheme.Stone,
                colorMultiplier = 1f,
                fadeDuration = 0.08f
            };

            scroll.viewport = viewport;
            scroll.content = content;
            scroll.verticalScrollbar = scrollbar;
            scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;
            scroll.verticalScrollbarSpacing = 6f;

            _charContent = content;
            BuildOptions(content);
        }

        // Clear and rebuild just the scroll content (on body type change / randomize).
        void RebuildOptions()
        {
            if (_charContent == null) { Rebake(); return; }
            _refreshers.Clear();
            // Destroy children immediately so BuildOptions sees a clean slate.
            for (int i = _charContent.childCount - 1; i >= 0; i--)
                DestroyImmediate(_charContent.GetChild(i).gameObject);
            _charContent.sizeDelta = Vector2.zero;
            BuildOptions(_charContent);
            Rebake();
        }

        void BuildOptions(RectTransform content)
        {
            BuildBodyTypeToggle(content);
            Divider(content, LitIsoTheme.Raised, 1f);

            var bodyDef = _cat.Find("lpc/body");
            BuildColorOnlyRow(content, "SKIN TONE", () => bodyDef,
                () => _appearance.skinVariant, v => _appearance.skinVariant = v);
            Divider(content, LitIsoTheme.Raised, 1f);

            var bodyExtras = _cat.BodyExtras();
            if (bodyExtras.Count > 0)
            {
                ItemPickerBlockNullable(content, "BODY EXTRAS", bodyExtras,
                    () => _appearance.bodyExtraId,
                    (id, variant) => { _appearance.bodyExtraId = id; _appearance.bodyExtraVariant = variant; });
                SwatchBlock(content, "EXTRAS COLOUR",
                    () => string.IsNullOrEmpty(_appearance.bodyExtraId) ? null : _cat.Find(_appearance.bodyExtraId),
                    () => _appearance.bodyExtraVariant, v => _appearance.bodyExtraVariant = v);
                Divider(content, LitIsoTheme.Raised, 1f);
            }

            var headExtras = _cat.HeadExtras();
            if (headExtras.Count > 0)
            {
                ItemPickerBlockNullable(content, "HEAD ACCESSORIES", headExtras,
                    () => _appearance.headAccId,
                    (id, variant) => { _appearance.headAccId = id; _appearance.headAccVariant = variant; });
                SwatchBlock(content, "ACCESSORY COLOUR",
                    () => string.IsNullOrEmpty(_appearance.headAccId) ? null : _cat.Find(_appearance.headAccId),
                    () => _appearance.headAccVariant, v => _appearance.headAccVariant = v);
                Divider(content, LitIsoTheme.Raised, 1f);
            }

            BuildSlotRow(content, "HAIR", "hair",
                () => _appearance.hairId, (id, v2) => { _appearance.hairId = id; _appearance.hairVariant = v2; },
                () => _cat.Find(_appearance.hairId), () => _appearance.hairVariant, v => _appearance.hairVariant = v);
            Divider(content, LitIsoTheme.Raised, 1f);

            BuildColorOnlyRow(content, "EYES",
                () => _cat.Find(_appearance.eyesId),
                () => _appearance.eyesVariant, v => _appearance.eyesVariant = v);
            Divider(content, LitIsoTheme.Raised, 1f);

            BuildSlotRow(content, "SHIRT", "shirt",
                () => _appearance.shirtId, (id, v2) => { _appearance.shirtId = id; _appearance.shirtVariant = v2; },
                () => _cat.Find(_appearance.shirtId), () => _appearance.shirtVariant, v => _appearance.shirtVariant = v);
            Divider(content, LitIsoTheme.Raised, 1f);

            BuildSlotRow(content, "PANTS", "pants",
                () => _appearance.pantsId, (id, v2) => { _appearance.pantsId = id; _appearance.pantsVariant = v2; },
                () => _cat.Find(_appearance.pantsId), () => _appearance.pantsVariant, v => _appearance.pantsVariant = v);
            Divider(content, LitIsoTheme.Raised, 1f);

            BuildSlotRow(content, "SHOES", "shoes",
                () => _appearance.shoesId, (id, v2) => { _appearance.shoesId = id; _appearance.shoesVariant = v2; },
                () => _cat.Find(_appearance.shoesId), () => _appearance.shoesVariant, v => _appearance.shoesVariant = v);
        }

        // ---- new slot-row builders (style picker + HSV colour picker) -----------

        void BuildSlotRow(RectTransform content, string label, string slot,
            Func<string> getCurrentId, Action<string, string> setIdVariant,
            Func<ItemDef> getDef, Func<string> getVariant, Action<string> setVariant)
        {
            var items = _cat.CosmeticSlot(slot);

            var block = NewRect("SlotRow_" + label, content);
            var bv = block.gameObject.AddComponent<VerticalLayoutGroup>();
            bv.spacing = 6f; bv.childControlWidth = true; bv.childControlHeight = true;
            bv.childForceExpandWidth = true; bv.childForceExpandHeight = false;
            block.gameObject.AddComponent<ContentSizeFitter>().verticalFit =
                ContentSizeFitter.FitMode.PreferredSize;

            // Label row — two columns matching content below
            var labelRow = NewRect("LabelRow", block.transform);
            FixedH(labelRow, 22f);
            var lh = labelRow.gameObject.AddComponent<HorizontalLayoutGroup>();
            lh.spacing = 16f; lh.childControlWidth = true; lh.childControlHeight = true;
            lh.childForceExpandWidth = false; lh.childForceExpandHeight = true;
            NewText(labelRow.transform, "SL", label + " STYLE", 14, TextAnchor.MiddleLeft, LabelColor, true)
                .gameObject.AddComponent<LayoutElement>().preferredWidth = 200f;
            NewText(labelRow.transform, "CL", label + " COLOUR", 14, TextAnchor.MiddleLeft, LabelColor, true)
                .gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;

            // Content row
            var contentRow = NewRect("ContentRow", block.transform);
            contentRow.gameObject.AddComponent<ContentSizeFitter>().verticalFit =
                ContentSizeFitter.FitMode.PreferredSize;
            var ch = contentRow.gameObject.AddComponent<HorizontalLayoutGroup>();
            ch.spacing = 16f; ch.childControlWidth = true; ch.childControlHeight = true;
            ch.childForceExpandWidth = false; ch.childForceExpandHeight = true;

            if (items.Count > 0)
            {
                var leftPane = NewRect("StylePane", contentRow.transform);
                var lv = leftPane.gameObject.AddComponent<VerticalLayoutGroup>();
                lv.spacing = 4f; lv.childControlWidth = true; lv.childControlHeight = true;
                lv.childForceExpandWidth = true; lv.childForceExpandHeight = false;
                leftPane.gameObject.AddComponent<LayoutElement>().preferredWidth = 200f;
                leftPane.gameObject.AddComponent<ContentSizeFitter>().verticalFit =
                    ContentSizeFitter.FitMode.PreferredSize;
                BuildInlineItemPicker(leftPane.transform, items, getCurrentId, setIdVariant);
            }

            var rightPane = NewRect("ColorPane", contentRow.transform);
            var rightLe = rightPane.gameObject.AddComponent<LayoutElement>();
            rightLe.flexibleWidth = 1f;
            BuildInlineColorPicker(rightPane.transform, getDef, getVariant, setVariant);
        }

        void BuildColorOnlyRow(RectTransform content, string label,
            Func<ItemDef> getDef, Func<string> getVariant, Action<string> setVariant)
        {
            var block = NewRect("ColorRow_" + label, content);
            var bv = block.gameObject.AddComponent<VerticalLayoutGroup>();
            bv.spacing = 6f; bv.childControlWidth = true; bv.childControlHeight = true;
            bv.childForceExpandWidth = true; bv.childForceExpandHeight = false;
            block.gameObject.AddComponent<ContentSizeFitter>().verticalFit =
                ContentSizeFitter.FitMode.PreferredSize;

            var lbl = NewText(block.transform, "L", label, 14, TextAnchor.UpperLeft, LabelColor, true);
            FixedH(lbl, 22f);

            var colorPane = NewRect("ColorPane", block.transform);
            colorPane.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
            BuildInlineColorPicker(colorPane.transform, getDef, getVariant, setVariant);
        }

        void BuildInlineItemPicker(Transform parent, List<ItemDef> items,
            Func<string> getCurrentId, Action<string, string> setIdVariant)
        {
            if (items.Count == 0) return;
            int curIdx = Mathf.Max(0, items.FindIndex(i => i.id == getCurrentId()));

            var pickerRow = NewRect("PickerRow", parent);
            FixedH(pickerRow, 44f);
            var ph = pickerRow.gameObject.AddComponent<HorizontalLayoutGroup>();
            ph.spacing = 6f; ph.childControlWidth = true; ph.childControlHeight = true;
            ph.childForceExpandWidth = false; ph.childForceExpandHeight = true;

            var nameText = NewText(pickerRow.transform, "Name",
                items[curIdx].displayName ?? items[curIdx].id,
                16, TextAnchor.MiddleCenter, LitIsoTheme.Parchment);
            nameText.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
            nameText.resizeTextForBestFit = true;
            nameText.resizeTextMaxSize = 16; nameText.resizeTextMinSize = 10;

            var countText = NewText(parent, "Count", $"{curIdx + 1} / {items.Count}",
                12, TextAnchor.UpperRight, LabelColor);
            FixedH(countText, 18f);

            void Refresh(int idx)
            {
                var item = items[idx];
                nameText.text = item.displayName ?? item.id;
                countText.text = $"{idx + 1} / {items.Count}";
                string curV = getCurrentId() == item.id
                    ? (_cat.Find(getCurrentId())?.SafeVariant(null) ?? "") : "";
                setIdVariant(item.id, item.SafeVariant(curV));
                Rebake();
            }

            var prevBtn = ThemeButton(pickerRow, "Prev", "◀", false,
                () => { curIdx = ((curIdx - 1) + items.Count) % items.Count; Refresh(curIdx); });
            prevBtn.transform.SetSiblingIndex(0);
            prevBtn.GetComponent<LayoutElement>().preferredWidth = 44f;

            var nextBtn = ThemeButton(pickerRow, "Next", "▶", false,
                () => { curIdx = (curIdx + 1) % items.Count; Refresh(curIdx); });
            nextBtn.GetComponent<LayoutElement>().preferredWidth = 44f;

            _refreshers.Add(() =>
            {
                curIdx = Mathf.Max(0, items.FindIndex(i => i.id == getCurrentId()));
                nameText.text = items[curIdx].displayName ?? items[curIdx].id;
                countText.text = $"{curIdx + 1} / {items.Count}";
            });
        }

        void BuildInlineColorPicker(Transform parent,
            Func<ItemDef> getDef, Func<string> getVariant, Action<string> setVariant)
        {
            var block = NewRect("HSVPicker", parent);
            var bv = block.gameObject.AddComponent<VerticalLayoutGroup>();
            bv.spacing = 4f; bv.childControlWidth = true; bv.childControlHeight = true;
            bv.childForceExpandWidth = true; bv.childForceExpandHeight = false;
            block.gameObject.AddComponent<ContentSizeFitter>().verticalFit =
                ContentSizeFitter.FitMode.PreferredSize;

            // Top row: colour preview + hex InputField
            var topRow = NewRect("TopRow", block.transform);
            FixedH(topRow, 36f);
            var trh = topRow.gameObject.AddComponent<HorizontalLayoutGroup>();
            trh.spacing = 8f; trh.childControlWidth = true; trh.childControlHeight = true;
            trh.childForceExpandWidth = false; trh.childForceExpandHeight = true;

            var previewGo = NewRect("Preview", topRow.transform);
            previewGo.gameObject.AddComponent<LayoutElement>().preferredWidth = 44f;
            var previewImg = previewGo.gameObject.AddComponent<Image>();
            previewImg.raycastTarget = false;
            var previewOl = previewGo.gameObject.AddComponent<Outline>();
            previewOl.effectColor = LitIsoTheme.Base;
            previewOl.effectDistance = new Vector2(2f, -2f);
            previewOl.useGraphicAlpha = false;

            var hexIF = BuildHexInput(topRow.transform, "HexInput", "#000000");
            var hexLe = hexIF.gameObject.GetComponent<LayoutElement>() ??
                        hexIF.gameObject.AddComponent<LayoutElement>();
            hexLe.flexibleWidth = 1f;

            // H / S / V sliders
            Slider hSlider = null, sSlider = null, vSlider = null;
            BuildLabeledSliderRow(block.transform, "H", 0f, out hSlider, "HSlider");
            BuildLabeledSliderRow(block.transform, "S", 0f, out sSlider, "SSlider");
            BuildLabeledSliderRow(block.transform, "V", 0f, out vSlider, "VSlider");

            // Review 2026-07-03: ApplyFromHSV -> Rebake -> refreshers -> SyncToVariant
            // snapped the sliders to the matched variant's exact colour MID-DRAG,
            // yanking the handle out from under the pointer. While the user drives the
            // sliders, skip the slider write-back (preview + hex still update).
            bool applyingFromSliders = false;

            void SyncToVariant()
            {
                var def = getDef();
                if (def?.variants == null || def.variants.Length == 0) return;
                string vv = getVariant();
                if (string.IsNullOrEmpty(vv)) vv = def.variants[0];
                Color c = CharacterCompositor.SwatchColor(def, vv, _appearance.bodyType);
                if (!applyingFromSliders)
                {
                    Color.RGBToHSV(c, out float h, out float s, out float val);
                    hSlider?.SetValueWithoutNotify(h);
                    sSlider?.SetValueWithoutNotify(s);
                    vSlider?.SetValueWithoutNotify(val);
                }
                if (previewImg != null) previewImg.color = c;
                if (hexIF != null && !hexIF.isFocused) hexIF.text = ColorToHex(c);
            }

            void ApplyFromHSV()
            {
                var def = getDef();
                if (def?.variants == null || def.variants.Length == 0) return;
                Color target = Color.HSVToRGB(hSlider.value, sSlider.value, vSlider.value);
                string best = BestVariant(def, target, _appearance.bodyType);
                if (best != getVariant())
                {
                    applyingFromSliders = true;
                    try { setVariant(best); Rebake(); }
                    finally { applyingFromSliders = false; }
                }
                Color actual = CharacterCompositor.SwatchColor(def, getVariant(), _appearance.bodyType);
                if (previewImg != null) previewImg.color = actual;
            }

            hSlider.onValueChanged.AddListener(_ => ApplyFromHSV());
            sSlider.onValueChanged.AddListener(_ => ApplyFromHSV());
            vSlider.onValueChanged.AddListener(_ => ApplyFromHSV());

            hexIF.onEndEdit.AddListener(hex =>
            {
                if (TryParseHex(hex, out Color target))
                {
                    var def = getDef();
                    if (def?.variants != null && def.variants.Length > 0)
                    {
                        string best = BestVariant(def, target, _appearance.bodyType);
                        if (best != getVariant()) { setVariant(best); Rebake(); }
                    }
                }
                SyncToVariant();
            });

            _refreshers.Add(() => SyncToVariant());
            SyncToVariant();
        }

        void BuildLabeledSliderRow(Transform parent, string labelText, float initVal,
            out Slider slider, string sliderName)
        {
            var row = NewRect(sliderName + "Row", parent);
            FixedH(row, 22f);
            var rh = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            rh.spacing = 6f; rh.childControlWidth = true; rh.childControlHeight = true;
            rh.childForceExpandWidth = false; rh.childForceExpandHeight = true;

            var lbl = NewText(row.transform, "L", labelText, 11, TextAnchor.MiddleLeft, LabelColor);
            lbl.gameObject.AddComponent<LayoutElement>().preferredWidth = 14f;

            slider = BuildSlider(row.transform, sliderName, 0f, 1f, initVal);
            slider.GetComponent<LayoutElement>().flexibleWidth = 1f;
        }

        static Slider BuildSlider(Transform parent, string name, float minV, float maxV, float initVal)
        {
            var go = NewRect(name, parent);
            go.gameObject.AddComponent<LayoutElement>();
            var bg = go.gameObject.AddComponent<Image>();
            bg.color = new Color(0.22f, 0.22f, 0.22f, 1f);

            var fillArea = NewRect("FillArea", go);
            fillArea.anchorMin = new Vector2(0f, 0.25f);
            fillArea.anchorMax = new Vector2(1f, 0.75f);
            fillArea.offsetMin = new Vector2(6f, 0f);
            fillArea.offsetMax = new Vector2(-14f, 0f);

            var fill = NewRect("Fill", fillArea);
            fill.anchorMin = Vector2.zero;
            fill.anchorMax = new Vector2(0f, 1f);
            fill.sizeDelta = Vector2.zero;
            fill.gameObject.AddComponent<Image>().color = LitIsoTheme.Gold;

            var handleArea = NewRect("HandleArea", go);
            Stretch(handleArea);

            var handle = NewRect("Handle", handleArea);
            handle.anchorMin = new Vector2(0f, 0.05f);
            handle.anchorMax = new Vector2(0f, 0.95f);
            handle.sizeDelta = new Vector2(12f, 0f);
            handle.gameObject.AddComponent<Image>().color = LitIsoTheme.Parchment;

            var slider = go.gameObject.AddComponent<Slider>();
            slider.fillRect = fill;
            slider.handleRect = handle;
            slider.targetGraphic = bg;
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = minV; slider.maxValue = maxV; slider.value = initVal;

            var cb = slider.colors;
            cb.normalColor = Color.white;
            cb.highlightedColor = new Color(0.88f, 0.88f, 0.88f, 1f);
            cb.pressedColor = new Color(0.72f, 0.72f, 0.72f, 1f);
            cb.fadeDuration = 0.05f;
            slider.colors = cb;
            return slider;
        }

        static InputField BuildHexInput(Transform parent, string name, string initText)
        {
            var go = NewRect(name, parent);
            go.gameObject.AddComponent<LayoutElement>();
            var bg = go.gameObject.AddComponent<Image>();
            bg.color = new Color(0.18f, 0.18f, 0.18f, 1f);

            var phGo = NewRect("Placeholder", go);
            Stretch(phGo);
            phGo.offsetMin = new Vector2(6f, 2f); phGo.offsetMax = new Vector2(-6f, -2f);
            var ph = phGo.gameObject.AddComponent<Text>();
            LitIsoTheme.ApplyBody(ph, 12, LitIsoTheme.WarmTan);
            ph.text = "#rrggbb"; ph.alignment = TextAnchor.MiddleLeft;

            var textGo = NewRect("Text", go);
            Stretch(textGo);
            textGo.offsetMin = new Vector2(6f, 2f); textGo.offsetMax = new Vector2(-6f, -2f);
            var t = textGo.gameObject.AddComponent<Text>();
            LitIsoTheme.ApplyBody(t, 12, LitIsoTheme.Parchment);
            t.alignment = TextAnchor.MiddleLeft;
            t.supportRichText = false;

            var input = go.gameObject.AddComponent<InputField>();
            input.targetGraphic = bg;
            input.textComponent = t;
            input.placeholder = ph;
            input.characterLimit = 7;
            input.contentType = InputField.ContentType.Standard;
            input.text = initText;
            return input;
        }

        string BestVariant(ItemDef def, Color target, string bodyType)
        {
            if (def?.variants == null || def.variants.Length == 0) return "";
            float minD = float.MaxValue;
            string best = def.variants[0];
            foreach (var v in def.variants)
            {
                Color c = CharacterCompositor.SwatchColor(def, v, bodyType);
                float d = (c.r - target.r) * (c.r - target.r) +
                          (c.g - target.g) * (c.g - target.g) +
                          (c.b - target.b) * (c.b - target.b);
                if (d < minD) { minD = d; best = v; }
            }
            return best;
        }

        static string ColorToHex(Color c)
        {
            Color32 c32 = c;
            return $"#{c32.r:X2}{c32.g:X2}{c32.b:X2}";
        }

        static bool TryParseHex(string hex, out Color color)
        {
            color = Color.black;
            if (string.IsNullOrEmpty(hex)) return false;
            hex = hex.TrimStart('#');
            if (hex.Length != 6) return false;
            try
            {
                byte r = Convert.ToByte(hex.Substring(0, 2), 16);
                byte g = Convert.ToByte(hex.Substring(2, 2), 16);
                byte b = Convert.ToByte(hex.Substring(4, 2), 16);
                color = new Color(r / 255f, g / 255f, b / 255f);
                return true;
            }
            catch { return false; }
        }

        /// <summary>
        /// For <= 6 items in the slot: shows them as side-by-side segmented toggle buttons —
        /// the selected one is gold, others are stone. For more items falls back to the
        /// prev / name / next picker so the layout stays clean at any catalog size.
        /// </summary>
        void SegmentedItemBlock(RectTransform content, string label, string slot,
            Func<string> getCurrentId, Action<string, string> setIdVariant)
        {
            var items = _cat.CosmeticSlot(slot);
            if (items.Count == 0) return;

            // Fall back to prev/next picker when there are many style options.
            if (items.Count > 6)
            {
                ItemPickerBlock(content, label + " STYLE", slot, getCurrentId, setIdVariant);
                return;
            }

            // Skip showing a one-item "picker" — there's nothing to choose.
            if (items.Count == 1)
            {
                // Just apply the single item silently.
                if (getCurrentId() != items[0].id)
                {
                    string variant = items[0].SafeVariant("");
                    setIdVariant(items[0].id, variant);
                }
                return;
            }

            var block = NewRect("Seg_" + label, content);
            var v = block.gameObject.AddComponent<VerticalLayoutGroup>();
            v.spacing = 8f; v.childControlWidth = true; v.childControlHeight = true;
            v.childForceExpandWidth = true; v.childForceExpandHeight = false;
            block.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var lbl = NewText(block.transform, "L", label, 14, TextAnchor.UpperLeft, LabelColor, true);
            FixedH(lbl, 20f);

            var row = NewRect("Row", block.transform);
            FixedH(row, 44f);
            var h = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.spacing = 6f; h.childControlWidth = true; h.childControlHeight = true;
            h.childForceExpandWidth = false; h.childForceExpandHeight = true;

            var btnImages = new List<Image>();
            for (int i = 0; i < items.Count; i++)
            {
                var item = items[i];
                string capturedId = item.id;
                bool sel = getCurrentId() == capturedId;

                var bt = NewRect("Seg_" + i, row.transform);
                bt.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
                var btImg = bt.gameObject.AddComponent<Image>();
                btImg.color = sel ? LitIsoTheme.Gold : LitIsoTheme.Raised;
                var btOl = bt.gameObject.AddComponent<Outline>();
                btOl.effectColor = LitIsoTheme.Base; btOl.effectDistance = new Vector2(2f, -2f); btOl.useGraphicAlpha = false;

                var btn = bt.gameObject.AddComponent<Button>();
                btn.targetGraphic = btImg;
                btn.transition = Selectable.Transition.None;
                btn.onClick.AddListener(() =>
                {
                    if (getCurrentId() == capturedId) return;
                    var def = _cat.Find(capturedId);
                    string variant = def?.SafeVariant("") ?? "";
                    setIdVariant(capturedId, variant);
                    Rebake();
                });

                var btLbl = NewText(bt, "T", item.displayName ?? item.id, 16,
                    TextAnchor.MiddleCenter, sel ? LitIsoTheme.GoldText : LitIsoTheme.Parchment);
                btLbl.raycastTarget = false;
                btLbl.rectTransform.anchorMin = Vector2.zero; btLbl.rectTransform.anchorMax = Vector2.one;
                btLbl.rectTransform.offsetMin = btLbl.rectTransform.offsetMax = Vector2.zero;
                btLbl.resizeTextForBestFit = true; btLbl.resizeTextMaxSize = 16; btLbl.resizeTextMinSize = 11;

                int capturedIdx = i;
                _refreshers.Add(() =>
                {
                    bool s = getCurrentId() == item.id;
                    btImg.color = s ? LitIsoTheme.Gold : LitIsoTheme.Raised;
                    btLbl.color = s ? LitIsoTheme.GoldText : LitIsoTheme.Parchment;
                });
                btnImages.Add(btImg);
            }
        }

        /// <summary>Male / Female segmented toggle.</summary>
        void BuildBodyTypeToggle(RectTransform content)
        {
            var block = NewRect("BodyTypeBlock", content);
            var v = block.gameObject.AddComponent<VerticalLayoutGroup>();
            v.spacing = 8f; v.childControlWidth = true; v.childControlHeight = true;
            v.childForceExpandWidth = true; v.childForceExpandHeight = false;
            block.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var lbl = NewText(block.transform, "L", "BODY TYPE", 14, TextAnchor.UpperLeft, LabelColor, true);
            FixedH(lbl, 20f);

            var row = NewRect("Row", block.transform);
            FixedH(row, 44f);
            var h = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.spacing = 10f; h.childControlWidth = true; h.childControlHeight = true;
            h.childForceExpandWidth = false; h.childForceExpandHeight = true;

            var types = _cat.bodyTypes;
            var buttons = new List<Button>();
            foreach (var bt in types)
            {
                string captured = bt;
                bool sel = _appearance.bodyType == captured;
                var b = ThemeButton(row, "BT_" + bt, bt == "male" ? "Male" : "Female", sel, () =>
                {
                    if (_appearance.bodyType == captured) return;
                    _appearance.bodyType = captured;
                    RebuildOptions();
                });
                b.GetComponent<LayoutElement>().preferredWidth = 130f;
                buttons.Add(b);
            }

            // Keep toggle highlights in sync after rebuild.
            _refreshers.Add(() =>
            {
                for (int i = 0; i < buttons.Count && i < types.Length; i++)
                {
                    var img = buttons[i].GetComponent<Image>();
                    if (img != null) img.color = (_appearance.bodyType == types[i]) ? LitIsoTheme.Gold : LitIsoTheme.Raised;
                }
            });
        }

        /// <summary>
        /// A labelled item-picker row: prev / item-name / next buttons that cycle
        /// through all creator-visible items in the slot. Selecting a new item
        /// preserves the current variant if it exists, otherwise picks the first.
        /// </summary>
        void ItemPickerBlock(RectTransform content, string label, string slot,
            Func<string> getCurrentId, Action<string, string> setIdVariant)
        {
            var items = _cat.CosmeticSlot(slot);
            if (items.Count == 0) return;

            var block = NewRect("Picker_" + label, content);
            var v = block.gameObject.AddComponent<VerticalLayoutGroup>();
            v.spacing = 6f; v.childControlWidth = true; v.childControlHeight = true;
            v.childForceExpandWidth = true; v.childForceExpandHeight = false;
            block.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var lbl = NewText(block.transform, "L", label, 14, TextAnchor.UpperLeft, LabelColor, true);
            FixedH(lbl, 20f);

            var row = NewRect("Row", block.transform);
            FixedH(row, 44f);
            var h = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.spacing = 8f; h.childControlWidth = true; h.childControlHeight = true;
            h.childForceExpandWidth = false; h.childForceExpandHeight = true;

            // Find current index.
            int curIdx = Mathf.Max(0, items.FindIndex(i => i.id == getCurrentId()));

            // Name label in the centre.
            var nameText = NewText(row.transform, "Name",
                items[curIdx].displayName ?? items[curIdx].id, 16, TextAnchor.MiddleCenter, LitIsoTheme.Parchment);
            nameText.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;

            void Refresh(int idx)
            {
                var item = items[idx];
                nameText.text = item.displayName ?? item.id;
                string curVariant = getCurrentId() == item.id
                    ? _cat.Find(getCurrentId())?.SafeVariant(null) ?? ""
                    : "";
                string variant = item.SafeVariant(curVariant);
                setIdVariant(item.id, variant);
                Rebake();
            }

            // Prev button — insert BEFORE the name text.
            var prev = ThemeButton(row, "Prev", "◀", false, () =>
            {
                curIdx = ((curIdx - 1) + items.Count) % items.Count;
                Refresh(curIdx);
            });
            prev.transform.SetSiblingIndex(0);
            prev.GetComponent<LayoutElement>().preferredWidth = 44f;

            // Next button — after name.
            var next = ThemeButton(row, "Next", "▶", false, () =>
            {
                curIdx = (curIdx + 1) % items.Count;
                Refresh(curIdx);
            });
            next.GetComponent<LayoutElement>().preferredWidth = 44f;

            // Item count hint.
            var count = NewText(block.transform, "Count",
                $"{curIdx + 1} / {items.Count}", 13, TextAnchor.UpperRight, LabelColor);
            FixedH(count, 16f);

            _refreshers.Add(() =>
            {
                curIdx = Mathf.Max(0, items.FindIndex(i => i.id == getCurrentId()));
                nameText.text = items[curIdx].displayName ?? items[curIdx].id;
                count.text = $"{curIdx + 1} / {items.Count}";
            });
        }

        void ItemPickerBlockNullable(RectTransform content, string label, List<ItemDef> items,
            Func<string> getCurrentId, Action<string, string> setIdVariant)
        {
            // Prepend a "None" synthetic entry
            var list = new List<(string id, string name)> { ("", "None") };
            foreach (var it in items) list.Add((it.id, it.displayName ?? it.id));

            var block = NewRect("NullPicker_" + label, content);
            var bv = block.gameObject.AddComponent<VerticalLayoutGroup>();
            bv.spacing = 6f; bv.childControlWidth = true; bv.childControlHeight = true;
            bv.childForceExpandWidth = true; bv.childForceExpandHeight = false;
            block.gameObject.AddComponent<ContentSizeFitter>().verticalFit =
                ContentSizeFitter.FitMode.PreferredSize;

            var lbl = NewText(block.transform, "L", label, 13, TextAnchor.UpperLeft, LabelColor, true);
            FixedH(lbl, 18f);

            var row = NewRect("Row", block.transform);
            FixedH(row, 36f);
            var rh = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            rh.spacing = 8f; rh.childControlWidth = true; rh.childControlHeight = true;
            rh.childForceExpandWidth = false; rh.childForceExpandHeight = true;

            int curIdx = Mathf.Max(0, list.FindIndex(x => x.id == (getCurrentId() ?? "")));

            var nameText = NewText(row.transform, "Name", list[curIdx].name, 14,
                TextAnchor.MiddleCenter, LitIsoTheme.Parchment);
            nameText.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;

            void Refresh(int idx)
            {
                string id = list[idx].id;
                nameText.text = list[idx].name;
                if (string.IsNullOrEmpty(id)) { setIdVariant("", ""); Rebake(); return; }
                var def = _cat.Find(id);
                string curV = getCurrentId() == id ? (def?.SafeVariant(null) ?? "") : "";
                setIdVariant(id, def?.SafeVariant(curV) ?? "");
                Rebake();
            }

            var prev = ThemeButton(row, "Prev", "◀", false, () =>
            {
                curIdx = ((curIdx - 1) + list.Count) % list.Count; Refresh(curIdx);
            });
            prev.transform.SetSiblingIndex(0);
            prev.GetComponent<LayoutElement>().preferredWidth = 36f;

            var next = ThemeButton(row, "Next", "▶", false, () =>
            {
                curIdx = (curIdx + 1) % list.Count; Refresh(curIdx);
            });
            next.GetComponent<LayoutElement>().preferredWidth = 36f;

            var cnt = NewText(block.transform, "Cnt", $"{curIdx} / {list.Count - 1}", 12,
                TextAnchor.UpperRight, LabelColor);
            FixedH(cnt, 16f);

            _refreshers.Add(() =>
            {
                curIdx = Mathf.Max(0, list.FindIndex(x => x.id == (getCurrentId() ?? "")));
                nameText.text = list[curIdx].name;
                cnt.text = $"{curIdx} / {list.Count - 1}";
            });
        }

        // A labelled block with a wrapping grid of colour swatches for one def.
        void SwatchBlock(RectTransform content, string label,
            Func<ItemDef> getDef, Func<string> getVariant, Action<string> setVariant)
        {
            var def = getDef();

            var block = NewRect("Block_" + label, content);
            var v = block.gameObject.AddComponent<VerticalLayoutGroup>();
            v.spacing = 8f; v.childControlWidth = true; v.childControlHeight = true;
            v.childForceExpandWidth = true; v.childForceExpandHeight = false;
            var fit = block.gameObject.AddComponent<ContentSizeFitter>();
            fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var lbl = NewText(block.transform, "L", label, 14, TextAnchor.UpperLeft, LabelColor, true);
            FixedH(lbl, 20f);

            if (def == null || def.variants == null || def.variants.Length == 0)
            {
                var none = NewText(block.transform, "None", "—", 14, TextAnchor.UpperLeft, LitIsoTheme.WarmTan);
                FixedH(none, 24f);
                return;
            }

            // HTML: 46x46 swatches, 12px gap, wrapping flex row.
            var grid = NewRect("Grid", block.transform);
            var g = grid.gameObject.AddComponent<GridLayoutGroup>();
            g.cellSize = new Vector2(46f, 46f);
            g.spacing = new Vector2(12f, 12f);
            g.padding = new RectOffset(0, 0, 0, 0);
            var gfit = grid.gameObject.AddComponent<ContentSizeFitter>();
            gfit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var variants = def.variants;
            var outlines = new List<Outline>(variants.Length);
            for (int i = 0; i < variants.Length; i++)
            {
                string variant = variants[i];
                var cell = NewRect("Sw", grid);
                var img = cell.gameObject.AddComponent<Image>();
                img.color = CharacterCompositor.SwatchColor(def, variant, _appearance.bodyType);
                var ol = cell.gameObject.AddComponent<Outline>();
                ol.effectColor = LitIsoTheme.Base; ol.effectDistance = new Vector2(3f, -3f); ol.useGraphicAlpha = false;
                outlines.Add(ol);
                var btn = cell.gameObject.AddComponent<Button>();
                btn.targetGraphic = img;
                string captured = variant;
                btn.onClick.AddListener(() =>
                {
                    if (captured == getVariant()) return;
                    setVariant(captured);
                    Rebake();
                });
            }

            _refreshers.Add(() =>
            {
                string cur = getVariant();
                for (int i = 0; i < outlines.Count && i < variants.Length; i++)
                {
                    bool s = variants[i] == cur;
                    outlines[i].effectColor = s ? LitIsoTheme.Gold : LitIsoTheme.Base;
                    outlines[i].effectDistance = s ? new Vector2(4f, -4f) : new Vector2(3f, -3f);
                }
            });
        }

        void Confirm()
        {
            _appearance.Save();
            OnConfirmed?.Invoke(_appearance.Clone());
            var anim = FindFirstObjectByType<LayeredCharacterAnimator>();
            if (anim != null) anim.Apply(_appearance.Clone());
            Destroy(gameObject);
        }

        void Cancel()
        {
            if (_confirmHandler != null) { OnConfirmed -= _confirmHandler; _confirmHandler = null; }
            _onCancelled?.Invoke();
            Destroy(gameObject);
        }

        void OnDestroy()
        {
            // Review 2026-07-03: the last baked spritesheet texture leaked on close
            // (Rebake destroyed the previous one, nothing destroyed the final one).
            if (_baked?.texture != null) Destroy(_baked.texture);
        }

        // ---------------------------------------------------------------- helpers
        static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            return rt;
        }

        static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        static void FixedH(Component c, float h)
        {
            var le = c.gameObject.GetComponent<LayoutElement>() ?? c.gameObject.AddComponent<LayoutElement>();
            le.minHeight = h;
            le.preferredHeight = h;
            le.flexibleHeight = 0f;
        }

        static Image NewImage(Transform parent, string name, Color color)
        {
            var rt = NewRect(name, parent);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = color;
            return img;
        }

        static Image Divider(Transform parent, Color color, float thickness)
        {
            var img = NewImage(parent, "Divider", color);
            img.raycastTarget = false;
            FixedH(img, thickness);
            return img;
        }

        static Text NewText(Transform parent, string name, string value, int size, TextAnchor anchor, Color color, bool display = false)
        {
            var rt = NewRect(name, parent);
            var t = rt.gameObject.AddComponent<Text>();
            t.text = value; t.alignment = anchor;
            if (display) LitIsoTheme.ApplyDisplay(t, size, color);
            else LitIsoTheme.ApplyBody(t, size, color);
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Truncate;
            return t;
        }

        static Image Panel(Transform parent, string name)
        {
            var rt = NewRect(name, parent);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = LitIsoTheme.Panel;
            var ol = rt.gameObject.AddComponent<Outline>();
            ol.effectColor = LitIsoTheme.Stone;
            ol.effectDistance = new Vector2(2f, -2f);
            ol.useGraphicAlpha = false;
            return img;
        }

        Button ThemeButton(Transform parent, string name, string label, bool gold, Action onClick,
            int size = 20, bool display = false)
        {
            var rt = NewRect(name, parent);
            rt.gameObject.AddComponent<LayoutElement>();
            var img = rt.gameObject.AddComponent<Image>();
            img.color = gold ? LitIsoTheme.Gold : LitIsoTheme.Raised;
            var ol = rt.gameObject.AddComponent<Outline>();
            ol.effectColor = LitIsoTheme.Base; ol.effectDistance = new Vector2(2f, -2f); ol.useGraphicAlpha = false;

            var btn = rt.gameObject.AddComponent<Button>();
            btn.targetGraphic = img;
            btn.transition = Selectable.Transition.ColorTint;
            var cb = btn.colors;
            cb.no