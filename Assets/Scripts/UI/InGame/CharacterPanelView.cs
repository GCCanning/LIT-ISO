using System;
using System.Collections.Generic;
using IsoCore.Foundation;
using UnityEngine;
using UnityEngine.UI;

namespace LitIso.UI.InGame
{
    // Owner-approved UI overhaul (2026-06-10): 9-tab System Window.
    // Skills = the skill web (the old XP-bucket list is retired);
    // Journal = quests; System = save/quit + System log; Settings is new.
    public enum CharacterPanelTab
    {
        Inventory,
        Crafting,
        Skills,
        Spells,
        Character,
        Journal,
        Map,
        Settings,
        System,
    }

    /// <summary>
    /// Canonical in-game uGUI panel for the LitRPG shell. It replaces the separate
    /// inventory/crafting/status windows with one tabbed surface.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CharacterPanelView : MonoBehaviour
    {
        IInventoryViewModel _inventory;
        ICraftingViewModel _crafting;
        ICharacterSheetViewModel _character;
        ISkillWebViewModel _skillWeb;
        FoundationProgression _progression;
        FoundationQoLService _qol;

        Canvas _canvas;
        GameObject _root;
        RectTransform _body;
        Text _title;
        Button[] _tabButtons;
        CharacterPanelTab _activeTab = CharacterPanelTab.Inventory;
        string _selectedRecipeId;

        struct SkillUiBucket
        {
            public string title;
            public string subtitle;
            public Color color;
            public FoundationProgressionActivity[] activities;

            public SkillUiBucket(string title, string subtitle, Color color, params FoundationProgressionActivity[] activities)
            {
                this.title = title;
                this.subtitle = subtitle;
                this.color = color;
                this.activities = activities;
            }
        }

        static readonly SkillUiBucket[] SkillBuckets =
        {
            new SkillUiBucket("Combat & Warding",
                "Weapon timing, dungeon threat, patrol defense, and creature pressure.",
                new Color(0.92f, 0.38f, 0.24f, 1f),
                FoundationProgressionActivity.Combat, FoundationProgressionActivity.Creature),
            new SkillUiBucket("Gathering & Exploration",
                "Resource reading, routes, landmarks, harvest technique, and hidden finds.",
                new Color(0.50f, 0.78f, 0.48f, 1f),
                FoundationProgressionActivity.Harvest, FoundationProgressionActivity.Explore),
            new SkillUiBucket("Crafting & Building",
                "Stations, quality, repairs, structures, rooms, paths, and town utility.",
                new Color(0.88f, 0.68f, 0.36f, 1f),
                FoundationProgressionActivity.Craft, FoundationProgressionActivity.Build),
            new SkillUiBucket("Hearth & Settlement",
                "Farming, food, trade, comfort, requests, vendors, and visitor support.",
                new Color(0.72f, 0.56f, 0.92f, 1f),
                FoundationProgressionActivity.Farm, FoundationProgressionActivity.Trade),
            new SkillUiBucket("Lore & Magic",
                "Relics, memory pages, shrines, ward clues, affinities, and class evidence.",
                new Color(0.48f, 0.70f, 0.95f, 1f),
                FoundationProgressionActivity.Lore),
        };

        public bool IsOpen => _root != null && _root.activeSelf;
        public event Action Closed;

        public void Init(IInventoryViewModel inventory, ICraftingViewModel crafting,
            ICharacterSheetViewModel character, FoundationProgression progression, FoundationQoLService qol,
            ISkillWebViewModel skillWeb = null)
        {
            Unsubscribe();
            _inventory = inventory;
            _crafting = crafting;
            _character = character;
            _skillWeb = skillWeb;
            _progression = progression;
            _qol = qol;
            Build();
            Subscribe();
            Hide();
        }

        void OnDestroy() => Unsubscribe();
        void OnEnable() => LitIsoFont.TextScaleChanged += HandleTextScaleChanged;
        void OnDisable() => LitIsoFont.TextScaleChanged -= HandleTextScaleChanged;

        void Subscribe()
        {
            if (_inventory != null) _inventory.Changed += Refresh;
            if (_crafting != null) _crafting.Changed += Refresh;
            if (_character != null) _character.Changed += Refresh;
            if (_skillWeb != null) _skillWeb.Changed += Refresh;
            if (_progression != null) _progression.Changed += Refresh;
        }

        void Unsubscribe()
        {
            if (_inventory != null) _inventory.Changed -= Refresh;
            if (_crafting != null) _crafting.Changed -= Refresh;
            if (_character != null) _character.Changed -= Refresh;
            if (_skillWeb != null) _skillWeb.Changed -= Refresh;
            if (_progression != null) _progression.Changed -= Refresh;
        }

        public void Show(CharacterPanelTab tab)
        {
            _activeTab = tab;
            if (_root != null) _root.SetActive(true);
            Refresh();
        }

        public void Hide()
        {
            if (_root != null) _root.SetActive(false);
            Closed?.Invoke();
        }

        void Build()
        {
            if (_canvas != null) Destroy(_canvas.gameObject);
            _canvas = UiBuilder.NewCanvas(transform, "CharacterPanelCanvas", 220);
            _root = new GameObject("Root", typeof(RectTransform));
            _root.transform.SetParent(_canvas.transform, false);
            UiBuilder.Stretch(_root.GetComponent<RectTransform>());

            var scrim = UiBuilder.NewScrim(_root.transform);
            var scrimButton = scrim.gameObject.AddComponent<Button>();
            scrimButton.transition = Selectable.Transition.None;
            scrimButton.onClick.AddListener(Hide);

            var panel = UiBuilder.NewPanel(_root.transform, "Panel", "system_panel", UiBuilder.PanelBg);
            var pr = panel.rectTransform;
            pr.anchorMin = pr.anchorMax = new Vector2(0.5f, 0.5f);
            pr.pivot = new Vector2(0.5f, 0.5f);
            pr.sizeDelta = new Vector2(1080f, 720f);
            PlayerResizableUi.Attach(pr, "panel.character", new Vector2(720f, 460f), new Vector2(1700f, 980f));

            _title = UiBuilder.NewText(panel.transform, "Title", "Character", 26, TextAnchor.MiddleLeft);
            var tr = _title.rectTransform;
            tr.anchorMin = new Vector2(0f, 1f);
            tr.anchorMax = new Vector2(1f, 1f);
            tr.pivot = new Vector2(0f, 1f);
            tr.anchoredPosition = new Vector2(28f, -18f);
            tr.sizeDelta = new Vector2(-96f, 36f);

            var close = UiBuilder.NewButton(panel.transform, "Close", "btn_close", "X", 18);
            close.onClick.AddListener(Hide);
            var cr = close.GetComponent<RectTransform>();
            cr.anchorMin = cr.anchorMax = new Vector2(1f, 1f);
            cr.pivot = new Vector2(1f, 1f);
            cr.anchoredPosition = new Vector2(-18f, -18f);
            cr.sizeDelta = new Vector2(42f, 42f);

            BuildTabs(panel.transform);

            _body = UiBuilder.NewRect("Body", panel.transform);
            _body.anchorMin = Vector2.zero;
            _body.anchorMax = Vector2.one;
            _body.offsetMin = new Vector2(28f, 34f);
            _body.offsetMax = new Vector2(-28f, -116f);
        }

        void BuildTabs(Transform parent)
        {
            var tabs = (CharacterPanelTab[])Enum.GetValues(typeof(CharacterPanelTab));
            _tabButtons = new Button[tabs.Length];
            float x = 28f;
            for (int i = 0; i < tabs.Length; i++)
            {
                var tab = tabs[i];
                var btn = UiBuilder.NewButton(parent, "Tab_" + tab, "craft_row", LabelFor(tab), 14);
                var rt = btn.GetComponent<RectTransform>();
                rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
                rt.pivot = new Vector2(0f, 1f);
                rt.anchoredPosition = new Vector2(x, -70f);
                rt.sizeDelta = new Vector2(106f, 40f);
                x += 112f;
                // meta tabs (Settings/System) sit visually apart
                if (tab == CharacterPanelTab.Map) x += 24f;
                btn.onClick.AddListener(() => Show(tab));
                _tabButtons[i] = btn;
            }
        }

        void Refresh()
        {
            if (_body == null || !IsOpen) return;
            foreach (Transform child in _body) Destroy(child.gameObject);
            UpdateTabButtons();
            if (_title != null) _title.text = LabelFor(_activeTab);

            try
            {
                switch (_activeTab)
                {
                    case CharacterPanelTab.Inventory: DrawInventory(); break;
                    case CharacterPanelTab.Crafting: DrawCrafting(); break;
                    case CharacterPanelTab.Skills: SkillWebDrawer.Draw(_body, _skillWeb, Refresh); break;
                    case CharacterPanelTab.Spells: DrawSpells(); break;
                    case CharacterPanelTab.Character: DrawStatus(); break;
                    case CharacterPanelTab.Journal: DrawQuests(); break;
                    case CharacterPanelTab.Map: DrawMap(); break;
                    case CharacterPanelTab.Settings: DrawSettings(); break;
                    case CharacterPanelTab.System: DrawSystem(); break;
                }
            }
            catch (Exception e)
            {
                // A failing tab must never blank the whole panel; surface the
                // error in place so it can be reported and fixed.
                Debug.LogException(e);
                var err = UiBuilder.NewText(_body, "TabError",
                    $"This tab hit an error:\n{e.GetType().Name}: {e.Message}\n(see Console for stack)",
                    16, TextAnchor.UpperLeft, new Color(0.95f, 0.55f, 0.45f, 1f));
                err.horizontalOverflow = HorizontalWrapMode.Wrap;
                UiBuilder.Stretch(err.rectTransform, 8f);
            }
        }

        void UpdateTabButtons()
        {
            if (_tabButtons == null) return;
            var tabs = (CharacterPanelTab[])Enum.GetValues(typeof(CharacterPanelTab));
            for (int i = 0; i < _tabButtons.Length && i < tabs.Length; i++)
            {
                var img = _tabButtons[i].targetGraphic as Image;
                if (img != null)
                    img.color = tabs[i] == _activeTab ? new Color(0.22f, 0.24f, 0.30f, 0.96f) : UiBuilder.SlotBg;
            }
        }

        void DrawInventory()
        {
            int cap = Mathf.Max(0, _inventory?.Capacity ?? 0);
            if (cap == 0)
            {
                TextLine("Inventory unavailable", 0, 18, UiBuilder.MutedCol);
                return;
            }

            DrawPaperDoll();

            const int cols = 6;
            const float slot = 66f;
            const float gap = 7f;
            const float bagX = 400f;
            TextLine($"Bag ({cap} slots)", 0, 15, UiBuilder.MutedCol, bagX);
            for (int i = 0; i < cap; i++)
            {
                int row = i / cols;
                int col = i % cols;
                var s = _inventory.GetSlot(i);
                var cell = UiBuilder.NewPanel(_body, "InvSlot_" + i, "inv_slot", UiBuilder.SlotBg);
                var rt = cell.rectTransform;
                rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
                rt.pivot = new Vector2(0f, 1f);
                rt.anchoredPosition = new Vector2(bagX + col * (slot + gap), -26f - row * (slot + gap));
                rt.sizeDelta = new Vector2(slot, slot);

                var icon = UiBuilder.NewImage(cell.transform, "Icon", s.icon, Color.white);
                icon.preserveAspect = true;
                icon.enabled = s.icon != null;
                UiBuilder.Stretch(icon.rectTransform, 10f);

                if (s.count > 1)
                {
                    var count = UiBuilder.NewText(cell.transform, "Count", s.count.ToString(), 14, TextAnchor.LowerRight);
                    UiBuilder.Stretch(count.rectTransform, 5f);
                }

                if (!string.IsNullOrWhiteSpace(s.label))
                {
                    var label = UiBuilder.NewText(cell.transform, "Label", s.label, 11, TextAnchor.LowerCenter, UiBuilder.TextCol);
                    var lr = label.rectTransform;
                    lr.anchorMin = new Vector2(0f, 0f);
                    lr.anchorMax = new Vector2(1f, 0f);
                    lr.pivot = new Vector2(0.5f, 0f);
                    lr.anchoredPosition = new Vector2(0f, 4f);
                    lr.sizeDelta = new Vector2(-6f, 18f);
                }
            }
        }

        /// <summary>
        /// Minecraft-style (but roomier) equipment paper-doll: live player sprite
        /// flanked by armor/accessory slots. Slots are visual placeholders until
        /// the Foundation equipment system lands — tools stay on the hotbar by design.
        /// </summary>
        void DrawPaperDoll()
        {
            var frame = UiBuilder.NewPanel(_body, "Doll", "system_row", new Color(0.07f, 0.08f, 0.11f, 0.94f));
            var fr = frame.rectTransform;
            fr.anchorMin = new Vector2(0f, 1f);
            fr.anchorMax = new Vector2(0f, 1f);
            fr.pivot = new Vector2(0f, 1f);
            fr.anchoredPosition = Vector2.zero;
            fr.sizeDelta = new Vector2(380f, 520f);

            // live player sprite (south idle frame of the current sheet)
            Sprite playerSprite = null;
            var sheet = Resources.LoadAll<Sprite>("Characters/Player/BlackMage_Idle_512x1024");
            if (sheet != null)
                for (int i = 0; i < sheet.Length; i++)
                    if (sheet[i].name.EndsWith("_0")) { playerSprite = sheet[i]; break; }
            var pv = UiBuilder.NewImage(frame.transform, "Player", playerSprite, Color.white);
            pv.preserveAspect = true;
            pv.enabled = playerSprite != null;
            var pvr = pv.rectTransform;
            pvr.anchorMin = pvr.anchorMax = new Vector2(0.5f, 1f);
            pvr.pivot = new Vector2(0.5f, 1f);
            pvr.anchoredPosition = new Vector2(0f, -40f);
            pvr.sizeDelta = new Vector2(150f, 260f);

            string[] leftSlots = { "Head", "Chest", "Legs", "Feet" };
            for (int i = 0; i < leftSlots.Length; i++)
                DollSlot(frame.transform, leftSlots[i], 16f, 36f + i * 78f);
            string[] rightSlots = { "Back", "Acc 1", "Acc 2" };
            for (int i = 0; i < rightSlots.Length; i++)
                DollSlot(frame.transform, rightSlots[i], 290f, 36f + i * 78f);

            var hint = UiBuilder.NewText(frame.transform, "Hint",
                "Equipment slots — armor & accessories.\nTools and weapons live on the hotbar.\n(Equip system arriving with the class update.)",
                13, TextAnchor.UpperLeft, UiBuilder.MutedCol);
            hint.horizontalOverflow = HorizontalWrapMode.Wrap;
            var hr = hint.rectTransform;
            hr.anchorMin = new Vector2(0f, 0f);
            hr.anchorMax = new Vector2(1f, 0f);
            hr.pivot = new Vector2(0f, 0f);
            hr.anchoredPosition = new Vector2(16f, 14f);
            hr.sizeDelta = new Vector2(-32f, 110f);
        }

        void DollSlot(Transform parent, string label, float x, float y)
        {
            var s = UiBuilder.NewPanel(parent, "Equip_" + label, "inv_slot", UiBuilder.SlotBg);
            var rt = s.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(x, -y);
            rt.sizeDelta = new Vector2(72f, 60f);
            var t = UiBuilder.NewText(s.transform, "L", label, 11, TextAnchor.MiddleCenter, UiBuilder.MutedCol);
            UiBuilder.Stretch(t.rectTransform, 2f);
        }

        /// <summary>Ability loadout tab. Visual contract for the confirmed Q/E/R/F +
        /// hold-X wheel scheme; binds to the Foundation ability API when it lands.</summary>
        void DrawSpells()
        {
            TextLine("Abilities", 0, 22, UiBuilder.TextCol);
            TextLine("Tap Q / E / R / F to cast. Hold X for the ability wheel: drag outward to assign, release on an ability to cast it once.",
                34, 14, UiBuilder.MutedCol);

            string[] keys = { "Q", "E", "R", "F" };
            for (int i = 0; i < 4; i++)
            {
                var slot = UiBuilder.NewPanel(_body, "Ability_" + keys[i], "inv_slot", UiBuilder.SlotBg);
                var rt = slot.rectTransform;
                rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
                rt.pivot = new Vector2(0f, 1f);
                rt.anchoredPosition = new Vector2(i * 130f, -80f);
                rt.sizeDelta = new Vector2(116f, 96f);
                var k = UiBuilder.NewText(slot.transform, "K", keys[i], 26, TextAnchor.UpperCenter, UiBuilder.TextCol);
                UiBuilder.Stretch(k.rectTransform, 8f);
                var e = UiBuilder.NewText(slot.transform, "E", "(empty)", 12, TextAnchor.LowerCenter, UiBuilder.MutedCol);
                UiBuilder.Stretch(e.rectTransform, 8f);
            }

            TextLine("Known abilities", 200, 18, UiBuilder.TextCol);
            string[] known =
            {
                "Steady Strike — stamina 10 · cooldown 3s · melee",
                "Guard Step — stamina 8 · cooldown 5s · defense",
                "Mana Bolt — mana 12 · cooldown 2s · neutral",
                "Ember Spark — mana 15 · requires Ember affinity rank 1",
            };
            for (int i = 0; i < known.Length; i++)
                TextLine(known[i], 236 + i * 30, 15, i == 0 ? UiBuilder.TextCol : UiBuilder.MutedCol);
            TextLine("Assignment activates with the class/trial update (runtime API pending).",
                236 + known.Length * 30 + 12, 13, new Color(0.98f, 0.84f, 0.52f, 1f));
        }

        /// <summary>Settings tab: volumes (same PlayerPrefs keys as the pause menu),
        /// UI scale and text scale.</summary>
        void DrawSettings()
        {
            TextLine("Settings", 0, 22, UiBuilder.TextCol);
            DrawPrefSlider("Master volume", "vol_master", 1f, 60f, v => AudioListener.volume = Mathf.Clamp01(v));
            DrawPrefSlider("UI scale", "ui.scale", 1f, 124f, null, 0.7f, 1.6f);
            float textScale = LitIsoFont.TextScale;
            TextLine($"Text size: {Mathf.RoundToInt(textScale * 100f)}%", 188, 16, UiBuilder.TextCol);
            DrawStepButtons(216f, () => LitIsoFont.SetTextScale(LitIsoFont.TextScale - 0.1f),
                                   () => LitIsoFont.SetTextScale(LitIsoFont.TextScale + 0.1f));
            TextLine("More options (bindings, HUD layout, accessibility) arrive with the overhaul. F1 cycles HUD modes; Alt-drag moves HUD panels.",
                290, 13, UiBuilder.MutedCol);
        }

        void DrawPrefSlider(string label, string prefKey, float def, float y,
            System.Action<float> onApply, float min = 0f, float max = 1f)
        {
            float val = PlayerPrefs.GetFloat(prefKey, def);
            TextLine($"{label}: {Mathf.RoundToInt(Mathf.InverseLerp(min, max, val) * 100f)}%", y, 16, UiBuilder.TextCol);
            DrawStepButtons(y + 28f,
                () => { float v = Mathf.Clamp(PlayerPrefs.GetFloat(prefKey, def) - (max - min) * 0.1f, min, max);
                        PlayerPrefs.SetFloat(prefKey, v); PlayerPrefs.Save(); onApply?.Invoke(v); Refresh(); },
                () => { float v = Mathf.Clamp(PlayerPrefs.GetFloat(prefKey, def) + (max - min) * 0.1f, min, max);
                        PlayerPrefs.SetFloat(prefKey, v); PlayerPrefs.Save(); onApply?.Invoke(v); Refresh(); });
        }

        void DrawStepButtons(float y, System.Action minus, System.Action plus)
        {
            var btnDown = UiBuilder.NewButton(_body, "Minus" + y, "button", "-", 18);
            var dr = btnDown.GetComponent<RectTransform>();
            dr.anchorMin = dr.anchorMax = new Vector2(0f, 1f);
            dr.pivot = new Vector2(0f, 1f);
            dr.anchoredPosition = new Vector2(0f, -y);
            dr.sizeDelta = new Vector2(48f, 34f);
            btnDown.onClick.AddListener(() => minus());
            var btnUp = UiBuilder.NewButton(_body, "Plus" + y, "button", "+", 18);
            var ur = btnUp.GetComponent<RectTransform>();
            ur.anchorMin = ur.anchorMax = new Vector2(0f, 1f);
            ur.pivot = new Vector2(0f, 1f);
            ur.anchoredPosition = new Vector2(56f, -y);
            ur.sizeDelta = new Vector2(48f, 34f);
            btnUp.onClick.AddListener(() => plus());
        }

        void DrawCrafting()
        {
            int count = Mathf.Max(0, _crafting?.RecipeCount ?? 0);
            if (count == 0)
            {
                TextLine("No crafting recipes available", 0, 18, UiBuilder.MutedCol);
                return;
            }

            if (!RecipeExists(_selectedRecipeId))
                _selectedRecipeId = _crafting.GetRecipe(0).id;

            var listFrame = UiBuilder.NewPanel(_body, "RecipeListFrame", "system_row", UiBuilder.SlotBg);
            var listFrameRt = listFrame.rectTransform;
            listFrameRt.anchorMin = new Vector2(0f, 0f);
            listFrameRt.anchorMax = new Vector2(0f, 1f);
            listFrameRt.offsetMin = Vector2.zero;
            listFrameRt.offsetMax = new Vector2(380f, 0f);

            var listTitle = UiBuilder.NewText(listFrame.transform, "ListTitle", $"Recipes ({count}) - all stations", 17, TextAnchor.MiddleLeft, UiBuilder.MutedCol);
            var listTitleRt = listTitle.rectTransform;
            listTitleRt.anchorMin = new Vector2(0f, 1f);
            listTitleRt.anchorMax = new Vector2(1f, 1f);
            listTitleRt.pivot = new Vector2(0f, 1f);
            listTitleRt.anchoredPosition = new Vector2(16f, -10f);
            listTitleRt.sizeDelta = new Vector2(-32f, 28f);

            var listScrollRoot = CreateScrollView(listFrame.transform, "RecipeScroll", out var listContent);
            listScrollRoot.offsetMin = new Vector2(10f, 10f);
            listScrollRoot.offsetMax = new Vector2(-10f, -48f);

            for (int i = 0; i < count; i++)
            {
                var row = _crafting.GetRecipe(i);
                var recipeRow = UiBuilder.NewPanel(listContent, "Recipe_" + SafeName(row.id), "craft_row",
                    row.id == _selectedRecipeId ? new Color(0.20f, 0.22f, 0.28f, 0.96f) : UiBuilder.SlotBg);
                recipeRow.color = row.id == _selectedRecipeId
                    ? new Color(0.20f, 0.22f, 0.28f, 0.96f)
                    : row.canCraft ? UiBuilder.SlotBg : new Color(0.08f, 0.09f, 0.12f, 0.84f);
                var rowRt = recipeRow.rectTransform;
                rowRt.sizeDelta = new Vector2(0f, 68f);
                var rowLayout = recipeRow.gameObject.AddComponent<LayoutElement>();
                rowLayout.preferredHeight = 68f;
                rowLayout.minHeight = 68f;

                var btn = recipeRow.gameObject.AddComponent<Button>();
                btn.targetGraphic = recipeRow;
                var id = row.id;
                btn.onClick.AddListener(() => { _selectedRecipeId = id; Refresh(); });

                var icon = UiBuilder.NewImage(recipeRow.transform, "Icon", row.icon, row.canCraft ? UiBuilder.TextCol : UiBuilder.MutedCol);
                icon.preserveAspect = true;
                icon.enabled = row.icon != null;
                var iconRt = icon.rectTransform;
                iconRt.anchorMin = iconRt.anchorMax = new Vector2(0f, 0.5f);
                iconRt.pivot = new Vector2(0f, 0.5f);
                iconRt.anchoredPosition = new Vector2(10f, 0f);
                iconRt.sizeDelta = new Vector2(44f, 44f);

                string station = string.IsNullOrWhiteSpace(row.station) ? "Hand" : row.station;
                var label = UiBuilder.NewText(recipeRow.transform, "Label", $"{row.display}\n{station}", 15, TextAnchor.MiddleLeft,
                    row.canCraft ? UiBuilder.TextCol : UiBuilder.MutedCol);
                label.horizontalOverflow = HorizontalWrapMode.Wrap;
                label.verticalOverflow = VerticalWrapMode.Truncate;
                var labelRt = label.rectTransform;
                labelRt.anchorMin = new Vector2(0f, 0f);
                labelRt.anchorMax = new Vector2(1f, 1f);
                labelRt.offsetMin = new Vector2(64f, 16f);
                labelRt.offsetMax = new Vector2(-142f, -8f);

                string rowStatus = row.canCraft
                    ? "Ready"
                    : string.IsNullOrWhiteSpace(row.disabledReason) ? "Locked" : row.disabledReason;
                var status = UiBuilder.NewText(recipeRow.transform, "Status", rowStatus, 12,
                    TextAnchor.MiddleRight, row.canCraft ? new Color(0.55f, 0.95f, 0.62f, 1f) : new Color(0.95f, 0.52f, 0.45f, 1f));
                status.horizontalOverflow = HorizontalWrapMode.Wrap;
                status.verticalOverflow = VerticalWrapMode.Truncate;
                var statusRt = status.rectTransform;
                statusRt.anchorMin = new Vector2(1f, 0f);
                statusRt.anchorMax = new Vector2(1f, 1f);
                statusRt.pivot = new Vector2(1f, 0.5f);
                statusRt.anchoredPosition = new Vector2(-10f, 0f);
                statusRt.sizeDelta = new Vector2(126f, -10f);
            }

            var details = _crafting.GetDetails(_selectedRecipeId);
            var selectedRow = SelectedRecipeRow();
            var detailsFrame = UiBuilder.NewPanel(_body, "RecipeDetailsFrame", "system_panel", new Color(0.08f, 0.10f, 0.14f, 0.92f));
            var detailsRt = detailsFrame.rectTransform;
            detailsRt.anchorMin = Vector2.zero;
            detailsRt.anchorMax = Vector2.one;
            detailsRt.offsetMin = new Vector2(404f, 0f);
            detailsRt.offsetMax = Vector2.zero;

            var title = UiBuilder.NewText(detailsFrame.transform, "RecipeTitle", details.display ?? "Recipe", 22, TextAnchor.UpperLeft);
            title.horizontalOverflow = HorizontalWrapMode.Wrap;
            var titleRt = title.rectTransform;
            titleRt.anchorMin = new Vector2(0f, 1f);
            titleRt.anchorMax = new Vector2(1f, 1f);
            titleRt.pivot = new Vector2(0f, 1f);
            titleRt.anchoredPosition = new Vector2(18f, -14f);
            titleRt.sizeDelta = new Vector2(-36f, 56f);

            string detailStation = string.IsNullOrWhiteSpace(selectedRow.station) ? "Hand" : selectedRow.station;
            string reason = details.canCraft
                ? "Ready to craft"
                : string.IsNullOrWhiteSpace(details.disabledReason) ? "Cannot craft" : details.disabledReason;
            var statusText = UiBuilder.NewText(detailsFrame.transform, "RecipeStatus", $"Station: {detailStation}\n{reason}", 16, TextAnchor.UpperLeft,
                details.canCraft ? new Color(0.55f, 0.95f, 0.62f, 1f) : new Color(0.95f, 0.52f, 0.45f, 1f));
            statusText.horizontalOverflow = HorizontalWrapMode.Wrap;
            var statusTextRt = statusText.rectTransform;
            statusTextRt.anchorMin = new Vector2(0f, 1f);
            statusTextRt.anchorMax = new Vector2(1f, 1f);
            statusTextRt.pivot = new Vector2(0f, 1f);
            statusTextRt.anchoredPosition = new Vector2(18f, -72f);
            statusTextRt.sizeDelta = new Vector2(-36f, 44f);

            var detailsScrollRoot = CreateScrollView(detailsFrame.transform, "DetailsScroll", out var detailsContent);
            detailsScrollRoot.offsetMin = new Vector2(12f, 84f);
            detailsScrollRoot.offsetMax = new Vector2(-12f, -124f);

            AddIngredientSection(detailsContent, "Ingredients", details.inputs, true);
            AddIngredientSection(detailsContent, "Creates", details.outputs, false);

            var craft = UiBuilder.NewButton(detailsFrame.transform, "Craft", "craft_button", details.canCraft ? "Craft" : "Cannot Craft", 20);
            craft.interactable = details.canCraft;
            craft.onClick.AddListener(() => { _crafting?.Craft(_selectedRecipeId); Refresh(); });
            var cr = craft.GetComponent<RectTransform>();
            cr.anchorMin = cr.anchorMax = new Vector2(1f, 0f);
            cr.pivot = new Vector2(1f, 0f);
            cr.anchoredPosition = new Vector2(-18f, 18f);
            cr.sizeDelta = new Vector2(240f, 58f);
        }

        bool RecipeExists(string recipeId)
        {
            if (string.IsNullOrWhiteSpace(recipeId) || _crafting == null)
                return false;

            for (int i = 0; i < _crafting.RecipeCount; i++)
            {
                if (_crafting.GetRecipe(i).id == recipeId)
                    return true;
            }

            return false;
        }

        CraftingRecipeRow SelectedRecipeRow()
        {
            if (string.IsNullOrWhiteSpace(_selectedRecipeId) || _crafting == null)
                return default;

            for (int i = 0; i < _crafting.RecipeCount; i++)
            {
                var row = _crafting.GetRecipe(i);
                if (row.id == _selectedRecipeId)
                    return row;
            }

            return default;
        }

        RectTransform CreateScrollView(Transform parent, string name, out RectTransform content)
        {
            var root = UiBuilder.NewRect(name, parent);
            UiBuilder.Stretch(root);
            var scroll = root.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.scrollSensitivity = 34f;

            var viewport = UiBuilder.NewRect("Viewport", root);
            UiBuilder.Stretch(viewport);
            viewport.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.001f);
            // RectMask2D instead of stencil Mask: a Mask over a (near-)transparent Image
            // can cull every masked child depending on canvas/material setup, which made
            // scroll-list rows render invisible (recipes/callings). RectMask2D clips by
            // rect with no stencil/graphic dependency.
            viewport.gameObject.AddComponent<RectMask2D>();

            content = UiBuilder.NewRect("Content", viewport);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.sizeDelta = Vector2.zero;

            var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 8f;
            layout.padding = new RectOffset(4, 4, 4, 4);
            layout.childControlWidth = true;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            var fitter = content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scroll.viewport = viewport;
            scroll.content = content;
            return root;
        }

