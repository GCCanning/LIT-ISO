using UnityEngine;
using UnityEngine.UI;

namespace LitIso.UI.InGame
{
    /// <summary>
    /// QUEST BOARD — two-panel modal matching the approved LIT-ISO front-end design.
    /// Left pane: 2-column quest card grid with risk badges and pin studs.
    /// Right pane: selected quest detail — giver, quote, objectives, rewards,
    /// ACCEPT / DECLINE. Panel uses the full pixel-art grammar with corner studs.
    /// Toggle with N or open externally via OpenExternal().
    /// </summary>
    public sealed class QuestBoardView : MonoBehaviour
    {
        // ── Static sample data ───────────────────────────────────────────────
        sealed class QuestData
        {
            public string title, giver, giverInitials, region, risk;
            public string quote;
            public string[] objectives, rewards;
        }

        static readonly QuestData[] Quests =
        {
            new QuestData
            {
                title = "The Hollow Hearth", giver = "Lady Priscas",
                giverInitials = "LP", region = "Emberfall Woods", risk = "LOW RISK",
                quote = "\"The old campfire at the wood's edge has gone cold. Light it again, " +
                        "traveler — the dark things keep their distance from honest flame.\"",
                objectives = new[] { "Gather 6 oak wood", "Light the old campfire", "Survive until dawn" },
                rewards = new[] { "180 XP", "80 Gold", "Ember Rune x8" },
            },
            new QuestData
            {
                title = "Frostfang Bounty", giver = "Hunter Bram",
                giverInitials = "HB", region = "Ashen Mines", risk = "HIGH RISK",
                quote = "\"The Frostfang pack has taken three miners. Drive them back " +
                        "before the north passage freezes shut for the season.\"",
                objectives = new[] { "Slay 5 Frostfang wolves", "Recover the miner's pack" },
                rewards = new[] { "340 XP", "120 Gold" },
            },
            new QuestData
            {
                title = "Salt for the Tannery", giver = "Maren the Curer",
                giverInitials = "MC", region = "Saltmarsh", risk = "LOW RISK",
                quote = "\"The salt flats are just a day's walk east. Bring back three " +
                        "casks and I'll see you right with coin and a good meal.\"",
                objectives = new[] { "Collect 3 salt casks from the flats" },
                rewards = new[] { "90 XP", "40 Gold" },
            },
            new QuestData
            {
                title = "The Sealed Door", giver = "Archivist Ven",
                giverInitials = "AV", region = "Ashen Mines", risk = "EPIC RISK",
                quote = "\"What lies behind that door has slept for two centuries. " +
                        "If you open it, be certain you can close it again.\"",
                objectives = new[] { "Find the Ember Key", "Unseal the vault", "Escape the mines" },
                rewards = new[] { "720 XP", "300 Gold", "Ancient Relic" },
            },
        };

        static Color RiskColor(string risk)
        {
            if (risk == null) return LitIsoTheme.WarmTan;
            if (risk.Contains("EPIC")) return LitIsoTheme.RarityEpic;
            if (risk.Contains("HIGH")) return LitIsoTheme.Amber;
            return LitIsoTheme.Green;
        }

        // ── Instance state ───────────────────────────────────────────────────
        static QuestBoardView s_instance;
        public KeyCode toggleKey = KeyCode.N;

        Canvas _canvas;
        bool IsOpen => _canvas != null;
        int _selected;
        RectTransform _detailContent;
        Image[] _cardHighlights;

        // ── Bootstrap ────────────────────────────────────────────────────────
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            if (s_instance != null) return;
            var go = new GameObject("QuestBoardView");
            s_instance = go.AddComponent<QuestBoardView>();
            DontDestroyOnLoad(go);
        }

