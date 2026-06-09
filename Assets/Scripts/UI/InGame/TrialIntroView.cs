using System.Collections;
using IsoCore.Foundation;
using UnityEngine;
using UnityEngine.UI;

namespace LitIso.UI.InGame
{
    /// <summary>
    /// Fresh-world cinematic overlay. It covers the HUD briefly, then fades out so the
    /// normal uGUI shell feels like it has booted into the Trial.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TrialIntroView : MonoBehaviour
    {
        Canvas _canvas;
        CanvasGroup _group;
        Text _body;
        FoundationBootstrap _playingFor;

        public void Play(FoundationBootstrap bootstrap)
        {
            if (bootstrap == null ||
                bootstrap.ActiveLaunchIsLoad ||
                bootstrap.ActiveLaunchMode != FoundationLaunchMode.Standard)
                return;

            if (_playingFor == bootstrap)
                return;

            _playingFor = bootstrap;
            StopAllCoroutines();
            Build(bootstrap);
            StartCoroutine(Sequence());
        }

        void Build(FoundationBootstrap bootstrap)
        {
            if (_canvas != null)
                Destroy(_canvas.gameObject);

            _canvas = UiBuilder.NewCanvas(transform, "TrialIntroCanvas", 520);
            _group = _canvas.gameObject.AddComponent<CanvasGroup>();
            _group.alpha = 1f;
            _group.blocksRaycasts = true;
            _group.interactable = true;

            var bg = UiBuilder.NewImage(_canvas.transform, "Veil", null, new Color(0.015f, 0.018f, 0.025f, 0.96f));
            bg.raycastTarget = true;
            UiBuilder.Stretch(bg.rectTransform);

            var panel = UiBuilder.NewPanel(_canvas.transform, "TrialPanel", "system_panel",
                new Color(0.065f, 0.072f, 0.095f, 0.98f));
            var pr = panel.rectTransform;
            pr.anchorMin = pr.anchorMax = new Vector2(0.5f, 0.5f);
            pr.pivot = new Vector2(0.5f, 0.5f);
            pr.sizeDelta = new Vector2(760f, 360f);

            var title = UiBuilder.NewText(panel.transform, "Title", "YOUR TRIAL AWAITS", 42, TextAnchor.MiddleCenter,
                new Color(1.00f, 0.82f, 0.32f, 1f));
            title.rectTransform.anchorMin = new Vector2(0f, 1f);
            title.rectTransform.anchorMax = new Vector2(1f, 1f);
            title.rectTransform.pivot = new Vector2(0.5f, 1f);
            title.rectTransform.anchoredPosition = new Vector2(0f, -38f);
            title.rectTransform.sizeDelta = new Vector2(-60f, 58f);

            string calling = bootstrap.Progression?.CurrentCalling?.Display ?? "Greenhand";
            string charName = string.IsNullOrWhiteSpace(bootstrap.ActiveCharacterName)
                ? "Unwritten"
                : bootstrap.ActiveCharacterName;

            _body = UiBuilder.NewText(panel.transform, "BootLines",
                $"Identity: {charName}\nCalling: {calling}\nWorld: {bootstrap.ActiveWorldName}\n\n[System] Foreign soul recognized...",
                20, TextAnchor.UpperCenter, UiBuilder.TextCol);
            _body.horizontalOverflow = HorizontalWrapMode.Wrap;
            _body.rectTransform.anchorMin = Vector2.zero;
            _body.rectTransform.anchorMax = Vector2.one;
            _body.rectTransform.offsetMin = new Vector2(70f, 58f);
            _body.rectTransform.offsetMax = new Vector2(-70f, -124f);

            var footer = UiBuilder.NewText(panel.transform, "Footer", "Inventory indexed // HUD online // Begin", 16,
                TextAnchor.MiddleCenter, new Color(0.55f, 0.82f, 0.95f, 1f));
            footer.rectTransform.anchorMin = new Vector2(0f, 0f);
            footer.rectTransform.anchorMax = new Vector2(1f, 0f);
            footer.rectTransform.pivot = new Vector2(0.5f, 0f);
            footer.rectTransform.anchoredPosition = new Vector2(0f, 32f);
            footer.rectTransform.sizeDelta = new Vector2(-60f, 30f);
        }

        IEnumerator Sequence()
        {
            yield return new WaitForSecondsRealtime(0.55f);
            if (_body != null) _body.text += "\n[System] Calling matrix stabilized.";
            yield return new WaitForSecondsRealtime(0.55f);
            if (_body != null) _body.text += "\n[System] Quest ledger opened.";
            yield return new WaitForSecondsRealtime(0.55f);
            if (_body != null) _body.text += "\n[System] HUD online.";
            yield return new WaitForSecondsRealtime(0.70f);

            float t = 0f;
            const float fade = 0.55f;
            while (t < fade && _group != null)
            {
                t += Time.unscaledDeltaTime;
                _group.alpha = Mathf.Lerp(1f, 0f, t / fade);
                yield return null;
            }

            if (_canvas != null)
                Destroy(_canvas.gameObject);
            _canvas = null;
            _group = null;
        }
    }
}
