using UnityEngine;

namespace LitIso.UI.InGame
{
    /// <summary>
    /// Owns the in-game panels (Inventory / Crafting / Character Sheet) and routes
    /// keyboard toggles between them. Spawned by <see cref="GameHudInitializer"/>
    /// alongside the HUD; one of each panel exists from session start, shown/hidden
    /// on demand. Esc closes the topmost open panel (last-opened wins).
    ///
    /// Default bindings:
    ///   I = Inventory · C = Crafting · K (or Tab) = Character Sheet · Esc = close
    ///
    /// Models default to placeholders until adapters are bound via Init*().
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GamePanelsController : MonoBehaviour
    {
        InventoryView _inv;
        CraftingView _craft;
        CharacterSheetView _char;
        System.Collections.Generic.List<System.Action> _closeOrder; // LIFO close stack

        public InventoryView Inventory => _inv;
        public CraftingView Crafting => _craft;
        public CharacterSheetView CharacterSheet => _char;

        void Awake()
        {
            _closeOrder = new System.Collections.Generic.List<System.Action>(4);

            _inv   = NewPanel<InventoryView>("InventoryPanel");
            _craft = NewPanel<CraftingView>("CraftingPanel");
            _char  = NewPanel<CharacterSheetView>("CharacterPanel");

            // Default placeholder models — adapters replace these via Init*().
            _inv.Init(new PlaceholderInventoryViewModel());
            _craft.Init(new PlaceholderCraftingViewModel());
            _char.Init(new PlaceholderCharacterSheetViewModel());

            // Track close order so Esc dismisses the most recently opened panel.
            _inv.Closed   += () => _closeOrder.Remove(_inv.Hide);
            _craft.Closed += () => _closeOrder.Remove(_craft.Hide);
            _char.Closed  += () => _closeOrder.Remove(_char.Hide);
        }

        T NewPanel<T>(string name) where T : MonoBehaviour
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            return go.AddComponent<T>();
        }

        public void BindInventory(IInventoryViewModel m) => _inv.Init(m);
        public void BindCrafting(ICraftingViewModel m)   => _craft.Init(m);
        public void BindCharacter(ICharacterSheetViewModel m) => _char.Init(m);

        void Update()
        {
            // Esc → close topmost open panel.
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (_closeOrder.Count > 0)
                {
                    var top = _closeOrder[_closeOrder.Count - 1];
                    _closeOrder.RemoveAt(_closeOrder.Count - 1);
                    top();
                    return;
                }
            }
            // Toggle keys (don't fire while typing in a text input — uGUI input fields
            // consume focus, so checking Input.GetKeyDown is fine here).
            if (Input.GetKeyDown(KeyCode.I)) Toggle(_inv, _inv.Hide);
            if (Input.GetKeyDown(KeyCode.C)) Toggle(_craft, _craft.Hide);
            if (Input.GetKeyDown(KeyCode.K) || Input.GetKeyDown(KeyCode.Tab)) Toggle(_char, _char.Hide);
        }

        void Toggle<T>(T panel, System.Action hide) where T : MonoBehaviour
        {
            // Use reflection-free dispatch via duck typing on the concrete types.
            if (panel is InventoryView i) {
                if (i.IsOpen) { i.Hide(); }
                else          { i.Show(); _closeOrder.Add(hide); }
            } else if (panel is CraftingView c) {
                if (c.IsOpen) { c.Hide(); }
                else          { c.Show(); _closeOrder.Add(hide); }
            } else if (panel is CharacterSheetView s) {
                if (s.IsOpen) { s.Hide(); }
                else          { s.Show(); _closeOrder.Add(hide); }
            }
        }
    }
}
