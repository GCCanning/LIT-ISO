using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Displays the 4 equipped spell slots with cooldown overlays and mana indicators.
///
/// Reads from SpellCaster.Instance each frame.
/// Wire slotRoots[0..3] and their children in the inspector.
/// </summary>
public class SpellHotbarUI : MonoBehaviour
{
    // -------------------------------------------------------------------------
    // Inspector
    // -------------------------------------------------------------------------

    [System.Serializable]
    public class SpellSlotUI
    {
        public GameObject root;
        public Image      iconImage;
        public Image      cooldownOverlay;    // Radial fill — set Image type to Filled, Method Radial360
        public TMP_Text   cooldownText;
        public TMP_Text   keyLabel;           // "1", "2", "3", "4"
        public TMP_Text   manaCostText;
        public Image      backgroundImage;
        public Color      readyColour   = new Color(1f, 1f, 1f, 1f);
        public Color      onCooldown    = new Color(0.4f, 0.4f, 0.4f, 1f);
        public Color      noManaColour  = new Color(0.4f, 0.4f, 0.9f, 0.7f);
    }

    [Header("Slots (0=slot1, 3=slot4)")]
    public SpellSlotUI[] slots = new SpellSlotUI[4];

    [Header("Empty Slot")]
    public Sprite emptySlotSprite;

    // -------------------------------------------------------------------------
    // Unity lifecycle
    // -------------------------------------------------------------------------

    private void Start()
    {
        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i].keyLabel != null)
                slots[i].keyLabel.text = (i + 1).ToString();
            if (slots[i].cooldownOverlay != null)
                slots[i].cooldownOverlay.fillAmount = 0f;
        }

        ClassSystem.OnClassAssigned += _ => RefreshAll();
    }

    private void OnDestroy()
    {
        ClassSystem.OnClassAssigned -= _ => RefreshAll();
    }

    private void Update()
    {
        RefreshAll();
    }

    // -------------------------------------------------------------------------
    // Private helpers
    // -------------------------------------------------------------------------

    private void RefreshAll()
    {
        var caster = SpellCaster.Instance;
        var mana   = PlayerMana.Instance;

        for (int i = 0; i < slots.Length; i++)
        {
            var slot = slots[i];
            if (slot == null || slot.root == null) continue;

            SpellDefinition spell = caster != null ? caster.GetEquippedSpell(i) : null;

            if (spell == null)
            {
                SetEmpty(slot);
                continue;
            }

            // Icon
            if (slot.iconImage != null)
                slot.iconImage.sprite = spell.icon != null ? spell.icon : emptySlotSprite;

            // Mana cost
            if (slot.manaCostText != null)
                slot.manaCostText.text = spell.manaCost > 0 ? $"{spell.manaCost:F0}" : "";

            // Cooldown overlay
            float cdRemain = caster != null ? caster.GetCooldownRemaining(i) : 0f;
            float cdTotal  = caster != null ? caster.GetCooldownTotal(i) : 1f;
            float fill     = cdTotal > 0f ? cdRemain / cdTotal : 0f;

            if (slot.cooldownOverlay != null)
                slot.cooldownOverlay.fillAmount = fill;

            if (slot.cooldownText != null)
                slot.cooldownText.text = cdRemain > 0.05f ? $"{cdRemain:F1}s" : "";

            // Colour
            bool hasMana = mana == null || mana.CurrentMana >= spell.manaCost;
            bool onCD    = cdRemain > 0.05f;

            if (slot.iconImage != null)
                slot.iconImage.color = onCD ? slot.onCooldown : (hasMana ? slot.readyColour : slot.noManaColour);
        }
    }

    private void SetEmpty(SpellSlotUI slot)
    {
        if (slot.iconImage != null)
        {
            slot.iconImage.sprite = emptySlotSprite;
            slot.iconImage.color  = new Color(1f, 1f, 1f, 0.3f);
        }
        if (slot.cooldownOverlay != null) slot.cooldownOverlay.fillAmount = 0f;
        if (slot.cooldownText != null)    slot.cooldownText.text = "";
        if (slot.manaCostText != null)    slot.manaCostText.text = "";
    }
}
