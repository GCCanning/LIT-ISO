using System;
using UnityEngine;

namespace IsoCore.Foundation
{
    public sealed class FoundationContextAction
    {
        public readonly string id;
        public readonly string label;
        public readonly Action execute;
        public readonly bool enabled;
        public readonly string disabledReason;

        public FoundationContextAction(string id, string label, Action execute,
            bool enabled = true, string disabledReason = "")
        {
            this.id = id;
            this.label = label;
            this.execute = execute;
            this.enabled = enabled;
            this.disabledReason = disabledReason;
        }
    }

    /// <summary>
    /// Foundation-owned lightweight IMGUI overlay for world context menus and tutorial
    /// notices. Claude can replace the view later while keeping the interaction model.
    /// </summary>
    public class FoundationInteractionOverlay : MonoBehaviour
    {
        string _flash = "";
        float _flashTimer;
        string _tutorial = "";
        float _tutorialTimer;

        bool _contextOpen;
        string _contextTitle = "";
        FoundationContextAction[] _contextActions = Array.Empty<FoundationContextAction>();
        Rect _contextRect;

        public bool PointerOverUI
        {
            get
            {
                if (!_contextOpen) return false;
                Vector2 guiPoint = new(Input.mousePosition.x, Screen.height - Input.mousePosition.y);
                return _contextRect.Contains(guiPoint);
            }
        }

        public void Flash(string message, float seconds = 2.5f)
        {
            _flash = message ?? "";
            _flashTimer = Mathf.Max(0.1f, seconds);
        }

        public void Tutorial(string message, float seconds = 6f)
        {
            _tutorial = message ?? "";
            _tutorialTimer = Mathf.Max(0.1f, seconds);
        }

        public void OpenContextMenu(string title, Vector2 screenPosition,
            FoundationContextAction[] actions)
        {
            _contextTitle = string.IsNullOrWhiteSpace(title) ? "Options" : title;
            _contextActions = actions ?? Array.Empty<FoundationContextAction>();

            float width = 230f;
            float height = 34f + Mathf.Max(1, _contextActions.Length) * 34f;
            float x = Mathf.Clamp(screenPosition.x, 8f, Screen.width - width - 8f);
            float y = Mathf.Clamp(Screen.height - screenPosition.y, 8f, Screen.height - height - 8f);
            _contextRect = new Rect(x, y, width, height);
            _contextOpen = true;
        }

        public void CloseContextMenu() => _contextOpen = false;

        void Update()
        {
            if (_flashTimer > 0f)
            {
                _flashTimer -= Time.deltaTime;
                if (_flashTimer <= 0f) _flash = "";
            }

            if (_tutorialTimer > 0f)
            {
                _tutorialTimer -= Time.deltaTime;
                if (_tutorialTimer <= 0f) _tutorial = "";
            }
        }

        void OnGUI()
        {
            if (_contextOpen)
                _contextRect = GUI.Window(7401, _contextRect, DrawContextWindow, _contextTitle);

            if (!string.IsNullOrEmpty(_tutorial))
            {
                var r = new Rect(20, 58, Mathf.Min(560f, Screen.width - 40f), 52f);
                GUI.Box(r, GUIContent.none);
                GUI.Label(new Rect(r.x + 12f, r.y + 8f, r.width - 24f, r.height - 16f), _tutorial);
            }

            if (!string.IsNullOrEmpty(_flash))
                GUI.Label(new Rect(Screen.width / 2f - 180f, Screen.height - 150f, 360f, 24f), _flash);
        }

        void DrawContextWindow(int id)
        {
            float y = 24f;
            if (_contextActions.Length == 0)
            {
                GUI.enabled = false;
                GUI.Button(new Rect(10f, y, _contextRect.width - 20f, 28f), "No actions");
                GUI.enabled = true;
            }

            foreach (var action in _contextActions)
            {
                bool canClick = action != null && action.enabled && action.execute != null;
                string label = action != null ? action.label : "";
                if (!canClick && action != null && !string.IsNullOrWhiteSpace(action.disabledReason))
                    label += $" ({action.disabledReason})";

                GUI.enabled = canClick;
                if (GUI.Button(new Rect(10f, y, _contextRect.width - 20f, 28f), label))
                {
                    _contextOpen = false;
                    action.execute();
                }
                GUI.enabled = true;
                y += 34f;
            }

            GUI.DragWindow(new Rect(0, 0, 10000, 22));
        }
    }
}
