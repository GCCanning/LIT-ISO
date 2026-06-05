using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using EthraClone.TrialWeek;

/// <summary>
/// Timed auction system. Requires an Auction House building in a Capital-tier settlement.
///
/// Add as a component on a persistent Managers GameObject.
///
/// Events:
///   OnListingPosted(AuctionListing)
///   OnBidPlaced(AuctionListing, string bidderId, long bidCopper)
///   OnListingExpired(AuctionListing, bool sold)
/// </summary>
public class AuctionHouse : MonoBehaviour
{
    public static AuctionHouse Instance { get; private set; }

    public static event Action<AuctionListing>                    OnListingPosted;
    public static event Action<AuctionListing, string, long>      OnBidPlaced;
    public static event Action<AuctionListing, bool>              OnListingExpired;

    // -------------------------------------------------------------------------
    // Data model
    // -------------------------------------------------------------------------

    [System.Serializable]
    public class AuctionListing
    {
        public string        listingId;
        public string        sellerId;
        public ItemDefinition item;
        public int           quantity;
        public long          startPriceCopper;
        public long          currentBidCopper;
        public string        currentBidderId;
        public float         expiresAtGameTime;   // TrialWeekManager game time seconds
        public int           durationDays;        // 1, 3, or 7 in-game days

        public bool HasBid => currentBidderId != null;
    }

    // -------------------------------------------------------------------------
    // Constants
    // -------------------------------------------------------------------------

    public const float ListingFeePercent   = 0.05f;   // 5% of start price, non-refundable
    public const float FinalSaleTaxPercent = 0.03f;   // 3% of winning bid deducted
    public const int   MaxListingsPerPlayer = 10;

    // -------------------------------------------------------------------------
    // Inspector
    // -------------------------------------------------------------------------

    [Header("Duration (in-game days per option)")]
    public int[] durationOptions = { 1, 3, 7 };

    [Tooltip("In-game seconds per day — should match TrialWeekManager.")]
    [Min(1f)] public float inGameSecondsPerDay = 180f;

    // -------------------------------------------------------------------------
    // Runtime state
    // -------------------------------------------------------------------------

    private readonly List<AuctionListing> listings = new();
    private float _gameTime = 0f;

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

    private void Update()
    {
        _gameTime += Time.deltaTime;
        CheckExpirations();
    }

    // -------------------------------------------------------------------------
    // Public API
    // -------------------------------------------------------------------------

    /// <summary>
    /// Post an item for auction.
    /// Returns null if requirements aren't met (no auction house, already maxed listings, etc.)
    /// </summary>
    public AuctionListing PostListing(string sellerId, ItemDefinition item, int quantity,
                                      long startPriceCopper, int durationDays, PlayerInventory inventory)
    {
        if (!AuctionHouseAvailable())
        {
            SystemNotifier.Instance?.Announce(
                "An Auction House requires a Capital settlement.", SystemNotifier.MessageType.Warning);
            return null;
        }

        if (CountPlayerListings(sellerId) >= MaxListingsPerPlayer)
        {
            SystemNotifier.Instance?.Announce(
                $"You can only have {MaxListingsPerPlayer} active listings.", SystemNotifier.MessageType.Warning);
            return null;
        }

        if (inventory == null || inventory.GetCount(item.itemId) < quantity)
        {
            SystemNotifier.Instance?.Announce("You don't have enough of that item.", SystemNotifier.MessageType.Warning);
            return null;
        }

        // Charge listing fee
        long fee = (long)Mathf.Ceil(startPriceCopper * ListingFeePercent);
        if (CurrencySystem.Instance != null && !CurrencySystem.Instance.SpendCopper(fee))
            return null;

        // Remove item from inventory (it's held by the auction)
        inventory.Remove(item.itemId, quantity);

        int days = Array.IndexOf(durationOptions, durationDays) >= 0 ? durationDays : durationOptions[0];

        var listing = new AuctionListing
        {
            listingId         = $"ah_{listings.Count}_{System.DateTime.UtcNow.Ticks}",
            sellerId          = sellerId,
            item              = item,
            quantity          = quantity,
            startPriceCopper  = startPriceCopper,
            currentBidCopper  = startPriceCopper,
            expiresAtGameTime = _gameTime + days * inGameSecondsPerDay,
            durationDays      = days,
        };

        listings.Add(listing);
        OnListingPosted?.Invoke(listing);
        ActionTracker.Instance?.LogAction(sellerId, "AuctionPosted", item.displayName, (int)(startPriceCopper / 100));

        SystemNotifier.Instance?.Announce(
            $"{item.displayName} ×{quantity} listed for {CopperToGold(startPriceCopper)}.",
            SystemNotifier.MessageType.Info);

        return listing;
    }

