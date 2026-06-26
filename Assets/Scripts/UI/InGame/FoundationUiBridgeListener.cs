using IsoCore.Foundation;
using UnityEngine;

namespace LitIso.UI.InGame
{
    /// <summary>
    /// UI-side listener for <see cref="FoundationUiBridge"/>. Foundation (gameplay)
    /// raises bridge events; this opens the corresponding LitIso.UI.InGame screens.
    /// Keeps the Foundation assembly free of any UI dependency (UI → Foundation only).
    /// Self-installs once at startup.
    /// </summary>
    public static class FoundationUiBridgeListener
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Hook()
        {
            FoundationUiBridge.DeathScreenRequested -= OnDeath;
            FoundationUiBridge.DeathScreenRequested += OnDeath;
            FoundationUiBridge.LevelUpRequested -= OnLevelUp;
            FoundationUiBridge.LevelUpRequested += OnLevelUp;
            FoundationUiBridge.CraftingStationRequested -= OnCraftingStation;
            FoundationUiBridge.CraftingStationRequested += OnCraftingStation;
            // QuestBoardRequested is subscribed inside QuestBoardView itself
            // (QuestBoardView.HookBridge) to avoid a hard type reference here.
            FoundationUiBridge.MerchantRequested -= OnMerchant;
            FoundationUiBridge.MerchantRequested += OnMerchant;
        }

        static void OnDeath() => DeathView.Show();
        static void OnLevelUp(int from, int to) => LevelUpView.Show(from, to);
        static void OnMerchant() => VendorView.OpenExternal();

        static void OnCraftingStation(StationType station, string id, int tier)
        {
            CraftingStationContext.Set(station, id, tier);
            Object.FindFirstObjectByType<CharacterPanelView>()?.Show(CharacterPanelTab.Crafting);
        }
    }
}