        void AddIngredientSection(RectTransform parent, string title, CraftingIngredient[] rows, bool compareHave)
        {
            var header = UiBuilder.NewText(parent, title + "Header", title, 18, TextAnchor.MiddleLeft, UiBuilder.MutedCol);
            var headerLayout = header.gameObject.AddComponent<LayoutElement>();
            headerLayout.preferredHeight = 30f;
            headerLayout.minHeight = 30f;

            if (rows == null || rows.Length == 0)
            {
                var empty = UiBuilder.NewText(parent, title + "Empty", "None", 15, TextAnchor.MiddleLeft, UiBuilder.MutedCol);
                var emptyLayout = empty.gameObject.AddComponent<LayoutElement>();
                emptyLayout.preferredHeight = 28f;
                emptyLayout.minHeight = 28f;
                return;
            }

            for (int i = 0; i < rows.Length; i++)
            {
                var input = rows[i];
                bool haveEnough = !compareHave || input.have >= input.needed;
                var row = UiBuilder.NewPanel(parent, title + "_" + SafeName(input.itemId) + "_" + i, "system_row",
                    haveEnough ? UiBuilder.SlotBg : new Color(0.18f, 0.08f, 0.08f, 0.82f));
                var layout = row.gameObject.AddComponent<LayoutElement>();
                layout.preferredHeight = 54f;
                layout.minHeight = 54f;

                var icon = UiBuilder.NewImage(row.transform, "Icon", input.icon, UiBuilder.MutedCol);
                icon.preserveAspect = true;
                icon.enabled = input.icon != null;
                var iconRt = icon.rectTransform;
                iconRt.anchorMin = iconRt.anchorMax = new Vector2(0f, 0.5f);
                iconRt.pivot = new Vector2(0f, 0.5f);
                iconRt.anchoredPosition = new Vector2(10f, 0f);
                iconRt.sizeDelta = new Vector2(34f, 34f);

                var label = UiBuilder.NewText(row.transform, "Label", input.display, 15, TextAnchor.MiddleLeft,
                    haveEnough ? UiBuilder.TextCol : new Color(0.95f, 0.52f, 0.45f, 1f));
                label.horizontalOverflow = HorizontalWrapMode.Wrap;
                label.verticalOverflow = VerticalWrapMode.Truncate;
                var labelRt = label.rectTransform;
                labelRt.anchorMin = new Vector2(0f, 0f);
                labelRt.anchorMax = new Vector2(1f, 1f);
                labelRt.offsetMin = new Vector2(54f, 8f);
                labelRt.offsetMax = new Vector2(-120f, -8f);

                string count = compareHave ? $"{input.have}/{input.needed}" : $"x{input.needed}";
                var amount = UiBuilder.NewText(row.transform, "Amount", count, 16, TextAnchor.MiddleRight,
                    haveEnough ? UiBuilder.TextCol : new Color(0.95f, 0.52f, 0.45f, 1f));
                var amountRt = amount.rectTransform;
                amountRt.anchorMin = new Vector2(1f, 0f);
                amountRt.anchorMax = new Vector2(1f, 1f);
                amountRt.pivot = new Vector2(1f, 0.5f);
                amountRt.anchoredPosition = new Vector2(-12f, 0f);
                amountRt.sizeDelta = new Vector2(104f, 0f);
            }
        }