        /// <summary>Open the Quest Board from outside (e.g. a quest-board prop).</summary>
        public static void OpenExternal() { if (s_instance != null) s_instance.Open(); }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void HookBridge()
        {
            IsoCore.Foundation.FoundationUiBridge.QuestBoardRequested -= OpenExternal;
            IsoCore.Foundation.FoundationUiBridge.QuestBoardRequested += OpenExternal;
        }

        void Update()
        {
            var ui = IsoCore.Foundation.FoundationUiCoordinator.Active;
            if (Input.GetKeyDown(toggleKey))
            {
                if (IsOpen) { Close(); ui?.ConsumeInputThisFrame(); }
                else if (ui != null && ui.CanOpenModal("questBoard")) { Open(); ui.ConsumeInputThisFrame(); }
            }
            else if (IsOpen && Input.GetKeyDown(KeyCode.Escape)) { Close(); ui?.ConsumeInputThisFrame(); }
        }

        void Close()
        {
            if (_canvas != null) Destroy(_canvas.gameObject);
            _canvas = null;
            _detailContent = null;
            _cardHighlights = null;
            IsoCore.Foundation.FoundationUiCoordinator.Active?.SetModalOpen("questBoard", false);
        }

        void OnDestroy()
        {
            IsoCore.Foundation.FoundationUiCoordinator.Active?.SetModalOpen("questBoard", false);
        }

        // ── Panel construction ───────────────────────────────────────────────

        void Open()
        {
            if (IsOpen) return;
            EnsureEventSystem();
            _selected = 0;
            _canvas = UiBuilder.NewCanvas(transform, "QuestBoardCanvas", 230);
            IsoCore.Foundation.FoundationUiCoordinator.Active?.SetModalOpen("questBoard", true);

            var root = UiBuilder.NewRect("Root", _canvas.transform);
            UiBuilder.Stretch(root);

            var scrim = UiBuilder.NewScrim(root);
            var scrimBtn = scrim.gameObject.AddComponent<Button>();
            scrimBtn.transition = Selectable.Transition.None;
            scrimBtn.onClick.AddListener(Close);

            // Outer panel — Stone frame + corner studs.
            const float panelW = 1080f, panelH = 660f;
            var panelImg = UiBuilder.NewImage(root, "Panel", null, LitIsoTheme.Panel);
            LitIsoTheme.StyleFrame(panelImg, LitIsoTheme.FrameStyle.Stone);
            var panel = panelImg.rectTransform;
            panel.anchorMin = panel.anchorMax = new Vector2(0.5f, 0.5f);
            panel.pivot = new Vector2(0.5f, 0.5f);
            panel.sizeDelta = new Vector2(panelW, panelH);
            UiBuilder.AddCornerStuds(panel);

            const float headerH = 68f;
            BuildHeader(panel, headerH);

            // Header divider.
            var hdiv = UiBuilder.NewImage(panel, "HDiv", null, LitIsoTheme.Base);
            hdiv.raycastTarget = false;
            var hdr = hdiv.rectTransform;
            hdr.anchorMin = new Vector2(0f, 1f); hdr.anchorMax = new Vector2(1f, 1f);
            hdr.pivot = new Vector2(0.5f, 1f);
            hdr.anchoredPosition = new Vector2(0f, -headerH);
            hdr.sizeDelta = new Vector2(0f, 1f);

            const float bodyY = headerH + 1f;
            float bodyH = panelH - bodyY;
            const float leftW = 460f;
            float rightW = panelW - leftW;

            BuildLeftPane(panel, leftW, bodyH, bodyY);
            BuildRightPane(panel, leftW, rightW, bodyH, bodyY);
        }

        // ── Header ──────────────────────────────────────────────────────────

