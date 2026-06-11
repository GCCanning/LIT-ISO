using System;
using System.Collections.Generic;
using IsoCore.Foundation;
using UnityEngine;
using UnityEngine.UI;

namespace LitIso.UI.InGame
{
    // ------------------------------------------------------------------------
    // Day-7 Class Assignment ceremony (owner-approved progression rework).
    // Contract + placeholder now; FoundationClassAssignmentAdapter binds the
    // real trial scoring / offer generation when Codex's runtime lands.
    // Preview in-game: F8.
    // ------------------------------------------------------------------------

    public struct ClassOfferData
    {
        public string id;
        public string name;
        public string rarity;        // Common / Uncommon / Rare / Epic
        public string flavor;
        public string[] receipts;    // "Evidence: 31 defense", ...
    }

    public interface IClassAssignmentViewModel
    {
        string Rank { get; }                 // F..S
        string RankFlavor { get; }
        string[] AxisLines { get; }          // volume/variety/difficulty/quality receipts
        int StartingPoints { get; }
        int OfferCount { get; }
        ClassOfferData GetOffer(int i);
        void Choose(string classId);
        event Action Closed;
    }

    public sealed class PlaceholderClassAssignmentViewModel : IClassAssignmentViewModel
    {
        readonly ClassOfferData[] _offers =
        {
            new ClassOfferData { id = "iron_warden", name = "Iron Warden", rarity = "Uncommon",
                flavor = "The wall others shelter behind.",
                receipts = new[]{ "Evidence: 31 defense", "Evidence: 18 mining", "Cleared a Tier-1 rift" } },
            new ClassOfferData { id = "ashvein_pyromancer", name = "Ashvein Pyromancer", rarity = "Rare",
                flavor = "Ember answers before you call.",
                receipts = new[]{ "Ember affinity: 11 (awakened)", "Evidence: 24 spellwork", "9 flawless casts" } },
            new ClassOfferData { id = "wayfarer", name = "Wayfarer", rarity = "Common",
                flavor = "Every road remembers you.",
                receipts = new[]{ "Evidence: 27 exploration", "11 landmarks found", "Traded with 3 vendors" } },
        };

        public string Rank => "B";
        public string RankFlavor => "Top 31% of recorded trials";
        public string[] AxisLines => new[]
        {
            "Volume — 142 recorded acts",
            "Variety — 8 of 11 disciplines attempted",
            "Difficulty — Tier-2 rift cleared, day 5",
            "Quality — 87% clean outcomes",
        };
        public int StartingPoints => 4;
        public int OfferCount => _offers.Length;
        public ClassOfferData GetOffer(int i) => _offers[i];
        public event Action Closed;
        public void Choose(string classId)
        {
            Debug.Log($"[ClassAssignment] (placeholder) chose {classId}");
            Closed?.Invoke();
        }
    }

    /// <summary>Full-screen System-space ceremony: void backdrop with drifting
    /// motes, rank reveal with receipts, then the class offers. The calling-card
    /// layout is recycled here per the design.</summary>
    public sealed class ClassAssignmentView : MonoBehaviour
    {
        public static void Show(IClassAssignmentViewModel vm)
        {
            var go = new GameObject("ClassAssignmentView");
            go.AddComponent<ClassAssignmentView>().Init(vm);
        }

        IClassAssignmentViewModel _vm;
        Canvas _canvas;
        string _selectedId;
        readonly List<(string id, Image bg)> _cards = new List<(string, Image)>();
        Button _confirm;

        void Init(IClassAssignmentViewModel vm)
        {
            _vm = vm;
            _vm.Closed += Close;
            Build();
            FoundationUiCoordinator.Active?.SetModalOpen("ceremony", true);
        }

        void OnDestroy()
        {
            if (_vm != null) _vm.Closed -= Close;
            FoundationUiCoordinator.Active?.SetModalOpen("ceremony", false);
        }

        void Close()
        {
            if (_canvas != null) Destroy(_canvas.gameObject);
            Destroy(gameObject);
        }