        static string SafeName(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return "none";

            var chars = value.ToCharArray();
            for (int i = 0; i < chars.Length; i++)
            {
                if (!char.IsLetterOrDigit(chars[i]) && chars[i] != '_' && chars[i] != '-')
                    chars[i] = '_';
            }

            return new string(chars);
        }

        void DrawStatus()
        {
            if (_character == null)
            {
                TextLine("Status unavailable", 0, 18, UiBuilder.MutedCol);
                return;
            }

            TextLine(_character.CharacterName, 0, 24, UiBuilder.TextCol);
            TextLine($"Class: {_character.ClassName}", 36, 18, UiBuilder.MutedCol);
            TextLine($"Title: {_character.TitleName}", 64, 18, UiBuilder.MutedCol);
            TextLine($"Level {_character.Level}", 92, 18, UiBuilder.TextCol);
            DrawBar("HP", _character.Health01, 136, new Color(0.80f, 0.25f, 0.25f, 1f));
            DrawBar("MP", _character.Mana01, 174, new Color(0.30f, 0.55f, 0.90f, 1f));
            DrawBar("XP", _character.Xp01, 212, new Color(0.85f, 0.70f, 0.30f, 1f));

            var s = _character.Stats;
            string[] labels = { "STR", "DEX", "INT", "VIT", "DEF", "LUCK" };
            int[] vals = { s.str, s.dex, s.intel, s.vit, s.def, s.luck };
            for (int i = 0; i < labels.Length; i++)
            {
                float x = 520f + (i % 2) * 170f;
                float y = 20f + (i / 2) * 62f;
                TextLine(labels[i], y, 16, UiBuilder.MutedCol, x);
                TextLine(vals[i].ToString(), y + 22f, 24, UiBuilder.TextCol, x);
            }
        }

