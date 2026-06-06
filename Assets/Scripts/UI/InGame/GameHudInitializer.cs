using UnityEngine;
using IsoCore.Foundation;

namespace LitIso.UI.InGame
{
    /// <summary>
    /// Auto-wires the uGUI HUD when the Foundation runtime is ready.
    ///
    /// Subscribes to <see cref="FoundationBootstrap.Ready"/> once per app lifetime.
    /// When Ready fires:
    ///   1. Build a <see cref="FoundationHudAdapter"/> — passes <c>bootstrap.Stats</c>
    ///      so vitals prefer Foundation over legacy singletons.
    ///   2. Spawn / re-init a <see cref="GameUIController"/> under DontDestroyOnLoad.
    ///   3. Spawn / re-init a <see cref="GamePanelsController"/> and bind:
    ///        - InventoryPanel  → <see cref="FoundationInventoryAdapter"/>
    ///        - CharacterPanel  → <see cref="FoundationCharacterSheetAdapter"/> (bootstrap.Stats)
    ///        - CraftingPanel   → <see cref="FoundationCraftingAdapter"/>
    ///   4. Spawn / re-init a <see cref="QuestTrackerView"/> and bind:
    ///        - <see cref="QuestTrackerAdapter"/> (bootstrap.Progression)
    ///   5. Disable the IMGUI HUD if present.
    ///
    /// No scene wiring needed — drop this file in and it works.
    /// </summary>
    public static class GameHudInitializer
    {
        static FoundationHudAdapter      _adapter;
        static GameUIController          _hud;
        static GamePanelsController      _panels;
        static QuestTrackerAdapter       _questAdapter;
        static QuestTrackerView          _questView;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Hook()
        {
            FoundationBootstrap.Ready -= OnFoundationReady;
            FoundationBootstrap.Ready += OnFoundationReady;
        }

        static void OnFoundationReady(FoundationBootstrap bootstrap)
        {
            if (bootstrap == null) return;

            // bootstrap.Stats → FoundationPlayerStats (may be null pre-merge of
            // codex/litrpg-foundation-systems; adapters fall back to legacy singletons).
            var stats       = bootstrap.Stats;
            var progression = bootstrap.Progression;

            // ---- HUD (vitals + hotbar slots) --------------------------------
            _adapter?.Dispose();
            _adapter = new FoundationHudAdapter(bootstrap.Inventory, bootstrap.Hotbar, bootstrap.Content, stats);

            if (_hud == null)
            {
                var go = new GameObject("[uGUI HUD]");
                Object.DontDestroyOnLoad(go);
                _hud = go.AddComponent<GameUIController>();
            }
            _hud.Init(_adapter);

            // ---- Panels (Inventory / Crafting / Character Sheet) ------------
            if (_panels == null)
            {
                var pgo = new GameObject("[uGUI Panels]");
                Object.DontDestroyOnLoad(pgo);
                _panels = pgo.AddComponent<GamePanelsController>();
            }

            _panels.BindInventory(new FoundationInventoryAdapter(bootstrap.Inventory, bootstrap.Content));
            _panels.BindCharacter(new FoundationCharacterSheetAdapter(stats));
            _panels.BindCrafting(new FoundationCraftingAdapter(bootstrap.Crafting, bootstrap.Inventory, bootstrap.Content));

            // ---- Quest tracker overlay --------------------------------------
            _questAdapter?.Dispose();
            _questAdapter = progression != null ? new QuestTrackerAdapter(progression) : null;

            if (_questView == null)
            {
                var qgo = new GameObject("[uGUI QuestTracker]");
                Object.DontDestroyOnLoad(qgo);
                _questView = qgo.AddComponent<QuestTrackerView>();
            }

            if (_questAdapter != null)
                _questView.Init(_questAdapter);

            // ---- Disable IMGUI HUD -----------------------------------------
            if (bootstrap.Hud != null) bootstrap.Hud.enabled = false;
        }
    }
}
