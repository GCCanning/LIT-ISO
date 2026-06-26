using System;
using System.Collections.Generic;
using IsoCore.Foundation;
using UnityEngine;
using UnityEngine.UI;

namespace LitIso.UI.InGame
{
    // ------------------------------------------------------------------------
    // Owner-confirmed combat input scheme: Q/E/R/F ability slots (tap = cast),
    // hold X = radial wheel (release on an ability = one-shot cast; while open,
    // click ability then click a slot anchor = assign). Contract + placeholder
    // until the Foundation ability runtime exposes cast/assign APIs.
    // ------------------------------------------------------------------------

    public struct AbilityData
    {
        public string id;
        public string name;
        public string costText;   // "ST 10" / "MP 12"
        public bool ready;        // off cooldown + affordable
    }

    public interface IAbilityLoadoutViewModel
    {
        int AbilityCount { get; }
        AbilityData GetAbility(int i);
        string GetSlot(int slot);              // ability id or null (slots 0..3 = Q E R F)
        void Assign(int slot, string abilityId);
        bool Cast(string abilityId);           // false = not ready
        event Action Changed;
    }

    /// <summary>Demo loadout persisted in PlayerPrefs so assignments survive
    /// sessions even before the runtime lands.</summary>
    public sealed class PlaceholderAbilityLoadoutViewModel : IAbilityLoadoutViewModel
    {
        static readonly AbilityData[] Known =
        {
            new AbilityData { id = "steady_strike", name = "Steady Strike", costText = "ST 10", ready = true },
            new AbilityData { id = "guard_step",    name = "Guard Step",    costText = "ST 8",  ready = true },
            new AbilityData { id = "mana_bolt",     name = "Mana Bolt",     costText = "MP 12", ready = true },
            new AbilityData { id = "ember_spark",   name = "Ember Spark",   costText = "MP 15", ready = false },
            new AbilityData { id = "root_snare",    name = "Root Snare",    costText = "MP 10", ready = true },
            new AbilityData { id = "stone_skin",    name = "Stone Skin",    costText = "ST 14", ready = true },
        };

        public int AbilityCount => Known.Length;
        public AbilityData GetAbility(int i) => Known[i];
        public event Action Changed;

        public string GetSlot(int slot)
        {
            var v = PlayerPrefs.GetString("ability.slot" + slot, "");
            return string.IsNullOrEmpty(v) ? null : v;
        }

        public void Assign(int slot, string abilityId)
        {
            PlayerPrefs.SetString("ability.slot" + slot, abilityId ?? "");
            PlayerPrefs.Save();
            Changed?.Invoke();
        }

        public bool Cast(string abilityId)
        {
            foreach (var a in Known)
                if (a.id == abilityId)
                {
                    Debug.Log($"[AbilityWheel] (placeholder) cast {a.name} — runtime pending");
                    return a.ready;
                }
            return false;
        }
    }

    /// <summary>HUD Q/E/R/F slot row + the hold-X radial wheel.</summary>
    [DisallowMultipleComponent]
    public sealed class AbilityWheelView : MonoBehaviour
    {
        static readonly KeyCode[] SlotKeys = { KeyCode.Q, KeyCode.E, KeyCode.R, KeyCode.F };
        static readonly string[] SlotNames = { "Q", "E", "R", "F" };

        IAbilityLoadoutViewModel _vm;
        Canvas _canvas;
        RectTransform _wheelRoot;
        Text[] _slotLabels;
        Image[] _slotBgs;
        Image[] _abilityBgs;
        string _pendingAssignId;   // ability clicked in wheel, awaiting anchor click

        public void Bind(IAbilityLoadoutViewModel vm)
        {
            if (_vm != null) _vm.Changed -= RefreshSlots;
            _vm = vm;
            _vm.Changed += RefreshSlots;
            if (_canvas == null) Build();
            RefreshSlots();
        }

        void OnDestroy() { if (_vm != null) _vm.Changed -= RefreshSlots; }

        void Build()
        {
            _canvas = UiBuilder.NewCanvas(transform, "AbilityCanvas", 210);

            // Retired visible Q/E/R/F slot ROW: the always-on ability bar now renders
            // via GameUIController.BuildAbilityBar (canonical HUD). We intentionally
            // skip building the duplicate bottom-left slot cells here. We KEEP this
            // view's functional layer: the hold-X radial wheel + tap-to-cast input
            // (Update/Cast/Assign), which the canonical HUD doesn't replicate.
            // _slotLabels/_slotBgs stay null — RefreshSlots()/FlashSlot() guard for it.

            BuildWheel();
        }

