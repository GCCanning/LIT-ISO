using IsoCore.Foundation;

namespace LitIso.UI.InGame
{
    /// <summary>
    /// Lightweight, process-wide hint describing which world station the player most
    /// recently interacted with to open the Crafting tab. The Crafting UI
    /// (<see cref="CharacterPanelView"/> DrawCrafting) can read this to filter the
    /// recipe list to the active station and, eventually, gate recipes/outputs by
    /// the station's tier.
    ///
    /// This is intentionally just a hint (a couple of static fields) and NOT a new
    /// system: it carries the station id + StationType + tier from the interact
    /// dispatch to the recipe view without rewiring events. Opening Crafting from a
    /// menu (Stations Hub) or hotkey should call <see cref="Clear"/> so the list
    /// shows all stations again.
    /// </summary>
    public static class CraftingStationContext
    {
        /// <summary>Placeable id of the station whose UI is open (e.g. "workbench",
        /// "furnace", "tannery"). Empty when opened without a specific station.</summary>
        public static string ActiveStationId { get; private set; } = string.Empty;

        /// <summary>The StationType the recipe list should be filtered to. None when
        /// no specific station is active (show everything).</summary>
        public static StationType ActiveStation { get; private set; } = StationType.None;

        /// <summary>
        /// Tier of the active station. PlaceableDefinition/RecipeDefinition do not yet
        /// carry a tier field, so this is always 0 today.
        /// TODO(tiers): once PlaceableDefinition gains a `stationTier` (and
        /// RecipeDefinition a `minStationTier`/`outputTier`), set this from the
        /// interacted placeable so DrawCrafting can hide recipes whose
        /// minStationTier &gt; ActiveStationTier and surface higher-tier outputs for
        /// higher-tier stations. Owner intent: higher-tier station -&gt; more recipes +
        /// better outputs.
        /// </summary>
        public static int ActiveStationTier { get; private set; } = 0;

        /// <summary>True when a specific world station is driving the Crafting view.</summary>
        public static bool HasActiveStation => ActiveStation != StationType.None;

        /// <summary>Record the station the player just interacted with.</summary>
        public static void Set(StationType station, string stationId, int tier = 0)
        {
            ActiveStation = station;
            ActiveStationId = stationId ?? string.Empty;
            ActiveStationTier = tier;
        }

        /// <summary>Clear the hint (Crafting opened generically -> show all stations).</summary>
        public static void Clear()
        {
            ActiveStation = StationType.None;
            ActiveStationId = string.Empty;
            ActiveStationTier = 0;
        }
    }
}
