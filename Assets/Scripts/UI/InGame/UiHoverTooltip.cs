using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace LitIso.UI.InGame
{
    /// <summary>
    /// Shared floating name tooltip (2026-06-13, owner request: hotbar slots need
    /// hover names). Lazily builds its own always-on-top canvas; Show/Hide are cheap
    /// (no rebuild) so it's safe to call every pointer-move frame.
    /// </summary>
    internal static class UiTooltip
    {
        static Canvas _canvas;
        static RectTransform _root;
        static Text _label;

        static void EnsureBuilt()
        {
            if (_root != null) return;
            _canvas = UiBuilder.NewCanvas(null, "TooltipCanvas", 1000);
            UnityEngine.Object.DontDestroyOnLoad(_canvas.gameObject);
            var bg = UiBuilder.NewPanel(_canvas.transform, "Tooltip", "tooltip_bg", new Color(0.05f, 0.05f, 0.08f, 0.95f));
            _root = bg.rectTransform;
            _root.pivot = Vector2.zero;
            _root.anchorMin = _root.anchorMax = Vector2.zero;
            _label = UiBuilder.NewText(_root, "Label", "", 14, TextAnchor.MiddleCenter);
            var lr = _label.rectTransform;
            lr.anchorMin = Vector2.zero; lr.anchorMax = Vector2.one;
            lr.offsetMin = new Vector2(8f, 2f); lr.offsetMax = new Vector2(-8f, -2f);
            _root.gameObject.SetActive(false);
        }

        internal static void Show(string text, Vector2 screenPos)
        {
            if (string.IsNullOrEmpty(text)) { Hide(); return; }
            EnsureBuilt();
            _label.text = text;
            float w = Mathf.Clamp(text.Length * 8f + 20f, 48f, 420f);
            _root.sizeDelta = new Vector2(w, 28f);
            _root.position = screenPos + new Vector2(14f, 14f);
            _root.gameObject.SetActive(true);
            _root.SetAsLastSibling();
        }

        internal static void Hide()
        {
            if (_root != null) _root.gameObject.SetActive(false);
        }
    }

    /// <summary>
    /// Attach to any raycast-target UI element; shows a floating tooltip with the
    /// text from <see cref="textProvider"/> while the pointer hovers it. Re-evaluated
    /// on every pointer move so it stays correct as the underlying slot changes
    /// (item swapped, stack count updated, etc.) without re-hovering.
    /// </summary>
    internal sealed class UiHoverTooltip : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerMoveHandler
    {
        public Func<string> textProvider;

        public void OnPointerEnter(PointerEventData e) => Refresh(e.position);
        public void OnPointerMove(PointerEventData e) => Refresh(e.position);
        public void OnPointerExit(PointerEventData e) => UiTooltip.Hide();
        void OnDisable() => UiTooltip.Hide();

        void Refresh(Vector2 screenPos)
        {
            string text = textProvider != null ? textProvider() : null;
            UiTooltip.Show(text, screenPos);
        }
    }
}
