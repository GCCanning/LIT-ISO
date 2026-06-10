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
        float _readableSkinTextScale = -1f;

        static float TextScale => Mathf.Clamp(PlayerPrefs.GetFloat("ui.textScale", 1.08f), 0.8f, 1.45f);

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
            float textScale = TextScale;
            float sw = Screen.width / scale;
            float sh = Screen.height / scale;
            float width = Mathf.Clamp(310f + (textScale - 1f) * 120f, 310f, 460f);
            float rowHeight = Mathf.Max(36f, 36f * textScale);
            float height = 50f + Mathf.Max(1, _contextActions.Length) * (rowHeight + 8f);
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
            float textScale = TextScale;
            var oldSkin = GUI.skin;
            GUI.skin = ReadableSkin();
            bool showPassiveHud = showPassiveMessages &&
                                  FoundationUiCoordinator.CurrentHudViewMode != FoundationHudViewMode.Hidden;

            if (_contextOpen)
                _contextRect = GUI.Window(7401, _contextRect, DrawContextWindow, _contextTitle);

            if (showPassiveHud && !string.IsNullOrEmpty(_tutorial))
            {
                var r = new Rect(20, 62, Mathf.Min(780f, sw - 40f), Mathf.Max(76f, 72f * textScale));
                GUI.Box(r, GUIContent.none);
                GUI.Label(new Rect(r.x + 12f, r.y + 8f, r.width - 24f, r.height - 16f), _tutorial);
            }

            if (showPassiveHud && !string.IsNullOrEmpty(_flash))
                GUI.Label(new Rect(sw / 2f - 240f, sh - 170f, 480f, Mathf.Max(32f, 30f * textScale)), _flash);

            GUI.skin = oldSkin;
            GUI.matrix = oldMatrix;
        }

        void DrawContextWindow(int id)
        {
            float textScale = TextScale;
            float buttonHeight = Mathf.Max(34f, 34f * textScale);
            float rowStep = buttonHeight + Mathf.Max(6f, 6f * textScale);
            float y = 30f;
            if (_contextActions.Length == 0)
            {
                GUI.enabled = false;
                GUI.Button(new Rect(10f, y, _contextRect.width - 20f, buttonHeight), "No actions");
                GUI.enabled = true;
            }

            foreach (var action in _contextActions)
            {
                bool canClick = action != null && action.enabled && action.execute != null;
                string label = action != null ? action.label : "";
                if (!canClick && action != null && !string.IsNullOrWhiteSpace(action.disabledReason))
                    label += $" ({action.disabledReason})";

                GUI.enabled = canClick;
                if (GUI.Button(new Rect(10f, y, _contextRect.width - 20f, buttonHeight), label))
                {
                    _contextOpen = false;
                    action.execute();
                }
                GUI.enabled = true;
                y += rowStep;
            }

            GUI.DragWindow(new Rect(0, 0, 10000, Mathf.Max(22f, 22f * textScale)));
        }

        GUISkin ReadableSkin()
        {
            float textScale = Mathf.Clamp(PlayerPrefs.GetFloat("ui.textScale", 1.08f), 0.8f, 1.45f);
            if (_readableSkin != null && Mathf.Abs(_readableSkinTextScale - textScale) < 0.001f)
                return _readableSkin;

            _readableSkin = Instantiate(GUI.skin);
            _readableSkinTextScale = textScale;
            _readableSkin.label.fontSize = Mathf.Max(12, Mathf.RoundToInt(18f * textScale));
            _readableSkin.label.normal.textColor = new Color(1f, 0.96f, 0.78f);
            _readableSkin.button.fontSize = Mathf.Max(12, Mathf.RoundToInt(17f * textScale));
            _readableSkin.window.fontSize = Mathf.Max(12, Mathf.RoundToInt(18f * textScale));
            _readableSkin.box.fontSize = Mathf.Max(12, Mathf.RoundToInt(17f * textScale));
            return _readableSkin;
        }
    }
}
