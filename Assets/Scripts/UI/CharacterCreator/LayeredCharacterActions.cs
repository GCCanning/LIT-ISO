using System;
using IsoCore.Foundation;
using UnityEngine;

namespace LitIso.CharacterCreator
{
    /// <summary>
    /// Drives the five one-shot LPC action animations (cast / thrust / slash /
    /// shoot / hurt) on a <see cref="LayeredCharacterAnimator"/> from real
    /// gameplay events, so every baked animation is actually reachable in play:
    ///
    ///  • <b>Basic attack</b> — press <see cref="attackKey"/> (default E): plays a
    ///    weapon-aware action chosen from the equipped weapon (bow→shoot,
    ///    spear/polearm→thrust, staff/wand→cast, sword/axe/blunt or unarmed→slash).
    ///  • <b>Spell cast</b> — <see cref="SpellCaster.OnSpellCast"/> (keys 1–4):
    ///    Projectile spells play "shoot", everything else plays "cast".
    ///  • <b>Hurt</b> — <see cref="PlayerHealth.OnHealthChanged"/>: any health
    ///    decrease plays "hurt".
    ///
    /// Movement is NOT locked while an action plays (owner choice) — the animator
    /// already lets movement keep updating facing during a one-shot.
    ///
    /// Attached to the player automatically by <see cref="LayeredCharacterPlayerHook"/>.
    /// Nothing here deals damage — it is purely the visual layer; hook the same
    /// events from gameplay if/when basic-attack damage is added.
    ///
    /// NOTE: the default attack key E currently also serves as the interact key
    /// and the ability-wheel "E" slot. Attack is suppressed while a UI modal is
    /// open (incl. the hold-X wheel), but it will still co-fire with a contextual
    /// interact. Change <see cref="attackKey"/> if that overlap is unwanted.
    /// </summary>
    [RequireComponent(typeof(LayeredCharacterAnimator))]
    public class LayeredCharacterActions : MonoBehaviour
    {
        [Tooltip("Key for the weapon-aware basic attack animation.")]
        public KeyCode attackKey = KeyCode.E;

        LayeredCharacterAnimator _anim;
        bool _healthSubscribed;
        int _lastHealth = int.MinValue;

        void Awake() => _anim = GetComponent<LayeredCharacterAnimator>();

        void OnEnable()
        {
            SpellCaster.OnSpellCast += OnSpellCast;
            TrySubscribeHealth();
        }

        void OnDisable()
        {
            SpellCaster.OnSpellCast -= OnSpellCast;
            if (_healthSubscribed && PlayerHealth.Instance != null)
                PlayerHealth.Instance.OnHealthChanged -= OnHealthChanged;
            _healthSubscribed = false;
        }

        void TrySubscribeHealth()
        {
            if (_healthSubscribed || PlayerHealth.Instance == null) return;
            PlayerHealth.Instance.OnHealthChanged += OnHealthChanged;
            _lastHealth = PlayerHealth.Instance.CurrentHealth;
            _healthSubscribed = true;
        }

        void Update()
        {
            // PlayerHealth.Instance may not exist yet at OnEnable; keep trying.
            if (!_healthSubscribed) TrySubscribeHealth();

            if (_anim == null || _anim.IsPlayingOneShot) return;

            // Don't attack through UI (also covers the hold-X ability wheel).
            var ui = FoundationUiCoordinator.Active;
            if (ui != null && ui.BlocksWorldInput) return;

            if (Input.GetKeyDown(attackKey))
                _anim.PlayOneShot(WeaponAttackAnim());
        }

        /// <summary>Pick the attack animation that fits the equipped weapon.</summary>
        string WeaponAttackAnim()
        {
            var ap = _anim.Appearance;
            if (ap?.equipped != null)
            {
                var cat = CharacterLayerCatalog.Instance;
                foreach (var entry in ap.equipped)
                {
                    var def = cat.Find(entry.itemId);
                    if (def == null || def.slot != "weapon") continue;
                    string id = entry.itemId;
                    if (id.Contains("bow") || id.Contains("ranged")) return "shoot";
                    if (id.Contains("polearm") || id.Contains("spear")) return "thrust";
                    if (id.Contains("magic") || id.Contains("staff") || id.Contains("wand")) return "spellcast";
                    return "slash"; // sword / dagger / blunt / axe / etc.
                }
            }
            return "slash"; // unarmed
        }

        void OnSpellCast(int slotIndex)
        {
            if (_anim == null || slotIndex < 0) return; // <0 is a sentinel (e.g. no mana)
            string anim = "spellcast";
            var sc = SpellCaster.Instance;
            if (sc != null)
            {
                var spell = sc.GetEquippedSpell(slotIndex);
                if (spell != null && spell.delivery == SpellDefinition.DeliveryType.Projectile)
                    anim = "shoot";
            }
            _anim.PlayOneShot(anim);
        }

        void OnHealthChanged(int current, int max)
        {
            if (_anim != null && _lastHealth != int.MinValue && current < _lastHealth)
                _anim.PlayOneShot("hurt");
            _lastHealth = current;
        }
    }
}
