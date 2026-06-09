namespace LitIso.UI.InGame
{
    /// <summary>
    /// Foundation-free read model for the HUD day/time strip. The View polls these each
    /// frame (time advances continuously, so there is no Changed event). The Foundation
    /// implementation is <see cref="FoundationDayClockAdapter"/>.
    /// </summary>
    public interface IDayClockViewModel
    {
        /// <summary>Wall-clock string, e.g. "08:34".</summary>
        string TimeText { get; }

        /// <summary>Phase name: "Dawn" / "Day" / "Dusk" / "Night".</summary>
        string PhaseLabel { get; }

        /// <summary>0 at full day, 1 at deep night — drives the strip's tint.</summary>
        float Night01 { get; }
    }
}
