using System;
using IsoCore.Foundation;
using UnityEngine;

namespace LitIso.UI.InGame
{
    /// <summary>
    /// Real ability loadout backed by the live FoundationAbilitySystem + dispatcher.
    /// Replaces PlaceholderAbilityLoadoutViewModel. Q/E/R/F default to
    /// flash_step / mana_bolt / ember_spark / mending_light, and Cast() routes through
    /// Abilities.TryUseAbility (cost/cooldown/XP/affinity) then AbilityDispatcher.Execute
    /// (the actual blink / projectile / heal + VFX). Assignments persist in PlayerPrefs.
    /// </summary>
    public sealed class FoundationAbilityLoadout : IAbilityLoadoutViewModel
    {
        // The abilities offered in the hold-X radial, and the Q/E/R/F defaults.
        static readonly string[] AllIds = { "flash_step", "mana_bolt", "ember_spark", "mending_light" };
        static readonly string[] DefaultSlots = { "flash_step", "mana_bolt", "ember_spark", "mending_light" };

        readonly FoundationBootstrap _boot;
        readonly string[] _slots = new string[4];

        public event Action Changed;

        public FoundationAbilityLoadout(FoundationBootstrap boot)
        {
            _boot = boot;
            for (int i = 0; i < 4; i++)
            {
                string fallback = i < DefaultSlots.Length ? DefaultSlots[i] : null;
                string saved = PlayerPrefs.GetString("ability.slot" + i, fallback);
                _slots[i] = string.IsNullOrEmpty(saved) ? fallback : saved;
            }
        }

        public int AbilityCount => AllIds.Length;

        public AbilityData GetAbility(int i)
        {
            string id = (i >= 0 && i < AllIds.Length) ? AllIds[i] : null;
            return ToData(id);
        }

        public string GetSlot(int slot) => (slot >= 0 && slot < 4) ? _slots[slot] : null;

        public void Assign(int slot, string abilityId)
        {
            if (slot < 0 || slot >= 4) return;
            _slots[slot] = abilityId;
            PlayerPrefs.SetString("ability.slot" + slot, abilityId ?? "");
            PlayerPrefs.Save();
            Changed?.Invoke();
        }

        public bool Cast(string abilityId)
        {
            if (string.IsNullOrEmpty(abilityId) || _boot?.Abilities == null)
                return false;

            if (!_boot.Abilities.TryUseAbility(abilityId, "player", out var result))
                return false;

            var def = _boot.Content?.Abilities.Get(abilityId);
            if (def != null)
                _boot.AbilityDispatcher?.Execute(def, result);

            Changed?.Invoke();
            return true;
        }

        AbilityData ToData(string id)
        {
            if (string.IsNullOrEmpty(id))
                return default;

            var def = _boot?.Content?.Abilities.Get(id);
            if (def == null)
                return new AbilityData { id = id, name = id, costText = "", ready = false };

            string cost = def.resource == FoundationAbilityResource.Mana
                ? $"MP {def.resourceCost}"
                : $"ST {def.resourceCost}";

            bool ready = true;
            var states = _boot.Abilities?.CaptureReadState();
            if (states != null)
                foreach (var s in states)
                    if (s.id == id) { ready = s.canUse; break; }

            return new AbilityData { id = id, name = def.Display, costText = cost, ready = ready };
        }
    }
}
