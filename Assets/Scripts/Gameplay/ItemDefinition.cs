using UnityEngine;

/// Category tags used by crafting, scoring, and UI filtering.
public enum ItemCategory { Resource, Tool, Consumable, Currency }

/// <summary>
/// Defines a single item type. Create via Assets > Iso World > Item Definition.
/// </summary>
[CreateAssetMenu(menuName = "Iso World/Item Definition", fileName = "ItemDefinition")]
public class ItemDefinition : ScriptableObject
{
    [Tooltip("Stable, lowercase key used as dictionary key. Never change at runtime. e.g. 'wood', 'copper_ore'.")]
    public string itemId = "item";

    [Tooltip("Player-visible name shown in UI.")]
    public string displayName = "Item";

    [Tooltip("Icon shown in the hotbar slot. Assign a 32x32+ Sprite. Leave null for a solid-colour placeholder.")]
    public Sprite icon;

    public ItemCategory category = ItemCategory.Resource;

    [Min(1)]
    [Tooltip("Maximum units in a single inventory stack.")]
    public int maxStack = 999;
}
