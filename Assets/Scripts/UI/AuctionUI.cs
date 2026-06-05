using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Auction House panel UI.
/// Open with Open() from an Auction House building interactable.
/// Tabs: Browse | My Listings | Post Item
/// </summary>
public class AuctionUI : MonoBehaviour
{
    public static AuctionUI Instance { get; private set; }

    // -------------------------------------------------------------------------
    // Inspector
    // -------------------------------------------------------------------------

    [Header("Panel")]
    public GameObject panelRoot;

    [Header("Tabs")]
    public Button browseTabButton;
    public Button myListingsTabButton;
    public Button postTabButton;

    [Header("Browse Tab")]
    public Transform  browseListParent;
    public GameObject listingRowPrefab;   // Seller, item name, quantity, current bid, time left, Bid button
    public TMP_InputField bidAmountInput;

    [Header("My Listings Tab")]
    public Transform  myListingsParent;
    public GameObject myListingRowPrefab; // Item, qty, current bid, expires, Cancel button

    [Header("Post Tab")]
    public TMP_InputField postItemIdInput;
    public TMP_InputField postQtyInput;
    public TMP_InputField postStartPriceInput;  // in Gold
    public TMP_Dropdown  postDurationDropdown;  // 1 day / 3 days / 7 days
    public Button        postButton;
    public TMP_Text      listingFeeText;

    private readonly List<GameObject> _browseRows    = new();
    private readonly List<GameObject> _myListingRows = new();

    private string _selectedListingId;

    // -------------------------------------------------------------------------
    // Singleton lifecycle
    // -------------------------------------------------------------------------

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;
    }

    private void Start()
    {
        if (panelRoot != null) panelRoot.SetActive(false);

        browseTabButton?.onClick.AddListener(RefreshBrowse);
        myListingsTabButton?.onClick.AddListener(RefreshMyListings);
        postButton?.onClick.AddListener(OnPostClicked);
        postStartPriceInput?.onValueChanged.AddListener(_ => UpdateFeePreview());

        AuctionHouse.OnListingPosted   += _ => RefreshBrowse();
        AuctionHouse.OnListingExpired  += (_, _) => { RefreshBrowse(); RefreshMyListings(); };
    }

    // -------------------------------------------------------------------------
    // Public API
    // -------------------------------------------------------------------------

    public void Open()
    {
        if (panelRoot != null) panelRoot.SetActive(true);
        RefreshBrowse();
    }

    public void Close()
    {
        if (panelRoot != null) panelRoot.SetActive(false);
    }

    // -------------------------------------------------------------------------
    // Browse tab
    // -------------------------------------------------------------------------

    private void RefreshBrowse()
    {
        foreach (var r in _browseRows) Destroy(r);
        _browseRows.Clear();

        var ah = AuctionHouse.Instance;
        if (ah == null || browseListParent == null || listingRowPrefab == null) return;

        foreach (var listing in ah.AllListings)
        {
            var row = Instantiate(listingRowPrefab, browseListParent);
            _browseRows.Add(row);

            var texts   = row.GetComponentsInChildren<TMP_Text>();
            var buttons = row.GetComponentsInChildren<Button>();

            if (texts.Length > 0) texts[0].text = listing.item?.displayName ?? "?";
            if (texts.Length > 1) texts[1].text = $"×{listing.quantity}";
            if (texts.Length > 2) texts[2].text = CurrencySystem.Instance != null
                ? $"{listing.currentBidCopper / CurrencySystem.CopperPerGold}g"
                : $"{listing.currentBidCopper}c";
            if (texts.Length > 3) texts[3].text = listing.sellerId;

            // Bid button
            if (buttons.Length > 0)
            {
                var capturedId = listing.listingId;
                buttons[0].interactable = listing.sellerId != "local_player";
                buttons[0].onClick.AddListener(() => OnBidClicked(capturedId));
            }
        }
    }

    private void OnBidClicked(string listingId)
    {
        _selectedListingId = listingId;
        if (!long.TryParse(bidAmountInput?.text ?? "0", out long gold)) return;
        long copper = gold * CurrencySystem.CopperPerGold;
        AuctionHouse.Instance?.PlaceBid("local_player", listingId, copper);
        RefreshBrowse();
    }

    // -------------------------------------------------------------------------
    // My Listings tab
    // -------------------------------------------------------------------------

    private void RefreshMyListings()
    {
        foreach (var r in _myListingRows) Destroy(r);
        _myListingRows.Clear();

        var ah = AuctionHouse.Instance;
        if (ah == null || myListingsParent == null || myListingRowPrefab == null) return;

        var inv = FindFirstObjectByType<PlayerInventory>();

        foreach (var listing in ah.AllListings)
        {
            if (listing.sellerId != "local_player") continue;

            var row = Instantiate(myListingRowPrefab, myListingsParent);
            _myListingRows.Add(row);

            var texts   = row.GetComponentsInChildren<TMP_Text>();
            var buttons = row.GetComponentsInChildren<Button>();

            if (texts.Length > 0) texts[0].text = $"{listing.item?.displayName} ×{listing.quantity}";
            if (texts.Length > 1) texts[1].text = listing.HasBid ? "Has bids" : "No bids";

            if (buttons.Length > 0)
            {
                var capturedId = listing.listingId;
                buttons[0].interactable = !listing.HasBid;
                buttons[0].GetComponentInChildren<TMP_Text>()?.SetText("Cancel");
                buttons[0].onClick.AddListener(() =>
                {
                    ah.CancelListing(capturedId, "local_player", inv);
                    RefreshMyListings();
                });
            }
        }
    }

    // -------------------------------------------------------------------------
    // Post tab
    // -------------------------------------------------------------------------

    private void OnPostClicked()
    {
        // Simplified: uses itemId string — in practice would be an item picker UI
        // For now, find first matching item in inventory by display name
        if (!long.TryParse(postStartPriceInput?.text ?? "1", out long goldPrice)) return;
        if (!int.TryParse(postQtyInput?.text ?? "1", out int qty)) return;

        int durationDays = AuctionHouse.Instance != null
            ? AuctionHouse.Instance.durationOptions[postDurationDropdown?.value ?? 0]
            : 1;

        // NOTE: Full implementation needs an item picker. This scaffolds the call.
        // Replace nulls with actual ItemDefinition reference from a picker.
        SystemNotifier.Instance?.Announce("Select an item from your inventory to post.", SystemNotifier.MessageType.Info);
    }

    private void UpdateFeePreview()
    {
        if (listingFeeText == null) return;
        if (!long.TryParse(postStartPriceInput?.text ?? "0", out long gold)) return;
        long copper = gold * CurrencySystem.CopperPerGold;
        long fee    = (long)Mathf.Ceil(copper * AuctionHouse.ListingFeePercent);
        long feeGold = fee / CurrencySystem.CopperPerGold;
        listingFeeText.text = $"Listing fee: {feeGold}g (5%)";
    }
}
