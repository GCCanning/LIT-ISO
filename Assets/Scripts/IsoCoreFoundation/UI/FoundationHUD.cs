using UnityEngine;

namespace IsoCore.Foundation
{
    /// <summary>
    /// IMGUI HUD: hotbar strip, inventory panel, crafting panel, interaction prompt,
    /// validation banner. IMGUI is used for zero-asset reliability; a uGUI/pixel
    /// upgrade is future scope (see migration plan).
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

        bool _invOpen;
        bool _craftOpen;
        StationType _station = StationType.Hand;
        string _flash = "";
        float _flashTimer;

        public string Banner = "ISO-Core Foundation";

        Rect _invRect = new(20, 70, 320, 360);
        Rect _craftRect = new(360, 70, 360, 420);
        Rect _hotbarRect;

        public void Init(Inventory inv, Hotbar hotbar, FoundationContent content,
            CraftingSystem crafting, IsoFoundationPlayer player, IsoWorld world, DayNightSystem dayNight)
        {
            _inv = inv; _hotbar = hotbar; _content = content;
            _crafting = crafting; _player = player; _world = world; _dayNight = dayNight;
        }

        public void ToggleInventory() => _invOpen = !_invOpen;

        public void ToggleCrafting(StationType station)
        {
            if (_craftOpen && _station == station) { _craftOpen = false; return; }
            _station = station; _craftOpen = true;
        }

        public void Flash(string msg) { _flash = msg; _flashTimer = 2.5f; }

        /// <summary>True when the cursor is over an open panel (suppress world clicks).</summary>
        public bool PointerOverUI
        {
            get
            {
                Vector2 g = new(Input.mousePosition.x, Screen.height - Input.mousePosition.y);
                if (_invOpen && _invRect.Contains(g)) return true;
                if (_craftOpen && _craftRect.Contains(g)) return true;
                if (_hotbarRect.Contains(g)) return true;
                return false;
            }
        }

        void Update()
        {
            if (_flashTimer > 0f) { _flashTimer -= Time.deltaTime; if (_flashTimer <= 0f) _flash = ""; }
        }

        void OnGUI()
        {
            if (_inv == null) return;

            // Cozy day/night tint over the world (drawn first, under the HUD widgets).
            if (_dayNight != null && _dayNight.NightTint.a > 0.01f)
            {
                var prev = GUI.color;
                GUI.color = _dayNight.NightTint;
                GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
                GUI.color = prev;
            }

            // Banner + controls.
            GUI.Label(new Rect(20, 10, 900, 24),
                $"{Banner}   |   WASD move · 1-9/scroll select · LMB use/place/break · RMB options/remove · I inv · C craft");
            if (_player != null && _world != null)
            {
                var c = _player.CurrentCell;
                var biome = _world.GetBiome(c.x, c.y);
                string time = _dayNight != null ? $"   {_dayNight.Clock} {_dayNight.PhaseLabel}" : "";
                GUI.Label(new Rect(20, 32, 900, 24),
                    $"cell ({c.x},{c.y})  height {_player.Height}  biome {(biome != null ? biome.Display : "?")}{time}");
            }

            DrawHotbar();
            if (_invOpen) _invRect = GUI.Window(7001, _invRect, DrawInventoryWindow, "Inventory");
            if (_craftOpen) _craftRect = GUI.Window(7002, _craftRect, DrawCraftingWindow, $"Crafting — {_station}");

            if (!string.IsNullOrEmpty(_flash))
                GUI.Label(new Rect(Screen.width / 2 - 150, Screen.height - 120, 300, 24), _flash);
        }

        void DrawHotbar()
        {
            int n = _hotbar.Size;
            float slot = 54f, pad = 6f;
            float total = n * (slot + pad);
            float x0 = (Screen.width - total) * 0.5f;
            float y = Screen.height - slot - 16f;
            _hotbarRect = new Rect(x0, y, total, slot + 4);

            for (int i = 0; i < n; i++)
            {
                var r = new Rect(x0 + i * (slot + pad), y, slot, slot);
                GUI.Box(r, GUIContent.none);
                if (i == _hotbar.Selected)
                {
                    var old = GUI.color; GUI.color = Color.yellow;
                    GUI.Box(r, GUIContent.none); GUI.color = old;
                }
                var st = _inv.GetSlot(i);
                if (!st.IsEmpty) DrawItem(new Rect(r.x + 4, r.y + 4, slot - 8, slot - 8), st);
                GUI.Label(new Rect(r.x + 3, r.y - 2, slot, 18), (i + 1).ToString());
            }
        }

        void DrawItem(Rect r, ItemStack st)
        {
            var def = _content.Items.Get(st.itemId);
            var col = def != null ? def.color : Color.gray;
            var old = GUI.color; GUI.color = col;
            GUI.DrawTexture(new Rect(r.x, r.y, r.width, r.height - 14), Texture2D.whiteTexture);
            GUI.color = old;
            GUI.Label(new Rect(r.x, r.y + r.height - 16, r.width, 16),
                $"{(def != null ? def.Display : st.itemId)} x{st.count}");
        }

        void DrawInventoryWindow(int id)
        {
            float y = 24f;
            for (int i = 0; i < _inv.SlotCount; i++)
            {
                var st = _inv.GetSlot(i);
                if (st.IsEmpty) continue;
                var def = _content.Items.Get(st.itemId);
                var old = GUI.color; GUI.color = def != null ? def.color : Color.gray;
                GUI.DrawTexture(new Rect(16, y, 16, 16), Texture2D.whiteTexture);
                GUI.color = old;
                GUI.Label(new Rect(40, y, 260, 18), $"{(def != null ? def.Display : st.itemId)}  x{st.count}");
                y += 20f;
                if (y > _invRect.height - 24) break;
            }
            GUI.DragWindow(new Rect(0, 0, 10000, 22));
        }

        void DrawCraftingWindow(int id)
        {
            float y = 26f;
            foreach (var r in _content.Recipes.All)
            {
                bool stationMatch = r.station == _station ||
                    r.station == StationType.Hand || r.station == StationType.None;
                if (!stationMatch) continue;

                bool can = _crafting.CanCraft(r);
                string label = DescribeRecipe(r);
                GUI.enabled = can;
                if (GUI.Button(new Rect(12, y, _craftRect.width - 24, 34), label))
                {
                    if (_crafting.TryCraft(r)) Flash($"Crafted {OutputName(r)}");
                }
                GUI.enabled = true;
                y += 38f;
                if (y > _craftRect.height - 30) break;
            }
            GUI.DragWindow(new Rect(0, 0, 10000, 22));
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
                for (int i = 0; i < r.inputs.Length; i++)
                {
                    var d = _content.Items.Get(r.inputs[i].itemId);
                    ins += (i > 0 ? ", " : "") + $"{(d != null ? d.Display : r.inputs[i].itemId)} x{r.inputs[i].count}";
                }
            return $"{outName} x{outCount}   ⟵   {ins}";
        }
    }
}
