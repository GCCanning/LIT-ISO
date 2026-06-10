using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using IsoCore.Foundation;

namespace LitIso.UI.InGame
{
    /// <summary>
    /// Procedural "System" message feed. Listens to <see cref="SystemNotifier.OnMessage"/>
    /// and renders each announcement as a fading toast stacked down the mid-left of the
    /// screen. Colour is keyed off <see cref="SystemNotifier.MessageType"/>.
    ///
    /// Foundation-free by design — it references only UnityEngine + the global
    /// <see cref="SystemNotifier"/>, never IsoCore.Foundation. Because it listens on the
    /// SystemNotifier channel, both Foundation-driven messages (via
    /// <see cref="FoundationNotificationBridge"/>) and any legacy
    /// <c>SystemNotifier.Announce()</c> call surface here transparently.
    ///
    /// No prefab — built via <see cref="UiBuilder"/>. Spawned by
    /// <see cref="GameHudInitializer"/>; self-subscribes in OnEnable.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class InGameNotificationView : MonoBehaviour
    {
        const int   MaxVisible = 5;
        const float FadeIn     = 0.30f;
        const float Hold       = 2.50f;
        const float FadeOut    = 0.45f;
        const float ToastW     = 360f;

        Canvas        _canvas;
        RectTransform _stack;
        FoundationHudViewMode _hudMode = FoundationHudViewMode.Adventure;
        readonly Queue<GameObject> _live = new();

        // ---- message palette -------------------------------------------------
        static Color ColorFor(SystemNotifier.MessageType type) => type switch
        {
            SystemNotifier.MessageType.LevelUp       => new Color(0.45f, 0.85f, 1.00f, 1f), // cyan
            SystemNotifier.MessageType.ClassAssign   => new Color(1.00f, 0.85f, 0.30f, 1f), // gold
            SystemNotifier.MessageType.DungeonClear  => new Color(0.40f, 0.80f, 0.55f, 1f), // green
            SystemNotifier.MessageType.WorldEvent    => new Color(1.00f, 0.55f, 0.30f, 1f), // orange
            SystemNotifier.MessageType.Warning       => new Color(1.00f, 0.80f, 0.30f, 1f), // amber
            SystemNotifier.MessageType.Achievement   => new Color(0.80f, 0.60f, 0.95f, 1f), // purple
            SystemNotifier.MessageType.QuestNew      => new Color(0.72f, 0.78f, 0.98f, 1f), // soft purple
            SystemNotifier.MessageType.QuestComplete => new Color(1.00f, 0.85f, 0.30f, 1f), // gold
            _                                        => new Color(0.95f, 0.91f, 0.74f, 1f), // cream (Info)
        };

        // ---- Unity lifecycle -------------------------------------------------

        void Awake()  => BuildUI();
        void OnEnable()
        {
            SystemNotifier.OnMessage += HandleMessage;
            FoundationUiCoordinator.HudViewModeChanged += ApplyHudViewMode;
            LitIsoFont.TextScaleChanged += HandleTextScaleChanged;
            ApplyHudViewMode(FoundationUiCoordinator.CurrentHudViewMode);
        }

        void OnDisable()
        {
            SystemNotifier.OnMessage -= HandleMessage;
            FoundationUiCoordinator.HudViewModeChanged -= ApplyHudViewMode;
            LitIsoFont.TextScaleChanged -= HandleTextScaleChanged;
        }

        // ---- construction ----------------------------------------------------

        void BuildUI()
        {
            if (_canvas != null)
                Destroy(_canvas.gameObject);

            _canvas = UiBuilder.NewCanvas(transform, "NotificationCanvas", sortingOrder: 30);

            // Upper-left column below vitals, growing downward. Newest toast is inserted at the top.
            var stackGo = new GameObject("NotificationStack", typeof(RectTransform));
            stackGo.transform.SetParent(_canvas.transform, false);
            _stack = stackGo.GetComponent<RectTransform>();
            _stack.anchorMin = new Vector2(0f, 1f);
            _stack.anchorMax = new Vector2(0f, 1f);
            _stack.pivot     = new Vector2(0f, 1f);
            _stack.anchoredPosition = new Vector2(24f, -228f);
            _stack.sizeDelta = new Vector2(ToastW, 0f);
            PlayerResizableUi.Attach(_stack, "hud.notifications", new Vector2(260f, 80f), new Vector2(900f, 520f));

            var layout = stackGo.AddComponent<VerticalLayoutGroup>();
            layout.childAlignment       = TextAnchor.UpperLeft;
            layout.spacing              = 6f;
            layout.childControlWidth    = true;
            layout.childControlHeight   = true;
            layout.childForceExpandWidth  = true;
            layout.childForceExpandHeight = false;

            var fitter = stackGo.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            ApplyHudViewMode(_hudMode);
        }

        // ---- message handling ------------------------------------------------

        void HandleMessage(string text, SystemNotifier.MessageType type)
        {
            if (_stack == null || string.IsNullOrWhiteSpace(text)) return;
            if (_hudMode == FoundationHudViewMode.Hidden)
                return;

            var toast = BuildToast(text, ColorFor(type));
            toast.transform.SetParent(_stack, false);
            toast.transform.SetAsFirstSibling(); // newest on top

            _live.Enqueue(toast);
            while (_live.Count > MaxVisible)
            {
                var oldest = _live.Dequeue();
                if (oldest != null) Destroy(oldest);
            }

            StartCoroutine(Animate(toast));
        }

        void ApplyHudViewMode(FoundationHudViewMode mode)
        {
            _hudMode = mode;
            bool show = mode != FoundationHudViewMode.Hidden;
            if (_canvas != null && _canvas.gameObject.activeSelf != show)
                _canvas.gameObject.SetActive(show);
        }

        void HandleTextScaleChanged(float _)
        {
            StopAllCoroutines();
            _live.Clear();
            BuildUI();
            ApplyHudViewMode(FoundationUiCoordinator.CurrentHudViewMode);
        }

        GameObject BuildToast(string text, Color accent)
        {
            var go = new GameObject("Toast", typeof(RectTransform));
            var bg = go.AddComponent<Image>();
            bg.color = new Color(0.06f, 0.08f, 0.12f, 0.92f);
            bg.raycastTarget = false;

            var group = go.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = false;
            group.interactable = false;

            // Padding so text doesn't sit flush against the pill edge.
            var layout = go.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(12, 12, 7, 7);
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth  = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;

            var fitter = go.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // Coloured left accent so message category reads at a glance.
            var accentGo = new GameObject("Accent", typeof(RectTransform));
            accentGo.transform.SetParent(go.transform, false);
            var accentImg = accentGo.AddComponent<Image>();
            accentImg.color = accent;
            accentImg.raycastTarget = false;
            var accLe = accentGo.AddComponent<LayoutElement>();
            accLe.minWidth = 4f; accLe.preferredWidth = 4f; accLe.flexibleWidth = 0f;

            var label = UiBuilder.NewText(go.transform, "Text", text, 16, TextAnchor.MiddleLeft, accent);
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.raycastTarget = false;
            var le = label.gameObject.AddComponent<LayoutElement>();
            le.flexibleWidth = 1f;

            return go;
        }

        IEnumerator Animate(GameObject toast)
        {
            var group = toast != null ? toast.GetComponent<CanvasGroup>() : null;
            if (group == null) yield break;

            yield return Fade(group, 0f, 1f, FadeIn);
            yield return new WaitForSeconds(Hold);
            yield return Fade(group, 1f, 0f, FadeOut);

            if (toast != null) Destroy(toast);
        }

        static IEnumerator Fade(CanvasGroup g, float from, float to, float dur)
        {
            float t = 0f;
            while (t < dur && g != null)
            {
                t += Time.unscaledDeltaTime;
                g.alpha = Mathf.Lerp(from, to, dur > 0f ? t / dur : 1f);
                yield return null;
            }
            if (g != null) g.alpha = to;
        }
    }
}