        void BuildHeader(RectTransform panel, float headerH)
        {
            var header = UiBuilder.NewRect("Header", panel);
            header.anchorMin = new Vector2(0f, 1f); header.anchorMax = new Vector2(1f, 1f);
            header.pivot = new Vector2(0.5f, 1f);
            header.anchoredPosition = Vector2.zero;
            header.sizeDelta = new Vector2(0f, headerH);

            var diam = UiBuilder.NewImage(header, "Diamond", null, LitIsoTheme.Gold);
            diam.raycastTarget = false;
            var dr = diam.rectTransform;
            dr.anchorMin = dr.anchorMax = new Vector2(0f, 0.5f); dr.pivot = new Vector2(0.5f, 0.5f);
            dr.anchoredPosition = new Vector2(32f, 0f); dr.sizeDelta = new Vector2(11f, 11f);
            dr.localRotation = Quaternion.Euler(0f, 0f, 45f);

            var title = UiBuilder.NewText(header, "Title", "QUEST BOARD",
                22, TextAnchor.MiddleLeft, LitIsoTheme.Gold);
            LitIsoTheme.ApplyDisplay(title, 22, LitIsoTheme.Gold);
            title.rectTransform.anchorMin = new Vector2(0f, 0f);
            title.rectTransform.anchorMax = new Vector2(0.5f, 1f);
            title.rectTransform.offsetMin = new Vector2(52f, 0f);
            title.rectTransform.offsetMax = Vector2.zero;

            var sub = UiBuilder.NewText(header, "Sub",
                "Bounties & errands posted across the realm.",
                15, TextAnchor.LowerLeft, LitIsoTheme.WarmTan);
            LitIsoTheme.ApplyBody(sub, 15, LitIsoTheme.WarmTan);
            sub.rectTransform.anchorMin = new Vector2(0f, 0f);
            sub.rectTransform.anchorMax = new Vector2(0.6f, 0.5f);
            sub.rectTransform.offsetMin = new Vector2(52f, 8f);
            sub.rectTransform.offsetMax = Vector2.zero;

            var close = UiBuilder.BuildPixelButton(header, "Close", "X",
                14, LitIsoTheme.ButtonStyle.Stone);
            close.onClick.AddListener(Close);
            var cr = close.GetComponent<RectTransform>();
            cr.anchorMin = cr.anchorMax = new Vector2(1f, 0.5f);
            cr.pivot = new Vector2(1f, 0.5f);
            cr.anchoredPosition = new Vector2(-18f, 0f);
            cr.sizeDelta = new Vector2(42f, 42f);
        }

        // ── Left pane ────────────────────────────────────────────────────────

        void BuildLeftPane(RectTransform panel, float leftW, float bodyH, float bodyY)
        {
            var left = UiBuilder.NewImage(panel, "LeftPane", null, LitIsoTheme.WoodFill);
            left.raycastTarget = false;
            var lr = left.rectTransform;
            lr.anchorMin = new Vector2(0f, 0f); lr.anchorMax = new Vector2(0f, 1f);
            lr.pivot = new Vector2(0f, 1f);
            lr.anchoredPosition = new Vector2(0f, -bodyY);
            lr.sizeDelta = new Vector2(leftW, bodyH);

            var sep = UiBuilder.NewImage(lr, "Sep", null, LitIsoTheme.Base);
            sep.raycastTarget = false;
            var sepr = sep.rectTransform;
            sepr.anchorMin = new Vector2(1f, 0f); sepr.anchorMax = Vector2.one;
            sepr.offsetMin = new Vector2(-1f, 0f); sepr.offsetMax = Vector2.zero;

            const float pad = 20f, cardGap = 14f;
            float cardW = (leftW - pad * 2f - cardGap) / 2f;
            const float cardH = 128f;

            _cardHighlights = new Image[Quests.Length];
            for (int i = 0; i < Quests.Length; i++)
            {
                int col = i % 2, row = i / 2;
                float cx = pad + col * (cardW + cardGap);
                float cy = -(pad + row * (cardH + cardGap));
                BuildQuestCard(lr, Quests[i], i, cx, cy, cardW, cardH);
            }
        }

