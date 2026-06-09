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
    ///   5. Leaves the retired IMGUI FoundationHUD uncreated so there is one
    ///      canonical player-facing UI shell.
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
        static FoundationNotificationBridge _notifyBridge;
        static InGameNotificationView    _notifyView;
        static FoundationDayClockAdapter _dayAdapter;
        static DayClockView              _dayView;
        static PlayerInteraction         _boundInteraction;
        static FoundationBootstrap       _boundBootstrap;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Hook()
        {
            FoundationBootstrap.Ready -= OnFoundationReady;
            FoundationBootstrap.Ready += OnFoundationReady;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void BindAlreadyLoadedFoundation()
        {
            var bootstrap = Object.FindFirstObjectByType<FoundationBootstrap>();
            if (bootstrap != null)
                OnFoundationReady(bootstrap);
        }

        static void OnFoundationReady(FoundationBootstrap bootstrap)
        {
            if (bootstrap == null) return;
            if (bootstrap.Content == null || bootstrap.Inventory == null || bootstrap.Hotbar == null)
                return;

            FoundationUiCoordinator.SuppressLegacyHudSurfaces();
            FoundationUiCoordinator.Active?.SetHudViewMode(FoundationHudViewMode.Adventure);

            if (_boundBootstrap == bootstrap && _hud != null)
                return;

            _boundBootstrap = bootstrap;

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
            _panels.BindProgression(progression, bootstrap.QoL);
            BindInteractionPanelRequests(bootstrap.Interaction);

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

            // ---- System message feed ----------------------------------------
            // Ensure a SystemNotifier singleton exists so Announce() reaches listeners.
            if (SystemNotifier.Instance == null)
            {
                var sgo = new GameObject("[SystemNotifier]");
                Object.DontDestroyOnLoad(sgo);
                sgo.AddComponent<SystemNotifier>();
            }

            // Procedural toast feed (Foundation-free; self-subscribes to OnMessage).
            if (_notifyView == null)
            {
                var ngo = new GameObject("[uGUI Notifications]");
                Object.DontDestroyOnLoad(ngo);
                _notifyView = ngo.AddComponent<InGameNotificationView>();
            }

            // Bridge Foundation progression → System messages.
            _notifyBridge?.Dispose();
            _notifyBridge = progression != null ? new FoundationNotificationBridge(progression) : null;

            // ---- Day/time strip ---------------------------------------------
            _dayAdapter = bootstrap.DayNight != null ? new FoundationDayClockAdapter(bootstrap.DayNight) : null;

            if (_dayView == null)
            {
                var dgo = new GameObject("[uGUI DayClock]");
                Object.DontDestroyOnLoad(dgo);
                _dayView = dgo.AddComponent<DayClockView>();
            }

            if (_dayAdapter != null)
                _dayView.Init(_dayAdapter);

            // FoundationBootstrap no longer creates the old IMGUI FoundationHUD. The
            // uGUI controller, panels, quest tracker, notifications, day clock, map,
            // pause menu, and interaction overlay now form the single runtime shell.
        }

        static void BindInteractionPanelRequests(PlayerInteraction interaction)
        {
            if (_boundInteraction != null)
                _boundInteraction.CraftingRequested -= OnCraftingRequested;

            _boundInteraction = interaction;
            if (_boundInteraction != null)
                _boundInteraction.CraftingRequested += OnCraftingRequested;
        }

        static void OnCraftingRequested(StationType station)
        {
            _panels?.OpenCrafting();
        }
    }
}