        void Build()
        {
            _canvas = UiBuilder.NewCanvas(transform, "CeremonyCanvas", 6000);

            // void backdrop + ambient motes (reuses the menu particle layer)
            var bg = UiBuilder.NewImage(_canvas.transform, "Void", null, new Color(0.015f, 0.02f, 0.035f, 0.985f));
            UiBuilder.Stretch(bg.rectTransform);

            // Cinematic backdrop, used when art exists at
            // Resources/UI/ClassSelection/background (the flat void color above
            // stays as the fallback when it doesn't). Slow Ken Burns drift +
            // dark scrim so the ceremony text stays readable over the art.
            var sceneSprite = Resources.Load<Sprite>("UI/ClassSelection/background");
            if (sceneSprite != null)
            {
                var scene = UiBuilder.NewImage(_canvas.transform, "Backdrop", sceneSprite, Color.white);
                UiBuilder.Stretch(scene.rectTransform);
                scene.preserveAspect = false;
                scene.raycastTarget = false;
                var drift = scene.gameObject.AddComponent<MenuCinematicBackground>();
                drift.cycleSeconds = 45f; // extra slow for the ceremony
                var scrim = UiBuilder.NewImage(_canvas.transform, "BackdropScrim", null,
                    new Color(0f, 0f, 0f, 0.45f));
                UiBuilder.Stretch(scrim.rectTransform);
                scrim.raycastTarget = false;
            }

            var motes = new GameObject("Motes", typeof(RectTransform));
            motes.transform.SetParent(_canvas.transform, false);
            UiBuilder.Stretch((RectTransform)motes.transform);
            motes.AddComponent<MenuAmbientParticles>();

            var header = UiBuilder.NewText(_canvas.transform, "Header",
                "[ SYSTEM ]  TRIAL COMPLETE", 30, TextAnchor.UpperCenter,
                new Color(0.55f, 0.85f, 1f, 1f));
            Top(header.rectTransform, -48f, 900f, 44f);

            var rank = UiBuilder.NewText(_canvas.transform, "Rank",
                $"RANK  {_vm.Rank}", 64, TextAnchor.UpperCenter,
                new Color(0.98f, 0.85f, 0.45f, 1f));
            Top(rank.rectTransform, -104f, 900f, 80f);

            var flavor = UiBuilder.NewText(_canvas.transform, "RankFlavor",
                _vm.RankFlavor + $"   ·   {_vm.StartingPoints} starting skill points", 17,
                TextAnchor.UpperCenter, UiBuilder.MutedCol);
            Top(flavor.rectTransform, -188f, 900f, 26f);

            var axes = _vm.AxisLines;
            for (int i = 0; i < axes.Length; i++)
            {
                var line = UiBuilder.NewText(_canvas.transform, "Axis" + i, axes[i], 15,
                    TextAnchor.UpperCenter, UiBuilder.TextCol);
                Top(line.rectTransform, -228f - i * 24f, 760f, 22f);
            }

            var offerLabel = UiBuilder.NewText(_canvas.transform, "OfferLabel",
                "The System extends the following paths:", 18,
                TextAnchor.UpperCenter, new Color(0.55f, 0.85f, 1f, 1f));
            Top(offerLabel.rectTransform, -350f, 900f, 28f);

            int n = _vm.OfferCount;
            float cardW = 340f, gap = 28f;
            float totalW = n * cardW + (n - 1) * gap;
            for (int i = 0; i < n; i++)
                BuildCard(_vm.GetOffer(i), -totalW / 2f + i * (cardW + gap), cardW);

            var confirmBg = UiBuilder.NewPanel(_canvas.transform, "Confirm", "button",
                new Color(0.95f, 0.72f, 0.25f, 0.95f));
            var crt = confirmBg.rectTransform;
            crt.anchorMin = crt.anchorMax = new Vector2(0.5f, 0f);
            crt.pivot = new Vector2(0.5f, 0f);
            crt.anchoredPosition = new Vector2(0f, 56f);
            crt.sizeDelta = new Vector2(280f, 56f);
            var cl = UiBuilder.NewText(confirmBg.transform, "L", "ACCEPT THIS PATH", 18,
                TextAnchor.MiddleCenter, new Color(0.12f, 0.10f, 0.04f, 1f));
            UiBuilder.Stretch(cl.rectTransform);
            cl.raycastTarget = false;
            _confirm = confirmBg.gameObject.AddComponent<Button>();
            _confirm.targetGraphic = confirmBg;
            _confirm.interactable = false;
            _confirm.onClick.AddListener(() => { if (_selectedId != null) _vm.Choose(_selectedId); });
        }