        void BuildQuestCard(RectTransform parent, QuestData q, int index,
            float x, float y, float cardW, float cardH)
        {
            var card = UiBuilder.NewImage(parent, "Card_" + index, null, LitIsoTheme.WoodBevel1);
            card.raycastTarget = false;
            var cr = card.rectTransform;
            cr.anchorMin = cr.anchorMax = new Vector2(0f, 1f);
            cr.pivot = new Vector2(0f, 1f);
            cr.anchoredPosition = new Vector2(x, y);
            cr.sizeDelta = new Vector2(cardW, cardH);

            var hi = UiBuilder.NewImage(cr, "Highlight", null, Color.clear);
            hi.raycastTarget = false;
            UiBuilder.AddPixelOutline(hi.gameObject, LitIsoTheme.Gold, 2f);
            var hir = hi.rectTransform;
            hir.anchorMin = Vector2.zero; hir.anchorMax = Vector2.one;
            hir.offsetMin = new Vector2(-2f, -2f); hir.offsetMax = new Vector2(2f, 2f);
            hi.gameObject.SetActive(index == _selected);
            _cardHighlights[index] = hi;

            var pin = UiBuilder.NewImage(cr, "Pin", null, LitIsoTheme.Amber);
            pin.raycastTarget = false;
            var pr = pin.rectTransform;
            pr.anchorMin = pr.anchorMax = new Vector2(0.5f, 1f); pr.pivot = new Vector2(0.5f, 0.5f);
            pr.anchoredPosition = Vector2.zero; pr.sizeDelta = new Vector2(12f, 12f);

            Color riskCol = RiskColor(q.risk);
            var riskBg = UiBuilder.NewImage(cr, "RiskBg", null,
                new Color(riskCol.r, riskCol.g, riskCol.b, 0.15f));
            riskBg.raycastTarget = false;
            UiBuilder.AddPixelOutline(riskBg.gameObject, riskCol, 1f);
            var rbr = riskBg.rectTransform;
            rbr.anchorMin = rbr.anchorMax = new Vector2(0f, 1f);
            rbr.pivot = new Vector2(0f, 1f);
            rbr.anchoredPosition = new Vector2(12f, -14f);
            rbr.sizeDelta = new Vector2(Mathf.Min(cardW - 24f, 90f), 20f);
            var riskTxt = UiBuilder.NewText(rbr, "Risk", q.risk,
                9, TextAnchor.MiddleCenter, riskCol);
            LitIsoTheme.ApplyDisplay(riskTxt, 9, riskCol);
            UiBuilder.Stretch(riskTxt.rectTransform);

            var titleT = UiBuilder.NewText(cr, "Title", q.title,
                16, TextAnchor.UpperLeft, LitIsoTheme.Parchment);
            LitIsoTheme.ApplyBody(titleT, 16, LitIsoTheme.Parchment);
            titleT.horizontalOverflow = HorizontalWrapMode.Wrap;
            titleT.rectTransform.anchorMin = new Vector2(0f, 1f);
            titleT.rectTransform.anchorMax = new Vector2(1f, 1f);
            titleT.rectTransform.pivot = new Vector2(0f, 1f);
            titleT.rectTransform.anchoredPosition = new Vector2(12f, -38f);
            titleT.rectTransform.sizeDelta = new Vector2(cardW - 24f, 46f);

            var giverT = UiBuilder.NewText(cr, "Giver", $"{q.giver}  ·  {q.region}",
                12, TextAnchor.LowerLeft, LitIsoTheme.WarmTan);
            LitIsoTheme.ApplyBody(giverT, 12, LitIsoTheme.WarmTan);
            giverT.rectTransform.anchorMin = new Vector2(0f, 0f);
            giverT.rectTransform.anchorMax = new Vector2(1f, 0f);
            giverT.rectTransform.pivot = new Vector2(0f, 0f);
            giverT.rectTransform.anchoredPosition = new Vector2(12f, 10f);
            giverT.rectTransform.sizeDelta = new Vector2(cardW - 24f, 18f);

            var overlay = UiBuilder.NewImage(cr, "Overlay", null, Color.clear);
            overlay.raycastTarget = true;
            UiBuilder.Stretch(overlay.rectTransform);
            var btn = overlay.gameObject.AddComponent<Button>();
            btn.targetGraphic = overlay;
            btn.transition = Selectable.Transition.None;
            int captured = index;
            btn.onClick.AddListener(() => SelectQuest(captured));
        }