    /// <summary>Place a bid on an existing listing. Returns false if bid is too low or listing not found.</summary>
    public bool PlaceBid(string bidderId, string listingId, long bidCopper)
    {
        var l = FindListing(listingId);
        if (l == null || l.expiresAtGameTime <= _gameTime) return false;

        if (bidderId == l.sellerId)
        {
            SystemNotifier.Instance?.Announce("You cannot bid on your own listing.", SystemNotifier.MessageType.Warning);
            return false;
        }

        if (bidCopper <= l.currentBidCopper)
        {
            SystemNotifier.Instance?.Announce(
                $"Bid must exceed current bid of {CopperToGold(l.currentBidCopper)}.",
                SystemNotifier.MessageType.Warning);
            return false;
        }

        // Refund previous bidder
        if (l.HasBid && CurrencySystem.Instance != null)
            CurrencySystem.Instance.AddCopper(l.currentBidCopper);

        // Charge new bidder
        if (CurrencySystem.Instance != null && !CurrencySystem.Instance.SpendCopper(bidCopper))
            return false;

        // Notify previous bidder (if local player)
        if (l.currentBidderId == "local_player")
            SystemNotifier.Instance?.Announce(
                $"Your bid on {l.item.displayName} was outbid!", SystemNotifier.MessageType.Warning);

        l.currentBidderId = bidderId;
        l.currentBidCopper = bidCopper;

        OnBidPlaced?.Invoke(l, bidderId, bidCopper);
        return true;
    }

    /// <summary>Cancel a listing before any bids are placed.</summary>
    public bool CancelListing(string listingId, string requesterId, PlayerInventory inventory)
    {
        var l = FindListing(listingId);
        if (l == null || l.sellerId != requesterId) return false;
        if (l.HasBid) { SystemNotifier.Instance?.Announce("Cannot cancel a listing with active bids.", SystemNotifier.MessageType.Warning); return false; }

        inventory?.Add(l.item, l.quantity);
        listings.Remove(l);
        return true;
    }

    public IReadOnlyList<AuctionListing> AllListings => listings;

    // -------------------------------------------------------------------------
    // Private helpers
    // -------------------------------------------------------------------------

    private void CheckExpirations()
    {
        for (int i = listings.Count - 1; i >= 0; i--)
        {
            var l = listings[i];
            if (_gameTime < l.expiresAtGameTime) continue;

            bool sold = l.HasBid;
            if (sold)
            {
                // Award seller (minus tax)
                long tax      = (long)Mathf.Ceil(l.currentBidCopper * FinalSaleTaxPercent);
                long payout   = l.currentBidCopper - tax;
                if (CurrencySystem.Instance != null)
                    CurrencySystem.Instance.AddCopper(payout);

                // Give item to buyer — if local player just add to inventory
                var inv = FindFirstObjectByType<PlayerInventory>();
                if (l.currentBidderId == "local_player" && inv != null)
                {
                    inv.Add(l.item, l.quantity);
                    SystemNotifier.Instance?.Announce(
                        $"You won {l.item.displayName} ×{l.quantity} for {CopperToGold(l.currentBidCopper)}!",
                        SystemNotifier.MessageType.Achievement);
                }
            }
            else
            {
                // Return item to seller if local player
                if (l.sellerId == "local_player")
                {
                    var inv = FindFirstObjectByType<PlayerInventory>();
                    inv?.Add(l.item, l.quantity);
                    SystemNotifier.Instance?.Announce(
                        $"Your listing for {l.item.displayName} expired unsold.",
                        SystemNotifier.MessageType.Info);
                }
            }

            OnListingExpired?.Invoke(l, sold);
            listings.RemoveAt(i);
        }
    }

    private bool AuctionHouseAvailable()
    {
        if (TownManager.Instance == null) return false;
        foreach (var s in TownManager.Instance.AllSettlements)
            if (s.tier >= 5 && s.HasBuilding("auction_house")) return true;
        return false;
    }

    private int CountPlayerListings(string playerId)
    {
        int count = 0;
        foreach (var l in listings) if (l.sellerId == playerId) count++;
        return count;
    }

    private AuctionListing FindListing(string id)
    {
        foreach (var l in listings) if (l.listingId == id) return l;
        return null;
    }

    private static string CopperToGold(long copper)
    {
        if (CurrencySystem.Instance == null) return $"{copper}c";
        long gold   = copper / CurrencySystem.CopperPerGold;
        long silver = (copper % CurrencySystem.CopperPerGold) / CurrencySystem.CopperPerSilver;
        long copRem = copper % CurrencySystem.CopperPerSilver;
        if (gold > 0)   return $"{gold}g {silver}s";
        if (silver > 0) return $"{silver}s {copRem}c";
        return $"{copRem}c";
    }
}
