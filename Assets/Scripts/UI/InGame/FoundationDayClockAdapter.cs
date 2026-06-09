using IsoCore.Foundation;
using UnityEngine;

namespace LitIso.UI.InGame
{
    /// <summary>
    /// Maps <see cref="DayNightSystem"/> onto <see cref="IDayClockViewModel"/> so the
    /// Foundation-free <see cref="DayClockView"/> can render the time strip without
    /// referencing IsoCore.Foundation. Reads live each access — cheap string/float getters.
    /// </summary>
    public sealed class FoundationDayClockAdapter : IDayClockViewModel
    {
        readonly DayNightSystem _day;

        public FoundationDayClockAdapter(DayNightSystem day) => _day = day;

        public string TimeText   => _day != null ? _day.Clock : "--:--";
        public string PhaseLabel => _day != null ? _day.PhaseLabel : "";
        public float  Night01    => _day != null ? Mathf.Clamp01(_day.NightFactor) : 0f;
    }
}