        // ── Right pane ───────────────────────────────────────────────────────

        void BuildRightPane(RectTransform panel, float leftW, float rightW,
            float bodyH, float bodyY)
        {
            var right = UiBuilder.NewImage(panel, "RightPane", null, LitIsoTheme.Panel);
            right.raycastTarget = false;
            var rr = right.rectTransform;
            rr.anchorMin = new Vector2(0f, 0f); rr.anchorMax = new Vector2(1f, 1f);
            rr.offsetMin = new Vector2(leftW, 0f); rr.offsetMax = new Vector2(0f, -bodyY);

            _detailContent = UiBuilder.NewRect("Detail", rr);
            UiBuilder.Stretch(_detailContent, 1f);
            BuildDetail(_detailContent, Quests[_selected], rightW);
        }

        void SelectQuest(int index)
        {
            if (index == _selected) return;
            _selected = index;

            if (_cardHighlights != null)
                for (int i = 0; i < _cardHighlights.Length; i++)
                    if (_cardHighlights[i] != null)
                        _cardHighlights[i].gameObject.SetActive(i == _selected);

            if (_detailContent == null) return;
            for (int i = _detailContent.childCount - 1; i >= 0; i--)
                Destroy(_detailContent.GetChild(i).gameObject);

            float w = _detailContent.rect.width;
            if (w < 100f) w = 620f;
            BuildDetail(_detailContent, Quests[_selected], w);
        }