        void BuildWheel()
        {
            _wheelRoot = UiBuilder.NewRect("Wheel", _canvas.transform);
            _wheelRoot.anchorMin = _wheelRoot.anchorMax = new Vector2(0.5f, 0.5f);
            _wheelRoot.sizeDelta = new Vector2(560f, 560f);

            var dim = UiBuilder.NewImage(_wheelRoot, "Dim", null, new Color(0f, 0f, 0f, 0.45f));
            var dr = dim.rectTransform;
            dr.anchorMin = dr.anchorMax = new Vector2(0.5f, 0.5f);
            dr.sizeDelta = new Vector2(4000f, 4000f);

            int n = Mathf.Max(1, _vm.AbilityCount);
            _abilityBgs = new Image[n];
            for (int i = 0; i < n; i++)
            {
                var a = _vm.GetAbility(i);
                float ang = Mathf.PI / 2f - i * (2f * Mathf.PI / n);
                var seg = UiBuilder.NewPanel(_wheelRoot, "A_" + a.id, "system_row",
                    a.ready ? UiBuilder.SlotBg : new Color(0.07f, 0.08f, 0.10f, 0.7f));
                var rt = seg.rectTransform;
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = new Vector2(212f * Mathf.Cos(ang), 212f * Mathf.Sin(ang));
                rt.sizeDelta = new Vector2(132f, 64f);
                _abilityBgs[i] = seg;

                var label = UiBuilder.NewText(seg.transform, "L", $"{a.name}\n{a.costText}", 13,
                    TextAnchor.MiddleCenter, a.ready ? UiBuilder.TextCol : UiBuilder.MutedCol);
                UiBuilder.Stretch(label.rectTransform, 4f);
                label.raycastTarget = false;
                // Longer ability names must shrink to stay inside the wedge.
                UiBuilder.FitText(label);

                var btn = seg.gameObject.AddComponent<Button>();
                btn.targetGraphic = seg;
                string id = a.id;
                btn.onClick.AddListener(() => OnAbilityClicked(id));
            }

            for (int s = 0; s < 4; s++)
            {
                float ang = Mathf.PI / 2f - s * (Mathf.PI / 2f) - Mathf.PI / 4f;
                var anchor = UiBuilder.NewPanel(_wheelRoot, "Anchor" + SlotNames[s], "slot", UiBuilder.SlotBg);
                var rt = anchor.rectTransform;
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = new Vector2(86f * Mathf.Cos(ang), 86f * Mathf.Sin(ang));
                rt.sizeDelta = new Vector2(52f, 52f);
                var t = UiBuilder.NewText(anchor.transform, "K", SlotNames[s], 18,
                    TextAnchor.MiddleCenter, new Color(0.98f, 0.85f, 0.45f, 1f));
                UiBuilder.Stretch(t.rectTransform);
                t.raycastTarget = false;
                var btn = anchor.gameObject.AddComponent<Button>();
                btn.targetGraphic = anchor;
                int slot = s;
                btn.onClick.AddListener(() => OnAnchorClicked(slot));
            }

            var hint = UiBuilder.NewText(_wheelRoot, "Hint",
                "release on nothing = close · click ability then Q/E/R/F = assign", 13,
                TextAnchor.MiddleCenter, UiBuilder.MutedCol);
            var hr = hint.rectTransform;
            hr.anchorMin = hr.anchorMax = new Vector2(0.5f, 0.5f);
            hr.anchoredPosition = new Vector2(0f, -300f);
            hr.sizeDelta = new Vector2(700f, 24f);
            UiBuilder.FitText(hint);

            _wheelRoot.gameObject.SetActive(false);
        }

        void OnAbilityClicked(string id)
        {
            _pendingAssignId = id;
            for (int i = 0; i < _abilityBgs.Length; i++)
                _abilityBgs[i].color = _vm.GetAbility(i).id == id
                    ? new Color(0.22f, 0.26f, 0.36f, 0.98f) : UiBuilder.SlotBg;
        }

        void OnAnchorClicked(int slot)
        {
            if (_pendingAssignId == null) return;
            _vm.Assign(slot, _pendingAssignId);
            _pendingAssignId = null;
        }

        void RefreshSlots()
        {
            if (_slotLabels == null) return;
            for (int i = 0; i < 4; i++)
            {
                string id = _vm.GetSlot(i);
                string label = "";
                if (id != null)
                    for (int a = 0; a < _vm.AbilityCount; a++)
                        if (_vm.GetAbility(a).id == id) { label = _vm.GetAbility(a).name; break; }
                _slotLabels[i].text = label;
            }
        }

        void Update()
        {
            if (_vm == null) return;
            var ui = FoundationUiCoordinator.Active;

            // Only OTHER modals/panels gate the wheel — NOT our own "abilityWheel"
            // modal (nor pointer-over-UI, which is the wheel itself once open).
            // Using BlocksWorldInput here was the flicker bug: opening the wheel set
            // a modal that made BlocksWorldInput true, which forced it shut the next
            // frame, which cleared the modal, which reopened it… every frame.
            bool otherModal = ui != null && ui.HasBlockingModalExcept("abilityWheel");

            bool currentlyOpen = _wheelRoot.gameObject.activeSelf;
            bool xHeld = Input.GetKey(KeyCode.X);
            // Stay open purely on X while already open; only gate the INITIAL open on
            // other panels being closed.
            bool wheelOpen = currentlyOpen ? xHeld : (xHeld && !otherModal);

            if (currentlyOpen != wheelOpen)
            {
                _wheelRoot.gameObject.SetActive(wheelOpen);
                if (!wheelOpen) _pendingAssignId = null;
                ui?.SetModalOpen("abilityWheel", wheelOpen);
            }

            if (wheelOpen) return;   // wheel handles its own clicks; suppress tap-cast
            if (otherModal) return;  // another panel owns input

            // tap-to-cast on Q/E/R/F
            for (int i = 0; i < 4; i++)
                if (Input.GetKeyDown(SlotKeys[i]))
                {
                    string id = _vm.GetSlot(i);
                    if (id != null && _vm.Cast(id))
                        StartCoroutine(FlashSlot(i));
                }
        }

        System.Collections.IEnumerator FlashSlot(int i)
        {
            if (_slotBgs == null || _slotBgs[i] == null) yield break;
            var img = _slotBgs[i];
            var c = img.color;
            img.color = new Color(0.95f, 0.72f, 0.25f, 1f);
            yield return new WaitForSecondsRealtime(0.12f);
            if (img != null) img.color = c;
        }
    }
}
