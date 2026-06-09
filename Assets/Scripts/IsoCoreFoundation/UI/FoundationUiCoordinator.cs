using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace IsoCore.Foundation
{
    public enum FoundationHudViewMode
    {
        Basic = 0,
        Adventure = 1,
        Hidden = 2,
    }

    /// <summary>
    /// One lightweight authority for in-game UI ownership. Runtime-created UI surfaces
    /// use this to avoid double-consuming Esc, panel toggles, map toggles, and world
    /// clicks. uGUI is the canonical player-facing shell.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FoundationUiCoordinator : MonoBehaviour
    {
        readonly Dictionary<string, bool> _modals = new();

        public const string UiScalePrefKey = "ui.scale";
        public const string HudViewModePrefKey = "hud.viewMode";
        const FoundationHudViewMode StartupHudViewMode = FoundationHudViewMode.Adventure;

        public static FoundationUiCoordinator Active { get; private set; }
        public static event System.Action<FoundationHudViewMode> HudViewModeChanged;

        public static float UiScale =>
            Mathf.Clamp(PlayerPrefs.GetFloat(UiScalePrefKey, 1f), 0.75f, 1.75f);

        public static FoundationHudViewMode CurrentHudViewMode =>
            Active != null ? Active.HudViewMode : FoundationHudViewMode.Adventure;

        public FoundationHudViewMode HudViewMode { get; private set; } = FoundationHudViewMode.Adventure;

        public bool HasBlockingModal
        {
            get
            {
                foreach (var kv in _modals)
                    if (kv.Value)
                        return true;
                return false;
            }
        }

        public bool InputConsumedThisFrame { get; private set; }

        public bool PointerOverUgui =>
            EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();

        public bool BlocksWorldInput => HasBlockingModal || PointerOverUgui || InputConsumedThisFrame;

        void Awake()
        {
            if (Active != null && Active != this)
                Destroy(Active);
            Active = this;
            SuppressLegacyHudSurfaces();
            HudViewMode = StartupHudViewMode;
            PlayerPrefs.SetInt(HudViewModePrefKey, (int)HudViewMode);
            PlayerPrefs.Save();
        }

        void Start()
        {
            SuppressLegacyHudSurfaces();
        }

        void OnDestroy()
        {
            if (Active == this)
                Active = null;
        }

        void Update()
        {
            if (!Input.GetKeyDown(KeyCode.F1) || InputConsumedThisFrame)
                return;

            int direction = (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)) ? -1 : 1;
            CycleHudViewMode(direction);
            ConsumeInputThisFrame();
        }

        void LateUpdate()
        {
            InputConsumedThisFrame = false;
        }

        public void SetHudViewMode(FoundationHudViewMode mode)
        {
            if (!System.Enum.IsDefined(typeof(FoundationHudViewMode), mode))
                mode = FoundationHudViewMode.Adventure;

            if (HudViewMode == mode)
                return;

            HudViewMode = mode;
            PlayerPrefs.SetInt(HudViewModePrefKey, (int)mode);
            PlayerPrefs.Save();
            Debug.Log($"[FoundationUI] HUD view mode: {mode}");
            HudViewModeChanged?.Invoke(mode);
        }

        public void CycleHudViewMode(int direction = 1)
        {
            var modes = new[]
            {
                FoundationHudViewMode.Basic,
                FoundationHudViewMode.Adventure,
                FoundationHudViewMode.Hidden,
            };

            int index = 1;
            for (int i = 0; i < modes.Length; i++)
            {
                if (modes[i] == HudViewMode)
                {
                    index = i;
                    break;
                }
            }

            int next = (index + direction) % modes.Length;
            if (next < 0)
                next += modes.Length;
            SetHudViewMode(modes[next]);
        }

        public static void SuppressLegacyHudSurfaces()
        {
            var canvases = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < canvases.Length; i++)
            {
                var canvas = canvases[i];
                if (canvas == null || canvas.gameObject == null)
                    continue;

                if (IsLegacyHudSurface(canvas))
                    canvas.gameObject.SetActive(false);
            }
        }

        static bool IsLegacyHudSurface(Canvas canvas)
        {
            string name = canvas.gameObject.name;
            if (name == "GameplayHUD" || name == "TrialWeekHUD Canvas")
                return true;

            if (canvas.transform.Find("ZoomControls") != null)
                return true;

            var behaviours = canvas.GetComponentsInChildren<MonoBehaviour>(true);
            for (int i = 0; i < behaviours.Length; i++)
            {
                var behaviour = behaviours[i];
                if (behaviour == null)
                    continue;

                string type = behaviour.GetType().Name;
                if (type == "HealthBarUI" ||
                    type == "HotbarUI" ||
                    type == "GameTimeUI" ||
                    type == "ManaBarUI" ||
                    type == "XPBarUI" ||
                    type == "SpellHotbarUI" ||
                    type == "StatusEffectsUI" ||
                    type == "QuestTrackerUI" ||
                    type == "MovementDebugOverlay" ||
                    type == "GameSettingsMenu")
                    return true;
            }

            return false;
        }

        public void SetModalOpen(string id, bool open)
        {
            if (string.IsNullOrWhiteSpace(id))
                return;

            _modals[id] = open;
        }

        public void ConsumeInputThisFrame()
        {
            InputConsumedThisFrame = true;
        }

        public bool CanOpenPause()
        {
            return !InputConsumedThisFrame && !HasBlockingModal;
        }

        public bool CanToggleMap()
        {
            return !InputConsumedThisFrame && !HasBlockingModal;
        }
    }
}
