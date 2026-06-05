using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Tracks item stack counts for the local player.
/// Add as a component on the Player GameObject.
///
/// Events:
///   OnStackChanged(ItemDefinition item, int newTotal, int delta)
///     Fired after every Add/Remove. delta is positive for additions, negative for removals.
/// </summary>
public class PlayerInventory : MonoBehaviour
{
    public static PlayerInventory Instance { get; private set; }

    // itemId → current stack count
    private readonly Dictionary<string, int> stacks = new Dictionary<string, int>();

    // Ordered list of every item type ever seen (for hotbar slot assignment)
    private readonly List<ItemDefinition> knownItems = new List<ItemDefinition>();

    // Lookup by id for O(1) definition access
    private readonly Dictionary<string, ItemDefinition> definitionById =
        new Dictionary<string, ItemDefinition>();

    /// <summary>
    /// Fired when an item stack changes.
    /// Args: definition, new total count, delta (positive = added, negative = removed).
    /// </summary>
    public event Action<ItemDefinition, int, int> OnStackChanged;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // -------------------------------------------------------------------------
    // Public API
    // -------------------------------------------------------------------------

    /// <summary>Add <paramref name="amount"/> units of <paramref name="item"/>.</summary>
    public void Add(ItemDefinition item, int amount)
    {
        if (item == null || amount <= 0) return;

        RegisterDefinition(item);

        stacks.TryGetValue(item.itemId, out int current);
        int newCount = Mathf.Min(current + amount, item.maxStack);
        int actualDelta = newCount - current;
        if (actualDelta <= 0) return;   // Already at max stack

        stacks[item.itemId] = newCount;
        OnStackChanged?.Invoke(item, newCount, actualDelta);
    }

    /// <summary>
    /// Remove <paramref name="amount"/> units. Returns false and does nothing if insufficient.
    /// </summary>
    public bool Remove(string itemId, int amount)
    {
        if (amount <= 0) return false;
        if (!stacks.TryGetValue(itemId, out int current) || current < amount) return false;

        int newCount = current - amount;
        stacks[itemId] = newCount;

        if (definitionById.TryGetValue(itemId, out var def))
            OnStackChanged?.Invoke(def, newCount, -amount);

        return true;
    }

    /// <summary>Returns the current stack count for <paramref name="itemId"/>, or 0.</summary>
    public int GetCount(string itemId)
    {
        stacks.TryGetValue(itemId, out int count);
        return count;
    }

    /// <summary>
    /// Ordered list of every item type ever seen — used by HotbarUI to assign slots
    /// in discovery order.
    /// </summary>
    public IReadOnlyList<ItemDefinition> KnownItems => knownItems;

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private void RegisterDefinition(ItemDefinition item)
    {
        if (!definitionById.ContainsKey(item.itemId))
        {
            definitionById[item.itemId] = item;
            knownItems.Add(item);
        }
    }
}