        void DrawSkills()
        {
            var state = _progression?.CaptureReadState();
            if (state?.skills == null || state.skills.Length == 0)
            {
                TextLine("No skills tracked yet", 0, 18, UiBuilder.MutedCol);
                return;
            }

            var scrollRoot = CreateScrollView(_body, "SkillsScroll", out var content);
            scrollRoot.offsetMin = Vector2.zero;
            scrollRoot.offsetMax = Vector2.zero;

            AddSkillsIntro(content, state);

            foreach (var bucket in SkillBuckets)
            {
                var matches = CollectSkills(state.skills, bucket);
                if (matches.Count == 0) continue;

                AddSkillBucketHeader(content, bucket, matches);
                for (int i = 0; i < matches.Count; i++)
                    AddSkillCard(content, matches[i], bucket);
            }
        }

        void AddSkillsIntro(RectTransform parent, FoundationProgressionReadState state)
        {
            var frame = UiBuilder.NewPanel(parent, "SkillsIntro", "system_panel", new Color(0.075f, 0.085f, 0.12f, 0.94f));
            AddLayoutHeight(frame.rectTransform, 112f);

            string calling = state.calling.hasCalling ? state.calling.displayName : "Unwritten";
            string title = state.calling.hasCalling ? state.calling.title : "No title yet";
            AddText(frame.transform, "Title", "LitRPG Skill Paths", 24, UiBuilder.TextCol, 18f, 12f, 420f, 30f);
            AddText(frame.transform, "Sub", "Combat, gathering, crafting, survival, settlement, lore, and future dungeon mastery all live here.", 15,
                UiBuilder.MutedCol, 18f, 48f, 660f, 44f);
            AddText(frame.transform, "Calling", $"Calling: {calling}\nTitle: {title}", 16, UiBuilder.TextCol, 720f, 18f, 260f, 58f, TextAnchor.UpperRight);
            AddText(frame.transform, "Hint", "Skills gain XP from real actions; unlock previews show what each path will feed later.", 13,
                new Color(0.98f, 0.84f, 0.52f, 1f), 18f, 86f, 790f, 24f);
        }

