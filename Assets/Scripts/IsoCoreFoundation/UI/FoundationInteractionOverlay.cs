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
    /// Foundation-owned lightweight interaction overlay for world context menus and
    /// tutorial notices. It is not a backup HUD; uGUI is the canonical HUD/panel shell.
    /// </summary>
    public class FoundationInteractionOverlay : MonoBehaviour
    {
        [Tooltip("Debug-only passive IMGUI prompts. Leave off so the uGUI HUD is the only always-on shell.")]
        public bool showPassiveMessages = false;

        string _flash = "";
        float _flashTimer;
        string _tutorial = "";
        float _tutorialTimer;

        bool _contextOpen;
        string _contextTitle = "";
        FoundationContextAction[] _contextActions = Array.Empty<FoundationContextAction>();
        Rect _contextRect;
        GUISkin _readableSkin;

        public bool PointerOverUI
        {
            get
            {
                if (!_contextOpen) return false;
                Vector2 guiPoint = new(Input.mousePosition.x, Screen.height - Input.mousePosition.y);
                float scale = FoundationUiCoordinator.UiScale;
                if (Mathf.Abs(scale - 1f) > 0.01f)
                    guiPoint /= scale;
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

            float scale = FoundationUiCoordinator.UiScale;
            float sw = Screen.width / scale;
            float sh = Screen.height / scale;
            float width = 310f;
            float height = 46f + Mathf.Max(1, _contextActions.Length) * 44f;
            float x = Mathf.Clamp(screenPosition.x / scale, 8f, sw - width - 8f);
            float y = Mathf.Clamp((Screen.height - screenPosition.y) / scale, 8f, sh - height - 8f);
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
            var oldMatrix = GUI.matrix;
            float scale = FoundationUiCoordinator.UiScale;
            if (Mathf.Abs(scale - 1f) > 0.01f)
                GUIUtility.ScaleAroundPivot(new Vector2(scale, scale), Vector2.zero);

            float sw = Screen.width / scale;
            float sh = Screen.height / scale;
            var oldSkin = GUI.skin;
            GUI.skin = ReadableSkin();
            bool showPassiveHud = showPassiveMessages &&
                                  FoundationUiCoordinator.CurrentHudViewMode != FoundationHudViewMode.Hidden;

            if (_contextOpen)
                _contextRect = GUI.Window(7401, _contextRect, DrawContextWindow, _contextTitle);

            if (showPassiveHud && !string.IsNullOrEmpty(_tutorial))
            {
                var r = new Rect(20, 62, Mathf.Min(720f, sw - 40f), 76f);
                GUI.Box(r, GUIContent.none);
                GUI.Label(new Rect(r.x + 12f, r.y + 8f, r.width - 24f, r.height - 16f), _tutorial);
            }

            if (showPassiveHud && !string.IsNullOrEmpty(_flash))
                GUI.Label(new Rect(sw / 2f - 220f, sh - 160f, 440f, 32f), _flash);

            GUI.skin = oldSkin;
            GUI.matrix = oldMatrix;
        }

        void DrawContextWindow(int id)
        {
            float y = 30f;
            if (_contextActions.Length == 0)
            {
                GUI.enabled = false;
                GUI.Button(new Rect(10f, y, _contextRect.width - 20f, 36f), "No actions");
                GUI.enabled = true;
            }

            foreach (var action in _contextActions)
            {
                bool canClick = action != null && action.enabled && action.execute != null;
                string label = action != null ? action.label : "";
                if (!canClick && action != null && !string.IsNullOrWhiteSpace(action.disabledReason))
                    label += $" ({action.disabledReason})";

                GUI.enabled = canClick;
                if (GUI.Button(new Rect(10f, y, _contextRect.width - 20f, 36f), label))
                {
                    _contextOpen = false;
                    action.execute();
                }
                GUI.enabled = true;
                y += 44f;
            }

            GUI.DragWindow(new Rect(0, 0, 10000, 22));
        }

        GUISkin ReadableSkin()
        {
            if (_readableSkin != null) return _readableSkin;
            _readableSkin = Instantiate(GUI.skin);
            _readableSkin.label.fontSize = 18;
            _readableSkin.label.normal.textColor = new Color(1f, 0.96f, 0.78f);
            _readableSkin.button.fontSize = 17;
            _readableSkin.window.fontSize = 18;
            _readableSkin.box.fontSize = 17;
            return _readableSkin;
        }
    }
}
