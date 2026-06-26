using System;

namespace IsoCore.Foundation
{
    /// <summary>
    /// One-way decoupling layer. Gameplay code in the IsoCore.Foundation assembly
    /// must NOT reference the UI assembly (LitIso.UI.InGame lives in Assembly-CSharp,
    /// which references Foundation, never the reverse). So Foundation RAISES these
    /// events and the UI-side FoundationUiBridgeListener subscribes and opens the
    /// actual screens. This keeps the layering clean and the build valid.
    /// </summary>
    public static class FoundationUiBridge
    {
        public static event Action DeathScreenRequested;
        public static event Action<int, int> LevelUpRequested;                 // (fromLevel, toLevel)
        public static event Action<StationType, string, int> CraftingStationRequested; // (station, id, tier)
        public static event Action QuestBoardRequested;
        public static event Action MerchantRequested;

        public static void RequestDeathScreen() => DeathScreenRequested?.Invoke();
        public static void RequestLevelUp(int fromLevel, int toLevel) => LevelUpRequested?.Invoke(fromLevel, toLevel);
        public static void RequestCraftingStation(StationType station, string stationId, int tier)
            => CraftingStationRequested?.Invoke(station, stationId, tier);
        public static void RequestQuestBoard() => QuestBoardRequested?.Invoke();
        public static void RequestMerchant() => MerchantRequested?.Invoke();
    }
}