        void AddSkillBucketHeader(RectTransform parent, SkillUiBucket bucket, List<FoundationSkillReadState> skills)
        {
            float average = 0f;
            int highLevel = 0;
            for (int i = 0; i < skills.Count; i++)
            {
                average += Mathf.Clamp01(skills[i].progress01);
                highLevel = Mathf.Max(highLevel, skills[i].level);
            }
            average = skills.Count > 0 ? average / skills.Count : 0f;

            var header = UiBuilder.NewPanel(parent, "SkillBucket_" + SafeName(bucket.title), "system_row", new Color(0.08f, 0.095f, 0.13f, 0.94f));
            AddLayoutHeight(header.rectTransform, 82f);

            var strip = UiBuilder.NewImage(header.transform, "Strip", null, bucket.color);
            Place(strip.rectTransform, 0f, 0f, 8f, 82f);
            AddText(header.transform, "Title", bucket.title, 20, UiBuilder.TextCol, 18f, 10f, 360f, 28f);
            AddText(header.transform, "Sub", bucket.subtitle, 14, UiBuilder.MutedCol, 18f, 42f, 600f, 32f);
            AddText(header.transform, "Count", $"{skills.Count} skills\nHighest Lv {highLevel}", 14, UiBuilder.TextCol, 760f, 12f, 190f, 46f, TextAnchor.UpperRight);
            AddLocalBar(header.transform, 635f, 58f, 310f, 10f, average, bucket.color);
        }

