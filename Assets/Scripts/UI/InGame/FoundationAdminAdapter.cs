using System;
using IsoCore.Foundation;
using UnityEngine;

namespace LitIso.UI.InGame
{
    /// <summary>One row in the Admin tab's "give item" grid.</summary>
    public struct AdminItemEntry
    {
        public string id;
        public string label;
        public Sprite icon;
        public Color color;
    }

    /// <summary>
    /// Model the Admin/debug tab renders from: a browsable list of every item
    /// definition (give to inventory for hotbar testing) plus +/- controls for
    /// player level and core stats. Foundation-free interface; the canonical
    /// implementation is <see cref="FoundationAdminAdapter"/>.
    /// </summary>
    public interface IAdminViewModel
    {
        event Action Changed;

        int ItemCount { get; }
        AdminItemEntry GetItem(int index);

        /// <summary>Gives <paramref name="count"/> of itemId to the player's inventory
        /// (for hotbar/equip testing). No-op if the inventory is full/unavailable.</summary>
        void GiveItem(string itemId, int count);

        int Level { get; }
        CharacterStats Stats { get; }

        /// <summary>Adjusts one core stat (STR/DEX/INT/VIT/DEF/LUCK) by +/-amount.</summary>
        void AdjustStat(FoundationStatType stat, int amount);

        /// <summary>Adjusts the player's level by +/-amount (min level 1).</summary>
        void AdjustLevel(int amount);
    }

    /// <summary>
    /// Wraps the live Foundation <see cref="Inventory"/>, <see cref="FoundationContent"/>
    /// item database, and <see cref="FoundationPlayerStats"/> for the Admin tab.
    /// </summary>
    public sealed class FoundationAdminAdapter : IAdminViewModel, IDisposable
    {
        readonly Inventory _inventory;
        readonly FoundationContent _content;
        readonly FoundationPlayerStats _stats;

        public event Action Changed;

        public FoundationAdminAdapter(Inventory inventory, FoundationContent content, FoundationPlayerStats stats)
        {
            _inventory = inventory;
            _content = content;
            _stats = stats;
            if (_stats != null)
                _stats.Changed += RaiseChanged;
        }

        public int ItemCount => _content?.Items?.Count ?? 0;

        public AdminItemEntry GetItem(int index)
        {
            var items = _content?.Items;
            if (items == null || index < 0 || index >= items.Count)
                return default;

            var def = items[index];
            if (def == null)
                return default;

            return new AdminItemEntry
            {
                id = def.Id,
                label = def.Display,
                icon = def.Icon,
                color = def.color,
            };
        }

        public void GiveItem(string itemId, int count)
        {
            if (_inventory == null || string.IsNullOrEmpty(itemId) || count <= 0)
                return;

            _inventory.Add(itemId, count);
            RaiseChanged();
        }

        public int Level => _stats?.Level ?? 1;

        public CharacterStats Stats => _stats == null
            ? default
            : new CharacterStats
            {
                str = _stats.STR,
                dex = _stats.DEX,
                intel = _stats.INT,
                vit = _stats.VIT,
                def = _stats.DEF,
                luck = _stats.LUCK,
            };

        public void AdjustStat(FoundationStatType stat, int amount) => _stats?.DebugAdjustStat(stat, amount);

        public void AdjustLevel(int amount)
        {
            if (_stats == null) return;
            _stats.DebugSetLevel(_stats.Level + amount);
        }

        void RaiseChanged() => Changed?.Invoke();

        public void Dispose()
        {
            if (_stats != null)
                _stats.Changed -= RaiseChanged;
        }
    }
}