        void BuildDetail(RectTransform content, QuestData q, float width)
        {
            const float pad = 28f;
            float innerW = width - pad * 2f;
            float y = 0f;

            // Giver row.
            const float avatarSz = 56f;
            var avatarBg = UiBuilder.NewImage(content, "AvatarBg", null, LitIsoTheme.GoldDeep);
            UiBuilder.AddPixelOutline(avatarBg.gameObject, LitIsoTheme.Gold, 2f);
            avatarBg.rectTransform.anchorMin = avatarBg.rectTransform.anchorMax = new Vector2(0f, 1f);
            avatarBg.rectTransform.pivot = new Vector2(0f, 1f);
            avatarBg.rectTransform.anchoredPosition = new Vector2(pad, -(y + 14f));
            avatarBg.rectTransform.sizeDelta = new Vector2(avatarSz, avatarSz);
            var initT = UiBuilder.NewText(avatarBg.rectTransform, "Init", q.giverInitials,
                15, TextAnchor.MiddleCenter, LitIsoTheme.Gold);
            LitIsoTheme.ApplyDisplay(initT, 15, LitIsoTheme.Gold);
            UiBuilder.Stretch(initT.rectTransform);

            float gx = pad + avatarSz + 14f;
            float gw = innerW - avatarSz - 14f - 100f;
            var giverName = UiBuilder.NewText(content, "GiverName", q.giver,
                18, TextAnchor.UpperLeft, LitIsoTheme.Parchment);
            LitIsoTheme.ApplyBody(giverName, 18, LitIsoTheme.Parchment);
            PlaceAbs(giverName.rectTransform, gx, y + 14f, gw, 24f);

            var giverSub = UiBuilder.NewText(content, "GiverSub",
                $"Quest giver  ·  {q.region}", 14, TextAnchor.UpperLeft, LitIsoTheme.WarmTan);
            LitIsoTheme.ApplyBody(giverSub, 14, LitIsoTheme.WarmTan);
            PlaceAbs(giverSub.rectTransform, gx, y + 40f, gw, 18f);

            Color riskCol = RiskColor(q.risk);
            var riskBg = UiBuilder.NewImage(content, "RiskBadge", null,
                new Color(riskCol.r, riskCol.g, riskCol.b, 0.18f));
            riskBg.raycastTarget = false;
            UiBuilder.AddPixelOutline(riskBg.gameObject, riskCol, 1f);
            riskBg.rectTransform.anchorMin = riskBg.rectTransform.anchorMax = new Vector2(1f, 1f);
            riskBg.rectTransform.pivot = new Vector2(1f, 1f);
            riskBg.rectTransform.anchoredPosition = new Vector2(-pad, -(y + 14f));
            riskBg.rectTransform.sizeDelta = new Vector2(90f, 24f);
            var riskTxt = UiBuilder.NewText(riskBg.rectTransform, "Risk", q.risk,
                9, TextAnchor.MiddleCenter, riskCol);
            LitIsoTheme.ApplyDisplay(riskTxt, 9, riskCol);
            UiBuilder.Stretch(riskTxt.rectTransform);

            y += avatarSz + 28f;

            // Quote box.
            const float quoteH = 82f;
            var quoteBg = UiBuilder.NewImage(content, "QuoteBg", null,
                LitIsoTheme.Hex("#1c1610"));
            quoteBg.raycastTarget = false;
            UiBuilder.AddPixelOutline(quoteBg.gameObject, LitIsoTheme.Hex("#6b5836"), 1f);
            PlaceAbs(quoteBg.rectTransform, pad, y, innerW, quoteH);
            var quoteT = UiBuilder.NewText(quoteBg.rectTransform, "Quote", q.quote,
                14, TextAnchor.MiddleLeft, LitIsoTheme.Hex("#c9a26a"));
            LitIsoTheme.ApplyBody(quoteT, 14, LitIsoTheme.Hex("#c9a26a"));
            quoteT.fontStyle = FontStyle.Italic;
            quoteT.horizontalOverflow = HorizontalWrapMode.Wrap;
            UiBuilder.Stretch(quoteT.rectTransform, 12f);
            y += quoteH + 18f;

            // Quest title.
            var questTitle = UiBuilder.NewText(content, "QuestTitle", q.title,
                24, TextAnchor.UpperLeft, LitIsoTheme.Gold);
            LitIsoTheme.ApplyDisplay(questTitle, 24, LitIsoTheme.Gold);
            PlaceAbs(questTitle.rectTransform, pad, y, innerW, 36f);
            y += 36f + 16f;

            // Objectives.
            var objHeader = UiBuilder.NewText(content, "ObjHeader", "OBJECTIVES",
                10, TextAnchor.UpperLeft, LitIsoTheme.WarmTan);
            LitIsoTheme.ApplyDisplay(objHeader, 10, LitIsoTheme.WarmTan);
            PlaceAbs(objHeader.rectTransform, pad, y, innerW, 16f);
            y += 16f + 8f;

            foreach (string obj in q.objectives)
            {
                var cb = UiBuilder.NewImage(content, "CB", null, Color.clear);
                cb.raycastTarget = false;
                UiBuilder.AddPixelOutline(cb.gameObject, LitIsoTheme.WarmTan, 1f);
                cb.rectTransform.anchorMin = cb.rectTransform.anchorMax = new Vector2(0f, 1f);
                cb.rectTransform.pivot = new Vector2(0f, 1f);
                cb.rectTransform.anchoredPosition = new Vector2(pad, -y);
                cb.rectTransform.sizeDelta = new Vector2(14f, 14f);

                var objT = UiBuilder.NewText(content, "Obj", obj,
                    16, TextAnchor.UpperLeft, LitIsoTheme.Parchment);
                LitIsoTheme.ApplyBody(objT, 16, LitIsoTheme.Parchment);
                PlaceAbs(objT.rectTransform, pad + 22f, y - 1f, innerW - 22f, 22f);
                y += 26f;
            }
            y += 14f;

            // Rewards.
            var rewHeader = UiBuilder.NewText(content, "RewHeader", "REWARDS",
                10, TextAnchor.UpperLeft, LitIsoTheme.WarmTan);
            LitIsoTheme.ApplyDisplay(rewHeader, 10, LitIsoTheme.WarmTan);
            PlaceAbs(rewHeader.rectTransform, pad, y, innerW, 16f);
            y += 16f + 10f;

            float rx = pad;
            foreach (string rew in q.rewards)
            {
                float rewW = Mathf.Max(80f, rew.Length * 10f + 24f);
                var rewBg = UiBuilder.NewImage(content, "Rew", null,
                    LitIsoTheme.Hex("#141008"));
                rewBg.raycastTarget = false;
                UiBuilder.AddPixelOutline(rewBg.gameObject, LitIsoTheme.GoldDeep, 1f);
                rewBg.rectTransform.anchorMin = rewBg.rectTransform.anchorMax = new Vector2(0f, 1f);
                rewBg.rectTransform.pivot = new Vector2(0f, 1f);
                rewBg.rectTransform.anchoredPosition = new Vector2(rx, -y);
                rewBg.rectTransform.sizeDelta = new Vector2(rewW, 28f);

                var rewDot = UiBuilder.NewImage(rewBg.rectTransform, "Dot", null, LitIsoTheme.Gold);
                rewDot.raycastTarget = false;
                rewDot.rectTransform.anchorMin = rewDot.rectTransform.anchorMax = new Vector2(0f, 0.5f);
                rewDot.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                rewDot.rectTransform.anchoredPosition = new Vector2(10f, 0f);
                rewDot.rectTransform.sizeDelta = new Vector2(7f, 7f);
                rewDot.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);

                var rewT = UiBuilder.NewText(rewBg.rectTransform, "T", rew,
                    13, TextAnchor.MiddleLeft, LitIsoTheme.GoldLit);
                LitIsoTheme.ApplyBody(rewT, 13, LitIsoTheme.GoldLit);
                rewT.rectTransform.anchorMin = Vector2.zero; rewT.rectTransform.anchorMax = Vector2.one;
                rewT.rectTransform.offsetMin = new Vector2(20f, 0f); rewT.rectTransform.offsetMax = new Vector2(-4f, 0f);
                rx += rewW + 10f;
            }
            y += 28f + 16f;