        void AddSkillCard(RectTransform parent, FoundationSkillReadState skill, SkillUiBucket bucket)
        {
            var card = UiBuilder.NewPanel(parent, "Skill_" + SafeName(skill.id), "system_panel", new Color(0.055f, 0.065f, 0.09f, 0.94f));
            AddLayoutHeight(card.rectTransform, 138f);

            var sigil = UiBuilder.NewPanel(card.transform, "Sigil", "slot_selected", new Color(bucket.color.r * 0.45f, bucket.color.g * 0.45f, bucket.color.b * 0.45f, 0.96f));
            Place(sigil.rectTransform, 16f, 18f, 58f, 58f);
            AddText(sigil.transform, "Letter", SkillInitial(skill), 26, UiBuilder.TextCol, 0f, 10f, 58f, 38f, TextAnchor.MiddleCenter);

            string tracked = skill.isTracked ? "Tracked" : "Dormant";
            AddText(card.transform, "Name", $"{skill.displayName}  Lv {skill.level}", 21, UiBuilder.TextCol, 92f, 12f, 420f, 30f);
            AddText(card.transform, "Meta", $"{ReadableActivity(skill.activity)} / {ReadableNodeKind(skill.primaryNodeKind)} / {tracked}", 13,
                new Color(0.98f, 0.84f, 0.52f, 1f), 92f, 42f, 480f, 22f);
            AddText(card.transform, "Desc", skill.description, 14, UiBuilder.MutedCol, 92f, 66f, 590f, 38f);
            AddLocalBar(card.transform, 92f, 112f, 440f, 12f, skill.progress01, bucket.color);
            AddText(card.transform, "Xp", $"{skill.xpIntoLevel}/{skill.xpToNextLevel} XP to next level", 13, UiBuilder.MutedCol,
                548f, 104f, 180f, 28f, TextAnchor.MiddleLeft);

            AddText(card.transform, "UnlockTitle", "Unlock path", 13, UiBuilder.TextCol, 742f, 14f, 160f, 20f, TextAnchor.UpperRight);
            AddText(card.transform, "Unlocks", FormatUnlocks(skill.unlocks), 13, UiBuilder.MutedCol, 640f, 40f, 310f, 76f, TextAnchor.UpperRight);
        }

