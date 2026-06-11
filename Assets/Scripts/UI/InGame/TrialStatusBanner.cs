using IsoCore.Foundation;
using UnityEngine;
using UnityEngine.UI;

namespace LitIso.UI.InGame
{
    /// <summary>
    /// Small HUD line: "TRIAL · Day 3 of 7 · Forecast B" — keeps the trial
    /// (tutorial-as-exam) present in the player's mind, per the transmigration
    /// design. Reads live FoundationProgression data; hides itself once the
    /// trial completes. Alt-draggable like other HUD panels.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TrialStatusBanner : MonoBehaviour
    {
        FoundationProgression _progression;
        Canvas _canvas;
        Text _text;
        float _nextPoll;
        string _lastShown;

        public void Bind(FoundationProgression progression)
        {
            _progression = progression;
            if (_canvas == null) Build();
            Poll(true);
        }

        void Build()
        {
            _canvas = UiBuilder.NewCanvas(transform, "TrialBannerCanvas", 205);
            var root = UiBuilder.NewPanel(_canvas.transform, "Banner", "system_row",
                new Color(0.05f, 0.08f, 0.12f, 0.88f));
            var rt = root.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0f, -64f);
            rt.sizeDelta = new Vector2(360f, 30f);
            PlayerResizableUi.Attach(rt, "hud.trialBanner", new Vector2(240f, 24f), new Vector2(560f, 60f));

            _text = UiBuilder.NewText(root.transform, "T", "", 14,
                TextAnchor.MiddleCenter, new Color(0.55f, 0.85f, 1f, 1f));
            UiBuilder.Stretch(_text.rectTransform, 4f);
            _text.raycastTarget = false;
        }

        void Update()
        {
            if (Time.unscaledTime >= _nextPoll) Poll(false);
        }

        void Poll(bool force)
        {
            _nextPoll = Time.unscaledTime + 1f;
            if (_progression == null || _canvas == null) return;
            bool show = !_progression.TrialCompleted;
            if (_canvas.gameObject.activeSelf != show)
                _canvas.gameObject.SetActive(show);
            if (show)
            {
                string s = $"TRIAL · Day {_progression.TrialDay} of {_progression.TrialDurationDays}" +
                           $" · Forecast {_progression.GradeForecast}";
                if (s != _lastShown) { _lastShown = s; _text.text = s; }
            }
        }
    }
}
