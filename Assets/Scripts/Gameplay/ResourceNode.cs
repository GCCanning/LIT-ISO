using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A harvestable world object (tree, rock, etc.) placed at runtime during chunk generation
/// or manually in the scene.
///
/// Attach to a GameObject that also has a SpriteRenderer.
/// A CircleCollider2D (trigger) is added automatically; its radius tracks
/// ResourceNodeDefinition.harvestRadius so IsoInteractionController's OverlapCircle can find it.
///
/// Harvest flow:
///   1. IsoInteractionController calls Harvest(inventory).
///   2. Drops are rolled and added to the inventory.
///   3. Floating "+item" texts appear at the node position.
///   4. A brief colour flash plays on the sprite.
///   5. The node enters its "harvested" visual state and disables interaction.
///   6. After harvestCooldown the node restores itself.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class ResourceNode : MonoBehaviour
{
    [Header("Definition")]
    public ResourceNodeDefinition definition;

    [Header("Feedback")]
    [Tooltip("Colour the sprite flashes to on harvest.")]
    public Color harvestFlashColour = new Color(1f, 0.95f, 0.55f, 1f);
    [Tooltip("Duration of the harvest flash in seconds.")]
    public float harvestFlashDuration = 0.18f;

    [Header("State (read-only at runtime)")]
    [SerializeField] private bool isHarvested = false;

    private SpriteRenderer     sr;
    private CircleCollider2D   col;
    private AudioSource        audioSrc;
    private InteractableHighlight highlight;

    // -------------------------------------------------------------------------
    // Unity lifecycle
    // -------------------------------------------------------------------------

    private void Awake()
    {
        sr = GetComponent<SpriteRenderer>();

        col = GetComponent<CircleCollider2D>();
        if (col == null)
            col = gameObject.AddComponent<CircleCollider2D>();
        col.isTrigger = true;

        // Highlight is optional — IsoHoverController can also add it on first hover
        highlight = GetComponent<InteractableHighlight>();
    }

    private void Start()
    {
        RefreshColliderRadius();
        ApplyVisualState(isHarvested);

        if (isHarvested)
            StartCoroutine(RespawnCoroutine());
    }

    // -------------------------------------------------------------------------
    // Public API
    // -------------------------------------------------------------------------

    /// <summary>
    /// Attempt to harvest this node. Called by IsoInteractionController.
    /// Does nothing if already harvested or if <paramref name="inventory"/> is null.
    /// </summary>
    public void Harvest(PlayerInventory inventory)
    {
        if (isHarvested || definition == null || inventory == null) return;

        // --- Roll drops and collect results for feedback ---
        var results = new List<(ItemDefinition item, int amount)>();
        foreach (var drop in definition.drops)
        {
            if (drop.item == null) continue;
            if (Random.value <= drop.chance)
            {
                int amount = Random.Range(drop.minAmount, drop.maxAmount + 1);
                if (amount > 0)
                {
                    inventory.Add(drop.item, amount);
                    results.Add((drop.item, amount));
                }
            }
        }

        // --- Floating text feedback (one popup per drop, staggered vertically) ---
        for (int i = 0; i < results.Count; i++)
        {
            var (item, amount) = results[i];
            Vector3 popupPos = transform.position + new Vector3(0f, 0.1f * i, 0f);
            WorldFloatingText.Spawn(
                popupPos,
                $"+{item.displayName}  ×{amount}",
                new Color(0.95f, 0.90f, 0.55f, 1f));
        }

        // --- Sound ---
        if (definition.harvestSound != null)
        {
            if (audioSrc == null)
            {
                audioSrc = gameObject.AddComponent<AudioSource>();
                audioSrc.spatialBlend = 0f;
                audioSrc.playOnAwake  = false;
            }
            audioSrc.PlayOneShot(definition.harvestSound, definition.harvestSoundVolume);
        }

        // --- State change ---
        isHarvested = true;
        col.enabled = false;

        // Clear hover outline before hiding the sprite
        if (highlight != null) highlight.SetHovered(false);

        StartCoroutine(HarvestFlashThenSwap());
        StartCoroutine(RespawnCoroutine());
    }

    /// <summary>True when this node has been harvested and is waiting to respawn.</summary>
    public bool IsHarvested => isHarvested;

    /// <summary>
    /// Returns true if <paramref name="worldPos"/> is within the node's harvest radius.
    /// Used by IsoInteractionController to filter candidates.
    /// </summary>
    public bool IsInRange(Vector2 worldPos)
    {
        if (definition == null) return false;
        return Vector2.Distance((Vector2)transform.position, worldPos) <= definition.harvestRadius;
    }

    // -------------------------------------------------------------------------
    // Visuals
    // -------------------------------------------------------------------------

    private void ApplyVisualState(bool harvested)
    {
        if (sr == null) return;

        if (harvested)
        {
            if (definition != null && definition.harvestedSprite != null)
            {
                sr.sprite  = definition.harvestedSprite;
                sr.enabled = true;
            }
            else
            {
                sr.enabled = false;
            }
        }
        else
        {
            sr.enabled = true;
            if (definition != null &&
                definition.nodeSprites != null &&
                definition.nodeSprites.Length > 0)
            {
                int hash = Mathf.Abs(Mathf.RoundToInt(
                    transform.position.x * 73856093 +
                    transform.position.y * 19349663));
                sr.sprite = definition.nodeSprites[hash % definition.nodeSprites.Length];
            }
        }

        // Keep the outline sprite in sync with the main sprite
        if (highlight != null) highlight.SyncSprite();
    }

    private IEnumerator HarvestFlashThenSwap()
    {
        // Brief colour flash
        Color original = sr.color;
        sr.color = harvestFlashColour;
        yield return new WaitForSeconds(harvestFlashDuration);

        // Restore and swap to harvested sprite
        sr.color = original;
        ApplyVisualState(true);
    }

    private void RefreshColliderRadius()
    {
        if (col == null || definition == null) return;
        col.radius = definition.harvestRadius;
    }

    // -------------------------------------------------------------------------
    // Respawn
    // -------------------------------------------------------------------------

    private IEnumerator RespawnCoroutine()
    {
        float cooldown = definition != null ? definition.harvestCooldown : 30f;
        yield return new WaitForSeconds(cooldown);

        isHarvested   = false;
        col.enabled   = true;
        ApplyVisualState(false);
    }
}