        static List<FoundationSkillReadState> CollectSkills(FoundationSkillReadState[] skills, SkillUiBucket bucket)
        {
            var result = new List<FoundationSkillReadState>();
            if (skills == null) return result;

            for (int i = 0; i < skills.Length; i++)
            {
                if (MatchesBucket(skills[i], bucket))
                    result.Add(skills[i]);
            }

            return result;
        }

        static bool MatchesBucket(FoundationSkillReadState skill, SkillUiBucket bucket)
        {
            if (bucket.activities == null) return false;
            for (int i = 0; i < bucket.activities.Length; i++)
                if (skill.activity == bucket.activities[i])
                    return true;
            return false;
        }

        static string SkillInitial(FoundationSkillReadState skill)
        {
            string source = string.IsNullOrWhiteSpace(skill.displayName) ? skill.id : skill.displayName;
            return string.IsNullOrEmpty(source) ? "?" : source.Substring(0, 1).ToUpperInvariant();
        }

        static string FormatUnlocks(string[] unlocks)
        {
            if (unlocks == null || unlocks.Length == 0)
                return "Future perks, passives, titles, and class evidence.";

            string value = "";
            for (int i = 0; i < unlocks.Length && i < 3; i++)
            {
                if (string.IsNullOrWhiteSpace(unlocks[i])) continue;
                if (value.Length > 0) value += "\n";
                value += "* " + unlocks[i];
            }
            return string.IsNullOrWhiteSpace(value) ? "Future perks, passives, titles, and class evidence." : value;
        }

        static string ReadableActivity(FoundationProgressionActivity activity)
        {
            switch (activity)
            {
                case FoundationProgressionActivity.Harvest: return "Gathering";
                case FoundationProgressionActivity.Craft: return "Crafting";
                case FoundationProgressionActivity.Build: return "Building";
                case FoundationProgressionActivity.Farm: return "Farming";
                case FoundationProgressionActivity.Explore: return "Exploration";
                case FoundationProgressionActivity.Creature: return "Creaturecraft";
                case FoundationProgressionActivity.Combat: return "Combat";
                case FoundationProgressionActivity.Magic: return "Magic";
                case FoundationProgressionActivity.Trade: return "Trade";
                case FoundationProgressionActivity.Lore: return "Lore";
                default: return activity.ToString();
            }
        }

        static string ReadableNodeKind(FoundationSkillNodeKind kind)
        {
            switch (kind)
            {
                case FoundationSkillNodeKind.Ease: return "Ease";
                case FoundationSkillNodeKind.Yield: return "Yield";
                case FoundationSkillNodeKind.Insight: return "Insight";
                case FoundationSkillNodeKind.Expression: return "Expression";
                case FoundationSkillNodeKind.Utility: return "Utility";
                case FoundationSkillNodeKind.Harmony: return "Harmony";
                default: return kind.ToString();
            }
        }

        void DrawQuests()
        {
            var state = _progression?.CaptureReadState();
            if (state?.quests == null || state.quests.Length == 0)
            {
                TextLine("No active quests", 0, 18, UiBuilder.MutedCol);
                return;
            }

            float y = 0f;
            foreach (var quest in state.quests)
            {
                TextLine($"{quest.displayName}  {(quest.completed ? "Complete" : "Active")}", y, 18, UiBuilder.TextCol);
                DrawBar("", quest.progress01, y + 28f, new Color(0.95f, 0.78f, 0.34f, 1f), 360f, 14f);
                y += 54f;
                if (quest.objectives != null)
                {
                    foreach (var objective in quest.objectives)
                    {
                        string mark = objective.completed ? "[x]" : "[ ]";
                        TextLine($"{mark} {objective.text}  {objective.current}/{objective.required}", y, 14, UiBuilder.MutedCol, 20f);
                        y += 24f;
                        if (y > 470f) return;
                    }
                }
                y += 14f;
                if (y > 470f) break;
            }
        }