        void BuildCard(ClassOfferData offer, float x, float w)
        {
            var card = UiBuilder.NewPanel(_canvas.transform, "Offer_" + offer.id, "system_row", UiBuilder.SlotBg);
            var rt = card.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0f, 0.5f);
            rt.anchoredPosition = new Vector2(x, -60f);
            rt.sizeDelta = new Vector2(w, 290f);
            _cards.Add((offer.id, card));

            var btn = card.gameObject.AddComponent<Button>();
            btn.targetGraphic = card;
            string id = offer.id;
            btn.onClick.AddListener(() => Select(id));

            Color rarityCol = offer.rarity switch
            {
                "Epic" => new Color(0.75f, 0.45f, 0.95f, 1f),
                "Rare" => new Color(0.45f, 0.65f, 0.98f, 1f),
                "Uncommon" => new Color(0.45f, 0.85f, 0.55f, 1f),
                _ => UiBuilder.MutedCol,
            };

            var name = UiBuilder.NewText(card.transform, "Name", offer.name, 21,
                TextAnchor.UpperCenter, new Color(0.98f, 0.85f, 0.45f, 1f));
            InCard(name.rectTransform, -14f, 30f);
            var rarity = UiBuilder.NewText(card.transform, "Rarity", offer.rarity.ToUpperInvariant(),
                13, TextAnchor.UpperCenter, rarityCol);
            InCard(rarity.rectTransform, -46f, 20f);
            var flavor = UiBuilder.NewText(card.transform, "Flavor", offer.flavor, 14,
                TextAnchor.UpperCenter, UiBuilder.MutedCol);
            flavor.fontStyle = FontStyle.Italic;
            InCard(flavor.rectTransform, -72f, 24f);

            for (int i = 0; i < offer.receipts.Length; i++)
            {
                var r = UiBuilder.NewText(card.transform, "R" + i, "·  " + offer.receipts[i], 13,
                    TextAnchor.UpperLeft, UiBuilder.TextCol);
                var rr = r.rectTransform;
                rr.anchorMin = new Vector2(0f, 1f); rr.anchorMax = new Vector2(1f, 1f);
                rr.pivot = new Vector2(0.5f, 1f);
                rr.anchoredPosition = new Vector2(0f, -116f - i * 26f);
                rr.offsetMin = new Vector2(20f, rr.offsetMin.y);
                rr.offsetMax = new Vector2(-12f, rr.offsetMax.y);
                rr.sizeDelta = new Vector2(rr.sizeDelta.x, 24f);
            }

            var hint = UiBuilder.NewText(card.transform, "Hint", "select", 12,
                TextAnchor.LowerCenter, UiBuilder.MutedCol);
            var hr = hint.rectTransform;
            hr.anchorMin = new Vector2(0f, 0f); hr.anchorMax = new Vector2(1f, 0f);
            hr.pivot = new Vector2(0.5f, 0f);
            hr.anchoredPosition = new Vector2(0f, 10f);
            hr.sizeDelta = new Vector2(-20f, 20f);
        }

        static void Top(RectTransform rt, float y, float w, float h)
        {
            rt.anchorMin = new Vector2(0.5f, 1f);
            rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0f, y);
            rt.sizeDelta = new Vector2(w, h);
        }

        void InCard(RectTransform rt, float y, float h)
        {
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0f, y);
            rt.sizeDelta = new Vector2(-16f, h);
        }

        void Select(string id)
        {
            _selectedId = id;
            foreach (var (cid, bg) in _cards)
                bg.color = cid == id ? new Color(0.22f, 0.26f, 0.36f, 0.98f) : UiBuilder.SlotBg;
            if (_confirm != null) _confirm.interactable = true;
        }
    }
}