            // Accept / Decline — pinned to the bottom.
            float acceptW = innerW - 150f - 14f;
            var accept = UiBuilder.BuildPixelButton(content, "Accept",
                "ACCEPT QUEST  ▸", 12, LitIsoTheme.ButtonStyle.Gold);
            accept.onClick.AddListener(Close);
            var acr = accept.GetComponent<RectTransform>();
            acr.anchorMin = acr.anchorMax = new Vector2(0f, 0f);
            acr.pivot = new Vector2(0f, 0f);
            acr.anchoredPosition = new Vector2(pad, 18f);
            acr.sizeDelta = new Vector2(acceptW, 52f);

            var decline = UiBuilder.BuildPixelButton(content, "Decline",
                "DECLINE", 12, LitIsoTheme.ButtonStyle.Stone);
            decline.onClick.AddListener(Close);
            var dcr = decline.GetComponent<RectTransform>();
            dcr.anchorMin = dcr.anchorMax = new Vector2(1f, 0f);
            dcr.pivot = new Vector2(1f, 0f);
            dcr.anchoredPosition = new Vector2(-pad, 18f);
            dcr.sizeDelta = new Vector2(140f, 52f);
        }

        static void PlaceAbs(RectTransform rt, float x, float y, float w, float h)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(x, -y);
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