        void DrawSystem()
        {
            // save / quit block (mirrors PauseMenu behaviour + same save path)
            DrawSystemButton("Save Game", 0f, () =>
            {
                var bootstrap = UnityEngine.Object.FindFirstObjectByType<IsoCore.Foundation.FoundationBootstrap>();
                if (bootstrap != null && bootstrap.Save(bootstrap.DefaultSavePath))
                    Debug.Log("[SystemTab] Game saved.");
                else
                    Debug.LogWarning("[SystemTab] Save failed.");
                Refresh();
            });
            DrawSystemButton("Save & Main Menu", 230f, () =>
            {
                var bootstrap = UnityEngine.Object.FindFirstObjectByType<IsoCore.Foundation.FoundationBootstrap>();
                if (bootstrap != null) bootstrap.Save(bootstrap.DefaultSavePath);
                UnityEngine.SceneManagement.SceneManager.LoadScene("MenuScene");
            });
            DrawSystemButton("Quit to Desktop", 460f, () =>
            {
                var bootstrap = UnityEngine.Object.FindFirstObjectByType<IsoCore.Foundation.FoundationBootstrap>();
                if (bootstrap != null) bootstrap.Save(bootstrap.DefaultSavePath);
                Application.Quit();
            });

            var read = _qol?.CaptureReadState();
            TextLine("System Log", 76, 22, UiBuilder.TextCol);
            TextLine("Filtered, persistent Ledger messages from trial evidence, quests, titles, affinities, and warnings.", 110, 15, UiBuilder.MutedCol);
            float y = 152f;
            if (read?.visibleMessages != null && read.visibleMessages.Length > 0)
            {
                for (int i = read.visibleMessages.Length - 1; i >= 0 && y < 480f; i--)
                {
                    var msg = read.visibleMessages[i];
                    TextLine($"[{msg.channel}] {msg.text}", y, 15, UiBuilder.TextCol);
                    y += 28f;
                }
            }
            else
            {
                TextLine("No System messages yet", y, 16, UiBuilder.MutedCol);
            }

            if (read?.pinnedGoals != null && read.pinnedGoals.Length > 0)
            {
                TextLine("Pinned Goals", 0, 18, UiBuilder.TextCol, 640f);
                float gy = 34f;
                foreach (var goal in read.pinnedGoals)
                {
                    TextLine(goal.available ? goal.displayName : goal.detail, gy, 15, goal.available ? UiBuilder.TextCol : UiBuilder.MutedCol, 640f);
                    DrawBar("", goal.progress01, gy + 24f, new Color(0.95f, 0.78f, 0.34f, 1f), 260f, 12f, 640f);
                    gy += 60f;
                    if (gy > 420f) break;
                }
            }
        }

        void DrawSystemButton(string label, float x, System.Action onClick)
        {
            var btn = UiBuilder.NewButton(_body, "Sys_" + label, "button", label, 16);
            var rt = btn.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(x, 0f);
            rt.sizeDelta = new Vector2(214f, 52f);
            btn.onClick.AddListener(() => onClick());
        }

        void DrawMap()
        {
            TextLine("Map", 0, 24, UiBuilder.TextCol);
            TextLine("Press M for the explored map. Drag the fullscreen map to pan and use mouse wheel to zoom.", 40, 17, UiBuilder.MutedCol);
            TextLine("Hold Alt and drag/resize HUD panels to author your layout. Alt+Shift+R resets HUD/map layout.", 78, 17, UiBuilder.MutedCol);
            TextLine("Map markers now show player, spawn/home, portals, resources, and buildings discovered in explored cells.", 116, 17, UiBuilder.MutedCol);
        }

        static void AddLayoutHeight(RectTransform rt, float height)
        {
            var layout = rt.gameObject.GetComponent<LayoutElement>();
            if (layout == null) layout = rt.gameObject.AddComponent<LayoutElement>();
            layout.minHeight = height;
            layout.preferredHeight = height;
        }

        static Text AddText(Transform parent, string name, string value, int size, Color color,
            float x, float y, float width, float height, TextAnchor anchor = TextAnchor.UpperLeft)
        {
            var text = UiBuilder.NewText(parent, name, value ?? "", size, anchor, color);
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            Place(text.rectTransform, x, y, width, height);
            return text;
        }

        static void AddLocalBar(Transform parent, float x, float y, float width, float height, float value, Color fillColor)
        {
            var track = UiBuilder.NewPanel(parent, "ProgressTrack", "bar_track", UiBuilder.SlotBg);
            Place(track.rectTransform, x, y, width, height);

            var fill = UiBuilder.NewImage(track.transform, "ProgressFill", null, fillColor);
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillAmount = Mathf.Clamp01(value);
            UiBuilder.Stretch(fill.rectTransform, 2f);
        }

        static void Place(RectTransform rt, float x, float y, float width, float height)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(x, -y);
            rt.sizeDelta = new Vector2(width, height);
        }

        Text TextLine(string value, float y, int size, Color color, float x = 0f)
        {
            var t = UiBuilder.NewText(_body, "Line", value ?? "", size, TextAnchor.UpperLeft, color);
            var rt = t.rectTransform;
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(x, -y);
            rt.sizeDelta = new Vector2(-x, size + 10f);
            return t;
        }

        void DrawBar(string label, float value, float y, Color color, float width = 380f, float height = 20f, float x = 0f)
        {
            if (!string.IsNullOrEmpty(label))
                TextLine(label, y - 2f, 14, UiBuilder.MutedCol, x);
            var track = UiBuilder.NewPanel(_body, "Bar", "bar_track", UiBuilder.SlotBg);
            var tr = track.rectTransform;
            tr.anchorMin = tr.anchorMax = new Vector2(0f, 1f);
            tr.pivot = new Vector2(0f, 1f);
            tr.anchoredPosition = new Vector2(x + (string.IsNullOrEmpty(label) ? 0f : 46f), -y);
            tr.sizeDelta = new Vector2(width, height);

            var fill = UiBuilder.NewImage(track.transform, "Fill", null, color);
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillAmount = Mathf.Clamp01(value);
            UiBuilder.Stretch(fill.rectTransform, 2f);
        }

        static string LabelFor(CharacterPanelTab tab)
        {
            switch (tab)
            {
                case CharacterPanelTab.Inventory: return "Inventory";
                case CharacterPanelTab.Crafting: return "Crafting";
                case CharacterPanelTab.Skills: return "Skills";
                case CharacterPanelTab.Spells: return "Spells";
                case CharacterPanelTab.Character: return "Character";
                case CharacterPanelTab.Journal: return "Journal";
                case CharacterPanelTab.Settings: return "Settings";
                case CharacterPanelTab.System: return "System";
                case CharacterPanelTab.Map: return "Map";
                default: return tab.ToString();
            }
        }

        void HandleTextScaleChanged(float _)
        {
            bool wasOpen = IsOpen;
            var activeTab = _activeTab;
            Unsubscribe();
            Build();
            Subscribe();
            if (wasOpen)
                Show(activeTab);
            else if (_root != null)
                _root.SetActive(false);
        }
    }
}
