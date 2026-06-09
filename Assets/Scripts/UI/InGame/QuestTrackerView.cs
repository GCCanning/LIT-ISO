using UnityEngine;
using UnityEngine.UI;
using IsoCore.Foundation;

namespace LitIso.UI.InGame
{
    /// <summary>
    /// Compact always-visible HUD overlay — top-right corner — showing the pinned
    /// active quest: title, first incomplete objective with a fill bar, and reward
    /// preview. Hides itself automatically when no quest is active.
    ///
    /// Spawned by <see cref="GameHudInitializer"/> alongside the HUD. Call
    /// <see cref="Init"/> once to bind a model; subsequent calls re-bind cleanly.
    ///
    /// No prefab required — built procedurally via <see cref="UiBuilder"/>.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class QuestTrackerView : MonoBehaviour
    {
        // ---- colours (match the rest of the in-game UI palette) -------------
        static readonly Color QuestTitleCol  = new Color(1.00f, 0.85f, 0.30f, 1f);
        static readonly Color TypeTagCol     = new Color(0.75f, 0.90f, 1.00f, 1f);
        static readonly Color ProgressFg     = new Color(0.40f, 0.80f, 0.55f, 1f);
        static readonly Color ProgressBg     = new Color(0.12f, 0.16f, 0.20f, 1f);
        static readonly Color RewardCol      = new Color(0.85f, 0.75f, 0.95f, 1f);

        // Panel dimensions (in reference-resolution pixels, 1920×1080)
        const float PanelW   = 430f;
        const float PanelH   = 164f;
        const float PanelPad = 16f;
        const float BarH     = 12f;

        // ---- runtime references ---------------------------------------------
        IQuestTrackerViewModel _model;

        Canvas   _canvas;
        GameObject _root;      // root panel — shown/hidden based on active quest

        Text  _typeText;
        Text  _titleText;
        Text  _objText;
        Image _progressBar;    // fill image scaled to [0,1]
        Text  _rewardText;
        bool _hasQuest;
        FoundationHudViewMode _hudMode = FoundationHudViewMode.Adventure;

        // ---- public API -----------------------------------------------------

        public void Init(IQuestTrackerViewModel model)
        {
            if (_model != null) _model.Changed -= OnModelChanged;
            _model = model;
            if (_model != null) _model.Changed += OnModelChanged;
            Refresh();
        }

        // ---- Unity lifecycle ------------------------------------------------

        void Awake()
        {
            BuildUI();
        }

        void OnEnable()
        {
            FoundationUiCoordinator.HudViewModeChanged += ApplyHudViewMode;
            ApplyHudViewMode(FoundationUiCoordinator.CurrentHudViewMode);
        }

        void OnDisable()
        {
            FoundationUiCoordinator.HudViewModeChanged -= ApplyHudViewMode;
        }

        void OnDestroy()
        {
            if (_model != null) _model.Changed -= OnModelChanged;
        }

        // ---- UI construction ------------------------------------------------

        void BuildUI()
        {
            _canvas = UiBuilder.NewCanvas(transform, "QuestTrackerCanvas", sortingOrder: 20);

            // Anchor root panel to top-right corner with a small margin.
            _root = new GameObject("QuestTrackerPanel");
            _root.transform.SetParent(_canvas.transform, false);
            var rootImg = _root.AddComponent<Image>();
            rootImg.color = UiBuilder.PanelBg;
            var rt = _root.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(1f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot     = new Vector2(1f, 1f);
            rt.anchoredPosition = new Vector2(-38f, -128f);
            rt.sizeDelta        = new Vector2(PanelW, PanelH);
            PlayerResizableUi.Attach(rt, "hud.quest_tracker", new Vector2(320f, 130f), new Vector2(760f, 360f));

            // Outline border
            var outline = _root.AddComponent<Outline>();
            outline.effectColor    = UiBuilder.Border;
            outline.effectDistance = new Vector2(1.5f, -1.5f);

            // -- row 1: type tag + title (y from top inside panel) ------------
            float y = -PanelPad;

            _typeText = UiBuilder.NewText(_root.transform, "QuestType", "", 12, TextAnchor.UpperLeft, TypeTagCol);
            var typeRt = _typeText.rectTransform;
            typeRt.anchorMin = new Vector2(0f, 1f); typeRt.anchorMax = new Vector2(0f, 1f);
            typeRt.pivot     = new Vector2(0f, 1f);
            typeRt.anchoredPosition = new Vector2(PanelPad, y);
            typeRt.sizeDelta = new Vector2(PanelW - PanelPad * 2, 18f);

            y -= 22f;

            _titleText = UiBuilder.NewText(_root.transform, "QuestTitle", "", 18, TextAnchor.UpperLeft, QuestTitleCol);
            var titleRt = _titleText.rectTransform;
            titleRt.anchorMin = new Vector2(0f, 1f); titleRt.anchorMax = new Vector2(0f, 1f);
            titleRt.pivot     = new Vector2(0f, 1f);
            titleRt.anchoredPosition = new Vector2(PanelPad, y);
            titleRt.sizeDelta = new Vector2(PanelW - PanelPad * 2, 24f);

            y -= 30f;

            // -- row 2: objective text ----------------------------------------
            _objText = UiBuilder.NewText(_root.transform, "ObjText", "", 15, TextAnchor.UpperLeft, UiBuilder.TextCol);
            _objText.horizontalOverflow = HorizontalWrapMode.Wrap;
            var objRt = _objText.rectTransform;
            objRt.anchorMin = new Vector2(0f, 1f); objRt.anchorMax = new Vector2(0f, 1f);
            objRt.pivot     = new Vector2(0f, 1f);
            objRt.anchoredPosition = new Vector2(PanelPad, y);
            objRt.sizeDelta = new Vector2(PanelW - PanelPad * 2, 34f);

            y -= 40f;

            // -- progress bar background + fill --------------------------------
            var barBgGo = new GameObject("ProgressBg", typeof(RectTransform));
            barBgGo.transform.SetParent(_root.transform, false);
            var barBgImg = barBgGo.AddComponent<Image>();
            barBgImg.color = ProgressBg;
            var barBgRt = barBgGo.GetComponent<RectTransform>();
            barBgRt.anchorMin = new Vector2(0f, 1f); barBgRt.anchorMax = new Vector2(0f, 1f);
            barBgRt.pivot     = new Vector2(0f, 1f);
            barBgRt.anchoredPosition = new Vector2(PanelPad, y);
            barBgRt.sizeDelta = new Vector2(PanelW - PanelPad * 2, BarH);

            var barFgGo = new GameObject("ProgressFg", typeof(RectTransform));
            barFgGo.transform.SetParent(barBgGo.transform, false);
            _progressBar = barFgGo.AddComponent<Image>();
            _progressBar.color = ProgressFg;
            var barFgRt = barFgGo.GetComponent<RectTransform>();
            barFgRt.anchorMin = Vector2.zero; barFgRt.anchorMax = new Vector2(0f, 1f);
            barFgRt.pivot     = Vector2.zero;
            barFgRt.offsetMin = Vector2.zero;
            barFgRt.offsetMax = new Vector2(0f, 0f); // width set in Refresh()

            y -= BarH + 10f;

            // -- reward preview -----------------------------------------------
            _rewardText = UiBuilder.NewText(_root.transform, "RewardText", "", 13, TextAnchor.UpperLeft, RewardCol);
            var rewRt = _rewardText.rectTransform;
            rewRt.anchorMin = new Vector2(0f, 1f); rewRt.anchorMax = new Vector2(0f, 1f);
            rewRt.pivot     = new Vector2(0f, 1f);
            rewRt.anchoredPosition = new Vector2(PanelPad, y);
            rewRt.sizeDelta = new Vector2(PanelW - PanelPad * 2, 22f);
            ApplyHudViewMode(_hudMode);
        }

        // ---- model change ---------------------------------------------------

        void OnModelChanged() => Refresh();

        void Refresh()
        {
            if (_root == null) return;

            var quest = _model?.PinnedQuest;
            _hasQuest = quest.HasValue;
            ApplyHudViewMode(_hudMode);
            if (!quest.HasValue) return;

            var q = quest.Value;

            _typeText.text  = string.IsNullOrEmpty(q.questType) ? "" : $"[{q.questType}]";
            _titleText.text = q.title;

            // Objective: "text (current/required)" or just progress numbers
            if (q.objectiveRequired > 1)
                _objText.text = $"• {q.objectiveText}  {q.objectiveCurrent}/{q.objectiveRequired}";
            else
                _objText.text = string.IsNullOrEmpty(q.objectiveText) ? "" : $"• {q.objectiveText}";

            // Fill bar — resize the fill rect's right edge
            float fill = q.objectiveRequired > 0
                ? Mathf.Clamp01((float)q.objectiveCurrent / q.objectiveRequired)
                : 0f;
            float barWidth = PanelW - PanelPad * 2f;
            var fgRt = _progressBar.rectTransform;
            fgRt.offsetMax = new Vector2(barWidth * fill, 0f);

            // Reward preview
            _rewardText.text = string.IsNullOrEmpty(q.rewardText)
                ? ""
                : $"Reward: {q.rewardText}";
        }

        void ApplyHudViewMode(FoundationHudViewMode mode)
        {
            _hudMode = mode;
            bool show = _hasQuest && mode == FoundationHudViewMode.Adventure;
            if (_root != null && _root.activeSelf != show)
                _root.SetActive(show);
        }
    }
}
