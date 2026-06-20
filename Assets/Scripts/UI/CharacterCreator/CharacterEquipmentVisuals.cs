using UnityEngine;

namespace LitIso.CharacterCreator
{
    /// <summary>
    /// Bridges in-game item drops to visible accessory layers (v2, LPC
    /// wardrobe). Attach next to a LayeredCharacterAnimator. When the
    /// inventory/equipment system equips an item whose definition names a
    /// catalog accessory (e.g. "lpc/cape_solid"), call <see cref="Equip"/>;
    /// the character re-bakes with the layer on.
    ///
    /// Inventory hookup is intentionally a plain public API: the Foundation
    /// equipment flow can call these two methods from wherever equip/unequip
    /// happens.
    /// </summary>
    [RequireComponent(typeof(LayeredCharacterAnimator))]
    public class CharacterEquipmentVisuals : MonoBehaviour
    {
        LayeredCharacterAnimator _anim;

        void Awake() => _anim = GetComponent<LayeredCharacterAnimator>();

        /// <param name="itemId">Catalog item id, e.g. "lpc/cape_solid".</param>
        /// <param name="variant">Variant name, e.g. "red". Null/invalid = first variant.</param>
        public void Equip(string itemId, string variant = null)
        {
            var cat = CharacterLayerCatalog.Instance;
            var def = cat.Find(itemId);
            if (def == null)
            {
                Debug.LogWarning($"[EquipmentVisuals] Unknown item '{itemId}'");
                return;
            }
            var appearance = _anim.Appearance ?? LayeredAppearance.LoadOrDefault();
            appearance.Equip(itemId, def.SafeVariant(variant), def.equipSlot);
            appearance.Save();
            _anim.Apply(appearance);
        }

        public void Unequip(string itemId)
        {
            var appearance = _anim.Appearance ?? LayeredAppearance.LoadOrDefault();
            appearance.Unequip(itemId);
            appearance.Save();
            _anim.Apply(appearance);
        }

        public bool IsEquipped(string itemId) =>
            (_anim.Appearance ?? LayeredAppearance.LoadOrDefault()).HasEquipped(itemId);
    }
}
