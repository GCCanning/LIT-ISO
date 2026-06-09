using UnityEngine;

namespace IsoCore.Foundation
{
    /// <summary>
    /// Foundation-owned fallback UI: hotbar plus a tabbed character panel for
    /// inventory, crafting, skills, and quests.
    /// </summary>
    public class FoundationHUD : MonoBehaviour
    {
        Inventory _inv;
        Hotbar _hotbar;
        FoundationContent _content;
        CraftingSystem _crafting;
        IsoFoundationPlayer _player;
        IsoWorld _world;
        DayNightSystem _dayNight;
        FoundationProgression _progression;

        bool _panelOpen;
        StationType _station = StationType.Hand;
        int _tabIndex;
        string _flash = "";
        float _flashTimer;
        Vector2 _craftScroll;

        public string Banner = "ISO-Core Foundation";

        Rect _panelRect = new(24, 112, 680, 500);
        Rect _hotbarRect;
        GUISkin _readableSkin;

        const string UiScalePrefKey = "ui.scale";
        public static float UiScale => Mathf.Clamp(PlayerPrefs.GetFloat(UiScalePrefKey, 1f), 0.75f, 1.75f);

        public void Init(Inventory inv, Hotbar hotbar, FoundationContent content,
            CraftingSystem crafting, IsoFoundationPlayer player, IsoWorld world, DayNightSystem dayNight)
        {
            _inv = inv;
            _hotbar = hotbar;
            _content = content;
            _crafting = crafting;
            _player = player;
            _world = world;
            _dayNight = dayNight;
        }

        public void BindProgression(FoundationProgression progression) => _progression = progression;

        public void ToggleInventory()
        {
            _tabIndex = 0;
            _panelOpen = !_panelOpen;
        }

        public void ToggleCrafting(StationType station)
        {
            _station = station;
            if (_panelOpen && _tabIndex == 1)
            {
                _panelOpen = false;
                return;
            }

            _tabIndex = 1;
            _panelOpen = true;
        }

        public void Flash(string msg)
        {
            _flash = msg;
            _flashTimer = 2.5f;
        }

        public bool PointerOverUI
        {
            get
            {
                Vector2 g = new(Input.mousePosition.x, Screen.height - Input.mousePosition.y);
                float scale = UiScale;
                if (Mathf.Abs(scale - 1f) > 0.01f)
                    g /= scale;
                if (_panelOpen && _panelRect.Contains(g)) return true;
                if (_hotbarRect.Contains(g)) return true;
                return false;
            }
        }

        void Update()
        {
            if (_flashTimer > 0f)
            {
                _flashTimer -= Time.deltaTime;
                if (_flashTimer <= 0f) _flash = "";
            }
        }

        void OnGUI()
        {
            if (_inv == null) return;

            if (_dayNight != null && _dayNight.NightTint.a > 0.01f)
            {
                var prev = GUI.color;
                GUI.color = _dayNight.NightTint;
                GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
                GUI.color = prev;
            }

            var oldMatrix = GUI.matrix;
            float scale = UiScale;
            if (Mathf.Abs(scale - 1f) > 0.01f)
                GUIUtility.ScaleAroundPivot(new Vector2(scale, scale), Vector2.zero);

            float sw = Screen.width / scale;
            float sh = Screen.height / scale;
            var oldSkin = GUI.skin;
            GUI.skin = ReadableSkin();

            DrawHeader();
            DrawHotbar(sw, sh);
            if (_panelOpen)
                _panelRect = GUI.Window(7001, _panelRect, DrawMainPanelWindow, "Character");

            if (!string.IsNullOrEmpty(_flash))
                GUI.Label(new Rect(sw / 2 - 220, sh - 160, 440, 32), _flash);

            GUI.skin = oldSkin;
            GUI.matrix = oldMatrix;
        }

        void DrawHeader()
        {
            GUI.Box(new Rect(10, 8, 650, 44), GUIContent.none);
            GUI.Label(new Rect(20, 10, 630, 20),
                $"{Banner} | WASD move | LMB use/place/break | RMB options");

            if (_player == null || _world == null) return;
            var c = _player.CurrentCell;
            var biome = _world.GetBiome(c.x, c.y);
            string time = _dayNight != null ? $" | {_dayNight.Clock} {_dayNight.PhaseLabel}" : "";
            GUI.Label(new Rect(20, 30, 630, 20),
                $"I inventory | C craft | M map | ({c.x},{c.y}) {(biome != null ? biome.Display : "?")}{time}");
        }

        GUISkin ReadableSkin()
        {
            if (_readableSkin != null) return _readableSkin;
            _readableSkin = Instantiate(GUI.skin);
            _readableSkin.label.fontSize = 16;
            _readableSkin.label.normal.textColor = new Color(1f, 0.96f, 0.78f);
            _readableSkin.button.fontSize = 15;
            _readableSkin.window.fontSize = 16;
            _readableSkin.box.fontSize = 15;
            return _readableSkin;
        }

