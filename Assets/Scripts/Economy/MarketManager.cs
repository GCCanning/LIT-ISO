using System;
using System.Collections.Generic;
using UnityEngine;
using EthraClone.TrialWeek;

/// <summary>
/// Peer-to-peer market for direct item trades between players within the same settlement.
/// Unlike the Auction House, listings are instant (no timer) and require both players present.
///
/// Add as a component on a persistent Managers GameObject.
///
/// Events:
///   OnListingAdded(MarketListing)
///   OnListingPurchased(MarketListing, string buyerId)
///   OnListingRemoved(MarketListing)
/// </summary>
public class MarketManager : MonoBehaviour
{
    public static MarketManager Instance { get; private set; }

    public static event Action<MarketListing>          OnListingAdded;
    public static event Action<MarketListing, string>  OnListingPurchased;
    public static event Action<MarketListing>          OnListingRemoved;

    // -------------------------------------------------------------------------
    // Data model
    // -------------------------------------------------------------------------

    [System.Serializable]
    public class MarketListing
    {
        public string        listingId;
        public string        sellerId;
        public string        sellerName;
        public ItemDefinition item;
        public int           quantity;
        public long          pricePerUnitCopper;

        public long TotalPriceCopper => pricePerUnitCopper * quantity;
    }

    // -------------------------------------------------------------------------
    // Inspector
    // -------------------------------------------------------------------------

    [Header("Limits")]
    [Min(1)] public int maxListingsPerSeller = 10;

    // -------------------------------------------------------------------------
    // Runtime state
    // -------------------------------------------------------------------------

    private readonly List<MarketListing> listings = new();

    // -------------------------------------------------------------------------
    // Singleton lifecycle
    // -------------------------------------------------------------------------

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void OnDestroy() { if (Instance == this) Instance = null; }

    // -------------------------------------------------------------------------
    // Public API
    // -------------------------------------------------------------------------

    /// <summary>Post an item for sale at a flat per-unit price.</summary>
    public MarketListing PostListing(string sellerId, string sellerName,
                                     ItemDefinition item, int quantity,
                                     long pricePerUnitCopper, PlayerInventory inventory)
    {
        if (item == null || quantity <= 0 || pricePerUnitCopper <= 0) return null;

        if (CountSellerListings(sellerId) >= maxListingsPerSeller)
        {
            SystemNotifier.Instance?.Announce(
                $"Maximum {maxListingsPerSeller} listings reached.", SystemNotifier.MessageType.Warning);
            return null;
        }

        if (inventory == null || inventory.GetCount(item.itemId) < quantity)
        {
            SystemNotifier.Instance?.Announce("Not enough items.", SystemNotifier.MessageType.Warning);
            return null;
        }

        inventory.Remove(item.itemId, quantity);

        var listing = new MarketListing
        {
            listingId           = $"mkt_{listings.Count}_{DateTime.UtcNow.Ticks}",
            sellerId            = sellerId,
            sellerName          = sellerName,
            item                = item,
            quantity            = quantity,
            pricePerUnitCopper  = pricePerUnitCopper,
        };

        listings.Add(listing);
        OnListingAdded?.Invoke(listing);
        ActionTracker.Instance?.LogAction(sellerId, "MarketListed", item.displayName, (int)(pricePerUnitCopper / 100));
        return listing;
    }

    /// <summary>Buy a listing outright. Returns false if not enough currency.</summary>
    public bool Purchase(string listingId, string buyerId, PlayerInventory buyerInventory)
    {
        var l = FindListing(listingId);
        if (l == null)
        {
            SystemNotifier.Instance?.Announce("Listing no longer available.", SystemNotifier.MessageType.Warning);
            return false;
        }

        if (buyerId == l.sellerId)
        {
            SystemNotifier.Instance?.Announce("You cannot buy your own listing.", SystemNotifier.MessageType.Warning);
            return false;
        }

        if (CurrencySystem.Instance != null && !CurrencySystem.Instance.SpendCopper(l.TotalPriceCopper))
            return false;

        // Give seller payment (if local player is seller)
        if (l.sellerId == "local_player" && CurrencySystem.Instance != null)
            CurrencySystem.Instance.AddCopper(l.TotalPriceCopper);

        buyerInventory?.Add(l.item, l.quantity);

        string priceStr = CurrencySystem.Instance != null
            ? $"{l.pricePerUnitCopper / CurrencySystem.CopperPerGold}g each"
            : $"{l.pricePerUnitCopper}c each";

        SystemNotifier.Instance?.Announce(
            $"Purchased {l.item.displayName} ×{l.quantity} from {l.sellerName} for {priceStr}.",
            SystemNotifier.MessageType.Info);

        OnListingPurchased?.Invoke(l, buyerId);
        ActionTracker.Instance?.LogAction(buyerId, "MarketPurchased", l.item.displayName, (int)(l.TotalPriceCopper / 100));

        listings.Remove(l);
        return true;
    }

    /// <summary>Remove a listing and return the item to the seller.</summary>
    public bool RemoveListing(string listingId, string requesterId, PlayerInventory sellerInventory)
    {
        var l = FindListing(listingId);
        if (l == null || l.sellerId != requesterId) return false;

        sellerInventory?.Add(l.item, l.quantity);
        listings.Remove(l);
        OnListingRemoved?.Invoke(l);
        return true;
    }

    /// <summary>All current market listings.</summary>
    public IReadOnlyList<MarketListing> AllListings => listings;

    /// <summary>Listings by a specific seller.</summary>
    public List<MarketListing> GetSellerListings(string sellerId)
    {
        var result = new List<MarketListing>();
        foreach (var l in listings) if (l.sellerId == sellerId) result.Add(l);
        return result;
    }

    // -------------------------------------------------------------------------
    // Private helpers
    // -------------------------------------------------------------------------

    private int CountSellerListings(string id)
    {
        int count = 0;
        foreach (var l in listings) if (l.sellerId == id) count++;
        return count;
    }

    private MarketListing FindListing(string id)
    {
        foreach (var l in listings) if (l.listingId == id) return l;
        return null;
    }
}
