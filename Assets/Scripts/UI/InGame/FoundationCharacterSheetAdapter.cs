using System;
using IsoCore.Foundation;
using UnityEngine;

namespace LitIso.UI.InGame
{
    /// <summary>
    /// Wires the CharacterSheetView to live player data: PlayerStats, PlayerHealth,
    /// PlayerMana, and XPSystem. All in Assembly-CSharp — Foundation stays untouched.
    /// </summary>
    public sealed class FoundationCharacterSheetAdapter : ICharacterSheetViewModel, IDisposable
    {
        public event Action Changed;

        public string CharacterName => "Adventurer";
        public string ClassName     => PlayerStats.Instance != null ? "Survivor" : "—";
        public string TitleName     => XPSystem.Instance    != null ? TitleForLevel(XPSystem.Instance.CurrentLevel) : "Newcomer";
        public Sprite Portrait      => Resources.Load<Sprite>("UI/InGame/system_portrait");
        public int    Level         => XPSystem.Instance    != null ? XPSystem.Instance.CurrentLevel : 1;

        public float Health01
        {
            get
            {
                var ph = PlayerHealth.Instance;
                return (ph != null && ph.maxHealth > 0) ? Mathf.Clamp01((float)ph.CurrentHealth / ph.maxHealth) : 1f;
            }
        }

        public float Mana01
        {
            get
            {
                var pm = PlayerMana.Instance;
                return (pm != null && pm.MaxMana > 0) ? Mathf.Clamp01((float)pm.CurrentMana / pm.MaxMana) : 1f;
            }
        }

        public float Xp01
        {
            get
            {
                var xp = XPSystem.Instance;
                if (xp == null) return 0f;
                int needed = xp.XPNeededForNextLevel;
                return needed > 0 ? Mathf.Clamp01((float)xp.XPInCurrentLevel / needed) : 1f;
            }
        }

        public CharacterStats Stats
        {
            get
            {
                var s = PlayerStats.Instance;
                if (s == null) return new CharacterStats { str=5, dex=5, intel=5, vit=5, def=5, luck=5 };
                // Map PlayerStats (STR/AGI/VIT/INT/WIS/END) -> CharacterSheet (STR/DEX/INT/VIT/DEF/LUCK)
                // DEF <- END (endurance provides knockback/damage resist)
                // LUCK <- WIS (wisdom/insight maps closest to fortune in LitRPG)
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

        public FoundationCharacterSheetAdapter()
        {
            if (PlayerHealth.Instance != null) PlayerHealth.Instance.OnHealthChanged += OnVitals;
            if (PlayerMana.Instance   != null) PlayerMana.Instance.OnManaChanged     += OnVitals;
            PlayerStats.OnStatsChanged += OnChanged;
            XPSystem.OnXPGained       += OnXp;
            XPSystem.OnLevelUp        += OnLevel;
        }

        public void Dispose()
        {
            if (PlayerHealth.Instance != null) PlayerHealth.Instance.OnHealthChanged -= OnVitals;
            if (PlayerMana.Instance   != null) PlayerMana.Instance.OnManaChanged     -= OnVitals;
            PlayerStats.OnStatsChanged -= OnChanged;
            XPSystem.OnXPGained       -= OnXp;
            XPSystem.OnLevelUp        -= OnLevel;
        }

        void OnChanged()              => Changed?.Invoke();
        void OnVitals(int _a, int _b) => Changed?.Invoke();
        void OnXp(int _g, int _t)     => Changed?.Invoke();
        void OnLevel(int _l)          => Changed?.Invoke();

        static string TitleForLevel(int level) => level switch
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