        void DrawHotbar(float sw, float sh)
        {
            int n = _hotbar.Size;
            float slot = 62f, pad = 8f;
            float total = n * (slot + pad);
            float x0 = (sw - total) * 0.5f;
            float y = sh - slot - 16f;
            _hotbarRect = new Rect(x0, y, total, slot + 4);

            for (int i = 0; i < n; i++)
            {
                var r = new Rect(x0 + i * (slot + pad), y, slot, slot);
                GUI.Box(r, GUIContent.none);
                if (i == _hotbar.Selected)
                {
                    var old = GUI.color;
                    GUI.color = Color.yellow;
                    GUI.Box(r, GUIContent.none);
                    GUI.color = old;
                }

                var st = _inv.GetSlot(i);
                if (!st.IsEmpty) DrawItem(new Rect(r.x + 4, r.y + 4, slot - 8, slot - 8), st);
                GUI.Label(new Rect(r.x + 3, r.y - 2, slot, 18), (i + 1).ToString());
            }
        }

        void DrawMainPanelWindow(int id)
        {
            string[] tabs = { "Inventory", "Crafting", "Skills", "Quests" };
            _tabIndex = GUI.Toolbar(new Rect(12, 28, _panelRect.width - 24, 36), _tabIndex, tabs);
            var body = new Rect(16, 74, _panelRect.width - 32, _panelRect.height - 94);
            GUI.Box(body, GUIContent.none);

            switch (_tabIndex)
            {
                case 0: DrawInventoryTab(body); break;
                case 1: DrawCraftingTab(body); break;
                case 2: DrawSkillsTab(body); break;
                case 3: DrawQuestsTab(body); break;
            }

            if (GUI.Button(new Rect(_panelRect.width - 76, 2, 66, 24), "Close"))
                _panelOpen = false;
            GUI.DragWindow(new Rect(0, 0, 10000, 24));
        }

        void DrawInventoryTab(Rect body)
        {
            GUI.Label(new Rect(body.x + 12, body.y + 10, body.width - 24, 24), "Inventory");
            float y = body.y + 42f;
            for (int i = 0; i < _inv.SlotCount; i++)
            {
                var st = _inv.GetSlot(i);
                if (st.IsEmpty) continue;
                var def = _content.Items.Get(st.itemId);
                var old = GUI.color;
                GUI.color = def != null ? def.color : Color.gray;
                GUI.DrawTexture(new Rect(body.x + 14, y + 4, 18, 18), Texture2D.whiteTexture);
                GUI.color = old;
                string dur = def != null && def.HasDurability ? $"  {st.durability}/{def.maxDurability}" : "";
                GUI.Label(new Rect(body.x + 44, y, body.width - 58, 26),
                    $"{(def != null ? def.Display : st.itemId)}  x{st.count}{dur}");
                y += 30f;
                if (y > body.yMax - 30) break;
            }
        }

        void DrawCraftingTab(Rect body)
        {
            var recipes = _content.Recipes.All;
            int visibleCount = recipes.Count;
            GUI.Label(new Rect(body.x + 12, body.y + 10, body.width - 24, 24),
                $"Crafting station: {_station} | Recipes ({visibleCount}) - all stations");

            var view = new Rect(body.x + 10f, body.y + 42f, body.width - 20f, body.height - 52f);
            float contentHeight = Mathf.Max(view.height, visibleCount * 62f + 8f);
            var contentRect = new Rect(0f, 0f, view.width - 18f, contentHeight);
            _craftScroll = GUI.BeginScrollView(view, _craftScroll, contentRect);

            float y = 6f;
            foreach (var r in _content.Recipes.All)
            {
                bool can = _crafting.CanCraft(r);
                var row = new Rect(4f, y, contentRect.width - 8f, 54f);
                GUI.Box(row, GUIContent.none);
                GUI.enabled = can;
                if (GUI.Button(new Rect(row.x + 8f, row.y + 7f, row.width - 170f, 38f), DescribeRecipe(r)))
                {
                    if (_crafting.TryCraft(r)) Flash($"Crafted {OutputName(r)}");
                }
                GUI.enabled = true;

                string status = can ? "Ready" : RecipeBlockedReason(r);
                GUI.Label(new Rect(row.x + row.width - 152f, row.y + 7f, 144f, 20f),
                    $"{(r.station == StationType.None ? StationType.Hand : r.station)}");
                GUI.Label(new Rect(row.x + row.width - 152f, row.y + 27f, 144f, 18f), status);
                y += 62f;
            }

            GUI.EndScrollView();
        }

