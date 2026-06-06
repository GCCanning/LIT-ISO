using System;
using IsoCore.Foundation;
using UnityEngine;

namespace LitIso.UI.InGame
{
    /// <summary>
    /// Wires <see cref="CharacterSheetView"/> to live player data.
    ///
    /// Preferred path (post <c>codex/litrpg-foundation-systems</c> merge):
    ///   Reads Class, Title, Level, all six stats, and all three vitals directly
    ///   from <see cref="FoundationPlayerStats"/> (<c>FoundationBootstrap.Stats</c>).
    ///   One Changed event drives the entire view.
    ///
    /// Legacy fallback (stats == null):
    ///   Falls back to Assembly-CSharp <c>PlayerStats</c>, <c>PlayerHealth</c>,
    ///   <c>PlayerMana</c>, <c>XPSystem</c> singletons exactly as before, so the
    ///   view still renders correctly on scenes without the Foundation runtime.
    ///
    /// Lives in Assembly-CSharp. Foundation assembly never references us.
    /// </summary>
    public sealed class FoundationCharacterSheetAdapter : ICharacterSheetViewModel, IDisposable
    {
        readonly FoundationPlayerStats _stats; // null → use legacy singletons

        public event Action Changed;

        // ---- constructor ----------------------------------------------------

        /// <param name="stats">
        ///   Pass <c>FoundationBootstrap.Stats</c>. Null falls back to legacy singletons.
        /// </param>
        public FoundationCharacterSheetAdapter(FoundationPlayerStats stats = null)
        {
            _stats = stats;

            if (_stats != null)
            {
                _stats.Changed += OnChanged;
            }
            else
            {
                // Legacy subscriptions — only wired when Foundation stats are absent.
                if (PlayerHealth.Instance != null) PlayerHealth.Instance.OnHealthChanged += OnVitals;
                if (PlayerMana.Instance   != null) PlayerMana.Instance.OnManaChanged     += OnVitals;
                PlayerStats.OnStatsChanged += OnChanged;
                XPSystem.OnXPGained       += OnXp;
                XPSystem.OnLevelUp        += OnLevel;
            }
        }

        public void Dispose()
        {
            if (_stats != null)
            {
                _stats.Changed -= OnChanged;
            }
            else
            {
                if (PlayerHealth.Instance != null) PlayerHealth.Instance.OnHealthChanged -= OnVitals;
                if (PlayerMana.Instance   != null) PlayerMana.Instance.OnManaChanged     -= OnVitals;
                PlayerStats.OnStatsChanged -= OnChanged;
                XPSystem.OnXPGained       -= OnXp;
                XPSystem.OnLevelUp        -= OnLevel;
            }
        }

        void OnChanged()              => Changed?.Invoke();
        void OnVitals(int _a, int _b) => Changed?.Invoke();
        void OnXp(int _g, int _t)     => Changed?.Invoke();
        void OnLevel(int _l)          => Changed?.Invoke();

        // ---- ICharacterSheetViewModel: identity -----------------------------

        public string CharacterName => "Adventurer";

        public string ClassName =>
            _stats != null ? _stats.Class
            : PlayerStats.Instance != null ? "Survivor"
            : "—";

        public string TitleName =>
            _stats != null ? _stats.Title
            : XPSystem.Instance != null ? LegacyTitleForLevel(XPSystem.Instance.CurrentLevel)
            : "Newcomer";

        public Sprite Portrait => Resources.Load<Sprite>("UI/InGame/system_portrait");

        public int Level =>
            _stats != null ? _stats.Level
            : XPSystem.Instance != null ? XPSystem.Instance.CurrentLevel
            : 1;

        // ---- ICharacterSheetViewModel: vitals -------------------------------

        public float Health01
        {
            get
            {
                if (_stats != null) return _stats.Health01;
                var ph = PlayerHealth.Instance;
                return (ph != null && ph.maxHealth > 0) ? Mathf.Clamp01((float)ph.CurrentHealth / ph.maxHealth) : 1f;
            }
        }

        public float Mana01
        {
            get
            {
                if (_stats != null) return _stats.Mana01;
                var pm = PlayerMana.Instance;
                return (pm != null && pm.MaxMana > 0) ? Mathf.Clamp01((float)pm.CurrentMana / pm.MaxMana) : 1f;
            }
        }

        public float Xp01
        {
            get
            {
                if (_stats != null) return _stats.Xp01;
                var xp = XPSystem.Instance;
                if (xp == null) return 0f;
                int needed = xp.XPNeededForNextLevel;
                return needed > 0 ? Mathf.Clamp01((float)xp.XPInCurrentLevel / needed) : 1f;
            }
        }

        // ---- ICharacterSheetViewModel: stats --------------------------------

        public CharacterStats Stats
        {
            get
            {
                if (_stats != null)
                {
                    // FoundationPlayerStats already uses STR/DEX/INT/VIT/DEF/LUCK —
                    // matches the CharacterStats struct field for field.
                    return new CharacterStats
                    {
                        str   = _stats.STR,
                        dex   = _stats.DEX,
                        intel = _stats.INT,
                        vit   = _stats.VIT,
                        def   = _stats.DEF,
                        luck  = _stats.LUCK,
                    };
                }

                // Legacy mapping: PlayerStats (STR/AGI/VIT/INT/WIS/END) ->
                //   CharacterSheet (STR/DEX/INT/VIT/DEF/LUCK)
                var s = PlayerStats.Instance;
                if (s == null) return new CharacterStats { str=5, dex=5, intel=5, vit=5, def=5, luck=5 };
                return new CharacterStats
                {
                    str   = s.STR,
                    dex   = s.AGI,
                    intel = s.INT,
                    vit   = s.VIT,
                    def   = s.END,
                    luck  = s.WIS,
                };
            }
        }

        // ---- legacy helper --------------------------------------------------

        static string LegacyTitleForLevel(int level) => level switch
        {
            1  => "Newcomer",
            2  => "Wanderer",
            3  => "Scout",
            4  => "Explorer",
            5  => "Adventurer",
            6  => "Veteran",
            7  => "Champion",
            8  => "Hero",
            9  => "Legend",
            _  => level >= 10 ? "Mythic" : "Newcomer",
        };
    }
}
