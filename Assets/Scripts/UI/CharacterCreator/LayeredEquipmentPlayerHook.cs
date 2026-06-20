using IsoCore.Foundation;
using UnityEngine;

namespace LitIso.CharacterCreator
{
    /// <summary>
    /// Phase 1 equipment->visual bridge (Assembly-CSharp side). Subscribes to the player's
    /// <see cref="EquipmentLoadout.EquipmentChanged"/> event and re-bakes the LPC wardrobe via
    /// <see cref="CharacterEquipmentVisuals"/> so equipping gear (which also applies its
    /// StatBonuses, Foundation-side) is visible on the character.
    ///
    /// Lives in Assembly-CSharp; hooks in via <see cref="FoundationBootstrap.Ready"/>. The
    /// Foundation EquipmentLoadout never references the wardrobe — this is the seam.
    /// </summary>
    [DefaultExecutionOrder(-800)]
    public static class LayeredEquipmentPlayerHook
    {
        static FoundationBootstrap _bootstrap;
        static CharacterEquipmentVisuals _visuals;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Register()
        {
            FoundationBootstrap.Ready += OnReady;
        }

        static void OnReady(FoundationBootstrap bootstrap)
        {
            if (!CharacterLayerCatalog.Available) return;
            _bootstrap = bootstrap;
            _visuals = null;

            if (bootstrap.Equipment != null)
            {
                bootstrap.Equipment.EquipmentChanged -= OnEquipmentChanged;
                bootstrap.Equipment.EquipmentChanged += OnEquipmentChanged;
            }
        }

        static void OnEquipmentChanged(EquipSlot slot, ItemStack stack, IsoCore.Foundation.ItemDefinition def)
        {
            var visuals = ResolveVisuals();
            if (visuals == null) return;

            // Empty stack / null def => the slot was cleared. We can't map an EquipSlot back to a
            // single catalog id on unequip, so unequip by the item's lpcCatalogId when known.
            if (def == null || string.IsNullOrEmpty(def.lpcCatalogId))
                return;

            if (stack.IsEmpty)
                visuals.Unequip(def.lpcCatalogId);
            else
                visuals.Equip(def.lpcCatalogId, def.lpcVariant);
        }

        static CharacterEquipmentVisuals ResolveVisuals()
        {
            if (_visuals != null) return _visuals;
            var player = _bootstrap != null ? _bootstrap.Player : null;
            if (player == null) return null;
            var go = player.gameObject;

            // Requires a LayeredCharacterAnimator (added by LayeredCharacterPlayerHook).
            if (go.GetComponent<LayeredCharacterAnimator>() == null) return null;
            _visuals = go.GetComponent<CharacterEquipmentVisuals>()
                       ?? go.AddComponent<CharacterEquipmentVisuals>();
            return _visuals;
        }
    }
}