        void DrawSkillsTab(Rect body)
        {
            var state = _progression?.CaptureReadState();
            if (state == null)
            {
                GUI.Label(new Rect(body.x + 12, body.y + 10, body.width - 24, 24), "Skills unavailable");
                return;
            }

            GUI.Label(new Rect(body.x + 12, body.y + 10, body.width - 24, 24),
                $"{state.calling.displayName}  Lv {state.calling.level}  {state.calling.title}");
            float y = body.y + 42f;
            if (state.skills == null) return;
            foreach (var skill in state.skills)
            {
                GUI.Label(new Rect(body.x + 12, y, 230, 24), $"{skill.displayName}  Lv {skill.level}");
                DrawMiniBar(new Rect(body.x + 250, y + 6, body.width - 370, 12), skill.progress01);
                GUI.Label(new Rect(body.x + body.width - 108, y, 96, 24), $"{skill.xpIntoLevel}/{skill.xpToNextLevel}");
                y += 30f;
                if (y > body.yMax - 30) break;
            }
        }

        void DrawQuestsTab(Rect body)
        {
            var state = _progression?.CaptureReadState();
            GUI.Label(new Rect(body.x + 12, body.y + 10, body.width - 24, 24), "Quest Journal");
            float y = body.y + 42f;
            if (state?.quests == null) return;

            foreach (var quest in state.quests)
            {
                GUI.Label(new Rect(body.x + 12, y, body.width - 24, 24),
                    $"{quest.displayName}  {(quest.completed ? "Complete" : "Active")}");
                y += 24f;
                if (quest.objectives != null)
                {
                    foreach (var obj in quest.objectives)
                    {
                        GUI.Label(new Rect(body.x + 28, y, body.width - 190, 22),
                            $"{(obj.completed ? "[x]" : "[ ]")} {obj.text}");
                        DrawMiniBar(new Rect(body.x + body.width - 150, y + 6, 130, 10), obj.progress01);
                        y += 22f;
                        if (y > body.yMax - 26) return;
                    }
                }
                y += 8f;
                if (y > body.yMax - 28) break;
            }
        }

        void DrawItem(Rect r, ItemStack st)
        {
            var def = _content.Items.Get(st.itemId);
            var col = def != null ? def.color : Color.gray;
            var old = GUI.color;
            GUI.color = col;
            GUI.DrawTexture(new Rect(r.x, r.y, r.width, r.height - 17), Texture2D.whiteTexture);
            GUI.color = old;
            if (def != null && def.HasDurability)
                DrawMiniBar(new Rect(r.x, r.y + r.height - 17, r.width, 4), st.durability / (float)Mathf.Max(1, def.maxDurability));
            GUI.Label(new Rect(r.x, r.y + r.height - 16, r.width, 16),
                $"{(def != null ? def.Display : st.itemId)} x{st.count}");
        }

        void DrawMiniBar(Rect r, float pct)
        {
            var old = GUI.color;
            GUI.color = new Color(0.08f, 0.07f, 0.05f, 0.9f);
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = Color.Lerp(new Color(0.95f, 0.28f, 0.18f), new Color(0.38f, 0.92f, 0.38f), Mathf.Clamp01(pct));
            GUI.DrawTexture(new Rect(r.x + 1, r.y + 1, Mathf.Max(0f, r.width - 2) * Mathf.Clamp01(pct), Mathf.Max(1f, r.height - 2)), Texture2D.whiteTexture);
            GUI.color = old;
        }

        string OutputName(RecipeDefinition r)
        {
            if (r.outputs == null || r.outputs.Length == 0) return r.Display;
            var def = _content.Items.Get(r.outputs[0].itemId);
            return def != null ? def.Display : r.outputs[0].itemId;
        }

        string DescribeRecipe(RecipeDefinition r)
        {
            string outName = OutputName(r);
            int outCount = (r.outputs != null && r.outputs.Length > 0) ? r.outputs[0].count : 1;
            string ins = "";
            if (r.inputs != null)
            {
                for (int i = 0; i < r.inputs.Length; i++)
                {
                    var d = _content.Items.Get(r.inputs[i].itemId);
                    ins += (i > 0 ? ", " : "") + $"{(d != null ? d.Display : r.inputs[i].itemId)} x{r.inputs[i].count}";
                }
            }
            return $"{outName} x{outCount} <- {ins}";
        }

        string RecipeBlockedReason(RecipeDefinition r)
        {
            if (r == null) return "Unavailable";
            if (_crafting == null || _inv == null) return "No crafting";

            if (r.station != StationType.None && r.station != StationType.Hand)
            {
                bool stationOk = _crafting.StationAvailable != null && _crafting.StationAvailable(r.station);
                if (!stationOk)
                    return $"Needs {r.station}";
            }

            if (r.inputs != null)
            {
                for (int i = 0; i < r.inputs.Length; i++)
                {
                    var input = r.inputs[i];
                    if (!_inv.Has(input.itemId, input.count))
                    {
                        var def = _content.Items.Get(input.itemId);
                        return $"Need {(def != null ? def.Display : input.itemId)}";
                    }
                }
            }

            if (!_inv.CanExchange(r.inputs, r.outputs))
                return "Inventory full";

            return "Cannot craft";
        }
    }
}
