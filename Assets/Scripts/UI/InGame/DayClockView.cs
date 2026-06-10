using UnityEngine;
using UnityEngine.UI;
using IsoCore.Foundation;

namespace LitIso.UI.InGame
{
    /// <summary>
    /// Compact day/time strip pinned to the top-left of the HUD: phase + clock, on a
    /// panel whose tint shifts from warm amber at midday to cool blue-grey at deep night.
    /// Polls its model each frame because game time advances continuously.
    ///
    /// Foundation-free — renders from <see cref="IDayClockViewModel"/>; the Foundation
    /// binding is supplied by <see cref="FoundationDayClockAdapter"/>. No prefab; built
    /// via <see cref="UiBuilder"/>. Spawned + bound by <see cref="GameHudInitializer"/>.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DayClockView : MonoBehaviour
    {
        static readonly Color DayTint   = new Color(0.32f, 0.22f, 0.08f, 0.92f); // warm amber-brown
        static readonly Color NightTint = new Color(0.07f, 0.10f, 0.20f, 0.92f); // cool blue-grey
        static readonly Color DayText   = new Color(1.00f, 0.92f, 0.72f, 1f);
        static readonly Color NightText = new Color(0.78f, 0.85f, 1.00f, 1f);

        const float PanelW = 260f;
        const float PanelH = 58f;

        IDayClockViewModel _model;

        Canvas _canvas;
        Image  _panel;
        Text   _text;
        FoundationHudViewMode _hudMode = FoundationHudViewMode.Adventure;

        public void Init(IDayClockViewModel model)
        {
            _model = model;
            Refresh();
            ApplyHudViewMode(FoundationUiCoordinator.CurrentHudViewMode);
        }

        void Awake() => BuildUI();

        void OnEnable()
        {
            FoundationUiCoordinator.HudViewModeChanged += ApplyHudViewMode;
            LitIsoFont.TextScaleChanged += HandleTextScaleChanged;
            ApplyHudViewMode(FoundationUiCoordinator.CurrentHudViewMode);
        }

        void OnDisable()
        {
            FoundationUiCoordinator.HudViewModeChanged -= ApplyHudViewMode;
            LitIsoFont.TextScaleChanged -= HandleTextScaleChanged;
        }

        void BuildUI()
        {
            if (_canvas != null)
                Destroy(_canvas.gameObject);

            _canvas = UiBuilder.NewCanvas(transform, "DayClockCanvas", sortingOrder: 25);

            _panel = UiBuilder.NewImage(_canvas.transform, "DayClockPanel", null, DayTint);
            _panel.raycastTarget = false;
            var rt = _panel.rectTransform;
            rt.anchorMin = new Vector2(1f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot     = new Vector2(1f, 1f);
            rt.anchoredPosition = new Vector2(-260f, -30f);
            rt.sizeDelta = new Vector2(PanelW, PanelH);
            PlayerResizableUi.Attach(rt, "hud.day_clock", new Vector2(180f, 42f), new Vector2(420f, 100f));

            var outline = _panel.gameObject.AddComponent<Outline>();
            outline.effectColor    = UiBuilder.Border;
            outline.effectDistance = new Vector2(1.5f, -1.5f);

            _text = UiBuilder.NewText(_panel.transform, "DayClockText", "", 16, TextAnchor.MiddleCenter, DayText);
            _text.raycastTarget = false;
            UiBuilder.Stretch(_text.rectTransform, 8f);
            ApplyHudViewMode(_hudMode);
        }

        // Time advances continuously, so poll rather than wait on an event.
        void Update() => Refresh();

        void Refresh()
        {
            if (_panel == null) return;

            if (_model == null)
            {
                _text.text = "";
                return;
            }

            float night = Mathf.Clamp01(_model.Night01);
            _panel.color = Color.Lerp(DayTint, NightTint, night);
            _text.color  = Color.Lerp(DayText, NightText, night);

            string phase = _model.PhaseLabel;
            _text.text = string.IsNullOrEmpty(phase)
                ? _model.TimeText
                : $"{_model.TimeText}   {phase}";
        }

        void ApplyHudViewMode(FoundationHudViewMode mode)
        {
            _hudMode = mode;
            bool show = mode == FoundationHudViewMode.Adventure;
            if (_canvas != null && _canvas.gameObject.activeSelf != show)
                _canvas.gameObject.SetActive(show);
        }

        void HandleTextScaleChanged(float _)
        {
            BuildUI();
            Refresh();
            ApplyHudViewMode(FoundationUiCoordinator.CurrentHudViewMode);
        }
    }
}
