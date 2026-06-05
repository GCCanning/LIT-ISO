using UnityEngine;
using IsoCore.Foundation;

namespace LitIso.UI.InGame
{
    /// <summary>
    /// Auto-wires the uGUI HUD when the Foundation runtime is ready.
    ///
    /// Subscribes to <see cref="FoundationBootstrap.Ready"/> once per app lifetime
    /// (cleared on scene reloads via the BeforeSceneLoad hook). When Ready fires:
    ///   1. Build a <see cref="FoundationHudAdapter"/> over the bootstrap's
    ///      Inventory/Hotbar/Content.
    ///   2. Spawn a <see cref="GameUIController"/> GameObject under
    ///      DontDestroyOnLoad and call Init(adapter).
    ///   3. Disable the temporary IMGUI HUD (if Codex didn't already set
    ///      createImguiHud=false on the scene's bootstrap component) so the two HUDs
    ///      don't overlap.
    ///
    /// No scene wiring needed — drop this file in and it works.
    /// </summary>
    public static class GameHudInitializer
    {
        static FoundationHudAdapter _adapter;
        static GameUIController _hud;
        static GamePanelsController _panels;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Hook()
        {
            // Defensive: BeforeSceneLoad fires once per domain reload, so re-subscribe
            // safely. (Static event handlers can outlive scene reloads in Editor.)
            FoundationBootstrap.Ready -= OnFoundationReady;
            FoundationBootstrap.Ready += OnFoundationReady;
        }

        static void OnFoundationReady(FoundationBootstrap bootstrap)
        {
            if (bootstrap == null) return;

            // Dispose any prior adapter (scene reload, etc.) before re-binding.
            _adapter?.Dispose();
            _adapter = new FoundationHudAdapter(bootstrap.Inventory, bootstrap.Hotbar, bootstrap.Content);

            // Spawn the View if missing, else re-Init it against the new adapter.
            if (_hud == null)
            {
                var go = new GameObject("[uGUI HUD]");
                Object.DontDestroyOnLoad(go);
                _hud = go.AddComponent<GameUIController>();
            }
            _hud.Init(_adapter);

            // Spawn the in-game panels controller (Inventory / Crafting / Character
            // Sheet) alongside the HUD. Defaults to placeholder models; adapters can
            // bind via BindInventory/BindCrafting/BindCharacter once data sources land.
            if (_panels == null)
            {
                var pgo = new GameObject("[uGUI Panels]");
                Object.DontDestroyOnLoad(pgo);
                _panels = pgo.AddComponent<GamePanelsController>();
            }
            // The inventory adapter shares the HUD's data plumbing, so panels stay in
            // sync with the hotbar without a second subscription.
            _panels.BindInventory(new FoundationInventoryAdapter(bootstrap.Inventory, bootstrap.Content));

            // Disable the temporary IMGUI HUD so the two don't overlap. The scene's
            // FoundationBootstrap.createImguiHud field also lets Codex preset this to
            // false on the prefab — but disabling at runtime is safe either way.
            if (bootstrap.Hud != null) bootstrap.Hud.enabled = false;
        }
    }
}
