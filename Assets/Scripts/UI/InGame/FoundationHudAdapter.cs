using System;
using IsoCore.Foundation;
using UnityEngine;

namespace LitIso.UI.InGame
{
    /// <summary>
    /// Maps Codex's runtime Foundation systems onto <see cref="IGameHudModel"/>
    /// so the uGUI HUD renders live data.
    ///
    /// Vitals (HP/MP/XP/Level) have two paths:
    ///   Preferred — <see cref="FoundationPlayerStats"/> (<c>FoundationBootstrap.Stats</c>)
    ///               available after <c>codex/litrpg-foundation-systems</c> merges.
    ///   Fallback  — Assembly-CSharp <c>PlayerHealth</c>/<c>PlayerMana</c>/<c>XPSystem</c>
    ///               singletons for scenes without the full Foundation runtime.
    ///
    /// Inventory/Hotbar slots always come from Foundation Inventory + Hotbar.
    ///
    /// Lives in Assembly-CSharp. Foundation assembly never references us.
    /// </summary>
    public sealed class FoundationHudAdapter : IGameHudModel, IDisposable
    {
        readonly Inventory _inv;
        readonly Hotbar _hotbar;
        readonly FoundationContent _content;
        readonly FoundationPlayerStats _stats; // null → use legacy singletons

        public event Action Changed;

        public FoundationHudAdapter(Inventory inv, Hotbar hotbar, FoundationContent content,
                                    FoundationPlayerStats stats = null)
        {
            _inv     = inv;
            _hotbar  = hotbar;
            _content = content;
            _stats   = stats;

            ItemIconResolver.Bind(content);

            if (_inv    != null) _inv.OnChanged            += OnChanged;
            if (_hotbar != null) _hotbar.OnSelectionChanged += OnChanged;

            if (_stats != null)
            {
                // Preferred: one event covers all vitals.
                _stats.Changed += OnChanged;
            }
            else
            {
                // Legacy subscriptions used when Foundation progression isn't present.
                if (PlayerHealth.Instance != null) PlayerHealth.Instance.OnHealthChanged += OnVitalsChanged;
                if (PlayerMana.Instance   != null) PlayerMana.Instance.OnManaChanged     += OnVitalsChanged;
                PlayerStats.OnStatsChanged += OnChanged;
                XPSystem.OnXPGained       += OnXpGained;
                XPSystem.OnLevelUp        += OnLevelUp;
            }
        }

        public void Dispose()
        {
            if (_inv    != null) _inv.OnChanged            -= OnChanged;
            if (_hotbar != null) _hotbar.OnSelectionChanged -= OnChanged;

            if (_stats != null)
            {
                _stats.Changed -= OnChanged;
            }
            else
            {
                if (PlayerHealth.Instance != null) PlayerHealth.Instance.OnHealthChanged -= OnVitalsChanged;
                if (PlayerMana.Instance   != null) PlayerMana.Instance.OnManaChanged     -= OnVitalsChanged;
                PlayerStats.OnStatsChanged -= OnChanged;
                XPSystem.OnXPGained       -= OnXpGained;
                XPSystem.OnLevelUp        -= OnLevelUp;
            }
        }

        void OnChanged()                         => Changed?.Invoke();
        void OnVitalsChanged(int _a, int _b)     => Changed?.Invoke();
        void OnXpGained(int _gained, int _total) => Changed?.Invoke();
        void OnLevelUp(int _newLevel)            => Changed?.Invoke();

        // ---- IGameHudModel: slot data ---------------------------------------

        public int SlotCount => _hotbar?.Size ?? 0;

        public HudSlot GetSlot(int i)
        {
            if (_inv == null) return default;
            var st = _inv.GetSlot(i);
            string itemId = st.itemId;
            bool empty = string.IsNullOrEmpty(itemId);

            string label = empty ? "" : itemId;
            if (!empty && _content != null)
            {
                var def = _content.Items.Get(itemId);
                if (def != null && !string.IsNullOrEmpty(def.Display)) label = def.Display;
            }

            return new HudSlot
            {
                label    = label,
                count    = empty ? 0 : st.count,
                icon     = empty ? null : ItemIconResolver.Resolve(itemId),
                selected = _hotbar != null && i == _hotbar.Selected,
            };
        }

        // ---- IGameHudModel: vitals ------------------------------------------

        public float Health01
        {
            get
            {
                if (_stats != null) return _stats.Health01;
                var ph = PlayerHealth.Instance;
                if (ph == null || ph.maxHealth <= 0) return 1f;
                return Mathf.Clamp01((float)ph.CurrentHealth / ph.maxHealth);
            }
        }

        public float Mana01
        {
            get
            {
                if (_stats != null) return _stats.Mana01;
                var pm = PlayerMana.Instance;
                if (pm == null || pm.MaxMana <= 0) return 1f;
                return Mathf.Clamp01((float)pm.CurrentMana / pm.MaxMana);
            }
        }

        public float Hunger01 => 0f;   // hidden by default — survival scope not yet active

        public float Xp01
        {
            get
            {
                if (_stats != null) return _stats.Xp01;
                var xp = XPSystem.Instance;
                if (xp == null) return 0f;
                int needed = xp.XPNeededForNextLevel;
                if (needed <= 0) return 1f;
                return Mathf.Clamp01((float)xp.XPInCurrentLevel / needed);
            }
        }

        public int Level
        {
            get
            {
                if (_stats != null) return _stats.Level;
                return XPSystem.Instance != null ? XPSystem.Instance.CurrentLevel : 1;
            }
        }
    }
}
