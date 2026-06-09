using System;
using IsoCore.Foundation;
using UnityEngine;

namespace LitIso.UI.InGame
{
    /// <summary>
    /// Foundation-backed character sheet adapter. Legacy singleton fallbacks were
    /// intentionally removed so the canonical UI depends on Foundation stats only.
    /// </summary>
    public sealed class FoundationCharacterSheetAdapter : ICharacterSheetViewModel, IDisposable
    {
        readonly FoundationPlayerStats _stats;
        readonly string _characterName;

        public event Action Changed;

        public FoundationCharacterSheetAdapter(FoundationPlayerStats stats = null, string characterName = null)
        {
            _stats = stats;
            _characterName = string.IsNullOrWhiteSpace(characterName) ? "Adventurer" : characterName.Trim();
            if (_stats != null)
                _stats.Changed += OnChanged;
        }

        public void Dispose()
        {
            if (_stats != null)
                _stats.Changed -= OnChanged;
        }

        void OnChanged() => Changed?.Invoke();

        public string CharacterName => _characterName;
        public string ClassName => _stats != null ? _stats.Class : "Greenhand";
        public string TitleName => _stats != null ? _stats.Title : "Newcomer";
        public Sprite Portrait => Resources.Load<Sprite>("UI/InGame/system_portrait");
        public int Level => _stats != null ? _stats.Level : 1;
        public float Health01 => _stats != null ? _stats.Health01 : 1f;
        public float Mana01 => _stats != null ? _stats.Mana01 : 1f;
        public float Xp01 => _stats != null ? _stats.Xp01 : 0f;

        public CharacterStats Stats
        {
            get
            {
                if (_stats == null)
                    return new CharacterStats { str = 5, dex = 5, intel = 5, vit = 5, def = 5, luck = 5 };

                return new CharacterStats
                {
                    str = _stats.STR,
                    dex = _stats.DEX,
                    intel = _stats.INT,
                    vit = _stats.VIT,
                    def = _stats.DEF,
                    luck = _stats.LUCK,
                };
            }
        }
    }
}
